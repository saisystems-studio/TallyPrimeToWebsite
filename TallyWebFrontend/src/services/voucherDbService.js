import { apiRequest } from "./api";

export async function getDbVouchers(
  companyId,
  fromDate = "",
  toDate = "",
  voucherType = "All"
) {
  const params = new URLSearchParams();

  params.set("companyId", companyId);

  if (fromDate) params.set("fromDate", fromDate);
  if (toDate) params.set("toDate", toDate);

  if (voucherType && voucherType !== "All") {
    params.set("voucherType", voucherType);
  }

  const response = await apiRequest(
    `/db/vouchers?${params.toString()}`
  );

  if (!response.ok) {
    throw new Error("Unable to fetch vouchers from database.");
  }

  return await response.json();
}