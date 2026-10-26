export function salesVoucherTypes(voucherTypes = []) {
  return [...new Set(voucherTypes
    .filter(type => String(type.parent || "").trim().toLowerCase() === "sales")
    .map(type => String(type.name || "").trim())
    .filter(Boolean))];
}

export function signedPartyAmount(voucher) {
  const value = voucher?.partyAmount;
  return value !== null && value !== undefined && value !== "" && Number.isFinite(Number(value))
    ? Number(value)
    : null;
}

export function salesRegisterTotals(entries) {
  if (entries.some(entry => signedPartyAmount(entry) === null))
    return { debit: null, credit: null, closing: null };
  const amounts = entries.map(salesMovementAmount);
  return {
    debit: amounts.filter(value => value < 0).reduce((sum, value) => sum + Math.abs(value), 0),
    credit: amounts.filter(value => value >= 0).reduce((sum, value) => sum + value, 0),
    closing: amounts.reduce((sum, value) => sum + value, 0),
  };
}

// The Sales Register reports the movement opposing the party ledger, not
// the party's receivable balance. Stored Tally signs remain untouched.
export function salesMovementAmount(voucher) {
  const party = signedPartyAmount(voucher);
  return party === null ? null : -party;
}

export function salesClosingBalance(value) {
  return value == null ? null : { amount: Math.abs(value), side: value < 0 ? "Dr" : value > 0 ? "Cr" : "" };
}

export function parseSalesDate(value) {
  const text = String(value ?? "");
  if (!/^(?:\d{8}|\d{4}-\d{2}-\d{2})$/.test(text)) return null;
  const match = /^(\d{4})(\d{2})(\d{2})$/.exec(text.replaceAll("-", ""));
  if (!match) return null;
  const [, y, m, d] = match;
  const date = new Date(0);
  date.setHours(0, 0, 0, 0);
  date.setFullYear(Number(y), Number(m) - 1, Number(d));
  return date.getFullYear() === Number(y) && date.getMonth() === Number(m) - 1 && date.getDate() === Number(d) ? date : null;
}

export function salesRegisterMonths(rows, { from = "", to = "", error } = {}) {
  const start = parseSalesDate(from), end = parseSalesDate(to);
  if (error || ((from || to) && (!start || !end || start > end))) return [];
  const keyOf = date => `${date.getFullYear()}${String(date.getMonth() + 1).padStart(2, "0")}`;
  const grouped = new Map();
  if (start) {
    const cursor = new Date(start); cursor.setDate(1);
    while (cursor <= end) {
      grouped.set(keyOf(cursor), []);
      cursor.setMonth(cursor.getMonth() + 1);
    }
  }
  for (const row of rows) {
    // API date is serialized from SQL VoucherDate.
    const date = parseSalesDate(row.date);
    if (!date || (start && (date < start || date > end))) continue;
    const key = keyOf(date);
    if (!grouped.has(key)) grouped.set(key, []);
    grouped.get(key).push(row);
  }
  return [...grouped.entries()].sort(([a], [b]) => a.localeCompare(b)).map(([key, entries]) => {
    const date = parseSalesDate(`${key}01`);
    const ordered = [...entries].sort((a, b) => parseSalesDate(a.date) - parseSalesDate(b.date));
    return { key, label: date.toLocaleDateString("en-IN", { month: "long", year: "numeric" }), entries: ordered,
      total: ordered.some(row => row.amount == null || row.amount === "" || !Number.isFinite(Number(row.amount))) ? null : ordered.reduce((sum, row) => sum + Math.abs(Number(row.amount)), 0) };
  });
}
