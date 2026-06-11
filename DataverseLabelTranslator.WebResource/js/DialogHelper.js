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
            '<p style="margin: 0 0 8px 0; color: #777; font-size: 15px;">Version 1.0.0.0</p>' +
            '<p style="margin: 0 auto 15px auto; color: #555; font-size: 15px; max-width: 620px;">A focused translation workspace for Microsoft Dataverse labels and descriptions.</p>' +
            '<hr style="border: none; border-top: 1px solid #eaeaea; margin: 20px 0;">' +
            '<p style="font-size: 15px; margin: 0 auto 8px auto; color: #444; max-width: 900px;">Load solution-scoped Dataverse components, review labels across installed languages, and publish translation changes back to Dataverse from one dashboard.</p>' +
            '<p style="font-size: 15px; margin: 0 auto; color: #555; max-width: 900px;">Built by ' +
            '<a href="https://github.com/phuocle" target="_blank" rel="noopener noreferrer" style="font-weight: 600; text-decoration: none;">Phuoc Le</a> ' +
            "with AI-assisted translation and dictionary workflows for repeatable label maintenance.</p>" +
            "</div>";

        openHtmlDialog({
            title: "About",
            body: html,
            width: 580,
            height: 310,
            maximizeOnOpen: true
        });
    };

    DialogHelper.ShowHelp = function () {
        function buildDetails(title, body) {
            return (
                '<details class="xqt-help-details">' +
                '<summary class="xqt-help-summary">' +
                escapeHtml(title) +
                "</summary>" +
                '<div class="xqt-help-detail-body">' +
                body +
                "</div>" +
                "</details>"
            );
        }

        function buildType(title, description, flow, notes) {
            var html =
                '<p class="xqt-help-detail-text">' +
                escapeHtml(description) +
                "</p>" +
                '<p class="xqt-help-detail-text"><b>Typical flow:</b> ' +
                escapeHtml(flow) +
                "</p>";

            if (notes) {
                html += '<p class="xqt-help-detail-text"><b>Notes:</b> ' + escapeHtml(notes) + "</p>";
            }

            return buildDetails(title, html);
        }

        var html =
            '<div class="xqt-help-body">' +
            '<p class="xqt-help-intro"><b>Dataverse Label Translator</b> is a focused dashboard for loading solution-scoped Dataverse labels, reviewing installed language columns side by side, editing translations in the grid, using dictionary or AI-assisted translation workflows, and saving/publishing the changed labels back to Dataverse.</p>' +
            '<h3 class="xqt-help-heading">Flows</h3>' +
            '<ol class="xqt-help-flow">' +
            "<li><b>Select scope:</b> choose a Solution first. For entity-dependent types, choose an Entity from that solution. For global types, keep Entity as None.</li>" +
            "<li><b>Choose work:</b> select a Type and, when available, choose Display Text or Description in the Component selector.</li>" +
            "<li><b>Load data:</b> click Load to retrieve rows and installed language columns. Use search to filter by schema/source or language text.</li>" +
            "<li><b>Edit translations:</b> edit language cells directly. The selected row stays highlighted and the focused cell is outlined. Changed cells enable Save and cell undo.</li>" +
            "<li><b>Accelerate translation:</b> use Apply Dictionary for existing matches, Add selected translation to dictionary for reusable terms, or Auto Translate for AI-assisted drafts.</li>" +
            "<li><b>Save and publish:</b> click Save after reviewing changes. The server adapter writes the selected component type and publishes when that type requires Dataverse publish.</li>" +
            "</ol>" +
            '<h3 class="xqt-help-heading">Types</h3>' +
            buildType(
                "Attributes",
                "Translates table column display names and descriptions for the selected entity.",
                "Select Solution, select Entity, choose Attributes, choose Display Text or Description, Load, edit language columns, then Save.",
                "Readonly or unavailable label values appear as placeholders. Empty editable labels can be restored through normal grid editing."
            ) +
            buildType(
                "Business Process Flows",
                "Translates process and stage labels for business process flows on the selected entity.",
                "Select Solution, select Entity, choose Business Process Flows, Load, update process or stage labels, then Save.",
                "Only labels returned by the Dataverse adapter are editable in the dashboard."
            ) +
            buildType(
                "Business Rules",
                "Translates business rule labels for the selected entity.",
                "Select Solution, select Entity, choose Business Rules, Load, edit language labels, then Save.",
                "Save updates changed rule XAML through the server adapter."
            ) +
            buildType(
                "Charts",
                "Translates chart labels returned by the selected solution.",
                "Select Solution, keep Entity as None, choose Charts, Load, edit chart labels, then Save.",
                "Review chart names carefully because users often see them in dashboards and view chart panes."
            ) +
            buildType(
                "Commands",
                "Translates modern command designer appaction labels for the selected entity.",
                "Select Solution, select Entity, choose Commands, Load, update command labels, then Save.",
                "Saving command labels publishes the selected table when the adapter requires it."
            ) +
            buildType(
                "Content Snippets",
                "Translates Power Pages content snippet labels, normally for the adx_contentsnippet table.",
                "Select Solution, select Entity, choose Content Snippets, Load, update snippet translations, then Save.",
                "This type is usually relevant only when the selected solution contains Power Pages content snippet rows."
            ) +
            buildType(
                "Dashboards",
                "Translates dashboard parent row labels only.",
                "Select Solution, keep Entity as None, choose Dashboards, Load, edit dashboard parent labels, then Save.",
                "Dashboard tabs, sections, and cells are intentionally not loaded into this type."
            ) +
            buildType(
                "Entity Messages",
                "Translates table message and display string labels from the selected solution translation package.",
                "Select Solution, select Entity, choose Entity Messages, Load, edit message labels, then Save.",
                "Use this when user-facing platform messages need localized wording."
            ) +
            buildType(
                "Entity Metadata",
                "Translates table display names, collection names, and descriptions returned for solution-scoped table metadata.",
                "Select Solution, keep Entity as None, choose Entity Metadata, choose Display Text or Description, Load, edit, then Save.",
                "Use this for the table-level labels users see in model-driven apps and metadata surfaces."
            ) +
            buildType(
                "Form Metadata",
                "Translates detailed form metadata such as tabs, sections, controls, and related labels.",
                "Select Solution, keep Entity as None, choose Form Metadata, Load, update language cells, then Save.",
                "This type is more granular than Forms and is useful when labels inside the form layout need direct maintenance."
            ) +
            buildType(
                "Forms",
                "Translates form labels handled by the server Easy Translator adapter for the selected entity.",
                "Select Solution, select Entity, choose Forms, Load, edit form label translations, then Save.",
                "Use Form Metadata when you need detailed form tabs, sections, controls, or related metadata labels."
            ) +
            buildType(
                "Global Option Set",
                "Translates global choice names and option labels.",
                "Select Solution, keep Entity as None, choose Global Option Set, Load, update labels, then Save.",
                "Use Option Sets for local table choices."
            ) +
            buildType(
                "Option Sets",
                "Translates local choice option labels that belong to the selected entity.",
                "Select Solution, select Entity, choose Option Sets, Load, update option labels across languages, then Save.",
                "Use Global Option Set when the choice is a reusable global choice rather than a local table column choice."
            ) +
            buildType(
                "Relationships",
                "Translates relationship labels and associated menu labels returned by the selected solution.",
                "Select Solution, keep Entity as None, choose Relationships, Load, update relationship/menu text, then Save.",
                "This helps keep related-record navigation names consistent across languages."
            ) +
            buildType(
                "Ribbons",
                "Translates classic ribbon labels for the selected entity.",
                "Select Solution, select Entity, choose Ribbons, Load, edit command labels, then Save.",
                "Save imports changed solution data and starts the required async Publish XML operation."
            ) +
            buildType(
                "Sitemap",
                "Translates model-driven app sitemap labels in the selected solution.",
                "Select Solution, keep Entity as None, choose Sitemap, Load, update area/group/subarea labels, then Save.",
                "Sitemap labels are solution-scoped and are not tied to one selected entity."
            ) +
            buildType(
                "Views",
                "Translates saved query display labels returned by the selected solution.",
                "Select Solution, keep Entity as None, choose Views, Load, edit view label cells, then Save.",
                "Search can help narrow long view lists before editing."
            ) +
            buildType(
                "Web Resources",
                "Translates localizable key/value content inside JavaScript JSON resources and RESX resources, plus web resource descriptions when Description is selected.",
                "Select Solution, keep Entity as None, choose Web Resources, choose Display Text or Description, Load, expand a web resource parent row, edit child key rows or descriptions, then Save.",
                "Only unmanaged web resources that follow the LCID naming convention are grouped. For example, AAA.1033, AAA.1041, and pl_/resx/AAAA.1033.resx are shown under parent AAA or AAAA."
            ) +
            '<h3 class="xqt-help-heading">Translation Tools</h3>' +
            buildDetails(
                "Auto Translate",
                '<p class="xqt-help-detail-text">Opens an AI translation workspace for visible grid rows. Choose source and target languages, select provider settings, optionally use dictionary matches, translate, review, then apply changes back to the main grid.</p>'
            ) +
            buildDetails(
                "Apply Dictionary",
                '<p class="xqt-help-detail-text">Applies existing dictionary entries to matching rows in the current grid without calling an AI provider. Choose missing-only mode when you want to fill blanks, or overwrite mode when dictionary values should replace existing target text.</p>'
            ) +
            buildDetails(
                "Add selected translation to dictionary",
                '<p class="xqt-help-detail-text">Creates a reusable dictionary entry from the currently selected translation row. Defaults come from the selected source and target cells, and the latest edited values are saved when Add is clicked.</p>'
            ) +
            buildDetails(
                "Dictionary",
                '<p class="xqt-help-detail-text">Manages source and target term pairs stored in the Dataverse Label Translator data web resource. Column headers include LCID values, for example Source English (1033), so entries stay tied to installed Dataverse languages.</p>'
            ) +
            buildDetails(
                "App Settings",
                '<p class="xqt-help-detail-text">Stores provider URLs, API keys, model names, and custom prompts used by AI translation. These settings are managed inside the app settings web resource.</p>'
            ) +
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
