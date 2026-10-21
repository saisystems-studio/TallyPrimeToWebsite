import { apiRequest } from "./api";

async function throwApiError(response, fallbackMessage) {
  const text = await response.text().catch(() => "");
  let body = {};
  try {
    body = text ? JSON.parse(text) : {};
  } catch {
    body = {};
  }

  const validationDetails = Object.values(body.errors || {})
    .flat()
    .filter(Boolean)
    .join(" ");
  const message = body.message || body.title || body.detail || validationDetails || text;
  const details = body.error && body.error !== message ? ` (${body.error})` : "";
  throw new Error(`${message || `${fallbackMessage} (HTTP ${response.status}).`}${details}`);
}

export async function getDbCompanies() {
  const response = await apiRequest("/db/companies");

  if (!response.ok) {
    await throwApiError(response, "Unable to fetch companies from database");
  }

  return await response.json();
}

export async function getDbLedgers(companyId) {
  const response = await apiRequest(`/tally/db/ledgers?companyId=${encodeURIComponent(companyId)}`);

  if (!response.ok) {
    await throwApiError(response, "Unable to fetch ledgers for this company");
  }

  return await response.json();
}
