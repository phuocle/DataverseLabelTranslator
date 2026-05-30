(function (TranslationPackageService, undefined) {
    "use strict";

    var CRM_TRANSLATIONS_FILE = "CrmTranslations.xml";
    var SPREADSHEET_NS = "urn:schemas-microsoft-com:office:spreadsheet";

    function getApiUrl() {
        return WebApiClient.GetApiUrl({ apiVersion: "9.2" });
    }

    function getAttr(node, name) {
        if (!node || !node.getAttribute) {
            return "";
        }

        return node.getAttribute(name) ||
            node.getAttribute("ss:" + name) ||
            node.getAttributeNS(SPREADSHEET_NS, name) ||
            "";
    }

    function getNodeText(node) {
        return node ? (node.textContent || "") : "";
    }

    function getElements(parent, localName) {
        var result = [];
        var nodes = parent ? parent.getElementsByTagName("*") : [];

        for (var i = 0; i < nodes.length; i++) {
            if (nodes[i].localName === localName || nodes[i].nodeName === localName || nodes[i].nodeName === "ss:" + localName) {
                result.push(nodes[i]);
            }
        }

        return result;
    }

    function getDirectChildren(parent, localName) {
        var result = [];
        var children = parent ? parent.childNodes : [];

        for (var i = 0; i < children.length; i++) {
            var child = children[i];
            if (child.nodeType === 1 && (child.localName === localName || child.nodeName === localName || child.nodeName === "ss:" + localName)) {
                result.push(child);
            }
        }

        return result;
    }

    function parseXml(xmlText) {
        var doc = new DOMParser().parseFromString(xmlText, "application/xml");

        if (doc.getElementsByTagName("parsererror").length > 0) {
            throw new Error("CrmTranslations.xml is not valid XML.");
        }

        return doc;
    }

    function serializeXml(doc) {
        return new XMLSerializer().serializeToString(doc);
    }

    function parseWorksheetRows(worksheet) {
        var table = getDirectChildren(worksheet, "Table")[0] || getElements(worksheet, "Table")[0];
        var rowNodes = table ? getDirectChildren(table, "Row") : [];
        var rows = [];

        for (var r = 0; r < rowNodes.length; r++) {
            var rowNode = rowNodes[r];
            var cellNodes = getDirectChildren(rowNode, "Cell");
            var cells = [];
            var columnIndex = 0;

            for (var c = 0; c < cellNodes.length; c++) {
                var cellNode = cellNodes[c];
                var explicitIndex = parseInt(getAttr(cellNode, "Index"), 10);
                if (!isNaN(explicitIndex) && explicitIndex > 0) {
                    columnIndex = explicitIndex - 1;
                }

                var dataNode = getDirectChildren(cellNode, "Data")[0];
                cells[columnIndex] = {
                    node: cellNode,
                    dataNode: dataNode,
                    text: getNodeText(dataNode)
                };
                columnIndex++;
            }

            rows.push({
                index: r,
                node: rowNode,
                cells: cells
            });
        }

        return rows;
    }

    function parseWorksheets(xmlText) {
        var doc = parseXml(xmlText);
        var worksheets = getElements(doc, "Worksheet").map(function (worksheetNode) {
            return {
                node: worksheetNode,
                name: getAttr(worksheetNode, "Name"),
                rows: parseWorksheetRows(worksheetNode)
            };
        });

        return {
            doc: doc,
            worksheets: worksheets
        };
    }

    function ensureCell(row, columnIndex) {
        var cells = row.cells;
        var cell = cells[columnIndex];
        var doc = row.node.ownerDocument;
        var insertBefore = null;
        var insertPosition = cells.length;

        if (cell && cell.node) {
            if (!cell.dataNode) {
                cell.dataNode = doc.createElementNS(SPREADSHEET_NS, "Data");
                cell.dataNode.setAttributeNS(SPREADSHEET_NS, "ss:Type", "String");
                cell.node.appendChild(cell.dataNode);
            }
            return cell;
        }

        for (var i = columnIndex + 1; i < cells.length; i++) {
            if (cells[i] && cells[i].node) {
                insertBefore = cells[i].node;
                insertPosition = i;
                break;
            }
        }

        var cellNode = doc.createElementNS(SPREADSHEET_NS, "Cell");
        cellNode.setAttributeNS(SPREADSHEET_NS, "ss:Index", String(columnIndex + 1));
        var dataNode = doc.createElementNS(SPREADSHEET_NS, "Data");
        dataNode.setAttributeNS(SPREADSHEET_NS, "ss:Type", "String");
        cellNode.appendChild(dataNode);

        if (insertBefore) {
            row.node.insertBefore(cellNode, insertBefore);
        } else {
            row.node.appendChild(cellNode);
        }

        cell = {
            node: cellNode,
            dataNode: dataNode,
            text: ""
        };
        cells[columnIndex] = cell;

        for (var j = columnIndex + 1; j < insertPosition; j++) {
            if (cells[j] && cells[j].node) {
                cells[j].node.setAttributeNS(SPREADSHEET_NS, "ss:Index", String(j + 1));
            }
        }

        return cell;
    }

    function setCellText(row, columnIndex, value) {
        var cell = ensureCell(row, columnIndex);
        cell.dataNode.textContent = value || "";
        cell.text = value || "";
    }

    function exportTranslations(solutionUniqueName) {
        if (!solutionUniqueName) {
            return Promise.reject(new Error("Solution unique name is required for translation export."));
        }

        return WebApiClient.SendRequest(
            "POST",
            getApiUrl() + "solutions/Microsoft.Dynamics.CRM.ExportTranslation",
            { SolutionName: solutionUniqueName }
        )
        .then(function (response) {
            var file = response && response.ExportTranslationFile;
            if (!file) {
                throw new Error("ExportTranslation did not return ExportTranslationFile.");
            }

            return file;
        });
    }

    function importTranslations(translationFileBase64, importJobId) {
        if (!translationFileBase64) {
            return Promise.reject(new Error("Translation file content is required for import."));
        }

        return WebApiClient.SendRequest(
            "POST",
            getApiUrl() + "ImportTranslation()",
            {
                TranslationFile: translationFileBase64,
                ImportJobId: importJobId || createGuid()
            }
        );
    }

    function createGuid() {
        if (window.crypto && window.crypto.getRandomValues) {
            var bytes = new Uint8Array(16);
            window.crypto.getRandomValues(bytes);
            bytes[6] = (bytes[6] & 0x0f) | 0x40;
            bytes[8] = (bytes[8] & 0x3f) | 0x80;

            var hex = [];
            for (var i = 0; i < bytes.length; i++) {
                hex.push((bytes[i] + 0x100).toString(16).substr(1));
            }

            return [
                hex.slice(0, 4).join(""),
                hex.slice(4, 6).join(""),
                hex.slice(6, 8).join(""),
                hex.slice(8, 10).join(""),
                hex.slice(10, 16).join("")
            ].join("-");
        }

        return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, function (c) {
            var r = Math.random() * 16 | 0;
            var v = c === "x" ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    function loadPackage(base64) {
        return JSZip.loadAsync(base64, { base64: true })
        .then(function (zip) {
            var file = zip.file(CRM_TRANSLATIONS_FILE);
            if (!file) {
                throw new Error(CRM_TRANSLATIONS_FILE + " was not found in the translation package.");
            }

            return file.async("string")
            .then(function (xmlText) {
                return {
                    zip: zip,
                    xmlText: xmlText,
                    workbook: parseWorksheets(xmlText)
                };
            });
        });
    }

    function writePackage(packageData) {
        var xmlText = serializeXml(packageData.workbook.doc);
        packageData.zip.file(CRM_TRANSLATIONS_FILE, xmlText);

        return packageData.zip.generateAsync({
            type: "base64",
            compression: "DEFLATE"
        });
    }

    TranslationPackageService.ExportTranslations = exportTranslations;
    TranslationPackageService.ImportTranslations = importTranslations;
    TranslationPackageService.LoadPackage = loadPackage;
    TranslationPackageService.WritePackage = writePackage;
    TranslationPackageService.SetCellText = setCellText;
    TranslationPackageService.GetAttr = getAttr;

}(window.TranslationPackageService = window.TranslationPackageService || {}));
