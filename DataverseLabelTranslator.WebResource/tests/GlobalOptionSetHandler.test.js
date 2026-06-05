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

function optionSet(overrides) {
  return Object.assign(
    {
      MetadataId: "os1",
      Name: "pl_globalchoice",
      IsCustomizable: { Value: true },
      IsGlobal: true,
      Options: [
        {
          Value: 222220000,
          Label: { LocalizedLabels: [{ LanguageCode: 1033, Label: "A" }] },
          Description: { LocalizedLabels: [{ LanguageCode: 1041, Label: "Desc JA" }] }
        },
        {
          Value: 222220001,
          Label: { LocalizedLabels: [{ LanguageCode: 1041, Label: "B" }] },
          Description: { LocalizedLabels: [] }
        }
      ],
      Description: { LocalizedLabels: [{ LanguageCode: 1033, Label: "Parent desc" }] }
    },
    overrides || {}
  );
}

function createActionResult(type, object) {
  return { ok: true, type: type, object: object || {} };
}

function getChangedOptionSetNames(input) {
  var optionSetNames = [];

  (input.optionValueUpdates || []).forEach(function (update) {
    if (optionSetNames.indexOf(update.optionSetName) === -1) {
      optionSetNames.push(update.optionSetName);
    }
  });
  (input.optionSetDescriptionUpdates || []).forEach(function (update) {
    if (optionSetNames.indexOf(update.optionSetName) === -1) {
      optionSetNames.push(update.optionSetName);
    }
  });

  return optionSetNames;
}

function createHarness(options) {
  options = options || {};
  var grid = options.grid || createGrid();
  var state = {
    component: options.component || "DisplayText",
    solution: Object.prototype.hasOwnProperty.call(options, "solution") ? options.solution : "solution-1",
    metadataById: options.metadataById || {},
    records: options.records || [],
    customActionCalls: []
  };

  var xrmTranslator = {
    baseLanguage: Object.prototype.hasOwnProperty.call(options, "baseLanguage") ? options.baseLanguage : 1033,
    metadata: [],
    GetComponent: vi.fn(function () {
      return state.component;
    }),
    IsDescriptionComponent: vi.fn(function () {
      return state.component === "Description";
    }),
    IsDisplayTextComponent: vi.fn(function () {
      return state.component === "DisplayText";
    }),
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
    EnableLoadAndSave: vi.fn(),
    LockGrid: vi.fn(),
    UnlockGrid: vi.fn(),
    errorHandler: vi.fn(),
    GetAllRecords: vi.fn(function () {
      return state.records;
    }),
    GetAttributeById: vi.fn(function (id) {
      return state.metadataById[id] || null;
    })
  };

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
    GetPlaceholderDescription: vi.fn(function () {
      return "Add description";
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
            return Promise.resolve(createActionResult(type, { optionSets: options.optionSets || [] }));
          }

          if (type === helper.CustomActionTypes.Saving) {
            return Promise.resolve(createActionResult(type, { optionSetNames: getChangedOptionSetNames(input) }));
          }

          if (type === helper.CustomActionTypes.Publishing) {
            var publishingNames = Object.prototype.hasOwnProperty.call(options, "publishingOptionSetNames")
              ? options.publishingOptionSetNames
              : input.optionSetNames;
            return Promise.resolve(createActionResult(type, { optionSetNames: publishingNames }));
          }

          if (type === helper.CustomActionTypes.Published) {
            var publishedNames = Object.prototype.hasOwnProperty.call(options, "publishedOptionSetNames")
              ? options.publishedOptionSetNames
              : input.optionSetNames;
            return Promise.resolve(createActionResult(type, { optionSetNames: publishedNames }));
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
    globalThis.window.GlobalOptionSetHandler = {};
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
  await import("../js/Handler/GlobalOptionSetHandler.js?test=" + ++importCounter);
  return globalThis.window.GlobalOptionSetHandler;
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

describe("GlobalOptionSetHandler.Load", function () {
  it("initializes the handler namespace when the window slot is missing", async function () {
    var context = await setup({ preexistingHandler: false, solution: null });

    await context.handler.Load();

    expect(globalThis.window.GlobalOptionSetHandler).toBe(context.handler);
    expect(context.Helper.ExecuteTypedCustomAction).toHaveBeenCalledWith("GlobalOptionSet", "Loading", {
      solutionId: null
    });
    expect(context.grid.add).toHaveBeenCalledWith([]);
  });

  it("loads, filters, sorts, and fills display text rows", async function () {
    var validBoolean = optionSet({
      MetadataId: "bool",
      Name: "pl_boolean",
      Options: undefined,
      Description: undefined,
      TrueOption: {
        Value: 1,
        Label: { LocalizedLabels: [{ LanguageCode: 1033, Label: "Yes" }] }
      },
      FalseOption: {
        Value: 0,
        Label: { LocalizedLabels: [{ LanguageCode: 1041, Label: "No JA" }] }
      }
    });
    var validPicklist = optionSet({ MetadataId: "pick", Name: "pl_picklist" });
    var context = await setup({
      component: "DisplayText",
      optionSets: [
        null,
        optionSet({ MetadataId: "bad1", Name: "bad1", IsCustomizable: { Value: false } }),
        optionSet({ MetadataId: "bad2", Name: "bad2", IsCustomizable: { Value: true }, IsGlobal: false }),
        validPicklist,
        validBoolean
      ]
    });

    var result = await context.handler.Load();

    expect(result.type).toBe("Loading");
    expect(
      context.XrmTranslator.metadata.map(function (os) {
        return os.Name;
      })
    ).toEqual(["pl_boolean", "pl_picklist"]);

    var records = context.grid.add.mock.calls[0][0];
    expect(records[0].schemaName).toBe("pl_boolean");
    expect(records[0]._emptyReadonlyPlaceholder).toBe("-");
    expect(records[0].w2ui.editable).toBe(false);
    expect(records[0].w2ui.children[0]).toMatchObject({
      recid: "bool|1",
      schemaName: "1",
      1033: "Yes",
      _emptyEditablePlaceholder: "Add display text"
    });
    expect(records[0].w2ui.children[0]._emptyEditablePlaceholders["1033"]).toBe("Add display text (*)");
    expect(records[1].schemaName).toBe("pl_picklist");
    expect(records[1].w2ui.children).toHaveLength(2);
    expect(context.XrmTranslator.UnlockGrid).toHaveBeenCalledOnce();
    expect(context.XrmTranslator.EnableLoadAndSave).not.toHaveBeenCalled();
  });

  it("fills description rows, including option sets without options or labels", async function () {
    var context = await setup({
      component: "Description",
      optionSets: [
        optionSet({ MetadataId: "empty", Name: "pl_empty", Options: [] }),
        optionSet({
          MetadataId: "withoutDescription",
          Name: "pl_without_description",
          Description: null,
          Options: undefined
        }),
        optionSet({ MetadataId: "withOption", Name: "pl_with_option" }),
        optionSet({ MetadataId: "descriptionObject", Name: "pl_description_object", Description: {}, Options: [] })
      ]
    });

    await context.handler.Load();

    var records = context.grid.add.mock.calls[0][0];
    var empty = records.find(function (record) {
      return record.schemaName === "pl_empty";
    });
    var withoutDescription = records.find(function (record) {
      return record.schemaName === "pl_without_description";
    });
    var withOption = records.find(function (record) {
      return record.schemaName === "pl_with_option";
    });
    var descriptionObject = records.find(function (record) {
      return record.schemaName === "pl_description_object";
    });

    expect(records).toHaveLength(4);
    expect(empty._emptyEditablePlaceholder).toBe("Add description");
    expect(empty["1033"]).toBe("Parent desc");
    expect(empty.w2ui.editable).toBe(true);
    expect(withoutDescription._emptyEditablePlaceholder).toBe("Add description");
    expect(withoutDescription.w2ui.children).toEqual([]);
    expect(withOption.w2ui.children[0]._emptyEditablePlaceholder).toBe("Add description");
    expect(withOption.w2ui.children[0]["1041"]).toBe("Desc JA");
    expect(descriptionObject["1033"]).toBeUndefined();
  });

  it("skips display text option sets that do not have option values", async function () {
    var context = await setup({
      component: "DisplayText",
      optionSets: [optionSet({ MetadataId: "empty", Name: "pl_empty", Options: [] })]
    });

    await context.handler.Load();

    expect(context.grid.add).toHaveBeenCalledWith([]);
  });

  it("fills fallback component rows without editable placeholders", async function () {
    var context = await setup({
      component: "Other",
      optionSets: [
        optionSet({
          MetadataId: "other",
          Name: "pl_other",
          Options: [
            {
              Value: 222220000,
              Other: { LocalizedLabels: [{ LanguageCode: 1033, Label: "Other text" }] }
            },
            { Value: 222220001 }
          ]
        })
      ]
    });

    await context.handler.Load();

    var record = context.grid.add.mock.calls[0][0][0];
    expect(record._emptyReadonlyPlaceholder).toBe("-");
    expect(record.w2ui.children[0]._emptyEditablePlaceholder).toBeUndefined();
    expect(record.w2ui.children[0]["1033"]).toBeUndefined();
    expect(record.w2ui.children[1]["1033"]).toBeUndefined();
  });

  it("covers fallback labels and names while filling rows", async function () {
    var context = await setup({
      component: "DisplayText",
      optionSets: [
        optionSet({ MetadataId: "unnamed", Name: undefined, Options: [{ Value: 1 }, { Value: 2, Label: {} }] }),
        optionSet({
          MetadataId: "boolean",
          Name: "pl_boolean_fallback",
          Options: undefined,
          TrueOption: { Value: 1 },
          FalseOption: { Value: 0, Label: {} }
        })
      ]
    });

    await context.handler.Load();

    var records = context.grid.add.mock.calls[0][0];
    var unnamed = records.find(function (record) {
      return record.recid === "unnamed";
    });
    var boolean = records.find(function (record) {
      return record.recid === "boolean";
    });
    expect(unnamed.schemaName).toBeUndefined();
    expect(unnamed.w2ui.children[0]["1033"]).toBeUndefined();
    expect(unnamed.w2ui.children[1]["1033"]).toBeUndefined();
    expect(boolean.w2ui.children[0]["1033"]).toBeUndefined();
    expect(boolean.w2ui.children[1]["1033"]).toBeUndefined();
  });

  it("uses empty loading output and sort-name fallbacks", async function () {
    var context = await setup({
      executeTypedCustomAction: function (functionName, type, input) {
        context.state.customActionCalls.push({ functionName: functionName, type: type, input: input });
        return Promise.resolve(createActionResult(type, {}));
      }
    });

    await context.handler.Load();

    expect(context.grid.add).toHaveBeenCalledWith([]);

    context = await setup({
      component: "DisplayText",
      optionSets: [
        optionSet({ MetadataId: "emptyNameA", Name: null }),
        optionSet({ MetadataId: "emptyNameB", Name: undefined })
      ]
    });

    await context.handler.Load();

    expect(
      context.XrmTranslator.metadata.map(function (os) {
        return os.MetadataId;
      })
    ).toEqual(["emptyNameA", "emptyNameB"]);
  });

  it("passes load failures to the shared error handler", async function () {
    var error = new Error("load failed");
    var context = await setup({
      executeTypedCustomAction: function () {
        return Promise.reject(error);
      }
    });

    await context.handler.Load();

    expect(context.XrmTranslator.UnlockGrid).toHaveBeenCalledOnce();
    expect(context.XrmTranslator.errorHandler).toHaveBeenCalledWith(error);
    expect(context.grid.add).not.toHaveBeenCalled();
  });
});

describe("GlobalOptionSetHandler.Save", function () {
  it("shows a dialog and skips saving when no valid changes exist", async function () {
    var option = optionSet({ MetadataId: "os1", Name: "pl_globalchoice" });
    var inheritedChanges = Object.create({ 1041: "Inherited" });
    var context = await setup({
      component: "Description",
      metadataById: { os1: option },
      records: [
        {},
        { recid: "missing|222220000", schemaName: "222220000", w2ui: { changes: { 1041: "X" } } },
        { recid: "os1", schemaName: "pl_globalchoice", w2ui: { changes: inheritedChanges } },
        { recid: "os1|bad", schemaName: "not-a-number", w2ui: { changes: { 1041: "X" } } }
      ]
    });

    var result = await context.handler.Save();

    expect(result).toBeUndefined();
    expect(context.DialogHelper.alert).toHaveBeenCalledWith("There are no global option set changes to save.", {
      title: "Global Option Sets"
    });
    expect(context.XrmTranslator.LockGrid).not.toHaveBeenCalled();
    expect(context.Helper.ExecuteTypedCustomAction).not.toHaveBeenCalled();
    expect(context.XrmTranslator.EnableLoadAndSave).not.toHaveBeenCalled();
  });

  it("stops when saving output does not include option set names", async function () {
    var context = await setup({
      component: "Description",
      metadataById: { os1: optionSet({ MetadataId: "os1", Name: "pl_globalchoice" }) },
      records: [{ recid: "os1", schemaName: "pl_globalchoice", w2ui: { changes: { 1041: "Parent JA" } } }],
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
    expect(context.XrmTranslator.EnableLoadAndSave).not.toHaveBeenCalled();
  });

  it("saves display text clears, publishes, marks published, and reloads", async function () {
    var option = optionSet({ MetadataId: "os1", Name: "pl_globalchoice" });
    var context = await setup({
      component: "DisplayText",
      metadataById: { os1: option },
      records: [
        {
          recid: "os1|222220000",
          schemaName: "222220000",
          w2ui: { changes: { 1033: "A updated", 1041: "", 1066: null } }
        },
        { recid: "os1|222220001", schemaName: "222220001", w2ui: { changes: { 1041: "B updated" } } }
      ],
      optionSets: []
    });

    var result = await context.handler.Save();

    expect(result).toEqual(createActionResult("Published", { optionSetNames: ["pl_globalchoice"] }));
    expect(
      context.state.customActionCalls.map(function (call) {
        return call.type;
      })
    ).toEqual(["Saving", "Publishing", "Published", "Loading"]);
    expect(getActionCalls(context, "Saving")[0].input).toEqual({
      component: "DisplayText",
      optionValueUpdates: [
        {
          optionSetName: "pl_globalchoice",
          value: 222220000,
          labels: [
            { LanguageCode: "1033", Label: "A updated" },
            { LanguageCode: "1041", Label: "" },
            { LanguageCode: "1066", Label: "" }
          ]
        },
        {
          optionSetName: "pl_globalchoice",
          value: 222220001,
          labels: [{ LanguageCode: "1041", Label: "B updated" }]
        }
      ],
      optionSetDescriptionUpdates: []
    });
    expect(getActionCalls(context, "Publishing")[0].input).toEqual({ optionSetNames: ["pl_globalchoice"] });
    expect(getActionCalls(context, "Published")[0].input).toEqual({ optionSetNames: ["pl_globalchoice"] });
    expect(context.XrmTranslator.LockGrid).toHaveBeenCalledWith("Publishing ...");
    expect(context.XrmTranslator.LockGrid).toHaveBeenCalledWith("Published");
    expect(context.XrmTranslator.LockGrid).toHaveBeenCalledWith("Re-Loading ...");
    expect(context.XrmTranslator.LockGrid).not.toHaveBeenCalledWith("Loading ...");
    expect(context.XrmTranslator.EnableLoadAndSave).not.toHaveBeenCalled();
  });

  it("does not reload when publishing returns no option set names", async function () {
    var option = optionSet({ MetadataId: "os1", Name: "pl_globalchoice" });
    var context = await setup({
      component: "Description",
      metadataById: { os1: option },
      records: [{ recid: "os1", schemaName: "pl_globalchoice", w2ui: { changes: { 1041: "Parent JA" } } }],
      publishingOptionSetNames: []
    });

    var result = await context.handler.Save();

    expect(result).toEqual(createActionResult("Publishing", { optionSetNames: [] }));
    expect(
      context.state.customActionCalls.map(function (call) {
        return call.type;
      })
    ).toEqual(["Saving", "Publishing"]);
    expect(getActionCalls(context, "Published")).toEqual([]);
    expect(getActionCalls(context, "Loading")).toEqual([]);
  });

  it("saves parent and option value descriptions", async function () {
    var option = optionSet({ MetadataId: "os1", Name: "pl_globalchoice" });
    var context = await setup({
      component: "Description",
      metadataById: { os1: option },
      records: [
        { recid: "os1", schemaName: "pl_globalchoice", w2ui: { changes: { 1033: "", 1041: "Parent JA" } } },
        { recid: "os1|222220000", schemaName: "222220000", w2ui: { changes: { 1041: "" } } }
      ]
    });

    await context.handler.Save();

    expect(getActionCalls(context, "Saving")[0].input).toEqual({
      component: "Description",
      optionValueUpdates: [
        {
          optionSetName: "pl_globalchoice",
          value: 222220000,
          labels: [{ LanguageCode: "1041", Label: "" }]
        }
      ],
      optionSetDescriptionUpdates: [
        {
          optionSetName: "pl_globalchoice",
          labels: [
            { LanguageCode: "1033", Label: "" },
            { LanguageCode: "1041", Label: "Parent JA" }
          ]
        }
      ]
    });
  });

  it("blocks empty base-language display text with language and row context", async function () {
    var option = optionSet({ MetadataId: "os1", Name: "pl_globalchoice" });
    var grid = createGrid();
    var context = await setup({
      component: "DisplayText",
      baseLanguage: 1041,
      grid: grid,
      metadataById: { os1: option },
      records: [{ recid: "os1|1", schemaName: "1", w2ui: { changes: { 1041: "" } } }]
    });

    expect(function () {
      context.handler.Save();
    }).toThrow(
      "Display Text in the base language (Japanese (ja-jp) (1041)) cannot be empty.\nRow: pl_globalchoice > 1"
    );

    context.XrmTranslator.baseLanguage = 1066;
    context.state.records = [{ recid: "os1|1", schemaName: "1", w2ui: { changes: { 1066: "" } } }];
    expect(function () {
      context.handler.Save();
    }).toThrow("Vietnamese (vi-vn) (1066)");

    grid.columns.push({ field: "7777" });
    context.XrmTranslator.baseLanguage = 7777;
    context.state.records = [{ recid: "os1|1", schemaName: "1", w2ui: { changes: { 7777: "" } } }];
    expect(function () {
      context.handler.Save();
    }).toThrow("base language (7777)");

    grid.columns = undefined;
    context.XrmTranslator.baseLanguage = 8888;
    context.state.records = [{ recid: "os1|1", schemaName: "1", w2ui: { changes: { 8888: "" } } }];
    expect(function () {
      context.handler.Save();
    }).toThrow("base language (8888)");

    expect(context.Helper.ExecuteTypedCustomAction).not.toHaveBeenCalled();
  });

  it("reports fallback row context when display text base language is empty", async function () {
    var context = await setup({
      component: "DisplayText",
      baseLanguage: 9999,
      metadataById: {
        same: optionSet({ MetadataId: "same", Name: "pl_same" }),
        optionOnly: optionSet({ MetadataId: "optionOnly", Name: "pl_option_only" }),
        unknown: optionSet({ MetadataId: "unknown", Name: "" })
      },
      records: [
        { recid: "same", schemaName: "pl_same", w2ui: { changes: { 9999: "" } } },
        { recid: "optionOnly|abc", schemaName: null, w2ui: { changes: { 9999: "" } } },
        { recid: "unknown|abc", schemaName: null, w2ui: { changes: { 9999: "" } } }
      ]
    });

    expect(function () {
      context.handler.Save();
    }).toThrow("Display Text in the base language (9999) cannot be empty.\nRow: pl_same");

    context.state.records.shift();
    expect(function () {
      context.handler.Save();
    }).toThrow("Display Text in the base language (9999) cannot be empty.\nRow: pl_option_only");

    context.state.records.shift();
    expect(function () {
      context.handler.Save();
    }).toThrow("Display Text in the base language (9999) cannot be empty.\nRow: (unknown)");
  });

  it("allows display text base-language clears when no base language is known", async function () {
    var option = optionSet({ MetadataId: "os1", Name: "pl_globalchoice" });
    var context = await setup({
      component: "DisplayText",
      baseLanguage: null,
      metadataById: { os1: option },
      records: [{ recid: "os1|222220000", schemaName: "222220000", w2ui: { changes: { 1033: "" } } }]
    });

    await context.handler.Save();

    expect(getActionCalls(context, "Saving")[0].input.optionValueUpdates[0].labels).toEqual([
      { LanguageCode: "1033", Label: "" }
    ]);
  });

  it("ignores parent display text changes because the parent row is readonly", async function () {
    var option = optionSet({ MetadataId: "os1", Name: "pl_globalchoice" });
    var context = await setup({
      component: "DisplayText",
      metadataById: { os1: option },
      records: [{ recid: "os1", schemaName: "pl_globalchoice", w2ui: { changes: { 1041: "Ignored parent label" } } }]
    });

    await context.handler.Save();

    expect(context.DialogHelper.alert).toHaveBeenCalledWith("There are no global option set changes to save.", {
      title: "Global Option Sets"
    });
    expect(getActionCalls(context, "Saving")).toEqual([]);
  });

  it("ignores empty labels for components that do not allow clears", async function () {
    var option = optionSet({ MetadataId: "os1", Name: "pl_globalchoice" });
    var context = await setup({
      component: "Other",
      metadataById: { os1: option },
      records: [
        { recid: "os1|222220000", schemaName: "222220000", w2ui: { changes: { 1041: "" } } },
        { recid: "os1|222220002", schemaName: "222220002", w2ui: { changes: { 1041: null } } },
        { recid: "os1|222220001", schemaName: "222220001", w2ui: { changes: { 1041: "Other value" } } }
      ]
    });

    await context.handler.Save();

    expect(getActionCalls(context, "Saving")[0].input.optionValueUpdates).toEqual([
      {
        optionSetName: "pl_globalchoice",
        value: 222220001,
        labels: [{ LanguageCode: "1041", Label: "Other value" }]
      }
    ]);
  });

  it("passes save failures through after unlocking", async function () {
    var error = new Error("save failed");
    var context = await setup({
      metadataById: { os1: optionSet({ MetadataId: "os1", Name: "pl_globalchoice" }) },
      records: [{ recid: "os1|222220001", schemaName: "222220001", w2ui: { changes: { 1041: "Updated" } } }],
      executeTypedCustomAction: function () {
        return Promise.reject(error);
      }
    });

    await expect(context.handler.Save()).rejects.toThrow(error);

    expect(context.XrmTranslator.UnlockGrid).toHaveBeenCalledOnce();
    expect(context.XrmTranslator.EnableLoadAndSave).not.toHaveBeenCalled();
  });
});
