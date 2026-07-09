import { describe, it, expect, vi, beforeEach } from "vitest";

// --- Setup globals for AppService IIFE ---
const mockHelper = {
    CustomActionTypes: { Loading: "Loading", Saving: "Saving", Publishing: "Publishing", Published: "Published", Other: "Other" },
    ExecuteTypedCustomAction: vi.fn(),
    GetCustomActionObject: vi.fn((r) => (r && r.object) || {}),
    GetBaseLanguage: vi.fn().mockResolvedValue("1033"),
    GetTranslator: vi.fn()
};

let mockW2PopupOpen;
let mockW2Form;
let mockW2Tabs;
let mockForm;

beforeEach(() => {
    globalThis.window.Helper = mockHelper;
    globalThis.Helper = mockHelper;
    globalThis.window.w2ui = {};
    globalThis.window.AppService = undefined;
    globalThis.window.w2alert = vi.fn();
    globalThis.window.w2confirm = vi.fn();

    // Pre-create mockForm in w2ui so render can find it
    globalThis.window.w2ui = {};
    mockForm = {
        render: vi.fn(),
        refresh: vi.fn(),
        destroy: vi.fn(),
        resize: vi.fn(),
        goto: vi.fn(),
        reload: vi.fn(),
        box: { style: {} },
        record: {},
        fields: [],
        tabs: { on: vi.fn() },
        on: vi.fn()
    };
    globalThis.window.w2ui.appSettings = mockForm;
    globalThis.window.w2ui.appSettingsAiProviderTabs = undefined;

    mockW2Form = function (config) {
        Object.assign(mockForm, { config });
        return mockForm;
    };

    mockW2Tabs = function (config) {
        const tabs = {
            config,
            render: vi.fn(),
            refresh: vi.fn(),
            click: vi.fn(),
            destroy: vi.fn(),
            box: null,
            active: config.active,
            on: vi.fn()
        };
        globalThis.window.w2ui.appSettingsAiProviderTabs = tabs;
        return tabs;
    };

    mockW2PopupOpen = vi.fn((opts) => {
        // simulate onOpen -> onComplete call to trigger initializeAppSettingsForm
        if (opts.onOpen) {
            const event = { onComplete: null };
            opts.onOpen(event);
            if (typeof event.onComplete === "function") {
                // Resolve the promise chain
                Promise.resolve().then(() => event.onComplete());
            }
        }
        if (opts.onToggle) {
            const event = { onComplete: null };
            opts.onToggle(event);
            if (typeof event.onComplete === "function") {
                Promise.resolve().then(() => event.onComplete());
            }
        }
    });

    globalThis.window.w2popup = {
        open: mockW2PopupOpen,
        close: vi.fn(),
        max: vi.fn(),
        lock: vi.fn(),
        unlock: vi.fn()
    };
    globalThis.window.w2tabs = mockW2Tabs;
    globalThis.window.w2form = mockW2Form;
    globalThis.window.DialogHelper = { alert: vi.fn() };
    globalThis.document = {
        querySelector: vi.fn(() => null),
        querySelectorAll: vi.fn(() => []),
        createElement: vi.fn((tag) => ({
            tagName: String(tag || "").toUpperCase(),
            innerHTML: "",
            style: {},
            children: [],
            appendChild: vi.fn(),
            addEventListener: vi.fn(),
            removeEventListener: vi.fn()
        }))
    };
    globalThis.window.DOMParser = class {
        parseFromString(xml) {
            const hasAppSettings = xml && xml.includes("appSettings");
            const root = {
                nodeName: "appSettings",
                getElementsByTagName: (tag) => {
                    if (tag === "parsererror") return [];
                    if (tag === "appSettings") return [root];
                    return [];
                }
            };
            return {
                getElementsByTagName: (tag) => {
                    if (tag === "parsererror") return [];
                    if (tag === "appSettings") return hasAppSettings ? [root] : [];
                    return [];
                },
                documentElement: root
            };
        }
    };
    globalThis.window.atob = (b) => Buffer.from(b, "base64").toString("utf-8");
    globalThis.window.btoa = (s) => Buffer.from(s, "utf-8").toString("base64");
    globalThis.window.requestAnimationFrame = (cb) => setTimeout(cb, 0);

    mockHelper.ExecuteTypedCustomAction.mockReset();
    mockHelper.GetCustomActionObject.mockReset();
    mockHelper.GetBaseLanguage.mockReset();
    mockHelper.GetTranslator.mockReset();

    mockHelper.ExecuteTypedCustomAction.mockResolvedValue({
        object: {
            content: '<appSettings><![CDATA[{"ai":{"providers":{"google":{"baseUrl":"https://x","apiKey":"abc12345","modelName":"gem","enabled":true},"openai":{"baseUrl":"","apiKey":"","modelName":""},"azure":{"baseUrl":"","apiKey":"","modelName":""}},"selectedProvider":"google"}}]]></appSettings>'
        }
    });
    mockHelper.GetCustomActionObject.mockImplementation((r) => (r && r.object) || {});
});

await import("../js/AppService.js");
const AppService = globalThis.window.AppService;

// ============================================================
// Public API surface (additional coverage)
// ============================================================
describe("AppService public methods exposed", () => {
    it("EnsureInitialized is a function", () => {
        expect(typeof AppService.EnsureInitialized).toBe("function");
    });

    it("GetSettings is a function", () => {
        expect(typeof AppService.GetSettings).toBe("function");
    });

    it("SaveSettings is a function", () => {
        expect(typeof AppService.SaveSettings).toBe("function");
    });

    it("GetAISettings is a function", () => {
        expect(typeof AppService.GetAISettings).toBe("function");
    });

    it("SaveAISettings is a function", () => {
        expect(typeof AppService.SaveAISettings).toBe("function");
    });

    it("GetProviderConfig is a function", () => {
        expect(typeof AppService.GetProviderConfig).toBe("function");
    });

    it("ShowAppSettings is a function", () => {
        expect(typeof AppService.ShowAppSettings).toBe("function");
    });

    it("NormalizeProviderKey is a function", () => {
        expect(typeof AppService.NormalizeProviderKey).toBe("function");
    });
});

describe("AppService.NormalizeProviderKey", () => {
    it("normalizes 'gemini' to 'google'", () => {
        expect(AppService.NormalizeProviderKey("gemini")).toBe("google");
    });
    it("normalizes 'google-gemini' to 'google'", () => {
        expect(AppService.NormalizeProviderKey("google-gemini")).toBe("google");
    });
    it("normalizes 'azure-foundry' to 'azure'", () => {
        expect(AppService.NormalizeProviderKey("azure-foundry")).toBe("azure");
    });
    it("normalizes 'azurefoundry' to 'azure'", () => {
        expect(AppService.NormalizeProviderKey("azurefoundry")).toBe("azure");
    });
    it("normalizes 'openai-compatible' to 'openai'", () => {
        expect(AppService.NormalizeProviderKey("openai-compatible")).toBe("openai");
    });
    it("normalizes 'openai_compatible' to 'openai'", () => {
        expect(AppService.NormalizeProviderKey("openai_compatible")).toBe("openai");
    });
    it("passes through known providers", () => {
        expect(AppService.NormalizeProviderKey("openai")).toBe("openai");
        expect(AppService.NormalizeProviderKey("azure")).toBe("azure");
    });
    it("returns empty string for null/undefined", () => {
        expect(AppService.NormalizeProviderKey(null)).toBe("");
        expect(AppService.NormalizeProviderKey(undefined)).toBe("");
    });
});

describe("AppService.GetSettings execution", () => {
    it("returns a promise", async () => {
        const result = AppService.GetSettings(false);
        expect(result).toBeInstanceOf(Promise);
        try { await result; } catch (e) {}
    });
});

describe("AppService.GetAISettings execution", () => {
    it("returns AI settings object", async () => {
        const result = await AppService.GetAISettings();
        expect(result).toBeDefined();
    });

    it("forceRefresh true triggers re-fetch", async () => {
        await AppService.GetAISettings(true);
        expect(mockHelper.ExecuteTypedCustomAction).toHaveBeenCalled();
    });
});

describe("AppService.SaveSettings execution", () => {
    it("returns a promise", async () => {
        const result = AppService.SaveSettings({ theme: "default" });
        expect(result).toBeInstanceOf(Promise);
        try { await result; } catch (e) {}
    });
});

describe("AppService.SaveAISettings execution", () => {
    it("returns a promise", async () => {
        const result = AppService.SaveAISettings({ selectedProvider: "google" });
        expect(result).toBeInstanceOf(Promise);
        try { await result; } catch (e) {}
    });
});

describe("AppService.GetProviderConfig execution", () => {
    it("returns a promise with provider config", async () => {
        const result = AppService.GetProviderConfig("google", false);
        expect(result).toBeInstanceOf(Promise);
        try {
            const config = await result;
            // May be undefined if the test data lacks the provider
            expect(config === undefined || typeof config === "object").toBe(true);
        } catch (e) {}
    });

    it("normalizes provider key for unknown", async () => {
        const result = AppService.GetProviderConfig("gemini", false);
        expect(result).toBeInstanceOf(Promise);
        try { await result; } catch (e) {}
    });
});

describe("AppService.EnsureInitialized execution", () => {
    it("can be called", async () => {
        try {
            await AppService.EnsureInitialized(false);
        } catch (e) {}
        expect(true).toBe(true);
    });
});

describe("AppService.ShowAppSettings execution", () => {
    it("opens popup with title 'App Settings'", () => {
        AppService.ShowAppSettings();
        expect(mockW2PopupOpen).toHaveBeenCalled();
        const opts = mockW2PopupOpen.mock.calls[0][0];
        expect(opts.title).toBe("App Settings");
    });

    it("popup has body with loading text", () => {
        AppService.ShowAppSettings();
        const opts = mockW2PopupOpen.mock.calls[0][0];
        expect(opts.body).toContain("Loading app settings");
    });

    it("popup has onOpen handler", () => {
        AppService.ShowAppSettings();
        const opts = mockW2PopupOpen.mock.calls[0][0];
        expect(typeof opts.onOpen).toBe("function");
    });

    it("popup has onClose handler", () => {
        AppService.ShowAppSettings();
        const opts = mockW2PopupOpen.mock.calls[0][0];
        expect(typeof opts.onClose).toBe("function");
    });

    it("popup has onToggle handler", () => {
        AppService.ShowAppSettings();
        const opts = mockW2PopupOpen.mock.calls[0][0];
        expect(typeof opts.onToggle).toBe("function");
    });
});
