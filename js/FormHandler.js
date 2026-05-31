(function (FormHandler, undefined) {
    "use strict";

    FormHandler.selectedForms = null;
    FormHandler.formsByLanguage = null;
    FormHandler.loadedFormsData = null;
    FormHandler.lastId = null;

    function GetParsedForm (form) {
        var parser = new DOMParser();
        var formXml = parser.parseFromString(form.formxml, "text/xml");

        return formXml;
    }

    function NodesWithIdAndLabels (node) {
        if (node.id && node.getElementsByTagName("labels").length > 0 && node.getElementsByTagName("control").length > 0) {
            return NodeFilter.FILTER_ACCEPT;
        }
        return NodeFilter.FILTER_SKIP;
    }

    function CreateTreeWalker(elementFilter, form, formXml) {
        if (!formXml) {
            formXml = GetParsedForm(form);
        }
        var treeWalker = document.createTreeWalker(formXml, NodeFilter.SHOW_ALL, elementFilter, false);

        return treeWalker;
    }

    function TraverseTree (treeWalker, tree) {
        // Dive down
        var child = CreateGridNode(treeWalker.firstChild());

        if (!child) {
            return;
        }

        // Push each first child per level
        tree.push(child);
        TraverseTree(treeWalker, child.w2ui.children);

        // We'll dive up level to level now and add all siblings
        while (treeWalker.nextSibling()) {
            var sibling = CreateGridNode(treeWalker.currentNode);
            tree.push(sibling);
            TraverseTree(treeWalker, sibling.w2ui.children);
        }

        treeWalker.parentNode();
    }

    function GetUpdates(records) {
        var updates = [];

        for (var i = 0; i < records.length; i++) {
            var record = records[i];

            if (record.w2ui && record.w2ui.changes) {
                var changes = record.w2ui.changes;

                var labels = [];

                for (var change in changes) {
                    if (!changes.hasOwnProperty(change)) {
                        continue;
                    }

                    // Skip empty labels
                    if (!changes[change]) {
                        continue;
                    }

                    var label = { LanguageCode: change, Text: changes[change] };
                    labels.push(label);
                }

                if (labels.length < 1) {
                    continue;
                }

                updates.push({
                    id: record.recid,
                    labels: labels
                });
            }
        }

        return updates;
    }

    function ApplyLocalizedLabelChanges(changes, labels) {
        labels = labels || [];

        for (var change in changes) {
            if (!changes.hasOwnProperty(change) || !changes[change]) {
                continue;
            }

            var found = false;
            for (var i = 0; i < labels.length; i++) {
                if (labels[i].LanguageCode == change) {
                    labels[i].Label = changes[change];
                    labels[i].HasChanged = true;
                    found = true;
                    break;
                }
            }

            if (!found) {
                labels.push({
                    LanguageCode: parseInt(change, 10),
                    Label: changes[change]
                });
            }
        }

        return labels;
    }

    function GetLabels(node) {
        var labelsNode = null;
        var children = node.children;

        for (var i = 0; i < children.length; i++) {
            var child = children[i];

            if (child && child.tagName === "labels") {
                labelsNode = child;
                break;
            }
        }

        return labelsNode;
    }

    function AttachLabels(node, gridNode) {
        if (!node) {
            return;
        }

        var labels = GetLabels(node);

        if (!labels) {
            return;
        }

        for (var i = 0; i < labels.children.length; i++) {
            var label = labels.children[i];

            var text = label.attributes["description"].value;
            var languageCode = label.attributes["languagecode"].value;

            gridNode[languageCode] = text;
        }
    }

    function CreateGridNode (node) {
        if (!node) {
            return null;
        }

        var attributes = node.attributes;
        var name = "";

        if (attributes["name"]) {
            name = attributes["name"].value;
        }
        else {
            // var nodeid =  attributes["id"].value;
            name = node.tagName;
        }

        var gridNode = {
            recid: node.id,
            schemaName: name,
            w2ui: {
                children: []
            }
        };

        AttachLabels(node, gridNode);

        for (var i = 0; i < FormHandler.selectedForms.length; i++) {
            var translatedNode = GetById(node.id, FormHandler.selectedForms[i]);
            AttachLabels(translatedNode, gridNode);
        }

        return gridNode;
    }

    function FillTable () {
        var grid = XrmTranslator.GetGrid();
        grid.clear();

        var records = [];

        var treeWalker = CreateTreeWalker(NodesWithIdAndLabels, XrmTranslator.metadata);
        TraverseTree(treeWalker, records);

        XrmTranslator.AddSummary(records, true);
        grid.add(records);
        grid.unlock();
        XrmTranslator.EnableLoadAndSave();
    }

    function FillAllFormsTable(allFormData) {
        var grid = XrmTranslator.GetGrid();
        var records = [];
        var loadedFormsData = [];

        grid.clear();

        for (var i = 0; i < allFormData.length; i++) {
            var formData = allFormData[i];
            var prefix = getFormPrefix(formData.formId);
            var formRecords = deepClone(formData.records || []);
            var formChildren = [];

            prefixRecords(formRecords, prefix);
            formChildren.push(createFormDescriptionRecord(formData, prefix));
            formChildren = formChildren.concat(formRecords);

            loadedFormsData.push({
                formId: formData.formId,
                formName: formData.formName,
                metadata: deepClone(formData.metadata),
                selectedForms: formData.selectedForms ? formData.selectedForms.slice() : [],
                descriptionLabels: deepClone(formData.descriptionLabels),
                prefix: prefix
            });

            records.push({
                recid: prefix + "_group",
                schemaName: GetFormDisplayText({
                    formid: formData.formId,
                    name: formData.formName,
                    type: formData.formType,
                    objecttypecode: formData.metadata ? formData.metadata.objecttypecode : ""
                }, {
                    0: "Dashboard",
                    2: "Main",
                    5: "Mobile Express",
                    6: "Quick View",
                    7: "Quick Create",
                    10: "App Module Main",
                    11: "Interactive Experience",
                    12: "Card"
                }),
                _isGroupNode: true,
                _isFormGroupNode: true,
                w2ui: {
                    children: formChildren,
                    editable: false,
                    hideCheckBox: true
                }
            });
        }

        FormHandler.loadedFormsData = loadedFormsData;

        grid.add(records);
        grid.unlock();
        XrmTranslator.EnableLoadAndSave();
    }

    function GetUserLanguageForm (forms) {
        for (var i = 0; i < forms.length; i++) {
            if (forms[i].languageCode === XrmTranslator.userSettings.uilanguageid) {
                return forms[i];
            }
        }

        return null;
    }

    function ProcessSelection(formId) {
        var formsByLanguage = FormHandler.formsByLanguage;
        var userLanguageForms = GetUserLanguageForm(formsByLanguage).forms.value;

        for (var i = 0; i < userLanguageForms.length; i++) {
            var languageForm = userLanguageForms[i];

            if (languageForm.formid === formId) {
                XrmTranslator.metadata = languageForm;
                break;
            }
        }

        FormHandler.selectedForms = [];
        for (var i = 0; i < formsByLanguage.length; i++) {
            var languageForms = formsByLanguage[i];

            for (var j = 0; j < languageForms.forms.value.length; j++) {
                var languageForm = languageForms.forms.value[j];

                if (languageForm.formid === formId) {
                    FormHandler.selectedForms.push(languageForm);
                    break;
                }
            }
        }

        FormHandler.loadedFormsData = null;
        FillTable();
    }

    // Expose ProcessSelection for AllInOneHandler
    FormHandler.ProcessSelection = ProcessSelection;

    function GetCleanText(value) {
        if (value === null || typeof value === "undefined") {
            return "";
        }

        return String(value).trim();
    }

    function deepClone(obj) {
        return JSON.parse(JSON.stringify(obj));
    }

    function getFormPrefix(formId) {
        return "form~" + formId + "~";
    }

    function createFormDescriptionRecord(formData, prefix) {
        var labels = formData.descriptionLabels && formData.descriptionLabels.Label
            ? formData.descriptionLabels.Label.LocalizedLabels
            : [];

        var record = {
            recid: prefix + "description",
            schemaName: "[Form] Description",
            _isFormDescriptionRow: true,
            _formId: formData.formId
        };

        for (var i = 0; i < labels.length; i++) {
            record[labels[i].LanguageCode.toString()] = labels[i].Label;
        }

        return record;
    }

    function prefixRecords(records, prefix) {
        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            record.recid = prefix + record.recid;

            if (record.w2ui && record.w2ui.children) {
                prefixRecords(record.w2ui.children, prefix);
            }
        }
    }

    function stripPrefix(recid, prefix) {
        if (recid && recid.indexOf(prefix) === 0) {
            return recid.substring(prefix.length);
        }

        return recid;
    }

    function stripPrefixFromRecords(records, prefix) {
        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            record.recid = stripPrefix(record.recid, prefix);

            if (record.w2ui && record.w2ui.children) {
                stripPrefixFromRecords(record.w2ui.children, prefix);
            }
        }
    }

    function hasChanges(records) {
        return records.some(function(record) {
            if (record.w2ui && record.w2ui.changes && Object.keys(record.w2ui.changes).length > 0) {
                return true;
            }

            if (record.w2ui && record.w2ui.children) {
                return hasChanges(record.w2ui.children);
            }

            return false;
        });
    }

    function saveFormDescriptionRows(formData, descriptionRows) {
        var changedRows = descriptionRows.filter(function(row) {
            return row.w2ui && row.w2ui.changes;
        });

        if (changedRows.length === 0) {
            return WebApiClient.Promise.resolve();
        }

        var labelsResponse = formData.descriptionLabels || { Label: { LocalizedLabels: [] } };
        if (!labelsResponse.Label) {
            labelsResponse.Label = { LocalizedLabels: [] };
        }
        if (!labelsResponse.Label.LocalizedLabels) {
            labelsResponse.Label.LocalizedLabels = [];
        }

        for (var i = 0; i < changedRows.length; i++) {
            ApplyLocalizedLabelChanges(changedRows[i].w2ui.changes, labelsResponse.Label.LocalizedLabels);
        }

        XrmTranslator.LockGridProgress("Saving 3. Forms", 1, 1);

        return WebApiClient.Execute(WebApiClient.Requests.SetLocLabelsRequest.with({
            payload: {
                Labels: labelsResponse.Label.LocalizedLabels,
                EntityMoniker: {
                    "@odata.type": "Microsoft.Dynamics.CRM.systemform",
                    formid: formData.formId
                },
                AttributeName: "description"
            }
        }))
        .then(function() {
            if (XrmTranslator.GetEntity().toLowerCase() === "none") {
                return XrmTranslator.AddToSolution([formData.formId], XrmTranslator.ComponentType.SystemForm, true, true);
            }

            return XrmTranslator.AddToSolution([formData.formId], XrmTranslator.ComponentType.SystemForm);
        });
    }

    FormHandler.CreateDescriptionRecord = createFormDescriptionRecord;
    FormHandler.SaveDescriptionRows = saveFormDescriptionRows;

    function isFormGroupGrid(records) {
        return records.some(function(record) {
            return record && record._isFormGroupNode;
        });
    }

    function IsNoneEntity(entityName) {
        return !entityName || String(entityName).toLowerCase() === "none";
    }

    function IsDashboardMode() {
        return XrmTranslator.GetType && XrmTranslator.GetType() === "dashboards";
    }

    function GetFormQuery(entityName) {
        if (IsDashboardMode()) {
            return "?$filter=formactivationstate eq 1 and iscustomizable/Value eq true and (type eq 0 or type eq 10)";
        }

        if (IsNoneEntity(entityName)) {
            return null;
        }

        return "?$filter=objecttypecode eq '" + entityName.toLowerCase() + "' and iscustomizable/Value eq true and formactivationstate eq 1 and type ne 0 and type ne 10";
    }

    function GetSelectedSolutionFormIds() {
        var solutionId = XrmTranslator.GetSolution();

        if (!solutionId || solutionId === "all") {
            return WebApiClient.Promise.resolve([]);
        }

        return WebApiClient.Retrieve({
            entityName: "solutioncomponent",
            queryParams: "?$select=objectid&$filter=_solutionid_value eq " + solutionId + " and componenttype eq " + XrmTranslator.ComponentType.SystemForm
        })
        .then(function(response) {
            return (response.value || []).map(function(component) {
                return component.objectid;
            });
        });
    }

    function GetFormQueryForLoad(entityName) {
        if (!IsDashboardMode()) {
            return WebApiClient.Promise.resolve(GetFormQuery(entityName));
        }

        return GetSelectedSolutionFormIds()
        .then(function(formIds) {
            if (formIds.length === 0) {
                return null;
            }

            var idFilter = formIds.map(function(id) {
                return "formid eq " + id;
            }).join(" or ");

            return "?$filter=formactivationstate eq 1 and iscustomizable/Value eq true and (type eq 0 or type eq 10) and (" + idFilter + ")";
        });
    }

    function GetFormDisplayText(form, formTypeMap) {
        var formTypeName = formTypeMap[form.type] || ("Type " + form.type);
        var name = GetCleanText(form.name) || GetCleanText(form.objecttypecode) || "Unnamed Form";

        return name + " [" + formTypeName + "]";
    }

    // Load all forms without showing picker dialog — used by AllInOneHandler
    FormHandler.LoadAllForms = function () {
        var entityName = XrmTranslator.GetEntity();

        var formTypeMap = {
            0: "Dashboard",
            2: "Main",
            5: "Mobile Express",
            6: "Quick View",
            7: "Quick Create",
            10: "App Module Main",
            11: "Interactive Experience",
            12: "Card"
        };

        return GetFormQueryForLoad(entityName)
        .then(function(query) {
            if (!query) {
                if (IsDashboardMode()) {
                    return [];
                }

                XrmTranslator.UnlockGrid();
                return DialogHelper.alert("Please select an entity before loading forms.");
            }

            var formRequest = {
                entityName: "systemform",
                queryParams: query
            };

            var languages = XrmTranslator.installedLanguages.LocaleIds;
            var initialLanguage = XrmTranslator.userSettings.uilanguageid;
            var requests = [];

            for (var i = 0; i < languages.length; i++) {
                requests.push({
                    action: "Update",
                    language: languages[i]
                });

                requests.push({
                    action: "Retrieve",
                    language: languages[i]
                });
            }

            requests.push({
                action: "Update",
                language: initialLanguage
            });

            return WebApiClient.Promise.reduce(requests, function(total, request){
                if (request.action === "Update") {
                    return WebApiClient.Update({
                        overriddenSetName: "usersettingscollection",
                        entityId: XrmTranslator.userId,
                        entity: { uilanguageid: request.language }
                    })
                    .then(function(response) {
                        return total;
                    });
                }
                else if (request.action === "Retrieve") {
                    return WebApiClient.Promise.props({
                        forms: WebApiClient.Retrieve(formRequest),
                        languageCode: request.language
                    })
                    .then(function (response) {
                        total.push(response);
                        return total;
                    });
                }
            }, []);
        })
        .then(function(responses) {
            if (!responses || responses.length === 0) {
                FormHandler.formsByLanguage = [];
                return [];
            }

            FormHandler.formsByLanguage = responses;

            // Process all forms and return per-form data
            var userLanguageForms = GetUserLanguageForm(responses).forms.value;
            var allFormData = [];

            return WebApiClient.Promise.resolve(userLanguageForms)
            .each(function(form) {
                ProcessSelection(form.formid);

                var grid = XrmTranslator.GetGrid();
                var records = grid.records.filter(function(r) { return !r.w2ui || !r.w2ui.summary; });
                var retrieveDescriptionLabelsRequest = WebApiClient.Requests.RetrieveLocLabelsRequest
                    .with({
                        urlParams: {
                            EntityMoniker: "{'@odata.id':'systemforms(" + form.formid + ")'}",
                            AttributeName: "'description'",
                            IncludeUnpublished: true
                        }
                    });

                return WebApiClient.Execute(retrieveDescriptionLabelsRequest)
                .then(function(descriptionLabels) {
                    allFormData.push({
                        formId: form.formid,
                        formName: form.name || form.formid,
                        formType: form.type,
                        formTypeName: formTypeMap[form.type] || ("Type " + form.type),
                        metadata: JSON.parse(JSON.stringify(XrmTranslator.metadata)),
                        selectedForms: FormHandler.selectedForms ? FormHandler.selectedForms.slice() : [],
                        descriptionLabels: descriptionLabels,
                        records: JSON.parse(JSON.stringify(records))
                    });
                });
            })
            .then(function() {
                return allFormData;
            });
        })
        .catch(XrmTranslator.errorHandler);
    };

    function IdFilter (node) {
        if (node.id == this.id) {
            return NodeFilter.FILTER_ACCEPT;
        }
        return NodeFilter.FILTER_SKIP;
    }

    function GetById (id, form, formXml) {
        var treeWalker = CreateTreeWalker(IdFilter.bind({ id: id }), form, formXml);

        return treeWalker.nextNode();
    }

    function ApplyLabelUpdates (labels, updates, formXml) {
        for (var i = 0; i < updates.length; i++) {
            var update = updates[i];

            for (var j = 0; j < labels.children.length; j++) {
                var label = labels.children[j];

                if (update.LanguageCode === label.attributes["languagecode"].value) {
                    label.attributes["description"].value = update.Text;
                }
                // We did not find it
                else if (j === labels.children.length - 1) {
                    var newLabel = formXml.createElement("label");

                    newLabel.setAttribute("description", update.Text);
                    newLabel.setAttribute("languagecode", update.LanguageCode);

                    labels.appendChild(newLabel);
                }
            }
        }
    }

    function SerializeXml(formXml) {
        var serializer = new XMLSerializer();

        return serializer.serializeToString(formXml);
    }

    function ApplyUpdates(updates, form, formXml) {
        for (var i = 0; i < updates.length; i++) {
            var update = updates[i];

            var node = GetById(update.id, form, formXml);
            var labels = GetLabels(node);

            ApplyLabelUpdates(labels, update.labels, formXml);
        }

        var serialized = SerializeXml(formXml);

        return {
            formxml: serialized
        };
    }

    function uuidv4() {
        return ([1e7]+-1e3+-4e3+-8e3+-1e11).replace(/[018]/g, c =>
            (c ^ crypto.getRandomValues(new Uint8Array(1))[0] & 15 >> c / 4).toString(16)
        );
    }

    FormHandler.RemoveOverriddenCellLabels = function() {
        if (!XrmTranslator.metadata || !XrmTranslator.metadata.formid) {
            return;
        }

        return DialogHelper.confirm(
            "This will remove ALL overridden attribute labels on this form and reset them to the default attribute labels. This action cannot be undone.\n\nDo you want to continue?",
            {
                title: "Remove Overridden Labels",
                width: 620,
                height: 270,
                popupClass: "xqt-remove-overridden-confirm"
            }
        )
        .then(function(confirmed) {
            if (!confirmed) {
                return;
            }

            var formXml = GetParsedForm(XrmTranslator.metadata);

            Array.from(formXml.getElementsByTagName("cell"))
            .forEach(function(c) {
                const control = c.getElementsByTagName("control");

                // We only want to fix overridden labels for attributes, all attribute controls have a datafieldname
                if (!control || !control.length || !control[0].getAttribute("datafieldname")) {
                    return;
                }

                // Regenerate cell id so that MS can't find the old overridden labels
                c.id = uuidv4();

                const labels = c.getElementsByTagName("labels");

                if(labels && labels.length) {
                    const labelsNode = labels[0];

                    // Remove all labels
                    Array.from(labelsNode.getElementsByTagName("label")).forEach(function(l) { labelsNode.removeChild(l); });
                }
            });

            const serializer = new XMLSerializer();
            const payload = {
                formxml: serializer.serializeToString(formXml)
            };

            return FormHandler.Save(payload)
            .then(function() { return DialogHelper.alert("Successfully removed all cell labels!"); })
            .catch(function(e) { return DialogHelper.alert("Failed to remove cell labels: " + e.message, { title: "Error" }); });
        });
    };

    FormHandler.Load = function () {
        var entityName = XrmTranslator.GetEntity();
        var query = GetFormQuery(entityName);

        if (!query) {
            XrmTranslator.UnlockGrid();
            return DialogHelper.alert("Please select an entity before loading forms.");
        }

        return FormHandler.LoadAllForms()
        .then(function(allFormData) {
            FormHandler.lastId = null;
            FillAllFormsTable(allFormData || []);
        })
        .catch(XrmTranslator.errorHandler);
    }

    function SaveLoadedFormsOnly(skipLanguageScope) {
        var grid = XrmTranslator.GetGrid();
        var originalRecords = grid.records;
        var originalTotal = grid.total;
        var formGroups = deepClone(grid.records.filter(function(record) {
            return record._isFormGroupNode && (!record.w2ui || !record.w2ui.parent_recid);
        }));

        var executeSave = function () {
            var chain = WebApiClient.Promise.resolve();

            for (var i = 0; i < FormHandler.loadedFormsData.length; i++) {
                (function(formData) {
                    chain = chain.then(function() {
                        var group = formGroups.find(function(record) {
                            return record.recid === formData.prefix + "_group";
                        });

                        if (!group || !group.w2ui || !group.w2ui.children || !hasChanges(group.w2ui.children)) {
                            return;
                        }

                        var descriptionRows = deepClone(group.w2ui.children.filter(function(record) {
                            return record._isFormDescriptionRow;
                        }));
                        var formRecords = deepClone(group.w2ui.children.filter(function(record) {
                            return !record._isFormDescriptionRow;
                        }));
                        stripPrefixFromRecords(formRecords, formData.prefix);

                        var saveChain = saveFormDescriptionRows(formData, descriptionRows);

                        if (hasChanges(formRecords)) {
                            saveChain = saveChain.then(function() {
                                XrmTranslator.metadata = deepClone(formData.metadata);
                                FormHandler.selectedForms = formData.selectedForms ? formData.selectedForms.slice() : [];

                                grid.records = formRecords;
                                grid.total = formRecords.length;

                                return FormHandler.SaveOnly(true);
                            });
                        }

                        return saveChain;
                    });
                })(FormHandler.loadedFormsData[i]);
            }

            return chain.then(function() {
                grid.records = originalRecords;
                grid.total = originalTotal;
            });
        };

        if (skipLanguageScope) {
            return executeSave();
        }

        return XrmTranslator.RunAsBaseLanguage(executeSave);
    }

    FormHandler.SaveOnly = function(skipLanguageScope) {
        if (FormHandler.loadedFormsData && isFormGroupGrid(XrmTranslator.GetGrid().records)) {
            return SaveLoadedFormsOnly(skipLanguageScope);
        }

        var records = XrmTranslator.GetAllRecords();
        var formXml = GetParsedForm(XrmTranslator.metadata);
        var updates = GetUpdates(records);

        if (updates.length === 0) {
            return WebApiClient.Promise.resolve();
        }

        var update = ApplyUpdates(updates, XrmTranslator.metadata, formXml);

        var executeSave = function () {
            XrmTranslator.LockGridProgress("Saving 3. Forms", 1, 1);
            return WebApiClient.Update({
                entityName: "systemform",
                entityId: XrmTranslator.metadata.formid,
                entity: update
            })
            .then(function () {
                if (XrmTranslator.GetEntity().toLowerCase() === "none") {
                    return XrmTranslator.AddToSolution([XrmTranslator.metadata.formid], XrmTranslator.ComponentType.SystemForm, true, true);
                }
                else {
                    return XrmTranslator.AddToSolution([XrmTranslator.metadata.formid], XrmTranslator.ComponentType.SystemForm);
                }
            });
        };

        if (skipLanguageScope) {
            return executeSave();
        }

        return XrmTranslator.RunAsBaseLanguage(function () {
            return executeSave();
        });
    }

    FormHandler.Save = function(payload) {
        return XrmTranslator.RunTypeSaveFlow({
            saveAction: function () {
                if (!payload) {
                    return FormHandler.SaveOnly();
                }

                // Direct payload from RemoveOverriddenCellLabels.
                return XrmTranslator.RunAsBaseLanguage(function () {
                    XrmTranslator.LockGridProgress("Saving 3. Forms", 1, 1);
                    return WebApiClient.Update({
                        entityName: "systemform",
                        entityId: XrmTranslator.metadata.formid,
                        entity: payload
                    });
                })
                .then(function () {
                    if (XrmTranslator.GetEntity().toLowerCase() === "none") {
                        return XrmTranslator.AddToSolution([XrmTranslator.metadata.formid], XrmTranslator.ComponentType.SystemForm, true, true);
                    }

                    return XrmTranslator.AddToSolution([XrmTranslator.metadata.formid], XrmTranslator.ComponentType.SystemForm);
                });
            },
            publishAction: function () {
                var entityName = XrmTranslator.GetEntity();
                if (entityName.toLowerCase() === "none") {
                    return XrmTranslator.PublishDashboard([{ recid: XrmTranslator.metadata.formid }]);
                }

                return XrmTranslator.Publish();
            },
            reloadAction: function () {
                FormHandler.lastId = XrmTranslator.metadata.formid;
                return FormHandler.Load();
            }
        });
    }
} (window.FormHandler = window.FormHandler || {}));
