import { describe, it, expect, vi, beforeEach } from "vitest";

// Setup minimal globals
beforeEach(() => {
    globalThis.window.w2utils = {
        decodeTags: (s) => String(s || ""),
        encodeTags: (s) => String(s || "")
    };
    globalThis.window.DataverseLabelTranslator = {
        GetGrid: vi.fn(() => ({ columns: [] })),
        GetColumns: vi.fn(() => []),
        errorHandler: vi.fn()
    };
    globalThis.window.w2popup = { open: vi.fn(), close: vi.fn(), lock: vi.fn(), unlock: vi.fn() };
    globalThis.window.w2alert = vi.fn();
    globalThis.window.w2confirm = vi.fn();
    globalThis.window.w2ui = {};
    globalThis.window.DialogHelper = { alert: vi.fn() };

    globalThis.window.Helper = {
        CustomActionTypes: { Other: "Other" },
        ExecuteTypedCustomAction: vi.fn().mockResolvedValue({}),
        GetCustomActionObject: vi.fn((r) => (r && r.object) || {}),
        GetBaseLanguage: vi.fn().mockResolvedValue("1033"),
        GetTranslator: vi.fn(),
        ClearSimpleGridSearchPlaceholder: vi.fn(),
        ApplySimpleGridContainsSearch: vi.fn()
    };
    globalThis.Helper = globalThis.window.Helper;
});

await import("../js/DictionaryService.js");
const DictionaryService = globalThis.window.DictionaryService;

// ============================================================
// SplitRecordsByDictionary tests
// ============================================================
describe("DictionaryService.SplitRecordsByDictionary", () => {
    it("is a function", () => {
        expect(typeof DictionaryService.SplitRecordsByDictionary).toBe("function");
    });

    it("returns matched and unmatched records when dictionary has matches", async () => {
        const dictionaryXml = `<?xml version="1.0"?>
<dictionary version="2.0" sourceLcid="1033">
  <entries>
    <entry active="true">
      <sourceText>Hello</sourceText>
      <targets>
        <target lcid="1034">Hola</target>
      </targets>
    </entry>
    <entry active="true">
      <sourceText>World</sourceText>
      <targets>
        <target lcid="1034">Mundo</target>
      </targets>
    </entry>
  </entries>
</dictionary>`;

        // Mock DOMParser for parseDictionaryXml
        const entries = [
            { sourceText: "Hello", isActive: true, targets: { "1034": "Hola" } },
            { sourceText: "World", isActive: true, targets: { "1034": "Mundo" } }
        ];

        // Set up XML parser mock that returns entries
        const TestMockParser = class {
            parseFromString(xml) {
                return {
                    getElementsByTagName: (n) => {
                        if (n === "parsererror") return [];
                        if (n === "entry") {
                            return entries.flatMap(e => [
                                { getAttribute: () => "true", getElementsByTagName: (n2) => {
                                    if (n2 === "sourceText") return [{ textContent: e.sourceText }];
                                    if (n2 === "target") return [{ getAttribute: () => "1034", textContent: e.targets["1034"] }];
                                    return [];
                                }}
                            ]);
                        }
                        if (n === "dictionary") return [{ getAttribute: () => "1033" }];
                        return [];
                    }
                };
            }
        };
        globalThis.DOMParser = TestMockParser;

        const records = [
            { recid: 1, "1033": "Hello" },
            { recid: 2, "1033": "World" },
            { recid: 3, "1033": "Other" }
        ];

        const result = await DictionaryService.SplitRecordsByDictionary("1033", "1034", records);
        expect(result).toHaveProperty("matchedResults");
        expect(result).toHaveProperty("unmatchedRecords");
        expect(Array.isArray(result.matchedResults)).toBe(true);
        expect(Array.isArray(result.unmatchedRecords)).toBe(true);
        expect(result.matchedResults.length).toBe(2);
        expect(result.unmatchedRecords.length).toBe(1);
    });

    it("returns empty matches and all records as unmatched when fromLcid doesn't match sourceLcid", async () => {
        // Mock DOMParser
        globalThis.DOMParser = class {
            parseFromString() {
                return {
                    getElementsByTagName: (n) => {
                        if (n === "parsererror") return [];
                        if (n === "dictionary") return [{ getAttribute: () => "1033" }];
                        return [];
                    }
                };
            }
        };

        const records = [{ recid: 1, "1033": "Hello" }];
        const result = await DictionaryService.SplitRecordsByDictionary("9999", "1034", records);
        expect(result.matchedResults).toEqual([]);
        expect(result.unmatchedRecords).toEqual(records);
    });

    it("skips inactive entries and entries without matching target", async () => {
        const entries = [
            { sourceText: "Inactive", isActive: false, targets: { "1034": "X" } },
            { sourceText: "NoTarget", isActive: true, targets: {} },
            { sourceText: "Hello", isActive: true, targets: { "1034": "Hola" } }
        ];
        globalThis.DOMParser = class {
            parseFromString() {
                return {
                    getElementsByTagName: (n) => {
                        if (n === "parsererror") return [];
                        if (n === "entry") {
                            return entries.flatMap(e => [
                                { getAttribute: () => e.isActive ? "true" : "false", getElementsByTagName: (n2) => {
                                    if (n2 === "sourceText") return [{ textContent: e.sourceText }];
                                    if (n2 === "target") {
                                        const targets = Object.keys(e.targets).map(lcid => ({
                                            getAttribute: () => lcid,
                                            textContent: e.targets[lcid]
                                        }));
                                        return targets;
                                    }
                                    return [];
                                }}
                            ]);
                        }
                        if (n === "dictionary") return [{ getAttribute: () => "1033" }];
                        return [];
                    }
                };
            }
        };

        const records = [{ recid: 1, "1033": "Hello" }];
        const result = await DictionaryService.SplitRecordsByDictionary("1033", "1034", records);
        expect(result.matchedResults.length).toBe(1);
        expect(result.matchedResults[0].translation).toBe("Hola");
        expect(result.unmatchedRecords.length).toBe(0);
    });

    it("handles empty records array", async () => {
        globalThis.DOMParser = class {
            parseFromString() {
                return {
                    getElementsByTagName: (n) => {
                        if (n === "parsererror") return [];
                        if (n === "dictionary") return [{ getAttribute: () => "1033" }];
                        return [];
                    }
                };
            }
        };

        const result = await DictionaryService.SplitRecordsByDictionary("1033", "1034", []);
        expect(result.matchedResults).toEqual([]);
        expect(result.unmatchedRecords).toEqual([]);
    });
});
