(function () {
    'use strict';

    var Nova = window.Nova;
    if (!Nova) return;

    function money(t) {
        var el;
        if ((el = document.getElementById('sumPrice'))) el.textContent = Nova.fmt(t.Subtotal);
        if ((el = document.getElementById('sumDiscount'))) el.textContent = t.Discount > 0 ? '\u2212' + Nova.fmt(t.Discount) : Nova.fmt(0);
        if ((el = document.getElementById('sumShip'))) el.textContent = t.Shipping == 0 ? 'FREE' : Nova.fmt(t.Shipping);
        if ((el = document.getElementById('sumTax'))) el.textContent = '\u20B9' + Number(t.Tax).toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
        if ((el = document.getElementById('sumTotal'))) el.textContent = Nova.fmt(t.Total);
        Nova.setCount('cartCount', t.Count);
    }

    function setQty(id, qty, lineEl) {
        Nova.post('/cart/update', { id: id, qty: qty }).then(function (res) {
            if (!res.ok) return;
            if (res.removed) {
                if (lineEl && lineEl.parentNode) lineEl.parentNode.removeChild(lineEl);
                Nova.toast('Item removed (out of stock quantity)');
            } else if (lineEl) {
                var input = lineEl.querySelector('.qty input');
                if (input) input.value = qty;
                var priceEl = lineEl.querySelector('.cl-price .price-now');
                if (priceEl) priceEl.textContent = Nova.fmt(res.lineTotal);
            }
            money(res.totals);
            if (res.removed && res.totals.Count === 0) window.location.reload();
        }).catch(function () { Nova.toast('Could not update cart', 'error'); });
    }

    document.addEventListener('click', function (e) {
        var step = e.target.closest ? e.target.closest('[data-step]') : null;
        if (step) {
            e.preventDefault();
            var wrap = step.closest('.qty');
            if (!wrap) return;
            var id = parseInt(wrap.getAttribute('data-id'), 10);
            var input = wrap.querySelector('input');
            var next = parseInt(input.value, 10) + parseInt(step.getAttribute('data-step'), 10);
            if (next < 1) next = 1;
            if (next > 99) next = 99;
            if (next === parseInt(input.value, 10)) return;
            setQty(id, next, step.closest('.cart-line'));
            return;
        }

        var remove = e.target.closest ? e.target.closest('[data-remove]') : null;
        if (remove) {
            e.preventDefault();
            var rid = remove.getAttribute('data-remove');
            Nova.post('/cart/remove', { id: rid }).then(function (res) {
                if (!res.ok) return;
                var line = document.querySelector('.cart-line[data-line="' + rid + '"]');
                if (line && line.parentNode) line.parentNode.removeChild(line);
                money(res.totals);
                Nova.toast('Removed from cart');
                if (res.totals.Count === 0) setTimeout(function () { window.location.reload(); }, 700);
            }).catch(function () { Nova.toast('Could not remove item', 'error'); });
            return;
        }

        var wishMove = e.target.closest ? e.target.closest('.cart-line [data-wish]') : null;
        if (wishMove) {
            e.preventDefault();
            var mwid = wishMove.getAttribute('data-wish');
            Nova.post('/wishlist/toggle', { id: mwid }).then(function (res) {
                if (res.ok) Nova.setCount('wishCount', res.count);
            });
            Nova.post('/cart/remove', { id: mwid }).then(function (res) {
                if (!res.ok) return;
                var line = document.querySelector('.cart-line[data-line="' + mwid + '"]');
                if (line && line.parentNode) line.parentNode.removeChild(line);
                money(res.totals);
                Nova.toast('Moved to wishlist');
                if (res.totals.Count === 0) setTimeout(function () { window.location.reload(); }, 700);
            }).catch(function () { Nova.toast('Could not move item', 'error'); });
            return;
        }

        var apply = e.target.closest ? e.target.closest('#applyCoupon') : null;
        if (apply) {
            e.preventDefault();
            var input = document.getElementById('couponInput');
            var msg = document.getElementById('couponMsg');
            var code = (input && input.value || '').trim();
            if (!code) { if (msg) { msg.textContent = 'Enter a coupon code first.'; msg.className = 'coupon-msg err'; } return; }
            Nova.post('/cart/applycoupon', { code: code }).then(function (res) {
                if (msg) { msg.textContent = res.message || (res.ok ? 'Coupon applied!' : 'Coupon not applicable.'); msg.className = 'coupon-msg ' + (res.ok ? 'ok' : 'err'); }
                if (res.ok) {
                    money(res.totals);
                    var applied = document.getElementById('couponApplied');
                    if (applied) {
                        applied.classList.remove('is-hidden');
                        var nm = document.getElementById('couponName');
                        if (nm) nm.textContent = res.totals.CouponCode || code;
                    }
                    Nova.toast('Coupon applied: ' + code);
                }
            }).catch(function () { Nova.toast('Could not apply coupon', 'error'); });
            return;
        }

        var removeCoupon = e.target.closest ? e.target.closest('#removeCoupon') : null;
        if (removeCoupon) {
            e.preventDefault();
            Nova.post('/cart/removecoupon', {}).then(function (res) {
                if (!res.ok) return;
                money(res.totals);
                var applied = document.getElementById('couponApplied');
                if (applied) applied.classList.add('is-hidden');
                var input = document.getElementById('couponInput');
                if (input) input.value = '';
                var msg = document.getElementById('couponMsg');
                if (msg) { msg.textContent = ''; msg.className = 'coupon-msg'; }
                Nova.toast('Coupon removed');
            }).catch(function () { Nova.toast('Could not remove coupon', 'error'); });
            return;
        }

        var hint = e.target.closest ? e.target.closest('.hint[data-code]') : null;
        if (hint) {
            e.preventDefault();
            var hi = document.getElementById('couponInput');
            if (hi) { hi.value = hint.getAttribute('data-code'); hi.focus(); }
            return;
        }

        var clear = e.target.closest ? e.target.closest('#clearCart') : null;
        if (clear) {
            e.preventDefault();
            if (!window.confirm('Remove all items from your cart?')) return;
            Nova.post('/cart/clear', {}).then(function () { window.location.reload(); })
                .catch(function () { Nova.toast('Could not clear cart', 'error'); });
        }
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && e.target && e.target.id === 'couponInput') {
            e.preventDefault();
            var btn = document.getElementById('applyCoupon');
            if (btn) btn.click();
        }
    });
})();
