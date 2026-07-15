using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Xml;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters
{
    public class EntityMessageAdapter : EasyTranslatorAdapterBase, IEasyTranslatorTypeAdapter
    {
        private const string TranslatorType = "entityMessages";
        private const string PublishKind = "entity";
        private const string DisplayStringsWorksheet = "Display Strings";
        private const string CrmTranslationsFile = "CrmTranslations.xml";
        private const string SpreadsheetNamespace = "urn:schemas-microsoft-com:office:spreadsheet";

        public EasyTranslatorLoadOutput Load(EasyTranslatorRuntimeContext context, EasyTranslatorLoadInput input)
        {
            var baseLanguage = GetBaseLanguage(context.ServiceAdmin);
            var entityInfo = GetSelectedEntityInfo(context.ServiceAdmin, input.entityName);

            if (entityInfo.IsCustomEntity)
            {
                return EmptyOutput(baseLanguage);
            }

            var identitySet = GetDisplayStringIdentitySet(context.ServiceAdmin, entityInfo);
            var solutionUniqueName = GetSolutionUniqueName(context.ServiceAdmin, input.solutionId);
            var package = LoadTranslationPackage(context, solutionUniqueName);
            var workbook = package.Workbook;
            var parsed = BuildRecords(workbook, identitySet, entityInfo);

            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage.ToString(),
                grid = new EasyTranslatorGridOutput
                {
                    mode = "flat",
                    title = "Entity Messages",
                    rows = parsed.Records
                }
            };
        }

        public EasyTranslatorSaveOutput Save(EasyTranslatorRuntimeContext context, EasyTranslatorSaveInput input)
        {
            var output = new EasyTranslatorSaveOutput();
            var validRows = new List<EasyTranslatorChangedRowInput>();

            foreach (var row in input.changedRows ?? new List<EasyTranslatorChangedRowInput>())
            {
                if (GetValidLabelChanges(row).Count > 0)
                {
                    validRows.Add(row);
                }
            }

            if (validRows.Count == 0)
            {
                return output;
            }

            var entityInfo = GetSelectedEntityInfo(context.ServiceAdmin, input.entityName);
            if (entityInfo.IsCustomEntity)
            {
                return output;
            }

            var identitySet = GetDisplayStringIdentitySet(context.ServiceAdmin, entityInfo);
            var solutionUniqueName = GetSolutionUniqueName(context.ServiceAdmin, input.solutionId);
            var package = LoadTranslationPackage(context, solutionUniqueName);
            var workbook = package.Workbook;
            var parsed = BuildRecords(workbook, identitySet, entityInfo);

            foreach (var changedRow in validRows)
            {
                var parts = ParseGridKey(changedRow.gridKey, TranslatorType, 2);
                var rowKey = parts[1];

                if (!parsed.RowMap.TryGetValue(rowKey, out var target))
                {
                    throw new InvalidPluginExecutionException("Entity message row changed or disappeared before save: " + rowKey);
                }

                foreach (var change in GetValidLabelChanges(changedRow))
                {
                    var language = change.LanguageCode.ToString();
                    if (!target.HeaderMap.LanguageColumns.TryGetValue(language, out var column))
                    {
                        throw new InvalidPluginExecutionException(
                            "Language column " + language + " was not found in the fresh translation package.");
                    }

                    SetCellText(target.Row, column, change.Label);
                }

                output.changedRowCount++;
            }

            var updatedPackage = WriteTranslationPackage(context, package);
            ImportTranslationPackage(context, updatedPackage);

            AddPublishTarget(output, new HashSet<string>(StringComparer.OrdinalIgnoreCase), PublishKind, entityInfo.LogicalName);
            return output;
        }

        public EasyTranslatorSaveOutput Publish(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            var entityNames = GetPublishTargetIds(input, PublishKind, false);
            if (entityNames.Count > 0)
            {
                context.ServiceAdmin.Execute(new PublishXmlRequest
                {
                    ParameterXml = BuildEntityPublishXml(entityNames)
                });
            }

            return ToPublishOutput(PublishKind, entityNames);
        }

        public EasyTranslatorSaveOutput Published(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            return ToPublishOutput(PublishKind, GetPublishTargetIds(input, PublishKind, false));
        }

        private static string GetSolutionUniqueName(IOrganizationService serviceAdmin, string solutionId)
        {
            var solutionGuid = ParseSolutionId(solutionId, "Entity Messages");
            if (!solutionGuid.HasValue)
            {
                throw new InvalidPluginExecutionException("Please select a solution before loading entity messages.");
            }

            var solution = serviceAdmin.Retrieve("solution", solutionGuid.Value, new ColumnSet("uniquename"));
            var uniqueName = solution.GetAttributeValue<string>("uniquename");
            if (string.IsNullOrWhiteSpace(uniqueName))
            {
                throw new InvalidPluginExecutionException("Could not resolve the selected solution unique name.");
            }

            return uniqueName;
        }

        private static EntityMessageEntityInfo GetSelectedEntityInfo(IOrganizationService serviceAdmin, string entityName)
        {
            var logicalName = (entityName ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(logicalName) || string.Equals(logicalName, "none", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidPluginExecutionException("Please select an entity before loading entity messages.");
            }

            var response = (RetrieveEntityResponse)serviceAdmin.Execute(new RetrieveEntityRequest
            {
                LogicalName = logicalName,
                EntityFilters = EntityFilters.Entity,
                RetrieveAsIfPublished = true
            });

            var metadata = response?.EntityMetadata;
            if (metadata == null)
            {
                throw new InvalidPluginExecutionException("Could not resolve metadata for entity " + logicalName + ".");
            }

            return new EntityMessageEntityInfo
            {
                LogicalName = logicalName,
                ObjectTypeCode = metadata.ObjectTypeCode,
                IsCustomEntity = metadata.IsCustomEntity.GetValueOrDefault(),
                SchemaName = metadata.SchemaName ?? logicalName,
                DisplayName = GetAnyLabel(metadata.DisplayName),
                DisplayCollectionName = GetAnyLabel(metadata.DisplayCollectionName)
            };
        }

        private static EntityMessageIdentitySet GetDisplayStringIdentitySet(
            IOrganizationService serviceAdmin,
            EntityMessageEntityInfo entityInfo)
        {
            if (!entityInfo.ObjectTypeCode.HasValue)
            {
                throw new InvalidPluginExecutionException("Could not resolve object type code for " + entityInfo.LogicalName + ".");
            }

            var query = new QueryExpression("displaystring")
            {
                ColumnSet = new ColumnSet("displaystringid", "displaystringkey")
            };
            var link = query.AddLink("displaystringmap", "displaystringid", "displaystringid");
            link.LinkCriteria.AddCondition("objecttypecode", ConditionOperator.Equal, entityInfo.ObjectTypeCode.Value);

            var identitySet = new EntityMessageIdentitySet();
            foreach (var row in Helper.RetrieveAll(serviceAdmin, query))
            {
                var id = row.GetAttributeValue<Guid>("displaystringid");
                if (id != Guid.Empty)
                {
                    identitySet.Ids.Add(Normalize(id.ToString("D")));
                }

                var key = row.GetAttributeValue<string>("displaystringkey");
                if (!string.IsNullOrWhiteSpace(key))
                {
                    identitySet.Keys.Add(Normalize(key));
                    identitySet.Keys.Add(NormalizeKey(key));
                }
            }

            return identitySet;
        }

        private static TranslationPackage LoadTranslationPackage(EasyTranslatorRuntimeContext context, string solutionUniqueName)
        {
            try
            {
                Trace(context, "Entity Messages: exporting translation package for solution '{0}'.", solutionUniqueName);
                var response = (ExportTranslationResponse)context.ServiceAdmin.Execute(new ExportTranslationRequest
                {
                    SolutionName = solutionUniqueName
                });

                var packageBytes = response?.ExportTranslationFile;
                if (packageBytes == null || packageBytes.Length == 0)
                {
                    throw new InvalidPluginExecutionException("ExportTranslation did not return ExportTranslationFile.");
                }

                Trace(context, "Entity Messages: exported package size {0} bytes.", packageBytes.Length);
                var xmlText = ReadZipEntry(context, packageBytes, CrmTranslationsFile);
                var doc = new XmlDocument { PreserveWhitespace = true };
                doc.LoadXml(xmlText);

                return new TranslationPackage
                {
                    Bytes = packageBytes,
                    Workbook = ParseWorkbook(doc)
                };
            }
            catch (Exception ex)
            {
                Trace(context, "Entity Messages: ZIP export/read failed. {0}", ex);
                throw new InvalidPluginExecutionException("Entity Messages server ZIP processing failed while reading the translation package.", ex);
            }
        }

        private static string ReadZipEntry(EasyTranslatorRuntimeContext context, byte[] zipBytes, string entryName)
        {
            Trace(context, "Entity Messages: opening ZIP package in plugin sandbox.");
            using (var stream = new MemoryStream(zipBytes))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var entry = archive.GetEntry(entryName);
                if (entry == null)
                {
                    throw new InvalidPluginExecutionException(entryName + " was not found in the translation package.");
                }

                using (var entryStream = entry.Open())
                using (var reader = new StreamReader(entryStream, Encoding.UTF8, true))
                {
                    var xml = reader.ReadToEnd();
                    Trace(context, "Entity Messages: read {0} chars from {1}.", xml.Length, entryName);
                    return xml;
                }
            }
        }

        private static byte[] WriteTranslationPackage(EasyTranslatorRuntimeContext context, TranslationPackage package)
        {
            try
            {
                Trace(context, "Entity Messages: writing updated XML back into ZIP package.");
                using (var source = new MemoryStream(package.Bytes))
                using (var output = new MemoryStream())
                {
                    source.CopyTo(output);
                    output.Position = 0;

                    using (var archive = new ZipArchive(output, ZipArchiveMode.Update, true))
                    {
                        var existing = archive.GetEntry(CrmTranslationsFile);
                        existing?.Delete();

                        var entry = archive.CreateEntry(CrmTranslationsFile, CompressionLevel.Optimal);
                        using (var stream = entry.Open())
                        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                        {
                            package.Workbook.Document.Save(writer);
                        }
                    }

                    var bytes = output.ToArray();
                    Trace(context, "Entity Messages: updated ZIP package size {0} bytes.", bytes.Length);
                    return bytes;
                }
            }
            catch (Exception ex)
            {
                Trace(context, "Entity Messages: ZIP write failed. {0}", ex);
                throw new InvalidPluginExecutionException("Entity Messages server ZIP processing failed while writing the translation package.", ex);
            }
        }

        private static void ImportTranslationPackage(EasyTranslatorRuntimeContext context, byte[] packageBytes)
        {
            try
            {
                Trace(context, "Entity Messages: importing updated translation package.");
                context.ServiceAdmin.Execute(new ImportTranslationRequest
                {
                    TranslationFile = packageBytes,
                    ImportJobId = Guid.NewGuid()
                });
                Trace(context, "Entity Messages: ImportTranslationRequest executed.");
            }
            catch (Exception ex)
            {
                Trace(context, "Entity Messages: ImportTranslationRequest failed. {0}", ex);
                throw new InvalidPluginExecutionException("Entity Messages import failed after server ZIP processing.", ex);
            }
        }

        private static EntityMessageWorkbook ParseWorkbook(XmlDocument doc)
        {
            var worksheets = new List<EntityMessageWorksheet>();
            foreach (XmlElement worksheetNode in SelectElements(doc, "Worksheet"))
            {
                worksheets.Add(new EntityMessageWorksheet
                {
                    Node = worksheetNode,
                    Name = GetSpreadsheetAttribute(worksheetNode, "Name"),
                    Rows = ParseWorksheetRows(worksheetNode)
                });
            }

            return new EntityMessageWorkbook
            {
                Document = doc,
                Worksheets = worksheets
            };
        }

        private static List<EntityMessageRow> ParseWorksheetRows(XmlElement worksheetNode)
        {
            var table = SelectDirectElement(worksheetNode, "Table") ?? SelectElement(worksheetNode, "Table");
            var rowNodes = table == null ? new List<XmlElement>() : SelectDirectElements(table, "Row");
            var rows = new List<EntityMessageRow>();

            for (var r = 0; r < rowNodes.Count; r++)
            {
                var rowNode = rowNodes[r];
                var cells = new List<EntityMessageCell>();
                var columnIndex = 0;

                foreach (var cellNode in SelectDirectElements(rowNode, "Cell"))
                {
                    var explicitIndex = ParseInt(GetSpreadsheetAttribute(cellNode, "Index"));
                    if (explicitIndex.HasValue && explicitIndex.Value > 0)
                    {
                        columnIndex = explicitIndex.Value - 1;
                    }

                    EnsureCellListSize(cells, columnIndex);
                    var dataNode = SelectDirectElement(cellNode, "Data");
                    cells[columnIndex] = new EntityMessageCell
                    {
                        Node = cellNode,
                        DataNode = dataNode,
                        Text = dataNode?.InnerText ?? string.Empty
                    };
                    columnIndex++;
                }

                rows.Add(new EntityMessageRow
                {
                    Index = r,
                    Node = rowNode,
                    Cells = cells
                });
            }

            return rows;
        }

        private static EntityMessageParsedRecords BuildRecords(
            EntityMessageWorkbook workbook,
            EntityMessageIdentitySet identitySet,
            EntityMessageEntityInfo entityInfo)
        {
            var worksheet = FindDisplayStringsWorksheet(workbook);
            if (worksheet == null)
            {
                throw new InvalidPluginExecutionException("The translation package does not contain a Display Strings worksheet.");
            }

            var headerRow = FindHeaderRow(worksheet);
            var headerMap = BuildHeaderMap(headerRow);
            var defaultColumn = FindColumn(headerMap, "default", "default display string", "default text");
            var customColumn = FindColumn(headerMap, "custom", "custom display string", "custom text");
            var publishedColumn = FindColumn(headerMap, "published", "published display string", "published text");
            var parsed = new EntityMessageParsedRecords();

            for (var r = headerRow.Index + 1; r < worksheet.Rows.Count; r++)
            {
                var row = worksheet.Rows[r];
                if (!RowMatchesEntity(row, headerMap, identitySet, entityInfo))
                {
                    continue;
                }

                var rowKey = BuildRowKey(row, headerMap);
                var messageKey = GetRecordName(row, headerMap);
                var defaultText = defaultColumn >= 0 ? GetCellText(row, defaultColumn) : string.Empty;
                var customText = customColumn >= 0 ? GetCellText(row, customColumn) : string.Empty;
                var publishedText = publishedColumn >= 0 ? GetCellText(row, publishedColumn) : string.Empty;
                var schemaName = !string.IsNullOrWhiteSpace(defaultText) && defaultText != messageKey
                    ? messageKey + " | " + defaultText
                    : messageKey;

                var record = new EasyTranslatorGridRowOutput
                {
                    Recid = "entityMessage:" + rowKey,
                    GridKey = BuildGridKey(TranslatorType, rowKey),
                    SchemaName = schemaName,
                    RowType = "entityMessages.row",
                    IsEditable = true,
                    IsTranslatable = true,
                    Ai = new EasyTranslatorAiOutput { include = true, location = entityInfo.LogicalName }
                };
                record["defaultText"] = defaultText;
                record["customText"] = customText;
                record["publishedText"] = publishedText;

                foreach (var languageColumn in headerMap.LanguageColumns)
                {
                    record.SetLanguageValue(languageColumn.Key, GetCellText(row, languageColumn.Value));
                }

                parsed.Records.Add(record);
                parsed.RowMap[rowKey] = new EntityMessageRowTarget
                {
                    Row = row,
                    HeaderMap = headerMap
                };
            }

            parsed.Records.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.SchemaName, b.SchemaName));
            return parsed;
        }

        private static EntityMessageWorksheet FindDisplayStringsWorksheet(EntityMessageWorkbook workbook)
        {
            foreach (var worksheet in workbook.Worksheets)
            {
                if (Normalize(worksheet.Name) == Normalize(DisplayStringsWorksheet))
                {
                    return worksheet;
                }
            }

            foreach (var worksheet in workbook.Worksheets)
            {
                var name = Normalize(worksheet.Name);
                if (name.Contains("display") && name.Contains("string"))
                {
                    return worksheet;
                }
            }

            return null;
        }

        private static EntityMessageRow FindHeaderRow(EntityMessageWorksheet worksheet)
        {
            foreach (var row in worksheet.Rows)
            {
                var texts = string.Empty;
                foreach (var cell in row.Cells)
                {
                    texts += Normalize(cell?.Text) + "|";
                }

                if ((texts.Contains("display") || texts.Contains("resource") || texts.Contains("key")) &&
                    System.Text.RegularExpressions.Regex.IsMatch(texts, "\\b10[0-9]{2,}\\b"))
                {
                    return row;
                }
            }

            if (worksheet.Rows.Count == 0)
            {
                throw new InvalidPluginExecutionException("The Display Strings worksheet does not contain rows.");
            }

            return worksheet.Rows[0];
        }

        private static EntityMessageHeaderMap BuildHeaderMap(EntityMessageRow headerRow)
        {
            var map = new EntityMessageHeaderMap();
            for (var i = 0; i < headerRow.Cells.Count; i++)
            {
                var text = GetCellText(headerRow, i).Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                map.ByName[Normalize(text)] = i;
                if (System.Text.RegularExpressions.Regex.IsMatch(text, "^\\d{4,5}$"))
                {
                    map.LanguageColumns[text] = i;
                }
            }

            return map;
        }

        private static int FindColumn(EntityMessageHeaderMap headerMap, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (headerMap.ByName.TryGetValue(Normalize(candidate), out var column))
                {
                    return column;
                }
            }

            foreach (var entry in headerMap.ByName)
            {
                foreach (var candidate in candidates)
                {
                    if (entry.Key.Contains(Normalize(candidate)))
                    {
                        return entry.Value;
                    }
                }
            }

            return -1;
        }

        private static bool RowMatchesEntity(
            EntityMessageRow row,
            EntityMessageHeaderMap headerMap,
            EntityMessageIdentitySet identitySet,
            EntityMessageEntityInfo entityInfo)
        {
            var idColumn = FindColumn(headerMap, "displaystringid", "display string id", "custom display string id");
            var keyColumn = FindColumn(headerMap, "displaystringkey", "display string key", "resource key");
            var objectTypeColumn = FindColumn(headerMap, "objecttypecode", "object type code");
            var logical = Normalize(entityInfo.LogicalName);
            var schema = Normalize(entityInfo.SchemaName);

            if (idColumn >= 0 && identitySet.Ids.Contains(Normalize(GetCellText(row, idColumn))))
            {
                return true;
            }

            if (keyColumn >= 0)
            {
                var key = GetCellText(row, keyColumn);
                if (identitySet.Keys.Contains(Normalize(key)) || identitySet.Keys.Contains(NormalizeKey(key)))
                {
                    return true;
                }

                if (Normalize(key).Contains(logical) || NormalizeKey(key).Contains(NormalizeKey(logical)))
                {
                    return true;
                }
            }

            if (objectTypeColumn >= 0)
            {
                var objectType = Normalize(GetCellText(row, objectTypeColumn));
                if (objectType == logical || objectType == schema || objectType == Normalize(entityInfo.DisplayName))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildRowKey(EntityMessageRow row, EntityMessageHeaderMap headerMap)
        {
            var idColumn = FindColumn(headerMap, "displaystringid", "display string id", "custom display string id");
            var keyColumn = FindColumn(headerMap, "displaystringkey", "display string key", "resource key");

            if (idColumn >= 0 && !string.IsNullOrWhiteSpace(GetCellText(row, idColumn)))
            {
                return "id:" + Normalize(GetCellText(row, idColumn)) + "~row:" + row.Index;
            }

            if (keyColumn >= 0 && !string.IsNullOrWhiteSpace(GetCellText(row, keyColumn)))
            {
                return "key:" + Normalize(GetCellText(row, keyColumn)) + "~row:" + row.Index;
            }

            var fallbackParts = new List<string>();
            for (var i = 0; i < row.Cells.Count; i++)
            {
                fallbackParts.Add(GetCellText(row, i));
            }

            return "row:" + row.Index + ":" + Normalize(string.Join("|", fallbackParts));
        }

        private static string GetRecordName(EntityMessageRow row, EntityMessageHeaderMap headerMap)
        {
            var keyColumn = FindColumn(headerMap, "displaystringkey", "display string key", "resource key");
            var defaultColumn = FindColumn(headerMap, "default", "default display string", "default text");
            var customColumn = FindColumn(headerMap, "custom", "custom display string", "custom text");

            var key = GetCellText(row, keyColumn);
            if (!string.IsNullOrWhiteSpace(key))
            {
                return key;
            }

            var defaultText = GetCellText(row, defaultColumn);
            if (!string.IsNullOrWhiteSpace(defaultText))
            {
                return defaultText;
            }

            var customText = GetCellText(row, customColumn);
            return string.IsNullOrWhiteSpace(customText) ? "Entity Message" : customText;
        }

        private static string GetCellText(EntityMessageRow row, int index)
        {
            if (row == null || index < 0 || index >= row.Cells.Count || row.Cells[index] == null)
            {
                return string.Empty;
            }

            return row.Cells[index].Text ?? string.Empty;
        }

        private static void SetCellText(EntityMessageRow row, int columnIndex, string value)
        {
            var cell = EnsureCell(row, columnIndex);
            cell.DataNode.InnerText = value ?? string.Empty;
            cell.Text = value ?? string.Empty;
        }

        private static EntityMessageCell EnsureCell(EntityMessageRow row, int columnIndex)
        {
            EnsureCellListSize(row.Cells, columnIndex);
            var cell = row.Cells[columnIndex];
            var doc = row.Node.OwnerDocument;

            if (cell?.Node != null)
            {
                if (cell.DataNode == null)
                {
                    cell.DataNode = doc.CreateElement("Data", SpreadsheetNamespace);
                    cell.DataNode.SetAttribute("Type", SpreadsheetNamespace, "String");
                    cell.Node.AppendChild(cell.DataNode);
                }

                return cell;
            }

            XmlElement insertBefore = null;
            for (var i = columnIndex + 1; i < row.Cells.Count && insertBefore == null; i++)
            {
                insertBefore = row.Cells[i]?.Node;
            }

            var cellNode = doc.CreateElement("Cell", SpreadsheetNamespace);
            cellNode.SetAttribute("Index", SpreadsheetNamespace, (columnIndex + 1).ToString());
            var dataNode = doc.CreateElement("Data", SpreadsheetNamespace);
            dataNode.SetAttribute("Type", SpreadsheetNamespace, "String");
            cellNode.AppendChild(dataNode);

            if (insertBefore != null)
            {
                row.Node.InsertBefore(cellNode, insertBefore);
            }
            else
            {
                row.Node.AppendChild(cellNode);
            }

            cell = new EntityMessageCell
            {
                Node = cellNode,
                DataNode = dataNode,
                Text = string.Empty
            };
            row.Cells[columnIndex] = cell;
            return cell;
        }

        private static List<XmlElement> SelectElements(XmlNode parent, string localName)
        {
            var result = new List<XmlElement>();
            foreach (XmlNode node in parent.SelectNodes("//*[local-name()='" + localName + "']"))
            {
                if (node is XmlElement element)
                {
                    result.Add(element);
                }
            }

            return result;
        }

        private static XmlElement SelectElement(XmlNode parent, string localName)
        {
            foreach (var element in SelectElements(parent, localName))
            {
                return element;
            }

            return null;
        }

        private static List<XmlElement> SelectDirectElements(XmlNode parent, string localName)
        {
            var result = new List<XmlElement>();
            foreach (XmlNode child in parent.ChildNodes)
            {
                if (child is XmlElement element && element.LocalName == localName)
                {
                    result.Add(element);
                }
            }

            return result;
        }

        private static XmlElement SelectDirectElement(XmlNode parent, string localName)
        {
            foreach (var element in SelectDirectElements(parent, localName))
            {
                return element;
            }

            return null;
        }

        private static string GetSpreadsheetAttribute(XmlElement element, string name)
        {
            if (element == null)
            {
                return string.Empty;
            }

            var value = element.GetAttribute(name);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            value = element.GetAttribute("ss:" + name);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            return element.GetAttribute(name, SpreadsheetNamespace) ?? string.Empty;
        }

        private static void EnsureCellListSize(List<EntityMessageCell> cells, int index)
        {
            while (cells.Count <= index)
            {
                cells.Add(null);
            }
        }

        private static int? ParseInt(string value)
        {
            return int.TryParse(value, out var result) ? result : (int?)null;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string NormalizeKey(string value)
        {
            var normalized = Normalize(value);
            var builder = new StringBuilder();
            foreach (var ch in normalized)
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString();
        }

        private static string GetAnyLabel(Label label)
        {
            if (label?.LocalizedLabels == null || label.LocalizedLabels.Count == 0)
            {
                return string.Empty;
            }

            return label.LocalizedLabels[0]?.Label ?? string.Empty;
        }

        private static string BuildEntityPublishXml(List<string> entityNames)
        {
            var sb = new StringBuilder();
            foreach (var name in entityNames)
            {
                sb.Append("<entity>").Append(name).Append("</entity>");
            }

            return string.Concat("<importexportxml><entities>", sb.ToString(), "</entities></importexportxml>");
        }

        private static void Trace(EasyTranslatorRuntimeContext context, string message, params object[] args)
        {
            var tracing = context?.Tracing;
            if (tracing == null)
            {
                return;
            }

            tracing.Trace(args == null || args.Length == 0 ? message : string.Format(message, args));
        }

        private static EasyTranslatorLoadOutput EmptyOutput(int baseLanguage)
        {
            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage.ToString(),
                grid = new EasyTranslatorGridOutput
                {
                    mode = "flat",
                    title = "Entity Messages",
                    rows = new List<EasyTranslatorGridRowOutput>()
                }
            };
        }

        private class EntityMessageEntityInfo
        {
            public string LogicalName { get; set; }
            public int? ObjectTypeCode { get; set; }
            public bool IsCustomEntity { get; set; }
            public string SchemaName { get; set; }
            public string DisplayName { get; set; }
            public string DisplayCollectionName { get; set; }
        }

        private class EntityMessageIdentitySet
        {
            public HashSet<string> Ids { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> Keys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private class TranslationPackage
        {
            public byte[] Bytes { get; set; }
            public EntityMessageWorkbook Workbook { get; set; }
        }

        private class EntityMessageWorkbook
        {
            public XmlDocument Document { get; set; }
            public List<EntityMessageWorksheet> Worksheets { get; set; }
        }

        private class EntityMessageWorksheet
        {
            public XmlElement Node { get; set; }
            public string Name { get; set; }
            public List<EntityMessageRow> Rows { get; set; }
        }

        private class EntityMessageRow
        {
            public int Index { get; set; }
            public XmlElement Node { get; set; }
            public List<EntityMessageCell> Cells { get; set; }
        }

        private class EntityMessageCell
        {
            public XmlElement Node { get; set; }
            public XmlElement DataNode { get; set; }
            public string Text { get; set; }
        }

        private class EntityMessageHeaderMap
        {
            public Dictionary<string, int> ByName { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, int> LanguageColumns { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        private class EntityMessageParsedRecords
        {
            public List<EasyTranslatorGridRowOutput> Records { get; } = new List<EasyTranslatorGridRowOutput>();
            public Dictionary<string, EntityMessageRowTarget> RowMap { get; } = new Dictionary<string, EntityMessageRowTarget>(StringComparer.OrdinalIgnoreCase);
        }

        private class EntityMessageRowTarget
        {
            public EntityMessageRow Row { get; set; }
            public EntityMessageHeaderMap HeaderMap { get; set; }
        }
    }
}
