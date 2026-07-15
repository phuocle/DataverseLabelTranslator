import { describe, it, expect, vi, beforeEach } from "vitest";

// Use a fresh global state for every test by setting up mocks before loading the file.
const createMockGrid = () => ({
  name: "grid",
  columns: [],
  records: [],
  searches: [],
  total: 0,
  last: null,
  searchData: [],
  box: null,
  lock: vi.fn(),
  unlock: vi.fn(),
  refresh: vi.fn(),
  refreshRow: vi.fn(),
  removeColumn: vi.fn(),
  addColumn: vi.fn(),
  addSearch: vi.fn(),
  removeSearch: vi.fn(),
  getSelection: vi.fn(() => []),
  sort: vi.fn(),
  clear: vi.fn(),
  show: { selectColumn: true }
});

const createMockToolbar = () => ({
  get: vi.fn((id) => {
    const values = {
      solutionSelect: { selected: "sol-1" },
      entitySelect: { selected: "account" },
      type: { selected: "attributes" },
      component: { selected: "DisplayText" },
      "w2ui-save": {}
    };
    return values[id] || null;
  }),
  show: vi.fn(),
  hide: vi.fn(),
  enable: vi.fn(),
  disable: vi.fn(),
  refresh: vi.fn(),
  set: vi.fn()
});

const createMockHelper = () => ({
  GetPlaceholderReadonly: vi.fn(() => "-"),
  GetPlaceholderDisplayText: vi.fn(() => "Add-display-text"),
  GetPlaceholderDescription: vi.fn(() => "Add-description"),
  GetOperationLoading: vi.fn(() => "Loading"),
  GetOperationSaving: vi.fn(() => "Saving"),
  GetOperationPublishing: vi.fn(() => "Publishing"),
  GetOperationPublished: vi.fn(() => "Published"),
  GetOperationReLoading: vi.fn(() => "Re-Loading"),
  ApplyPlaceholder: vi.fn(),
  FinalizeGrid: vi.fn(),
  RunServerLoad: vi.fn(() => Promise.resolve({})),
  RunServerSaveFlow: vi.fn(() => Promise.resolve({})),
  FormatLanguageColumnHeader: vi.fn((f) => "L-" + f),
  GetLanguageColumnCode: vi.fn((f) => "code-" + f),
  GetTypeStateLabel: vi.fn((t) => "Lbl-" + t),
  GetTranslator: vi.fn(() => ({
    GetType: vi.fn(() => "attributes"),
    GetSolution: vi.fn(() => "sol-1"),
    GetEntity: vi.fn(() => "account"),
    GetEntityId: vi.fn(() => "guid-123"),
    GetComponent: vi.fn(() => "DisplayText"),
    GetBaseLanguage: vi.fn(() => "1033"),
    GetAllRecords: vi.fn(() => []),
    GetCurrentToolbarTypeText: vi.fn(() => "Attributes"),
    GetTypeStateLabel: vi.fn((t) => t),
    LockGrid: vi.fn(),
    UnlockGrid: vi.fn(),
    SetMetadata: vi.fn(),
    SetBaseLanguage: vi.fn(),
    StartOperationStatus: vi.fn(),
    ApplyStoredOperationStatus: vi.fn()
  }))
});

const createMockDialogHelper = () => ({
  alert: vi.fn(() => Promise.resolve()),
  confirm: vi.fn(() => Promise.resolve(true))
});

let mockGrid;
let mockToolbar;
let mockHelper;
let mockDialogHelper;

beforeEach(() => {
  mockGrid = createMockGrid();
  mockToolbar = createMockToolbar();
  mockHelper = createMockHelper();
  mockDialogHelper = createMockDialogHelper();

  globalThis.window.w2ui = { grid: mockGrid, grid_toolbar: mockToolbar };
  globalThis.window.Helper = mockHelper;
  globalThis.window.w2alert = vi.fn();
  globalThis.window.w2confirm = vi.fn();
  globalThis.window.w2popup = { open: vi.fn(), close: vi.fn() };
  globalThis.window.AIService = { OpenWorkspace: vi.fn() };
  globalThis.window.AppService = { ShowAppSettings: vi.fn() };
  globalThis.window.DictionaryService = { ShowDictionaryPrompt: vi.fn() };
  globalThis.window.DialogHelper = mockDialogHelper;
});

// Load the IIFE module
await import("../js/DataverseLabelTranslator.js");

const Translator = globalThis.window.DataverseLabelTranslator;

// ============================================================
// Public API - GetGrid
// ============================================================
describe("DataverseLabelTranslator.GetGrid", () => {
  it("returns the w2ui grid instance", () => {
    const grid = Translator.GetGrid();
    expect(grid).toBe(mockGrid);
  });
});

// ============================================================
// Public API - GetType
// ============================================================
describe("DataverseLabelTranslator.GetType", () => {
  it("returns the selected type from toolbar", () => {
    const type = Translator.GetType();
    expect(type).toBe("attributes");
  });

  it("defaults to sitemap if toolbar returns null", () => {
    mockToolbar.get.mockReturnValue(null);
    expect(Translator.GetType()).toBe("sitemap");
  });
});

// ============================================================
// Public API - GetEntity
// ============================================================
describe("DataverseLabelTranslator.GetEntity", () => {
  it("returns the selected entity", () => {
    const entity = Translator.GetEntity();
    expect(entity).toBe("account");
  });

  it("returns none when no entity selected", () => {
    mockToolbar.get.mockReturnValue(null);
    expect(Translator.GetEntity()).toBe("none");
  });
});

// ============================================================
// Public API - GetSolution
// ============================================================
describe("DataverseLabelTranslator.GetSolution", () => {
  it("returns the selected solution id", () => {
    expect(Translator.GetSolution()).toBe("sol-1");
  });

  it("returns null when no solution selected", () => {
    mockToolbar.get.mockReturnValue(null);
    expect(Translator.GetSolution()).toBeNull();
  });
});

// ============================================================
// Public API - GetEntityId
// ============================================================
describe("DataverseLabelTranslator.GetEntityId", () => {
  it("returns null when entity not in metadata", () => {
    expect(Translator.GetEntityId()).toBeNull();
  });
});

// ============================================================
// Public API - GetComponent
// ============================================================
describe("DataverseLabelTranslator.GetComponent", () => {
  it("returns the selected component", () => {
    expect(Translator.GetComponent()).toBe("DisplayText");
  });

  it("defaults to DisplayText when nothing selected", () => {
    mockToolbar.get.mockReturnValue(null);
    expect(Translator.GetComponent()).toBe("DisplayText");
  });
});

// ============================================================
// Public API - IsDescriptionComponent / IsDisplayTextComponent
// ============================================================
describe("DataverseLabelTranslator.IsDescriptionComponent", () => {
  it("returns false for DisplayText component", () => {
    expect(Translator.IsDescriptionComponent()).toBe(false);
  });

  it("returns true for Description component", () => {
    mockToolbar.get.mockImplementation((id) => {
      if (id === "component") return { selected: "Description" };
      return null;
    });
    expect(Translator.IsDescriptionComponent()).toBe(true);
  });
});

describe("DataverseLabelTranslator.IsDisplayTextComponent", () => {
  it("returns true for DisplayText component", () => {
    expect(Translator.IsDisplayTextComponent()).toBe(true);
  });

  it("returns false for Description component", () => {
    mockToolbar.get.mockImplementation((id) => {
      if (id === "component") return { selected: "Description" };
      return null;
    });
    expect(Translator.IsDisplayTextComponent()).toBe(false);
  });
});

// ============================================================
// Public API - GetCurrentComponentText
// ============================================================
describe("DataverseLabelTranslator.GetCurrentComponentText", () => {
  it("returns 'Display Text' for DisplayText component", () => {
    expect(Translator.GetCurrentComponentText()).toBe("Display Text");
  });

  it("returns 'Description' for Description component", () => {
    mockToolbar.get.mockImplementation((id) => {
      if (id === "component") return { selected: "Description" };
      return null;
    });
    expect(Translator.GetCurrentComponentText()).toBe("Description");
  });
});

// ============================================================
// Public API - GetCurrentToolbarTypeText
// ============================================================
describe("DataverseLabelTranslator.GetCurrentToolbarTypeText", () => {
  it("returns the type label", () => {
    const text = Translator.GetCurrentToolbarTypeText();
    expect(typeof text).toBe("string");
    expect(text.length).toBeGreaterThan(0);
  });
});

// ============================================================
// Public API - SetMetadata / GetMetadata
// ============================================================
describe("DataverseLabelTranslator.SetMetadata", () => {
  it("stores the metadata", () => {
    Translator.SetMetadata([{ MetadataId: "1" }, { MetadataId: "2" }]);
    expect(Translator.GetMetadata()).toHaveLength(2);
  });

  it("stores empty array when undefined", () => {
    Translator.SetMetadata(undefined);
    expect(Translator.GetMetadata()).toEqual([]);
  });
});

describe("DataverseLabelTranslator.GetMetadata", () => {
  it("returns empty array when not set", () => {
    expect(Translator.GetMetadata()).toEqual([]);
  });
});

// ============================================================
// Public API - GetBaseLanguage / SetBaseLanguage
// ============================================================
describe("DataverseLabelTranslator.GetBaseLanguage", () => {
  it("returns null initially", () => {
    expect(Translator.GetBaseLanguage()).toBeNull();
  });
});

describe("DataverseLabelTranslator.SetBaseLanguage", () => {
  it("sets the base language", () => {
    Translator.SetBaseLanguage("1033");
    expect(Translator.GetBaseLanguage()).toBe("1033");
  });
});

// ============================================================
// Public API - GetAllRecords
// ============================================================
describe("DataverseLabelTranslator.GetAllRecords", () => {
  it("returns empty array when no records", () => {
    expect(Translator.GetAllRecords()).toEqual([]);
  });

  it("returns the records from the grid", () => {
    mockGrid.records = [
      { recid: "1", w2ui: {} },
      { recid: "2", w2ui: {} }
    ];
    const records = Translator.GetAllRecords();
    expect(records).toHaveLength(2);
    expect(records[0].recid).toBe("1");
  });
});

// ============================================================
// Public API - GetColumns
// ============================================================
describe("DataverseLabelTranslator.GetColumns", () => {
  it("returns column field names", () => {
    mockGrid.columns = [{ field: "1033" }, { field: "1031" }, { field: "schemaName" }];
    expect(Translator.GetColumns(false)).toEqual(["1033", "1031"]);
  });

  it("includes schemaName when requested", () => {
    mockGrid.columns = [{ field: "1033" }, { field: "schemaName" }];
    expect(Translator.GetColumns(true)).toEqual(["1033", "schemaName"]);
  });
});

// ============================================================
// Public API - GetByRecId
// ============================================================
describe("DataverseLabelTranslator.GetByRecId", () => {
  it("returns the record matching the recid", () => {
    const records = [
      { recid: "1", w2ui: {} },
      { recid: "2", w2ui: {} }
    ];
    expect(Translator.GetByRecId(records, "2")).toBe(records[1]);
  });

  it("returns null when no match", () => {
    const records = [{ recid: "1", w2ui: {} }];
    expect(Translator.GetByRecId(records, "9")).toBeNull();
  });

  it("uses GetAllRecords when no records passed", () => {
    mockGrid.records = [{ recid: "x", w2ui: {} }];
    expect(Translator.GetByRecId(null, "x").recid).toBe("x");
  });
});

// ============================================================
// Public API - GetAttributeById
// ============================================================
describe("DataverseLabelTranslator.GetAttributeById", () => {
  it("returns the metadata with matching id", () => {
    Translator.SetMetadata([{ MetadataId: "id-1" }, { MetadataId: "id-2" }]);
    expect(Translator.GetAttributeById("id-2").MetadataId).toBe("id-2");
  });

  it("returns null when no match", () => {
    Translator.SetMetadata([{ MetadataId: "id-1" }]);
    expect(Translator.GetAttributeById("id-9")).toBeNull();
  });
});

// ============================================================
// Public API - ClearColumns
// ============================================================
describe("DataverseLabelTranslator.ClearColumns", () => {
  it("removes non-schemaName columns from grid", () => {
    mockGrid.columns = [{ field: "1033" }, { field: "1031" }, { field: "schemaName" }];
    mockGrid.searches = [{ field: "1033" }, { field: "schemaName" }];
    Translator.ClearColumns();
    expect(mockGrid.removeColumn).toHaveBeenCalledWith("1033");
    expect(mockGrid.removeColumn).toHaveBeenCalledWith("1031");
    expect(mockGrid.removeColumn).not.toHaveBeenCalledWith("schemaName");
  });
});

// ============================================================
// Public API - ApplyLanguageColumns
// ============================================================
describe("DataverseLabelTranslator.ApplyLanguageColumns", () => {
  it("adds the columns to the grid", () => {
    Translator.ApplyLanguageColumns([
      { field: "1033", text: "English" },
      { field: "1031", text: "German" }
    ]);
    expect(mockGrid.addColumn).toHaveBeenCalledTimes(2);
    expect(mockGrid.addSearch).toHaveBeenCalledTimes(2);
    expect(mockGrid.refresh).toHaveBeenCalled();
  });

  it("handles empty columns array", () => {
    Translator.ApplyLanguageColumns([]);
    expect(mockGrid.addColumn).not.toHaveBeenCalled();
    expect(mockGrid.refresh).toHaveBeenCalled();
  });

  it("skips duplicate fields", () => {
    Translator.ApplyLanguageColumns([{ field: "1033" }, { field: "1033" }]);
    expect(mockGrid.addColumn).toHaveBeenCalledTimes(1);
  });
});

// ============================================================
// Public API - HasPendingChanges
// ============================================================
describe("DataverseLabelTranslator.HasPendingChanges", () => {
  it("returns false when no changes", () => {
    mockGrid.records = [{ recid: "1", w2ui: {} }];
    expect(Translator.HasPendingChanges()).toBe(false);
  });

  it("returns true when there are changes", () => {
    mockGrid.records = [{ recid: "1", w2ui: { changes: { 1033: "Hello" } } }];
    expect(Translator.HasPendingChanges()).toBe(true);
  });
});

// ============================================================
// Public API - HasLoadedRecords
// ============================================================
describe("DataverseLabelTranslator.HasLoadedRecords", () => {
  it("returns false when no records", () => {
    expect(Translator.HasLoadedRecords()).toBe(false);
  });

  it("returns true when records exist", () => {
    mockGrid.records = [{ recid: "1", w2ui: {} }];
    expect(Translator.HasLoadedRecords()).toBe(true);
  });
});

// ============================================================
// Public API - GetSelectedRecordIds
// ============================================================
describe("DataverseLabelTranslator.GetSelectedRecordIds", () => {
  it("returns array of selected ids from selection", () => {
    mockGrid.getSelection.mockReturnValue([{ recid: "1" }, { recid: "2" }]);
    const ids = Translator.GetSelectedRecordIds();
    expect(Array.isArray(ids)).toBe(true);
    expect(ids.length).toBe(2);
  });
});

// ============================================================
// Public API - RefreshGridRow
// ============================================================
describe("DataverseLabelTranslator.RefreshGridRow", () => {
  it("calls grid refreshRow with the recid", () => {
    Translator.RefreshGridRow("rec-1");
    expect(mockGrid.refreshRow).toHaveBeenCalledWith("rec-1");
  });
});

// ============================================================
// Public API - RefreshGrid
// ============================================================
describe("DataverseLabelTranslator.RefreshGrid", () => {
  it("calls grid refresh", () => {
    Translator.RefreshGrid();
    expect(mockGrid.refresh).toHaveBeenCalled();
  });
});

// ============================================================
// Public API - LockGrid / UnlockGrid
// ============================================================
describe("DataverseLabelTranslator.LockGrid", () => {
  it("locks the grid with the message", () => {
    Translator.LockGrid("Loading");
    expect(mockGrid.lock).toHaveBeenCalledWith("Loading", true);
  });

  it("uses default message if none given", () => {
    Translator.LockGrid();
    expect(mockGrid.lock).toHaveBeenCalledWith("Loading...", true);
  });
});

describe("DataverseLabelTranslator.UnlockGrid", () => {
  it("unlocks the grid", () => {
    Translator.UnlockGrid();
    expect(mockGrid.unlock).toHaveBeenCalled();
  });
});

// ============================================================
// Public API - IsUnifiedType
// ============================================================
describe("DataverseLabelTranslator.IsUnifiedType", () => {
  it("returns true for unified types", () => {
    expect(Translator.IsUnifiedType("attributes")).toBe(true);
    expect(Translator.IsUnifiedType("forms")).toBe(true);
    expect(Translator.IsUnifiedType("sitemap")).toBe(true);
    expect(Translator.IsUnifiedType("relationships")).toBe(true);
  });

  it("returns false for unknown type", () => {
    expect(Translator.IsUnifiedType("foo")).toBe(false);
  });
});

// ============================================================
// Public API - BuildAiTranslateDataSource
// ============================================================
describe("DataverseLabelTranslator.BuildAiTranslateDataSource", () => {
  it("returns data source object", () => {
    const ds = Translator.BuildAiTranslateDataSource();
    expect(ds).toBeDefined();
    expect(typeof ds).toBe("object");
  });

  it.each([
    ["relationships", "Relationships", "DisplayText"],
    ["sitemap", "Sitemap", "Description"]
  ])("builds type-specific AI data source for %s", (type, typeText, component) => {
    mockToolbar.get.mockImplementation((id) => {
      if (id === "type") return { selected: type };
      if (id === "type:" + type) return { text: typeText };
      if (id === "component") return { selected: component };
      if (id === "component:" + component)
        return { text: component === "DisplayText" ? "Display Text" : "Description" };
      return null;
    });
    Translator.SetBaseLanguage("1033");
    mockGrid.columns = [
      { field: "schemaName", text: "Schema Name" },
      { field: "1033", text: "English", code: "en" },
      { field: "1041", text: "Japanese", code: "ja" }
    ];
    mockGrid.records = [
      {
        recid: type + "-row",
        gridKey: type + "|row",
        schemaName: typeText + " row",
        rowType: type + ".row",
        1033: "English",
        1041: ""
      }
    ];

    const ds = Translator.BuildAiTranslateDataSource();

    expect(ds.typeName).toBe(typeText);
    expect(ds.componentText).toBe(component === "DisplayText" ? "Display Text" : "Description");
    expect(ds.baseLcid).toBe("1033");
    expect(ds.languages.map((language) => language.lcid)).toEqual(["1033", "1041"]);
    expect(ds.rows[0]).toMatchObject({
      targetRecid: type + "-row",
      schemaName: typeText + " row"
    });
  });
});

// ============================================================
// Public API - ShowAITranslate
// ============================================================
describe("DataverseLabelTranslator.ShowAITranslate", () => {
  it("is callable", () => {
    expect(typeof Translator.ShowAITranslate).toBe("function");
    expect(() => Translator.ShowAITranslate()).not.toThrow();
  });
});

// ============================================================
// Public API - RenderTranslationCell
// ============================================================
describe("DataverseLabelTranslator.RenderTranslationCell", () => {
  it("renders the value as html", () => {
    const html = Translator.RenderTranslationCell({ 1033: "Hello" }, "1033");
    expect(html).toBe("Hello");
  });

  it("escapes html tags", () => {
    const html = Translator.RenderTranslationCell({ 1033: "<b>X</b>" }, "1033");
    expect(html).toContain("&lt;");
    expect(html).not.toContain("<b>");
  });

  it("returns readonly placeholder when no value", () => {
    const record = { _emptyReadonlyPlaceholder: "RO" };
    const html = Translator.RenderTranslationCell(record, "1033");
    expect(html).toContain("RO");
  });

  it("returns empty string when no value and no placeholder", () => {
    const html = Translator.RenderTranslationCell({}, "1033");
    expect(html).toBe("");
  });
});

// ============================================================
// Public API - CreateTranslationCellRenderer
// ============================================================
describe("DataverseLabelTranslator.CreateTranslationCellRenderer", () => {
  it("returns a renderer function", () => {
    const renderer = Translator.CreateTranslationCellRenderer("1033");
    expect(typeof renderer).toBe("function");
  });

  it("the returned renderer renders the record field", () => {
    const renderer = Translator.CreateTranslationCellRenderer("1033");
    const html = renderer({ 1033: "Hello" });
    expect(html).toBe("Hello");
  });
});

// ============================================================
// Public API - Load
// ============================================================
describe("DataverseLabelTranslator.Load", () => {
  it("is a callable function", () => {
    expect(typeof Translator.Load).toBe("function");
  });

  it("loads server rows, applies language columns, and finalizes the grid", async () => {
    mockHelper.GetTranslator.mockReturnValue(Translator);
    mockHelper.RunServerLoad.mockImplementation((options) => {
      expect(options.getPayload()).toMatchObject({
        translatorType: "attributes",
        solutionId: "sol-1",
        entityName: "account",
        component: "DisplayText"
      });

      options.onLoaded({
        baseLanguage: "1033",
        grid: {
          languageColumns: [
            { field: "1033", text: "English" },
            { field: "1041", text: "Japanese" }
          ],
          rows: [
            {
              gridKey: "parent",
              schemaName: "Parent",
              isEditable: true,
              1033: "Parent",
              children: [
                {
                  gridKey: "child",
                  schemaName: "Child",
                  isEditable: false,
                  1041: "Child JP"
                }
              ]
            }
          ]
        }
      });

      return Promise.resolve();
    });

    await Translator.Load("Custom loading");

    expect(mockGrid.lock).toHaveBeenCalledWith("Custom loading", true);
    expect(mockGrid.clear).toHaveBeenCalled();
    expect(mockGrid.addColumn).toHaveBeenCalledTimes(2);
    expect(mockHelper.ApplyPlaceholder).toHaveBeenCalled();
    expect(mockHelper.FinalizeGrid).toHaveBeenCalledWith(
      [
        expect.objectContaining({
          recid: "parent",
          schemaName: "Parent",
          w2ui: expect.objectContaining({
            editable: true,
            children: [
              expect.objectContaining({
                recid: "child",
                _emptyReadonlyPlaceholder: "-"
              })
            ]
          })
        })
      ],
      Translator
    );
    expect(Translator.GetBaseLanguage()).toBe("1033");
  });

  it("infers language columns from server rows when metadata omits them", async () => {
    mockHelper.GetTranslator.mockReturnValue(Translator);
    mockHelper.RunServerLoad.mockImplementation((options) => {
      options.onLoaded({
        grid: {
          rows: [
            {
              gridKey: "row-1",
              schemaName: "Row 1",
              1041: "JP",
              children: [{ gridKey: "row-1-child", schemaName: "Child", 1033: "EN" }]
            }
          ]
        }
      });

      return Promise.resolve();
    });

    await Translator.Load();

    expect(mockGrid.addColumn).toHaveBeenCalledWith(expect.objectContaining({ field: "1033" }));
    expect(mockGrid.addColumn).toHaveBeenCalledWith(expect.objectContaining({ field: "1041" }));
  });

  it("builds Sitemap load payload with Entity None and Description component", async () => {
    mockHelper.GetTranslator.mockReturnValue(Translator);
    mockToolbar.get.mockImplementation((id) => {
      if (id === "type") return { selected: "sitemap" };
      if (id === "type:sitemap") return { text: "Sitemap" };
      if (id === "solutionSelect") return { selected: "sol-sitemap" };
      if (id === "entitySelect") return { selected: "none" };
      if (id === "component") return { selected: "Description" };
      return null;
    });

    mockHelper.RunServerLoad.mockImplementation((options) => {
      expect(options.getPayload()).toEqual({
        translatorType: "sitemap",
        solutionId: "sol-sitemap",
        entityName: "none",
        entityId: null,
        component: "Description"
      });
      options.onLoaded({ baseLanguage: "1033", grid: { rows: [] } });
      return Promise.resolve();
    });

    await Translator.Load();

    expect(mockHelper.RunServerLoad).toHaveBeenCalled();
    expect(Translator.GetCurrentToolbarTypeText()).toBe("Sitemap");
  });
});

// ============================================================
// Public API - Save
// ============================================================
describe("DataverseLabelTranslator.Save", () => {
  it("alerts when no changes to save", () => {
    mockGrid.records = [];
    Translator.Save();
    expect(mockDialogHelper.alert).toHaveBeenCalled();
  });

  it("is callable when there are changes", () => {
    mockGrid.records = [
      { recid: "1", gridKey: "k1", schemaName: "attr1", rowType: "attr", w2ui: { changes: { 1033: "Hello" } } }
    ];
    expect(() => Translator.Save()).not.toThrow();
  });

  it("builds changed row payload and publish payload for server save", async () => {
    mockHelper.GetTranslator.mockReturnValue(Translator);
    Translator.SetBaseLanguage("1033");
    mockGrid.records = [
      {
        recid: "1",
        gridKey: "k1",
        rowType: "attribute",
        1033: "Old",
        w2ui: { changes: { 1033: "Hello", schemaName: "Ignored" } }
      },
      {
        recid: "2",
        gridKey: "k2",
        rowType: "attribute",
        isEditable: false,
        w2ui: { changes: { 1033: "Skip" } }
      }
    ];

    let savePayload;
    let publishPayload;
    mockHelper.RunServerSaveFlow.mockImplementation((options) => {
      savePayload = options.getSavePayload();
      publishPayload = options.getPublishPayload({ publishTargets: ["account"] });
      expect(options.getPublishedPayload({ publishTargets: [] })).toBeNull();
      expect(options.shouldReload({ changed: true })).toBe(true);
      return Promise.resolve({ ok: true });
    });

    await Translator.Save();

    expect(savePayload).toMatchObject({
      translatorType: "attributes",
      solutionId: "sol-1",
      entityName: "account",
      component: "DisplayText",
      baseLanguage: "1033",
      changedRows: [
        {
          gridKey: "k1",
          recid: "1",
          rowType: "attribute",
          changes: { 1033: "Hello" }
        }
      ]
    });
    expect(publishPayload).toEqual({
      translatorType: "attributes",
      publishTargets: ["account"]
    });
  });

  it("builds Relationship save payload from relationship grid rows", async () => {
    mockHelper.GetTranslator.mockReturnValue(Translator);
    Translator.SetBaseLanguage("1033");
    mockToolbar.get.mockImplementation((id) => {
      if (id === "type") return { selected: "relationships" };
      if (id === "type:relationships") return { text: "Relationships" };
      if (id === "solutionSelect") return { selected: "sol-rel" };
      if (id === "entitySelect") return { selected: "none" };
      if (id === "component") return { selected: "DisplayText" };
      return null;
    });
    mockGrid.records = [
      {
        recid: "rel-1",
        gridKey: "relationships|rel-id|1:N|account_contact|AssociatedMenuConfiguration|contact",
        rowType: "relationships.row",
        1033: "Old",
        w2ui: { changes: { 1033: "Contacts", 1041: "連絡先", schemaName: "Ignored" } }
      }
    ];

    let savePayload;
    let publishPayload;
    mockHelper.RunServerSaveFlow.mockImplementation((options) => {
      savePayload = options.getSavePayload();
      publishPayload = options.getPublishPayload({
        publishTargets: [{ kind: "entity", id: "contact" }]
      });
      return Promise.resolve();
    });

    await Translator.Save();

    expect(savePayload).toMatchObject({
      translatorType: "relationships",
      solutionId: "sol-rel",
      entityName: "none",
      entityId: null,
      component: "DisplayText",
      baseLanguage: "1033",
      changedRows: [
        {
          gridKey: "relationships|rel-id|1:N|account_contact|AssociatedMenuConfiguration|contact",
          recid: "rel-1",
          rowType: "relationships.row",
          changes: { 1033: "Contacts", 1041: "連絡先" }
        }
      ]
    });
    expect(publishPayload).toEqual({
      translatorType: "relationships",
      publishTargets: [{ kind: "entity", id: "contact" }]
    });
  });

  it("starts operation status for ribbon import saves", async () => {
    mockHelper.GetTranslator.mockReturnValue(Translator);
    mockToolbar.get.mockImplementation((id) => {
      if (id === "type") return { selected: "ribbons" };
      if (id === "solutionSelect") return { selected: "sol-1" };
      if (id === "entitySelect") return { selected: "account" };
      if (id === "component") return { selected: "DisplayText" };
      if (id === "w2ui-save") return {};
      return null;
    });
    mockGrid.records = [
      {
        recid: "1",
        gridKey: "k1",
        rowType: "ribbon",
        w2ui: { changes: { 1033: "Hello" } }
      }
    ];

    mockHelper.RunServerSaveFlow.mockImplementation((options) => {
      expect(
        options.afterSave({
          import: {
            importJobId: "job-1",
            operationId: "op-1"
          }
        })
      ).toBe(true);
      return Promise.resolve();
    });

    await Translator.Save();

    expect(mockGrid.lock).toHaveBeenCalledWith("Importing ribbons", true);
  });
});

// ============================================================
// Public API - RemoveOverriddenCellLabels
// ============================================================
describe("DataverseLabelTranslator.RemoveOverriddenCellLabels", () => {
  it("is callable", () => {
    expect(typeof Translator.RemoveOverriddenCellLabels).toBe("function");
    expect(() => Translator.RemoveOverriddenCellLabels()).not.toThrow();
  });

  it("runs server save flow after confirmation", async () => {
    mockHelper.GetTranslator.mockReturnValue(Translator);
    mockDialogHelper.confirm.mockResolvedValue(true);

    let savePayload;
    mockHelper.RunServerSaveFlow.mockImplementation((options) => {
      savePayload = options.getSavePayload();
      expect(options.shouldReload({ publishTargets: ["form"] })).toBe(true);
      expect(options.getPublishPayload({ publishTargets: ["form"] })).toEqual({
        translatorType: "attributes",
        publishTargets: ["form"]
      });
      return Promise.resolve("removed");
    });

    await Translator.RemoveOverriddenCellLabels();

    expect(savePayload).toMatchObject({
      translatorType: "attributes",
      operation: "RemoveOverriddenCellLabels"
    });
  });
});

// ============================================================
// Public API - errorHandler
// ============================================================
describe("DataverseLabelTranslator.errorHandler", () => {
  it("is a function", () => {
    expect(typeof Translator.errorHandler).toBe("function");
  });
});
