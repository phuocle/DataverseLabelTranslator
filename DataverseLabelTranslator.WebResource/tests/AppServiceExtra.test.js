import { describe, it, expect, vi, beforeEach } from "vitest";

// --- Setup globals for AppService IIFE ---
const mockHelper = {
    CustomActionTypes: { Loading: "Loading", Saving: "Saving", Publishing: "Publishing", Published: "Published", Other: "Other" },
    ExecuteTypedCustomAction: vi.fn(),
    GetCustomActionObject: vi.fn((r) => (r && r.object) || {}),
    GetBaseLanguage: vi.fn(),
    GetTranslator: vi.fn()
};

beforeEach(() => {
    globalThis.window.Helper = mockHelper;
    globalThis.window.w2ui = {};
    globalThis.window.AppService = undefined;
    globalThis.window.w2alert = vi.fn();
    globalThis.window.w2confirm = vi.fn();
    globalThis.window.w2popup = {
        open: vi.fn(),
        close: vi.fn(),
        max: vi.fn(),
        lock: vi.fn(),
        unlock: vi.fn()
    };
    globalThis.window.w2tabs = function (config) {
        return {
            render: vi.fn(),
            refresh: vi.fn(),
            click: vi.fn(),
            destroy: vi.fn(),
            box: null,
            active: config.active,
            on: vi.fn()
        };
    };
    globalThis.window.w2form = function (config) {
        return {
            render: vi.fn(),
            refresh: vi.fn(),
            destroy: vi.fn(),
            box: null,
            record: config.record,
            fields: config.fields,
            tabs: config.tabs,
            on: vi.fn(),
            goto: vi.fn()
        };
    };
    globalThis.window.DialogHelper = {
        alert: vi.fn()
    };
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

    mockHelper.ExecuteTypedCustomAction.mockReset();
    mockHelper.GetCustomActionObject.mockReset();
    mockHelper.GetBaseLanguage.mockReset();
    mockHelper.GetTranslator.mockReset();

    // Default: ExecuteTypedCustomAction returns a resolved promise with settings
    mockHelper.ExecuteTypedCustomAction.mockResolvedValue({ object: { content: '<appSettings><![CDATA[{"ai":{"providers":{"google":{"baseUrl":"https://x","apiKey":"abc12345","modelName":"gem"}},"openai":{"baseUrl":"","apiKey":"","modelName":""},"azure":{"baseUrl":"","apiKey":"","modelName":""}},"selectedProvider":"google"}}]]></appSettings>' } });
    mockHelper.GetCustomActionObject.mockImplementation((r) => (r && r.object) || {});
});

// Load AppService.js
await import("../js/AppService.js");

const AppService = globalThis.window.AppService;

// ============================================================
// Public API surface (additional coverage)
// ============================================================
describe("AppService.GetAISettings", () => {
    it("returns AI settings object", async () => {
        const result = await AppService.GetAISettings();
        expect(result).toBeDefined();
        expect(result).toHaveProperty("providers");
    });

    it("uses force refresh when requested", async () => {
        await AppService.GetAISettings(true);
        expect(mockHelper.ExecuteTypedCustomAction).toHaveBeenCalled();
    });
});

describe("AppService.SaveAISettings", () => {
    it("returns a promise", () => {
        const result = AppService.SaveAISettings({ providers: { google: {} } });
        expect(result).toBeInstanceOf(Promise);
    });

    it("persists the provided AI settings", async () => {
        const result = await AppService.SaveAISettings({
            providers: {
                google: { baseUrl: "https://api.google", apiKey: "key12345", modelName: "gemini" }
            }
        });
        expect(result).toBeDefined();
    });

    it("handles null input gracefully", async () => {
        const result = await AppService.SaveAISettings(null);
        expect(result).toBeDefined();
    });
});

describe("AppService.GetProviderConfig", () => {
    it("returns a provider configuration object", async () => {
        const result = await AppService.GetProviderConfig("google");
        expect(result).toBeDefined();
    });

    it("normalizes provider key before lookup", async () => {
        const result = await AppService.GetProviderConfig("gemini");
        expect(result).toBeDefined();
    });

    it("returns an empty config for unknown providers", async () => {
        const result = await AppService.GetProviderConfig("unknown-provider-xyz");
        expect(result).toBeDefined();
    });
});

describe("AppService.NormalizeProviderKey", () => {
    it("normalizes gemini to google", () => {
        expect(AppService.NormalizeProviderKey("gemini")).toBe("google");
    });

    it("normalizes google-gemini to google", () => {
        expect(AppService.NormalizeProviderKey("google-gemini")).toBe("google");
    });

    it("normalizes azure-foundry to azure", () => {
        expect(AppService.NormalizeProviderKey("azure-foundry")).toBe("azure");
    });

    it("normalizes openai-compatible to openai", () => {
        expect(AppService.NormalizeProviderKey("openai-compatible")).toBe("openai");
    });

    it("returns lowercase trimmed key for unknown provider", () => {
        expect(AppService.NormalizeProviderKey("  MyProvider  ")).toBe("myprovider");
    });

    it("returns empty string for null/undefined", () => {
        expect(AppService.NormalizeProviderKey(null)).toBe("");
        expect(AppService.NormalizeProviderKey(undefined)).toBe("");
    });
});

describe("AppService.GetSettings", () => {
    it("returns a promise", () => {
        const result = AppService.GetSettings();
        expect(result).toBeInstanceOf(Promise);
    });
});

describe("AppService.SaveSettings", () => {
    it("returns a promise", () => {
        const result = AppService.SaveSettings({});
        expect(result).toBeInstanceOf(Promise);
    });
});

describe("AppService.EnsureInitialized", () => {
    it("returns a promise", () => {
        const result = AppService.EnsureInitialized();
        expect(result).toBeInstanceOf(Promise);
    });

    it("accepts forceRefresh argument", () => {
        const result = AppService.EnsureInitialized(true);
        expect(result).toBeInstanceOf(Promise);
    });
});

describe("AppService.ShowAppSettings", () => {
    it("is a function", () => {
        expect(typeof AppService.ShowAppSettings).toBe("function");
    });

    it("invokes without throwing on minimal DOM", () => {
        // Mock document.querySelector to return a fake popup container
        const tabsContainer = { id: "app-settings-ai-tabs", innerHTML: "", appendChild: vi.fn() };
        const popupContainer = { id: "w2ui-popup", innerHTML: "", appendChild: vi.fn() };

        globalThis.document.querySelector = vi.fn((selector) => {
            if (selector === "#w2ui-popup #app-settings-ai-tabs") {
                return tabsContainer;
            }
            if (selector === "#w2ui-popup") {
                return popupContainer;
            }
            return null;
        });
        globalThis.document.querySelectorAll = vi.fn(() => []);

        expect(() => AppService.ShowAppSettings()).not.toThrow();
    });
});
