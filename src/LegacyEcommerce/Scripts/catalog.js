(function () {
    'use strict';

    var Nova = window.Nova;
    var catalog = document.getElementById('catalog');
    if (!catalog || !Nova) return;

    var grid = document.getElementById('productGrid');
    var sentinel = document.getElementById('scrollSentinel');
    var loadBtn = document.getElementById('loadMoreBtn');
    var loader = document.getElementById('gridLoader');
    var pager = document.getElementById('classicPager');
    var loadWrap = document.getElementById('loadMoreWrap');
    var showing = document.getElementById('showingLabel');

    var moreUrl = catalog.getAttribute('data-more-url') || '';
    var mode = catalog.getAttribute('data-mode') || 'scroll';
    var totalPages = parseInt(catalog.getAttribute('data-totalpages'), 10) || 1;
    var page = parseInt(catalog.getAttribute('data-page'), 10) || 1;
    var total = 0;

    try { total = parseInt((showing || '').textContent.replace(/.*of\s*([\d,]+).*/, '$1').replace(/,/g, ''), 10) || 0; } catch (e) { total = 0; }

    var loading = false;
    var finished = page >= totalPages;

    function applyModeUI() {
        var isScroll = mode === 'scroll';
        catalog.classList.toggle('mode-scroll', isScroll);
        catalog.classList.toggle('mode-pages', !isScroll);
        if (pager) pager.style.display = isScroll ? 'none' : '';
        if (loadWrap) loadWrap.style.display = (isScroll && !finished) ? '' : 'none';
        if (sentinel) sentinel.style.display = isScroll ? '' : 'none';
        var ms = document.getElementById('modeScroll');
        var mp = document.getElementById('modePages');
        if (ms) ms.classList.toggle('is-on', isScroll);
        if (mp) mp.classList.toggle('is-on', !isScroll);
    }

    function updateLabel(from, to) {
        if (!showing || !from) return;
        showing.textContent = 'Showing ' + from + '\u2013' + to + ' of ' + total.toLocaleString('en-IN') + ' products';
    }

    function loadNext() {
        if (loading || finished || !grid) return;
        loading = true;
        if (loader) loader.classList.add('is-on');
        if (loadBtn) { loadBtn.disabled = true; loadBtn.textContent = 'Loading...'; }

        Nova.get(moreUrl + '&page=' + (page + 1)).then(function (res) {
            loading = false;
            if (loader) loader.classList.remove('is-on');
            if (loadBtn) { loadBtn.disabled = false; loadBtn.textContent = 'Load more products'; }

            if (res && res.html) {
                var frag = document.createElement('div');
                frag.innerHTML = res.html;
                while (frag.firstChild) grid.appendChild(frag.firstChild);
                page = res.page || (page + 1);
                total = res.total || total;
                updateLabel(res.from, res.to);
                finished = !res.hasMore;
            } else {
                finished = true;
            }
            applyModeUI();
        }).catch(function () {
            loading = false;
            if (loader) loader.classList.remove('is-on');
            if (loadBtn) { loadBtn.disabled = false; loadBtn.textContent = 'Load more products'; }
            if (Nova.toast) Nova.toast('Could not load more products.', 'error');
        });
    }

    if (loadBtn) loadBtn.addEventListener('click', loadNext);

    if (sentinel && 'IntersectionObserver' in window) {
        var io = new IntersectionObserver(function (entries) {
            if (entries[0] && entries[0].isIntersecting) loadNext();
        }, { rootMargin: '600px 0px' });
        io.observe(sentinel);
    } else if (sentinel && mode === 'scroll') {
        window.addEventListener('scroll', function () {
            var r = sentinel.getBoundingClientRect();
            if (r.top - window.innerHeight < 800) loadNext();
        }, { passive: true });
    }

    // ---------- toolbar controls ----------
    var sortSel = document.getElementById('sortSel');
    if (sortSel) {
        sortSel.addEventListener('change', function () {
            nav({ sort: sortSel.value, page: 1 });
        });
    }

    var sizeSel = document.getElementById('sizeSel');
    if (sizeSel) {
        sizeSel.addEventListener('change', function () {
            nav({ pageSize: sizeSel.value, page: 1 });
        });
    }

    function nav(patch) {
        var params = new URLSearchParams(window.location.search);
        for (var k in patch) {
            if (Object.prototype.hasOwnProperty.call(patch, k)) {
                if (patch[k] === null || patch[k] === '') params.delete(k);
                else params.set(k, patch[k]);
            }
        }
        window.location.href = window.location.pathname + '?' + params.toString();
    }

    // ---------- mode toggle ----------
    function setMode(next) {
        try { localStorage.setItem('NK.mode', next); } catch (e) { }
        nav({ mode: next, page: 1 });
    }
    var msBtn = document.getElementById('modeScroll');
    var mpBtn = document.getElementById('modePages');
    if (msBtn) msBtn.addEventListener('click', function () { if (mode !== 'scroll') setMode('scroll'); });
    if (mpBtn) mpBtn.addEventListener('click', function () { if (mode !== 'pages') setMode('pages'); });

    // apply stored preference when the URL did not specify a mode
    try {
        var stored = localStorage.getItem('NK.mode');
        if (stored && stored !== mode && window.location.search.indexOf('mode=') < 0) {
            nav({ mode: stored });
        }
    } catch (e) { }

    applyModeUI();
})();
