import test from 'node:test';
import assert from 'node:assert/strict';
import { invoiceModel, amountWords, paginateRows, parseNumber } from './invoiceDocument.js';
import { invoicePdfFilename, printInvoicePages, shareInvoicePdf } from './invoiceRenderer.js';

test('purchase uses actual supplier, retains Tamil, separates units and does not invent discount or dates', () => {
  const model = invoiceModel({ voucherType: 'Purchase', voucherNumber: 'P1', reference: 'SUP9', partyName: 'Supplier', totalAmount: '228040', items: [{ itemName: 'உளுந்தம் பருப்பு', quantity: '2,000.000 kg', rate: '114.02/kg', amount: '228040' }] }, { name: 'Company' }, { name: 'Supplier', address: 'Real address' });
  assert.equal(model.partyLabel, 'Supplier (Bill From)');
  assert.equal(model.party.address, 'Real address');
  assert.equal(model.supplierNumber, 'SUP9');
  assert.equal(model.supplierDate, '');
  assert.equal(model.consignee.name, '');
  assert.equal(model.rows[0].description, 'உளுந்தம் பருப்பு');
  assert.equal(model.rows[0].per, 'kg');
  assert.equal(model.rows[0].discount, '');
  assert.equal(model.quantityTotal, '2,000.000 kg');
  assert.equal(model.total, '₹2,28,040.00');
});

test('printing uses exactly the preview pages at A4 size and waits for every image', async () => {
  const children = []; const styles = []; let printed = false; let decoded = 0;
  const popup = { closed: false, document: { title: '', createElement: tag => tag === 'img' ? { decode: async () => { decoded++; } } : {}, head: { append: node => styles.push(node) }, body: { append: node => children.push(node) }, close() {} }, focus() {}, print() { assert.equal(decoded,2); printed = true; } };
  await printInvoicePages(['page-one','page-two'], 'Invoice 123', () => popup);
  assert.deepEqual(children.map(node => node.src), ['page-one','page-two']);
  assert.match(styles[0].textContent, /A4 portrait/);
  assert.match(styles[0].textContent, /210mm/);
  assert.equal(printed,true);
  await assert.rejects(printInvoicePages(['page'], '', () => null), /Allow popups/);
});

test('PDF sharing sends the exact generated file when supported and downloads it otherwise', async () => {
  const file = { name: 'Invoice-661.pdf', type: 'application/pdf' };
  let shared;
  let downloaded;
  assert.equal(invoicePdfFilename({ isInvoice: true, number: '661' }), 'Invoice-661.pdf');
  const result = await shareInvoicePdf(file, {
    navigatorObject: {
      canShare: ({ files }) => files[0] === file,
      share: async payload => { shared = payload.files[0]; },
    },
    fallbackDownload: value => { downloaded = value; },
  });
  assert.equal(result, 'shared');
  assert.equal(shared, file);
  assert.equal(downloaded, undefined);

  const fallback = await shareInvoicePdf(file, {
    navigatorObject: {},
    fallbackDownload: value => { downloaded = value; },
  });
  assert.equal(fallback, 'downloaded');
  assert.equal(downloaded, file);
});

test('unavailable native sharing falls back after an API error but not after user cancellation', async () => {
  const file = { name: 'Voucher-Receipt_1.pdf' };
  let downloads = 0;
  assert.equal(invoicePdfFilename({ isInvoice: false, number: 'Receipt/1' }), 'Voucher-Receipt_1.pdf');
  const fallback = await shareInvoicePdf(file, {
    navigatorObject: { canShare: () => true, share: async () => { throw new Error('Not allowed'); } },
    fallbackDownload: () => { downloads++; },
  });
  assert.equal(fallback, 'downloaded');
  assert.equal(downloads, 1);

  const cancelled = await shareInvoicePdf(file, {
    navigatorObject: { canShare: () => true, share: async () => { throw Object.assign(new Error('Cancelled'), { name: 'AbortError' }); } },
    fallbackDownload: () => { downloads++; },
  });
  assert.equal(cancelled, 'cancelled');
  assert.equal(downloads, 1);
});

test('sales labels buyer and non-invoices never acquire an INVOICE heading', () => {
  assert.equal(invoiceModel({ voucherType: 'Sales' }, {}).partyLabel, 'Buyer (Bill To)');
  for (const type of ['Receipt', 'Payment', 'Journal', 'Debit Note', 'Sales Return']) {
    const model = invoiceModel({ voucherType: type }, {});
    assert.equal(model.isInvoice, false);
    assert.ok(!model.heading.includes('INVOICE'));
  }
});

test('Indian words preserve paise, zero and negative amounts; invalid values remain unavailable', () => {
  assert.equal(amountWords('228040'), 'INR Two Lakh Twenty Eight Thousand Forty Only');
  assert.equal(amountWords('0'), 'INR Zero Only');
  assert.equal(amountWords('-12.05'), 'INR Minus Twelve and Five Paise Only');
  assert.equal(amountWords('not supplied'), '');
  assert.equal(parseNumber(''), null);
});

test('mixed quantities remain grouped by unit and supplied invoice total is never replaced by item sums', () => {
  const m = invoiceModel({ voucherType: 'Sales', totalAmount: '120', items: [{ quantity: '2 kg', amount: '50' }, { quantity: '3 Nos', amount: '50' }] }, {});
  assert.equal(m.quantityTotal, '2.000 kg + 3.000 Nos');
  assert.equal(m.total, '₹120.00');
  assert.equal(m.itemTotal, '₹100.00');
});

test('a missing quantity cannot produce a misleading partial quantity total', () => {
  const m = invoiceModel({ voucherType: 'Sales', items: [{ quantity: '2 kg' }, { quantity: '' }] }, {});
  assert.equal(m.quantityTotal, '');
});

test('a document cannot combine a voucher with another company', () => {
  assert.throws(() => invoiceModel({ companyId: 1, voucherType: 'Sales' }, { id: 2, name: 'Other company' }), /different company/i);
});

test('voucher identity and precision are preserved, including genuine Cash and optional consignee', () => {
  const voucher = { voucherType: 'Sales', voucherNumber: 'S/009', date: '20241101', partyName: 'Cash', totalAmount: '135.795', consignee: { name: 'Actual Receiver', address: 'Delivery Road' }, items: [{ itemName: 'தமிழ்', quantity: '11.000 kg', rate: '12.345/kg', amount: '135.795' }] };
  const model = invoiceModel(voucher, { name: 'Company' }, { name: 'Another ledger' });
  assert.equal(model.party.name, 'Cash');
  assert.equal(model.number, 'S/009'); assert.equal(model.date, '01-Nov-2024');
  assert.equal(model.rows[0].quantity, '11.000 kg'); assert.equal(model.rows[0].rate, '12.345');
  assert.equal(model.rows[0].amount, '135.795'); assert.equal(model.total, '₹135.795');
  assert.equal(model.consignee.name, 'Actual Receiver'); assert.equal(model.consignee.address, 'Delivery Road');
});

test('live detail fields take precedence over current ledger master and preserve supplier reference date', () => {
  const model = invoiceModel({ voucherType: 'Purchase', voucherNumber: '661', partyName: 'Supplier on voucher', partyLedgerName: 'Supplier Ledger', partyAddress: 'Voucher Street\nVoucher City', partyStateCode: '33', partyGstin: '33AGIPS9108F1ZB', state: 'Tamil Nadu', reference: '2882/24-25', referenceDate: '20241023', consignee: { name: 'Separate receiver', address: 'Ship Road', state: 'Tamil Nadu', stateCode: '33' }, items: [{ quantity: '2 kg', rate: '100/kg', discount: '5', amount: '190' }], totalAmount: '190' }, {}, { name: 'Supplier Ledger', address: 'Current master address' });
  assert.equal(model.party.name,'Supplier on voucher');
  assert.equal(model.party.address,'Voucher Street\nVoucher City');
  assert.equal(model.party.stateCode,'33');
  assert.equal(model.consignee.name,'Separate receiver');
  assert.equal(model.consignee.stateCode,'33');
  assert.equal(model.supplierNumber,'2882/24-25');
  assert.equal(model.supplierDate,'23-Oct-2024');
  assert.equal(model.rows[0].discount,'5');
});

test('pagination preserves every complete row, reserves the final footer and rejects an oversized row', () => {
  const rows = Array.from({ length: 84 }, (_, id) => ({ id, height: 32 }));
  const pages = paginateRows(rows, 200, 600, 160);
  assert.deepEqual(pages.flat(), rows);
  assert.ok(pages.length > 1);
  pages.forEach((page, index) => assert.ok(page.reduce((s, r) => s+r.height, 0) <= (index === 0 ? 200 : 600) - (index === pages.length-1 ? 160 : 0)));
  assert.throws(() => paginateRows([{ height: 700 }], 200, 600, 160), /too tall/i);
});
