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

    // --- variant picker ---
    var picker = document.getElementById('variantPicker');
    if (picker) {
        var SWATCHES = {
            black: '#1f2937', white: '#ffffff', silver: '#cbd5e1', navy: '#1e3a5f',
            red: '#dc2626', blue: '#2563eb', green: '#16a34a', violet: '#7c3aed',
            purple: '#9333ea', pink: '#ec4899', rose: '#e11d48', gold: '#d4a017',
            yellow: '#eab308', orange: '#ea580c', teal: '#0d9488', cyan: '#06b6d4',
            brown: '#92400e', beige: '#d6c6a7', grey: '#6b7280', gray: '#6b7280',
            charcoal: '#374151', maroon: '#800000', indigo: '#4338ca', ivory: '#f5f0e1',
            cream: '#f3e6c8', copper: '#b87333', turquoise: '#2ec4b6', 'burnt orange': '#c2410c',
            'forest green': '#166534', sky: '#38bdf8'
        };
        var normalize = function (s) { return (s || '').trim().toLowerCase(); };
        var byLabel = function (cls) {
            var el = picker.querySelector('.' + cls + '.is-selected');
            return el ? el.getAttribute('data-label') : null;
        };
        var renderPick = function () {
            var parts = [];
            var c = byLabel('swatch-opt');
            var s = byLabel('size-opt');
            if (c) parts.push(c);
            if (s) parts.push(s);
            var text = document.getElementById('variantPick');
            if (text) text.textContent = parts.length ? parts.join(' \u00B7 ') : 'Choose your variant';
        };
        var onClick = function (cls) {
            return function (e) {
                var btn = e.target.closest ? e.target.closest('.' + cls) : null;
                if (!btn) return;
                var all = picker.querySelectorAll('.' + cls);
                for (var i = 0; i < all.length; i++) all[i].classList.toggle('is-selected', all[i] === btn);
                renderPick();
            };
        };
        picker.addEventListener('click', onClick('swatch-opt'));
        picker.addEventListener('click', onClick('size-opt'));

        var swatches = picker.querySelectorAll('.swatch-opt');
        for (var k = 0; k < swatches.length; k++) {
            var name = normalize(swatches[k].getAttribute('data-swatch'));
            var hex = SWATCHES[name] || '#e5e7eb';
            swatches[k].style.setProperty('--c', hex);
            if (name === 'white' || name === 'ivory' || name === 'cream' || name === 'silver' || name === 'beige') {
                swatches[k].classList.add('is-light');
            }
        }
        renderPick();
    }
})();
