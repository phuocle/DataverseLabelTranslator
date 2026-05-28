(function (root) {
    "use strict";

    root.PLRibbonTest = root.PLRibbonTest || {};

    root.PLRibbonTest.enable = function () {
        return true;
    };

    root.PLRibbonTest.onClick = function () {
        if (root.Xrm && root.Xrm.Navigation && root.Xrm.Navigation.openAlertDialog) {
            return root.Xrm.Navigation.openAlertDialog({
                text: "PL ribbon test button clicked.",
                title: "PL Ribbon Test"
            });
        }

        return null;
    };
}(window));
