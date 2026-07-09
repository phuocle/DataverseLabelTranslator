import { describe, it, expect, vi, beforeEach } from "vitest";

// Coverage for additional public functions of DataverseLabelTranslator.js:
// IsUnifiedType, RenderTranslationCell, CreateTranslationCellRenderer,
// ApplyGridChangeValue, ShowStatusBanner, BuildAiTranslateDataSource,
// ShowAITranslate, GetSolution, GetEventColumn-driven UpdateChangedCellFooter.

const mockToolbar = {
    get: vi.fn(),
    show: vi.fn(),
    hide: vi.fn(),
    enable: vi.fn(),
    disable: vi.fn(),
    refresh: vi.fn(),
    click: vi.fn(),
    insert: vi.fn(),
};

const mockGrid = {
    name: "grid",
    columns: [],
    records: [],
    searches: [],
    total: 0,
    lock: vi.fn(),
    unlock: vi.fn(),
    refresh: vi.fn(),
    refreshRow: vi.fn(),
    removeColumn: vi.fn(),
    addColumn: vi.fn(),
    addSearch: vi.fn(),
    removeSearch: vi.fn(),
    getSelection: vi.fn(() => []),
    show: {},
    last: null,
    searchData: [],
    box: null,
    localSearch: vi.fn(),
};

const footerEl = { innerHTML: "" };
const gridBox = {
    querySelector: vi.fn(() => footerEl),
    querySelectorAll: vi.fn(() => []),
    contains: vi.fn(() => true),
};

beforeEach(() => {
    window.w2ui = { grid: mockGrid, grid_toolbar: mockToolbar };

    mockGrid.columns = [];
    mockGrid.records = [];
    mockGrid.searches = [];
    mockGrid.total = 0;
    mockGrid.last = null;
    mockGrid.searchData = [];
    mockGrid.box = gridBox;
    mockGrid.lock.mockReset();
    mockGrid.unlock.mockReset();
    mockGrid.refresh.mockReset();
    mockGrid.refreshRow.mockReset();
    mockGrid.removeColumn.mockReset();
    mockGrid.addColumn.mockReset();
    mockGrid.addSearch.mockReset();
    mockGrid.getSelection.mockReset(() => []);

    mockToolbar.get.mockReset();
    mockToolbar.show.mockReset();
    mockToolbar.hide.mockReset();
    mockToolbar.enable.mockReset();
    mockToolbar.disable.mockReset();
    mockToolbar.refresh.mockReset();
    mockToolbar.insert.mockReset();

    footerEl.innerHTML = "";

    window.Helper = {
        GetTranslator: vi.fn(() => ({
            GetType: vi.fn(() => "attributes"),
            GetGrid: vi.fn(() => mockGrid),
            LockGrid: vi.fn(),
            UnlockGrid: vi.fn(),
            SetMetadata: vi.fn(),
            SetBaseLanguage: vi.fn(),
            GetBaseLanguage: vi.fn(() => 1033),
            GetTypeStateLabel: vi.fn((t) => t + " label"),
            GetComponent: vi.fn(() => "DisplayText"),
            IsDescriptionComponent: vi.fn(() => false),
            IsDisplayTextComponent: vi.fn(() => true),
            GetCurrentComponentText: vi.fn(() => "Display Text"),
            GetCurrentToolbarTypeText: vi.fn(() => "Attributes"),
            GetSolution: vi.fn(() => "sol1"),
            GetEntity: vi.fn(() => "account"),
            GetEntityId: vi.fn(() => "guid-1"),
        })),
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
        GetSolutions: vi.fn(() => Promise.resolve([])),
        GetEntities: vi.fn(() => Promise.resolve([])),
        GetBaseLanguage: vi.fn(() => Promise.resolve(1033)),
        GetAllNoneBaseLanguageCodes: vi.fn(() => Promise.resolve({ LocaleIds: [] })),
        BuildLanguageColumns: vi.fn(() => Promise.resolve([])),
    };

    window.DialogHelper = {
        alert: vi.fn(() => Promise.resolve()),
        confirm: vi.fn(() => Promise.resolve(false)),
        question: vi.fn(() => Promise.resolve("")),
    };
    window.AppService = {
        EnsureInitialized: vi.fn(() => Promise.resolve()),
        GetSettings: vi.fn(),
        SaveSettings: vi.fn(),
    };
    window.DictionaryService = {
        EnsureInitialized: vi.fn(() => Promise.resolve()),
        GetStorageInfo: vi.fn(),
        UpsertEntries: vi.fn(() => Promise.resolve()),
        SaveFromGrid: vi.fn(() => Promise.resolve()),
    };
    window.AIService = { OpenWorkspace: vi.fn(() => Promise.resolve()) };
    window.w2popup = { open: vi.fn(), close: vi.fn(), max: vi.fn() };
    window.w2alert = vi.fn(() => Promise.resolve());
    window.w2utils = {
        stripTags: vi.fn((s) => String(s).replace(/<[^>]*>/g, "")),
        decodeTags: vi.fn((s) => String(s)),
    };

    if (!globalThis.document || !globalThis.document.getElementById) {
        globalThis.document = {
            getElementById: vi.fn(),
            querySelector: vi.fn(),
            querySelectorAll: vi.fn(() => []),
            createElement: vi.fn(() => ({
                innerHTML: "",
                setAttribute: vi.fn(),
                appendChild: vi.fn(),
                style: {},
            })),
        };
    }
});

import "../js/DataverseLabelTranslator.js";

const DLT = window.DataverseLabelTranslator;

describe("IsUnifiedType", () => {
    it.each([
        "sitemap",
        "dashboards",
        "webresources",
        "globalOptionSet",
        "attributes",
        "options",
        "forms",
        "entityMeta",
        "views",
        "formMeta",
        "relationships",
        "charts",
        "ribbons",
        "bpf",
        "entityMessages",
        "commands",
        "businessRules",
        "content",
    ])("returns true for valid unified type %s", (type) => {
        expect(DLT.IsUnifiedType(type)).toBe(true);
    });

    it("returns false for unknown type", () => {
        expect(DLT.IsUnifiedType("nope")).toBe(false);
        expect(DLT.IsUnifiedType("")).toBe(false);
        expect(DLT.IsUnifiedType(undefined)).toBe(false);
        expect(DLT.IsUnifiedType(null)).toBe(false);
    });
});

describe("RenderTranslationCell / CreateTranslationCellRenderer", () => {
    it("escapes an existing value", () => {
        expect(DLT.RenderTranslationCell({ "1033": "Hello <b>World</b>" }, "1033")).toBe(
            "Hello &lt;b&gt;World&lt;/b&gt;"
        );
    });

    it("prefers w2ui.changes value over the record value", () => {
        const record = { "1033": "old", w2ui: { changes: { "1033": "new" } } };
        expect(DLT.RenderTranslationCell(record, "1033")).toBe("new");
    });

    it("returns empty string when value is empty and no placeholders", () => {
        expect(DLT.RenderTranslationCell({ "1033": "" }, "1033")).toBe("");
        expect(DLT.RenderTranslationCell({}, "1033")).toBe("");
        expect(DLT.RenderTranslationCell(null, "1033")).toBe("");
    });

    it("renders _emptyEditablePlaceholders field hint", () => {
        const record = { "1033": "", _emptyEditablePlaceholders: { "1033": "(edit me)" } };
        const out = DLT.RenderTranslationCell(record, "1033");
        expect(out).toContain("xqt-empty-cell-hint-editable");
        expect(out).toContain("(edit me)");
        expect(out).toContain("Click to edit");
    });

    it("renders _emptyEditablePlaceholder hint", () => {
        const record = { "1033": "", _emptyEditablePlaceholder: "(edit any)" };
        const out = DLT.RenderTranslationCell(record, "1033");
        expect(out).toContain("xqt-empty-cell-hint-editable");
        expect(out).toContain("(edit any)");
    });

    it("renders _emptyReadonlyPlaceholder hint", () => {
        const record = { "1033": "", _emptyReadonlyPlaceholder: "N/A" };
        const out = DLT.RenderTranslationCell(record, "1033");
        expect(out).toContain("xqt-empty-cell-hint-readonly");
        expect(out).toContain("Read only");
        expect(out).toContain("N/A");
    });

    it("escapes placeholder HTML", () => {
        const record = { "1033": "", _emptyReadonlyPlaceholder: "<x>" };
        expect(DLT.RenderTranslationCell(record, "1033")).toContain("&lt;x&gt;");
    });

    it("CreateTranslationCellRenderer returns a bound renderer", () => {
        const render = DLT.CreateTranslationCellRenderer("1033");
        expect(typeof render).toBe("function");
        expect(render({ "1033": "Hi" })).toBe("Hi");
        expect(render({ "1033": "" })).toBe("");
    });
});

describe("ApplyGridChangeValue", () => {
    it("returns false when record is null", () => {
        expect(DLT.ApplyGridChangeValue(null, "1033", "x")).toBe(false);
    });

    it("deletes existing change when new value equals original", () => {
        const record = { recid: "1", "1033": "orig", w2ui: { changes: { "1033": "modified" } } };
        // value matches original -> change should be removed
        const result = DLT.ApplyGridChangeValue(record, "1033", "orig");
        expect(result).toBe(true);
        expect(record.w2ui.changes).toBeUndefined();
    });

    it("returns false when value equals original and no existing change", () => {
        const record = { recid: "1", "1033": "orig" };
        expect(DLT.ApplyGridChangeValue(record, "1033", "orig")).toBe(false);
    });

    it("sets a new change value when different", () => {
        const record = { recid: "1", "1033": "orig" };
        expect(DLT.ApplyGridChangeValue(record, "1033", "newval")).toBe(true);
        expect(record.w2ui.changes["1033"]).toBe("newval");
    });

    it("returns false when value equals an already-stored change", () => {
        const record = { recid: "1", "1033": "orig", w2ui: { changes: { "1033": "stored" } } };
        expect(DLT.ApplyGridChangeValue(record, "1033", "stored")).toBe(false);
    });

    it("updates change when value differs from stored change", () => {
        const record = { recid: "1", "1033": "orig", w2ui: { changes: { "1033": "stored" } } };
        expect(DLT.ApplyGridChangeValue(record, "1033", "updated")).toBe(true);
        expect(record.w2ui.changes["1033"]).toBe("updated");
    });

    it("treats null/undefined value as no-op-on-original-equality", () => {
        const record = { recid: "1" };
        // null original vs null value -> equal, no existing change -> false
        expect(DLT.ApplyGridChangeValue(record, "1033", null)).toBe(false);
        // undefined original vs undefined -> equal, false
        expect(DLT.ApplyGridChangeValue(record, "missing", undefined)).toBe(false);
    });
});

describe("ShowStatusBanner", () => {
    it("alerts when a message is provided", () => {
        DLT.ShowStatusBanner({ message: "hi", title: "X" });
        expect(window.DialogHelper.alert).toHaveBeenCalledWith("hi", { title: "X" });
    });

    it("defaults title to Status", () => {
        DLT.ShowStatusBanner({ message: "m" });
        expect(window.DialogHelper.alert).toHaveBeenCalledWith("m", { title: "Status" });
    });

    it("does nothing when no options or no message", () => {
        expect(() => DLT.ShowStatusBanner()).not.toThrow();
        expect(() => DLT.ShowStatusBanner({})).not.toThrow();
        expect(() => DLT.ShowStatusBanner({ title: "t" })).not.toThrow();
        expect(window.DialogHelper.alert).not.toHaveBeenCalled();
    });
});

describe("GetSolution", () => {
    it("returns selected solution when toolbar item present", () => {
        mockToolbar.get.mockImplementation((id) =>
            id === "solutionSelect" ? { selected: "sol1" } : null
        );
        expect(DLT.GetSolution()).toBe("sol1");
    });

    it("returns null when toolbar item missing", () => {
        mockToolbar.get.mockReturnValue(null);
        expect(DLT.GetSolution()).toBeNull();
    });
});

describe("BuildAiTranslateDataSource", () => {
    beforeEach(() => {
        mockToolbar.get.mockImplementation((id) => {
            if (id === "type") return { selected: "attributes" };
            if (id === "component") return { selected: "DisplayText" };
            return null;
        });
    });

    it("returns data source with language columns and base lcid", () => {
        DLT.SetBaseLanguage(1033);
        mockGrid.columns = [
            { field: "schemaName", text: "Schema Name" },
            { field: "1033", text: "English", code: "en" },
            { field: "1041", text: "Japanese", code: "ja" },
        ];
        mockGrid.records = [
            { recid: "r1", schemaName: "attr1", "1033": "Hello", "1041": "Konnichiwa" },
        ];
        const ds = DLT.BuildAiTranslateDataSource();
        // typeName uses public GetTypeStateLabel(internal GetType=toolbar.get("type").selected
        // -> "attributes" -> GetTypeStateLabel looks up toolbar.get("type:attributes")
        // which we don't mock here -> falls back to raw type "attributes".
        expect(ds.typeName).toBe("attributes");
        expect(ds.languages.length).toBe(2);
        expect(ds.languages[0].lcid).toBe("1033");
        expect(ds.baseLcid).toBe("1033");
        expect(typeof ds.applyChanges).toBe("function");
    });

    it("falls back to first language lcid when base language not set", () => {
        DLT.SetBaseLanguage(null);
        mockGrid.columns = [{ field: "1041", text: "Japanese", code: "ja" }];
        mockGrid.records = [];
        const ds = DLT.BuildAiTranslateDataSource();
        expect(ds.baseLcid).toBe("1041");
    });

    it("uses empty base lcid when no languages and no base language", () => {
        DLT.SetBaseLanguage(null);
        mockGrid.columns = [];
        mockGrid.records = [];
        const ds = DLT.BuildAiTranslateDataSource();
        expect(ds.baseLcid).toBe("");
        expect(ds.languages).toEqual([]);
    });
});

describe("ShowAITranslate", () => {
    beforeEach(() => {
        mockToolbar.get.mockImplementation((id) => {
            if (id === "type") return { selected: "attributes" };
            if (id === "component") return { selected: "DisplayText" };
            return null;
        });
    });

    it("warns unavailable when AIService.OpenWorkspace is missing", async () => {
        const saved = window.AIService;
        window.AIService = {};
        await DLT.ShowAITranslate();
        // DialogHelper.alert is the preferred unavailable path when present.
        expect(window.DialogHelper.alert).toHaveBeenCalled();
        window.AIService = saved;
    });

    it("opens workspace when translatable rows exist", async () => {
        DLT.SetBaseLanguage(1033);
        mockGrid.columns = [
            { field: "schemaName", text: "Schema Name" },
            { field: "1033", text: "English", code: "en" },
            { field: "1041", text: "Japanese", code: "ja" },
        ];
        mockGrid.records = [
            { recid: "r1", gridKey: "key1", schemaName: "attr1", "1033": "Hello", "1041": "" },
        ];
        await DLT.ShowAITranslate();
        expect(window.AIService.OpenWorkspace).toHaveBeenCalled();
    });

    it("warns unavailable when no translatable rows", async () => {
        DLT.SetBaseLanguage(1033);
        mockGrid.columns = [{ field: "schemaName", text: "Schema Name" }];
        mockGrid.records = [];
        await DLT.ShowAITranslate();
        // No language columns -> rows are not translatable -> unavailable path uses DialogHelper.alert.
        expect(window.DialogHelper.alert).toHaveBeenCalled();
        expect(window.AIService.OpenWorkspace).not.toHaveBeenCalled();
    });
});

describe("UpdateChangedCellFooter", () => {
    it("clears footer when event has no record context", () => {
        // empty event -> no context -> footer cleared
        DLT.UpdateChangedCellFooter({});
        expect(footerEl.innerHTML).toBe("");
    });

    it("clears footer when record has no matching change for the column", () => {
        mockGrid.columns = [{ field: "1033", text: "English" }];
        mockGrid.records = [{ recid: "r1", "1033": "orig" }];
        DLT.UpdateChangedCellFooter({ recid: "r1", column: 0 });
        expect(footerEl.innerHTML).toBe("");
    });

    it("sets footer HTML and active cell when record has a matching change", () => {
        mockGrid.columns = [{ field: "1033", text: "English" }];
        mockGrid.records = [{ recid: "r1", "1033": "orig", w2ui: { changes: { "1033": "new" } } }];
        DLT.UpdateChangedCellFooter({ recid: "r1", column: 0 });
        expect(footerEl.innerHTML).toContain("English");
        expect(footerEl.innerHTML).toContain("orig");
        expect(footerEl.innerHTML).toContain("new");
    });

    it("resolves column by string field name", () => {
        mockGrid.columns = [{ field: "1033", text: "English" }];
        mockGrid.records = [{ recid: "r1", "1033": "orig", w2ui: { changes: { "1033": "ed" } } }];
        DLT.UpdateChangedCellFooter({ recid: "r1", column: "1033" });
        expect(footerEl.innerHTML).toContain("ed");
    });
});

describe("RemoveOverriddenCellLabels", () => {
    it("returns null when not confirmed", async () => {
        window.DialogHelper.confirm.mockResolvedValueOnce(false);
        const result = await DLT.RemoveOverriddenCellLabels();
        expect(result).toBeNull();
        expect(window.Helper.RunServerSaveFlow).not.toHaveBeenCalled();
    });

    it("runs save flow when confirmed", async () => {
        window.DialogHelper.confirm.mockResolvedValueOnce(true);
        window.Helper.RunServerSaveFlow.mockResolvedValueOnce("done");
        mockToolbar.get.mockImplementation((id) => {
            if (id === "entitySelect") return { selected: "account" };
            if (id === "type") return { selected: "forms" };
            if (id === "component") return { selected: "DisplayText" };
            if (id === "solutionSelect") return { selected: "sol1" };
            return null;
        });
        const result = await DLT.RemoveOverriddenCellLabels();
        expect(window.Helper.RunServerSaveFlow).toHaveBeenCalled();
        expect(result).toBe("done");
    });
});
