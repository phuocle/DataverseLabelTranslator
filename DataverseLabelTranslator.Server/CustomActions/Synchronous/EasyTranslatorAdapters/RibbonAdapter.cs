using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace DataverseLabelTranslator.Server.CustomActions.Synchronous.EasyTranslatorAdapters
{
    public class RibbonAdapter : EasyTranslatorAdapterBase, IEasyTranslatorTypeAdapter
    {
        private const string TranslatorType = "ribbons";
        private const string PublishKind = "publishAllXml";
        private const string CustomizationsFile = "customizations.xml";
        private static readonly string[] TranslatableAttributes = { "LabelText", "ToolTipTitle", "ToolTipDescription" };
        private static readonly Dictionary<string, string> PropertyDisplayNames =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "LabelText", "Text" },
                { "ToolTipTitle", "Title" },
                { "ToolTipDescription", "Description" }
            };

        public EasyTranslatorLoadOutput Load(EasyTranslatorRuntimeContext context, EasyTranslatorLoadInput input)
        {
            var baseLanguage = GetBaseLanguage(context.ServiceAdmin);
            var languages = RetrieveAvailableLanguages(context.ServiceAdmin);
            var solution = GetSelectedSolution(context.ServiceAdmin, input.solutionId);
            var entityInfo = GetSelectedEntityInfo(context.ServiceAdmin, input.entityName);
            var package = ExportSolutionPackage(context, solution.UniqueName);
            var document = ParseXml(package.CustomizationsXml);
            var entityNode = FindEntityNode(document, entityInfo.LogicalName);
            if (entityNode == null)
            {
                throw new InvalidPluginExecutionException("Entity " + entityInfo.LogicalName + " was not found in exported customizations.xml.");
            }

            var ribbonDiffXml = GetDirectChild(entityNode, "RibbonDiffXml");
            var rows = BuildRecordsFromRibbon(ribbonDiffXml, entityInfo, languages, baseLanguage);
            rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.SchemaName, b.SchemaName));

            return new EasyTranslatorLoadOutput
            {
                baseLanguage = baseLanguage.ToString(),
                grid = new EasyTranslatorGridOutput
                {
                    mode = "tree",
                    title = "Ribbons",
                    languageColumns = BuildLanguageColumns(languages),
                    rows = rows
                }
            };
        }

        public EasyTranslatorSaveOutput Save(EasyTranslatorRuntimeContext context, EasyTranslatorSaveInput input)
        {
            var updates = BuildUpdates(input);
            var output = new EasyTranslatorSaveOutput();
            if (updates.Count == 0)
            {
                return output;
            }

            var solution = GetSelectedSolution(context.ServiceAdmin, input.solutionId);
            var entityInfo = GetSelectedEntityInfo(context.ServiceAdmin, input.entityName);
            var package = ExportSolutionPackage(context, solution.UniqueName);
            var updatedXml = ApplyRibbonXmlChanges(package.CustomizationsXml, entityInfo.LogicalName, updates);
            if (string.Equals(package.CustomizationsXml, updatedXml, StringComparison.Ordinal))
            {
                return output;
            }

            var updatedPackage = WriteZipEntry(package.Bytes, CustomizationsFile, updatedXml);
            var importJobId = Guid.NewGuid();
            var response = (ImportSolutionAsyncResponse)context.ServiceAdmin.Execute(new ImportSolutionAsyncRequest
            {
                CustomizationFile = updatedPackage,
                ImportJobId = importJobId,
                OverwriteUnmanagedCustomizations = true,
                PublishWorkflows = false
            });

            output.changed = true;
            output.changedRowCount = updates.Count;
            output.needsPublish = true;
            output.publishKind = PublishKind;
            output.import = new EasyTranslatorAsyncOperationOutput
            {
                mode = "async",
                kind = "importSolution",
                operationId = response.AsyncOperationId.ToString("D"),
                importJobId = importJobId.ToString("D"),
                translatorType = TranslatorType,
                entityName = entityInfo.LogicalName,
                startedOn = DateTime.UtcNow.ToString("o")
            };

            return output;
        }

        public EasyTranslatorSaveOutput Publish(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            var response = (PublishAllXmlAsyncResponse)context.ServiceAdmin.Execute(new PublishAllXmlAsyncRequest());
            var now = DateTime.UtcNow.ToString("o");
            return new EasyTranslatorSaveOutput
            {
                changed = true,
                publish = new EasyTranslatorAsyncOperationOutput
                {
                    mode = "async",
                    kind = PublishKind,
                    operationId = response.AsyncOperationId.ToString("D"),
                    importJobId = input.importJobId,
                    translatorType = TranslatorType,
                    entityName = input.entityName,
                    startedOn = now,
                    lastKnownRunningOn = now
                }
            };
        }

        public EasyTranslatorSaveOutput Published(EasyTranslatorRuntimeContext context, EasyTranslatorPublishInput input)
        {
            return new EasyTranslatorSaveOutput();
        }

        private static List<EasyTranslatorGridRowOutput> BuildRecordsFromRibbon(
            XElement ribbonDiffXml,
            RibbonEntityInfo entityInfo,
            List<int> languages,
            int baseLanguage)
        {
            var records = new List<EasyTranslatorGridRowOutput>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var locLabels = BuildLocLabelMap(ribbonDiffXml);
            var controlIndex = 0;

            foreach (var controlNode in Descendants(ribbonDiffXml).Where(IsControlNode))
            {
                var children = BuildControlChildren(controlNode, entityInfo, locLabels, languages, controlIndex, seen);
                if (children.Count > 0)
                {
                    var surface = DetectSurface(controlNode);
                    var surfaceLabel = GetSurfaceLabel(surface);
                    var previewLabel = GetControlPreviewLabel(controlNode, locLabels, baseLanguage);
                    records.Add(new EasyTranslatorGridRowOutput
                    {
                        Recid = "ribbon-control-" + controlIndex,
                        GridKey = BuildGridKey(TranslatorType, "control", entityInfo.LogicalName, surface, controlNode.Name.LocalName, GetAttribute(controlNode, "Id")),
                        SchemaName = GetControlNodeText(surfaceLabel, controlNode, previewLabel),
                        RowType = "ribbons.control",
                        IsEditable = false,
                        IsTranslatable = false,
                        Ai = new EasyTranslatorAiOutput { include = false },
                        Children = children
                    });
                }

                controlIndex++;
            }

            return records;
        }

        private static List<EasyTranslatorGridRowOutput> BuildControlChildren(
            XElement controlNode,
            RibbonEntityInfo entityInfo,
            Dictionary<string, Dictionary<string, string>> locLabels,
            List<int> languages,
            int controlIndex,
            HashSet<string> seen)
        {
            var children = new List<EasyTranslatorGridRowOutput>();
            var controlId = GetAttribute(controlNode, "Id");
            var commandId = GetAttribute(controlNode, "Command");
            var surface = DetectSurface(controlNode);
            var surfaceLabel = GetSurfaceLabel(surface);
            var location = GetControlNodeText(surfaceLabel, controlNode, GetControlPreviewLabel(controlNode, locLabels, 0));

            foreach (var property in TranslatableAttributes)
            {
                var existingLocLabelId = GetLocLabelId(controlNode, property);
                var locLabelId = existingLocLabelId ?? GetDefaultLocLabelId(controlId, property);
                var dedupeKey = string.Join("|", surface, controlId, property, locLabelId);
                if (!seen.Add(dedupeKey))
                {
                    continue;
                }

                var row = new EasyTranslatorGridRowOutput
                {
                    Recid = "ribbon-control-" + controlIndex + "-label-" + children.Count,
                    GridKey = BuildGridKey(TranslatorType, "label", entityInfo.LogicalName, surface, controlNode.Name.LocalName, controlId, property, locLabelId),
                    SchemaName = PropertyDisplayNames.ContainsKey(property) ? PropertyDisplayNames[property] : property,
                    RowType = "ribbons.label",
                    IsEditable = true,
                    IsTranslatable = true,
                    Ai = new EasyTranslatorAiOutput { include = true, location = location }
                };
                row["commandId"] = commandId;

                var labels = locLabels.ContainsKey(locLabelId)
                    ? locLabels[locLabelId]
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var language in languages)
                {
                    var key = language.ToString();
                    row.SetLanguageValue(key, labels.ContainsKey(key) ? labels[key] : string.Empty);
                }

                children.Add(row);
            }

            return children;
        }

        private static List<RibbonLabelUpdate> BuildUpdates(EasyTranslatorSaveInput input)
        {
            var updates = new List<RibbonLabelUpdate>();
            foreach (var row in input.changedRows ?? new List<EasyTranslatorChangedRowInput>())
            {
                var changes = GetValidLabelChanges(row);
                if (changes.Count == 0)
                {
                    continue;
                }

                var parts = ParseGridKey(row.gridKey, TranslatorType, 8);
                if (!string.Equals(parts[1], "label", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidPluginExecutionException("Ribbon gridKey must target a label row.");
                }

                updates.Add(new RibbonLabelUpdate
                {
                    EntityLogicalName = parts[2],
                    Surface = parts[3],
                    Component = parts[4],
                    ControlId = parts[5],
                    Property = parts[6],
                    LocLabelId = parts[7],
                    Labels = changes
                });
            }

            return updates;
        }

        private static string ApplyRibbonXmlChanges(string customizationsXml, string entityLogicalName, List<RibbonLabelUpdate> updates)
        {
            var document = ParseXml(customizationsXml);
            var entityNode = FindEntityNode(document, entityLogicalName);
            if (entityNode == null)
            {
                throw new InvalidPluginExecutionException("Entity " + entityLogicalName + " was not found in customizations.xml.");
            }

            var ribbonDiffXml = GetDirectChild(entityNode, "RibbonDiffXml");
            if (ribbonDiffXml == null)
            {
                throw new InvalidPluginExecutionException("RibbonDiffXml was not found for entity " + entityLogicalName + ".");
            }

            var locLabelsNode = GetOrCreateDirectChild(ribbonDiffXml, "LocLabels");
            var changed = false;

            foreach (var update in updates)
            {
                var controlNode = FindControlNodeByUpdate(ribbonDiffXml, update);
                if (controlNode == null)
                {
                    throw new InvalidPluginExecutionException("Ribbon control " + update.ControlId + " was not found in customizations.xml.");
                }

                var expectedLocLabel = "$LocLabels:" + update.LocLabelId;
                if (!string.Equals(GetAttribute(controlNode, update.Property), expectedLocLabel, StringComparison.Ordinal))
                {
                    controlNode.SetAttributeValue(update.Property, expectedLocLabel);
                    changed = true;
                }

                var locLabel = GetOrCreateLocLabel(locLabelsNode, update.LocLabelId, ref changed);
                var titles = GetOrCreateDirectChild(locLabel, "Titles");
                foreach (var label in update.Labels)
                {
                    var title = GetOrCreateTitle(titles, label.LanguageCode.ToString(), ref changed);
                    var next = label.Label ?? string.Empty;
                    if (!string.Equals(GetAttribute(title, "description"), next, StringComparison.Ordinal))
                    {
                        title.SetAttributeValue("description", next);
                        changed = true;
                    }
                }
            }

            return changed ? document.ToString(SaveOptions.DisableFormatting) : customizationsXml;
        }

        private static XElement FindControlNodeByUpdate(XElement ribbonDiffXml, RibbonLabelUpdate update)
        {
            foreach (var node in Descendants(ribbonDiffXml).Where(IsControlNode))
            {
                if (!string.Equals(node.Name.LocalName, update.Component, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(GetAttribute(node, "Id"), update.ControlId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(update.Surface) ||
                    string.Equals(DetectSurface(node), update.Surface, StringComparison.OrdinalIgnoreCase))
                {
                    return node;
                }
            }

            return Descendants(ribbonDiffXml)
                .Where(IsControlNode)
                .FirstOrDefault(node => string.Equals(GetAttribute(node, "Id"), update.ControlId, StringComparison.Ordinal));
        }

        private static RibbonSolutionInfo GetSelectedSolution(IOrganizationService serviceAdmin, string solutionId)
        {
            var solutionGuid = ParseSolutionId(solutionId, "Ribbon");
            if (!solutionGuid.HasValue)
            {
                throw new InvalidPluginExecutionException("Please select a solution before loading ribbon labels.");
            }

            var solution = serviceAdmin.Retrieve("solution", solutionGuid.Value, new ColumnSet("solutionid", "uniquename", "friendlyname", "version"));
            var uniqueName = solution.GetAttributeValue<string>("uniquename");
            if (string.IsNullOrWhiteSpace(uniqueName))
            {
                throw new InvalidPluginExecutionException("Selected solution unique name was not found.");
            }

            return new RibbonSolutionInfo
            {
                Id = solutionGuid.Value,
                UniqueName = uniqueName
            };
        }

        private static RibbonEntityInfo GetSelectedEntityInfo(IOrganizationService serviceAdmin, string entityName)
        {
            var logicalName = (entityName ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(logicalName) || string.Equals(logicalName, "none", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidPluginExecutionException("Please select an entity before loading ribbon labels.");
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

            return new RibbonEntityInfo
            {
                LogicalName = metadata.LogicalName ?? logicalName,
                SchemaName = metadata.SchemaName ?? logicalName,
                MetadataId = metadata.MetadataId
            };
        }

        private static RibbonSolutionPackage ExportSolutionPackage(EasyTranslatorRuntimeContext context, string solutionUniqueName)
        {
            try
            {
                var response = (ExportSolutionResponse)context.ServiceAdmin.Execute(new ExportSolutionRequest
                {
                    SolutionName = solutionUniqueName,
                    Managed = false
                });

                var bytes = response?.ExportSolutionFile;
                if (bytes == null || bytes.Length == 0)
                {
                    throw new InvalidPluginExecutionException("ExportSolution did not return ExportSolutionFile.");
                }

                return new RibbonSolutionPackage
                {
                    Bytes = bytes,
                    CustomizationsXml = ReadZipEntry(bytes, CustomizationsFile)
                };
            }
            catch (Exception ex)
            {
                Trace(context, "Ribbon export/read failed. {0}", ex);
                throw new InvalidPluginExecutionException("Ribbon server ZIP processing failed while reading the solution package.", ex);
            }
        }

        private static string ReadZipEntry(byte[] zipBytes, string entryName)
        {
            using (var stream = new MemoryStream(zipBytes))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var entry = archive.GetEntry(entryName);
                if (entry == null)
                {
                    throw new InvalidPluginExecutionException("Exported solution does not contain " + entryName + ".");
                }

                using (var entryStream = entry.Open())
                using (var reader = new StreamReader(entryStream, Encoding.UTF8, true))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static byte[] WriteZipEntry(byte[] zipBytes, string entryName, string content)
        {
            using (var input = new MemoryStream(zipBytes))
            using (var output = new MemoryStream())
            {
                input.CopyTo(output);
                output.Position = 0;

                using (var archive = new ZipArchive(output, ZipArchiveMode.Update, true))
                {
                    var existing = archive.GetEntry(entryName);
                    existing?.Delete();

                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    using (var stream = entry.Open())
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.Write(content ?? string.Empty);
                    }
                }

                return output.ToArray();
            }
        }

        private static XDocument ParseXml(string xml)
        {
            try
            {
                return XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Invalid XML returned from solution export.", ex);
            }
        }

        private static XElement FindEntityNode(XDocument document, string logicalName)
        {
            var expected = (logicalName ?? string.Empty).Trim().ToLowerInvariant();
            return document.Descendants()
                .Where(element => element.Name.LocalName == "Entity")
                .FirstOrDefault(element => string.Equals(GetElementText(element, "Name").ToLowerInvariant(), expected, StringComparison.Ordinal));
        }

        private static XElement GetDirectChild(XElement parent, string localName)
        {
            return parent?.Elements().FirstOrDefault(element => element.Name.LocalName == localName);
        }

        private static XElement GetOrCreateDirectChild(XElement parent, string localName)
        {
            var child = GetDirectChild(parent, localName);
            if (child != null)
            {
                return child;
            }

            child = new XElement(localName);
            parent.Add(child);
            return child;
        }

        private static string GetElementText(XElement parent, string localName)
        {
            return Convert.ToString(GetDirectChild(parent, localName)?.Value ?? string.Empty);
        }

        private static string GetAttribute(XElement element, string name)
        {
            if (element == null)
            {
                return string.Empty;
            }

            foreach (var attribute in element.Attributes())
            {
                if (string.Equals(attribute.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return attribute.Value ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private static IEnumerable<XElement> Descendants(XElement root)
        {
            return root == null ? Enumerable.Empty<XElement>() : root.Descendants();
        }

        private static bool IsControlNode(XElement node)
        {
            var name = node?.Name.LocalName;
            return string.Equals(name, "Button", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "SplitButton", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "FlyoutAnchor", StringComparison.OrdinalIgnoreCase);
        }

        private static string DetectSurface(XElement controlNode)
        {
            var customAction = controlNode?.Ancestors().FirstOrDefault(element => element.Name.LocalName == "CustomAction");
            var location = GetAttribute(customAction, "Location");

            if (location.IndexOf("HomepageGrid", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "main_grid";
            }

            if (location.IndexOf("SubGrid", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "sub_grid";
            }

            if (location.IndexOf("Form", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "form";
            }

            return string.Empty;
        }

        private static string GetSurfaceLabel(string surface)
        {
            if (string.Equals(surface, "main_grid", StringComparison.OrdinalIgnoreCase))
            {
                return "Homepage Grid";
            }

            if (string.Equals(surface, "sub_grid", StringComparison.OrdinalIgnoreCase))
            {
                return "Subgrid";
            }

            if (string.Equals(surface, "form", StringComparison.OrdinalIgnoreCase))
            {
                return "Form";
            }

            return "Ribbon";
        }

        private static string GetLocLabelId(XElement controlNode, string property)
        {
            var value = GetAttribute(controlNode, property);
            return value.StartsWith("$LocLabels:", StringComparison.Ordinal)
                ? value.Substring("$LocLabels:".Length)
                : null;
        }

        private static string GetDefaultLocLabelId(string controlId, string property)
        {
            return string.IsNullOrWhiteSpace(controlId) ? property : controlId + "." + property;
        }

        private static Dictionary<string, Dictionary<string, string>> BuildLocLabelMap(XElement ribbonDiffXml)
        {
            var map = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var locLabel in Descendants(ribbonDiffXml).Where(element => element.Name.LocalName == "LocLabel"))
            {
                var id = GetAttribute(locLabel, "Id");
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (!map.ContainsKey(id))
                {
                    map[id] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                foreach (var title in locLabel.Descendants().Where(element => element.Name.LocalName == "Title"))
                {
                    var language = GetAttribute(title, "languagecode");
                    if (!string.IsNullOrWhiteSpace(language))
                    {
                        map[id][language] = GetAttribute(title, "description");
                    }
                }
            }

            return map;
        }

        private static string GetFirstLabel(Dictionary<string, string> labels, int baseLanguage)
        {
            if (labels == null || labels.Count == 0)
            {
                return string.Empty;
            }

            if (baseLanguage > 0 && labels.ContainsKey(baseLanguage.ToString()) && !string.IsNullOrWhiteSpace(labels[baseLanguage.ToString()]))
            {
                return labels[baseLanguage.ToString()];
            }

            return labels.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static string GetControlPreviewLabel(
            XElement controlNode,
            Dictionary<string, Dictionary<string, string>> locLabels,
            int baseLanguage)
        {
            var labelLocId = GetLocLabelId(controlNode, "LabelText");
            if (!string.IsNullOrWhiteSpace(labelLocId) && locLabels.ContainsKey(labelLocId))
            {
                var preview = GetFirstLabel(locLabels[labelLocId], baseLanguage);
                if (!string.IsNullOrWhiteSpace(preview))
                {
                    return preview;
                }
            }

            var controlId = GetAttribute(controlNode, "Id");
            var parts = (controlId ?? string.Empty).Split('.');
            return parts.Length == 0 ? controlId : parts[parts.Length - 1];
        }

        private static string GetControlNodeText(string surfaceLabel, XElement controlNode, string previewLabel)
        {
            var controlType = controlNode.Name.LocalName;
            return !string.IsNullOrWhiteSpace(previewLabel)
                ? surfaceLabel + " / " + controlType + ": " + previewLabel
                : surfaceLabel + " / " + controlType;
        }

        private static XElement GetOrCreateLocLabel(XElement locLabelsNode, string locLabelId, ref bool changed)
        {
            var locLabel = locLabelsNode.Elements().FirstOrDefault(element =>
                element.Name.LocalName == "LocLabel" &&
                string.Equals(GetAttribute(element, "Id"), locLabelId, StringComparison.Ordinal));
            if (locLabel != null)
            {
                return locLabel;
            }

            locLabel = new XElement("LocLabel", new XAttribute("Id", locLabelId));
            locLabelsNode.Add(locLabel);
            changed = true;
            return locLabel;
        }

        private static XElement GetOrCreateTitle(XElement titlesNode, string language, ref bool changed)
        {
            var title = titlesNode.Elements().FirstOrDefault(element =>
                element.Name.LocalName == "Title" &&
                string.Equals(GetAttribute(element, "languagecode"), language, StringComparison.Ordinal));
            if (title != null)
            {
                return title;
            }

            title = new XElement("Title", new XAttribute("languagecode", language));
            titlesNode.Add(title);
            changed = true;
            return title;
        }

        private static List<int> RetrieveAvailableLanguages(IOrganizationService serviceAdmin)
        {
            var response = (RetrieveAvailableLanguagesResponse)serviceAdmin.Execute(new RetrieveAvailableLanguagesRequest());
            return response.LocaleIds == null ? new List<int>() : response.LocaleIds.ToList();
        }

        private static List<EasyTranslatorLanguageColumnOutput> BuildLanguageColumns(List<int> languages)
        {
            var columns = new List<EasyTranslatorLanguageColumnOutput>();
            foreach (var language in languages ?? new List<int>())
            {
                columns.Add(new EasyTranslatorLanguageColumnOutput
                {
                    field = language.ToString(),
                    text = language.ToString()
                });
            }

            return columns;
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

        private class RibbonSolutionInfo
        {
            public Guid Id { get; set; }
            public string UniqueName { get; set; }
        }

        private class RibbonEntityInfo
        {
            public string LogicalName { get; set; }
            public string SchemaName { get; set; }
            public Guid? MetadataId { get; set; }
        }

        private class RibbonSolutionPackage
        {
            public byte[] Bytes { get; set; }
            public string CustomizationsXml { get; set; }
        }

        private class RibbonLabelUpdate
        {
            public string EntityLogicalName { get; set; }
            public string Surface { get; set; }
            public string Component { get; set; }
            public string ControlId { get; set; }
            public string Property { get; set; }
            public string LocLabelId { get; set; }
            public List<EasyTranslatorLabelChange> Labels { get; set; } = new List<EasyTranslatorLabelChange>();
        }
    }
}
