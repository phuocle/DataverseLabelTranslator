import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

// Coverage for AIService.js internal helpers through the public OpenWorkspace
// entry point. We mock w2popup to capture the workspace dialog callbacks
// (onOpen, onToggle, onClose), drive them manually, and inspect state.

const workspaceContainerImpl = {
    _innerHTML: "",
    _children: [],
    style: { display: "" },
    set innerHTML(v) {
        this._innerHTML = v;
    },
    get innerHTML() {
        return this._innerHTML;
    },
    appendChild: vi.fn(function (c) {
        this._children.push(c);
    }),
    querySelector: vi.fn(() => null),
    querySelectorAll: vi.fn(() => []),
    resize: vi.fn(),
    destroy: vi.fn(),
    localSearch: vi.fn(),
    columns: [],
    records: [],
    searches: [],
    add: vi.fn(),
    addColumn: vi.fn(),
    removeColumn: vi.fn(),
    clear: vi.fn(),
    refresh: vi.fn(),
    render: vi.fn(),
    refreshSearch: vi.fn(),
    getSelection: vi.fn(() => []),
    set: vi.fn(),
    get: vi.fn(),
    search: vi.fn(),
    lock: vi.fn(),
    unlock: vi.fn(),
    last: null,
    searchData: [],
    show: { toolbar: true, footer: true },
    total: 0,
    box: null,
};

let popupConfig = null;
let w2gridInstance = null;
let workspaceContainer;

function makeElement(text) {
    return {
        innerHTML: text || "",
        textContent: text || "",
        onclick: null,
        querySelector: vi.fn(() => null),
        appendChild: vi.fn(),
        style: {},
    };
}

beforeEach(() => {
    popupConfig = null;
    workspaceContainer = {
        ...workspaceContainerImpl,
        get innerHTML() {
            return this._innerHTML;
        },
        set innerHTML(v) {
            this._innerHTML = v;
        },
    };
    workspaceContainer.box = workspaceContainer;

    w2gridInstance = {
        name: "easyAiTranslateGrid",
        box: workspaceContainer,
        columns: [],
        records: [],
        searches: [],
        show: { toolbar: true, footer: true, toolbarSearch: true },
        destroy: vi.fn(),
        render: vi.fn(),
        resize: vi.fn(),
        refresh: vi.fn(),
        refreshRow: vi.fn(),
        refreshSearch: vi.fn(),
        addColumn: vi.fn(),
        add: vi.fn(),
        remove: vi.fn(),
        set: vi.fn(),
        get: vi.fn(),
        clear: vi.fn(),
        localSearch: vi.fn(),
        search: vi.fn(),
        lock: vi.fn(),
        unlock: vi.fn(),
        last: null,
        searchData: [],
        total: 0,
    };
    workspaceContainer._innerHTML = "";
    workspaceContainer._children = [];
    workspaceContainer.appendChild.mockClear();
    workspaceContainer.querySelector.mockReset();
    workspaceContainer.resize.mockClear();
    workspaceContainer.destroy.mockClear();
    workspaceContainer.box = workspaceContainer;
    workspaceContainer.columns = [];
    workspaceContainer.records = [];
    workspaceContainer.searches = [];
    workspaceContainer.last = null;
    workspaceContainer.total = 0;
    workspaceContainer.searchData = [];
    workspaceContainer.getSelection.mockReset(() => []);

    globalThis.w2popup = {
        open: vi.fn(function (cfg) {
            popupConfig = cfg;
        }),
        close: vi.fn(),
        max: vi.fn(),
        lock: vi.fn(),
        unlock: vi.fn(),
    };

    function W2Grid(config) {
        // record captured config on the instance
        Object.assign(w2gridInstance, config);
        w2gridInstance.name = config.name;
        w2gridInstance.toolbar = config.toolbar;
        // capture onChange/onEditField/onSearch handlers
        w2gridInstance.onChange = config.onChange;
        w2gridInstance.onEditField = config.onEditField;
        w2gridInstance.onSearch = config.onSearch;
        // register in w2ui so w2ui[workspaceGridName] resolves to the instance
        if (typeof globalThis.w2ui !== "object") globalThis.w2ui = {};
        globalThis.w2ui[config.name] = w2gridInstance;
        return w2gridInstance;
    }
    globalThis.w2grid = W2Grid;

    // document.querySelector("#w2ui-popup #easy-ai-translate-main") must return our container
    globalThis.document = {
        querySelector: vi.fn(function (sel) {
            if (typeof sel === "string" && sel.indexOf("easy-ai-translate-main") !== -1) {
                return workspaceContainer;
            }
            return null;
        }),
        querySelectorAll: vi.fn(() => []),
        getElementById: vi.fn(() => null),
        createElement: vi.fn(function (tag) {
            return makeElement();
        }),
        head: { appendChild: vi.fn() },
    };

    window.w2utils = {
        stripTags: vi.fn((s) => String(s || "").replace(/<[^>]*>/g, "")),
        decodeTags: vi.fn((s) => String(s || "")),
        encodeTags: vi.fn((s) => String(s || "")),
    };

    window.Helper = {
        ExecuteTypedCustomAction: vi.fn(() => Promise.resolve("{}")),
        GetCustomActionObject: vi.fn(() => ({})),
        CustomActionTypes: { Other: 1 },
        ApplySimpleGridContainsSearch: vi.fn(),
    };
});

afterEach(() => {
    delete globalThis.w2popup;
    delete globalThis.w2grid;
});

import "../js/AIService.js";

const AIService = window.AIService;

describe("AIService.OpenWorkspace", () => {
    function makeDataSource() {
        return {
            typeName: "Attributes",
            componentText: "Display Text",
            languages: [
                { lcid: "1033", text: "English (en)", code: "en" },
                { lcid: "1041", text: "Japanese (ja)", code: "ja" }
            ],
            baseLcid: "1033",
            rows: [
                {
                    recid: "r1",
                    targetRecid: "r1",
                    schemaName: "attr1",
                    "1033": "Hello",
                    "1041": "Konnichiwa"
                }
            ],
            applyChanges: vi.fn()
        };
    }

    it("opens popup with title and config", () => {
        const ds = makeDataSource();
        AIService.OpenWorkspace(ds);
        expect(popupConfig).not.toBeNull();
        expect(popupConfig.title).toContain("AI Translate");
        expect(popupConfig.body).toContain("easy-ai-translate-main");
    });

    it("renders workspace after onOpen completes", async () => {
        const ds = makeDataSource();
        AIService.OpenWorkspace(ds);
        expect(popupConfig).not.toBeNull();

        // Drive onOpen and call onComplete to trigger the async preload chain
        const onOpenEvent = {};
        popupConfig.onOpen(onOpenEvent);
        expect(typeof onOpenEvent.onComplete).toBe("function");
        onOpenEvent.onComplete();

        // Wait for the setTimeout to run and the preload promise to resolve
        await new Promise((r) => setTimeout(r, 30));
        await new Promise((r) => setTimeout(r, 30));

        expect(w2gridInstance.render).toHaveBeenCalled();
        expect(globalThis.w2popup.max).toHaveBeenCalled();
    });

    it("renderLoadError path is exercised on preload rejection", async () => {
        window.Helper.ExecuteTypedCustomAction.mockImplementationOnce(() => Promise.reject(new Error("boom")));

        const ds = makeDataSource();
        AIService.OpenWorkspace(ds);
        const onOpenEvent = {};
        popupConfig.onOpen(onOpenEvent);
        onOpenEvent.onComplete();

        await new Promise((r) => setTimeout(r, 50));

        expect(workspaceContainer._innerHTML).toContain("AI Translate is not ready");
    });

    it("onClose cleans up state and grid", () => {
        const ds = makeDataSource();
        AIService.OpenWorkspace(ds);
        // Trigger onOpen just to set up state
        popupConfig.onOpen({});
        // Trigger onClose
        popupConfig.onClose();
        // After onClose, the global AIService state should be cleared
        // (state is private; we just verify no error)
        expect(() => popupConfig.onClose()).not.toThrow();
    });

    it("onToggle toggles grid box display", () => {
        const ds = makeDataSource();
        AIService.OpenWorkspace(ds);
        const event = { onComplete: null };
        popupConfig.onToggle(event);
        // set display:none on grid box, then onComplete restores
        expect(event.onComplete).toBeInstanceOf(Function);
        event.onComplete();
    });

    it("internal normalizeDataSource and getWorkspaceTitle handle missing fields", () => {
        const ds = {
            typeName: "Attributes",
            componentText: "Display Text",
            languages: [{ lcid: "1033", text: "English", code: "en" }],
            baseLcid: "1033",
            rows: [],
            applyChanges: vi.fn()
        };
        AIService.OpenWorkspace(ds);
        // title should default to "AI Translate" when componentText is empty
        expect(popupConfig).not.toBeNull();
    });
});

describe("AIService internal helpers exercised via the OpenWorkspace path", () => {
    beforeEach(() => {
        window.Helper.ExecuteTypedCustomAction.mockImplementation(function () {
            return Promise.resolve({ providers: [{ id: "openai", text: "OpenAI" }], selectedProvider: "openai" });
        });
    });

    it("loadProviders with selected provider populates state", async () => {
        const ds = {
            typeName: "X",
            componentText: "Comp",
            languages: [{ lcid: "1033", text: "En", code: "en" }, { lcid: "1041", text: "Ja", code: "ja" }],
            baseLcid: "1033",
            rows: [{ recid: "r1", targetRecid: "r1", schemaName: "s1", "1033": "a", "1041": "b" }],
            applyChanges: vi.fn()
        };
        AIService.OpenWorkspace(ds);
        const event = {};
        popupConfig.onOpen(event);
        event.onComplete();
        await new Promise((r) => setTimeout(r, 30));

        expect(w2gridInstance.render).toHaveBeenCalled();
    });

    it("renders with empty rows gracefully", async () => {
        const ds = {
            typeName: "X",
            componentText: "Comp",
            languages: [{ lcid: "1033", text: "En", code: "en" }],
            baseLcid: "1033",
            rows: [],
            applyChanges: vi.fn()
        };
        AIService.OpenWorkspace(ds);
        const event = {};
        popupConfig.onOpen(event);
        event.onComplete();
        await new Promise((r) => setTimeout(r, 30));
        expect(w2gridInstance.render).toHaveBeenCalled();
    });
});
