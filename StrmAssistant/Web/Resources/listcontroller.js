// YingYi list controller: preserve the server supplied order during redraws.
(function () {
    'use strict';
    document.addEventListener('beforesort', function (event) {
        var list = event.target && event.target.querySelectorAll
            ? event.target.querySelectorAll('[data-yingyi-order]') : [];
        Array.prototype.forEach.call(list, function (item, index) {
            item.setAttribute('data-yingyi-order', String(index));
        });
    });
}());
