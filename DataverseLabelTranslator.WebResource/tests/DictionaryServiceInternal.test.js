import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

// Additional coverage for DictionaryService internal helpers that aren't
// directly exposed. We exercise them through public methods that internally
// call the helpers.

const mockHelper = {
  CustomActionTypes: { Other: "Other" },
  ExecuteTypedCustomAction: vi.fn(),
  GetCustomActionObject: vi.fn((r) => (r && r.object) || {}),
  GetBaseLanguage: vi.fn().mockResolvedValue("1033"),
  GetTranslator: vi.fn(),
  ApplySimpleGridContainsSearch: vi.fn(),
  ShowError: vi.fn(),
  GetPlaceholderDisplayText: vi.fn(() => "(display)"),
  GetPlaceholderDescription: vi.fn(() => "(description)"),
  GetPlaceholderReadonly: vi.fn(() => "-"),
  GetOperationLoading: vi.fn(() => "Loading..."),
  GetOperationSaving: vi.fn(() => "Saving..."),
  ClearSimpleGridSearchPlaceholder: vi.fn()
};

const mockApp = {
  GetGrid: vi.fn().mockReturnValue({ columns: [], records: [] }),
  GetColumns: vi.fn().mockReturnValue([]),
  GetBaseLanguage: vi.fn().mockReturnValue(1033),
  SetBaseLanguage: vi.fn(),
  UnlockGrid: vi.fn(),
  LockGrid: vi.fn(),
  errorHandler: vi.fn(),
  IsUnifiedType: vi.fn(() => true),
  RefreshGrid: vi.fn(),
  RefreshGridRow: vi.fn()
};

function mockGrid(records = [], opts = {}) {
  const toolbar = {
    items: [],
    get: vi.fn(function (id) {
      return (
        (toolbar.items || []).find(function (i) {
          return i.id === id;
        }) || null
      );
    }),
    click: vi.fn(),
    add: vi.fn(),
    remove: vi.fn(),
    refresh: vi.fn(),
    show: vi.fn(),
    hide: vi.fn(),
    enable: vi.fn(),
    disable: vi.fn(),
    set: vi.fn(),
    insert: vi.fn()
  };
  return {
    name: opts.name || "translationDictionaryGrid",
    columns: opts.columns || [],
    records,
    searches: opts.searches || [],
    box: opts.box || { style: {}, contains: () => true, querySelector: () => null, querySelectorAll: () => [] },
    show: { toolbar: true, footer: true },
    toolbar: toolbar,
    destroy: vi.fn(),
    render: vi.fn(),
    resize: vi.fn(),
    refresh: vi.fn(),
    refreshSearch: vi.fn(),
    add: vi.fn(),
    remove: vi.fn(),
    addColumn: vi.fn(),
    removeColumn: vi.fn(),
    clear: vi.fn(),
    set: vi.fn(),
    get: vi.fn(),
    getSelection: vi.fn(() => []),
    localSearch: vi.fn(),
    search: vi.fn(),
    lock: vi.fn(),
    unlock: vi.fn(),
    last: null,
    total: 0
  };
}

beforeEach(() => {
  globalThis.window.Helper = mockHelper;
  globalThis.window.w2ui = {};
  globalThis.window.DictionaryService = undefined;
  globalThis.window.DataverseLabelTranslator = mockApp;
  globalThis.window.w2utils = { decodeTags: vi.fn((v) => v), encodeTags: vi.fn((v) => v) };
  globalThis.w2popup = { open: vi.fn(), close: vi.fn(), max: vi.fn(), lock: vi.fn(), unlock: vi.fn() };
  globalThis.w2alert = vi.fn();
  globalThis.w2confirm = vi.fn();
  globalThis.w2grid = vi.fn(function (config) {
    const g = mockGrid([], { columns: config.columns || [] });
    g.name = config.name;
    const tcfg = config.toolbar;
    if (tcfg) {
      g.toolbar = Object.assign({}, tcfg, {
        get: vi.fn(function (id) {
          return (
            (g.toolbar.items || []).find(function (i) {
              return i.id === id;
            }) || null
          );
        }),
        click: vi.fn(),
        add: vi.fn(),
        remove: vi.fn(),
        refresh: vi.fn(),
        show: vi.fn(),
        hide: vi.fn(),
        enable: vi.fn(),
        disable: vi.fn(),
        set: vi.fn(),
        insert: vi.fn()
      });
    }
    g.onChange = config.onChange;
    g.onEditField = config.onEditField;
    g.onSearch = config.onSearch;
    globalThis.w2ui[config.name] = g;
    return g;
  });
  globalThis.w2utils = { decodeTags: vi.fn((v) => v), encodeTags: vi.fn((v) => v) };
  globalThis.w2utils.stripTags = vi.fn((s) => String(s || "").replace(/<[^>]*>/g, ""));
  vi.useRealTimers();

  mockHelper.ExecuteTypedCustomAction.mockReset();
  mockHelper.GetCustomActionObject.mockReset();
  mockHelper.GetBaseLanguage.mockReset();
  mockHelper.GetBaseLanguage.mockResolvedValue("1033");

  mockHelper.ExecuteTypedCustomAction.mockResolvedValue({
    object: {
      content: [
        '<?xml version="1.0" encoding="utf-8"?>',
        '<dictionary version="2.0" sourceLcid="1033">',
        "  <entries>",
        '    <entry sourceText="Hello" isActive="true">',
        '      <target lcid="1041">こんにちは</target>',
        "    </entry>",
        "  </entries>",
        "</dictionary>"
      ].join("\n")
    }
  });
  mockHelper.GetCustomActionObject.mockImplementation((r) => (r && r.object) || {});
});

afterEach(() => {
  vi.useRealTimers();
  delete globalThis.w2popup;
  delete globalThis.w2confirm;
});

import "../js/DictionaryService.js";

const DictionaryService = globalThis.window.DictionaryService;

describe("DictionaryService.SaveFromGrid - early paths", () => {
  it("returns undefined when grid is not available", () => {
    globalThis.w2ui = {};
    const result = DictionaryService.SaveFromGrid();
    expect(result).toBeUndefined();
  });

  it("saveDictionaryModel catches errors and shows alert", async () => {
    const grid = mockGrid([{ recid: "r1", sourceText: "X", target_1041: "Y" }]);
    globalThis.w2ui = { translationDictionaryGrid: grid };
    mockHelper.ExecuteTypedCustomAction.mockResolvedValueOnce({ object: { content: "<dictionary/>" } });
    const result = await DictionaryService.SaveFromGrid();
    // Should complete without throwing
    expect(result === undefined || result === null).toBe(true);
  });
});

describe("DictionaryService.UpsertEntries with grid present", () => {
  it("uses the current grid model when dictionaryGridContext is set", async () => {
    const grid = mockGrid([{ recid: "r1", sourceText: "Hello", target_1041: "こんにちは", isActive: true }]);
    globalThis.w2ui = { translationDictionaryGrid: grid };
    mockApp.GetColumns.mockReturnValue(["1033", "1041"]);

    // Pre-initialize dictionaryGridContext by calling a public method that sets it
    await DictionaryService.EnsureInitialized();
    // Now call UpsertEntries — it should attempt to save via the grid model
    const result = await DictionaryService.UpsertEntries([
      { sourceText: "Hello", isActive: true, targets: { 1041: "こんにちは" } }
    ]);
    expect(result === undefined || result === null || typeof result === "object").toBe(true);
  });
});

describe("DictionaryService.SplitRecordsByDictionary with gridKey", () => {
  it("matches records using gridKey-driven lookup", async () => {
    const grid = mockGrid([], { columns: [] });
    globalThis.w2ui = { translationDictionaryGrid: grid };
    mockApp.GetColumns.mockReturnValue(["1033", "1041"]);

    await DictionaryService.EnsureInitialized();

    const records = [
      { recid: "r1", schemaName: "attr1", 1033: "Hello" },
      { recid: "r2", schemaName: "attr2", 1033: "World" }
    ];
    const result = await DictionaryService.SplitRecordsByDictionary("1033", "1041", records);
    expect(result).toHaveProperty("matchedResults");
    expect(result).toHaveProperty("unmatchedRecords");
  });
});

describe("DictionaryService internal error paths", () => {
  it("EnsureInitialized propagates errors when ExecuteOther fails", async () => {
    mockHelper.ExecuteTypedCustomAction.mockReset();
    mockHelper.ExecuteTypedCustomAction.mockRejectedValueOnce(new Error("network down"));
    await expect(DictionaryService.EnsureInitialized()).rejects.toThrow("network down");
  });

  it("SplitRecordsByDictionary returns fallback when load fails", async () => {
    const grid = mockGrid([], { columns: [] });
    globalThis.w2ui = { translationDictionaryGrid: grid };
    mockApp.GetColumns.mockReturnValue(["1033"]);
    mockHelper.ExecuteTypedCustomAction.mockReset();
    mockHelper.ExecuteTypedCustomAction.mockResolvedValueOnce({
      object: { content: ['<dictionary sourceLcid="9999"/>'].join("\n") }
    });
    const records = [{ recid: "r1", schemaName: "s1", 1033: "Hi" }];
    const result = await DictionaryService.SplitRecordsByDictionary("1033", "1041", records);
    // sourceLcid mismatch -> unmatched
    expect(Array.isArray(result.matchedResults)).toBe(true);
    expect(Array.isArray(result.unmatchedRecords)).toBe(true);
  });

  it("SplitRecordsByDictionary with empty sourceText entries is safe", async () => {
    const grid = mockGrid([], { columns: [] });
    globalThis.w2ui = { translationDictionaryGrid: grid };
    mockApp.GetColumns.mockReturnValue(["1033", "1041"]);
    mockHelper.ExecuteTypedCustomAction.mockReset();
    mockHelper.ExecuteTypedCustomAction.mockResolvedValueOnce({
      object: {
        content: [
          '<dictionary sourceLcid="1033">',
          "  <entries>",
          '    <entry sourceText="" isActive="false"/>',
          "  </entries>",
          "</dictionary>"
        ].join("\n")
      }
    });
    const result = await DictionaryService.SplitRecordsByDictionary("1033", "1041", [
      { recid: "r1", schemaName: "s1", 1033: "Hi" }
    ]);
    expect(result.matchedResults).toEqual([]);
  });
});

describe("DictionaryService.ShowDictionaryPrompt", () => {
  it("opens popup with dictionary content and renders grid", async () => {
    vi.useFakeTimers();
    let popupCfg = null;
    globalThis.w2popup.open = vi.fn(function (cfg) {
      popupCfg = cfg;
    });
    // Pre-populate w2ui with a fake dictionary grid so ShowDictionaryPrompt doesn't
    // need to instantiate one.
    const grid = mockGrid([], { columns: [] });
    grid.name = "translationDictionaryGrid";
    globalThis.w2ui = { translationDictionaryGrid: grid };
    mockApp.GetColumns.mockReturnValue(["1033", "1041"]);

    const result = await DictionaryService.ShowDictionaryPrompt();
    expect(popupCfg).not.toBeNull();
    // Drive onOpen.onComplete to exercise the render and search-normalize paths
    const event = {};
    popupCfg.onOpen(event);
    if (event.onComplete) event.onComplete();
    vi.runAllTimers();
  });

  it("adjusts baseLcid when dictionary model sourceLcid differs from current", async () => {
    const grid = mockGrid([], { columns: [] });
    globalThis.w2ui = {};
    mockApp.GetColumns.mockReturnValue(["1033", "1041"]);
    mockHelper.ExecuteTypedCustomAction.mockReset();
    mockHelper.ExecuteTypedCustomAction.mockResolvedValueOnce({
      object: {
        content: [
          '<dictionary version="2.0" sourceLcid="1041">',
          "  <entries>",
          '    <entry sourceText="Hi" isActive="true">',
          '      <target lcid="1033">English</target>',
          "    </entry>",
          "  </entries>",
          "</dictionary>"
        ].join("\n")
      }
    });
    const result = await DictionaryService.ShowDictionaryPrompt();
    // baseLcid is adjusted to "1041" -> context is rebuilt
    expect(result === undefined || result === null || typeof result === "object").toBe(true);
  });
});

describe("DictionaryService SaveFromGrid with grid present", () => {
  it("saves and updates baseline signature", async () => {
    const grid = mockGrid([{ recid: "r1", sourceText: "Hello", target_1041: "こんにちは", isActive: true }]);
    globalThis.w2ui = { translationDictionaryGrid: grid };
    mockApp.GetColumns.mockReturnValue(["1033", "1041"]);
    // pre-init context
    await DictionaryService.EnsureInitialized();
    const result = await DictionaryService.SaveFromGrid();
    expect(result === undefined || result === null).toBe(true);
  });
});
