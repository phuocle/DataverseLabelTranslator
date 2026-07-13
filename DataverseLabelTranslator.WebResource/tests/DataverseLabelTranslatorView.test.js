import { beforeEach, describe, expect, it, vi } from "vitest";

import "../js/DataverseLabelTranslator.js";

const toolbar = {
  get: vi.fn(),
  enable: vi.fn(),
  disable: vi.fn(),
  show: vi.fn(),
  hide: vi.fn(),
  refresh: vi.fn()
};

const grid = {
  name: "grid",
  columns: [],
  records: [],
  searches: [],
  show: { selectColumn: true },
  clear: vi.fn(),
  addColumn: vi.fn(),
  removeColumn: vi.fn(),
  addSearch: vi.fn(),
  removeSearch: vi.fn(),
  lock: vi.fn(),
  unlock: vi.fn(),
  refresh: vi.fn(),
  sort: vi.fn(),
  getSelection: vi.fn(() => []),
  box: null
};

function configureToolbar({
  type = "views",
  component = "DisplayText",
  entity = "none",
  solution = "solution-id"
} = {}) {
  toolbar.get.mockImplementation((id) => {
    if (id === "type") return { selected: type };
    if (id === "type:views") return { id: "views", text: "Views" };
    if (id === "component") return { selected: component };
    if (id === "entitySelect") return { selected: entity };
    if (id === "solutionSelect") return { selected: solution };
    if (id === "removeOverriddenAttributeLabels") return { id };
    return null;
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  grid.columns = [];
  grid.records = [];
  grid.searches = [];
  grid.show.selectColumn = true;

  window.w2ui = {
    grid,
    grid_toolbar: toolbar
  };

  configureToolbar();

  window.Helper = {
    GetTranslator: vi.fn(() => window.DataverseLabelTranslator),
    GetOperationLoading: vi.fn(() => "Loading..."),
    GetOperationReLoading: vi.fn(() => "Reloading..."),
    GetPlaceholderDisplayText: vi.fn(() => "(display text)"),
    GetPlaceholderDescription: vi.fn(() => "(description)"),
    GetPlaceholderReadonly: vi.fn(() => "(readonly)"),
    ApplyPlaceholder: vi.fn(),
    FinalizeGrid: vi.fn((records) => {
      grid.records = records;
    }),
    FormatLanguageColumnHeader: vi.fn((lcid) => `Language ${lcid}`),
    GetLanguageColumnCode: vi.fn((field) => `LCID ${field}`),
    RunServerLoad: vi.fn(({ getPayload, onLoaded }) => {
      const payload = getPayload();
      onLoaded({
        baseLanguage: "1033",
        grid: {
          mode: "tree",
          title: "Views",
          languageColumns: [{ field: "1033", text: "English" }],
          rows: []
        }
      });
      return Promise.resolve(payload);
    }),
    RunServerSaveFlow: vi.fn(({ getSavePayload, getPublishPayload, getPublishedPayload, afterSave, shouldReload }) => {
      const payload = getSavePayload();
      const output = {
        changed: false,
        publishTargets: [{ kind: "entity", id: "account" }]
      };
      return Promise.resolve({
        payload,
        publishPayload: getPublishPayload(output),
        publishedPayload: getPublishedPayload(output),
        afterSave: afterSave(output),
        shouldReload: shouldReload(output)
      });
    }),
    ShowError: vi.fn()
  };

  window.DialogHelper = {
    alert: vi.fn(() => Promise.resolve())
  };
});

describe("DataverseLabelTranslator Views", () => {
  it("treats Views as a unified global type with Description support", () => {
    configureToolbar({ type: "views", component: "Description" });

    expect(window.DataverseLabelTranslator.IsUnifiedType("views")).toBe(true);
    expect(window.DataverseLabelTranslator.GetType()).toBe("views");
    expect(window.DataverseLabelTranslator.GetCurrentToolbarTypeText()).toBe("Views");
    expect(window.DataverseLabelTranslator.GetComponent()).toBe("Description");
    expect(window.DataverseLabelTranslator.IsDescriptionComponent()).toBe(true);
  });

  it("loads Views with the selected solution, none entity, and selected component", async () => {
    configureToolbar({
      type: "views",
      component: "Description",
      entity: "none",
      solution: "phuocle-solution-id"
    });

    const payload = await window.DataverseLabelTranslator.Load("Loading Views");

    expect(window.Helper.RunServerLoad).toHaveBeenCalledTimes(1);
    expect(payload).toEqual({
      translatorType: "views",
      solutionId: "phuocle-solution-id",
      entityName: "none",
      entityId: null,
      component: "Description"
    });
  });

  it("saves edited View child rows and normalizes placeholders", async () => {
    configureToolbar({
      type: "views",
      component: "DisplayText",
      entity: "none",
      solution: "phuocle-solution-id"
    });
    window.DataverseLabelTranslator.SetBaseLanguage("1033");
    grid.records = [
      {
        recid: "view:account-active",
        gridKey: "views|00000000-0000-0000-0000-000000000001|name|account",
        rowType: "views.row",
        w2ui: {
          changes: {
            1033: "Updated View",
            1041: "(display text)",
            notLanguage: "ignored"
          }
        }
      },
      {
        recid: "views:entity:account",
        gridKey: "views|account",
        isEditable: false,
        w2ui: {
          changes: {
            1033: "Ignored parent"
          }
        }
      }
    ];

    const result = await window.DataverseLabelTranslator.Save();

    expect(window.Helper.RunServerSaveFlow).toHaveBeenCalledTimes(1);
    expect(result.payload).toMatchObject({
      translatorType: "views",
      solutionId: "phuocle-solution-id",
      entityName: "none",
      component: "DisplayText",
      baseLanguage: "1033"
    });
    expect(result.payload.changedRows).toEqual([
      {
        gridKey: "views|00000000-0000-0000-0000-000000000001|name|account",
        recid: "view:account-active",
        rowType: "views.row",
        changes: {
          1033: "Updated View",
          1041: ""
        }
      }
    ]);
    expect(result.publishPayload).toEqual({
      translatorType: "views",
      publishTargets: [{ kind: "entity", id: "account" }]
    });
    expect(result.publishedPayload).toEqual(result.publishPayload);
    expect(result.afterSave).toBe(false);
    expect(result.shouldReload).toBe(true);
  });
});
