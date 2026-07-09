import { vi } from "vitest";

// Make `window` available as a global for IIFE modules that reference `window` directly.
// In Node environment, `window` is not defined by default.
// This must run at module scope (before any test file imports the IIFE sources).

// Assign globalThis to both `window` and `self` so IIFE patterns like
//   (function (X, undefined) { ... })((window.X = window.X || {}));
// resolve correctly in Node.
if (typeof globalThis.window === "undefined") {
    globalThis.window = globalThis;
}
if (typeof globalThis.self === "undefined") {
    globalThis.self = globalThis;
}

// Ensure minimal DOM-like globals used by the IIFE modules
if (typeof globalThis.document === "undefined") {
    globalThis.document = {
        querySelector: vi.fn().mockReturnValue(null),
        querySelectorAll: vi.fn().mockReturnValue([]),
        createElement: vi.fn(function (tag) {
            return {
                tagName: String(tag || "").toUpperCase(),
                innerHTML: "",
                style: {},
                children: [],
                appendChild: vi.fn(),
                addEventListener: vi.fn(),
                removeEventListener: vi.fn()
            };
        })
    };
}

if (typeof globalThis.w2popup === "undefined") {
    globalThis.w2popup = {
        open: vi.fn(),
        close: vi.fn(),
        max: vi.fn()
    };
}

if (typeof globalThis.w2ui === "undefined") {
    globalThis.w2ui = {};
}

if (typeof globalThis.w2alert === "undefined") {
    globalThis.w2alert = vi.fn();
}

if (typeof globalThis.Xrm === "undefined") {
    globalThis.Xrm = {
        WebApi: {
            online: {
                execute: vi.fn()
            }
        }
    };
}

if (typeof globalThis.parent === "undefined") {
    globalThis.parent = {};
}

// DOMParser is used by AppService.js and other modules for XML parsing
if (typeof globalThis.DOMParser === "undefined") {
    globalThis.DOMParser = class DOMParser {
        parseFromString(xmlString, contentType) {
            // Build a non-recursive mock element. The previous implementation called
            // mockElement from inside its own getElementsByTagName, producing
            // "Maximum call stack size exceeded" whenever the same tag was
            // requested more than once.
            const makeElement = (tagName, textContent) => {
                const el = {
                    tagName: tagName,
                    textContent: textContent || "",
                    childNodes: [],
                    children: [],
                    getAttribute: vi.fn().mockReturnValue(null),
                    setAttribute: vi.fn(),
                    querySelectorAll: vi.fn().mockReturnValue([]),
                    querySelector: vi.fn().mockReturnValue(null),
                    appendChild: vi.fn(),
                    cloneNode: vi.fn(function () { return makeElement(tagName, textContent); })
                };
                // Each element exposes its own getElementsByTagName.
                // It returns itself for matching tag, without recursing into makeElement.
                el.getElementsByTagName = function (name) {
                    if (name === tagName) return [el];
                    return [];
                };
                return el;
            };

            const documentElement = makeElement("root", xmlString);

            // Document-level getElementsByTagName — check if xmlString contains <name
            const getElementsByTagName = (name) => {
                if (!xmlString) return [];
                if (xmlString.indexOf("<" + name) !== -1) {
                    return [makeElement(name, "")];
                }
                return [];
            };

            return {
                documentElement: documentElement,
                querySelectorAll: vi.fn().mockReturnValue([]),
                querySelector: vi.fn().mockReturnValue(null),
                getElementsByTagName: getElementsByTagName
            };
        }
    };
}
