// YingYi library-options editor hook.
(function () {
    'use strict';
    document.querySelectorAll('form[data-library-options], .libraryOptions').forEach(function (form) {
        form.setAttribute('data-yingyi-library-options', 'true');
        form.addEventListener('change', function () { form.setAttribute('data-yingyi-dirty', 'true'); });
    });
}());
