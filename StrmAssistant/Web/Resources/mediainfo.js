// YingYi media information display hook.
(function () {
    'use strict';
    document.querySelectorAll('[data-mediainfo]').forEach(function (node) {
        node.classList.add('yingyi-mediainfo');
        node.setAttribute('data-yingyi-ready', 'true');
    });
}());
