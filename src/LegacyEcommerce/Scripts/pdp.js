(function () {
    'use strict';

    // gallery thumbnails
    var thumbs = document.getElementById('thumbs');
    var main = document.getElementById('mainImage');
    if (thumbs && main) {
        thumbs.addEventListener('click', function (e) {
            var btn = e.target.closest ? e.target.closest('.thumb') : null;
            if (!btn) return;
            var img = btn.querySelector('img');
            var mainImg = main.querySelector('img');
            if (!img || !mainImg) return;
            mainImg.src = img.src;
            var all = thumbs.querySelectorAll('.thumb');
            for (var i = 0; i < all.length; i++) all[i].classList.toggle('is-on', all[i] === btn);
        });
    }

    // tabs
    var tabs = document.getElementById('pdpTabs');
    if (tabs) {
        tabs.addEventListener('click', function (e) {
            var btn = e.target.closest ? e.target.closest('[data-tab]') : null;
            if (!btn) return;
            var name = btn.getAttribute('data-tab');
            var heads = tabs.querySelectorAll('[data-tab]');
            for (var i = 0; i < heads.length; i++) heads[i].classList.toggle('is-on', heads[i] === btn);
            var panes = tabs.querySelectorAll('.tab-pane');
            for (var j = 0; j < panes.length; j++) {
                panes[j].classList.toggle('is-on', panes[j].id === 'tab-' + name);
            }
        });
    }
})();
