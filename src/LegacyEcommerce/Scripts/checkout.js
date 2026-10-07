(function () {
  'use strict';

  var form = document.getElementById('checkoutForm');
  if (!form) return;

  var fld = {
    name: document.getElementById('FullName'),
    phone: document.getElementById('Phone'),
    address: document.getElementById('AddressLine'),
    city: document.getElementById('City'),
    state: document.getElementById('State'),
    pin: document.getElementById('PostalCode'),
    sel: document.getElementById('SelectedAddressId')
  };

  function apply(radio) {
    if (!radio || !radio.checked) return;
    if (fld.sel) fld.sel.value = radio.value;
    if (!radio.hasAttribute('data-fill')) return;
    if (fld.name) fld.name.value = radio.getAttribute('data-name');
    if (fld.phone) fld.phone.value = radio.getAttribute('data-phone');
    if (fld.address) fld.address.value = radio.getAttribute('data-address');
    if (fld.city) fld.city.value = radio.getAttribute('data-city');
    if (fld.state) fld.state.value = radio.getAttribute('data-state');
    if (fld.pin) fld.pin.value = radio.getAttribute('data-pin');
  }

  form.addEventListener('change', function (e) {
    if (e.target && e.target.name === 'addressPick') {
      apply(e.target);
    }
  });
})();