import { apiRequest } from "./api";

export async function getDayBook({
  companyId = "",
  fromDate = "",
  toDate = "",
  voucherType = "All",
} = {}) {
  const params = new URLSearchParams();

  // Empty companyId = All Companies
  if (companyId) {
    params.set("companyId", companyId);
  }

  if (fromDate) {
    params.set("fromDate", fromDate);
  }

  if (toDate) {
    params.set("toDate", toDate);
  }

  if (voucherType && voucherType !== "All") {
    params.set("voucherType", voucherType);
  }

  const response = await apiRequest(
    `/db/day-book?${params.toString()}`
  );

  if (!response.ok) {
    throw new Error("Unable to fetch Day Book from database.");
  }

  return await response.json();
}