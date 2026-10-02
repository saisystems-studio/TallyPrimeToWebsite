import { apiRequest } from "./api";

export async function getDbStockItems(companyId) {
  const response = await apiRequest(
    `/db/stock-items?companyId=${companyId}`
  );

  if (!response.ok) {
    throw new Error("Unable to fetch Stock Items from database.");
  }

  return await response.json();
}