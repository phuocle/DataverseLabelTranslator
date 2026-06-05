import { beforeEach, describe, expect, it, vi } from "vitest";

var importCounter = 0;

function createGrid() {
  var grid = {
    columns: [
      { field: "schemaName", text: "Schema Name" },
      { field: "1033", text: "English (en-us) (1033)" },
      { field: "1041", caption: "Japanese (ja-jp) (1041)" },
      { field: "1066", label: "Vietnamese (vi-vn) (1066)" }
    ],
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

function actionResult(type, object) {
  return { ok: true, type: type, object: object || {} };
}

function group(key, displayName, resources) {
  return { key: key, displayName: displayName, resources: resources };
}

function resource(overrides) {
  return Object.assign(
    {
      webresourceid: "wr-1033",
      lcid: "1033",
      content: { hello: "Hello", empty: "", nil: null }
    },
    overrides || {}
  );
}

function createHarness(options) {
  options = options || {};
  var grid = options.grid || createGrid();
  var state = {
    solution: Object.prototype.hasOwnProperty.call(options, "solution") ? options.solution : "solution-1",
    records: options.records || [],
    calls: []
  };

  var xrmTranslator = {
    baseLanguage: Object.prototype.hasOwnProperty.call(options, "baseLanguage") ? options.baseLanguage : 1033,
    metadata: {},
    GetGrid: vi.fn(function () {
      return grid;
    }),
    GetSolution: vi.fn(function () {
      return state.solution;
    }),
    SetMetadata: vi.fn(function (metadata) {
      xrmTranslator.metadata = metadata;
    }),
    GetMetadata: vi.fn(function () {
      return xrmTranslator.metadata;
    }),
    AddSummary: vi.fn(),
    LockGrid: vi.fn(),
    UnlockGrid: vi.fn(),
    errorHandler: vi.fn(),
    GetAllRecords: vi.fn(function () {
      return state.records;
    })
  };

  var helper = {
    CustomActionTypes: {
      Loading: "Loading",
      Saving: "Saving",
      Publishing: "Publishing",
      Published: "Published"
    },
    IsEmptyLabelValue: vi.fn(function (value) {
      return value == null || String(value).trim().length === 0;
    }),
    GetTranslator: vi.fn(function () {
      return xrmTranslator;
    }),
    GetPlaceholderDisplayText: vi.fn(function () {
      return "Add display text";
    }),
    GetPlaceholderDisplayTextBase: vi.fn(function () {
      return "Add display text (*)";
    }),
    GetPlaceholderReadonly: vi.fn(function () {
      return "-";
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
    ApplyPlaceholder: vi.fn(function (record, editablePlaceholder, basePlaceholder, app) {
      if (!editablePlaceholder) {
        return;
      }

      record._emptyEditablePlaceholder = editablePlaceholder;
      if (basePlaceholder && app.baseLanguage) {
        record._emptyEditablePlaceholders = record._emptyEditablePlaceholders || {};
        record._emptyEditablePlaceholders[String(app.baseLanguage)] = basePlaceholder;
      }
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

      if (!app.baseLanguage) {
        return;
      }

      var baseLanguage = String(app.baseLanguage);
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
      var field = String(languageCode);

      for (var i = 0; i < grid.columns.length; i++) {
        if (String(grid.columns[i].field) === field) {
          return grid.columns[i].text || grid.columns[i].caption || grid.columns[i].label || field;
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
          state.calls.push({ functionName: functionName, type: type, input: input });

          if (type === helper.CustomActionTypes.Loading) {
            return Promise.resolve(
              actionResult(type, {
                baseLanguage: options.loadedBaseLanguage,
                groups: Object.prototype.hasOwnProperty.call(options, "groups")
                  ? options.groups
                  : [
                      group("pl_/resources/messages.", "pl_/resources/messages.1033.js", [
                        resource(),
                        resource({
                          webresourceid: "wr-1041",
                          lcid: "1041",
                          content: { hello: "Konnichiwa", added: "Added", nil: undefined }
                        }),
                        resource({
                          webresourceid: "wr-empty-lcid",
                          lcid: "",
                          content: { ignored: "Ignored" }
                        })
                      ])
                    ]
              })
            );
          }

          if (type === helper.CustomActionTypes.Saving) {
            return Promise.resolve(
              actionResult(type, {
                webresourceIds: Object.prototype.hasOwnProperty.call(options, "savedIds") ? options.savedIds : []
              })
            );
          }

          if (type === helper.CustomActionTypes.Publishing) {
            return Promise.resolve(
              actionResult(type, {
                webresourceIds: Object.prototype.hasOwnProperty.call(options, "publishingIds")
                  ? options.publishingIds
                  : input.webresourceIds
              })
            );
          }

          if (type === helper.CustomActionTypes.Published) {
            return Promise.resolve(
              actionResult(type, {
                webresourceIds: Object.prototype.hasOwnProperty.call(options, "publishedIds")
                  ? options.publishedIds
                  : input.webresourceIds
              })
            );
          }

          return Promise.resolve(actionResult(type, {}));
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
  globalThis.XrmTranslator = xrmTranslator;
  globalThis.Helper = helper;
  globalThis.DialogHelper = {
    alert: vi.fn(function () {
      return Promise.resolve();
    })
  };
  globalThis.w2utils = {
    encodeTags: vi.fn(function (value) {
      return String(value).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    }),
    decodeTags: vi.fn(function (value) {
      return String(value == null ? "" : value)
        .replace(/&lt;/g, "<")
        .replace(/&gt;/g, ">")
        .replace(/&amp;/g, "&");
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

async function setup(options) {
  var harness = createHarness(options);
  await import("../js/Handler/WebResourceHandler.js?test=" + ++importCounter);
  return Object.assign({ handler: globalThis.window.WebResourceHandler }, harness);
}

function getCalls(context, type) {
  return context.state.calls.filter(function (call) {
    return call.type === type;
  });
}

beforeEach(function () {
  vi.restoreAllMocks();
  delete globalThis.window;
  delete globalThis.XrmTranslator;
  delete globalThis.Helper;
  delete globalThis.DialogHelper;
  delete globalThis.w2utils;
});

describe("WebResourceHandler.Load", function () {
  it("loads groups from the server action and fills parent and child rows", async function () {
    var context = await setup({ loadedBaseLanguage: 1033 });

    await context.handler.Load();

    expect(context.Helper.ExecuteTypedCustomAction).toHaveBeenCalledWith("WebResource", "Loading", {
      solutionId: "solution-1"
    });
    expect(context.XrmTranslator.metadata["pl_/resources/messages."].__displayName).toBe(
      "pl_/resources/messages.1033.js"
    );

    var parent = context.grid.add.mock.calls[0][0][0];
    expect(parent).toMatchObject({
      recid: "pl_/resources/messages.",
      schemaName: "pl_/resources/messages.1033.js",
      _emptyReadonlyPlaceholder: "-"
    });
    expect(parent.w2ui.editable).toBe(false);
    expect(parent.w2ui.children).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          recid: "pl_/resources/messages.|hello",
          schemaName: "hello",
          1033: "Hello",
          1041: "Konnichiwa",
          _emptyEditablePlaceholder: "Add display text"
        }),
        expect.objectContaining({ schemaName: "empty", 1033: "" }),
        expect.objectContaining({ schemaName: "nil", 1033: "", 1041: "" }),
        expect.objectContaining({ schemaName: "added", 1041: "Added" })
      ])
    );
    expect(parent.w2ui.children[0]._emptyEditablePlaceholders["1033"]).toBe("Add display text (*)");
    expect(context.XrmTranslator.AddSummary).toHaveBeenCalledWith(context.grid.add.mock.calls[0][0]);
    expect(context.XrmTranslator.UnlockGrid).toHaveBeenCalledOnce();
    expect(context.XrmTranslator.LockGrid).toHaveBeenCalledWith("Loading ...");
  });

  it("handles missing group collections and load errors", async function () {
    var emptyContext = await setup({ baseLanguage: null, groups: null });

    await emptyContext.handler.Load();

    expect(emptyContext.grid.add).toHaveBeenCalledWith([]);

    var fallbackContext = await setup({ groups: [group("empty-group", undefined, undefined)] });
    delete fallbackContext.XrmTranslator.SetMetadata;
    delete fallbackContext.XrmTranslator.GetMetadata;

    await fallbackContext.handler.Load();

    expect(fallbackContext.grid.add.mock.calls[0][0][0]).toMatchObject({
      recid: "empty-group",
      schemaName: "empty-group"
    });
    expect(fallbackContext.grid.add.mock.calls[0][0][0].w2ui.children).toEqual([]);

    var error = new Error("load failed");
    var failingContext = await setup({
      executeTypedCustomAction: function () {
        return Promise.reject(error);
      }
    });

    await failingContext.handler.Load();

    expect(failingContext.XrmTranslator.UnlockGrid).toHaveBeenCalledOnce();
    expect(failingContext.XrmTranslator.errorHandler).toHaveBeenCalledWith(error);
  });
});

describe("WebResourceHandler.Save", function () {
  it("saves changed existing and new localized resources", async function () {
    var inheritedChanges = Object.create({ 3082: "Hola" });
    inheritedChanges["1033"] = "Hello updated";
    inheritedChanges["1041"] = "Konnichiwa updated";
    inheritedChanges["1066"] = "Xin chao";

    var context = await setup({
      savedIds: ["wr-1033", "created-1066"],
      publishedIds: ["wr-1033", "created-1066"],
      groups: [
        group("pl_/resources/messages.", "pl_/resources/messages.1033.js", [
          resource(),
          resource({ webresourceid: "wr-1041", lcid: "1041", content: { hello: "Konnichiwa" } })
        ])
      ],
      records: [
        {
          recid: "pl_/resources/messages.|hello",
          schemaName: "hello",
          w2ui: { changes: inheritedChanges }
        },
        { recid: "pl_/resources/messages.", schemaName: "parent", w2ui: {} },
        { recid: "orphan|ignored", schemaName: "ignored" }
      ]
    });
    context.XrmTranslator.metadata = {
      "pl_/resources/messages.": [
        resource({ __lcid: "1033" }),
        resource({ webresourceid: "wr-1041", __lcid: "1041", content: { hello: "Konnichiwa" } })
      ]
    };
    context.XrmTranslator.metadata["pl_/resources/messages."].__displayName = "Messages";

    await context.handler.Save();

    var saveInput = getCalls(context, "Saving")[0].input;
    expect(saveInput.resourceChanges).toEqual([
      {
        webresourceid: "wr-1033",
        contentChanges: [{ key: "hello", value: "Hello updated" }]
      },
      {
        webresourceid: "wr-1041",
        contentChanges: [{ key: "hello", value: "Konnichiwa updated" }]
      },
      {
        webresourceid: null,
        lcid: "1066",
        baseWebresourceid: "wr-1033",
        contentChanges: [{ key: "hello", value: "Xin chao" }]
      }
    ]);
    expect(getCalls(context, "Publishing")[0].input).toEqual({ webresourceIds: ["wr-1033", "created-1066"] });
    expect(getCalls(context, "Published")[0].input).toEqual({ webresourceIds: ["wr-1033", "created-1066"] });
    expect(getCalls(context, "Loading")).toHaveLength(1);
    expect(context.XrmTranslator.LockGrid).toHaveBeenCalledWith("Saving ...");
    expect(context.XrmTranslator.LockGrid).toHaveBeenCalledWith("Publishing ...");
    expect(context.XrmTranslator.LockGrid).toHaveBeenCalledWith("Published");
    expect(context.XrmTranslator.LockGrid).toHaveBeenCalledWith("Re-Loading ...");
  });

  it("returns without publishing when the save result has no web resource ids", async function () {
    var context = await setup({
      records: [{ recid: "newgroup|title", schemaName: "title", w2ui: { changes: { 1041: "Title" } } }]
    });

    var result = await context.handler.Save();

    expect(context.Helper.GetCustomActionObject(result).webresourceIds).toEqual([]);
    expect(getCalls(context, "Publishing")).toHaveLength(0);
    expect(context.XrmTranslator.UnlockGrid).toHaveBeenCalledTimes(2);
    expect(getCalls(context, "Saving")[0].input.resourceChanges).toEqual([
      {
        webresourceid: null,
        lcid: "1041",
        baseWebresourceid: null,
        contentChanges: [{ key: "title", value: "Title" }]
      }
    ]);
  });

  it("shows a dialog and skips the server when there are no changes", async function () {
    var context = await setup({ records: [{ recid: "group", schemaName: "group", w2ui: {} }] });

    await context.handler.Save();

    expect(context.DialogHelper.alert).toHaveBeenCalledWith("There are no web resource changes to save.", {
      title: "Web Resources"
    });
    expect(getCalls(context, "Saving")).toHaveLength(0);
    expect(context.XrmTranslator.LockGrid).not.toHaveBeenCalled();
  });

  it("supports legacy app metadata fallback during save", async function () {
    var context = await setup({
      baseLanguage: null,
      records: [{ recid: "legacy|title", schemaName: "title", w2ui: { changes: { 1041: "Title" } } }]
    });
    delete context.XrmTranslator.GetMetadata;
    delete context.XrmTranslator.metadata;

    await context.handler.Save();

    expect(getCalls(context, "Saving")[0].input.resourceChanges).toEqual([
      {
        webresourceid: null,
        lcid: "1041",
        baseWebresourceid: null,
        contentChanges: [{ key: "title", value: "Title" }]
      }
    ]);
  });

  it("blocks empty base-language display text with the best row path", async function () {
    var context = await setup({
      records: [
        {
          recid: "pl_/resources/messages.|hello",
          schemaName: "hello",
          w2ui: { changes: { 1033: " " } }
        }
      ]
    });
    context.XrmTranslator.metadata = { "pl_/resources/messages.": [] };
    context.XrmTranslator.metadata["pl_/resources/messages."].__displayName = "Messages";

    expect(function () {
      context.handler.Save();
    }).toThrow("Display Text in the base language (English (en-us) (1033)) cannot be empty.\nRow: Messages > hello");

    expect(context.XrmTranslator.LockGrid).not.toHaveBeenCalled();
    expect(getCalls(context, "Saving")).toHaveLength(0);
  });

  it("uses fallback validation paths and unlocks when save fails", async function () {
    var noBaseContext = await setup({
      baseLanguage: null,
      executeTypedCustomAction: function (functionName, type, input) {
        noBaseContext.state.calls.push({ functionName: functionName, type: type, input: input });
        return Promise.resolve(actionResult(type, {}));
      },
      records: [{ recid: "row-without-separator", schemaName: "", w2ui: { changes: { 1033: "" } } }]
    });

    await noBaseContext.handler.Save();

    expect(getCalls(noBaseContext, "Saving")).toHaveLength(1);

    var missingLanguageContext = await setup({
      baseLanguage: 9999,
      records: [{ recid: "missing|name", schemaName: "", w2ui: { changes: { 9999: "" } } }]
    });

    expect(function () {
      missingLanguageContext.handler.Save();
    }).toThrow("Display Text in the base language (9999) cannot be empty.\nRow: missing");

    var nullSchemaContext = await setup({
      records: [{ recid: "null-schema", schemaName: null, w2ui: { changes: { 1033: "" } } }]
    });

    expect(function () {
      nullSchemaContext.handler.Save();
    }).toThrow("Display Text in the base language (English (en-us) (1033)) cannot be empty.\nRow: null-schema");

    var rowOnlyContext = await setup({
      records: [{ recid: "same", schemaName: "same", w2ui: { changes: { 1033: "" } } }]
    });

    expect(function () {
      rowOnlyContext.handler.Save();
    }).toThrow("Display Text in the base language (English (en-us) (1033)) cannot be empty.\nRow: same");

    var unknownContext = await setup({
      records: [{ recid: "", schemaName: "", w2ui: { changes: { 1033: "" } } }]
    });

    expect(function () {
      unknownContext.handler.Save();
    }).toThrow("Display Text in the base language (English (en-us) (1033)) cannot be empty.\nRow: (unknown)");

    var error = new Error("save failed");
    var failingContext = await setup({
      records: [{ recid: "failed|title", schemaName: "title", w2ui: { changes: { 1041: "Title" } } }],
      executeTypedCustomAction: function () {
        return Promise.reject(error);
      }
    });

    await expect(failingContext.handler.Save()).rejects.toThrow(error);
    expect(failingContext.XrmTranslator.UnlockGrid).toHaveBeenCalledOnce();
  });
});
