const value = (input) => input == null || input === '' ? '—' : String(input);

export function voucherText(voucher, company) {
  const date = String(voucher.date || '');
  const lines = [value(company), 'VOUCHER DETAILS',
    `Voucher Type: ${value(voucher.voucherType)}`, `Voucher Number: ${value(voucher.voucherNumber)}`,
    `Date: ${/^\d{8}$/.test(date) ? `${date.slice(6)}/${date.slice(4,6)}/${date.slice(0,4)}` : value(date)}`,
    `Reference: ${value(voucher.reference)}`, '', 'PARTY DETAILS',
    `Party: ${value(voucher.partyName)}`, `GSTIN: ${value(voucher.partyGstin)}`,
    `State: ${value(voucher.state)}`, `Place of Supply: ${value(voucher.placeOfSupply)}`, '', 'ITEM DETAILS'];
  if (!voucher.items?.length) lines.push('No item details');
  for (const item of voucher.items || []) lines.push(`${value(item.itemName)}\nQuantity: ${value(item.quantity)}   Rate: ${value(item.rate)}   Amount: ${value(item.amount)}`);
  if (voucher.ledgerEntries?.length) {
    lines.push('', 'LEDGER DETAILS');
    for (const entry of voucher.ledgerEntries) lines.push(`${value(entry.ledgerName)}: ${value(entry.amount)}`);
  }
  lines.push('', 'NARRATION', value(voucher.narration), '', `Total Amount: ${value(voucher.totalAmount ?? voucher.amount)}`);
  return lines.join('\n');
}

export function shareUrl(channel, subject, text) {
  return channel === 'whatsapp'
    ? `https://wa.me/?text=${encodeURIComponent(text)}`
    : `mailto:?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(text)}`;
}

// Raster pages preserve the browser's Unicode font rendering, including Tamil.
export function imagePdf(pages) {
  const encoder = new TextEncoder();
  const chunks = []; const offsets = [0]; let length = 0;
  const add = (data) => { const bytes = typeof data === 'string' ? encoder.encode(data) : data; chunks.push(bytes); length += bytes.length; };
  const object = (id, body) => { offsets[id] = length; add(`${id} 0 obj\n`); body(); add('\nendobj\n'); };
  add('%PDF-1.4\n');
  object(1, () => add('<< /Type /Catalog /Pages 2 0 R >>'));
  object(2, () => add(`<< /Type /Pages /Count ${pages.length} /Kids [${pages.map((_, i) => `${3+i*3} 0 R`).join(' ')}] >>`));
  pages.forEach((jpeg, i) => {
    const id = 3+i*3;
    object(id, () => add(`<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /XObject << /PageImage ${id+1} 0 R >> >> /Contents ${id+2} 0 R >>`));
    object(id+1, () => { add(`<< /Type /XObject /Subtype /Image /Width 1190 /Height 1684 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length ${jpeg.length} >>\nstream\n`); add(jpeg); add('\nendstream'); });
    const commands = 'q 595 0 0 842 0 0 cm /PageImage Do Q';
    object(id+2, () => add(`<< /Length ${commands.length} >>\nstream\n${commands}\nendstream`));
  });
  const start = length; const count = 3+pages.length*3;
  add(`xref\n0 ${count}\n0000000000 65535 f \n`);
  for (let i=1; i<count; i++) add(`${String(offsets[i]).padStart(10,'0')} 00000 n \n`);
  add(`trailer\n<< /Size ${count} /Root 1 0 R >>\nstartxref\n${start}\n%%EOF`);
  return new Blob(chunks, { type: 'application/pdf' });
}

