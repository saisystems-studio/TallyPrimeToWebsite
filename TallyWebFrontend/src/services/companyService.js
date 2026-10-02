import { apiRequest } from "./api";

export async function getDbCompanies() {
  const response = await apiRequest("/db/companies");

  if (!response.ok) {
    throw new Error("Unable to fetch companies from database.");
  }

  return await response.json();
}