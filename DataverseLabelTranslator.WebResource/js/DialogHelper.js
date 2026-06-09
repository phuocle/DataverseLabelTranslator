(function (DialogHelper, undefined) {
    "use strict";

    var icons = {
        alert: '<span class="xqt-dialog-icon xqt-dialog-icon-alert">&#9432;</span>',
        confirm: '<span class="xqt-dialog-icon xqt-dialog-icon-confirm">&#9888;</span>',
        question: ""
    };

    function escapeHtml(text) {
        return text.replace(/[&<>\n]/g, function (m) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", "\n": "<br/>" }[m];
        });
    }

    function buildBody(type, message) {
        return (
            '<div class="xqt-dialog-body xqt-dialog-body-' +
            type +
            '">' +
            (icons[type] || "") +
            '<span class="xqt-dialog-message">' +
            escapeHtml(message) +
            "</span>" +
            "</div>"
        );
    }

    function buildButton(text, onclickValue) {
        return (
            '<button class="w2ui-btn" onclick="DialogHelper._resolve(' +
            onclickValue +
            ');">' +
            escapeHtml(text) +
            "</button>"
        );
    }

    function openDialog(type, title, message, buttonsHtml, options, onResolve) {
        options = options || {};
        var result;

        return new Promise(function (resolve) {
            w2popup.open({
                title: title,
                body: buildBody(type, message),
                buttons: buttonsHtml,
                width: options.width || 450,
                height: options.height || 220,
                modal: true,
                showClose: true,
                showMax: false,
                style: options.style || "",
                onOpen: function (event) {
                    event.onComplete = function () {
                        var popup = document.querySelector("#w2ui-popup");
                        if (!popup) {
                            return;
                        }

                        popup.classList.add("xqt-dialog-popup");
                        if (options.popupClass) {
                            popup.classList.add(options.popupClass);
                        }
                    };
                },
                onClose: function () {
                    DialogHelper._resolve = null;
                    resolve(result);
                }
            });

            DialogHelper._resolve = function (value) {
                result = onResolve(value);
                w2popup.close();
            };
        });
    }

    function openHtmlDialog(options) {
        options = options || {};

        w2popup.open({
            title: options.title || "",
            body: options.body || "",
            buttons: options.buttons || "",
            width: options.width || 600,
            height: options.height || 400,
            modal: true,
            showClose: true,
            showMax: false,
            onOpen: function (event) {
                if (!options.maximizeOnOpen) {
                    return;
                }

                event.onComplete = function () {
                    setTimeout(function () {
                        w2popup.max();
                    }, 100);
                };
            }
        });
    }

    DialogHelper.alert = function (message, options) {
        return openDialog(
            "alert",
            (options && options.title) || "Information",
            message,
            buildButton("OK", ""),
            options,
            function () {
                return undefined;
            }
        );
    };

    DialogHelper.confirm = function (message, options) {
        options = options || {};
        return openDialog(
            "confirm",
            options.title || "Confirm",
            message,
            buildButton(options.noText || "No", "false") + " " + buildButton(options.yesText || "Yes", "true"),
            options,
            function (value) {
                return value;
            }
        );
    };

    DialogHelper.question = function (message, buttons, options) {
        var buttonHtml = buttons
            .map(function (btn, index) {
                return buildButton(btn.text, index);
            })
            .join(" ");

        return openDialog(
            "question",
            (options && options.title) || "Question",
            message,
            buttonHtml,
            options,
            function (index) {
                return buttons[index].value;
            }
        );
    };

    function getFindAndReplaceColumnItems(columns) {
        var languageItems = [];

        for (var i = 0; i < columns.length; i++) {
            if (columns[i].field === "schemaName") {
                continue;
            }

            languageItems.push({ id: columns[i].field, text: columns[i].text });
        }

        return languageItems;
    }

    DialogHelper.ShowFindAndReplaceDialog = function (columns, onSubmit) {
        var languageItems = getFindAndReplaceColumnItems(columns || []);

        if (!w2ui.findAndReplace) {
            new w2form({
                name: "findAndReplace",
                style: "border: 0px; background-color: transparent;",
                formHTML:
                    '<div class="w2ui-page page-0 xqt-find-replace-form">' +
                    '    <div class="xqt-find-replace-field">' +
                    '        <label>Replace in Column <span class="xqt-required">*</span></label>' +
                    '        <input name="column" type="list"/>' +
                    "    </div>" +
                    '    <div class="xqt-find-replace-field">' +
                    '        <label>Find <span class="xqt-required">*</span></label>' +
                    '        <input name="find" type="text"/>' +
                    "    </div>" +
                    '    <div class="xqt-find-replace-field">' +
                    '        <label>Replace <span class="xqt-required">*</span></label>' +
                    '        <input name="replace" type="text"/>' +
                    "    </div>" +
                    '    <div class="xqt-find-replace-options">' +
                    '        <label><input name="regex" type="checkbox"/> Use Regex</label>' +
                    '        <label><input name="ignoreCase" type="checkbox"/> Ignore Case</label>' +
                    '        <label><input name="selectRecords" type="checkbox"/> Select Records</label>' +
                    "    </div>" +
                    "</div>" +
                    '<div class="w2ui-buttons xqt-find-replace-buttons">' +
                    '    <button class="w2ui-btn" name="cancel">Cancel</button>' +
                    '    <button class="w2ui-btn" name="ok">Ok</button>' +
                    "</div>",
                fields: [
                    { field: "find", type: "text", required: true },
                    { field: "replace", type: "text", required: true },
                    { field: "regex", type: "checkbox", required: true },
                    { field: "ignoreCase", type: "checkbox", required: true },
                    { field: "selectRecords", type: "checkbox", required: false },
                    { field: "column", type: "list", required: true, options: { items: languageItems } }
                ],
                actions: {
                    ok: function () {
                        var errors = this.validate();
                        if (errors.length > 0 || !this.record.column) {
                            return;
                        }

                        w2popup.close();
                        onSubmit({
                            find: this.record.find,
                            replace: this.record.replace,
                            regex: this.record.regex,
                            ignoreCase: this.record.ignoreCase,
                            columnId: this.record.column.id,
                            columnText: this.record.column.text,
                            selectRecords: this.record.selectRecords
                        });
                    },
                    cancel: function () {
                        w2popup.close();
                    }
                }
            });
        } else {
            var columnField = w2ui.findAndReplace.fields.find(function (field) {
                return field.field === "column";
            });

            if (columnField) {
                columnField.options.items = languageItems;
            }

            w2ui.findAndReplace.refresh();
        }

        w2popup.open({
            title: "Find and Replace",
            name: "findAndReplacePopup",
            body: '<div id="form" class="xqt-find-replace-popup-form"></div>',
            style: "padding: 0",
            width: 720,
            height: 330,
            showMax: false,
            onToggle: function (event) {
                w2ui.findAndReplace.box.style.display = "none";
                event.onComplete = function () {
                    w2ui.findAndReplace.box.style.display = "";
                    w2ui.findAndReplace.resize();
                };
            },
            onOpen: function (event) {
                event.onComplete = function () {
                    var popup = document.querySelector("#w2ui-popup");
                    if (popup) {
                        popup.classList.add("xqt-find-replace-popup");
                    }
                    w2ui.findAndReplace.render("#w2ui-popup #form");
                };
            },
            onClose: function () {
                var popup = document.querySelector("#w2ui-popup");
                if (popup) {
                    popup.classList.remove("xqt-find-replace-popup");
                }
            }
        });
    };

    DialogHelper.ShowFindAndReplaceResults = function (results, onApply) {
        if (!w2ui.findAndReplaceGrid) {
            new w2grid({
                name: "findAndReplaceGrid",
                show: { selectColumn: true },
                multiSelect: true,
                columns: [
                    { field: "schemaName", text: "Schema Name", size: "25%", sortable: true, searchable: true },
                    { field: "column", text: "Column LCID", sortable: true, searchable: true, hidden: true },
                    { field: "columnName", text: "Column", size: "25%", sortable: true, searchable: true },
                    { field: "current", text: "Current Text", size: "25%", sortable: true, searchable: true },
                    {
                        field: "replaced",
                        text: "Replaced Text",
                        size: "25%",
                        sortable: true,
                        searchable: true,
                        editable: { type: "text" }
                    }
                ],
                records: []
            });
        }

        w2ui.findAndReplaceGrid.clear();
        w2ui.findAndReplaceGrid.add(results);

        DialogHelper._applyFindAndReplaceResults = function () {
            onApply(w2ui.findAndReplaceGrid.getSelection(), w2ui.findAndReplaceGrid.records);
            w2popup.close();
        };

        w2popup.open({
            title: "Apply Find and Replace",
            buttons:
                '<button class="w2ui-btn" onclick="w2popup.close();">Cancel</button> ' +
                '<button class="w2ui-btn" onclick="DialogHelper._applyFindAndReplaceResults();">Apply</button>',
            width: 900,
            height: 600,
            showMax: true,
            body: '<div id="main" style="position: absolute; left: 5px; top: 5px; right: 5px; bottom: 5px;"></div>',
            onOpen: function (event) {
                event.onComplete = function () {
                    w2ui.findAndReplaceGrid.render("#w2ui-popup #main");
                    w2ui.findAndReplaceGrid.selectAll();
                };
            },
            onClose: function () {
                DialogHelper._applyFindAndReplaceResults = null;
            },
            onToggle: function (event) {
                w2ui.findAndReplaceGrid.box.style.display = "none";
                event.onComplete = function () {
                    w2ui.findAndReplaceGrid.box.style.display = "";
                    w2ui.findAndReplaceGrid.resize();
                };
            }
        });
    };

    DialogHelper.ShowApplyDictionaryPrompt = function (onApply) {
        var applyModeItems = [
            { id: "overwrite", text: "All Overwrite" },
            { id: "missing", text: "All Missing" }
        ];

        if (w2ui.applyDictionaryPrompt) {
            w2ui.applyDictionaryPrompt.destroy();
        }

        if (!w2ui.applyDictionaryPrompt) {
            new w2form({
                name: "applyDictionaryPrompt",
                style: "border: 0px; background-color: transparent;",
                formHTML:
                    '<div class="w2ui-page page-0 xqt-apply-dictionary-form">' +
                    '    <p class="xqt-apply-dictionary-description">Apply existing dictionary entries to all matching records in the current grid.</p>' +
                    '    <div class="xqt-apply-dictionary-row">' +
                    '        <label class="xqt-apply-dictionary-label" for="applyMode">Mode:</label>' +
                    '        <div class="xqt-apply-dictionary-control"><input name="applyMode" type="list" /></div>' +
                    "    </div>" +
                    "</div>" +
                    '<div class="w2ui-buttons">' +
                    '    <button class="w2ui-btn" name="cancel">Cancel</button>' +
                    '    <button class="w2ui-btn" name="ok">Ok</button>' +
                    "</div>",
                fields: [{ field: "applyMode", type: "list", required: true, options: { items: applyModeItems } }],
                record: {
                    applyMode: applyModeItems[0]
                },
                actions: {
                    ok: function () {
                        if (this.validate().length > 0) return;
                        var mode = this.record.applyMode ? this.record.applyMode.id : "overwrite";
                        w2popup.close();
                        onApply(mode);
                    },
                    cancel: function () {
                        w2popup.close();
                    }
                }
            });
        }

        w2popup.open({
            title: "Apply Dictionary",
            name: "applyDictionaryPopup",
            body: '<div id="form" class="xqt-apply-dictionary-popup-form"></div>',
            style: "padding: 0px; overflow-x: hidden;",
            width: 620,
            height: 230,
            showMax: false,
            onOpen: function (event) {
                event.onComplete = function () {
                    w2ui.applyDictionaryPrompt.render("#w2ui-popup #form");
                    w2ui.applyDictionaryPrompt.resize();
                };
            }
        });
    };

    DialogHelper.ShowAbout = function () {
        var html =
            '<div style="padding: 25px 30px; font-size: 16px; line-height: 1.6; text-align: center;">' +
            '<h2 style="margin: 0 0 10px 0; font-size: 26px; font-weight: 600;">Dataverse Label Translator</h2>' +
            '<p style="margin: 0 0 10px 0; color: #777; font-size: 15px;">Version: 1.0.0.0</p>' +
            '<p style="margin: 0 0 15px 0; color: #777; font-size: 15px;">Translation dashboard for Microsoft Dataverse labels.</p>' +
            '<hr style="border: none; border-top: 1px solid #eaeaea; margin: 20px 0;">' +
            '<p style="text-align: justify; text-align-last: center; font-size: 15px; margin: 0; color: #444;">Developed by ' +
            '<a href="https://github.com/phuocle" target="_blank" rel="noopener noreferrer" style="font-weight: 500; text-decoration: none;">Phuoc Le</a>. ' +
            "Use it to load solution-scoped Dataverse components, edit labels across installed languages, save the changes back to Dataverse, and maintain AI and dictionary-assisted translations.</p>" +
            "</div>";

        openHtmlDialog({
            title: "About",
            body: html,
            width: 580,
            height: 310,
            buttons: '<button class="w2ui-btn" onclick="w2popup.close();">Close</button>',
            maximizeOnOpen: true
        });
    };

    DialogHelper.ShowHelp = function () {
        var html =
            '<div style="padding: 15px 20px; font-size: 13px; line-height: 1.75;">' +
            '<p style="margin: 0 0 10px 0;"><b>Basic flow:</b> Select a Solution, choose an Entity when the selected type needs one, choose a Type, click Load, edit language columns, then Save.</p>' +
            '<p style="margin: 0 0 10px 0;"><b>Solution scope:</b> The Solution selector filters component loading. Entity-dependent types require an entity from the selected solution. Global types are available when Entity is set to None.</p>' +
            '<hr style="margin: 8px 0; border: none; border-top: 1px solid #ddd;">' +
            "<b>Entity-dependent types</b>" +
            '<ul style="margin: 4px 0 12px 0; padding-left: 20px;">' +
            "<li><b>Attributes</b>: table column labels and descriptions.</li>" +
            "<li><b>Option Sets</b>: local choice option labels.</li>" +
            "<li><b>Forms</b>: form labels handled by the server Easy Translator adapter.</li>" +
            "<li><b>Views</b>: saved query display labels.</li>" +
            "<li><b>Form Metadata</b>: form tabs, sections, controls, and related form metadata labels.</li>" +
            "<li><b>Entity Metadata</b>: table display names, collection names, and descriptions.</li>" +
            "<li><b>Relationships</b>: relationship and associated menu labels.</li>" +
            "<li><b>Charts</b>: chart labels.</li>" +
            "<li><b>Business Process Flows</b>: process and stage labels.</li>" +
            "<li><b>Business Rules</b>: business rule labels. Save updates changed rule XAML through the server adapter.</li>" +
            "<li><b>Ribbons</b>: classic ribbon labels. Save imports the changed solution data and starts async Publish XML.</li>" +
            "<li><b>Commands</b>: modern command designer appaction labels, then publishes the selected table.</li>" +
            "<li><b>Entity Messages</b>: table message and display string labels from the selected solution translation package.</li>" +
            "<li><b>Content Snippets</b>: Power Pages content snippet labels, normally for the adx_contentsnippet table.</li>" +
            "</ul>" +
            "<b>Global types</b>" +
            '<ul style="margin: 4px 0 12px 0; padding-left: 20px;">' +
            "<li><b>Sitemap</b>: model-driven app sitemap labels in the selected solution.</li>" +
            "<li><b>Dashboards</b>: dashboard parent row labels only. Tabs, sections, and cells are intentionally not loaded.</li>" +
            "<li><b>Web Resources</b>: web resource display names and descriptions.</li>" +
            "<li><b>Global Option Sets</b>: global choice and option labels.</li>" +
            "</ul>" +
            '<hr style="margin: 8px 0; border: none; border-top: 1px solid #ddd;">' +
            "<b>Translation tools</b>" +
            '<ul style="margin: 4px 0 12px 0; padding-left: 20px;">' +
            "<li><b>Auto Translate</b>: translates from the selected source language to the selected target language with the configured provider.</li>" +
            "<li><b>App Settings</b>: stores provider URLs, API keys, model names, and prompts in the Dataverse app settings web resource.</li>" +
            "<li><b>Apply Dictionary</b>: applies existing dictionary matches to the current grid without calling an AI provider.</li>" +
            "<li><b>Add selected translation to dictionary</b>: saves the selected row translation pair to the dictionary.</li>" +
            "<li><b>Dictionary</b>: manages source and target term pairs stored in the Dataverse Label Translator data web resource.</li>" +
            "</ul>" +
            "</div>";

        openHtmlDialog({
            title: "Translation Guide",
            body: html,
            width: 700,
            height: 520,
            maximizeOnOpen: true
        });
    };
})((window.DialogHelper = window.DialogHelper || {}));
