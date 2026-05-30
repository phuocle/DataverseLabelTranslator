(function (EntityMessageHandler, undefined) {
    "use strict";

    var DISPLAY_STRINGS_WORKSHEET = "Display Strings";
    var state = null;

    function normalize(value) {
        return String(value || "").trim().toLowerCase();
    }

    function normalizeKey(value) {
        return normalize(value).replace(/[^a-z0-9]/g, "");
    }

    function getCellText(row, index) {
        var cell = row && row.cells ? row.cells[index] : null;
        return cell ? (cell.text || "") : "";
    }

    function getSolutionUniqueName(solutionId) {
        if (!solutionId) {
            return Promise.reject(new Error("Please select a solution before loading entity messages."));
        }

        return WebApiClient.Retrieve({
            entityName: "solution",
            entityId: solutionId,
            queryParams: "?$select=uniquename"
        })
        .then(function (solution) {
            if (!solution || !solution.uniquename) {
                throw new Error("Could not resolve the selected solution unique name.");
            }

            return solution.uniquename;
        });
    }

    function getSelectedEntityInfo() {
        var logicalName = XrmTranslator.GetEntity();

        if (!logicalName || logicalName === "none") {
            throw new Error("Please select an entity before loading entity messages.");
        }

        return WebApiClient.Retrieve({
            entityName: "EntityDefinition",
            entityId: XrmTranslator.GetEntityId(),
            queryParams: "?$select=LogicalName,ObjectTypeCode,SchemaName,DisplayName,DisplayCollectionName,IsCustomEntity"
        })
        .then(function (entity) {
            return {
                logicalName: logicalName,
                metadataId: XrmTranslator.GetEntityId(),
                objectTypeCode: entity.ObjectTypeCode,
                isCustomEntity: entity.IsCustomEntity === true,
                schemaName: entity.SchemaName || logicalName,
                displayName: getLocalizedMetadataLabel(entity.DisplayName),
                displayCollectionName: getLocalizedMetadataLabel(entity.DisplayCollectionName)
            };
        });
    }

    function getLocalizedMetadataLabel(label) {
        var localized = label && label.LocalizedLabels ? label.LocalizedLabels : [];
        var baseLanguage = XrmTranslator.baseLanguage;

        for (var i = 0; i < localized.length; i++) {
            if (localized[i].LanguageCode == baseLanguage && localized[i].Label) {
                return localized[i].Label;
            }
        }

        return localized.length > 0 ? (localized[0].Label || "") : "";
    }

    function getDisplayStringIdentitySet(entityInfo) {
        if (entityInfo.objectTypeCode == null) {
            return Promise.reject(new Error("Could not resolve object type code for " + entityInfo.logicalName + "."));
        }

        var fetchXml =
            "<fetch distinct='true'>" +
            "  <entity name='displaystring'>" +
            "    <attribute name='displaystringid' />" +
            "    <attribute name='displaystringkey' />" +
            "    <link-entity name='displaystringmap' from='displaystringid' to='displaystringid' alias='dsm'>" +
            "      <filter>" +
            "        <condition attribute='objecttypecode' operator='eq' value='" + entityInfo.objectTypeCode + "' />" +
            "      </filter>" +
            "    </link-entity>" +
            "  </entity>" +
            "</fetch>";

        return WebApiClient.Retrieve({
            entityName: "displaystring",
            fetchXml: fetchXml,
            returnAllPages: true
        })
        .then(function (response) {
            var rows = response && response.value ? response.value : [];
            var ids = {};
            var keys = {};

            for (var i = 0; i < rows.length; i++) {
                if (rows[i].displaystringid) {
                    ids[normalize(rows[i].displaystringid)] = true;
                }
                if (rows[i].displaystringkey) {
                    keys[normalize(rows[i].displaystringkey)] = true;
                    keys[normalizeKey(rows[i].displaystringkey)] = true;
                }
            }

            return {
                ids: ids,
                keys: keys
            };
        });
    }

    function findDisplayStringsWorksheet(workbook) {
        for (var i = 0; i < workbook.worksheets.length; i++) {
            if (normalize(workbook.worksheets[i].name) === normalize(DISPLAY_STRINGS_WORKSHEET)) {
                return workbook.worksheets[i];
            }
        }

        for (var j = 0; j < workbook.worksheets.length; j++) {
            if (normalize(workbook.worksheets[j].name).indexOf("display") !== -1 &&
                normalize(workbook.worksheets[j].name).indexOf("string") !== -1) {
                return workbook.worksheets[j];
            }
        }

        return null;
    }

    function findHeaderRow(worksheet) {
        var rows = worksheet ? worksheet.rows : [];

        for (var r = 0; r < rows.length; r++) {
            var row = rows[r];
            var texts = row.cells.map(function (cell) {
                return normalize(cell && cell.text);
            }).join("|");

            if ((texts.indexOf("display") !== -1 || texts.indexOf("resource") !== -1 || texts.indexOf("key") !== -1) &&
                /\b10[0-9]{2,}\b/.test(texts)) {
                return row;
            }
        }

        return rows.length > 0 ? rows[0] : null;
    }

    function buildHeaderMap(headerRow) {
        var headers = [];
        var byName = {};
        var languageColumns = {};

        for (var i = 0; i < headerRow.cells.length; i++) {
            var text = getCellText(headerRow, i).trim();
            if (!text) {
                continue;
            }

            var normalized = normalize(text);
            headers[i] = text;
            byName[normalized] = i;

            if (/^\d{4,5}$/.test(text)) {
                languageColumns[text] = i;
            }
        }

        return {
            headers: headers,
            byName: byName,
            languageColumns: languageColumns
        };
    }

    function findColumn(headerMap, candidates) {
        for (var i = 0; i < candidates.length; i++) {
            var candidate = normalize(candidates[i]);
            if (Object.prototype.hasOwnProperty.call(headerMap.byName, candidate)) {
                return headerMap.byName[candidate];
            }
        }

        for (var key in headerMap.byName) {
            if (!Object.prototype.hasOwnProperty.call(headerMap.byName, key)) {
                continue;
            }

            for (var j = 0; j < candidates.length; j++) {
                if (key.indexOf(normalize(candidates[j])) !== -1) {
                    return headerMap.byName[key];
                }
            }
        }

        return -1;
    }

    function rowMatchesEntity(row, headerMap, identitySet, entityInfo) {
        var idColumn = findColumn(headerMap, ["displaystringid", "display string id", "custom display string id"]);
        var keyColumn = findColumn(headerMap, ["displaystringkey", "display string key", "resource key"]);
        var objectTypeColumn = findColumn(headerMap, ["objecttypecode", "object type code"]);
        var logical = normalize(entityInfo.logicalName);
        var schema = normalize(entityInfo.schemaName);

        if (idColumn >= 0 && identitySet.ids[normalize(getCellText(row, idColumn))]) {
            return true;
        }

        if (keyColumn >= 0) {
            var key = getCellText(row, keyColumn);
            if (identitySet.keys[normalize(key)] || identitySet.keys[normalizeKey(key)]) {
                return true;
            }
            if (normalize(key).indexOf(logical) !== -1 || normalizeKey(key).indexOf(normalizeKey(logical)) !== -1) {
                return true;
            }
        }

        if (objectTypeColumn >= 0) {
            var objectType = normalize(getCellText(row, objectTypeColumn));
            if (objectType === logical || objectType === schema || objectType === normalize(entityInfo.displayName)) {
                return true;
            }
        }

        return false;
    }

    function buildRowKey(row, headerMap) {
        var idColumn = findColumn(headerMap, ["displaystringid", "display string id", "custom display string id"]);
        var keyColumn = findColumn(headerMap, ["displaystringkey", "display string key", "resource key"]);
        var fallbackParts = [];

        if (idColumn >= 0 && getCellText(row, idColumn)) {
            return "id:" + normalize(getCellText(row, idColumn)) + "~row:" + row.index;
        }

        if (keyColumn >= 0 && getCellText(row, keyColumn)) {
            return "key:" + normalize(getCellText(row, keyColumn)) + "~row:" + row.index;
        }

        for (var i = 0; i < row.cells.length; i++) {
            fallbackParts.push(getCellText(row, i));
        }

        return "row:" + row.index + ":" + normalize(fallbackParts.join("|"));
    }

    function getRecordName(row, headerMap) {
        var keyColumn = findColumn(headerMap, ["displaystringkey", "display string key", "resource key"]);
        var defaultColumn = findColumn(headerMap, ["default", "default display string", "default text"]);
        var customColumn = findColumn(headerMap, ["custom", "custom display string", "custom text"]);

        return getCellText(row, keyColumn) ||
            getCellText(row, defaultColumn) ||
            getCellText(row, customColumn) ||
            "Entity Message";
    }

    function buildRecords(packageData, identitySet, entityInfo) {
        var worksheet = findDisplayStringsWorksheet(packageData.workbook);
        if (!worksheet) {
            throw new Error("The translation package does not contain a Display Strings worksheet.");
        }

        var headerRow = findHeaderRow(worksheet);
        var headerMap = buildHeaderMap(headerRow);
        var defaultColumn = findColumn(headerMap, ["default", "default display string", "default text"]);
        var customColumn = findColumn(headerMap, ["custom", "custom display string", "custom text"]);
        var publishedColumn = findColumn(headerMap, ["published", "published display string", "published text"]);
        var records = [];
        var rowMap = {};

        for (var r = headerRow.index + 1; r < worksheet.rows.length; r++) {
            var row = worksheet.rows[r];
            if (!rowMatchesEntity(row, headerMap, identitySet, entityInfo)) {
                continue;
            }

            var rowKey = buildRowKey(row, headerMap);
            var messageKey = getRecordName(row, headerMap);
            var defaultText = defaultColumn >= 0 ? getCellText(row, defaultColumn) : "";
            var customText = customColumn >= 0 ? getCellText(row, customColumn) : "";
            var publishedText = publishedColumn >= 0 ? getCellText(row, publishedColumn) : "";
            var record = {
                recid: rowKey,
                schemaName: defaultText && defaultText !== messageKey ? messageKey + " | " + defaultText : messageKey,
                defaultText: defaultText,
                customText: customText,
                publishedText: publishedText,
                _isEntityMessageRow: true,
                _entityLogicalName: entityInfo.logicalName,
                _translationWorksheet: worksheet.name,
                _translationRowKey: rowKey
            };

            Object.keys(headerMap.languageColumns).forEach(function (lcid) {
                record[lcid] = getCellText(row, headerMap.languageColumns[lcid]);
            });

            records.push(record);
            rowMap[rowKey] = {
                row: row,
                headerMap: headerMap
            };
        }

        return {
            worksheet: worksheet,
            headerMap: headerMap,
            records: records,
            rowMap: rowMap
        };
    }

    function fillTable(records) {
        var grid = XrmTranslator.GetGrid();

        grid.clear();
        XrmTranslator.AddSummary(records);
        grid.add(records);
        XrmTranslator.SetSaveButtonDisabled(true);
        grid.unlock();
    }

    function collectChanges(records) {
        var changes = [];

        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            if (!record._isEntityMessageRow || !record.w2ui || !record.w2ui.changes) {
                continue;
            }

            for (var language in record.w2ui.changes) {
                if (!Object.prototype.hasOwnProperty.call(record.w2ui.changes, language)) {
                    continue;
                }
                if (!/^\d{4,5}$/.test(String(language))) {
                    continue;
                }

                changes.push({
                    rowKey: record._translationRowKey,
                    language: String(language),
                    value: record.w2ui.changes[language]
                });
            }
        }

        return changes;
    }

    function applyChangesToPackage(packageData, identitySet, entityInfo, changes) {
        var parsed = buildRecords(packageData, identitySet, entityInfo);

        for (var i = 0; i < changes.length; i++) {
            var change = changes[i];
            var target = parsed.rowMap[change.rowKey];
            if (!target) {
                throw new Error("Entity message row changed or disappeared before save: " + change.rowKey);
            }

            var column = target.headerMap.languageColumns[change.language];
            if (column == null) {
                throw new Error("Language column " + change.language + " was not found in the fresh translation package.");
            }

            TranslationPackageService.SetCellText(target.row, column, change.value);
        }
    }

    function commitChanges(changes) {
        var grid = XrmTranslator.GetGrid();

        for (var i = 0; i < changes.length; i++) {
            var record = grid.get(changes[i].rowKey);
            if (!record) {
                continue;
            }

            record[changes[i].language] = changes[i].value;
            if (record.w2ui && record.w2ui.changes) {
                delete record.w2ui.changes[changes[i].language];
                if (Object.keys(record.w2ui.changes).length === 0) {
                    delete record.w2ui.changes;
                }
            }
            grid.refreshRow(record.recid);
        }

        XrmTranslator.SetSaveButtonDisabled(true);
    }

    EntityMessageHandler.Load = function () {
        var solutionUniqueName;
        var entityInfo;

        XrmTranslator.LockGrid("Loading entity messages");

        return Promise.all([
            getSolutionUniqueName(XrmTranslator.GetSolution()),
            getSelectedEntityInfo()
        ])
        .then(function (resolved) {
            solutionUniqueName = resolved[0];
            entityInfo = resolved[1];

            if (entityInfo.isCustomEntity) {
                state = {
                    solutionUniqueName: solutionUniqueName,
                    entityInfo: entityInfo,
                    identitySet: { ids: {}, keys: {} },
                    rowCount: 0
                };
                XrmTranslator.metadata = [];
                fillTable([]);
                return null;
            }

            return getDisplayStringIdentitySet(entityInfo);
        })
        .then(function (identitySet) {
            if (!identitySet) {
                return null;
            }

            return TranslationPackageService.ExportTranslations(solutionUniqueName)
            .then(TranslationPackageService.LoadPackage)
            .then(function (packageData) {
                var parsed = buildRecords(packageData, identitySet, entityInfo);

                state = {
                    solutionUniqueName: solutionUniqueName,
                    entityInfo: entityInfo,
                    identitySet: identitySet,
                    rowCount: parsed.records.length
                };
                XrmTranslator.metadata = parsed.records;

                fillTable(parsed.records);
            });
        })
        .catch(function (error) {
            XrmTranslator.GetGrid().unlock();
            XrmTranslator.errorHandler(error);
        });
    };

    EntityMessageHandler.SaveOnly = function () {
        if (!state) {
            return Promise.reject(new Error("Please load entity messages before saving."));
        }

        var changes = collectChanges(XrmTranslator.GetAllRecords());
        if (changes.length === 0) {
            XrmTranslator.SetSaveButtonDisabled(true);
            return Promise.resolve(false);
        }

        XrmTranslator.LockGrid("Re-exporting translations");

        return TranslationPackageService.ExportTranslations(state.solutionUniqueName)
        .then(TranslationPackageService.LoadPackage)
        .then(function (packageData) {
            applyChangesToPackage(packageData, state.identitySet, state.entityInfo, changes);
            return TranslationPackageService.WritePackage(packageData);
        })
        .then(function (updatedBase64) {
            XrmTranslator.LockGrid("Importing translations");
            return TranslationPackageService.ImportTranslations(updatedBase64);
        })
        .then(function () {
            commitChanges(changes);
            return true;
        });
    };

    EntityMessageHandler.Save = function () {
        XrmTranslator.StartOperationStatus({
            phase: "importingTranslations",
            tone: "info",
            message: "Importing entity message translations. Save and Load are temporarily disabled.",
            type: "entityMessages",
            entityLogicalName: state && state.entityInfo ? state.entityInfo.logicalName : XrmTranslator.GetEntity(),
            blockSave: true,
            blockLoad: true
        });

        return EntityMessageHandler.SaveOnly()
        .then(function (saved) {
            if (!saved) {
                XrmTranslator.ClearOperationStatus();
                XrmTranslator.UnlockGrid();
                return false;
            }

            XrmTranslator.UpdateOperationStatus({
                phase: "publishing",
                tone: "info",
                message: "Publishing entity messages for " + state.entityInfo.logicalName + ". Save and Load are temporarily disabled.",
                type: "entityMessages",
                entityLogicalName: state.entityInfo.logicalName,
                blockSave: true,
                blockLoad: true
            });

            return XrmTranslator.Publish()
            .then(function () {
                XrmTranslator.ClearOperationStatus();
                XrmTranslator.LockGrid("Reloading entity messages");
                return EntityMessageHandler.Load();
            });
        })
        .catch(function (error) {
            XrmTranslator.ClearOperationStatus();
            XrmTranslator.GetGrid().unlock();
            XrmTranslator.errorHandler(error);
        });
    };

}(window.EntityMessageHandler = window.EntityMessageHandler || {}));
