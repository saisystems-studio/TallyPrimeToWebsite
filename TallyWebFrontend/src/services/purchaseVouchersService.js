import { apiRequest } from "./api";

async function readJsonResponse(response, fallbackMessage) {
  const bodyText = await response.text().catch(() => "");
  let body = null;

  if (bodyText.trim()) {
    try {
      body = JSON.parse(bodyText);
    } catch {
      if (!response.ok) {
        throw new Error(`${fallbackMessage} (HTTP ${response.status}${response.statusText ? ` ${response.statusText}` : ""}).`);
      }
      throw new Error(`${fallbackMessage}: API returned a non-JSON response (HTTP ${response.status}).`);
    }
  }

  if (!response.ok) {
    if (response.status >= 500) {
      const trace = body?.traceId ? ` Trace ID: ${body.traceId}.` : "";
      throw new Error(`${fallbackMessage}: the server could not complete the request (HTTP ${response.status}).${trace}`);
    }
    const message = body?.message || body?.title || body?.detail || bodyText.trim();
    throw new Error(message || `${fallbackMessage} (HTTP ${response.status}${response.statusText ? ` ${response.statusText}` : ""}).`);
  }

  if (!bodyText.trim() || response.status === 204) {
    throw new Error(`${fallbackMessage}: API returned an empty response (HTTP ${response.status}).`);
  }

  return body;
}

export async function getPurchaseVouchers(companyId, fromDate, toDate) {
  const params = new URLSearchParams({
    companyId: String(companyId),
    fromDate,
    toDate,
  });
  const response = await apiRequest(`/reports/purchase-register?${params}`);
  return readJsonResponse(response, "Unable to fetch Purchase vouchers");
}

export async function getPurchaseVoucherPeriods(companyId) {
  const response = await apiRequest(`/reports/purchase-register/periods?companyId=${encodeURIComponent(companyId)}`);
  return readJsonResponse(response, "Unable to fetch available Purchase voucher dates");
}
