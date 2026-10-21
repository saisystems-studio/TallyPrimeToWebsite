import test from 'node:test';
import assert from 'node:assert/strict';
import { getDayBookInvoiceContext } from './dayBookInvoice.js';
import { invoiceModel } from '../Vouchers/invoiceDocument.js';
import { invoicePdfFilename } from '../Vouchers/invoiceRenderer.js';

const company = {
  id: 24,
  name: 'JAYA JOTHI MALIGAI',
  address: '318, East Masi Street\nMadurai',
  state: 'Tamil Nadu',
  gstin: '33AFHPB5979B1ZO',
};

test('Day Book Purchase 661 exports the selected purchase detail with supplier reference and consignee', () => {
  const row = { companyId: 24, companyName: company.name, tallyGuid: '5bd36bbb-820e-4784-b692-fe9891019a11-000075f0', voucherNumber: '661', voucherType: 'Purchase' };
  const detail = {
    guid: '5bd36bbb-820e-4784-b692-fe9891019a11-000075f0', voucherNumber: '661', voucherType: 'Purchase', date: '20241101',
    partyName: 'Shri Amman Dhall Mill', partyLedgerName: 'Shri Amman Dhall Mill',
    partyAddress: 'B/7/269/3-Bye-Pass Road,\nUnjampatty\nTheni', partyGstin: '33AGIPS9108F1ZB',
    reference: '2882/24-25', referenceDate: '20241023', totalAmount: '228040.00',
    ledgerEntries: [{ ledgerName: 'Shri Amman Dhall Mill', amount: '228040.00' }, { ledgerName: 'GST EXEMPTED PURCHASE', amount: '-228040.00' }],
    consignee: { name: 'JAYA JOTHI MALIGAI', address: '318, East Masi Street\nMadurai', gstin: '33AFHPB5979B1ZO', state: 'Tamil Nadu', pincode: '625001' },
    items: [{ itemName: 'உளுந்தம் பருப்பு', quantity: '2000.000 kg', rate: '114.02/kg', amount: '228040.00', discount: '0' }],
  };
  const context = getDayBookInvoiceContext(row, detail, [company]);
  assert.deepEqual(invoiceModel(context.voucher, context.company).taxEntries, [], 'Existing Vouchers preview keeps its default output');
  const model = invoiceModel(context.voucher, context.company, {}, { includeTaxLedgerEntries: true });
  assert.equal(model.number, '661');
  assert.equal(model.party.name, 'Shri Amman Dhall Mill');
  assert.equal(model.supplierNumber, '2882/24-25');
  assert.equal(model.supplierDate, '23-Oct-2024');
  assert.equal(model.consignee.name, 'JAYA JOTHI MALIGAI');
  assert.equal(model.rows[0].description, 'உளுந்தம் பருப்பு');
  assert.equal(model.rows[0].quantity, '2000.000 kg');
  assert.equal(model.rows[0].amount, '2,28,040.00');
  assert.equal(model.total, '₹2,28,040.00');
  assert.deepEqual(model.taxEntries, [{ name: 'GST EXEMPTED PURCHASE', amount: '-₹2,28,040.00' }]);
  assert.equal(invoicePdfFilename(model), 'Invoice-661.pdf');
});

test('Day Book Sales 1 exports the selected buyer and exact item and total', () => {
  const row = { companyId: 24, companyName: company.name, tallyGuid: '5bd36bbb-820e-4784-b692-fe9891019a11-00006be2', voucherNumber: '1', voucherType: 'Sales' };
  const detail = {
    guid: '5bd36bbb-820e-4784-b692-fe9891019a11-00006be2', voucherNumber: '1', voucherType: 'Sales', date: '20240401',
    partyName: 'AgSundry Parties', partyLedgerName: 'AgSundry Parties', state: 'Tamil Nadu',
    totalAmount: '4180.00', consignee: { name: 'AgSundry Parties', state: 'Tamil Nadu', country: 'India' },
    ledgerEntries: [{ ledgerName: 'AgSundry Parties', amount: '-4180.00' }, { ledgerName: 'GST EXEMPTED SALES', amount: '4180.00' }],
    items: [{ itemName: 'Tholi Ulunthu Payaru', quantity: '50.000 kg =  1 bg', rate: '83.60/kg', amount: '4180.00', discount: '0' }],
  };
  const context = getDayBookInvoiceContext(row, detail, [company]);
  const model = invoiceModel(context.voucher, context.company, {}, { includeTaxLedgerEntries: true });
  assert.equal(model.number, '1');
  assert.equal(model.partyLabel, 'Buyer (Bill To)');
  assert.equal(model.party.name, 'AgSundry Parties');
  assert.equal(model.consignee.name, 'AgSundry Parties');
  assert.equal(model.rows[0].description, 'Tholi Ulunthu Payaru');
  assert.equal(model.rows[0].quantity, '50.000 kg =  1 bg');
  assert.equal(model.rows[0].rate, '83.60');
  assert.equal(model.total, '₹4,180.00');
  assert.deepEqual(model.taxEntries, [{ name: 'GST EXEMPTED SALES', amount: '₹4,180.00' }]);
  assert.equal(invoicePdfFilename(model), 'Invoice-1.pdf');
});

test('Day Book export rejects a different voucher GUID or company', () => {
  const row = { companyId: 24, companyName: company.name, tallyGuid: 'selected-guid' };
  assert.throws(() => getDayBookInvoiceContext(row, { guid: 'other-guid' }, [company]), /did not match/i);
  assert.throws(() => getDayBookInvoiceContext(row, { guid: 'selected-guid' }, [{ ...company, id: 25 }]), /company could not be matched/i);
});
