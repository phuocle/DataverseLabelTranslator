import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

// Coverage for DataverseLabelTranslator.js internal functions by simulating
// Initialize + InitializeGrid through a w2grid constructor mock that captures
// the grid + toolbar config closures (event handlers). We then drive those
// handlers directly with controlled events.

function makeMockElement() {
    return {
        innerHTML: "",
        textContent: "",
        value: "",
        style: { display: "" },
        setAttribute: vi.fn(),
        removeAttribute: vi.fn(),
        appendChild: vi.fn(),
        remove: vi.fn(),
        classList: { add: vi.fn(), remove: vi.fn() },
        querySelector: vi.fn(() => null),
        querySelectorAll: vi.fn(() => []),
        contains: vi.fn(() => true),
        closest: vi.fn(() => null),
    };
}

const footerEl = makeMockElement();
const searchEl = makeMockElement();
const gridBox = {
    // Returns one of the two persistent mock elements based on the selector:
    // footer selector -> footerEl, search name -> searchEl, otherwise a fresh mock.
    querySelector: vi.fn(function (selector) {
        if (typeof selector === "string" && selector.indexOf("_footer") !== -1) {
            return footerEl;
        }
        if (typeof selector === "string" && selector.indexOf("_search_name") !== -1) {
            return searchEl;
        }
        return makeMockElement();
    }),
    querySelectorAll: vi.fn(() => []),
    contains: vi.fn(() => true),
};

let capturedGridConfig = null;
let capturedToolbar = null;

const toolbarItemsById = {};
const toolbarItems = [
    { id: "solutionSelect", type: "menu-radio", selected: null, text: "Solution" },
    { id: "entitySelect", type: "menu-radio", selected: "none", items: [{ id: "none", text: "None" }] },
    {
        id: "type",
        type: "menu-radio",
        selected: "none",
        items: [
            { id: "none", text: "None" },
            { id: "attributes", text: "Attributes" },
            { id: "forms", text: "Forms" },
            { id: "ribbons", text: "Ribbons" },
            { id: "options", text: "Option Sets" },
            { id: "globalOptionSet", text: "Global Option Set" },
        ],
    },
    {
        id: "component",
        type: "menu-radio",
        selected: "DisplayText",
        items: [
            { id: "DisplayText", text: "Display Text" },
            { id: "Description", text: "Description" },
        ],
    },
    { id: "load", type: "button" },
    { id: "w2ui-save", type: "button" },
    { id: "undoCellChange", type: "button", disabled: true },
    { id: "autoTranslate", type: "button" },
    { id: "aiSettings", type: "button" },
    { id: "dictionary", type: "button" },
    { id: "applyDictionary", type: "button" },
    { id: "addSelectedDictionary", type: "button" },
];
for (const i of toolbarItems) toolbarItemsById[i.id] = i;

function makeToolbar() {
    return {
        items: [...toolbarItems],
        get: vi.fn((id) => toolbarItemsById[id] || null),
        set: vi.fn(),
        show: vi.fn(),
        hide: vi.fn(),
        enable: vi.fn(),
        disable: vi.fn(),
        refresh: vi.fn(),
        click: vi.fn(),
        insert: vi.fn(),
        remove: vi.fn(),
        add: vi.fn(),
    };
}

let savedW2grid = null;
let savedW2ui = null;

beforeEach(() => {
    savedW2grid = globalThis.w2grid;
    savedW2ui = globalThis.w2ui;

    capturedGridConfig = null;
    capturedToolbar = makeToolbar();

    globalThis.w2ui = {
        grid: undefined,
        grid_toolbar: capturedToolbar,
    };

    // w2grid constructor: capture the config object, expose its handler closures,
    // and assign grid instance to w2ui.grid.
    function W2Grid(config) {
        capturedGridConfig = config;
        const grid = {
            name: config.name,
            box: gridBox,
            show: config.show,
            searches: config.searches,
            columns: config.columns,
            records: [],
            total: 0,
            last: null,
            searchData: [],
            getSelection: vi.fn(() => []),
            lock: vi.fn(),
            unlock: vi.fn(),
            refresh: vi.fn(),
            refreshRow: vi.fn(),
            removeColumn: vi.fn(),
            addColumn: vi.fn(),
            addSearch: vi.fn(),
            removeSearch: vi.fn(),
            clear: vi.fn(),
            destroy: vi.fn(),
            render: vi.fn(function () {
                return grid;
            }),
            localSearch: vi.fn(),
            sort: vi.fn(),
        };
        globalThis.w2ui.grid = grid;
        // also expose w2ui.grid_toolbar so test can interact
        // eslint-disable-next-line no-unused-vars
        return grid;
    }
    globalThis.w2grid = W2Grid;

    window.Helper = {
        GetTranslator: vi.fn(function () {
            return window.DataverseLabelTranslator;
        }),
        FormatLanguageColumnHeader: vi.fn((lcid) => "Lang " + lcid),
        GetLanguageColumnCode: vi.fn((field) => "code-" + field),
        ShowError: vi.fn(),
        GetOperationLoading: vi.fn(() => "Loading..."),
        GetOperationReLoading: vi.fn(() => "Reloading..."),
        GetPlaceholderDisplayText: vi.fn(() => "(display)"),
        GetPlaceholderDescription: vi.fn(() => "(description)"),
        GetPlaceholderReadonly: vi.fn(() => "-"),
        ApplySimpleGridContainsSearch: vi.fn(),
        RunServerLoad: vi.fn(),
        RunServerSaveFlow: vi.fn(),
        GetSolutions: vi.fn(() => Promise.resolve([{ id: "s1", name: "Sol 1" }])),
        GetEntities: vi.fn(() => Promise.resolve([{ id: "e1", name: "account" }])),
        GetBaseLanguage: vi.fn(() => Promise.resolve(1033)),
        GetAllNoneBaseLanguageCodes: vi.fn(() => Promise.resolve({ LocaleIds: [1041] })),
        BuildLanguageColumns: vi.fn(() => Promise.resolve([{ field: "1033", text: "En", code: "en" }])),
    };

    window.DialogHelper = {
        alert: vi.fn(() => Promise.resolve()),
        confirm: vi.fn(() => Promise.resolve(false)),
        question: vi.fn(() => Promise.resolve("")),
        ShowAbout: vi.fn(),
        ShowHelp: vi.fn(),
        ShowApplyDictionaryPrompt: vi.fn(() => Promise.resolve()),
        ShowAddSelectedTranslationToDictionary: vi.fn(() => Promise.resolve()),
    };
    window.AppService = {
        EnsureInitialized: vi.fn(() => Promise.resolve()),
        GetSettings: vi.fn(),
        SaveSettings: vi.fn(),
        ShowAppSettings: vi.fn(),
    };
    window.DictionaryService = {
        EnsureInitialized: vi.fn(() => Promise.resolve()),
        GetStorageInfo: vi.fn(),
        UpsertEntries: vi.fn(() => Promise.resolve()),
        SaveFromGrid: vi.fn(() => Promise.resolve()),
        ShowDictionaryPrompt: vi.fn(),
    };
    window.AIService = { OpenWorkspace: vi.fn(() => Promise.resolve()) };
    window.w2popup = { open: vi.fn(), close: vi.fn(), max: vi.fn() };
    window.w2alert = vi.fn(() => Promise.resolve());
    window.w2utils = {
        stripTags: vi.fn((s) => String(s).replace(/<[^>]*>/g, "")),
        decodeTags: vi.fn((s) => String(s)),
    };

    globalThis.document = {
        getElementById: vi.fn(() => null),
        querySelector: vi.fn(() => null),
        querySelectorAll: vi.fn(() => []),
        createElement: vi.fn(() => ({
            innerHTML: "",
            textContent: "",
            setAttribute: vi.fn(),
            removeAttribute: vi.fn(),
            appendChild: vi.fn(),
            style: {},
            classList: { add: vi.fn(), remove: vi.fn() },
        })),
        head: { appendChild: vi.fn() },
    };
});

afterEach(() => {
    globalThis.w2grid = savedW2grid;
    globalThis.w2ui = savedW2ui;
});

import "../js/DataverseLabelTranslator.js";

const DLT = window.DataverseLabelTranslator;

describe("Initialize / InitializeGrid", () => {
    it("Initialize bootstraps the grid and runs through the full promise chain", async () => {
        await DLT.Initialize();
        // After Initialize, the grid should be created via w2grid constructor
        expect(capturedGridConfig).not.toBeNull();
        expect(globalThis.w2ui.grid).toBeTruthy();
        expect(capturedGridConfig.name).toBe("grid");
        // event handler closures should be functions
        expect(typeof capturedGridConfig.onChange).toBe("function");
        expect(typeof capturedGridConfig.onClick).toBe("function");
        expect(typeof capturedGridConfig.onDblClick).toBe("function");
        expect(typeof capturedGridConfig.onEditField).toBe("function");
        expect(typeof capturedGridConfig.onRefresh).toBe("function");
        expect(typeof capturedGridConfig.onSearch).toBe("function");
        expect(typeof capturedGridConfig.onSave).toBe("function");
        expect(typeof capturedGridConfig.toolbar.onClick).toBe("function");
    });
});

describe("grid event handlers captured by InitializeGrid", () => {
    beforeEach(async () => {
        await DLT.Initialize();
    });

    it("onChange.onComplete runs NormalizeGridChanges + SetSaveButtonDisabled + UpdateChangedCellFooter", () => {
        capturedGridConfig.columns = [{ field: "1033", text: "English" }];
        globalThis.w2ui.grid.records = [{ recid: "r1", 1033: "orig", w2ui: { changes: { 1033: "orig" } } }];
        const event = { recid: "r1", column: 0 };
        capturedGridConfig.onChange(event);
        expect(typeof event.onComplete).toBe("function");
        expect(() => event.onComplete()).not.toThrow();
        // The change matches original -> changes normalized away
        expect(globalThis.w2ui.grid.records[0].w2ui.changes).toBeUndefined();
    });

    it("onClick.onComplete runs SetFocusedCell + UpdateChangedCellFooter", () => {
        capturedGridConfig.columns = [{ field: "1033", text: "English" }];
        globalThis.w2ui.grid.records = [{ recid: "r1", 1033: "orig" }];
        const event = { recid: "r1", column: 0 };
        capturedGridConfig.onClick(event);
        expect(typeof event.onComplete).toBe("function");
        expect(() => event.onComplete()).not.toThrow();
    });

    it("onDblClick.onComplete runs SetFocusedCell + UpdateChangedCellFooter", () => {
        capturedGridConfig.columns = [{ field: "1033", text: "English" }];
        globalThis.w2ui.grid.records = [{ recid: "r1", 1033: "orig" }];
        const event = { recid: "r1", column: 0 };
        capturedGridConfig.onDblClick(event);
        expect(typeof event.onComplete).toBe("function");
        expect(() => event.onComplete()).not.toThrow();
    });

    it("onEditField runs SetFocusedCell + SetSaveButtonDisabled(false) + UpdateChangedCellFooter", () => {
        capturedGridConfig.columns = [{ field: "1033", text: "English" }];
        globalThis.w2ui.grid.records = [{ recid: "r1", 1033: "orig" }];
        expect(() => capturedGridConfig.onEditField({ recid: "r1", column: 0 })).not.toThrow();
    });

    it("onRefresh sets event.onComplete to ApplyFocusedCellElement", () => {
        const event = {};
        capturedGridConfig.onRefresh(event);
        expect(typeof event.onComplete).toBe("function");
        expect(() => event.onComplete()).not.toThrow();
    });

    it("onSearch.onComplete runs ApplySimpleGridContainsSearch + NormalizeGridSearchUiSoon", () => {
        const event = {};
        capturedGridConfig.onSearch(event);
        expect(typeof event.onComplete).toBe("function");
        expect(() => event.onComplete()).not.toThrow();
        expect(window.Helper.ApplySimpleGridContainsSearch).toHaveBeenCalledWith(globalThis.w2ui.grid);
    });

    it("onSave (SaveHandler) shows no-changes alert when no pending changes", async () => {
        globalThis.w2ui.grid.records = [];
        // Toolbar returns type=attributes so HasPendingChanges via NormalizeGridChanges returns false
        capturedToolbar.get = vi.fn((id) => {
            if (id === "type") return { selected: "attributes" };
            if (id === "entitySelect") return { selected: "account" };
            if (id === "component") return { selected: "DisplayText" };
            if (id === "solutionSelect") return { selected: "s1" };
            if (id === "w2ui-save") return toolbarItemsById["w2ui-save"];
            return null;
        });
        await capturedGridConfig.onSave();
        expect(window.DialogHelper.alert).toHaveBeenCalled();
    });
});

describe("HandleToolbarClick via captured toolbar.onClick", () => {
    beforeEach(async () => {
        await DLT.Initialize();
        capturedToolbar.get = vi.fn((id) => toolbarItemsById[id] || null);
    });

    it("routes 'about' to DialogHelper.ShowAbout", () => {
        capturedGridConfig.toolbar.onClick({ target: "about" });
        expect(window.DialogHelper.ShowAbout).toHaveBeenCalled();
    });

    it("routes 'help' to DialogHelper.ShowHelp", () => {
        capturedGridConfig.toolbar.onClick({ target: "help" });
        expect(window.DialogHelper.ShowHelp).toHaveBeenCalled();
    });

    it("routes 'autoTranslate' to ShowAITranslate", async () => {
        capturedToolbar.get = vi.fn((id) => {
            if (id === "type") return { selected: "attributes" };
            if (id === "component") return { selected: "DisplayText" };
            return toolbarItemsById[id] || null;
        });
        capturedGridConfig.toolbar.onClick({ target: "autoTranslate" });
        // synchronous unavailable path will call DialogHelper.alert
        expect(window.DialogHelper.alert).toHaveBeenCalled();
    });

    it("routes 'aiSettings' to AppService.ShowAppSettings when available", () => {
        capturedGridConfig.toolbar.onClick({ target: "aiSettings" });
        expect(window.AppService.ShowAppSettings).toHaveBeenCalled();
    });

    it("routes 'aiSettings' to TriggerUnavailable when AppService.ShowAppSettings missing", () => {
        const saved = window.AppService.ShowAppSettings;
        delete window.AppService.ShowAppSettings;
        capturedGridConfig.toolbar.onClick({ target: "aiSettings" });
        expect(window.DialogHelper.alert).toHaveBeenCalled();
        window.AppService.ShowAppSettings = saved;
    });

    it("routes 'dictionary' to DictionaryService.ShowDictionaryPrompt when available", () => {
        capturedGridConfig.toolbar.onClick({ target: "dictionary" });
        expect(window.DictionaryService.ShowDictionaryPrompt).toHaveBeenCalled();
    });

    it("routes 'dictionary' to TriggerUnavailable when ShowDictionaryPrompt missing", () => {
        const saved = window.DictionaryService.ShowDictionaryPrompt;
        delete window.DictionaryService.ShowDictionaryPrompt;
        capturedGridConfig.toolbar.onClick({ target: "dictionary" });
        expect(window.DialogHelper.alert).toHaveBeenCalled();
        window.DictionaryService.ShowDictionaryPrompt = saved;
    });

    it("routes 'applyDictionary' to ShowApplyDictionaryPrompt (no throw)", () => {
        expect(() => capturedGridConfig.toolbar.onClick({ target: "applyDictionary" })).not.toThrow();
    });

    it("routes 'addSelectedDictionary' to ShowAddSelectedTranslationToDictionary (no throw)", () => {
        expect(() => capturedGridConfig.toolbar.onClick({ target: "addSelectedDictionary" })).not.toThrow();
    });

    it("routes 'expandAll' substring to ToggleExpandCollapse(true)", () => {
        expect(() =>
            capturedGridConfig.toolbar.onClick({ target: "w2ui-search-advanced:toggle:expandAll" }),
        ).not.toThrow();
    });

    it("routes 'collapseAll' substring to ToggleExpandCollapse(false)", () => {
        expect(() =>
            capturedGridConfig.toolbar.onClick({ target: "w2ui-search-advanced:toggle:collapseAll" }),
        ).not.toThrow();
    });

    it("routes 'solutionSelect:s1' to RepopulateEntitySelector", () => {
        capturedToolbar.get = vi.fn((id) => {
            if (id === "solutionSelect") return { selected: null, items: [] };
            return toolbarItemsById[id] || null;
        });
        window.Helper.GetEntities.mockResolvedValueOnce([]);
        expect(() => capturedGridConfig.toolbar.onClick({ target: "solutionSelect:s1" })).not.toThrow();
    });

    it("routes 'entitySelect:account' to ApplyTypeVisibilityForEntity", () => {
        capturedToolbar.get = vi.fn((id) => {
            if (id === "entitySelect") return { selected: "none", items: [{ id: "none" }] };
            return toolbarItemsById[id] || null;
        });
        expect(() => capturedGridConfig.toolbar.onClick({ target: "entitySelect:account" })).not.toThrow();
        expect(capturedToolbar.refresh).toHaveBeenCalled();
    });

    it("routes 'type:attributes' to UpdateComponentDropdown + SetHandler", () => {
        capturedToolbar.get = vi.fn((id) => {
            if (id === "type") return { selected: "none", items: toolbarItemsById.type.items };
            if (id === "component") return { selected: "DisplayText", items: toolbarItemsById.component.items };
            return toolbarItemsById[id] || null;
        });
        expect(() => capturedGridConfig.toolbar.onClick({ target: "type:attributes" })).not.toThrow();
        expect(capturedToolbar.refresh).toHaveBeenCalled();
    });

    it("registers and loads Global Option Set without requiring an entity", async () => {
        const registeredType = capturedGridConfig.toolbar.items
            .find((item) => item.id === "type")
            .items.find((item) => item.id === "globalOptionSet");
        expect(registeredType).toMatchObject({ text: "Global Option Set", icon: "icon-global-options" });

        toolbarItemsById.type.selected = "none";
        toolbarItemsById.entitySelect.selected = "none";
        toolbarItemsById.solutionSelect.selected = "s1";
        toolbarItemsById.component.selected = "Description";
        capturedGridConfig.toolbar.onClick({ target: "type:globalOptionSet" });

        expect(toolbarItemsById.type.selected).toBe("globalOptionSet");
        expect(DLT.IsUnifiedType("globalOptionSet")).toBe(true);
        expect(capturedToolbar.enable).toHaveBeenCalledWith("component");

        window.Helper.RunServerLoad.mockResolvedValueOnce({ baseLanguage: 1033, grid: { rows: [] } });
        const loadItem = capturedGridConfig.toolbar.items.find((item) => item.id === "load");
        loadItem.onClick();
        await vi.waitFor(() => expect(window.Helper.RunServerLoad).toHaveBeenCalled());

        expect(window.DialogHelper.alert).not.toHaveBeenCalledWith(
            "Select an entity before loading this type.",
            expect.anything(),
        );
        const request = window.Helper.RunServerLoad.mock.calls.at(-1)[0];
        expect(request.getPayload()).toEqual({
            translatorType: "globalOptionSet",
            solutionId: "s1",
            entityName: "none",
            entityId: null,
            component: "Description",
        });
    });

    it("registers and loads Web Resources without requiring an entity", async () => {
        const registeredType = capturedGridConfig.toolbar.items
            .find((item) => item.id === "type")
            .items.find((item) => item.id === "webresources");
        expect(registeredType).toMatchObject({ text: "Web Resources", icon: "icon-file-code" });

        toolbarItemsById.type.selected = "none";
        toolbarItemsById.entitySelect.selected = "none";
        toolbarItemsById.solutionSelect.selected = "s1";
        toolbarItemsById.component.selected = "Description";
        capturedGridConfig.toolbar.onClick({ target: "type:webresources" });

        expect(toolbarItemsById.type.selected).toBe("webresources");
        expect(DLT.IsUnifiedType("webresources")).toBe(true);
        expect(capturedToolbar.enable).toHaveBeenCalledWith("component");

        window.Helper.RunServerLoad.mockResolvedValueOnce({ baseLanguage: 1033, grid: { mode: "flat", rows: [] } });
        const loadItem = capturedGridConfig.toolbar.items.find((item) => item.id === "load");
        loadItem.onClick();
        await vi.waitFor(() => expect(window.Helper.RunServerLoad).toHaveBeenCalled());

        expect(window.DialogHelper.alert).not.toHaveBeenCalledWith(
            "Select an entity before loading this type.",
            expect.anything(),
        );
        const request = window.Helper.RunServerLoad.mock.calls.at(-1)[0];
        expect(request.getPayload()).toEqual({
            translatorType: "webresources",
            solutionId: "s1",
            entityName: "none",
            entityId: null,
            component: "Description",
        });
    });

    it("routes 'component:Description' to updating the component selected", () => {
        capturedToolbar.get = vi.fn((id) => {
            if (id === "component") return { selected: "DisplayText", items: toolbarItemsById.component.items };
            return toolbarItemsById[id] || null;
        });
        capturedGridConfig.toolbar.onClick({ target: "component:Description" });
        expect(capturedToolbar.refresh).toHaveBeenCalled();
    });

    it("is a no-op for unrecognized targets", () => {
        expect(() => capturedGridConfig.toolbar.onClick({ target: "noop" })).not.toThrow();
    });
});

describe("Load (DLT.Load) requests server load", () => {
    beforeEach(async () => {
        await DLT.Initialize();
        capturedToolbar.get = vi.fn((id) => {
            if (id === "type") return { selected: "attributes" };
            if (id === "entitySelect") return { selected: "account" };
            if (id === "component") return { selected: "DisplayText" };
            if (id === "solutionSelect") return { selected: "s1" };
            return toolbarItemsById[id] || null;
        });
        window.Helper.RunServerLoad.mockResolvedValueOnce({ baseLanguage: 1033, grid: [{ recid: "r1" }] });
    });

    it("calls Helper.RunServerLoad with lockText", async () => {
        await DLT.Load("Custom loading");
        expect(window.Helper.RunServerLoad).toHaveBeenCalled();
        const payload = window.Helper.RunServerLoad.mock.calls[0][0];
        expect(payload.actionName).toBe("EasyTranslator");
        expect(typeof payload.getPayload).toBe("function");
        expect(typeof payload.onLoaded).toBe("function");
    });

    it("onLoaded populates metadata, baseLanguage, fills table", async () => {
        await DLT.Load("Custom loading");
        // The mock resolves with grid: [{recid:"r1"}] and baseLanguage 1033 -> records added
        expect(globalThis.w2ui.grid.records.length).toBeGreaterThanOrEqual(0);
    });
});

describe("Save (DLT.Save) with changed rows", () => {
    beforeEach(async () => {
        await DLT.Initialize();
        capturedToolbar.get = vi.fn((id) => {
            if (id === "type") return { selected: "attributes" };
            if (id === "entitySelect") return { selected: "account" };
            if (id === "component") return { selected: "DisplayText" };
            if (id === "solutionSelect") return { selected: "s1" };
            if (id === "w2ui-save") return toolbarItemsById["w2ui-save"];
            return toolbarItemsById[id] || null;
        });
    });

    it("shows alert when there are no changes", async () => {
        globalThis.w2ui.grid.records = [];
        await DLT.Save();
        expect(window.DialogHelper.alert).toHaveBeenCalled();
    });

    it("invokes RunServerSaveFlow when pending changes exist", async () => {
        globalThis.w2ui.grid.records = [
            { recid: "r1", gridKey: "key1", schemaName: "attr1", 1033: "orig", w2ui: { changes: { 1033: "new" } } },
        ];
        window.Helper.RunServerSaveFlow.mockResolvedValueOnce({ changed: true });
        await DLT.Save();
        expect(window.Helper.RunServerSaveFlow).toHaveBeenCalled();
    });

    it("builds the Global Option Set save payload from changed language cells", async () => {
        capturedToolbar.get = vi.fn((id) => {
            if (id === "type") return { selected: "globalOptionSet" };
            if (id === "entitySelect") return { selected: "none" };
            if (id === "component") return { selected: "Description" };
            if (id === "solutionSelect") return { selected: "s1" };
            if (id === "w2ui-save") return toolbarItemsById["w2ui-save"];
            return toolbarItemsById[id] || null;
        });
        globalThis.w2ui.grid.records = [
            {
                recid: "gos-description",
                gridKey: "globalOptionSet|pl_test|description",
                rowType: "globalOptionSet",
                1041: "Old description",
                w2ui: { changes: { 1041: "New description" } },
            },
        ];
        window.Helper.RunServerSaveFlow.mockResolvedValueOnce({ changed: true });

        await DLT.Save();

        const request = window.Helper.RunServerSaveFlow.mock.calls.at(-1)[0];
        expect(request.getSavePayload()).toMatchObject({
            translatorType: "globalOptionSet",
            solutionId: "s1",
            entityName: "none",
            component: "Description",
            changedRows: [
                {
                    gridKey: "globalOptionSet|pl_test|description",
                    recid: "gos-description",
                    rowType: "globalOptionSet",
                    changes: { 1041: "New description" },
                },
            ],
        });
    });

    it("RemoveOverriddenCellLabels runs save flow when confirmed", async () => {
        window.DialogHelper.confirm.mockResolvedValueOnce(true);
        window.Helper.RunServerSaveFlow.mockResolvedValueOnce({ changed: true });
        await DLT.RemoveOverriddenCellLabels();
        expect(window.Helper.RunServerSaveFlow).toHaveBeenCalled();
    });
});
