export function toApiDate(value) {
  return value ? value.replaceAll("-", "") : "";
}

export function normalizeVoucherDate(value) {
  const text = String(value || "").trim();
  if (/^\d{8}$/.test(text)) return text;
  const iso = text.match(/^(\d{4})-(\d{2})-(\d{2})$/);
  return iso ? `${iso[1]}${iso[2]}${iso[3]}` : "";
}

export function resolveLedgerReportRange(fromDate, toDate, availableDates) {
  let from = toApiDate(fromDate);
  let to = toApiDate(toDate);
  if (!from || !to) {
    const dates = availableDates.map(normalizeVoucherDate).filter(Boolean).sort();
    if (!dates.length) {
      throw new Error("No synchronized voucher dates are available for this company, so the Ledger Report period cannot be resolved.");
    }
    from ||= dates[0];
    to ||= dates[dates.length - 1];
  }
  if (from > to) {
    throw new Error("The resolved From Date is later than the To Date. Check the synchronized history and selected dates.");
  }
  return { fromDate: from, toDate: to };
}

