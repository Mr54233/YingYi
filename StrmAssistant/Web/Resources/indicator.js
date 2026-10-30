// YingYi rating and metadata indicator enhancement.
(function () {
    'use strict';
    function decorate(root) {
        (root || document).querySelectorAll('[data-id]:not([data-yingyi-indicator])').forEach(function (node) {
            node.setAttribute('data-yingyi-indicator', '1');
            var title = node.getAttribute('aria-label') || node.title;
            if (title && title.indexOf('✓') < 0) node.title = title + ' ✓';
        });
    }
    decorate(document);
    new MutationObserver(function (changes) { changes.forEach(function (change) {
        change.addedNodes.forEach(function (node) { if (node.nodeType === 1) decorate(node); });
    }); }).observe(document.body || document.documentElement, { childList: true, subtree: true });
}());
