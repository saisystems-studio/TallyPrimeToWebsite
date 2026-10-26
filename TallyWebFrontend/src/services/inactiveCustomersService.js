
import { apiRequest } from "./api";

export async function getInactiveCustomers(companyId) {
  if (!companyId || Number(companyId) <= 0) {
    throw new Error("Please select a valid company.");
  }

  const params = new URLSearchParams({
    companyId: String(companyId),
  });

  const response = await apiRequest(
    `/reports/inactive-customers?${params.toString()}`
  );

  const bodyText = await response.text().catch(() => "");
  let data = null;

  if (bodyText.trim()) {
    try {
      data = JSON.parse(bodyText);
    } catch {
      throw new Error(
        `Invalid API response (HTTP ${response.status}).`
      );
    }
  }

  if (!response.ok) {
    if (response.status === 401) {
      throw new Error("Session expired. Please login again.");
    }

    if (response.status >= 500) {
      throw new Error(
        `Unable to load inactive customers (HTTP ${response.status}).`
      );
    }

    throw new Error(
      data?.message ||
      `Unable to load inactive customers (HTTP ${response.status}).`
    );
  }

  if (!data || !Array.isArray(data.customers)) {
    throw new Error("Invalid inactive customers data received.");
  }

  return data;
}
