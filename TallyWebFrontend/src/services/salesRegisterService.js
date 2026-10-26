import { apiRequest } from "./api";

export async function getSalesRegister(companyId, fromDate = "", toDate = "") {
  const params = new URLSearchParams({ companyId: String(companyId) });
  if (fromDate && toDate) { params.set("fromDate", fromDate); params.set("toDate", toDate); }
  const response = await apiRequest(`/sales-register?${params}`);
  const data = await response.json();
  if (!response.ok) throw new Error(data.message || "Unable to fetch Sales Register from SQL.");
  return data;
}

export async function getStoredSalesVoucher(companyId, id) {
  const response = await apiRequest(`/sales-register/vouchers/${encodeURIComponent(id)}?companyId=${encodeURIComponent(companyId)}`);
  const data = await response.json();
  if (!response.ok) throw new Error(data.message || "Unable to fetch stored Sales voucher.");
  return data;
}

export async function getStoredSalesPeriods(companyId) {
  const response = await apiRequest(`/sales-register/periods?companyId=${encodeURIComponent(companyId)}`);
  const data = await response.json();
  if (!response.ok) throw new Error(data.message || "Unable to fetch stored Sales periods.");
  return data;
}
