import { imagePdf } from '../Vouchers/voucherExport.js';

export function findOutstandingBill(rows, selected) {
  if (!selected?.companyId || !selected?.ledgerId || !selected?.ledgerName || !selected?.internalBillReference) return null;
  const matches = (Array.isArray(rows) ? rows : []).filter((row) =>
    String(row.companyId) === String(selected.companyId) &&
    String(row.ledgerId) === String(selected.ledgerId) &&
    row.ledgerName?.trim().toLocaleLowerCase() === selected.ledgerName.trim().toLocaleLowerCase() &&
    row.internalBillReference === selected.internalBillReference &&
    row.rowType === selected.rowType,
  );
  return matches.length === 1 ? matches[0] : null;
}

export function outstandingBillModel(row) {
  if (!row?.companyId || !row?.companyName || !row?.ledgerId || !row?.ledgerName || !row?.internalBillReference) {
    throw new Error('The selected outstanding bill is missing its company, party, or bill identity.');
  }
  const allocations = Array.isArray(row.allocations) ? row.allocations : [];
  return {
    statementKind: 'Outstanding',
    documentTitle: 'Outstanding Bill Statement',
    companyName: row.companyName,
    ledgerName: row.ledgerName,
    billReference: row.billReference || row.internalBillReference,
    billDate: row.billDate || row.voucherDate || '',
    originalAmount: row.rowType === 'OnAccount' ? null : row.originalAmount,
    pendingAmount: row.pendingAmount,
    pendingDays: row.pendingDays,
    billType: row.rowType === 'OnAccount' ? 'On Account' : row.balanceType,
    status: row.status,
    transactions: allocations.map((allocation) => ({
      date: allocation.voucherDate || '',
      voucherNumber: allocation.voucherNumber || '',
      voucherType: allocation.voucherType || '',
      billType: allocation.billType || '',
      debit: Number(allocation.signedAmount || 0) < 0 ? Math.abs(Number(allocation.signedAmount)) : null,
      credit: Number(allocation.signedAmount || 0) > 0 ? Number(allocation.signedAmount) : null,
    })),
  };
}

function settlementBillModel(row, balanceType) {
  if (!row?.companyId || !row?.companyName || !row?.ledgerId || !row?.ledgerName || !row?.internalBillReference || row.rowType !== 'Bill' || row.balanceType !== balanceType) {
    throw new Error(`The selected ${balanceType.toLowerCase()} bill is missing its company, party, or bill identity.`);
  }
  return {
    statementKind: balanceType,
    documentTitle: `${balanceType} Bill Statement`,
    companyName: row.companyName,
    ledgerName: row.ledgerName,
    billReference: row.billReference || row.internalBillReference,
    billDate: row.billDate || row.voucherDate || '',
    originalAmount: row.originalAmount,
    pendingAmount: row.pendingAmount,
    pendingDays: row.pendingDays,
    transactions: (Array.isArray(row.allocations) ? row.allocations : []).map((allocation) => ({
      date: allocation.voucherDate || '',
      voucherNumber: allocation.voucherNumber || '',
      voucherType: allocation.voucherType || '',
      billType: allocation.billType || '',
      amount: allocation.amount == null ? null : Number(allocation.amount),
    })),
  };
}

export function payableBillModel(row) { return settlementBillModel(row, 'Payable'); }
export function receivableBillModel(row) { return settlementBillModel(row, 'Receivable'); }

const PAGE_W = 1190, PAGE_H = 1684, LEFT = 60, WIDTH = 1070, BOTTOM = 1585;
const FONT = '"Nirmala UI", "Noto Sans Tamil", Latha, Arial, sans-serif';
const COLUMNS = [42, 150, 155, 195, 145, 180, 203];
const HEADERS = ['S.No', 'Date', 'Voucher No', 'Voucher Type', 'Bill Type', 'Debit', 'Credit'];
const money = (value) => value == null || !Number.isFinite(Number(value)) ? '—' : `₹ ${Number(value).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
const date = (value) => /^\d{8}$/.test(String(value || '')) ? `${value.slice(6, 8)}/${value.slice(4, 6)}/${value.slice(0, 4)}` : value || '—';

export function renderOutstandingBill(model, createCanvas = () => document.createElement('canvas')) {
  const isSettlement = model.statementKind === 'Payable' || model.statementKind === 'Receivable';
  const columns = isSettlement ? [42, 170, 170, 220, 180, 288] : COLUMNS;
  const headers = isSettlement ? ['S.No', 'Date', 'Voucher No', 'Voucher Type', 'Bill Type', 'Amount'] : HEADERS;
  const measure = createCanvas(); measure.width = PAGE_W; measure.height = PAGE_H;
  const ctx = measure.getContext('2d');
  if (!ctx) throw new Error('PDF rendering is unavailable in this browser.');
  const wrap = (value, maxWidth, size = 17, bold = false) => {
    ctx.font = `${bold ? 'bold ' : ''}${size}px ${FONT}`;
    const lines = [];
    for (const paragraph of String(value ?? '—').split('\n')) {
      let line = '';
      for (const { segment } of new Intl.Segmenter(undefined, { granularity: 'grapheme' }).segment(paragraph)) {
        if (line && ctx.measureText(line + segment).width > maxWidth) { lines.push(line); line = ''; }
        line += segment;
      }
      lines.push(line || '—');
    }
    return lines;
  };
  const rows = model.transactions.map((tx, index) => {
    const cells = isSettlement
      ? [String(index + 1), date(tx.date), tx.voucherNumber || '—', tx.voucherType || '—', tx.billType || '—', money(tx.amount)]
      : [String(index + 1), date(tx.date), tx.voucherNumber || '—', tx.voucherType || '—', tx.billType || '—', money(tx.debit), money(tx.credit)];
    const lines = cells.map((cell, col) => wrap(cell, columns[col] - 10, 15, col === 2));
    return { lines, height: Math.max(38, ...lines.map((list) => list.length * 19 + 10)) };
  });
  const totalsSpace = 52;
  const firstTop = 425, tableHeader = 48, usableFirst = BOTTOM - firstTop - tableHeader - totalsSpace;
  const usableNext = BOTTOM - 185 - tableHeader - totalsSpace;
  const pages = []; let pending = rows.slice(); let first = true;
  while (pending.length || pages.length === 0) {
    const capacity = first ? usableFirst : usableNext; const page = []; let used = 0;
    while (pending.length && used + pending[0].height <= capacity) { const row = pending.shift(); page.push(row); used += row.height; }
    if (!page.length && pending.length) throw new Error('A transaction detail is too large to fit on an A4 page.');
    pages.push(page); first = false;
  }
  const totalDebit = model.transactions.reduce((sum, tx) => sum + (Number(tx.debit) || 0), 0);
  const totalCredit = model.transactions.reduce((sum, tx) => sum + (Number(tx.credit) || 0), 0);
  const totalAmount = model.transactions.reduce((sum, tx) => sum + (Number(tx.amount) || 0), 0);
  const canvases = [];
  pages.forEach((pageRows, pageIndex) => {
    const canvas = createCanvas(); canvas.width = PAGE_W; canvas.height = PAGE_H;
    const page = canvas.getContext('2d'); page.fillStyle = '#fff'; page.fillRect(0, 0, PAGE_W, PAGE_H); page.fillStyle = '#18263d';
    const text = (value, x, y, { size = 17, bold = false, align = 'left' } = {}) => { page.font = `${bold ? 'bold ' : ''}${size}px ${FONT}`; page.textAlign = align; page.fillText(String(value ?? ''), x, y); page.textAlign = 'left'; };
    const line = (x1, y1, x2, y2) => { page.beginPath(); page.moveTo(x1, y1); page.lineTo(x2, y2); page.stroke(); };
    page.strokeStyle = '#637083'; page.lineWidth = 1;
    text(String(model.documentTitle || 'Outstanding Bill Statement').toUpperCase(), PAGE_W / 2, 57, { size: 25, bold: true, align: 'center' });
    let tableY;
    if (pageIndex === 0) {
      text(model.companyName, LEFT, 112, { size: 23, bold: true });
      text(`Party / Ledger: ${model.ledgerName}`, LEFT, 158, { size: 19, bold: true });
      text(`Bill Reference: ${model.billReference}`, LEFT, 196);
      text(`Bill Date: ${date(model.billDate)}`, LEFT, 231);
      text(`${isSettlement ? 'Bill Amount' : 'Original Amount'}: ${money(model.originalAmount)}`, LEFT, 266);
      text(`Pending Amount: ${money(model.pendingAmount)}`, LEFT, 301, { bold: true });
      text(`Pending Days: ${model.pendingDays ?? '—'}`, LEFT, 336);
      if (!isSettlement) text(`Bill Type: ${model.billType || '—'}    Status: ${model.status || '—'}`, LEFT, 371);
      tableY = firstTop;
    } else {
      text(model.companyName, LEFT, 105, { size: 21, bold: true });
      text(`${model.ledgerName}  |  Bill ${model.billReference}  |  Continued`, LEFT, 142);
      tableY = 185;
    }
    page.fillStyle = '#f1f4f8'; page.fillRect(LEFT, tableY, WIDTH, tableHeader); page.fillStyle = '#18263d';
    page.strokeRect(LEFT, tableY, WIDTH, BOTTOM - tableY);
    let x = LEFT;
    headers.forEach((header, col) => { text(header, x + columns[col] / 2, tableY + 30, { size: 15, bold: true, align: 'center' }); x += columns[col]; if (col < columns.length - 1) line(x, tableY, x, BOTTOM); });
    line(LEFT, tableY + tableHeader, LEFT + WIDTH, tableY + tableHeader);
    let y = tableY + tableHeader;
    if (model.transactions.length === 0) text('No allocation history available.', PAGE_W / 2, y + 28, { align: 'center' });
    pageRows.forEach((row) => {
      let cellX = LEFT;
      row.lines.forEach((cellLines, col) => {
        const numeric = isSettlement ? col === 5 : col === 5 || col === 6;
        cellLines.forEach((value, lineIndex) => text(value, numeric ? cellX + columns[col] - 6 : cellX + 5, y + 22 + lineIndex * 19, { size: 15, bold: col === 2, align: numeric ? 'right' : 'left' }));
        cellX += columns[col];
      });
      y += row.height; line(LEFT, y, LEFT + WIDTH, y);
    });
    if (pageIndex === pages.length - 1) {
      line(LEFT, BOTTOM - totalsSpace, LEFT + WIDTH, BOTTOM - totalsSpace);
      if (isSettlement) {
        text('Allocation total', LEFT + columns.slice(0, 5).reduce((a, b) => a + b, 0) - 8, BOTTOM - 17, { bold: true, align: 'right' });
        text(money(totalAmount), LEFT + WIDTH - 8, BOTTOM - 17, { bold: true, align: 'right' });
      } else {
        text('Transaction totals', LEFT + columns.slice(0, 5).reduce((a, b) => a + b, 0) - 8, BOTTOM - 17, { bold: true, align: 'right' });
        text(money(totalDebit), LEFT + columns.slice(0, 6).reduce((a, b) => a + b, 0) - 8, BOTTOM - 17, { bold: true, align: 'right' });
        text(money(totalCredit), LEFT + WIDTH - 8, BOTTOM - 17, { bold: true, align: 'right' });
      }
    } else text('Continued on next page', LEFT + WIDTH, BOTTOM + 25, { align: 'right' });
    text(`Page ${pageIndex + 1} of ${pages.length}`, PAGE_W / 2, 1640, { size: 15, align: 'center' });
    canvases.push(canvas);
  });
  return canvases;
}

export function outstandingBillPdf(model, createCanvas) {
  return imagePdf(renderOutstandingBill(model, createCanvas).map((canvas) => {
    const raw = atob(canvas.toDataURL('image/jpeg', 0.98).split(',')[1]);
    return Uint8Array.from(raw, (char) => char.charCodeAt(0));
  }));
}

export function outstandingBillFilename(model) {
  const safe = String(model.billReference || 'details').replace(/[^\p{L}\p{N}._-]/gu, '_');
  const prefix = model.statementKind === 'Payable' ? 'Payable' : model.statementKind === 'Receivable' ? 'Receivable' : 'Outstanding';
  return `${prefix}-${safe}.pdf`;
}
