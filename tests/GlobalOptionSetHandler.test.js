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
  return Object.assign({
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
    Description: { LocalizedLabels: [{ LanguageCode: 1033, Label: "Parent desc" }] },
    "@odata.context": "context",
    "@odata.etag": "etag"
  }, overrides || {});
}

function createBatchRequest(options) {
  Object.assign(this, options);
}

function createHarness(options) {
  options = options || {};
  var grid = options.grid || createGrid();
  var state = {
    component: options.component || "DisplayName",
    solution: Object.prototype.hasOwnProperty.call(options, "solution") ? options.solution : "solution-1",
    metadataById: options.metadataById || {},
    records: options.records || [],
    changeSetRequests: [],
    capturedFlow: null
  };

  var xrmTranslator = {
    baseLanguage: Object.prototype.hasOwnProperty.call(options, "baseLanguage") ? options.baseLanguage : 1033,
    metadata: [],
    ComponentType: { OptionSet: 9 },
    GetComponent: vi.fn(function () { return state.component; }),
    GetGrid: vi.fn(function () { return grid; }),
    GetSolution: vi.fn(function () { return state.solution; }),
    AddSummary: vi.fn(),
    EnableLoadAndSave: vi.fn(),
    errorHandler: vi.fn(),
    GetAllRecords: vi.fn(function () { return state.records; }),
    GetAttributeById: vi.fn(function (id) { return state.metadataById[id] || null; }),
    GetCurrentToolbarTypeText: vi.fn(function () { return "18. Global Option Sets"; }),
    ExecuteChangeSetBatches: vi.fn(function (updates, batchOptions) {
      var requests = updates.map(function (update, index) {
        return batchOptions.buildRequest(update, { contentId: index + 1 });
      });
      state.changeSetRequests.push({ updates: updates, batchOptions: batchOptions, requests: requests });
      return Promise.resolve(requests);
    }),
    RunAsBaseLanguage: vi.fn(function (action) {
      return Promise.resolve().then(action);
    }),
    RunTypeSaveFlow: vi.fn(function (flow) {
      state.capturedFlow = flow;
      return Promise.resolve()
        .then(function () {
          return flow.saveAction();
        })
        .then(function (result) {
          if (flow.shouldPublish(result)) {
            return flow.publishAction(result).then(function () {
              return result;
            });
          }
          return result;
        });
    })
  };

  var webApiClient = {
    Promise: Promise,
    Retrieve: vi.fn(options.retrieve || function () {
      return Promise.resolve({ value: [{ objectid: "os1" }] });
    }),
    SendRequest: vi.fn(options.sendRequest || function () {
      return Promise.resolve(optionSet());
    }),
    GetApiUrl: vi.fn(function () { return "https://example.crm/api/data/v9.2/"; }),
    BatchRequest: createBatchRequest,
    Requests: {
      PublishXmlRequest: {
        with: vi.fn(function (request) {
          return { name: "PublishXml", payload: request.payload };
        })
      }
    },
    Execute: vi.fn(function (request) {
      return Promise.resolve({ executed: request });
    })
  };

  globalThis.window = {};
  if (options.preexistingHandler !== false) {
    globalThis.window.GlobalOptionSetHandler = {};
  }
  globalThis.XrmTranslator = xrmTranslator;
  globalThis.WebApiClient = webApiClient;

  return {
    grid: grid,
    state: state,
    XrmTranslator: xrmTranslator,
    WebApiClient: webApiClient
  };
}

async function loadHandler(harness) {
  await import("../js/GlobalOptionSetHandler.js?test=" + (++importCounter));
  return {
    handler: globalThis.window.GlobalOptionSetHandler,
    harness: harness
  };
}

async function setup(options) {
  var harness = createHarness(options);
  var loaded = await loadHandler(harness);
  return Object.assign(loaded, harness);
}

beforeEach(function () {
  vi.restoreAllMocks();
  delete globalThis.window;
  delete globalThis.XrmTranslator;
  delete globalThis.WebApiClient;
});

describe("GlobalOptionSetHandler.Load", function () {
  it("initializes the handler namespace when the window slot is missing", async function () {
    var context = await setup({ preexistingHandler: false, solution: null });

    await context.handler.Load();

    expect(globalThis.window.GlobalOptionSetHandler).toBe(context.handler);
    expect(context.grid.add).toHaveBeenCalledWith([]);
  });

  it("loads no option sets when the solution is all", async function () {
    var context = await setup({ solution: "all" });

    await context.handler.Load();

    expect(context.WebApiClient.Retrieve).not.toHaveBeenCalled();
    expect(context.WebApiClient.SendRequest).not.toHaveBeenCalled();
    expect(context.XrmTranslator.metadata).toEqual([]);
    expect(context.grid.add).toHaveBeenCalledWith([]);
    expect(context.grid.unlock).toHaveBeenCalledOnce();
    expect(context.XrmTranslator.EnableLoadAndSave).toHaveBeenCalledOnce();
  });

  it("retrieves, filters, sorts, and fills display text rows", async function () {
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
    var responses = [
      null,
      optionSet({ MetadataId: "bad1", Name: "bad1", IsCustomizable: { Value: false } }),
      optionSet({ MetadataId: "bad2", Name: "bad2", IsCustomizable: { Value: true }, IsGlobal: false }),
      JSON.stringify(validPicklist),
      validBoolean
    ];
    var context = await setup({
      component: "DisplayName",
      retrieve: function () {
        return Promise.resolve({
          value: responses.map(function (_, index) { return { objectid: "id" + index }; })
        });
      },
      sendRequest: function () {
        return Promise.resolve(responses.shift());
      }
    });

    await context.handler.Load();

    expect(context.WebApiClient.Retrieve).toHaveBeenCalledWith({
      entityName: "solutioncomponent",
      queryParams: "?$select=objectid&$filter=_solutionid_value eq solution-1 and componenttype eq 9"
    });
    expect(context.WebApiClient.SendRequest).toHaveBeenCalledTimes(5);
    expect(context.XrmTranslator.metadata.map(function (os) { return os.Name; })).toEqual(["pl_boolean", "pl_picklist"]);

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
  });

  it("fills description rows, including option sets without options", async function () {
    var context = await setup({
      component: "Description",
      retrieve: function () {
        return Promise.resolve({ value: [{ objectid: "empty" }, { objectid: "withoutDescription" }, { objectid: "withOption" }] });
      },
      sendRequest: function (_, url) {
        if (url.indexOf("empty") !== -1) {
          return Promise.resolve(optionSet({ MetadataId: "empty", Name: "pl_empty", Options: [] }));
        }
        if (url.indexOf("withoutDescription") !== -1) {
          return Promise.resolve(optionSet({ MetadataId: "withoutDescription", Name: "pl_without_description", Description: null, Options: undefined }));
        }
        return Promise.resolve(optionSet({ MetadataId: "withOption", Name: "pl_with_option" }));
      }
    });

    await context.handler.Load();

    var records = context.grid.add.mock.calls[0][0];
    var empty = records.find(function (record) { return record.schemaName === "pl_empty"; });
    var withoutDescription = records.find(function (record) { return record.schemaName === "pl_without_description"; });
    var withOption = records.find(function (record) { return record.schemaName === "pl_with_option"; });

    expect(records).toHaveLength(3);
    expect(empty._emptyEditablePlaceholder).toBe("Add description");
    expect(empty["1033"]).toBe("Parent desc");
    expect(empty.w2ui.editable).toBe(true);
    expect(withoutDescription._emptyEditablePlaceholder).toBe("Add description");
    expect(withoutDescription.w2ui.children).toEqual([]);
    expect(withOption.w2ui.children[0]._emptyEditablePlaceholder).toBe("Add description");
    expect(withOption.w2ui.children[0]["1041"]).toBe("Desc JA");
  });

  it("skips display text option sets that do not have option values", async function () {
    var context = await setup({
      component: "DisplayName",
      retrieve: function () {
        return Promise.resolve({ value: [{ objectid: "empty" }] });
      },
      sendRequest: function () {
        return Promise.resolve(optionSet({ MetadataId: "empty", Name: "pl_empty", Options: [] }));
      }
    });

    await context.handler.Load();

    expect(context.grid.add).toHaveBeenCalledWith([]);
  });

  it("fills fallback component rows without editable placeholders", async function () {
    var context = await setup({
      component: "Other",
      retrieve: function () {
        return Promise.resolve({ value: [{ objectid: "other" }] });
      },
      sendRequest: function () {
        return Promise.resolve(optionSet({
          MetadataId: "other",
          Name: "pl_other",
          Options: [
            {
              Value: 222220000,
              Other: { LocalizedLabels: [{ LanguageCode: 1033, Label: "Other text" }] }
            }
          ]
        }));
      }
    });

    await context.handler.Load();

    var record = context.grid.add.mock.calls[0][0][0];
    expect(record._emptyReadonlyPlaceholder).toBe("-");
    expect(record.w2ui.children[0]._emptyEditablePlaceholder).toBeUndefined();
    expect(record.w2ui.children[0]["1033"]).toBe("Other text");
  });

  it("covers fallback labels and names while filling rows", async function () {
    var context = await setup({
      component: "DisplayName",
      retrieve: function () {
        return Promise.resolve({ value: [{ objectid: "named" }, { objectid: "unnamed" }, { objectid: "boolean" }] });
      },
      sendRequest: function (_, url) {
        if (url.indexOf("unnamed") !== -1) {
          return Promise.resolve(optionSet({
            MetadataId: "unnamed",
            Name: undefined,
            Options: [
              { Value: 1 },
              { Value: 2, Label: {} }
            ]
          }));
        }
        if (url.indexOf("boolean") !== -1) {
          return Promise.resolve(optionSet({
            MetadataId: "boolean",
            Name: "pl_boolean_fallback",
            Options: undefined,
            TrueOption: { Value: 1 },
            FalseOption: { Value: 0, Label: {} }
          }));
        }
        return Promise.resolve(optionSet({ MetadataId: "named", Name: "pl_named", Options: [] }));
      }
    });

    await context.handler.Load();

    var records = context.grid.add.mock.calls[0][0];
    var unnamed = records.find(function (record) { return record.recid === "unnamed"; });
    var boolean = records.find(function (record) { return record.recid === "boolean"; });
    expect(unnamed.schemaName).toBeUndefined();
    expect(unnamed.w2ui.children[0]["1033"]).toBeUndefined();
    expect(unnamed.w2ui.children[1]["1033"]).toBeUndefined();
    expect(boolean.w2ui.children[0]["1033"]).toBeUndefined();
    expect(boolean.w2ui.children[1]["1033"]).toBeUndefined();
  });

  it("handles solutioncomponent responses without a value collection", async function () {
    var context = await setup({
      retrieve: function () {
        return Promise.resolve({});
      }
    });

    await context.handler.Load();

    expect(context.WebApiClient.SendRequest).not.toHaveBeenCalled();
    expect(context.grid.add).toHaveBeenCalledWith([]);
  });

  it("uses caption, label, field, and empty-column fallbacks in base-language errors", async function () {
    var option = optionSet({ MetadataId: "os1", Name: "pl_globalchoice" });
    var grid = createGrid();
    var context = await setup({
      component: "DisplayName",
      baseLanguage: 1041,
      grid: grid,
      metadataById: { os1: option },
      records: [
        { recid: "os1|1", schemaName: "1", w2ui: { changes: { "1041": "" } } }
      ]
    });

    await expect(context.handler.Save()).rejects.toThrow("Japanese (ja-jp) (1041)");

    context.XrmTranslator.baseLanguage = 1066;
    context.state.records = [{ recid: "os1|1", schemaName: "1", w2ui: { changes: { "1066": "" } } }];
    await expect(context.handler.Save()).rejects.toThrow("Vietnamese (vi-vn) (1066)");

    grid.columns.push({ field: "7777" });
    context.XrmTranslator.baseLanguage = 7777;
    context.state.records = [{ recid: "os1|1", schemaName: "1", w2ui: { changes: { "7777": "" } } }];
    await expect(context.handler.Save()).rejects.toThrow("base language (7777)");

    grid.columns = undefined;
    context.XrmTranslator.baseLanguage = 8888;
    context.state.records = [{ recid: "os1|1", schemaName: "1", w2ui: { changes: { "8888": "" } } }];
    await expect(context.handler.Save()).rejects.toThrow("base language (8888)");
  });

  it("fills description rows when localized label collections are omitted", async function () {
    var context = await setup({
      component: "Description",
      retrieve: function () {
        return Promise.resolve({ value: [{ objectid: "description" }] });
      },
      sendRequest: function () {
        return Promise.resolve(optionSet({
          MetadataId: "description",
          Name: "pl_description",
          Description: {},
          Options: []
        }));
      }
    });

    await context.handler.Load();

    var record = context.grid.add.mock.calls[0][0][0];
    expect(record["1033"]).toBeUndefined();
    expect(record._emptyEditablePlaceholder).toBe("Add description");
  });

  it("passes load failures to the shared error handler", async function () {
    var error = new Error("retrieve failed");
    var context = await setup({
      retrieve: function () {
        return Promise.reject(error);
      }
    });

    await context.handler.Load();

    expect(context.XrmTranslator.errorHandler).toHaveBeenCalledWith(error);
    expect(context.grid.add).not.toHaveBeenCalled();
  });
});

describe("GlobalOptionSetHandler.Save", function () {
  it("returns no option set names when no valid changes exist", async function () {
    var option = optionSet({ MetadataId: "os1", Name: "pl_globalchoice" });
    var inheritedChanges = Object.create({ "1041": "Inherited" });
    var context = await setup({
      component: "Description",
      metadataById: { os1: option },
      records: [
        {},
        { recid: "missing|222220000", schemaName: "222220000", w2ui: { changes: { "1041": "X" } } },
        { recid: "os1", schemaName: "pl_globalchoice", w2ui: { changes: inheritedChanges } },
        { recid: "os1|bad", schemaName: "not-a-number", w2ui: { changes: { "1041": "X" } } }
      ]
    });

    var result = await context.handler.Save();

    expect(result).toEqual({ optionSetNames: [] });
    expect(context.XrmTranslator.ExecuteChangeSetBatches).not.toHaveBeenCalled();
    expect(context.WebApiClient.Execute).not.toHaveBeenCalled();
  });

  it("saves display text clears for non-base languages and publishes once", async function () {
    var option = optionSet({ MetadataId: "os1", Name: "pl_globalchoice" });
    var context = await setup({
      component: "DisplayName",
      metadataById: { os1: option },
      records: [
        { recid: "os1|222220000", schemaName: "222220000", w2ui: { changes: { "1033": "A updated", "1041": "", "1066": null } } },
        { recid: "os1|222220001", schemaName: "222220001", w2ui: { changes: { "1041": "B updated" } } }
      ]
    });

    var result = await context.handler.Save();

    expect(result).toEqual({ optionSetNames: ["pl_globalchoice"] });
    expect(context.state.changeSetRequests).toHaveLength(1);
    expect(context.state.changeSetRequests[0].batchOptions.batchNamePrefix).toBe("batch_updateglobaloptionvalue");
    expect(context.state.changeSetRequests[0].updates).toEqual([
      {
        Value: 222220000,
        Label: {
          LocalizedLabels: [
            { LanguageCode: "1033", Label: "A updated" },
            { LanguageCode: "1041", Label: "" },
            { LanguageCode: "1066", Label: "" }
          ]
        },
        MergeLabels: true,
        OptionSetName: "pl_globalchoice"
      },
      {
        Value: 222220001,
        Label: { LocalizedLabels: [{ LanguageCode: "1041", Label: "B updated" }] },
        MergeLabels: true,
        OptionSetName: "pl_globalchoice"
      }
    ]);
    expect(context.state.changeSetRequests[0].requests[0]).toMatchObject({
      method: "POST",
      url: "https://example.crm/api/data/v9.2/UpdateOptionValue"
    });
    expect(context.WebApiClient.Requests.PublishXmlRequest.with).toHaveBeenCalledWith({
      payload: { ParameterXml: "<importexportxml><optionsets><optionset>pl_globalchoice</optionset></optionsets></importexportxml>" }
    });
    expect(context.WebApiClient.Execute).toHaveBeenCalledOnce();
    expect(context.XrmTranslator.RunAsBaseLanguage).toHaveBeenCalledOnce();
  });

  it("blocks empty base-language display text with row context", async function () {
    var option = optionSet({ MetadataId: "os1", Name: "pl_globalchoice" });
    var context = await setup({
      component: "DisplayName",
      metadataById: { os1: option },
      records: [
        { recid: "os1|222220000", schemaName: "222220000", w2ui: { changes: { "1033": "   " } } }
      ]
    });

    await expect(context.handler.Save()).rejects.toThrow(
      "Display Text in the base language (English (en-us) (1033)) cannot be empty.\nRow: pl_globalchoice > 222220000"
    );
    expect(context.XrmTranslator.ExecuteChangeSetBatches).not.toHaveBeenCalled();
  });

  it("reports fallback row context when display text base language is empty", async function () {
    var context = await setup({
      component: "DisplayName",
      baseLanguage: 9999,
      metadataById: {
        same: optionSet({ MetadataId: "same", Name: "pl_same" }),
        optionOnly: optionSet({ MetadataId: "optionOnly", Name: "pl_option_only" }),
        unknown: optionSet({ MetadataId: "unknown", Name: "" })
      },
      records: [
        { recid: "same", schemaName: "pl_same", w2ui: { changes: { "9999": "" } } },
        { recid: "optionOnly|abc", schemaName: null, w2ui: { changes: { "9999": "" } } },
        { recid: "unknown|abc", schemaName: null, w2ui: { changes: { "9999": "" } } }
      ]
    });

    await expect(context.handler.Save()).rejects.toThrow(
      "Display Text in the base language (9999) cannot be empty.\nRow: pl_same"
    );

    context.state.records.shift();
    await expect(context.handler.Save()).rejects.toThrow(
      "Display Text in the base language (9999) cannot be empty.\nRow: pl_option_only"
    );

    context.state.records.shift();
    await expect(context.handler.Save()).rejects.toThrow(
      "Display Text in the base language (9999) cannot be empty.\nRow: (unknown)"
    );
  });

  it("allows display text base-language clears when no base language is known", async function () {
    var option = optionSet({ MetadataId: "os1", Name: "pl_globalchoice" });
    var context = await setup({
      component: "DisplayName",
      baseLanguage: null,
      metadataById: { os1: option },
      records: [
        { recid: "os1|222220000", schemaName: "222220000", w2ui: { changes: { "1033": "" } } }
      ]
    });

    await context.handler.Save();

    expect(context.state.changeSetRequests[0].updates[0].Label.LocalizedLabels).toEqual([
      { LanguageCode: "1033", Label: "" }
    ]);
  });

  it("saves parent and option value descriptions, then publishes and reloads", async function () {
    var option = optionSet({ MetadataId: "os1", Name: "pl_globalchoice" });
    var context = await setup({
      component: "Description",
      metadataById: { os1: option },
      records: [
        { recid: "os1", schemaName: "pl_globalchoice", w2ui: { changes: { "1033": "", "1041": "Parent JA" } } },
        { recid: "os1|222220000", schemaName: "222220000", w2ui: { changes: { "1041": "" } } }
      ],
      retrieve: function () {
        return Promise.resolve({ value: [] });
      }
    });

    await context.handler.Save();

    expect(context.state.changeSetRequests).toHaveLength(2);
    var metadataBatch = context.state.changeSetRequests[0];
    var valueBatch = context.state.changeSetRequests[1];
    expect(metadataBatch.batchOptions.batchNamePrefix).toBe("batch_updateglobaloptionset");
    expect(metadataBatch.updates[0].payload.Description.LocalizedLabels).toEqual([
      { LanguageCode: "1033", Label: "" },
      { LanguageCode: "1041", Label: "Parent JA" }
    ]);
    expect(metadataBatch.updates[0].payload["@odata.context"]).toBeUndefined();
    expect(metadataBatch.updates[0].payload["@odata.etag"]).toBeUndefined();
    expect(metadataBatch.requests[0]).toMatchObject({
      method: "PUT",
      url: "https://example.crm/api/data/v9.2/GlobalOptionSetDefinitions(os1)",
      headers: [{ key: "MSCRM.MergeLabels", value: "true" }]
    });
    expect(valueBatch.updates[0]).toEqual({
      Value: 222220000,
      Description: { LocalizedLabels: [{ LanguageCode: "1041", Label: "" }] },
      MergeLabels: true,
      OptionSetName: "pl_globalchoice"
    });
    await context.state.capturedFlow.reloadAction();
    expect(context.grid.add).toHaveBeenCalledWith([]);
  });

  it("saves parent-only descriptions and publishes their option set", async function () {
    var option = optionSet({ MetadataId: "os1", Name: "pl_globalchoice" });
    var context = await setup({
      component: "Description",
      metadataById: { os1: option },
      records: [
        { recid: "os1", schemaName: "pl_globalchoice", w2ui: { changes: { "1041": "Parent JA" } } }
      ]
    });

    var result = await context.handler.Save();

    expect(result).toEqual({ optionSetNames: ["pl_globalchoice"] });
    expect(context.state.changeSetRequests).toHaveLength(1);
    expect(context.state.changeSetRequests[0].batchOptions.batchNamePrefix).toBe("batch_updateglobaloptionset");
    expect(context.WebApiClient.Requests.PublishXmlRequest.with).toHaveBeenCalledWith({
      payload: { ParameterXml: "<importexportxml><optionsets><optionset>pl_globalchoice</optionset></optionsets></importexportxml>" }
    });
  });

  it("creates missing description metadata while saving parent descriptions", async function () {
    var option = optionSet({ MetadataId: "os1", Name: "pl_globalchoice", Description: null });
    var context = await setup({
      component: "Description",
      metadataById: { os1: option },
      records: [
        { recid: "os1", schemaName: "pl_globalchoice", w2ui: { changes: { "1041": "Parent JA" } } }
      ]
    });

    await context.handler.Save();

    expect(context.state.changeSetRequests[0].updates[0].payload.Description.LocalizedLabels).toEqual([
      { LanguageCode: "1041", Label: "Parent JA" }
    ]);
  });

  it("exposes a no-op publish action for empty option set names", async function () {
    var context = await setup();
    context.XrmTranslator.RunTypeSaveFlow.mockImplementationOnce(function (flow) {
      context.state.capturedFlow = flow;
      return Promise.resolve("captured");
    });

    await context.handler.Save();
    var result = await context.state.capturedFlow.publishAction({ optionSetNames: [] });
    var missingResult = await context.state.capturedFlow.publishAction(null);

    expect(result).toBeUndefined();
    expect(missingResult).toBeUndefined();
    expect(context.XrmTranslator.RunAsBaseLanguage).not.toHaveBeenCalled();
    expect(context.state.capturedFlow.shouldPublish({ optionSetNames: [] })).toBe(false);
    expect(context.state.capturedFlow.shouldPublish({ optionSetNames: ["pl_globalchoice"] })).toBe(true);
  });

  it("ignores parent display text changes because the parent row is readonly", async function () {
    var option = optionSet({ MetadataId: "os1", Name: "pl_globalchoice" });
    var context = await setup({
      component: "DisplayName",
      metadataById: { os1: option },
      records: [
        { recid: "os1", schemaName: "pl_globalchoice", w2ui: { changes: { "1041": "Ignored parent label" } } }
      ]
    });

    var result = await context.handler.Save();

    expect(result).toEqual({ optionSetNames: [] });
    expect(context.XrmTranslator.ExecuteChangeSetBatches).not.toHaveBeenCalled();
  });

  it("ignores empty labels for components that do not allow clears", async function () {
    var option = optionSet({ MetadataId: "os1", Name: "pl_globalchoice" });
    var context = await setup({
      component: "Other",
      metadataById: { os1: option },
      records: [
        { recid: "os1|222220000", schemaName: "222220000", w2ui: { changes: { "1041": "" } } },
        { recid: "os1|222220002", schemaName: "222220002", w2ui: { changes: { "1041": null } } },
        { recid: "os1|222220001", schemaName: "222220001", w2ui: { changes: { "1041": "Other value" } } }
      ]
    });

    await context.handler.Save();

    expect(context.state.changeSetRequests[0].updates).toEqual([
      {
        Value: 222220001,
        Other: { LocalizedLabels: [{ LanguageCode: "1041", Label: "Other value" }] },
        MergeLabels: true,
        OptionSetName: "pl_globalchoice"
      }
    ]);
  });
});
