import { parseSalesDate } from "./salesRegisterModel.js";

export function storedSalesDetail(payload, selected, companyId) {
  const v = payload?.voucher;
  if (payload?.source !== "SalesRegisterSQL" || !v || String(v.companyId) !== String(companyId) ||
      String(v.id) !== String(selected.id) || String(v.guid).toLowerCase() !== String(selected.guid).toLowerCase() ||
      v.date !== selected.date || !parseSalesDate(v.date)) throw new Error("Stored voucher does not match the selected company and entry. Reload the report.");
  if (!Array.isArray(v.postings) || !v.postings.length || !v.partyLedgerGuid) throw new Error("Stored ledger postings are unavailable.");
  const postings = [...v.postings].sort((a, b) => a.entryIndex - b.entryIndex);
  if (postings.some((p, i) => p.entryIndex !== i || !p.ledgerGuid || !p.ledgerName || p.amount === "" || p.amount == null || !Number.isFinite(Number(p.amount))))
    throw new Error("Stored ledger posting sequence or amounts are invalid.");
  const party = postings.filter(p => p.ledgerGuid.toLowerCase() === v.partyLedgerGuid.toLowerCase());
  const partyAmount = party.reduce((s, p) => s + Number(p.amount), 0);
  if (!party.length || Math.abs(postings.reduce((s, p) => s + Number(p.amount), 0)) > 0.00005 ||
      v.amount == null || v.amount === "" || !Number.isFinite(Number(v.amount)) || Math.abs(Math.abs(partyAmount) - Number(v.amount)) > 0.00005)
    throw new Error("Stored voucher amounts do not reconcile.");
  return { id: v.id, companyId: v.companyId, guid: v.guid, date: v.date, voucherNumber: v.voucherNumber,
    voucherType: v.voucherType, partyName: v.partyName, partyLedgerGuid: v.partyLedgerGuid, amount: Number(v.amount),
    totalAmount: Number(v.amount), partyAmount, postings,
    ledgerEntries: postings.map(p => ({ ...p, amount: Number(p.amount) })) };
}

export function storedFinancialYears({ availableFrom, availableTo, startingFrom } = {}) {
  const first = parseSalesDate(availableFrom), last = parseSalesDate(availableTo), start = parseSalesDate(startingFrom);
  if (!first || !last || !start || first > last) return [];
  const annual = year => new Date(year, start.getMonth(), Math.min(start.getDate(), new Date(year, start.getMonth() + 1, 0).getDate()));
  const yearOf = date => date < annual(date.getFullYear()) ? date.getFullYear() - 1 : date.getFullYear();
  const iso = d => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
  const years = [];
  for (let year = yearOf(last); year >= yearOf(first); year--) {
    const end = annual(year + 1); end.setDate(end.getDate() - 1);
    years.push({ label: `${year}–${year + 1}`, from: iso(annual(year)), to: iso(end) });
  }
  return years;
}

const escape = v => String(v ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
export const storedPostingAmount = amount => Number(amount).toLocaleString("en-IN", { useGrouping: false, minimumFractionDigits: 2, maximumFractionDigits: 4 });
export function storedVoucherHtml(voucher, companyName) {
  return `<html><meta charset="utf-8"><title>Stored Sales Voucher</title><h1>Sales Voucher ${escape(voucher.voucherNumber)}</h1><p>${escape(companyName)}</p><p>${escape(voucher.date)} | ${escape(voucher.voucherType)} | ${escape(voucher.partyName)}</p><table><tr><th>Sequence</th><th>Ledger</th><th>Signed amount</th></tr>${voucher.ledgerEntries.map(p => `<tr><td>${p.entryIndex + 1}</td><td>${escape(p.ledgerName)}</td><td>${storedPostingAmount(p.amount)}</td></tr>`).join("")}</table><p>Amount ${voucher.totalAmount.toFixed(2)}</p></html>`;
}
