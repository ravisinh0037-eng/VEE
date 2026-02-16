var GenerateQuotation = GenerateQuotation || {};

GenerateQuotation.Form = (function () {

    function onOptionChange(executionContext) {
        var formContext = executionContext.getFormContext();

        var optionAttr = formContext.getAttribute("cr4ee_option");
        if (!optionAttr) return;

        var optionValue = optionAttr.getValue();

        // Clear fields if lookup is removed
        if (!optionValue || optionValue.length === 0) {
            formContext.getAttribute("cr4ee_shorttext").setValue(null);
            formContext.getAttribute("cr4ee_listprice").setValue(null);
            return;
        }

        var optionId = optionValue[0].id.replace("{", "").replace("}", "");

        Xrm.WebApi.retrieveRecord(
            "cr4ee_productoption",
            optionId,
            "?$select=cr4ee_shorttext,cr4ee_listprice"
        ).then(
            function success(result) {
                formContext.getAttribute("cr4ee_shorttext")
                    .setValue(result.cr4ee_shorttext || null);

                if (result.cr4ee_listprice) {
                    formContext.getAttribute("cr4ee_listprice")
                        .setValue(result.cr4ee_listprice);
                }
            },
            function error(err) {
                console.error("GenerateQuotation error:", err.message);
            }
        );
    }

    // EURO PREFIX FUNCTION
    function onEuroChange(executionContext) {
        var formContext = executionContext.getFormContext();
        var euroAttr = formContext.getAttribute("cr4ee_euro");

        if (!euroAttr) return;

        var value = euroAttr.getValue();
        if (!value) return;

        value = value.replace("€", "").trim();

        var numericPattern = /^\d+(\.\d{1,2})?$/;

        if (!numericPattern.test(value)) {
            euroAttr.setValue(null);

            formContext.getControl("cr4ee_euro").setNotification(
                "Please enter numbers only (example: 100 or 99.99).",
                "euro_error"
            );
            return;
        }

        //Clear previous error if valid
        formContext.getControl("cr4ee_euro").clearNotification("euro_error");

        //Format display with €
        euroAttr.setValue("€" + value);
    }

    // Public methods
    return {
        OnOptionChange: onOptionChange,
        OnEuroChange: onEuroChange
    };

})();
