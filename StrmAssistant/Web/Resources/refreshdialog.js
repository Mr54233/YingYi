// YingYi refresh dialog defaults: keep a visible, accessible status message.
(function () {
    'use strict';
    document.addEventListener('dialogopen', function (event) {
        var dialog = event.target;
        if (!dialog || !dialog.querySelector) return;
        if (!dialog.querySelector('.yingyi-refresh-hint')) {
            var hint = document.createElement('p');
            hint.className = 'yingyi-refresh-hint';
            hint.textContent = 'Metadata refresh options are provided by YingYi.';
            dialog.appendChild(hint);
        }
    });
}());
