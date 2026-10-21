import { apiRequest } from "./api";

function dateParam(value, fieldName) {
  if (value == null || value === "") return "";
  if (typeof value !== "string") {
    throw new Error(`${fieldName} must be a valid date.`);
  }
  const match = value.match(/^(\d{4})-(\d{2})-(\d{2})$/) || value.match(/^(\d{4})(\d{2})(\d{2})$/);
  if (!match) throw new Error(`${fieldName} must be a valid date.`);
  const [, yearText, monthText, dayText] = match;
  const year = Number(yearText); const month = Number(monthText); const day = Number(dayText);
  const date = new Date(Date.UTC(year, month - 1, day));
  if (date.getUTCFullYear() !== year || date.getUTCMonth() !== month - 1 || date.getUTCDate() !== day) {
    throw new Error(`${fieldName} must be a valid date.`);
  }
  return `${yearText}${monthText}${dayText}`;
}

async function readError(response) {
  const text = await response.text().catch(() => "");
  const body = (() => {
    try { return text ? JSON.parse(text) : {}; } catch { return {}; }
  })();

  const validation = Object.entries(body.errors || {})
    .map(([field, messages]) => {
      const details = (Array.isArray(messages) ? messages : [messages]).filter(Boolean).join(" ");
      return details ? `${field}: ${details}` : "";
    })
    .filter(Boolean)
    .join(" ");
  const detail = body.message || body.detail || validation || body.title || text;
  const status = `HTTP ${response.status}`;
  return detail ? `${detail} (${status})` : `Ledger Report request failed (${status}).`;
}

export async function getLedgerReport({ companyId, ledgerId, fromDate, toDate }) {
  if (!Number.isInteger(Number(companyId)) || Number(companyId) <= 0) throw new Error("Select a valid company.");
  if (!Number.isInteger(Number(ledgerId)) || Number(ledgerId) <= 0) throw new Error("Select a valid ledger.");
  const params = new URLSearchParams({ companyId: String(companyId), ledgerId: String(ledgerId) });
  const from = dateParam(fromDate, "From Date");
  const to = dateParam(toDate, "To Date");
  if (from) params.set("fromDate", from);
  if (to) params.set("toDate", to);
  const response = await apiRequest(`/LedgerReport?${params}`);
  if (!response.ok) throw new Error(await readError(response));
  return response.json();
}
