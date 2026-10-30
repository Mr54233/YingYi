// YingYi linked-item affordance for related media cards.
(function () {
    'use strict';
    document.querySelectorAll('[data-linked-items]').forEach(function (container) {
        container.classList.add('yingyi-linked-items');
        container.setAttribute('aria-label', container.getAttribute('aria-label') || 'Related items');
    });
}());
