import { parseSalesDate } from "../SalesRegister/salesRegisterModel";

export function purchaseMovementAmount(voucher) {
  const debit = Number(voucher?.debit), credit = Number(voucher?.credit);
  return Number.isFinite(debit) && Number.isFinite(credit) ? debit - credit : null;
}

export function purchaseRegisterTotals(rows) {
  if (rows.some(row => !Number.isFinite(Number(row.debit)) || !Number.isFinite(Number(row.credit))))
    return { debit: null, credit: null, closing: null };
  const debit = rows.reduce((sum, row) => sum + Number(row.debit), 0);
  const credit = rows.reduce((sum, row) => sum + Number(row.credit), 0);
  return { debit, credit, closing: debit - credit };
}

export function purchaseClosingBalance(value) {
  return value == null ? null : { amount: Math.abs(value), side: value > 0 ? "Dr" : value < 0 ? "Cr" : "" };
}

export function purchaseRegisterMonths(rows, { from = "", to = "", error } = {}) {
  const start = parseSalesDate(from), end = parseSalesDate(to);
  if (error || ((from || to) && (!start || !end || start > end))) return [];
  const keyOf = date => `${date.getFullYear()}${String(date.getMonth() + 1).padStart(2, "0")}`;
  const grouped = new Map();
  if (start) {
    const cursor = new Date(start); cursor.setDate(1);
    while (cursor <= end) { grouped.set(keyOf(cursor), []); cursor.setMonth(cursor.getMonth() + 1); }
  }
  for (const row of rows) {
    const date = parseSalesDate(row.date);
    if (!date || (start && (date < start || date > end))) continue;
    const key = keyOf(date); if (!grouped.has(key)) grouped.set(key, []); grouped.get(key).push(row);
  }
  return [...grouped.entries()].sort(([a], [b]) => a.localeCompare(b)).map(([key, entries]) => {
    const date = parseSalesDate(`${key}01`);
    const ordered = [...entries].sort((a, b) => parseSalesDate(a.date) - parseSalesDate(b.date));
    return { key, label: date.toLocaleDateString("en-IN", { month: "long", year: "numeric" }), entries: ordered };
  });
}

export function storedPurchaseDetail(selected, companyId) {
  if (!selected || String(selected.companyId) !== String(companyId) || !selected.guid || !parseSalesDate(selected.date))
    throw new Error("Stored Purchase voucher does not match the selected company and entry.");
  const postings = Array.isArray(selected.postings) ? [...selected.postings].sort((a, b) => a.entryIndex - b.entryIndex) : [];
  if (!postings.length || postings.some((p, i) => p.entryIndex !== i || !p.ledgerGuid || !p.ledgerName || !Number.isFinite(Number(p.amount))))
    throw new Error("Stored Purchase ledger postings or sequence are invalid.");
  if (Math.abs(postings.reduce((sum, p) => sum + Number(p.amount), 0)) > 0.00005)
    throw new Error("Stored Purchase voucher postings do not balance.");
  const purchaseRows = postings.filter(p => p.isPurchaseLedger);
  const debit = purchaseRows.reduce((sum, p) => sum + (Number(p.amount) < 0 ? -Number(p.amount) : 0), 0);
  const credit = purchaseRows.reduce((sum, p) => sum + (Number(p.amount) > 0 ? Number(p.amount) : 0), 0);
  if (Math.abs(debit - Number(selected.debit)) > 0.005 || Math.abs(credit - Number(selected.credit)) > 0.005)
    throw new Error("Stored Purchase account debit/credit totals do not reconcile.");
  return { ...selected, totalAmount: Number(selected.amount), ledgerEntries: postings.map(p => ({ ...p, amount: Number(p.amount) })) };
}

const escape = value => String(value ?? "").replace(/[&<>"']/g, c => ({ "&":"&amp;", "<":"&lt;", ">":"&gt;", '"':"&quot;", "'":"&#39;" }[c]));
export const storedPurchasePostingAmount = value => Number(value).toLocaleString("en-IN", { useGrouping: false, minimumFractionDigits: 2, maximumFractionDigits: 4 });
export function storedPurchaseVoucherHtml(voucher, companyName) {
  return `<html><meta charset="utf-8"><title>Stored Purchase Voucher</title><h1>Purchase Voucher ${escape(voucher.voucherNumber)}</h1><p>${escape(companyName)}</p><p>${escape(voucher.date)} | ${escape(voucher.voucherType)} | ${escape(voucher.partyName)}</p><p>${escape(voucher.narration)}</p><table><tr><th>Sequence</th><th>Ledger</th><th>Signed amount</th></tr>${voucher.ledgerEntries.map(p => `<tr><td>${p.entryIndex + 1}</td><td>${escape(p.ledgerName)}</td><td>${storedPurchasePostingAmount(p.amount)}</td></tr>`).join("")}</table><p>Purchase debit ${Number(voucher.debit).toFixed(2)} | credit ${Number(voucher.credit).toFixed(2)}</p></html>`;
}
