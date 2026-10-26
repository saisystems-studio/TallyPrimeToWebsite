import { apiRequest } from "./api";

export async function getCompanies() {
  const response = await apiRequest("/tally/companies");

  if (!response.ok) {
    throw new Error("Unable to fetch Tally company.");
  }

  return await response.text();
}

export async function getVoucherTypes(companyName) {
  const response = await apiRequest(`/tally/voucher-types?companyName=${encodeURIComponent(companyName)}`);
  if (!response.ok) {
    let detail = "";
    try {
      const body = await response.json();
      detail = body.error || body.message || "";
    } catch { /* Keep the user-facing error useful for non-JSON responses. */ }
    throw new Error(detail || `Unable to read voucher type classification from TallyPrime (HTTP ${response.status}).`);
  }
  return await response.json();
}

export async function getCurrentCompany() {
  const response = await apiRequest("/tally/current-company");

  if (!response.ok) {
    throw new Error("Unable to fetch current Tally company.");
  }

  return await response.json();
}

export async function getLedgersRaw() {
  const response = await apiRequest("/tally/ledgers-raw");

  if (!response.ok) {
    throw new Error("Unable to fetch raw ledger data.");
  }

  return await response.text();
}

export async function getCompanyRaw() {
  const response = await apiRequest("/tally/company-raw");

  if (!response.ok) {
    throw new Error("Unable to fetch raw company data.");
  }

  return await response.text();
}

export async function getGstRegistrationsRaw() {
  const response = await apiRequest("/tally/gst-registrations-raw");

  if (!response.ok) {
    throw new Error("Unable to fetch GST registration data.");
  }

  return await response.text();
}

export async function getLedgers() {
  const response = await apiRequest("/tally/ledgers");

  if (!response.ok) {
    throw new Error("Unable to fetch ledgers from Tally.");
  }

  return await response.json();
}

export async function getStockItemsRaw() {
  const response = await apiRequest("/tally/stock-items-raw");

  if (!response.ok) {
    throw new Error("Unable to fetch raw stock item data.");
  }

  return await response.text();
}

export async function getStockGroupsRaw() {
  const response = await apiRequest("/tally/stock-groups-raw");

  if (!response.ok) {
    throw new Error("Unable to fetch raw stock group data.");
  }

  return await response.text();
}

export async function getStockItems() {
  const response = await apiRequest("/tally/stock-items");

  if (!response.ok) {
    throw new Error("Unable to fetch stock items from Tally.");
  }

  return await response.json();
}

export async function getDbLedgers(companyId) {
  const response = await apiRequest(
    `/tally/db/ledgers?companyId=${companyId}`
  );

  if (!response.ok) {
    throw new Error("Unable to fetch ledgers from database.");
  }

  return await response.json();
}
