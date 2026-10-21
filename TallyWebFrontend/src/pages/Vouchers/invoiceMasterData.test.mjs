import test from 'node:test';
import assert from 'node:assert/strict';
import { mergeInvoiceCompany } from './invoiceMasterData.js';

test('matched company retains all address lines and separate contact fields', () => {
  const result = mergeInvoiceCompany({ id: 1, name: 'Company', tallyGuid: 'abc', gstin: 'GST1' }, [{ name: 'Company', tallyGuid: 'abc', address: '12 Main Road\nFirst Floor\nMadurai', state: 'Tamil Nadu', phone: '111', mobile: '222', email: 'real@example.test' }], []);
  assert.equal(result.address, '12 Main Road\nFirst Floor\nMadurai');
  assert.equal(result.phone, '111'); assert.equal(result.mobile, '222');
});

test('GST address fills only a missing company address and matches the saved registration', () => {
  const company = { name: 'Company', gstin: 'GST1', state: 'Tamil Nadu' };
  const units = [{ gstin: 'GST2', address: 'Wrong branch' }, { gstin: 'GST1', address: 'Registered Road\nMadurai', mobile: '222' }];
  assert.equal(mergeInvoiceCompany(company, [], units).address, 'Registered Road\nMadurai');
  assert.equal(mergeInvoiceCompany({ ...company, address: 'Company mailing address' }, [], units).address, 'Company mailing address');
  assert.equal(mergeInvoiceCompany({ name: 'Company' }, [], units).address, undefined);
});

test('missing addresses remain missing and mismatched company identities are rejected', () => {
  const company = { name: 'Company', tallyGuid: 'abc', gstin: 'GST1', state: 'Tamil Nadu', country: 'India', email: 'contact@example.test' };
  const result = mergeInvoiceCompany(company, [{ name: 'Company', tallyGuid: 'other', address: 'Wrong company' }], [{ gstin: 'GST1', address: 'Unverified registration' }]);
  assert.equal(result.address, undefined);
  assert.equal(result.email, 'contact@example.test');
});
