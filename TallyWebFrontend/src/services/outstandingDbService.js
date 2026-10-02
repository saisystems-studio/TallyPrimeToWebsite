import { apiRequest } from "./api";

export async function getDbOutstanding(
  companyId = "",
  ledgerName = "",
  includeSettled = false
) {
  const params = new URLSearchParams();

  // Company is optional.
  // Empty companyId = fetch all companies.
  if (companyId) {
    params.set("companyId", companyId);
  }

  if (ledgerName) {
    params.set("ledgerName", ledgerName);
  }

  params.set("includeSettled", includeSettled.toString());

  const response = await apiRequest(
    `/db/outstanding?${params.toString()}`
  );

  if (!response.ok) {
    throw new Error(
      "Unable to fetch outstanding data from database."
    );
  }

  return await response.json();
}