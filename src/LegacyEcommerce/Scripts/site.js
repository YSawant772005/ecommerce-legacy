(function () {
    'use strict';

    var Nova = window.Nova = window.Nova || {};

    Nova.fmt = function (n) {
        if (n === null || n === undefined || isNaN(n)) n = 0;
        return '\u20B9' + Math.round(Number(n)).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    };

    Nova.toast = function (msg, type) {
        var root = document.getElementById('toastRoot');
        if (!root) return;
        var el = document.createElement('div');
        el.className = 'toast ' + (type === 'error' ? 'err' : 'ok');
        el.textContent = msg;
        root.appendChild(el);
        setTimeout(function () {
            el.classList.add('out');
            setTimeout(function () { if (el.parentNode) el.parentNode.removeChild(el); }, 320);
        }, 2600);
    };

    Nova.post = function (url, data) {
        var body = [];
        for (var k in data) {
            if (Object.prototype.hasOwnProperty.call(data, k)) {
                body.push(encodeURIComponent(k) + '=' + encodeURIComponent(data[k]));
            }
        }
        return fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
            body: body.join('&'),
            credentials: 'same-origin'
        }).then(function (r) { return r.json(); });
    };

    Nova.get = function (url) {
        return fetch(url, { credentials: 'same-origin' }).then(function (r) { return r.json(); });
    };

    function setCount(id, n) {
        var el = document.getElementById(id);
        if (!el) return;
        el.textContent = n;
        el.classList.toggle('is-empty', !n);
        el.classList.remove('bump');
        void el.offsetWidth;
        el.classList.add('bump');
    }
    Nova.setCount = setCount;

    document.addEventListener('click', function (e) {
        var addBtn = e.target.closest ? e.target.closest('[data-add]') : null;
        if (addBtn && !addBtn.disabled) {
            e.preventDefault();
            var id = addBtn.getAttribute('data-add');
            addBtn.classList.add('is-busy');
            Nova.post('/cart/add', { id: id, qty: 1 }).then(function (res) {
                addBtn.classList.remove('is-busy');
                if (!res.ok) { Nova.toast(res.error || 'Could not add to cart', 'error'); return; }
                setCount('cartCount', res.count);
                window.__CART_COUNT__ = res.count;
                Nova.toast('Added to cart: ' + res.name);
                addBtn.classList.add('is-done');
                setTimeout(function () { addBtn.classList.remove('is-done'); }, 900);
            }).catch(function () {
                addBtn.classList.remove('is-busy');
                Nova.toast('Something went wrong. Please try again.', 'error');
            });
            return;
        }

        var buyBtn = e.target.closest ? e.target.closest('[data-buy]') : null;
        if (buyBtn && !buyBtn.disabled) {
            e.preventDefault();
            var buyId = buyBtn.getAttribute('data-buy');
            buyBtn.classList.add('is-busy');
            Nova.post('/cart/add', { id: buyId, qty: 1 }).then(function (res) {
                buyBtn.classList.remove('is-busy');
                if (!res.ok) { Nova.toast(res.error || 'Could not add to cart', 'error'); return; }
                setCount('cartCount', res.count);
                window.location.href = '/checkout';
            }).catch(function () {
                buyBtn.classList.remove('is-busy');
                Nova.toast('Something went wrong. Please try again.', 'error');
            });
            return;
        }

        var wishBtn = e.target.closest ? e.target.closest('[data-wish]') : null;
        if (wishBtn) {
            e.preventDefault();
            if (wishBtn.closest && wishBtn.closest('.cart-line')) return; // cart.js owns move-to-wishlist
            var wid = wishBtn.getAttribute('data-wish');
            Nova.post('/wishlist/toggle', { id: wid }).then(function (res) {
                if (!res.ok) { Nova.toast('Could not update wishlist', 'error'); return; }
                setCount('wishCount', res.count);
                var all = document.querySelectorAll('[data-wish="' + wid + '"]');
                for (var i = 0; i < all.length; i++) {
                    all[i].classList.toggle('is-on', res.on);
                }
                Nova.toast(res.on ? 'Saved to wishlist' : 'Removed from wishlist');
                if (!res.on && window.location.pathname.indexOf('/wishlist') === 0) {
                    setTimeout(function () { window.location.reload(); }, 500);
                }
            }).catch(function () {
                Nova.toast('Could not update wishlist', 'error');
            });
        }
    });

    // ---------- header shadow ----------
    var header = document.getElementById('siteHeader');
    if (header) {
        var onScroll = function () {
            header.classList.toggle('is-stuck', window.scrollY > 8);
        };
        window.addEventListener('scroll', onScroll, { passive: true });
        onScroll();
    }

    // ---------- search suggestions ----------
    var input = document.getElementById('searchInput');
    var box = document.getElementById('suggestBox');
    if (input && box) {
        var timer = null;
        var active = -1;
        var items = [];

        var close = function () { box.hidden = true; box.innerHTML = ''; active = -1; items = []; };

        var render = function (list) {
            if (!list.length) { close(); return; }
            items = list;
            var html = '';
            for (var i = 0; i < list.length; i++) {
                var it = list[i];
                html += '<a class="suggest-item" href="/product/' + encodeURIComponent(it.slug) + '">' +
                    '<img src="' + it.image + '" alt="" />' +
                    '<span><span class="s-name">' + it.name + '</span><span class="s-meta">' + it.category + '</span></span>' +
                    '<span class="s-price">' + Nova.fmt(it.price) + '</span></a>';
            }
            box.innerHTML = html;
            box.hidden = false;
            active = -1;
        };

        input.addEventListener('input', function () {
            var q = input.value.trim();
            clearTimeout(timer);
            if (q.length < 2) { close(); return; }
            timer = setTimeout(function () {
                Nova.get('/products/suggest?q=' + encodeURIComponent(q)).then(function (res) {
                    render(res.items || []);
                }).catch(function () { close(); });
            }, 160);
        });

        input.addEventListener('keydown', function (e) {
            if (box.hidden) return;
            var nodes = box.querySelectorAll('.suggest-item');
            if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
                e.preventDefault();
                if (!nodes.length) return;
                active += (e.key === 'ArrowDown' ? 1 : -1);
                if (active < 0) active = nodes.length - 1;
                if (active >= nodes.length) active = 0;
                for (var i = 0; i < nodes.length; i++) nodes[i].classList.toggle('is-active', i === active);
            } else if (e.key === 'Enter') {
                if (active >= 0 && nodes[active]) {
                    e.preventDefault();
                    window.location.href = nodes[active].getAttribute('href');
                } else {
                    close();
                }
            } else if (e.key === 'Escape') {
                close();
            }
        });

        document.addEventListener('click', function (e) {
            if (!box.hidden && !box.contains(e.target) && e.target !== input) close();
        });
    }
})();
