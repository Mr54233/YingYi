// YingYi list enhancement: stable, opt-in DOM markers for list rows.
(function () {
    'use strict';
    function mark() {
        document.querySelectorAll('.card, .listItem, [data-type]').forEach(function (item, index) {
            if (!item.hasAttribute('data-yingyi-order')) item.setAttribute('data-yingyi-order', String(index));
        });
    }
    mark();
    window.addEventListener('viewshow', mark);
    window.addEventListener('hashchange', mark);
}());
