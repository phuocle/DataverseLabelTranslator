import { beforeEach, describe, expect, it, vi } from "vitest";

var importCounter = 0;

function encode(text) {
  return Buffer.from(String(text), "utf8").toString("base64");
}

function decode(text) {
  return Buffer.from(String(text || ""), "base64").toString("utf8");
}

function jsonContent(content) {
  return encode(JSON.stringify(content));
}

function resxContent(values) {
  var data = Object.keys(values)
    .map(function (key) {
      return '<data name="' + key + '" xml:space="preserve"><value>' + values[key] + "</value></data>";
    })
    .join("");

  return encode("<root>" + data + "</root>");
}

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

function createWebResource(overrides) {
  return Object.assign(
    {
      webresourceid: "wr-1033",
      name: "pl_/resources/messages.1033.js",
      displayname: "Messages 1033",
      content: jsonContent({ hello: "Hello", empty: "" }),
      webresourcetype: 3
    },
    overrides || {}
  );
}

function createXmlNode(name, text) {
  var attributes = {};
  var children = [];
  return {
    nodeName: name,
    textContent: text || "",
    setAttribute: function (attributeName, value) {
      attributes[attributeName] = value;
    },
    getAttribute: function (attributeName) {
      return attributes[attributeName] || null;
    },
    appendChild: function (child) {
      children.push(child);
      this.textContent = child.textContent;
    },
    getElementsByTagName: function (tagName) {
      return children.filter(function (child) {
        return child.nodeName === tagName;
      });
    },
    _attributes: attributes,
    _children: children
  };
}

function installXmlFakes() {
  globalThis.DOMParser = function () {};
  globalThis.DOMParser.prototype.parseFromString = function (xml) {
    var dataNodes = [];
    var dataRegex = /<data\b([^>]*)>([\s\S]*?)<\/data>/g;
    var match;

    while ((match = dataRegex.exec(xml))) {
      var nameMatch = /name="([^"]+)"/.exec(match[1]);
      var valueMatch = /<value>([\s\S]*?)<\/value>/.exec(match[2]);
      var dataNode = createXmlNode("data");
      dataNode.setAttribute("name", nameMatch ? nameMatch[1] : "");
      if (valueMatch) {
        dataNode.appendChild(createXmlNode("value", valueMatch[1]));
      }
      dataNodes.push(dataNode);
    }

    var root = {
      appendChild: function (child) {
        dataNodes.push(child);
      }
    };

    return {
      documentElement: root,
      createElement: function (name) {
        return createXmlNode(name);
      },
      getElementsByTagName: function (name) {
        if (name === "parsererror") {
          return xml.indexOf("<parsererror") !== -1 ? [{}] : [];
        }

        if (name === "data") {
          return dataNodes;
        }

        if (name === "value") {
          return [];
        }

        return [];
      },
      _dataNodes: dataNodes
    };
  };

  globalThis.XMLSerializer = function () {};
  globalThis.XMLSerializer.prototype.serializeToString = function (doc) {
    return (
      "<root>" +
      doc._dataNodes
        .map(function (node) {
          var name = node.getAttribute("name");
          var valueNode = node.getElementsByTagName ? node.getElementsByTagName("value")[0] : node._children[0];
          var value = valueNode ? valueNode.textContent || "" : "";
          return '<data name="' + name + '" xml:space="preserve"><value>' + value + "</value></data>";
        })
        .join("") +
      "</root>"
    );
  };
}

function createHarness(options) {
  options = options || {};
  var grid = options.grid || createGrid();
  var state = {
    solution: Object.prototype.hasOwnProperty.call(options, "solution") ? options.solution : "solution-1",
    records: options.records || [],
    changeSetRequests: [],
    updateRequests: [],
    createRequests: [],
    capturedFlow: null
  };

  var xrmTranslator = {
    baseLanguage: Object.prototype.hasOwnProperty.call(options, "baseLanguage") ? options.baseLanguage : 1033,
    metadata: {},
    ComponentType: { WebResource: 61 },
    GetGrid: vi.fn(function () {
      return grid;
    }),
    GetSolution: vi.fn(function () {
      return state.solution;
    }),
    GetBaseLanguage: vi.fn(function () {
      return Promise.resolve(xrmTranslator.baseLanguage);
    }),
    AddSummary: vi.fn(),
    EnableLoadAndSave: vi.fn(),
    errorHandler: vi.fn(),
    GetAllRecords: vi.fn(function () {
      return state.records;
    }),
    GetCurrentToolbarTypeText: vi.fn(function () {
      return "17. Web Resources";
    }),
    ExecuteChangeSetBatches: vi.fn(function (updates, batchOptions) {
      var responses = updates.map(function (update, index) {
        batchOptions.buildRequest(update, { contentId: index + 1 });
        return {
          contentId: index + 1,
          headers: { "OData-EntityId": "https://example.crm/api/data/v9.2/webresourceset(created-" + index + ")" }
        };
      });
      state.changeSetRequests.push({ updates: updates, batchOptions: batchOptions });
      return Promise.resolve(responses);
    }),
    PublishWebResources: vi.fn(function (ids) {
      return Promise.resolve({ ids: ids });
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
              return flow.reloadAction(result);
            });
          }
          return result;
        });
    })
  };

  var webApiClient = {
    Promise: Promise,
    Retrieve: vi.fn(
      options.retrieve ||
        function () {
          return Promise.resolve({ value: [] });
        }
    ),
    Update: vi.fn(function (request) {
      state.updateRequests.push(request);
      return { type: "update", request: request };
    }),
    Create: vi.fn(function (request) {
      state.createRequests.push(request);
      return { type: "create", request: request };
    })
  };

  globalThis.window = {};
  globalThis.XrmTranslator = xrmTranslator;
  globalThis.WebApiClient = webApiClient;
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
  globalThis.atob = function (text) {
    return Buffer.from(String(text || ""), "base64").toString("binary");
  };
  globalThis.btoa = function (text) {
    return Buffer.from(String(text), "binary").toString("base64");
  };
  installXmlFakes();

  return {
    grid: grid,
    state: state,
    XrmTranslator: xrmTranslator,
    WebApiClient: webApiClient
  };
}

async function setup(options) {
  var harness = createHarness(options);
  await import("../js/Handler/WebResourceHandler.js?test=" + ++importCounter);
  return Object.assign({ handler: globalThis.window.WebResourceHandler }, harness);
}

beforeEach(function () {
  vi.restoreAllMocks();
  delete globalThis.window;
  delete globalThis.XrmTranslator;
  delete globalThis.WebApiClient;
  delete globalThis.w2utils;
  delete globalThis.DOMParser;
  delete globalThis.XMLSerializer;
});

describe("WebResourceHandler.Load", function () {
  it("loads JSON and RESX resources, groups localized rows, and applies display text placeholders", async function () {
    var baseJson = createWebResource();
    var baseResx = createWebResource({
      webresourceid: "resx-1033",
      name: "",
      displayname: "pl_/resources/labels.1033.resx",
      content: resxContent({ title: "Title", blank: "" }),
      webresourcetype: 12
    });
    var context = await setup({
      solution: null,
      retrieve: function (request) {
        if (request.entityName === "solutioncomponent") {
          throw new Error("solutioncomponent should not be queried without a solution");
        }

        if (request.queryParams.indexOf("contains(name, '1033')") !== -1) {
          return Promise.resolve({
            value: [
              baseJson,
              baseResx,
              createWebResource({ name: "pl_/resources/ignore.txt", webresourcetype: 3 }),
              createWebResource({ name: "pl_/resources/notbase.1041.js", webresourceid: "wr-1041" })
            ]
          });
        }

        if (request.queryParams.indexOf("messages.") !== -1) {
          return Promise.resolve({
            value: [
              baseJson,
              createWebResource({
                webresourceid: "wr-1041",
                name: "pl_/resources/messages.1041.js",
                displayname: "Messages 1041",
                content: jsonContent({ hello: "こんにちは", added: "Added" }),
                webresourcetype: 3
              }),
              createWebResource({
                webresourceid: "bad-json",
                name: "pl_/resources/messages.1066.js",
                content: encode("{bad"),
                webresourcetype: 3
              }),
              createWebResource({
                webresourceid: "bad-resx",
                name: "pl_/resources/messages.3082.resx",
                content: encode("<parsererror></parsererror>"),
                webresourcetype: 12
              })
            ]
          });
        }

        return Promise.resolve({
          value: [
            baseResx,
            createWebResource({
              webresourceid: "resx-1041",
              name: "pl_/resources/labels.1041.resx",
              displayname: "pl_/resources/labels.1041.resx",
              content: resxContent({ title: "タイトル" }),
              webresourcetype: 12
            })
          ]
        });
      }
    });

    await context.handler.Load();

    expect(context.XrmTranslator.metadata["pl_/resources/messages."]).toBeDefined();
    expect(context.XrmTranslator.metadata["pl_/resources/labels."]).toBeDefined();

    var records = context.grid.add.mock.calls[0][0];
    var jsonParent = records.find(function (record) {
      return record.recid === "pl_/resources/messages.";
    });
    var resxParent = records.find(function (record) {
      return record.recid === "pl_/resources/labels.";
    });

    expect(jsonParent.schemaName).toBe("pl_/resources/messages.1033.js");
    expect(jsonParent._emptyReadonlyPlaceholder).toBe("-");
    expect(jsonParent.w2ui.editable).toBe(false);
    expect(jsonParent.w2ui.children).toEqual(
      expect.arrayContaining([
        expect.objectContaining({
          recid: "pl_/resources/messages.|hello",
          schemaName: "hello",
          1033: "Hello",
          1041: "こんにちは",
          _emptyEditablePlaceholder: "Add display text"
        }),
        expect.objectContaining({
          recid: "pl_/resources/messages.|empty",
          schemaName: "empty",
          1033: ""
        }),
        expect.objectContaining({
          recid: "pl_/resources/messages.|added",
          schemaName: "added",
          1041: "Added"
        })
      ])
    );
    expect(jsonParent.w2ui.children[0]._emptyEditablePlaceholders["1033"]).toBe("Add display text (*)");
    expect(resxParent.w2ui.children).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ schemaName: "title", 1033: "Title", 1041: "タイトル" }),
        expect.objectContaining({ schemaName: "blank", 1033: "" })
      ])
    );
    expect(context.grid.unlock).toHaveBeenCalledOnce();
    expect(context.XrmTranslator.EnableLoadAndSave).toHaveBeenCalledOnce();
  });

  it("uses solution membership, filters base resources, and escapes OData strings", async function () {
    var context = await setup({
      solution: "solution-1",
      retrieve: function (request) {
        if (request.entityName === "solutioncomponent") {
          return Promise.resolve({ value: [{ objectid: "id-1" }, { objectid: "id-2" }] });
        }

        if (request.entityId === "id-1") {
          return Promise.resolve(
            createWebResource({
              webresourceid: "id-1",
              name: "publisher's/messages.1033.js",
              displayname: null,
              content: jsonContent({ hello: "Hello" })
            })
          );
        }

        if (request.entityId === "id-2") {
          return Promise.resolve(createWebResource({ webresourceid: "id-2", name: "other.1041.js" }));
        }

        expect(request.queryParams).toContain("publisher''s/messages.");
        return Promise.resolve({
          value: [
            createWebResource({
              webresourceid: "id-1",
              name: "publisher's/messages.1033.js",
              displayname: null,
              content: jsonContent({ hello: "Hello" })
            })
          ]
        });
      }
    });

    await context.handler.Load();

    expect(context.WebApiClient.Retrieve).toHaveBeenCalledWith({
      entityName: "solutioncomponent",
      queryParams: "?$select=objectid&$filter=_solutionid_value eq solution-1 and componenttype eq 61"
    });
    expect(context.grid.add.mock.calls[0][0][0].recid).toBe("publisher's/messages.");
  });

  it("supports numeric RESX names and fallback display names", async function () {
    var context = await setup({
      solution: null,
      retrieve: function (request) {
        if (request.queryParams.indexOf("contains(name, '1033')") !== -1) {
          return Promise.resolve({
            value: [
              createWebResource({
                webresourceid: "numeric-resx",
                name: "1033",
                displayname: "",
                content: resxContent({ only: "Only" }),
                webresourcetype: "customresx"
              }),
              createWebResource({
                webresourceid: "not-numeric-resx",
                name: "not-numeric",
                displayname: "",
                content: resxContent({ ignored: "Ignored" }),
                webresourcetype: "customresx"
              })
            ]
          });
        }

        return Promise.resolve({
          value: [
            createWebResource({
              webresourceid: "numeric-resx",
              name: "1033",
              displayname: "",
              content: resxContent({ only: "Only" }),
              webresourcetype: "customresx"
            })
          ]
        });
      }
    });

    await context.handler.Load();

    expect(context.grid.add.mock.calls[0][0][0]).toMatchObject({
      recid: "",
      schemaName: "1033",
      _emptyReadonlyPlaceholder: "-"
    });
    expect(context.grid.add.mock.calls[0][0][0].w2ui.children[0]).toMatchObject({
      schemaName: "only",
      1033: "Only"
    });
  });

  it("covers rare load fallbacks for missing names, content, and LCID", async function () {
    var dynamicNameCalls = 0;
    var dynamicResource = createWebResource({
      webresourceid: "dynamic",
      displayname: "",
      content: jsonContent({ dynamic: "Dynamic" }),
      webresourcetype: 3
    });
    Object.defineProperty(dynamicResource, "name", {
      get: function () {
        dynamicNameCalls++;
        return dynamicNameCalls <= 1 ? "dynamic.1033.js" : "";
      }
    });
    var baseNameCalls = 0;
    var baseWithoutNames = createWebResource({
      webresourceid: "base-without-names",
      displayname: "",
      content: jsonContent({ dynamic: "Base" }),
      webresourcetype: 3
    });
    Object.defineProperty(baseWithoutNames, "name", {
      get: function () {
        baseNameCalls++;
        return baseNameCalls <= 1 ? "empty.1033.js" : "";
      }
    });

    var context = await setup({
      solution: null,
      retrieve: function (request) {
        if (request.queryParams.indexOf("contains(name, '1033')") !== -1) {
          return Promise.resolve({
            value: [baseWithoutNames]
          });
        }

        return Promise.resolve({
          value: [
            dynamicResource,
            createWebResource({
              webresourceid: "blank-content",
              name: "blank.1033.js",
              displayname: "",
              content: undefined,
              webresourcetype: 3
            }),
            null
          ]
        });
      }
    });

    await context.handler.Load();

    var parent = context.grid.add.mock.calls[0][0][0];
    expect(parent.recid).toBe("");
    expect(parent.schemaName).toBe("");
    expect(parent.w2ui.children[0]).toMatchObject({
      recid: "|dynamic",
      schemaName: "dynamic",
      _emptyEditablePlaceholder: "Add display text"
    });
    expect(parent.w2ui.children[0]["1033"]).toBeUndefined();
  });

  it("uses empty fallback records when no solution is selected or parsing fails", async function () {
    var parseError = new Error("retrieve failed");
    var context = await setup({
      solution: "all",
      retrieve: function () {
        return Promise.reject(parseError);
      }
    });

    await context.handler.Load();

    expect(context.XrmTranslator.errorHandler).toHaveBeenCalledWith(parseError);
    expect(context.grid.add).not.toHaveBeenCalled();
  });
});

describe("WebResourceHandler.Save", function () {
  it("returns no changes without publishing when there are no updates", async function () {
    var context = await setup({ records: [{ recid: "group", schemaName: "group" }] });

    var result = await context.handler.Save();

    expect(result).toEqual({ hasChanges: false, ids: [] });
    expect(context.XrmTranslator.ExecuteChangeSetBatches).not.toHaveBeenCalled();
    expect(context.XrmTranslator.PublishWebResources).not.toHaveBeenCalled();
  });

  it("exposes no-op publish behavior when save results have no ids", async function () {
    var context = await setup();
    context.XrmTranslator.RunTypeSaveFlow.mockImplementationOnce(function (flow) {
      context.state.capturedFlow = flow;
      return Promise.resolve("captured");
    });

    await context.handler.Save();

    await expect(context.state.capturedFlow.publishAction(null)).resolves.toBeUndefined();
    await expect(context.state.capturedFlow.publishAction({ ids: [] })).resolves.toBeUndefined();
    expect(context.state.capturedFlow.shouldPublish(null)).toBe(false);
    expect(context.state.capturedFlow.shouldPublish({ hasChanges: true })).toBe(true);
    expect(context.XrmTranslator.PublishWebResources).not.toHaveBeenCalled();
  });

  it("updates existing JSON resources, creates missing localized resources, publishes, and reloads", async function () {
    var context = await setup({
      records: [
        {
          recid: "pl_/resources/messages.|hello",
          schemaName: "hello",
          w2ui: { changes: { 1041: "こんにちは", 1066: "Xin &lt;b&gt;chào&lt;/b&gt;" } }
        },
        {
          recid: "pl_/resources/messages.|empty",
          schemaName: "empty",
          w2ui: { changes: { 1041: "" } }
        }
      ],
      retrieve: function () {
        return Promise.resolve({ value: [] });
      }
    });
    context.XrmTranslator.metadata = {
      "pl_/resources/messages.": [
        createWebResource({
          webresourceid: "wr-1033",
          name: "pl_/resources/messages.1033.js",
          displayname: "Messages 1033",
          content: { hello: "Hello", empty: "" },
          __format: "json",
          __rawContent: '{"hello":"Hello","empty":""}',
          __lcid: "1033"
        }),
        createWebResource({
          webresourceid: "wr-1041",
          name: "pl_/resources/messages.1041.js",
          displayname: "Messages 1041",
          content: { hello: "", empty: "" },
          __format: "json",
          __rawContent: '{"hello":"","empty":""}',
          __lcid: "1041"
        })
      ]
    };

    await context.handler.Save();

    expect(context.state.changeSetRequests[0].batchOptions).toMatchObject({
      progressLabel: "Saving 17. Web Resources",
      batchNamePrefix: "batch_savewebresources",
      changeSetNamePrefix: "changeset_savewebresources"
    });
    expect(context.state.updateRequests).toHaveLength(1);
    expect(context.state.updateRequests[0]).toMatchObject({
      overriddenSetName: "webresourceset",
      entityId: "wr-1041",
      entity: {
        content: expect.any(String)
      },
      asBatch: true
    });
    expect(JSON.parse(decode(context.state.updateRequests[0].entity.content))).toEqual({
      hello: "こんにちは",
      empty: ""
    });
    expect(context.state.createRequests).toHaveLength(1);
    expect(context.state.createRequests[0]).toMatchObject({
      overriddenSetName: "webresourceset",
      entity: {
        name: "pl_/resources/messages.1066.js",
        displayname: "Messages 1066",
        webresourcetype: 3
      },
      asBatch: true
    });
    expect(JSON.parse(decode(context.state.createRequests[0].entity.content))).toEqual({
      hello: "Xin <b>chào</b>",
      empty: null
    });
    expect(context.XrmTranslator.PublishWebResources).toHaveBeenCalledWith(["wr-1041", "created-1"]);
    expect(context.grid.add).toHaveBeenCalledWith([]);
  });

  it("updates RESX resources and creates new RESX keys while preserving resource type", async function () {
    var context = await setup({
      records: [{ recid: "pl_/resources/labels.|title", schemaName: "title", w2ui: { changes: { 1041: "" } } }]
    });
    context.XrmTranslator.metadata = {
      "pl_/resources/labels.": [
        {
          webresourceid: "resx-1033",
          name: "pl_/resources/labels.1033.resx",
          displayname: "Labels 1033",
          content: { title: "Title", subtitle: "Subtitle" },
          __format: "resx",
          __rawContent: '<root><data name="title"><value>Title</value></data></root>',
          __lcid: "1033",
          webresourcetype: 12
        },
        {
          webresourceid: "resx-1041",
          name: "pl_/resources/labels.1041.resx",
          displayname: "Labels 1041",
          content: { title: "" },
          __format: "resx",
          __rawContent: '<root><data name="title"><value>Old</value></data></root>',
          __lcid: "1041",
          webresourcetype: 12
        }
      ]
    };

    await context.handler.Save();

    expect(context.state.updateRequests).toHaveLength(1);
    expect(context.state.updateRequests[0]).toMatchObject({
      overriddenSetName: "webresourceset",
      entityId: "resx-1041",
      asBatch: true
    });
    expect(decode(context.state.updateRequests[0].entity.content)).toContain("<value></value>");
  });

  it("creates empty RESX values for new data nodes", async function () {
    var context = await setup({
      records: [{ recid: "pl_/resources/labels.|subtitle", schemaName: "subtitle", w2ui: { changes: { 1041: "" } } }]
    });
    context.XrmTranslator.metadata = {
      "pl_/resources/labels.": [
        {
          webresourceid: "resx-1033",
          name: "pl_/resources/labels.1033.resx",
          displayname: "Labels 1033",
          content: { subtitle: "Subtitle" },
          __format: "resx",
          __rawContent: "<root></root>",
          __lcid: "1033",
          webresourcetype: 12
        }
      ]
    };

    await context.handler.Save();

    expect(decode(context.state.createRequests[0].entity.content)).toContain(
      '<data name="subtitle" xml:space="preserve"><value></value></data>'
    );
  });

  it("skips malformed RESX entries while parsing and serializing", async function () {
    var context = await setup({
      solution: null,
      retrieve: function (request) {
        if (request.queryParams.indexOf("contains(name, '1033')") !== -1) {
          return Promise.resolve({
            value: [
              createWebResource({
                webresourceid: "resx-1033",
                name: "broken.1033.resx",
                displayname: "",
                content: encode('<root><data><value>No name</value></data><data name="novalue"></data></root>'),
                webresourcetype: 12
              })
            ]
          });
        }

        return Promise.resolve({
          value: [
            createWebResource({
              webresourceid: "resx-1033",
              name: "broken.1033.resx",
              displayname: "",
              content: encode('<root><data><value>No name</value></data><data name="novalue"></data></root>'),
              webresourcetype: 12
            })
          ]
        });
      }
    });

    await context.handler.Load();

    expect(context.grid.add.mock.calls[0][0][0]).toMatchObject({
      recid: "broken.",
      schemaName: "broken.1033.resx",
      w2ui: { editable: false, children: [] }
    });

    context.state.records = [{ recid: "broken.|title", schemaName: "title", w2ui: { changes: { 1041: "Title" } } }];
    context.XrmTranslator.metadata = {
      "broken.": [
        {
          webresourceid: "resx-1033",
          name: "broken.1033.resx",
          displayname: "",
          content: { title: "Title" },
          __format: "resx",
          __rawContent: '<root><data><value>No name</value></data><data name="novalue"></data></root>',
          __lcid: "1033",
          webresourcetype: 12
        }
      ]
    };

    await context.handler.Save();

    expect(decode(context.state.createRequests[0].entity.content)).toContain("title");
  });

  it("blocks empty base-language display text before save", async function () {
    var context = await setup({
      records: [
        {
          recid: "pl_/resources/messages.|hello",
          schemaName: "hello",
          w2ui: { changes: { 1033: " " } }
        }
      ]
    });
    context.XrmTranslator.metadata = {
      "pl_/resources/messages.": [
        {
          webresourceid: "wr-1033",
          name: "pl_/resources/messages.1033.js",
          displayname: "Messages 1033",
          content: { hello: "Hello" },
          __format: "json",
          __rawContent: "{}",
          __lcid: "1033"
        }
      ]
    };

    await expect(context.handler.Save()).rejects.toThrow(
      "Display Text in the base language (English (en-us) (1033)) cannot be empty.\n" +
        "Row: pl_/resources/messages. > hello"
    );
    expect(context.XrmTranslator.ExecuteChangeSetBatches).not.toHaveBeenCalled();
  });

  it("uses caption, label, field, and empty column fallbacks in validation errors", async function () {
    var grid = createGrid();
    var context = await setup({
      baseLanguage: 1041,
      grid: grid,
      records: [{ recid: "|caption", schemaName: "caption", w2ui: { changes: { 1041: "" } } }]
    });

    await expect(context.handler.Save()).rejects.toThrow("Japanese (ja-jp) (1041)");

    context.XrmTranslator.baseLanguage = 1066;
    context.state.records = [{ recid: "|label", schemaName: "label", w2ui: { changes: { 1066: "" } } }];
    await expect(context.handler.Save()).rejects.toThrow("Vietnamese (vi-vn) (1066)");

    grid.columns.push({ field: "7777" });
    context.XrmTranslator.baseLanguage = 7777;
    context.state.records = [{ recid: "|field", schemaName: "field", w2ui: { changes: { 7777: "" } } }];
    await expect(context.handler.Save()).rejects.toThrow("base language (7777)");

    grid.columns = undefined;
    context.XrmTranslator.baseLanguage = 8888;
    context.state.records = [{ recid: "|missing", schemaName: null, w2ui: { changes: { 8888: "" } } }];
    await expect(context.handler.Save()).rejects.toThrow(
      "Display Text in the base language (8888) cannot be empty.\nRow: (unknown)"
    );
  });

  it("covers validation fallback branches and inherited change keys", async function () {
    var grid = createGrid();
    grid.columns = [{ field: "schemaName", text: "Schema Name" }];
    var inheritedChanges = Object.create({ 1041: "Inherited" });
    inheritedChanges["9999"] = "";
    var inheritedOnlyChanges = Object.create({ 1041: "Inherited" });
    var context = await setup({
      baseLanguage: 9999,
      grid: grid,
      records: [
        {
          recid: "same",
          schemaName: "same",
          w2ui: { changes: { 9999: "" } }
        }
      ]
    });
    context.XrmTranslator.metadata = {
      same: [
        {
          webresourceid: "same-1033",
          name: "same1033.js",
          displayname: "same",
          content: { same: "Same" },
          __format: "json",
          __rawContent: "{}",
          __lcid: "1033"
        }
      ],
      "": [
        {
          name: "",
          displayname: "",
          content: { missing: "Missing" },
          __format: "json",
          __rawContent: "{}",
          __lcid: "1033"
        }
      ]
    };

    await expect(context.handler.Save()).rejects.toThrow(
      "Display Text in the base language (9999) cannot be empty.\nRow: same"
    );

    context.state.records = [
      {
        recid: "|missing",
        schemaName: null,
        w2ui: { changes: { 9999: "" } }
      }
    ];
    await expect(context.handler.Save()).rejects.toThrow(
      "Display Text in the base language (9999) cannot be empty.\nRow: (unknown)"
    );

    context.XrmTranslator.metadata.displayed = [
      {
        name: "displayed1033.js",
        displayname: "Displayed 1033",
        content: { row: "Row" },
        __format: "json",
        __rawContent: "{}",
        __lcid: "1033"
      }
    ];
    context.XrmTranslator.metadata.displayed.__displayName = "Display Group";
    context.state.records = [
      {
        recid: "displayed|row",
        schemaName: "row",
        w2ui: { changes: { 9999: "" } }
      }
    ];
    await expect(context.handler.Save()).rejects.toThrow(
      "Display Text in the base language (9999) cannot be empty.\nRow: Display Group > row"
    );

    context.state.records = [
      {
        recid: "same",
        schemaName: "same",
        w2ui: { changes: { 9999: "Known", 1041: "JA" } }
      },
      {
        recid: "same|same",
        schemaName: "same",
        w2ui: { changes: inheritedOnlyChanges }
      }
    ];
    await context.handler.Save();

    expect(context.state.createRequests[0].entity.content).toBeDefined();
  });

  it("allows base-language clears when no base language is available", async function () {
    var context = await setup({
      baseLanguage: null,
      records: [
        {
          recid: "group|hello",
          schemaName: "hello",
          w2ui: { changes: { 1033: "" } }
        }
      ]
    });
    context.XrmTranslator.metadata = {
      group: [
        {
          webresourceid: "wr-1033",
          name: "group1033.js",
          displayname: "Group 1033",
          content: { hello: "Hello" },
          __format: "json",
          __rawContent: "{}",
          __lcid: "1033"
        }
      ]
    };

    await context.handler.Save();

    expect(JSON.parse(decode(context.state.updateRequests[0].entity.content))).toEqual({ hello: "" });
  });

  it("surfaces unresolved created ids as a publish error", async function () {
    var context = await setup({
      records: [{ recid: "group|hello", schemaName: "hello", w2ui: { changes: { 1041: "JA" } } }]
    });
    context.XrmTranslator.metadata = {
      group: [
        {
          name: "group1033.js",
          displayname: "Group 1033",
          content: { hello: "Hello" },
          __format: "json",
          __rawContent: "{}",
          __lcid: "1033"
        }
      ]
    };
    context.XrmTranslator.ExecuteChangeSetBatches.mockImplementationOnce(function (updates, batchOptions) {
      batchOptions.buildRequest(updates[0], { contentId: 1 });
      return Promise.resolve([{ contentId: 1, headers: {} }]);
    });

    await expect(context.handler.Save()).rejects.toThrow(
      "Saved web resources, but could not resolve every web resource id for publish."
    );
  });

  it("surfaces missing response headers while resolving created ids", async function () {
    var context = await setup({
      records: [{ recid: "group|hello", schemaName: "hello", w2ui: { changes: { 1041: "JA" } } }]
    });
    context.XrmTranslator.metadata = {
      group: [
        {
          name: "group1033.js",
          displayname: "",
          content: { hello: "Hello" },
          __format: null,
          __rawContent: "{}",
          __lcid: "1033"
        }
      ]
    };
    context.XrmTranslator.ExecuteChangeSetBatches.mockImplementationOnce(function (updates, batchOptions) {
      batchOptions.buildRequest(updates[0], { contentId: 1 });
      return Promise.resolve([{ contentId: 1 }]);
    });

    await expect(context.handler.Save()).rejects.toThrow(
      "Saved web resources, but could not resolve every web resource id for publish."
    );
    expect(context.state.createRequests[0].entity).toMatchObject({
      displayname: "group1041.js",
      webresourcetype: 3
    });
  });

  it("creates fallback unnamed web resources when the base template has no names", async function () {
    var context = await setup({
      records: [{ recid: "blank|hello", schemaName: "hello", w2ui: { changes: { 1041: "JA" } } }]
    });
    context.XrmTranslator.metadata = {
      blank: [
        {
          name: "",
          displayname: "",
          content: { hello: "Hello" },
          __format: "",
          __rawContent: "{}",
          __lcid: "1033"
        }
      ]
    };
    context.XrmTranslator.ExecuteChangeSetBatches.mockImplementationOnce(function (updates, batchOptions) {
      batchOptions.buildRequest(updates[0], { contentId: 1 });
      return Promise.resolve([{ contentId: 1, headers: { "odata-entityid": "webresourceset(created-lower)" } }]);
    });

    await context.handler.Save();

    expect(context.state.createRequests[0].entity).toMatchObject({
      name: "",
      displayname: "",
      webresourcetype: 3
    });
    expect(context.XrmTranslator.PublishWebResources).toHaveBeenCalledWith(["created-lower"]);
  });
});
