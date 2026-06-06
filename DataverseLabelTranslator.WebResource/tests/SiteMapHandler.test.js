import { beforeEach, describe, expect, it, vi } from "vitest";

var importCounter = 0;

function createXmlNode(tagName, attributes, children) {
  return {
    nodeName: tagName,
    nodeType: 1,
    attributes: attributes || {},
    childNodes: children || [],
    getAttribute: function (name) {
      return this.attributes[name] || null;
    },
    getElementsByTagName: function (tag) {
      var result = [];
      function visit(node) {
        if (!node || !node.childNodes) {
          return;
        }

        for (var i = 0; i < node.childNodes.length; i++) {
          var child = node.childNodes[i];
          if (child && child.nodeType === 1) {
            if (child.nodeName === tag) {
              result.push(child);
            }
            visit(child);
          }
        }
      }

      visit(this);
      return result;
    }
  };
}

function createXmlDocument(xmlString) {
  var stack = [createXmlNode("root", {}, [])];
  var tagPattern = /<\/?([A-Za-z_:][\w:.-]*)([^>]*)>/g;

  var match;
  while ((match = tagPattern.exec(xmlString)) !== null) {
    var fullTag = match[0];
    var tagName = match[1];
    var attributeText = match[2] || "";
    var attributes = {};
    var attrPattern = /([A-Za-z_:][\w:.-]*)="([^"]*)"/g;
    var attrMatch;

    while ((attrMatch = attrPattern.exec(attributeText)) !== null) {
      attributes[attrMatch[1]] = attrMatch[2];
    }

    if (fullTag.indexOf("</") === 0) {
      stack.pop();
      continue;
    }

    var node = createXmlNode(tagName, attributes, []);
    stack[stack.length - 1].childNodes.push(node);

    if (fullTag.slice(-2) !== "/>") {
      stack.push(node);
    }
  }

  return {
    getElementsByTagName: function (tagName) {
      var results = [];
      function visit(node) {
        if (!node || !node.childNodes) {
          return;
        }

        for (var i = 0; i < node.childNodes.length; i++) {
          var child = node.childNodes[i];
          if (child && child.nodeType === 1 && child.nodeName === tagName) {
            results.push(child);
          }
          visit(child);
        }
      }

      visit(stack[0]);
      return results;
    }
  };
}

function createGrid() {
  return {
    columns: [
      { field: "schemaName", text: "Schema Name" },
      { field: "1033", text: "English (en-us) (1033)" },
      { field: "1036", text: "French (fr-fr) (1036)" }
    ],
    records: [],
    clear: vi.fn(function () {
      this.records = [];
    }),
    add: vi.fn(function (records) {
      this.records = this.records.concat(records);
    }),
    unlock: vi.fn()
  };
}

function createHarness(options) {
  options = options || {};
  var grid = options.grid || createGrid();
  var state = {
    records: options.records || [],
    metadata: options.initialMetadata || []
  };

  var app = {
    baseLanguage: options.baseLanguage || 1033,
    metadata: state.metadata,
    IsDisplayTextComponent: vi.fn(function () {
      return options.component !== "Description";
    }),
    IsDescriptionComponent: vi.fn(function () {
      return options.component === "Description";
    }),
    GetGrid: vi.fn(function () {
      return grid;
    }),
    GetSolution: vi.fn(function () {
      return options.solution || "solution-1";
    }),
    GetEntity: vi.fn(function () {
      return "sitemap";
    }),
    GetEntityId: vi.fn(function () {
      return "entity-id";
    }),
    GetComponent: vi.fn(function () {
      return options.component || "DisplayText";
    }),
    GetMetadata: options.withGetMetadata
      ? vi.fn(function () {
          return app.metadata;
        })
      : undefined,
    SetMetadata: options.noSetMetadata
      ? undefined
      : vi.fn(function (metadata) {
          app.metadata = metadata;
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
    ComponentTypes: {
      DisplayText: "DisplayText",
      Description: "Description"
    },
    GetTranslator: vi.fn(function () {
      return app;
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
    ApplyPlaceholder: vi.fn(function () {
      return null;
    }),
    AddLocalizedLabelsToRecord: vi.fn(function () {
      return null;
    }),
    GetComponentLocalizedLabels: vi.fn(function () {
      return [];
    }),
    GetChangedLabels: vi.fn(function (changes) {
      var labels = [];
      for (var key in changes) {
        if (Object.prototype.hasOwnProperty.call(changes, key)) {
          labels.push({ LanguageCode: key, Label: changes[key] });
        }
      }
      return labels;
    }),
    ValidateBaseLanguageNotEmpty:
      options.validateBaseLanguage ||
      vi.fn(function (record, changes, context) {
        if (context && typeof context.getRowPath === "function") {
          context.getRowPath(record);
        }
        return null;
      }),
    FinalizeGrid: vi.fn(function (records, translator) {
      translator.AddSummary(records);
      translator.GetGrid().add(records);
      translator.UnlockGrid();
    }),
    RunServerLoad: vi.fn(function (runOptions) {
      var payload = typeof runOptions.getPayload === "function" ? runOptions.getPayload() : null;
      return Promise.resolve({
        ok: true,
        object: options.loadObject || { sitemaps: options.loadedSitemaps || [], payload: payload },
        type: "Loading"
      }).then(function (result) {
        runOptions.onLoaded(result.object, result);
        return result;
      });
    }),
    RunServerSaveFlow: vi.fn(function (runOptions) {
      var savePayload = typeof runOptions.getSavePayload === "function" ? runOptions.getSavePayload() : null;
      var publishPayload =
        typeof runOptions.getPublishPayload === "function"
          ? runOptions.getPublishPayload({ sitemapIds: ["sitemap-1"] })
          : null;
      var publishedPayload =
        typeof runOptions.getPublishedPayload === "function"
          ? runOptions.getPublishedPayload({ sitemapIds: ["sitemap-1"] })
          : null;
      var shouldReload =
        typeof runOptions.shouldReload === "function" ? runOptions.shouldReload({ sitemapIds: ["sitemap-1"] }) : false;

      return Promise.resolve({
        ok: true,
        object: {
          sitemapIds: ["sitemap-1"],
          savePayload: savePayload,
          publishPayload: publishPayload,
          publishedPayload: publishedPayload,
          shouldReload: shouldReload
        },
        type: "Saving"
      }).then(function (result) {
        if (shouldReload && typeof runOptions.reloadAction === "function") {
          return runOptions.reloadAction(result, result.object).then(function () {
            return result;
          });
        }

        return result;
      });
    })
  };

  if (options.saveHandler) {
    helper.RunServerSaveFlow = vi.fn(options.saveHandler);
  }

  globalThis.window = {};
  globalThis.DOMParser = options.domParserError
    ? class {
        parseFromString() {
          throw new Error("Invalid sitemap XML");
        }
      }
    : class {
        parseFromString(xmlString) {
          return createXmlDocument(xmlString);
        }
      };
  if (!options.noExistingHandler) {
    globalThis.window.SiteMapHandler = {};
  }
  globalThis.XrmTranslator = app;
  globalThis.Helper = helper;
  globalThis.DialogHelper = {
    alert: vi.fn(function () {
      return Promise.resolve();
    })
  };

  return { grid: grid, app: app, helper: helper, DialogHelper: globalThis.DialogHelper };
}

async function loadHandler() {
  vi.resetModules();
  await import("../js/Handler/SiteMapHandler.js?test=" + ++importCounter);
  return globalThis.window.SiteMapHandler;
}

async function setup(options) {
  var harness = createHarness(options);
  var handler = await loadHandler();
  return Object.assign({ handler: handler }, harness);
}

beforeEach(function () {
  vi.restoreAllMocks();
  delete globalThis.window;
  delete globalThis.XrmTranslator;
  delete globalThis.Helper;
  delete globalThis.DialogHelper;
});

describe("SiteMapHandler", function () {
  it("loads sitemap data through the server helper and fills the grid", async function () {
    var context = await setup({
      loadedSitemaps: [
        {
          sitemapid: "sitemap-1",
          sitemapname: "Primary sitemap",
          sitemapxml: '<sitemap><Area Id="A"><Titles><Title LCID="1033" Title="Area" /></Titles></Area></sitemap>'
        }
      ]
    });

    await context.handler.Load("Loading ...");

    expect(context.helper.RunServerLoad).toHaveBeenCalledWith(
      expect.objectContaining({
        actionName: "SiteMap"
      })
    );
    expect(context.app.SetMetadata).toHaveBeenCalledWith([
      { sitemapid: "sitemap-1", sitemapname: "Primary sitemap", sitemapxml: expect.any(String) }
    ]);
    expect(context.grid.add).toHaveBeenCalled();
  });

  it("uses the fallback metadata setter and parses sitemap XML labels", async function () {
    var context = await setup({
      noSetMetadata: true,
      loadedSitemaps: [
        null,
        {
          sitemapid: "sitemap-1",
          sitemapname: "Primary sitemap",
          sitemapxml:
            '<sitemap><Area Id="A"><Titles><Title LCID="1033" Title="Area" /></Titles><Descriptions><Description LCID="1033" Description="Area desc" /></Descriptions></Area><Area Id="B"><Group Id="G"><SubArea Id="S" Entity="account"><Titles><Title LCID="1033" Title="SubArea" /></Titles></SubArea></Group></Area></sitemap>'
        },
        {
          sitemapid: "missing-xml",
          sitemapname: "Missing XML"
        }
      ]
    });

    await context.handler.Load("Loading ...");

    expect(context.app.metadata).toContainEqual({
      sitemapid: "sitemap-1",
      sitemapname: "Primary sitemap",
      sitemapxml: expect.any(String)
    });
    expect(context.grid.add).toHaveBeenCalled();
    expect(
      context.grid.add.mock.calls[0][0].some(function (record) {
        return record && record.recid === "sitemap-1";
      })
    ).toBe(true);
  });

  it("handles invalid sitemap XML without crashing and supports save reload flow", async function () {
    var saveCalls = [];
    var context = await setup({
      loadedSitemaps: [
        {
          sitemapid: "sitemap-1",
          sitemapname: "Primary sitemap",
          sitemapxml: '<sitemap><Area Id="Broken"></sitemap>'
        }
      ],
      domParserError: true,
      saveHandler: vi.fn(function (runOptions) {
        saveCalls.push(runOptions);
        return Promise.resolve({ ok: true, object: { sitemapIds: ["sitemap-1"] }, type: "Saving" }).then(
          function (result) {
            var shouldReload =
              typeof runOptions.shouldReload === "function" ? runOptions.shouldReload(result.object) : false;
            if (shouldReload && typeof runOptions.reloadAction === "function") {
              return runOptions.reloadAction(result, result.object).then(function () {
                return result;
              });
            }
            return result;
          }
        );
      })
    });

    await context.handler.Load("Loading ...");
    expect(context.app.errorHandler).toHaveBeenCalled();

    context = await setup({
      records: [
        null,
        {
          recid: "sitemap-1|A|G|S",
          _siteMapId: "",
          _siteMapCompositeId: "A|G|S",
          _siteMapNodeType: "SubArea",
          w2ui: { changes: { 1033: "Changed" } }
        },
        { recid: "", w2ui: { changes: { 1033: "Changed" } } }
      ],
      saveHandler: vi.fn(function (runOptions) {
        saveCalls.push(runOptions);
        return Promise.resolve({ ok: true, object: { sitemapIds: ["sitemap-1"] }, type: "Saving" }).then(
          function (result) {
            return result;
          }
        );
      })
    });

    await context.handler.Save();

    expect(saveCalls.length).toBeGreaterThan(0);
    expect(context.helper.RunServerSaveFlow).toHaveBeenCalledWith(
      expect.objectContaining({
        actionName: "SiteMap"
      })
    );
  });

  it("alerts on no changes and reloads after save", async function () {
    var context = await setup({
      records: [{ recid: "row-1", w2ui: { changes: {} } }]
    });

    await context.handler.Save();

    expect(context.DialogHelper.alert).toHaveBeenCalled();
    expect(context.helper.RunServerSaveFlow).not.toHaveBeenCalled();

    context = await setup({
      records: [{ recid: "row-1", w2ui: { changes: { 1033: "Changed" } } }]
    });

    await context.handler.Save();

    expect(context.helper.RunServerSaveFlow).toHaveBeenCalledWith(
      expect.objectContaining({
        actionName: "SiteMap"
      })
    );
  });

  it("uses server entity-label fallback and XML title fallback for display text", async function () {
    var context = await setup({
      loadedSitemaps: [
        {
          sitemapid: "sitemap-1",
          sitemapname: "Primary sitemap",
          entityLabels: [
            {
              entityName: "account",
              labels: [
                { LanguageCode: 1033, Label: "Account" },
                { LanguageCode: 1036, Label: "Compte" }
              ]
            }
          ],
          sitemapxml:
            '<sitemap><Area Id="A" Title="Area default"><Group Id="G"><SubArea Id="S" Entity="account" /><SubArea Id="C" Entity="contact" /></Group></Area></sitemap>'
        }
      ]
    });

    await context.handler.Load();

    expect(context.app.LockGrid).toHaveBeenCalledWith("Loading ...");
    var parent = context.grid.add.mock.calls[0][0][0];
    expect(parent.w2ui.children[0]["1033"]).toBe("Area default");
    expect(parent.w2ui.children[0]["1036"]).toBe("Area default");
    expect(parent.w2ui.children[2]["1033"]).toBe("Account");
    expect(parent.w2ui.children[2]["1036"]).toBe("Compte");
    expect(parent.w2ui.children[3]["1033"]).toBeUndefined();
  });

  it("loads description rows through GetMetadata and handles empty load output", async function () {
    var context = await setup({
      component: "Description",
      withGetMetadata: true,
      noExistingHandler: true,
      loadObject: {},
      initialMetadata: [
        {
          sitemapid: "sitemap-1",
          sitemapxml:
            '<sitemap><Area Id="A"><Descriptions><Description LCID="1033" Description="Area desc" /></Descriptions></Area></sitemap>'
        }
      ]
    });

    await context.handler.Load("Custom loading");

    expect(context.app.LockGrid).toHaveBeenCalledWith("Custom loading");
    expect(context.app.SetMetadata).toHaveBeenCalledWith([]);
    expect(context.grid.add).toHaveBeenCalledWith([]);

    context = await setup({
      component: "Description",
      withGetMetadata: true,
      loadedSitemaps: [
        {
          sitemapid: "sitemap-1",
          sitemapxml:
            '<sitemap><Area Id="A"><Descriptions><Description LCID="1033" Description="Area desc" /></Descriptions></Area></sitemap>'
        }
      ]
    });

    await context.handler.Load("Loading ...");
    var parent = context.grid.add.mock.calls[0][0][0];
    expect(parent.schemaName).toBe("sitemap-1");
    expect(parent.w2ui.children[0]["1033"]).toBe("Area desc");
    expect(context.helper.GetPlaceholderDescription).toHaveBeenCalled();
  });

  it("skips publish and reload when the server save output has no sitemap ids", async function () {
    var saveCalls = [];
    var context = await setup({
      component: "Description",
      records: [
        { recid: "ignored" },
        { recid: "ignored", w2ui: {} },
        { recid: "sitemap-1|A", schemaName: "Area", w2ui: { changes: { 1033: "Changed" } } }
      ],
      saveHandler: vi.fn(function (runOptions) {
        saveCalls.push({
          savePayload: runOptions.getSavePayload(),
          publishPayload: runOptions.getPublishPayload({}),
          publishedPayload: runOptions.getPublishedPayload({}),
          shouldReload: runOptions.shouldReload({})
        });
        return Promise.resolve({ ok: true, object: {}, type: "Saving" });
      })
    });

    await context.handler.Save();

    expect(saveCalls[0].savePayload.sitemapUpdates).toEqual([
      {
        sitemapId: "sitemap-1",
        compositeId: "A",
        nodeType: "SubArea",
        component: "Description",
        labels: [{ LanguageCode: "1033", Label: "Changed" }]
      }
    ]);
    expect(saveCalls[0].publishPayload).toBeNull();
    expect(saveCalls[0].publishedPayload).toBeNull();
    expect(saveCalls[0].shouldReload).toBe(false);
  });

  it("handles sitemap nodes with missing optional XML attributes", async function () {
    var context = await setup({
      loadedSitemaps: [
        {
          sitemapid: "sitemap-1",
          sitemapxml: "<sitemap><Area><Group><SubArea /></Group></Area></sitemap>"
        }
      ]
    });

    await context.handler.Load("Loading ...");

    var parent = context.grid.add.mock.calls[0][0][0];
    expect(parent.w2ui.children).toHaveLength(3);
    expect(parent.w2ui.children[0].recid).toBe("sitemap-1|");
    expect(parent.w2ui.children[1].recid).toBe("sitemap-1||");
    expect(parent.w2ui.children[2].recid).toBe("sitemap-1|||");
    expect(parent.w2ui.children[2].schemaName).toBe("[SubArea] ");
  });

  it("handles missing entity label collections and grids without language columns", async function () {
    var grid = createGrid();
    delete grid.columns;

    var context = await setup({
      grid: grid,
      loadedSitemaps: [
        {
          sitemapid: "sitemap-1",
          sitemapxml:
            '<sitemap><Area Id="A" Title="Default"><Group Id="G"><SubArea Id="S" Entity="account" /></Group></Area></sitemap>'
        },
        {
          sitemapid: "sitemap-2",
          entityLabels: [{ entityName: "account" }],
          sitemapxml: '<sitemap><Area Id="A"><Group Id="G"><SubArea Id="S" Entity="account" /></Group></Area></sitemap>'
        }
      ]
    });

    await context.handler.Load("Loading ...");

    var records = context.grid.add.mock.calls[0][0];
    expect(records[0].w2ui.children[0]["1033"]).toBeUndefined();
    expect(records[0].w2ui.children[2]["1033"]).toBeUndefined();
    expect(records[1].w2ui.children[2]["1033"]).toBeUndefined();
  });
});
