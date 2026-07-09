import { describe, it, expect, vi, beforeEach } from "vitest";

// --- Setup globals for DialogHelper IIFE ---
const mockW2Popup = {
    open: vi.fn(),
    close: vi.fn(),
    max: vi.fn()
};

beforeEach(() => {
    globalThis.w2popup = mockW2Popup;
    globalThis.w2ui = {};
    globalThis.window.w2popup = mockW2Popup;
    globalThis.window.w2ui = {};
    globalThis.window.DialogHelper = undefined;

    mockW2Popup.open.mockReset();
    mockW2Popup.close.mockReset();
});

// Load DialogHelper.js
await import("../js/DialogHelper.js");

const DialogHelper = globalThis.window.DialogHelper;

// ============================================================
// escapeHtml (tested indirectly through buildBody)
// ============================================================
describe("DialogHelper internal escapeHtml", () => {
    it("escapes ampersand in alert message", () => {
        DialogHelper.alert("A & B");
        var callArgs = mockW2Popup.open.mock.calls[0][0];
        expect(callArgs.body).toContain("&amp;");
        expect(callArgs.body).toContain("A &amp; B");
    });

    it("escapes less-than in confirm message", () => {
        DialogHelper.confirm("A < B");
        var callArgs = mockW2Popup.open.mock.calls[0][0];
        expect(callArgs.body).toContain("&lt;");
    });

    it("escapes greater-than in message", () => {
        DialogHelper.alert("A > B");
        var callArgs = mockW2Popup.open.mock.calls[0][0];
        expect(callArgs.body).toContain("&gt;");
    });

    it("converts newline to br in message", () => {
        DialogHelper.alert("Line1\nLine2");
        var callArgs = mockW2Popup.open.mock.calls[0][0];
        expect(callArgs.body).toContain("<br/>");
    });
});

// ============================================================
// alert
// ============================================================
describe("DialogHelper.alert", () => {
    it("opens popup with alert type and default title", () => {
        DialogHelper.alert("Test message");
        expect(mockW2Popup.open).toHaveBeenCalled();

        var callArgs = mockW2Popup.open.mock.calls[0][0];
        expect(callArgs.title).toBe("Information");
        expect(callArgs.body).toContain("Test message");
        expect(callArgs.body).toContain("xqt-dialog-icon-alert");
        expect(callArgs.modal).toBe(true);
    });

    it("uses custom title from options", () => {
        DialogHelper.alert("Test", { title: "Custom Title" });
        var callArgs = mockW2Popup.open.mock.calls[0][0];
        expect(callArgs.title).toBe("Custom Title");
    });

    it("uses default width and height", () => {
        DialogHelper.alert("Test");
        var callArgs = mockW2Popup.open.mock.calls[0][0];
        expect(callArgs.width).toBe(450);
        expect(callArgs.height).toBe(220);
    });

    it("uses custom width and height from options", () => {
        DialogHelper.alert("Test", { width: 600, height: 300 });
        var callArgs = mockW2Popup.open.mock.calls[0][0];
        expect(callArgs.width).toBe(600);
        expect(callArgs.height).toBe(300);
    });

    it("returns a promise", () => {
        var result = DialogHelper.alert("Test");
        expect(result).toBeInstanceOf(Promise);
    });
});

// ============================================================
// confirm
// ============================================================
describe("DialogHelper.confirm", () => {
    it("opens popup with confirm type and default title", () => {
        DialogHelper.confirm("Are you sure?");
        var callArgs = mockW2Popup.open.mock.calls[0][0];
        expect(callArgs.title).toBe("Confirm");
        expect(callArgs.body).toContain("Are you sure?");
        expect(callArgs.body).toContain("xqt-dialog-icon-confirm");
    });

    it("includes Yes and No buttons by default", () => {
        DialogHelper.confirm("Confirm?");
        var callArgs = mockW2Popup.open.mock.calls[0][0];
        expect(callArgs.buttons).toContain("Yes");
        expect(callArgs.buttons).toContain("No");
    });

    it("uses custom yes/no text from options", () => {
        DialogHelper.confirm("Confirm?", { yesText: "Oui", noText: "Non" });
        var callArgs = mockW2Popup.open.mock.calls[0][0];
        expect(callArgs.buttons).toContain("Oui");
        expect(callArgs.buttons).toContain("Non");
    });

    it("uses custom title from options", () => {
        DialogHelper.confirm("Test", { title: "Warning" });
        var callArgs = mockW2Popup.open.mock.calls[0][0];
        expect(callArgs.title).toBe("Warning");
    });

    it("returns a promise", () => {
        var result = DialogHelper.confirm("Test");
        expect(result).toBeInstanceOf(Promise);
    });
});

// ============================================================
// question
// ============================================================
describe("DialogHelper.question", () => {
    it("opens popup with question type and custom buttons", () => {
        var buttons = [
            { text: "Option A", value: "a" },
            { text: "Option B", value: "b" }
        ];
        DialogHelper.question("Choose one", buttons);
        var callArgs = mockW2Popup.open.mock.calls[0][0];
        expect(callArgs.title).toBe("Question");
        expect(callArgs.buttons).toContain("Option A");
        expect(callArgs.buttons).toContain("Option B");
    });

    it("uses custom title from options", () => {
        DialogHelper.question("Pick", [{ text: "OK", value: "ok" }], { title: "Pick One" });
        var callArgs = mockW2Popup.open.mock.calls[0][0];
        expect(callArgs.title).toBe("Pick One");
    });

    it("returns a promise", () => {
        var result = DialogHelper.question("Test", [{ text: "OK", value: "ok" }]);
        expect(result).toBeInstanceOf(Promise);
    });
});

// ============================================================
// _resolve callback
// ============================================================
describe("DialogHelper._resolve", () => {
    it("is null after dialog closes", () => {
        DialogHelper.alert("Test");
        var onClose = mockW2Popup.open.mock.calls[0][0].onClose;
        onClose();
        expect(DialogHelper._resolve).toBeNull();
    });
});

// ============================================================
// ShowAbout
// ============================================================
describe("DialogHelper.ShowAbout", () => {
    it("opens popup with About title", () => {
        DialogHelper.ShowAbout();
        expect(mockW2Popup.open).toHaveBeenCalled();
        var callArgs = mockW2Popup.open.mock.calls[0][0];
        expect(callArgs.title).toBe("About");
        expect(callArgs.body).toContain("Dataverse Label Translator");
    });

    it("sets correct dimensions", () => {
        DialogHelper.ShowAbout();
        var callArgs = mockW2Popup.open.mock.calls[0][0];
        expect(callArgs.width).toBe(580);
        expect(callArgs.height).toBe(310);
    });
});

// ============================================================
// ShowHelp
// ============================================================
describe("DialogHelper.ShowHelp", () => {
    it("opens popup with Translation Guide title", () => {
        DialogHelper.ShowHelp();
        expect(mockW2Popup.open).toHaveBeenCalled();
        var callArgs = mockW2Popup.open.mock.calls[0][0];
        expect(callArgs.title).toBe("Translation Guide");
        expect(callArgs.body).toContain("Dataverse Label Translator");
    });

    it("contains type details", () => {
        DialogHelper.ShowHelp();
        var callArgs = mockW2Popup.open.mock.calls[0][0];
        expect(callArgs.body).toContain("Attributes");
        expect(callArgs.body).toContain("Option Sets");
        expect(callArgs.body).toContain("Forms");
    });
});
