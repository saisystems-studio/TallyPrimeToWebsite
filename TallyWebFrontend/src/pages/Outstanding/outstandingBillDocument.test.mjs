import test from 'node:test';
import assert from 'node:assert/strict';
import { findOutstandingBill, outstandingBillFilename, outstandingBillModel, payableBillModel, receivableBillModel, renderOutstandingBill } from './outstandingBillDocument.js';

// Synthetic test-only row; live outstanding figures are verified only from the API.
const onAccount = {
  companyId: 4, companyName: 'Example School', ledgerId: 31, ledgerName: 'மாதிரி கணக்கு',
  internalBillReference: 'On Account', billReference: 'On Account', rowType: 'OnAccount',
  balanceType: 'Receivable', status: 'On Account', originalAmount: 0, pendingAmount: 243,
  pendingDays: 0, allocations: [
    { voucherDate: '20260824', voucherNumber: 'PMP-RET-101-2627', voucherType: 'Online Sales Return', billType: 'On Account', signedAmount: 243 },
    { voucherDate: '20260822', voucherNumber: 'RCPT-7', voucherType: 'Receipt', billType: 'Agst Ref', signedAmount: -100 },
  ],
};

test('selected On Account bill retains exact identity, Tamil, pending values, and signed transaction history', () => {
  const model = outstandingBillModel(onAccount);
  assert.equal(model.companyName, 'Example School');
  assert.equal(model.ledgerName, 'மாதிரி கணக்கு');
  assert.equal(model.originalAmount, null);
  assert.equal(model.pendingAmount, 243);
  assert.deepEqual(model.transactions.map(({ debit, credit }) => [debit, credit]), [[null, 243], [100, null]]);
});

test('bill lookup requires exact company, ledger, reference, and row type', () => {
  const other = { ...onAccount, companyId: 5, ledgerId: 32, internalBillReference: 'INV-10', billReference: 'INV-10', rowType: 'Bill' };
  const selected = { ...other };
  assert.equal(findOutstandingBill([onAccount, other], selected), other);
  assert.equal(findOutstandingBill([onAccount], selected), null);
  assert.equal(findOutstandingBill([other, { ...other }], selected), null);
  assert.equal(outstandingBillFilename(outstandingBillModel(other)), 'Outstanding-INV-10.pdf');
});

test('payable statement preserves bill values and the selected Purchase allocation', () => {
  const payable = {
    companyId: 8, companyName: 'Example Mill', ledgerId: 91, ledgerName: 'Supplier A',
    internalBillReference: 'PUCH_001', billReference: 'PUCH_001', rowType: 'Bill', balanceType: 'Payable',
    originalAmount: 2500, pendingAmount: 2500, billDate: '20261002', pendingDays: 19,
    allocations: [{ voucherDate: '20261002', voucherNumber: 'PUCH_001', voucherType: 'Purchase', billType: 'New Ref', amount: 2500 }],
  };
  const model = payableBillModel(payable);
  assert.equal(model.documentTitle, 'Payable Bill Statement');
  assert.equal(model.originalAmount, 2500);
  assert.equal(model.pendingAmount, 2500);
  assert.deepEqual(model.transactions, [{ date: '20261002', voucherNumber: 'PUCH_001', voucherType: 'Purchase', billType: 'New Ref', amount: 2500 }]);
  assert.equal(outstandingBillFilename(model), 'Payable-PUCH_001.pdf');

  const drawn = [];
  const createCanvas = () => {
    const context = {
      measureText: (value) => ({ width: String(value).length * 8 }),
      fillText: (value) => drawn.push(String(value)),
      fillRect() {}, strokeRect() {}, beginPath() {}, moveTo() {}, lineTo() {}, stroke() {},
    };
    return { getContext: () => context, toDataURL: () => '' };
  };
  assert.equal(renderOutstandingBill(model, createCanvas).length, 1);
  for (const text of ['PAYABLE BILL STATEMENT', 'Example Mill', 'Party / Ledger: Supplier A', 'Bill Amount: ₹ 2,500.00', 'Pending Amount: ₹ 2,500.00', 'PUCH_001', 'Purchase', 'New Ref']) assert.ok(drawn.includes(text), `missing PDF text: ${text}`);
  assert.ok(drawn.some((text) => text.endsWith('2,500.00')));
});

test('receivable statement preserves the selected bill and Sales allocation without mixing another bill', () => {
  // Synthetic fixture matching the requested fields; this is not a live API verification.
  const receivable = {
    companyId: 9, companyName: 'PMP Rice Mill', ledgerId: 103, ledgerName: 'Lakshmi Traders',
    internalBillReference: '1', billReference: '1', rowType: 'Bill', balanceType: 'Receivable',
    originalAmount: 25000, pendingAmount: 25000, billDate: '20261002', pendingDays: 17,
    allocations: [{ voucherDate: '20261002', voucherNumber: 'SAL-1', voucherType: 'Sales', billType: 'New Ref', amount: 25000 }],
  };
  const model = receivableBillModel(receivable);
  assert.equal(model.documentTitle, 'Receivable Bill Statement');
  assert.equal(model.originalAmount, 25000);
  assert.equal(model.pendingAmount, 25000);
  assert.deepEqual(model.transactions[0], { date: '20261002', voucherNumber: 'SAL-1', voucherType: 'Sales', billType: 'New Ref', amount: 25000 });
  assert.equal(outstandingBillFilename(model), 'Receivable-1.pdf');
  assert.throws(() => receivableBillModel({ ...receivable, balanceType: 'Payable' }), /receivable bill/i);

  const drawn = [];
  const createCanvas = () => {
    const context = { measureText: (value) => ({ width: String(value).length * 8 }), fillText: (value) => drawn.push(String(value)), fillRect() {}, strokeRect() {}, beginPath() {}, moveTo() {}, lineTo() {}, stroke() {} };
    return { getContext: () => context, toDataURL: () => '' };
  };
  assert.equal(renderOutstandingBill(model, createCanvas).length, 1);
  for (const text of ['RECEIVABLE BILL STATEMENT', 'PMP Rice Mill', 'Party / Ledger: Lakshmi Traders', 'Bill Reference: 1', 'Bill Amount: ₹ 25,000.00', 'Pending Amount: ₹ 25,000.00', 'SAL-1', 'Sales']) assert.ok(drawn.includes(text), `missing PDF text: ${text}`);
});
