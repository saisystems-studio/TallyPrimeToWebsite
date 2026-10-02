import { apiRequest } from "./api";

export async function getStockSummary(fromDate, toDate) {
  const response = await apiRequest(
    `/StockSummary?fromDate=${encodeURIComponent(
      fromDate
    )}&toDate=${encodeURIComponent(toDate)}`
  );

  if (!response.ok) {
    throw new Error(
      "Unable to fetch Stock Summary from Tally."
    );
  }

  return await response.json();
}