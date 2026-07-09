import { describe, it, expect, vi, beforeEach } from "vitest";

// --- Setup global IIFE host before loading Helper.js ---
const mockXrm = {
    WebApi: {
        online: {
            execute: vi.fn()
        }
    }
};

const mockDialogHelper = {
    alert: vi.fn()
};

const mockW2Alert = vi.fn();
const mockTranslator = {
    GetGrid: vi.fn(),
    UnlockGrid: vi.fn(),
    LockGrid: vi.fn(),
    baseLanguage: null,
    GetBaseLanguage: vi.fn(),
    IsDisplayTextComponent: vi.fn()
};

beforeEach(() => {
    globalThis.Xrm = mockXrm;
    globalThis.window.Xrm = mockXrm;
    globalThis.window.parent = { Xrm: mockXrm };
    globalThis.window.DialogHelper = mockDialogHelper;
    globalThis.window.w2alert = mockW2Alert;
    globalThis.window.alert = vi.fn();
    globalThis.window.DataverseLabelTranslator = mockTranslator;
    globalThis.window.Helper = globalThis.window.Helper || {};

    mockXrm.WebApi.online.execute.mockReset();
    mockDialogHelper.alert.mockReset();
    mockW2Alert.mockReset();
    mockTranslator.GetGrid.mockReset();
    mockTranslator.UnlockGrid.mockReset();
    mockTranslator.LockGrid.mockReset();
    mockTranslator.baseLanguage = null;
    mockTranslator.GetBaseLanguage.mockReset();
    mockTranslator.IsDisplayTextComponent.mockReset();
});

// --- Load Helper.js into the test environment ---
await import("../js/Helper.js");

const Helper = globalThis.window.Helper;

// ============================================================
// Constants
// ============================================================
describe("Helper constants", () => {
    it("CustomActionTypes has expected keys", () => {
        expect(Helper.CustomActionTypes.Loading).toBe("Loading");
        expect(Helper.CustomActionTypes.Saving).toBe("Saving");
        expect(Helper.CustomActionTypes.Publishing).toBe("Publishing");
        expect(Helper.CustomActionTypes.Published).toBe("Published");
        expect(Helper.CustomActionTypes.Other).toBe("Other");
    });

    it("ComponentTypes has expected keys", () => {
        expect(Helper.ComponentTypes.DisplayText).toBe("DisplayText");
        expect(Helper.ComponentTypes.Description).toBe("Description");
    });

    it("UiText.Placeholders has expected values", () => {
        expect(Helper.UiText.Placeholders.DisplayText).toBe("Add-display-text");
        expect(Helper.UiText.Placeholders.Description).toBe("Add-description");
        expect(Helper.UiText.Placeholders.Readonly).toBe("-");
    });

    it("UiText.Operations has expected values", () => {
        expect(Helper.UiText.Operations.Loading).toBe("Loading ....");
        expect(Helper.UiText.Operations.Saving).toBe("Saving ...");
        expect(Helper.UiText.Operations.Publishing).toBe("Publishing ...");
        expect(Helper.UiText.Operations.Published).toBe("Published");
        expect(Helper.UiText.Operations.ReLoading).toBe("Re-Loading ...");
    });
});

// ============================================================
// IsEmptyLabelValue
// ============================================================
describe("Helper.IsEmptyLabelValue", () => {
    it("returns true for null", () => {
        expect(Helper.IsEmptyLabelValue(null)).toBe(true);
    });

    it("returns true for undefined", () => {
        expect(Helper.IsEmptyLabelValue(undefined)).toBe(true);
    });

    it("returns true for empty string", () => {
        expect(Helper.IsEmptyLabelValue("")).toBe(true);
    });

    it("returns true for whitespace-only string", () => {
        expect(Helper.IsEmptyLabelValue("   ")).toBe(true);
    });

    it("returns false for non-empty string", () => {
        expect(Helper.IsEmptyLabelValue("hello")).toBe(false);
    });

    it("returns false for number", () => {
        expect(Helper.IsEmptyLabelValue(0)).toBe(false);
        expect(Helper.IsEmptyLabelValue(123)).toBe(false);
    });

    it("returns false for non-empty trimmed string", () => {
        expect(Helper.IsEmptyLabelValue("  x  ")).toBe(false);
    });
});

// ============================================================
// GetPlaceholder helpers
// ============================================================
describe("GetPlaceholder helpers", () => {
    it("GetPlaceholderDisplayText returns correct value", () => {
        expect(Helper.GetPlaceholderDisplayText()).toBe("Add-display-text");
    });

    it("GetPlaceholderDescription returns correct value", () => {
        expect(Helper.GetPlaceholderDescription()).toBe("Add-description");
    });

    it("GetPlaceholderReadonly returns correct value", () => {
        expect(Helper.GetPlaceholderReadonly()).toBe("-");
    });
});

// ============================================================
// HasSearchValue
// ============================================================
describe("Helper.HasSearchValue", () => {
    it("returns false for null", () => {
        expect(Helper.HasSearchValue(null)).toBe(false);
    });

    it("returns false for undefined", () => {
        expect(Helper.HasSearchValue(undefined)).toBe(false);
    });

    it("returns false for empty string", () => {
        expect(Helper.HasSearchValue("")).toBe(false);
    });

    it("returns false for whitespace-only string", () => {
        expect(Helper.HasSearchValue("   ")).toBe(false);
    });

    it("returns true for non-empty string", () => {
        expect(Helper.HasSearchValue("test")).toBe(true);
    });

    it("returns true for string with leading/trailing whitespace", () => {
        expect(Helper.HasSearchValue("  a  ")).toBe(true);
    });
});

// ============================================================
// Operation helpers
// ============================================================
describe("Operation text helpers", () => {
    it("GetOperationLoading", () => {
        expect(Helper.GetOperationLoading()).toBe("Loading ....");
    });

    it("GetOperationSaving", () => {
        expect(Helper.GetOperationSaving()).toBe("Saving ...");
    });

    it("GetOperationPublishing", () => {
        expect(Helper.GetOperationPublishing()).toBe("Publishing ...");
    });

    it("GetOperationPublished", () => {
        expect(Helper.GetOperationPublished()).toBe("Published");
    });

    it("GetOperationReLoading", () => {
        expect(Helper.GetOperationReLoading()).toBe("Re-Loading ...");
    });
});

// ============================================================
// GetCustomActionObject
// ============================================================
describe("Helper.GetCustomActionObject", () => {
    it("returns object property when result has object", () => {
        var obj = { key: "value" };
        expect(Helper.GetCustomActionObject({ object: obj })).toEqual(obj);
    });

    it("returns empty object when result is null", () => {
        expect(Helper.GetCustomActionObject(null)).toEqual({});
    });

    it("returns empty object when result has no object property", () => {
        expect(Helper.GetCustomActionObject({})).toEqual({});
    });
});

// ============================================================
// AddLocalizedLabelsToRecord
// ============================================================
describe("Helper.AddLocalizedLabelsToRecord", () => {
    it("adds labels to record by language code", () => {
        var record = {};
        var labels = [
            { LanguageCode: 1033, Label: "English" },
            { LanguageCode: 1041, Label: "Japanese" }
        ];
        Helper.AddLocalizedLabelsToRecord(record, labels);
        expect(record["1033"]).toBe("English");
        expect(record["1041"]).toBe("Japanese");
    });

    it("handles null localizedLabels gracefully", () => {
        var record = {};
        Helper.AddLocalizedLabelsToRecord(record, null);
        expect(Object.keys(record).length).toBe(0);
    });

    it("handles empty localizedLabels array", () => {
        var record = {};
        Helper.AddLocalizedLabelsToRecord(record, []);
        expect(Object.keys(record).length).toBe(0);
    });
});

// ============================================================
// GetComponentLocalizedLabels
// ============================================================
describe("Helper.GetComponentLocalizedLabels", () => {
    it("returns Label localized labels for DisplayText component", () => {
        var metadata = {
            Label: { LocalizedLabels: [{ LanguageCode: 1033, Label: "Name" }] }
        };
        var result = Helper.GetComponentLocalizedLabels(metadata, Helper.ComponentTypes.DisplayText);
        expect(result.length).toBe(1);
        expect(result[0].Label).toBe("Name");
    });

    it("returns Description localized labels for Description component", () => {
        var metadata = {
            Description: { LocalizedLabels: [{ LanguageCode: 1033, Label: "Desc" }] }
        };
        var result = Helper.GetComponentLocalizedLabels(metadata, Helper.ComponentTypes.Description);
        expect(result.length).toBe(1);
        expect(result[0].Label).toBe("Desc");
    });

    it("returns empty array when metadata is null", () => {
        var result = Helper.GetComponentLocalizedLabels(null, Helper.ComponentTypes.DisplayText);
        expect(result).toEqual([]);
    });

    it("returns empty array when property has no LocalizedLabels", () => {
        var metadata = { Label: {} };
        var result = Helper.GetComponentLocalizedLabels(metadata, Helper.ComponentTypes.DisplayText);
        expect(result).toEqual([]);
    });
});

// ============================================================
// GetChangedLabels
// ============================================================
describe("Helper.GetChangedLabels", () => {
    it("returns labels for each changed field", () => {
        var changes = { "1033": "English", "1041": "Japanese" };
        var result = Helper.GetChangedLabels(changes, false);
        expect(result.length).toBe(2);
        expect(result).toContainEqual({ LanguageCode: "1033", Label: "English" });
        expect(result).toContainEqual({ LanguageCode: "1041", Label: "Japanese" });
    });

    it("skips null labels when allowEmpty is false", () => {
        var changes = { "1033": "English", "1041": null };
        var result = Helper.GetChangedLabels(changes, false);
        expect(result.length).toBe(1);
        expect(result[0].Label).toBe("English");
    });

    it("skips empty string labels when allowEmpty is false", () => {
        var changes = { "1033": "English", "1041": "" };
        var result = Helper.GetChangedLabels(changes, false);
        expect(result.length).toBe(1);
    });

    it("includes null labels as empty string when allowEmpty is true", () => {
        var changes = { "1033": "English", "1041": null };
        var result = Helper.GetChangedLabels(changes, true);
        expect(result.length).toBe(2);
        expect(result).toContainEqual({ LanguageCode: "1041", Label: "" });
    });

    it("skips inherited properties", () => {
        var changes = Object.create({ inherited: "no" });
        changes["1033"] = "English";
        var result = Helper.GetChangedLabels(changes, false);
        expect(result.length).toBe(1);
    });
});

// ============================================================
// ApplyPlaceholder
// ============================================================
describe("Helper.ApplyPlaceholder", () => {
    it("sets _emptyEditablePlaceholder when provided", () => {
        var record = {};
        Helper.ApplyPlaceholder(record, "Add-display-text");
        expect(record._emptyEditablePlaceholder).toBe("Add-display-text");
    });

    it("does not set _emptyEditablePlaceholder when placeholder is falsy", () => {
        var record = {};
        Helper.ApplyPlaceholder(record, null);
        expect(record._emptyEditablePlaceholder).toBeUndefined();
    });

    it("does not set _emptyEditablePlaceholder when placeholder is empty string", () => {
        var record = {};
        Helper.ApplyPlaceholder(record, "");
        expect(record._emptyEditablePlaceholder).toBeUndefined();
    });
});

// ============================================================
// FormatLanguageColumnHeader
// ============================================================
describe("Helper.FormatLanguageColumnHeader", () => {
    it("returns name with code when locale is provided", () => {
        var locale = { language: "English" };
        expect(Helper.FormatLanguageColumnHeader(1033, locale)).toBe("English (1033)");
    });

    it("returns code as name when locale has no language", () => {
        var locale = {};
        expect(Helper.FormatLanguageColumnHeader(1033, locale)).toBe("1033 (1033)");
    });

    it("returns code only when no locale provided and languageLocales is empty", () => {
        expect(Helper.FormatLanguageColumnHeader(1033)).toBe("1033 (1033)");
    });

    it("handles empty string languageCode", () => {
        expect(Helper.FormatLanguageColumnHeader("")).toBe(" ()");
    });
});

// ============================================================
// ShowError
// ============================================================
describe("Helper.ShowError", () => {
    it("delegates to DialogHelper.alert when available", () => {
        var result = Helper.ShowError("test error");
        expect(mockDialogHelper.alert).toHaveBeenCalledWith("test error", { title: "Error" });
    });

    it("extracts message property from error object", () => {
        Helper.ShowError({ message: "inner error" });
        expect(mockDialogHelper.alert).toHaveBeenCalledWith("inner error", { title: "Error" });
    });

    it("uses custom title from options", () => {
        Helper.ShowError("test", { title: "Custom" });
        expect(mockDialogHelper.alert).toHaveBeenCalledWith("test", { title: "Custom" });
    });

    it("falls back to w2alert when DialogHelper is not available", () => {
        globalThis.window.DialogHelper = undefined;
        Helper.ShowError("fallback test");
        expect(mockW2Alert).toHaveBeenCalledWith("fallback test", "Error");
        globalThis.window.DialogHelper = mockDialogHelper;
    });

    it("falls back to window.alert when neither DialogHelper nor w2alert", () => {
        globalThis.window.DialogHelper = undefined;
        globalThis.window.w2alert = undefined;
        Helper.ShowError("final fallback");
        expect(globalThis.window.alert).toHaveBeenCalledWith("final fallback");
        globalThis.window.DialogHelper = mockDialogHelper;
        globalThis.window.w2alert = mockW2Alert;
    });
});

// ============================================================
// GetTranslator
// ============================================================
describe("Helper.GetTranslator", () => {
    it("returns window.DataverseLabelTranslator when available", () => {
        expect(Helper.GetTranslator()).toBe(mockTranslator);
    });

    it("throws when DataverseLabelTranslator is not available", () => {
        globalThis.window.DataverseLabelTranslator = undefined;
        expect(() => Helper.GetTranslator()).toThrow("Translator is not available.");
        globalThis.window.DataverseLabelTranslator = mockTranslator;
    });
});

// ============================================================
// GetLanguageColumnText
// ============================================================
describe("Helper.GetLanguageColumnText", () => {
    it("returns column text matching language code", () => {
        var grid = {
            columns: [
                { field: "1033", text: "English (1033)" },
                { field: "1041", text: "Japanese (1041)" }
            ]
        };
        mockTranslator.GetGrid.mockReturnValue(grid);
        expect(Helper.GetLanguageColumnText("1033", grid)).toBe("English (1033)");
    });

    it("returns field as fallback when no column matches", () => {
        var grid = { columns: [] };
        expect(Helper.GetLanguageColumnText("9999", grid)).toBe("9999");
    });

    it("falls back to caption or label when text is missing", () => {
        var grid = {
            columns: [{ field: "1033", caption: "Cap" }]
        };
        expect(Helper.GetLanguageColumnText("1033", grid)).toBe("Cap");
    });

    it("falls back to label when both text and caption are missing", () => {
        var grid = {
            columns: [{ field: "1033", label: "Lbl" }]
        };
        expect(Helper.GetLanguageColumnText("1033", grid)).toBe("Lbl");
    });
});

// ============================================================
// ExecuteCustomAction
// ============================================================
describe("Helper.ExecuteCustomAction", () => {
    it("sends request with correct metadata and returns parsed output", async () => {
        var actionOutput = { ok: true, output: JSON.stringify({ type: "Loading", data: "test" }) };
        mockXrm.WebApi.online.execute.mockResolvedValue({
            ok: true,
            json: () => Promise.resolve(actionOutput)
        });

        var result = await Helper.ExecuteCustomAction("TestFunc", { key: "val" });
        expect(result.data).toBe("test");

        var callArgs = mockXrm.WebApi.online.execute.mock.calls[0][0];
        expect(callArgs.f).toBe("TestFunc");
        expect(JSON.parse(callArgs.input)).toEqual({ key: "val" });
        var metadata = callArgs.getMetadata();
        expect(metadata.operationName).toBe("pl_DataverseLabelTranslatorCustomAction");
    });

    it("throws when response is not ok", async () => {
        mockXrm.WebApi.online.execute.mockResolvedValue({
            ok: false,
            statusText: "Server Error"
        });

        await expect(Helper.ExecuteCustomAction("Fail", null)).rejects.toThrow("Custom action failed: Fail");
    });

    it("throws when output is empty", async () => {
        mockXrm.WebApi.online.execute.mockResolvedValue({
            ok: true,
            json: () => Promise.resolve({})
        });

        await expect(Helper.ExecuteCustomAction("Empty", null)).rejects.toThrow("Custom action returned empty output");
    });

    it("throws when output.ok is false", async () => {
        mockXrm.WebApi.online.execute.mockResolvedValue({
            ok: true,
            json: () => Promise.resolve({ output: JSON.stringify({ ok: false, message: "server error" }) })
        });

        await expect(Helper.ExecuteCustomAction("Bad", null)).rejects.toThrow("server error");
    });
});

// ============================================================
// ExecuteTypedCustomAction
// ============================================================
describe("Helper.ExecuteTypedCustomAction", () => {
    it("includes type in payload and validates response type", async () => {
        mockXrm.WebApi.online.execute.mockResolvedValue({
            ok: true,
            json: () => Promise.resolve({ output: JSON.stringify({ type: "Loading", object: {} }) })
        });

        var result = await Helper.ExecuteTypedCustomAction("Func", Helper.CustomActionTypes.Loading, {});
        expect(result.type).toBe("Loading");
    });

    it("throws when returned type mismatches", async () => {
        mockXrm.WebApi.online.execute.mockResolvedValue({
            ok: true,
            json: () => Promise.resolve({ output: JSON.stringify({ ok: true, type: "Saving", object: {} }) })
        });

        await expect(
            Helper.ExecuteTypedCustomAction("Func", Helper.CustomActionTypes.Loading, {})
        ).rejects.toThrow("returned unexpected type");
    });

    it("throws when ExecuteCustomAction returns null output", async () => {
        mockXrm.WebApi.online.execute.mockResolvedValue({
            ok: true,
            json: () => Promise.resolve({ output: JSON.stringify(null) })
        });

        await expect(
            Helper.ExecuteTypedCustomAction("Func", Helper.CustomActionTypes.Loading, {})
        ).rejects.toThrow();
    });
});

// ============================================================
// RunServerLoad
// ============================================================
describe("Helper.RunServerLoad", () => {
    it("calls onLoaded with output on success", async () => {
        mockXrm.WebApi.online.execute.mockResolvedValue({
            ok: true,
            json: () =>
                Promise.resolve({
                    output: JSON.stringify({ type: "Loading", object: { rows: [] } })
                })
        });

        var onLoaded = vi.fn();
        await Helper.RunServerLoad({
            actionName: "Test",
            onLoaded: onLoaded
        });
        expect(onLoaded).toHaveBeenCalledWith({ rows: [] }, expect.any(Object));
    });

    it("calls errorHandler on failure when handleError is not false", async () => {
        mockXrm.WebApi.online.execute.mockRejectedValue(new Error("fail"));

        var errorHandler = vi.fn();
        mockTranslator.errorHandler = errorHandler;

        var result = await Helper.RunServerLoad({
            actionName: "Test",
            handleError: true
        });
        expect(result).toBeNull();
        expect(errorHandler).toHaveBeenCalled();
        delete mockTranslator.errorHandler;
    });

    it("throws when handleError is false", async () => {
        mockXrm.WebApi.online.execute.mockRejectedValue(new Error("fail"));

        await expect(
            Helper.RunServerLoad({ actionName: "Test", handleError: false })
        ).rejects.toThrow("fail");
    });

    it("calls UnlockGrid on error", async () => {
        mockXrm.WebApi.online.execute.mockRejectedValue(new Error("fail"));

        try {
            await Helper.RunServerLoad({ actionName: "Test", handleError: false });
        } catch (e) {
            // expected
        }
        expect(mockTranslator.UnlockGrid).toHaveBeenCalled();
    });
});

// ============================================================
// GetSimpleGridSearchValue
// ============================================================
describe("Helper.GetSimpleGridSearchValue", () => {
    it("returns search input value when found", () => {
        var mockInput = { value: "search term" };
        var grid = {
            name: "myGrid",
            box: {
                querySelector: vi.fn().mockReturnValue(mockInput)
            }
        };
        expect(Helper.GetSimpleGridSearchValue(grid)).toBe("search term");
    });

    it("returns empty string when no search input found", () => {
        var grid = {
            name: "myGrid",
            box: {
                querySelector: vi.fn().mockReturnValue(null)
            }
        };
        expect(Helper.GetSimpleGridSearchValue(grid)).toBe("");
    });

    it("returns empty string when grid has no box", () => {
        var grid = { name: "myGrid" };
        expect(Helper.GetSimpleGridSearchValue(grid)).toBe("");
    });

    it("returns empty string when grid is null", () => {
        expect(Helper.GetSimpleGridSearchValue(null)).toBe("");
    });
});

// ============================================================
// ClearSimpleGridSearchPlaceholder
// ============================================================
describe("Helper.ClearSimpleGridSearchPlaceholder", () => {
    it("clears placeholder and resets search fields", () => {
        var mockInput = { value: "undefined", placeholder: "search", setAttribute: vi.fn(), removeAttribute: vi.fn() };
        var mockNameEl = { style: { display: "" }, querySelector: vi.fn().mockReturnValue(null) };
        var grid = {
            name: "myGrid",
            box: {
                querySelector: vi.fn((sel) => {
                    if (sel.includes("_search_all")) return mockInput;
                    if (sel.includes("_search_name")) return mockNameEl;
                    return null;
                })
            },
            searchSelected: "old",
            last: { field: "old", label: "old" }
        };

        Helper.ClearSimpleGridSearchPlaceholder(grid);

        expect(mockInput.value).toBe("");
        expect(mockInput.removeAttribute).toHaveBeenCalledWith("placeholder");
        expect(grid.searchSelected).toBeNull();
        expect(grid.last.field).toBe("all");
    });

    it("handles null grid gracefully", () => {
        expect(() => Helper.ClearSimpleGridSearchPlaceholder(null)).not.toThrow();
    });
});

// ============================================================
// ApplySimpleGridContainsSearch
// ============================================================
describe("Helper.ApplySimpleGridContainsSearch", () => {
    it("returns early when grid is null", () => {
        expect(Helper.ApplySimpleGridContainsSearch(null)).toBeUndefined();
    });

    it("resets search when no search value", () => {
        var searchInput = {
            value: "",
            placeholder: "",
            setAttribute: vi.fn(),
            removeAttribute: vi.fn()
        };
        var grid = {
            name: "myGrid",
            searches: [{ field: "col1", type: "text" }],
            searchReset: vi.fn(),
            refresh: vi.fn(),
            box: {
                querySelector: vi.fn((sel) => {
                    if (sel.includes("_search_all")) return searchInput;
                    return null;
                })
            }
        };

        Helper.ApplySimpleGridContainsSearch(grid);
        expect(grid.searchReset).toHaveBeenCalledWith(true);
    });

    it("builds searchData with contains operator when search value exists", () => {
        var searchInput = {
            value: "test",
            placeholder: "",
            setAttribute: vi.fn(),
            removeAttribute: vi.fn()
        };
        var grid = {
            name: "myGrid",
            searches: [
                { field: "col1", type: "text" },
                { field: "col2", type: "text" }
            ],
            refresh: vi.fn(),
            localSearch: vi.fn(),
            box: {
                querySelector: vi.fn((sel) => {
                    if (sel.includes("_search_all")) return searchInput;
                    return null;
                })
            }
        };

        Helper.ApplySimpleGridContainsSearch(grid);

        expect(grid.searchData.length).toBe(2);
        expect(grid.searchData[0].operator).toBe("contains");
        expect(grid.searchData[0].value).toBe("test");
        expect(grid.last.logic).toBe("OR");
        expect(grid.localSearch).toHaveBeenCalledWith(true);
    });

    it("skips searches without field", () => {
        var searchInput = {
            value: "test",
            placeholder: "Search...",
            setAttribute: vi.fn(),
            removeAttribute: vi.fn()
        };
        var grid = {
            name: "myGrid",
            searches: [
                { field: "col1", type: "text" },
                { type: "text" }
            ],
            refresh: vi.fn(),
            localSearch: vi.fn(),
            box: {
                querySelector: vi.fn((sel) => {
                    if (sel.includes("_search_all")) return searchInput;
                    return null;
                })
            }
        };

        Helper.ApplySimpleGridContainsSearch(grid);
        expect(grid.searchData.length).toBe(1);
    });

    it("returns early when no searches have fields", () => {
        var searchInput = {
            value: "test",
            placeholder: "",
            setAttribute: vi.fn(),
            removeAttribute: vi.fn()
        };
        var grid = {
            name: "myGrid",
            searches: [{ type: "text" }],
            refresh: vi.fn(),
            box: {
                querySelector: vi.fn((sel) => {
                    if (sel.includes("_search_all")) return searchInput;
                    return null;
                })
            }
        };

        var result = Helper.ApplySimpleGridContainsSearch(grid);
        expect(result).toBeUndefined();
    });
});

// ============================================================
// FinalizeGrid
// ============================================================
describe("Helper.FinalizeGrid", () => {
    it("adds records and calls app.UnlockGrid", () => {
        var mockGrid = { add: vi.fn(), unlock: vi.fn() };
        mockTranslator.GetGrid.mockReturnValue(mockGrid);

        var records = [{ recid: 1, name: "test" }];
        Helper.FinalizeGrid(records, mockTranslator);

        expect(mockGrid.add).toHaveBeenCalledWith(records);
        expect(mockTranslator.UnlockGrid).toHaveBeenCalled();
    });

    it("calls grid.unlock when app.UnlockGrid is not a function", () => {
        var mockGrid = { add: vi.fn(), unlock: vi.fn() };
        var appWithoutUnlock = {
            GetGrid: vi.fn().mockReturnValue(mockGrid)
        };
        mockTranslator.GetGrid.mockReturnValue(mockGrid);

        Helper.FinalizeGrid([], appWithoutUnlock);
        expect(mockGrid.unlock).toHaveBeenCalled();
    });
});

// ============================================================
// RunServerSaveFlow
// ============================================================
describe("Helper.RunServerSaveFlow", () => {
    it("executes save then publishes then published sequentially", async () => {
        var callCount = 0;
        mockXrm.WebApi.online.execute.mockImplementation(() => {
            callCount++;
            var type = callCount === 1 ? "Saving" : callCount === 2 ? "Publishing" : "Published";
            return Promise.resolve({
                ok: true,
                json: () =>
                    Promise.resolve({
                        output: JSON.stringify({ type: type, object: { step: callCount } })
                    })
            });
        });

        var mockGrid = { add: vi.fn(), unlock: vi.fn() };
        mockTranslator.GetGrid.mockReturnValue(mockGrid);

        var result = await Helper.RunServerSaveFlow({
            actionName: "Test",
            getSavePayload: () => ({ data: "save" }),
            getPublishPayload: () => ({ data: "publish" }),
            getPublishedPayload: () => ({ data: "published" })
        });

        expect(callCount).toBe(3);
    });

    it("unlocks grid after save when no publish payload", async () => {
        mockXrm.WebApi.online.execute.mockResolvedValue({
            ok: true,
            json: () =>
                Promise.resolve({
                    output: JSON.stringify({ type: "Saving", object: {} })
                })
        });

        var mockGrid = { add: vi.fn(), unlock: vi.fn() };
        mockTranslator.GetGrid.mockReturnValue(mockGrid);

        await Helper.RunServerSaveFlow({
            actionName: "Test",
            getSavePayload: () => ({}),
            getPublishPayload: () => null
        });

        expect(mockTranslator.UnlockGrid).toHaveBeenCalled();
    });

    it("calls afterSave and skips post flow when it returns true", async () => {
        mockXrm.WebApi.online.execute.mockResolvedValue({
            ok: true,
            json: () =>
                Promise.resolve({
                    output: JSON.stringify({ type: "Saving", object: {} })
                })
        });

        var afterSave = vi.fn().mockReturnValue(true);
        var result = await Helper.RunServerSaveFlow({
            actionName: "Test",
            getSavePayload: () => ({}),
            afterSave: afterSave
        });

        expect(afterSave).toHaveBeenCalled();
    });

    it("unlocks grid on error", async () => {
        mockXrm.WebApi.online.execute.mockRejectedValue(new Error("fail"));

        var mockGrid = { add: vi.fn(), unlock: vi.fn() };
        mockTranslator.GetGrid.mockReturnValue(mockGrid);

        await expect(
            Helper.RunServerSaveFlow({
                actionName: "Test",
                getSavePayload: () => ({})
            })
        ).rejects.toThrow("fail");

        expect(mockTranslator.UnlockGrid).toHaveBeenCalled();
    });

    it("reloads when shouldReload returns true", async () => {
        var callCount = 0;
        mockXrm.WebApi.online.execute.mockImplementation(() => {
            callCount++;
            return Promise.resolve({
                ok: true,
                json: () =>
                    Promise.resolve({
                        output: JSON.stringify({ type: "Saving", object: {} })
                    })
            });
        });

        var mockGrid = { add: vi.fn(), unlock: vi.fn() };
        mockTranslator.GetGrid.mockReturnValue(mockGrid);

        var reloadAction = vi.fn().mockResolvedValue("reloaded");

        var result = await Helper.RunServerSaveFlow({
            actionName: "Test",
            getSavePayload: () => ({}),
            getPublishPayload: () => null,
            shouldReload: () => true,
            reloadAction: reloadAction
        });

        expect(reloadAction).toHaveBeenCalled();
    });
});

// ============================================================
// GetLanguageColumnCode
// ============================================================
describe("Helper.GetLanguageColumnCode", () => {
    it("returns empty string for unknown code when languageLocales is empty", () => {
        expect(Helper.GetLanguageColumnCode("9999")).toBe("");
    });

    it("returns empty string for empty code", () => {
        expect(Helper.GetLanguageColumnCode("")).toBe("");
    });
});

// ============================================================
// GetXrm (indirect via ExecuteCustomAction)
// ============================================================
describe("GetXrm resolution", () => {
    it("uses window.Xrm when global Xrm is available", async () => {
        mockXrm.WebApi.online.execute.mockResolvedValue({
            ok: true,
            json: () =>
                Promise.resolve({ output: JSON.stringify({ ok: true, type: "Other", object: {} }) })
        });

        await Helper.ExecuteTypedCustomAction("Func", Helper.CustomActionTypes.Other, {});
    });

    it("throws when Xrm is not available", () => {
        var savedXrm = globalThis.Xrm;
        var savedWindowXrm = globalThis.window.Xrm;
        var savedParentXrm = globalThis.window.parent.Xrm;

        globalThis.Xrm = undefined;
        globalThis.window.Xrm = undefined;
        globalThis.window.parent = {};

        expect(() => Helper.ExecuteCustomAction("Func", null)).toThrow("Xrm is not available");

        globalThis.Xrm = savedXrm;
        globalThis.window.Xrm = savedWindowXrm;
        globalThis.window.parent = { Xrm: savedParentXrm };
    });
});
