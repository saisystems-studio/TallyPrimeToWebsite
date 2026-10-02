import { apiRequest } from "./api";

export async function getSalesReport(fromDate, toDate) {
  const response = await apiRequest(
    `/SalesReport?fromDate=${encodeURIComponent(
      fromDate
    )}&toDate=${encodeURIComponent(toDate)}`
  );

  if (!response.ok) {
    throw new Error("Unable to fetch Sales Report from Tally.");
  }

  return await response.json();
}