import test from 'node:test';
import assert from 'node:assert/strict';
import { voucherText, imagePdf, shareUrl } from './voucherExport.js';

test('export preserves voucher content, zero total and Unicode without recalculating', () => {
  const text = voucherText({ voucherType: 'Sales', voucherNumber: 'S/7', date: '20240402', partyName: 'தமிழ் Store', partyGstin: 'GST123', items: [{ itemName: 'Rice', quantity: 2, rate: 50, amount: 100 }], narration: '<paid>\nThank you', totalAmount: 0, amount: 100 }, 'Company A');
  for (const value of ['Company A', 'Sales', 'S/7', '02/04/2024', 'தமிழ் Store', 'GST123', 'Rice', '2', '50', '100', '<paid>', 'Total Amount: 0']) assert.ok(text.includes(value), value);
});

test('share URLs preserve special characters and Unicode without attaching files', () => {
  const whatsapp = new URL(shareUrl('whatsapp', 'Company & Co', 'தமிழ்\nAmount: ₹100 & paid?'));
  assert.equal(whatsapp.origin, 'https://wa.me');
  assert.equal(whatsapp.searchParams.get('text'), 'தமிழ்\nAmount: ₹100 & paid?');
  const email = new URL(shareUrl('email', 'Sales #1 & Co', 'Amount: ₹100\nItems: A&B'));
  assert.equal(email.protocol, 'mailto:');
  assert.equal(email.searchParams.get('subject'), 'Sales #1 & Co');
  assert.equal(email.searchParams.get('body'), 'Amount: ₹100\nItems: A&B');
  assert.equal(email.searchParams.has('attachment'), false);
});

test('PDF has one image page per supplied page and valid byte offsets', async () => {
  const pdf = imagePdf([new Uint8Array([255,216,255,217]), new Uint8Array([255,216,255,217])]);
  const bytes = new Uint8Array(await pdf.arrayBuffer());
  const text = new TextDecoder('latin1').decode(bytes);
  assert.ok(text.startsWith('%PDF-1.4'));
  assert.match(text, /\/Count 2/);
  const offset = Number(text.match(/startxref\n(\d+)/)[1]);
  assert.equal(new TextDecoder().decode(bytes.slice(offset, offset + 4)), 'xref');
  assert.match(text, /%%EOF/);
});
