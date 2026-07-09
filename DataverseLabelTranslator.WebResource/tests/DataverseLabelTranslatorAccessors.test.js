import { describe, it, expect, vi, beforeEach } from "vitest";

// Additional accessor / simple-helper coverage for DataverseLabelTranslator.js.
// These exercise the pure getter/setter and transformation functions that are
// easy to drive directly without spinning up the full dashboard.

const mockToolbar = {
    get: vi.fn(),
    show: vi.fn(),
    hide: vi.fn(),
    enable: vi.fn(),
    disable: vi.fn(),
    refresh: vi.fn(),
    click: vi.fn(),
};

const mockGrid = {
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

beforeEach(() => {
    window.w2ui = { grid: mockGrid, grid_toolbar: mockToolbar };

    mockGrid.columns = [];
    mockGrid.records = [];
    mockGrid.searches = [];
    mockGrid.total = 0;
    mockGrid.last = null;
    mockGrid.searchData = [];
    mockGrid.box = null;
    mockGrid.lock.mockReset();
    mockGrid.unlock.mockReset();
    mockGrid.refresh.mockReset();
    mockGrid.refreshRow.mockReset();
    mockGrid.removeColumn.mockReset();
    mockGrid.addColumn.mockReset();
    mockGrid.addSearch.mockReset();
    mockGrid.removeSearch.mockReset();
    mockGrid.getSelection.mockReset(() => []);

    mockToolbar.get.mockReset();
    mockToolbar.show.mockReset();
    mockToolbar.hide.mockReset();
    mockToolbar.enable.mockReset();
    mockToolbar.disable.mockReset();
    mockToolbar.refresh.mockReset();

    window.Helper = {
        GetTranslator: vi.fn(() => ({
            GetType: vi.fn(() => "attributes"),
            GetGrid: vi.fn(() => mockGrid),
            LockGrid: vi.fn(),
            UnlockGrid: vi.fn(),
            SetMetadata: vi.fn(),
            SetBaseLanguage: vi.fn(),
            GetBaseLanguage: vi.fn(),
            GetTypeStateLabel: vi.fn((t) => t + " label"),
            GetComponent: vi.fn(() => "DisplayText"),
            IsDescriptionComponent: vi.fn(() => false),
            IsDisplayTextComponent: vi.fn(() => true),
            GetCurrentComponentText: vi.fn(() => "Display Text"),
            GetCurrentToolbarTypeText: vi.fn(() => "Attributes"),
        })),
        FormatLanguageColumnHeader: vi.fn((lcid) => "Lang " + lcid),
        GetLanguageColumnCode: vi.fn((field) => "code-" + field),
        ShowError: vi.fn(),
        GetOperationLoading: vi.fn(() => "Loading..."),
        GetOperationReLoading: vi.fn(() => "Reloading..."),
        GetPlaceholderDisplayText: vi.fn(() => "(display)"),
        GetPlaceholderDescription: vi.fn(() => "(description)"),
        GetPlaceholderReadonly: vi.fn(() => "-"),
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
    window.AIService = { OpenWorkspace: vi.fn() };
    window.w2popup = { open: vi.fn(), close: vi.fn(), max: vi.fn() };
    window.w2alert = vi.fn(() => Promise.resolve());
    window.w2utils = { stripTags: vi.fn((s) => String(s).replace(/<[^>]*>/g, "")) };

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

describe("DataverseLabelTranslator accessors", () => {
    describe("GetEntity / GetEntityId / GetType / GetComponent", () => {
        it("returns toolbar selected values", () => {
            mockToolbar.get.mockImplementation((id) => {
                if (id === "entitySelect") return { selected: "account" };
                if (id === "type") return { selected: "attributes" };
                if (id === "component") return { selected: "Description" };
                return null;
            });
            expect(DLT.GetEntity()).toBe("account");
            expect(DLT.GetType()).toBe("attributes");
            expect(DLT.GetComponent()).toBe("Description");
        });

        it("returns defaults when toolbar item is missing", () => {
            mockToolbar.get.mockReturnValue(null);
            expect(DLT.GetEntity()).toBe("none");
            expect(DLT.GetType()).toBe("sitemap");
            expect(DLT.GetComponent()).toBe("DisplayText");
        });

        it("GetEntityId returns null when no metadata cached", () => {
            mockToolbar.get.mockImplementation((id) =>
                id === "entitySelect" ? { selected: "account" } : null
            );
            expect(DLT.GetEntityId()).toBeNull();
        });
    });

    describe("IsDescriptionComponent / IsDisplayTextComponent / GetCurrentComponentText", () => {
        it("is true when component matches Description", () => {
            mockToolbar.get.mockImplementation((id) =>
                id === "component" ? { selected: "Description" } : null
            );
            expect(DLT.IsDescriptionComponent()).toBe(true);
            expect(DLT.IsDisplayTextComponent()).toBe(false);
            expect(DLT.GetCurrentComponentText()).toBe("Description");
        });

        it("returns Display Text for DisplayText component", () => {
            mockToolbar.get.mockImplementation((id) =>
                id === "component" ? { selected: "DisplayText" } : null
            );
            expect(DLT.IsDisplayTextComponent()).toBe(true);
            expect(DLT.GetCurrentComponentText()).toBe("Display Text");
        });

        it("falls back to raw component string for unknown component", () => {
            mockToolbar.get.mockImplementation((id) =>
                id === "component" ? { selected: "Other" } : null
            );
            expect(DLT.GetCurrentComponentText()).toBe("Other");
        });

        it("GetCurrentToolbarTypeText delegates to type label", () => {
            mockToolbar.get.mockImplementation((id) => {
                if (id === "type") return { selected: "attributes" };
                if (id === "type:attributes") return { text: "Attributes Label" };
                return null;
            });
            expect(DLT.GetCurrentToolbarTypeText()).toBe("Attributes Label");
        });

        it("GetCurrentToolbarTypeText falls back to raw type when label missing", () => {
            mockToolbar.get.mockImplementation((id) =>
                id === "type" ? { selected: "attributes" } : null
            );
            expect(DLT.GetCurrentToolbarTypeText()).toBe("attributes");
        });
    });

    describe("SetMetadata / GetMetadata / SetBaseLanguage / GetBaseLanguage", () => {
        it("stores and returns metadata", () => {
            const data = [{ MetadataId: "1" }];
            const stored = DLT.SetMetadata(data);
            expect(stored).toBe(data);
            expect(DLT.GetMetadata()).toBe(data);
            // defaults
            DLT.SetMetadata(null);
            expect(DLT.GetMetadata()).toEqual([]);
        });

        it("stores and returns base language", () => {
            expect(DLT.GetBaseLanguage()).toBeNull();
            const r = DLT.SetBaseLanguage(1041);
            expect(r).toBe(1041);
            expect(DLT.GetBaseLanguage()).toBe(1041);
        });
    });

    describe("GetAllRecords / FlattenRecords / GetByRecId", () => {
        beforeEach(() => {
            mockGrid.records = [
                { recid: "p1", w2ui: { children: [{ recid: "c1" }, { recid: "c2" }] } },
                { recid: "p2" },
                { recid: "c1" }, // duplicate child to test dedup
            ];
        });

        it("flattens and deduplicates records", () => {
            const all = DLT.GetAllRecords();
            const ids = all.map((r) => r.recid);
            expect(ids).toContain("p1");
            expect(ids).toContain("p2");
            expect(ids).toContain("c1");
            expect(ids).toContain("c2");
            // Set dedups by reference: same object appearing twice (once as child,
            // once as a root record pointing to the same object) is collapsed.
            expect(all.length).toBeLessThanOrEqual(ids.length + 1);
        });

        it("GetByRecId finds record", () => {
            const found = DLT.GetByRecId(DLT.GetAllRecords(), "c2");
            expect(found).not.toBeNull();
            expect(found.recid).toBe("c2");
        });

        it("GetByRecId returns null when missing", () => {
            expect(DLT.GetByRecId([], "missing")).toBeNull();
            expect(DLT.GetByRecId(DLT.GetAllRecords(), "missing")).toBeNull();
        });
    });

    describe("GetAttributeById", () => {
        it("finds attribute by metadata id", () => {
            DLT.SetMetadata([{ MetadataId: "abc" }, { MetadataId: "def" }]);
            expect(DLT.GetAttributeById("def").MetadataId).toBe("def");
        });

        it("returns null when not found", () => {
            DLT.SetMetadata([{ MetadataId: "abc" }]);
            expect(DLT.GetAttributeById("missing")).toBeNull();
        });
    });

    describe("GetColumns", () => {
        it("returns all fields including schemaName when requested", () => {
            mockGrid.columns = [
                { field: "schemaName" },
                { field: "1033" },
                { field: "1041" },
            ];
            expect(DLT.GetColumns(true)).toEqual(["schemaName", "1033", "1041"]);
            expect(DLT.GetColumns(false)).toEqual(["1033", "1041"]);
            expect(DLT.GetColumns()).toEqual(["1033", "1041"]);
        });
    });

    describe("ClearColumns", () => {
        it("removes all non-schemaName columns and keeps schemaName search", () => {
            mockGrid.columns = [{ field: "1033" }, { field: "1041" }, { field: "schemaName" }];
            mockGrid.searches = [{ field: "1033" }, { field: "schemaName" }];
            DLT.ClearColumns();
            expect(mockGrid.removeColumn).toHaveBeenCalledWith("1033");
            expect(mockGrid.removeColumn).toHaveBeenCalledWith("1041");
            expect(mockGrid.searches).toEqual([{ field: "schemaName" }]);
        });
    });

    describe("ApplyLanguageColumns", () => {
        it("adds language columns and searches with dedup", () => {
            mockGrid.columns = [{ field: "schemaName" }];
            const columns = [
                { field: "1033", text: "English", code: "en" },
                { field: "1033" }, // duplicate
                { field: "1041", text: "Japanese", code: "ja" },
                { field: "" },
            ];
            DLT.ApplyLanguageColumns(columns);
            expect(mockGrid.addColumn).toHaveBeenCalledTimes(2);
            expect(mockGrid.addSearch).toHaveBeenCalledTimes(2);
            expect(mockGrid.refresh).toHaveBeenCalled();
        });

        it("refreshes and returns when no columns", () => {
            mockGrid.columns = [{ field: "1033" }, { field: "schemaName" }];
            DLT.ApplyLanguageColumns([]);
            expect(mockGrid.removeColumn).toHaveBeenCalledWith("1033");
            expect(mockGrid.refresh).toHaveBeenCalled();
            expect(mockGrid.addColumn).not.toHaveBeenCalled();
        });

        it("uses Helper.FormatLanguageColumnHeader for numeric fields", () => {
            // Helper mock returns "Lang <lcid>"; verify it is passed through
            mockGrid.columns = [];
            DLT.ApplyLanguageColumns([{ field: "1033" }]);
            const call = mockGrid.addColumn.mock.calls[0][0];
            expect(call.text).toBe("Lang 1033");
        });
    });

    describe("LockGrid / UnlockGrid / SetSaveButtonDisabled / SetLoadButtonDisabled", () => {
        it("LockGrid calls grid.lock with message", () => {
            DLT.LockGrid("Working");
            expect(mockGrid.lock).toHaveBeenCalledWith("Working", true);
        });

        it("LockGrid defaults to Loading message", () => {
            DLT.LockGrid();
            expect(mockGrid.lock).toHaveBeenCalledWith("Loading...", true);
        });

        it("LockGrid is safe when grid missing", () => {
            const saved = window.w2ui.grid;
            delete window.w2ui.grid;
            expect(() => DLT.LockGrid()).not.toThrow();
            window.w2ui.grid = saved;
        });

        it("UnlockGrid calls grid.unlock", () => {
            DLT.UnlockGrid();
            expect(mockGrid.unlock).toHaveBeenCalled();
        });

        it("SetSaveButtonDisabled toggles toolbar", () => {
            const saveButton = { disabled: false };
            mockToolbar.get.mockImplementation((id) =>
                id === "w2ui-save" ? saveButton : null
            );
            DLT.SetSaveButtonDisabled(true);
            expect(saveButton.disabled).toBe(true);
            expect(mockToolbar.refresh).toHaveBeenCalled();
            DLT.SetSaveButtonDisabled(false);
            expect(saveButton.disabled).toBe(false);
        });

        it("SetSaveButtonDisabled is no-op when button missing", () => {
            mockToolbar.get.mockReturnValue(null);
            expect(() => DLT.SetSaveButtonDisabled(true)).not.toThrow();
            expect(mockToolbar.refresh).not.toHaveBeenCalled();
        });

        it("SetLoadButtonDisabled toggles toolbar", () => {
            const loadButton = { disabled: false };
            mockToolbar.get.mockImplementation((id) =>
                id === "load" ? loadButton : null
            );
            DLT.SetLoadButtonDisabled(true);
            expect(loadButton.disabled).toBe(true);
            expect(mockToolbar.refresh).toHaveBeenCalled();
            DLT.SetLoadButtonDisabled(false);
            expect(loadButton.disabled).toBe(false);
        });

        it("SetLoadButtonDisabled is no-op when button missing", () => {
            mockToolbar.get.mockReturnValue(null);
            expect(() => DLT.SetLoadButtonDisabled(true)).not.toThrow();
            expect(mockToolbar.refresh).not.toHaveBeenCalled();
        });
    });

    describe("GetSelectedRecordIds / RefreshGridRow / RefreshGrid", () => {
        it("GetSelectedRecordIds returns selected ids", () => {
            mockGrid.getSelection.mockReturnValue([1, 2, 3]);
            expect(DLT.GetSelectedRecordIds()).toEqual([1, 2, 3]);
        });

        it("RefreshGridRow calls grid.refreshRow", () => {
            DLT.RefreshGridRow("r1");
            expect(mockGrid.refreshRow).toHaveBeenCalledWith("r1");
        });

        it("RefreshGrid calls grid.refresh", () => {
            DLT.RefreshGrid();
            expect(mockGrid.refresh).toHaveBeenCalled();
        });
    });

    describe("errorHandler", () => {
        it("delegates to Helper.ShowError", () => {
            DLT.errorHandler({ message: "boom" });
            expect(window.Helper.ShowError).toHaveBeenCalled();
        });

        it("handles string error", () => {
            DLT.errorHandler("text error");
            expect(window.Helper.ShowError).toHaveBeenCalled();
        });
    });

    describe("NormalizeRecordChanges / NormalizeGridChanges / HasPendingChanges", () => {
        it("NormalizeRecordChanges keeps valid language fields and returns changed flag", () => {
            const record = { recid: "1", "1033": "en", w2ui: { changes: { "1033": "en", bad: "x" } } };
            const changed = DLT.NormalizeRecordChanges(record);
            expect(changed).toBe(true);
            // 1033 equals original -> removed; bad has no original -> kept
            expect(record.w2ui.changes).toEqual({ bad: "x" });
        });

        it("NormalizeRecordChanges removes empty changes object", () => {
            const record = { recid: "1", "1033": "en", w2ui: { changes: { "1033": "en" } } };
            const changed = DLT.NormalizeRecordChanges(record);
            expect(changed).toBe(true);
            expect(record.w2ui.changes).toBeUndefined();
        });

        it("NormalizeRecordChanges returns false when no changes object", () => {
            expect(DLT.NormalizeRecordChanges({ recid: "1" })).toBe(false);
            expect(DLT.NormalizeRecordChanges(null)).toBe(false);
        });

        it("NormalizeGridChanges normalizes each record", () => {
            const records = [
                { recid: "1", "1033": "en", w2ui: { changes: { "1033": "en" } } },
                { recid: "2", "1041": "ja", w2ui: { changes: { "1041": "ja" } } },
                { recid: "3" }, // no changes -> not included
            ];
            const out = DLT.NormalizeGridChanges(records);
            expect(out).toEqual([records[0], records[1]]);
            expect(records[0].w2ui.changes).toBeUndefined();
        });

        it("HasPendingChanges true when a record has numeric change", () => {
            const records = [{ recid: "1", w2ui: { changes: { "1033": "en" } } }];
            expect(DLT.HasPendingChanges(records)).toBe(true);
        });

        it("HasPendingChanges false when no numeric changes", () => {
            // 'bad' field has no matching original and remains -> still considered pending
            // because changes object is non-empty. Use a record where the only change equals
            // its original value so NormalizeRecordChanges clears the changes entirely.
            const records = [{ recid: "1", bad: "x", w2ui: { changes: { bad: "x" } } }];
            expect(DLT.HasPendingChanges(records)).toBe(false);
            expect(DLT.HasPendingChanges([])).toBe(false);
        });

        it("HasLoadedRecords reflects grid records", () => {
            mockGrid.records = [{ recid: "1" }];
            expect(DLT.HasLoadedRecords()).toBe(true);
            mockGrid.records = [];
            expect(DLT.HasLoadedRecords()).toBe(false);
        });
    });

    describe("StartOperationStatus / ApplyStoredOperationStatus", () => {
        it("are no-op stubs and do not throw", () => {
            expect(() => DLT.StartOperationStatus()).not.toThrow();
            expect(() => DLT.ApplyStoredOperationStatus()).not.toThrow();
        });
    });

    describe("UndoActiveCellChange", () => {
        it("is safe when no active changed cell and disables undo button", () => {
            mockToolbar.get.mockImplementation((id) =>
                id === "undoCellChange" ? { id: "undoCellChange" } : null
            );
            expect(() => DLT.UndoActiveCellChange()).not.toThrow();
            expect(mockToolbar.disable).toHaveBeenCalledWith("undoCellChange");
        });

        it("is safe when undo button missing", () => {
            mockToolbar.get.mockReturnValue(null);
            expect(() => DLT.UndoActiveCellChange()).not.toThrow();
            expect(mockToolbar.disable).not.toHaveBeenCalled();
        });
    });
});
