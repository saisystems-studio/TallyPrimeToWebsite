import { apiRequest } from "./api";

export async function getLedgerReport(
  ledgerName,
  fromDate,
  toDate
) {
  const response = await apiRequest(
    `/LedgerReport?ledgerName=${encodeURIComponent(
      ledgerName
    )}&fromDate=${encodeURIComponent(
      fromDate
    )}&toDate=${encodeURIComponent(toDate)}`
  );

  if (!response.ok) {
    throw new Error(
      "Unable to fetch Ledger Report from Tally."
    );
  }

  return await response.json();
}