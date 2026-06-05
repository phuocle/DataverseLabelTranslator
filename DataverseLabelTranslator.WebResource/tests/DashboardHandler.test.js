import { beforeEach, describe, expect, it, vi } from "vitest";

var importCounter = 0;

function languageColumns() {
  return [
    { field: "schemaName", text: "Schema Name" },
    { field: "1033", text: "English (en-us) (1033)" },
    { field: "1041", caption: "Japanese (ja-jp) (1041)" },
    { field: "1066", label: "Vietnamese (vi-vn) (1066)" }
  ];
}

function createGrid() {
  var grid = {
    columns: languageColumns(),
    records: [],
    clear: vi.fn(function () {
      grid.records = [];
    }),
    add: vi.fn(function (records) {
      grid.records = grid.records.concat(records);
    }),
    unlock: vi.fn()
  };

  return grid;
}

function dashboard(overrides) {
  return Object.assign(
    {
      formid: "dashboard-1",
      name: "Sales Dashboard",
      type: 0,
      objecttypecode: "none",
      Label: {
        LocalizedLabels: [
          { LanguageCode: 1033, Label: "Sales" },
          { LanguageCode: 1041, Label: "Sales JA" }
        ]
      }
    },
    overrides || {}
  );
}

function createActionResult(type, object) {
  return { ok: true, type: type, object: object || {} };
}

function getChangedDashboardIds(input) {
  var dashboardIds = [];

  (input.dashboardUpdates || []).forEach(function (update) {
    if (dashboardIds.indexOf(update.dashboardId) === -1) {
      dashboardIds.push(update.dashboardId);
    }
  });

  return dashboardIds;
}

function createHarness(options) {
  options = options || {};
  var grid = options.grid || createGrid();
  var state = {
    solution: Object.prototype.hasOwnProperty.call(options, "solution") ? options.solution : "solution-1",
    records: options.records || [],
    customActionCalls: []
  };

  var xrmTranslator = {
    baseLanguage: Object.prototype.hasOwnProperty.call(options, "baseLanguage") ? options.baseLanguage : 1033,
    metadata: Object.prototype.hasOwnProperty.call(options, "initialMetadata") ? options.initialMetadata : [],
    IsDisplayTextComponent: vi.fn(function () {
      return true;
    }),
    GetGrid: vi.fn(function () {
      return grid;
    }),
    GetSolution: vi.fn(function () {
      return state.solution;
    }),
    SetMetadata: vi.fn(function (metadata) {
      if (options.setMetadataNoop) {
        return;
      }

      xrmTranslator.metadata = metadata;
    }),
    AddSummary: vi.fn(),
    EnableLoadAndSave: vi.fn(),
    LockGrid: vi.fn(),
    UnlockGrid: vi.fn(),
    errorHandler: vi.fn(),
    GetAllRecords: vi.fn(function () {
      return state.records;
    })
  };

  if (options.includeGetMetadata !== false) {
    xrmTranslator.GetMetadata = vi.fn(function () {
      return Object.prototype.hasOwnProperty.call(options, "getMetadataResult")
        ? options.getMetadataResult
        : xrmTranslator.metadata;
    });
  }

  if (options.includeSetBaseLanguage !== false) {
    xrmTranslator.SetBaseLanguage = vi.fn(function (baseLanguage) {
      xrmTranslator.baseLanguage = baseLanguage;
    });
  }

  if (options.includeGetBaseLanguage !== false) {
    xrmTranslator.GetBaseLanguage = vi.fn(function () {
      return Object.prototype.hasOwnProperty.call(options, "getBaseLanguage")
        ? options.getBaseLanguage
        : xrmTranslator.baseLanguage;
    });
  }

  var helper = {
    CustomActionTypes: {
      Loading: "Loading",
      Saving: "Saving",
      Publishing: "Publishing",
      Published: "Published",
      Other: "Other"
    },
    ComponentTypes: {
      DisplayText: "DisplayText",
      Description: "Description"
    },
    GetTranslator: vi.fn(function () {
      return xrmTranslator;
    }),
    GetPlaceholderDisplayText: vi.fn(function () {
      return "Add display text";
    }),
    GetPlaceholderDisplayTextBase: vi.fn(function () {
      return "Add display text (*)";
    }),
    GetOperationLoading: vi.fn(function () {
      return "Loading ...";
    }),
    GetOperationSaving: vi.fn(function () {
      return "Saving ...";
    }),
    GetOperationPublishing: vi.fn(function () {
      return "Publishing ...";
    }),
    GetOperationPublished: vi.fn(function () {
      return "Published";
    }),
    GetOperationReLoading: vi.fn(function () {
      return "Re-Loading ...";
    }),
    IsEmptyLabelValue: vi.fn(function (value) {
      return value == null || String(value).trim().length === 0;
    }),
    ApplyPlaceholder: vi.fn(function (record, editablePlaceholder, baseEditablePlaceholder, app) {
      if (!editablePlaceholder) {
        return;
      }

      record._emptyEditablePlaceholder = editablePlaceholder;
      if (baseEditablePlaceholder && app.baseLanguage) {
        record._emptyEditablePlaceholders = record._emptyEditablePlaceholders || {};
        record._emptyEditablePlaceholders[String(app.baseLanguage)] = baseEditablePlaceholder;
      }
    }),
    AddLocalizedLabelsToRecord: vi.fn(function (record, localizedLabels) {
      (localizedLabels || []).forEach(function (label) {
        record[String(label.LanguageCode)] = label.Label;
      });
    }),
    GetComponentLocalizedLabels: vi.fn(function (metadata, component) {
      var propertyName = component === helper.ComponentTypes.Description ? "Description" : "Label";
      return metadata && metadata[propertyName] && metadata[propertyName].LocalizedLabels
        ? metadata[propertyName].LocalizedLabels
        : [];
    }),
    GetChangedLabels: vi.fn(function (changes, allowEmpty) {
      var labels = [];

      for (var change in changes) {
        if (!Object.prototype.hasOwnProperty.call(changes, change)) {
          continue;
        }

        var label = changes[change];
        if (label == null) {
          if (!allowEmpty) {
            continue;
          }
          label = "";
        }

        if (!allowEmpty && !label) {
          continue;
        }

        labels.push({ LanguageCode: change, Label: label });
      }

      return labels;
    }),
    ValidateBaseLanguageNotEmpty: vi.fn(function (record, changes, validateOptions) {
      validateOptions = validateOptions || {};
      var app = validateOptions.app || xrmTranslator;

      if (!app.IsDisplayTextComponent()) {
        return;
      }

      var baseLanguage = app.baseLanguage;
      if (!baseLanguage) {
        return;
      }

      baseLanguage = String(baseLanguage);
      if (!Object.prototype.hasOwnProperty.call(changes, baseLanguage)) {
        return;
      }

      if (!helper.IsEmptyLabelValue(changes[baseLanguage])) {
        return;
      }

      var rowPath =
        typeof validateOptions.getRowPath === "function" ? validateOptions.getRowPath(record) : record.schemaName;
      throw new Error(
        "Display Text in the base language (" +
          helper.GetLanguageColumnText(baseLanguage) +
          ") cannot be empty.\nRow: " +
          (rowPath || "(unknown)")
      );
    }),
    GetLanguageColumnText: vi.fn(function (languageCode) {
      var columns = (grid && grid.columns) || [];
      var field = String(languageCode);

      for (var i = 0; i < columns.length; i++) {
        if (String(columns[i].field) === field) {
          return columns[i].text || columns[i].caption || columns[i].label || field;
        }
      }

      return field;
    }),
    GetCustomActionObject: vi.fn(function (result) {
      return result && result.object ? result.object : {};
    }),
    FinalizeGrid: vi.fn(function (records, app) {
      app.AddSummary(records);
      app.GetGrid().add(records);

      if (typeof app.UnlockGrid === "function") {
        app.UnlockGrid();
      } else {
        app.GetGrid().unlock();
      }
    }),
    ExecuteTypedCustomAction: vi.fn(
      options.executeTypedCustomAction ||
        function (functionName, type, input) {
          state.customActionCalls.push({ functionName: functionName, type: type, input: input });

          if (type === helper.CustomActionTypes.Loading) {
            var output = {};
            if (options.omitLoadingBaseLanguage !== true) {
              output.baseLanguage = Object.prototype.hasOwnProperty.call(options, "loadingBaseLanguage")
                ? options.loadingBaseLanguage
                : "1033";
            }

            if (options.omitLoadingDashboards !== true) {
              output.dashboards = options.dashboards || [];
            }

            return Promise.resolve(createActionResult(type, output));
          }

          if (type === helper.CustomActionTypes.Saving) {
            return Promise.resolve(createActionResult(type, { dashboardIds: getChangedDashboardIds(input) }));
          }

          if (type === helper.CustomActionTypes.Publishing) {
            var publishingIds = Object.prototype.hasOwnProperty.call(options, "publishingDashboardIds")
              ? options.publishingDashboardIds
              : input.dashboardIds;
            return Promise.resolve(createActionResult(type, { dashboardIds: publishingIds }));
          }

          if (type === helper.CustomActionTypes.Published) {
            var publishedIds = Object.prototype.hasOwnProperty.call(options, "publishedDashboardIds")
              ? options.publishedDashboardIds
              : input.dashboardIds;
            return Promise.resolve(createActionResult(type, { dashboardIds: publishedIds }));
          }

          return Promise.resolve(createActionResult(type, {}));
        }
    ),
    RunServerLoad: vi.fn(function (runOptions) {
      var app = runOptions.app || xrmTranslator;
      var payload = typeof runOptions.getPayload === "function" ? runOptions.getPayload() : runOptions.payload || {};

      return helper
        .ExecuteTypedCustomAction(runOptions.actionName, helper.CustomActionTypes.Loading, payload)
        .then(function (result) {
          var output = helper.GetCustomActionObject(result);

          if (typeof runOptions.onLoaded === "function") {
            runOptions.onLoaded(output, result);
          }

          return result;
        })
        .catch(function (error) {
          if (typeof app.UnlockGrid === "function") {
            app.UnlockGrid();
          }

          if (runOptions.handleError !== false && typeof app.errorHandler === "function") {
            app.errorHandler(error);
            return null;
          }

          throw error;
        });
    }),
    RunServerSaveFlow: vi.fn(function (runOptions) {
      var app = runOptions.app || xrmTranslator;
      var savePayload =
        typeof runOptions.getSavePayload === "function" ? runOptions.getSavePayload() : runOptions.payload || {};

      app.LockGrid(helper.GetOperationSaving());

      return helper
        .ExecuteTypedCustomAction(runOptions.actionName, helper.CustomActionTypes.Saving, savePayload)
        .then(function (saveResult) {
          var saveOutput = helper.GetCustomActionObject(saveResult);
          var publishPayload =
            typeof runOptions.getPublishPayload === "function"
              ? runOptions.getPublishPayload(saveOutput, saveResult)
              : null;

          if (!publishPayload) {
            app.UnlockGrid();
            return saveResult;
          }

          app.LockGrid(helper.GetOperationPublishing());
          return helper
            .ExecuteTypedCustomAction(runOptions.actionName, helper.CustomActionTypes.Publishing, publishPayload)
            .then(function (publishingResult) {
              var publishingOutput = helper.GetCustomActionObject(publishingResult);
              var publishedPayload =
                typeof runOptions.getPublishedPayload === "function"
                  ? runOptions.getPublishedPayload(publishingOutput, publishingResult, saveOutput)
                  : publishPayload;

              if (!publishedPayload) {
                app.UnlockGrid();
                return publishingResult;
              }

              app.LockGrid(helper.GetOperationPublished());
              return helper.ExecuteTypedCustomAction(
                runOptions.actionName,
                helper.CustomActionTypes.Published,
                publishedPayload
              );
            });
        })
        .then(function (result) {
          var output = helper.GetCustomActionObject(result);
          var shouldReload =
            typeof runOptions.shouldReload === "function" ? runOptions.shouldReload(output, result) : false;

          if (!shouldReload) {
            app.UnlockGrid();
            return result;
          }

          app.LockGrid(helper.GetOperationReLoading());

          if (typeof runOptions.reloadAction !== "function") {
            app.UnlockGrid();
            return result;
          }

          return runOptions.reloadAction(result, output).then(function () {
            app.UnlockGrid();
            return result;
          });
        })
        .catch(function (error) {
          if (typeof app.UnlockGrid === "function") {
            app.UnlockGrid();
          }

          throw error;
        });
    })
  };

  globalThis.window = {};
  if (options.preexistingHandler !== false) {
    globalThis.window.DashboardHandler = {};
  }
  globalThis.XrmTranslator = xrmTranslator;
  globalThis.Helper = helper;
  globalThis.DialogHelper = {
    alert: vi.fn(function () {
      return Promise.resolve();
    })
  };

  return {
    grid: grid,
    state: state,
    XrmTranslator: xrmTranslator,
    Helper: helper,
    DialogHelper: globalThis.DialogHelper
  };
}

async function loadHandler() {
  vi.resetModules();
  await import("../js/Handler/DashboardHandler.js?test=" + ++importCounter);
  return globalThis.window.DashboardHandler;
}

async function setup(options) {
  var harness = createHarness(options);
  var handler = await loadHandler();
  return Object.assign({ handler: handler }, harness);
}

function getActionCalls(context, type) {
  return context.state.customActionCalls.filter(function (call) {
    return call.type === type;
  });
}

beforeEach(function () {
  vi.restoreAllMocks();
  delete globalThis.window;
  delete globalThis.XrmTranslator;
  delete globalThis.Helper;
  delete globalThis.DialogHelper;
});

describe("DashboardHandler.Load", function () {
  it("initializes the namespace and fills parent dashboard rows", async function () {
    var context = await setup({
      preexistingHandler: false,
      solution: null,
      dashboards: [
        dashboard({
          formid: "dashboard-b",
          name: "B name",
          Label: { LocalizedLabels: [{ LanguageCode: 1033, Label: "Beta" }] }
        }),
        dashboard({
          formid: "dashboard-a",
          name: "A name",
          Label: {
            LocalizedLabels: [
              { LanguageCode: 1033, Label: "Alpha" },
              { LanguageCode: 1066, Label: "" }
            ]
          }
        })
      ]
    });

    var result = await context.handler.Load("Custom loading");

    expect(globalThis.window.DashboardHandler).toBe(context.handler);
    expect(result.type).toBe("Loading");
    expect(context.XrmTranslator.LockGrid).toHaveBeenCalledWith("Custom loading");
    expect(context.Helper.ExecuteTypedCustomAction).toHaveBeenCalledWith("Dashboard", "Loading", {
      solutionId: null
    });
    expect(context.XrmTranslator.SetBaseLanguage).toHaveBeenCalledWith("1033");
    expect(
      context.XrmTranslator.metadata.map(function (item) {
        return item.formid;
      })
    ).toEqual(["dashboard-b", "dashboard-a"]);

    var records = context.grid.add.mock.calls[0][0];
    expect(records).toHaveLength(2);
    expect(records[0]).toMatchObject({
      recid: "dashboard-a",
      schemaName: "Alpha",
      1033: "Alpha",
      1066: "",
      _emptyEditablePlaceholder: "Add display text"
    });
    expect(records[0]._emptyEditablePlaceholders["1033"]).toBe("Add display text (*)");
    expect(records[0].w2ui).toBeUndefined();
    expect(records[1].schemaName).toBe("Beta");
    expect(context.XrmTranslator.UnlockGrid).toHaveBeenCalledOnce();
    expect(context.XrmTranslator.EnableLoadAndSave).not.toHaveBeenCalled();
  });

  it("uses base-language fallbacks and handles empty load output", async function () {
    var context = await setup({
      baseLanguage: null,
      getBaseLanguage: 1041,
      loadingBaseLanguage: null,
      dashboards: [
        dashboard({
          formid: "dashboard-fallback",
          name: "Fallback name",
          Label: { LocalizedLabels: [{ LanguageCode: 1033, Label: "English only" }] }
        }),
        dashboard({
          formid: null,
          name: null,
          Label: {}
        })
      ]
    });

    await context.handler.Load();

    var records = context.grid.add.mock.calls[0][0];
    expect(records[0].schemaName).toBe("(unknown)");
    expect(records[1].schemaName).toBe("Fallback name");
    expect(context.XrmTranslator.LockGrid).toHaveBeenCalledWith("Loading ...");

    context = await setup({
      baseLanguage: null,
      includeSetBaseLanguage: false,
      includeGetBaseLanguage: false,
      loadingBaseLanguage: "1066",
      dashboards: []
    });

    await context.handler.Load();

    expect(context.XrmTranslator.baseLanguage).toBe("1066");
    expect(context.grid.add).toHaveBeenCalledWith([]);
  });

  it("covers metadata, base-language, and label fallbacks", async function () {
    var context = await setup({
      baseLanguage: null,
      includeGetMetadata: false,
      includeGetBaseLanguage: false,
      omitLoadingBaseLanguage: true,
      dashboards: [
        dashboard({
          formid: "metadata-fallback",
          name: "Metadata fallback",
          Label: { LocalizedLabels: [{ LanguageCode: 1033, Label: "English only" }] }
        })
      ]
    });

    await context.handler.Load();

    expect(context.grid.add.mock.calls[0][0][0].schemaName).toBe("Metadata fallback");

    context = await setup({
      dashboards: [
        dashboard({
          formid: "null-label",
          name: "Null label fallback",
          Label: { LocalizedLabels: [{ LanguageCode: 1033, Label: null }] }
        })
      ]
    });

    await context.handler.Load();

    expect(context.grid.add.mock.calls[0][0][0].schemaName).toBe("Null label fallback");

    context = await setup({
      includeGetMetadata: false,
      initialMetadata: null,
      setMetadataNoop: true,
      omitLoadingDashboards: true
    });

    await context.handler.Load();

    expect(context.grid.add).toHaveBeenCalledWith([]);
  });

  it("passes load failures to the shared error handler", async function () {
    var error = new Error("load failed");
    var context = await setup({
      executeTypedCustomAction: function () {
        return Promise.reject(error);
      }
    });

    var result = await context.handler.Load();

    expect(result).toBeNull();
    expect(context.XrmTranslator.UnlockGrid).toHaveBeenCalledOnce();
    expect(context.XrmTranslator.errorHandler).toHaveBeenCalledWith(error);
    expect(context.grid.add).not.toHaveBeenCalled();
  });
});

describe("DashboardHandler.Save", function () {
  it("shows a dialog and skips saving when no valid changes exist", async function () {
    var inheritedChanges = Object.create({ 1041: "Inherited" });
    var context = await setup({
      records: [
        {},
        { recid: "summary", schemaName: "Summary", w2ui: { summary: true, changes: { 1041: "Ignored" } } },
        { recid: "dashboard-1", schemaName: "Sales", w2ui: { changes: inheritedChanges } }
      ]
    });

    var result = await context.handler.Save();

    expect(result).toBeUndefined();
    expect(context.DialogHelper.alert).toHaveBeenCalledWith("There are no dashboard changes to save.", {
      title: "Dashboards"
    });
    expect(context.XrmTranslator.LockGrid).not.toHaveBeenCalled();
    expect(context.Helper.ExecuteTypedCustomAction).not.toHaveBeenCalled();
    expect(context.XrmTranslator.EnableLoadAndSave).not.toHaveBeenCalled();
  });

  it("saves display text clears, publishes, marks published, and reloads", async function () {
    var context = await setup({
      records: [
        {
          recid: "dashboard-1",
          schemaName: "Sales",
          w2ui: { changes: { 1033: "Sales updated", 1041: "", 1066: null } }
        },
        { recid: "dashboard-2", schemaName: "Service", w2ui: { changes: { 1041: "Service JA" } } }
      ],
      dashboards: []
    });

    var result = await context.handler.Save();

    expect(result).toEqual(createActionResult("Published", { dashboardIds: ["dashboard-1", "dashboard-2"] }));
    expect(
      context.state.customActionCalls.map(function (call) {
        return call.type;
      })
    ).toEqual(["Saving", "Publishing", "Published", "Loading"]);
    expect(getActionCalls(context, "Saving")[0].input).toEqual({
      dashboardUpdates: [
        {
          dashboardId: "dashboard-1",
          labels: [
            { LanguageCode: "1033", Label: "Sales updated" },
            { LanguageCode: "1041", Label: "" },
            { LanguageCode: "1066", Label: "" }
          ]
        },
        {
          dashboardId: "dashboard-2",
          labels: [{ LanguageCode: "1041", Label: "Service JA" }]
        }
      ]
    });
    expect(getActionCalls(context, "Publishing")[0].input).toEqual({
      dashboardIds: ["dashboard-1", "dashboard-2"]
    });
    expect(getActionCalls(context, "Published")[0].input).toEqual({
      dashboardIds: ["dashboard-1", "dashboard-2"]
    });
    expect(context.XrmTranslator.LockGrid).toHaveBeenCalledWith("Publishing ...");
    expect(context.XrmTranslator.LockGrid).toHaveBeenCalledWith("Published");
    expect(context.XrmTranslator.LockGrid).toHaveBeenCalledWith("Re-Loading ...");
    expect(context.XrmTranslator.LockGrid).not.toHaveBeenCalledWith("Loading ...");
    expect(context.XrmTranslator.EnableLoadAndSave).not.toHaveBeenCalled();
  });

  it("does not reload when publishing returns no dashboard ids", async function () {
    var context = await setup({
      records: [{ recid: "dashboard-1", schemaName: "Sales", w2ui: { changes: { 1041: "Sales JA" } } }],
      publishingDashboardIds: []
    });

    var result = await context.handler.Save();

    expect(result).toEqual(createActionResult("Publishing", { dashboardIds: [] }));
    expect(
      context.state.customActionCalls.map(function (call) {
        return call.type;
      })
    ).toEqual(["Saving", "Publishing"]);
    expect(getActionCalls(context, "Published")).toEqual([]);
    expect(getActionCalls(context, "Loading")).toEqual([]);
  });

  it("blocks empty base-language display text with row context", async function () {
    var context = await setup({
      baseLanguage: 1041,
      records: [{ recid: "dashboard-1", schemaName: "Sales", w2ui: { changes: { 1041: "" } } }]
    });

    expect(function () {
      context.handler.Save();
    }).toThrow("Display Text in the base language (Japanese (ja-jp) (1041)) cannot be empty.\nRow: Sales");

    context.state.records = [{ recid: "dashboard-1", schemaName: "", w2ui: { changes: { 1041: "" } } }];
    expect(function () {
      context.handler.Save();
    }).toThrow("Row: dashboard-1");

    context.state.records = [{ recid: "", schemaName: "", w2ui: { changes: { 1041: "" } } }];
    expect(function () {
      context.handler.Save();
    }).toThrow("Row: (unknown)");

    expect(context.Helper.ExecuteTypedCustomAction).not.toHaveBeenCalled();
  });

  it("allows display text clears when no base language is known", async function () {
    var context = await setup({
      baseLanguage: null,
      records: [{ recid: "dashboard-1", schemaName: "Sales", w2ui: { changes: { 1033: "" } } }]
    });

    await context.handler.Save();

    expect(getActionCalls(context, "Saving")[0].input.dashboardUpdates[0].labels).toEqual([
      { LanguageCode: "1033", Label: "" }
    ]);
  });

  it("stops after saving when saving output does not include dashboard ids", async function () {
    var context = await setup({
      records: [{ recid: "dashboard-1", schemaName: "Sales", w2ui: { changes: { 1041: "Sales JA" } } }],
      executeTypedCustomAction: function (functionName, type, input) {
        context.state.customActionCalls.push({ functionName: functionName, type: type, input: input });
        return Promise.resolve(createActionResult(type, {}));
      }
    });
    context.Helper.GetCustomActionObject.mockImplementationOnce(function () {
      return null;
    });

    var result = await context.handler.Save();

    expect(result).toEqual(createActionResult("Saving", {}));
    expect(
      context.state.customActionCalls.map(function (call) {
        return call.type;
      })
    ).toEqual(["Saving"]);
    expect(context.XrmTranslator.UnlockGrid).toHaveBeenCalledTimes(2);
  });

  it("passes save failures through after unlocking", async function () {
    var error = new Error("save failed");
    var context = await setup({
      records: [{ recid: "dashboard-1", schemaName: "Sales", w2ui: { changes: { 1041: "Updated" } } }],
      executeTypedCustomAction: function () {
        return Promise.reject(error);
      }
    });

    await expect(context.handler.Save()).rejects.toThrow(error);

    expect(context.XrmTranslator.UnlockGrid).toHaveBeenCalledOnce();
    expect(context.XrmTranslator.EnableLoadAndSave).not.toHaveBeenCalled();
  });
});
