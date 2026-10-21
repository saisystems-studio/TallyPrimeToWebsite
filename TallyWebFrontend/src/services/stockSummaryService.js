import { apiRequest } from "./api";

export async function getStockSummary(filters, signal) {
  const { companyId, fromDate, toDate } = filters || {};
  if (companyId == null || !/^\d+$/.test(String(companyId))) {
    throw new Error("Please select a company.");
  }
  const query = new URLSearchParams({ companyId: String(companyId) });
  const appendDate = (key, value) => {
    if (value == null || value === "") return;
    if (typeof value !== "string" || !/^\d{4}-\d{2}-\d{2}$/.test(value)) {
      throw new Error(`A valid ${key === "fromDate" ? "From Date" : "To Date"} is required.`);
    }
    query.set(key, value);
  };
  appendDate("fromDate", fromDate);
  appendDate("toDate", toDate);
  const response = await apiRequest("/StockSummary?" + query, { signal });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    const modelState = Object.values(data.errors || {}).flat().filter(Boolean).join(" ");
    throw new Error(data.message || data.detail || data.title || modelState || `Unable to load Stock Summary (HTTP ${response.status}).`);
  }
  return data;
}

