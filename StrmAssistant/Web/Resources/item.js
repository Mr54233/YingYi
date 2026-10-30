// YingYi item-page affordances.
(function () {
    'use strict';
    document.addEventListener('click', function (event) {
        var button = event.target.closest && event.target.closest('[data-yingyi-action]');
        if (!button) return;
        var action = button.getAttribute('data-yingyi-action');
        if (action === 'copy-id') {
            var id = button.getAttribute('data-id');
            if (id && navigator.clipboard) navigator.clipboard.writeText(id);
        }
    });
    document.querySelectorAll('[data-id]').forEach(function (node) {
        if (!node.querySelector('.yingyi-item-id')) {
            var badge = document.createElement('span');
            badge.className = 'yingyi-item-id';
            badge.textContent = node.getAttribute('data-id');
            badge.hidden = true;
            node.appendChild(badge);
        }
    });
}());
