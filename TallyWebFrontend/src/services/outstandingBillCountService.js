
import { apiRequest } from "./api";

async function readResponse(response, fallback) {
  if (!response.ok) {
    const body = await response.json().catch(() => null);

    if (response.status === 401) {
      throw new Error("Session expired. Please login again.");
    }

    throw new Error(
      body?.message || `${fallback} (HTTP ${response.status})`
    );
  }

  return response.json();
}

export async function getOutstandingBillCount(companyId) {
  if (!companyId) {
    throw new Error("Please select a company.");
  }

  const response = await apiRequest(
    `/outstanding-bill-count?companyId=${encodeURIComponent(companyId)}`
  );

  return readResponse(response, "Unable to fetch outstanding bill count.");
}

export async function getOutstandingBillDetails(companyId, ledgerName) {
  const params = new URLSearchParams({
    companyId: String(companyId),
    ledgerName: String(ledgerName),
  });

  const response = await apiRequest(
    `/outstanding-bill-count/details?${params.toString()}`
  );

  return readResponse(response, "Unable to fetch outstanding bill details.");
}

export async function getGroupWiseOutstandingBillCount(companyId) {
  if (!companyId) {
    throw new Error("Please select a company.");
  }

  const response = await apiRequest(
    `/outstanding-bill-count/groups?companyId=${encodeURIComponent(companyId)}`
  );

  return readResponse(response, "Unable to fetch group-wise outstanding.");
}
