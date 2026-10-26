import { apiRequest } from "./api";

export async function getPurchaseRegister(companyId, fromDate = "", toDate = "") {
  const params = new URLSearchParams({ companyId: String(companyId) });
  if (fromDate && toDate) { params.set("fromDate", fromDate); params.set("toDate", toDate); }
  const response = await apiRequest(`/purchase-register?${params}`);
  const data = await response.json();
  if (!response.ok) throw new Error(data.message || "Unable to fetch Purchase Register from SQL.");
  return data;
}

export async function getStoredPurchasePeriods(companyId) {
  const response = await apiRequest(`/purchase-register/periods?companyId=${encodeURIComponent(companyId)}`);
  const data = await response.json();
  if (!response.ok) throw new Error(data.message || "Unable to fetch stored Purchase periods.");
  return data;
}
