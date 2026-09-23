import { apiRequest } from "./api";

export async function getVouchersRaw() {
  const response = await apiRequest("/voucher/vouchers-raw");

  if (!response.ok) {
    throw new Error("Unable to fetch vouchers from Tally.");
  }

  return await response.text();
}

export async function getVouchers(fromDate, toDate) {
  const response = await apiRequest(
    `/voucher/vouchers?fromDate=${fromDate}&toDate=${toDate}`
  );

  if (!response.ok) {
    throw new Error("Unable to fetch vouchers from Tally.");
  }

  return await response.json();
}

export async function getVoucherDetail(guid, fromDate, toDate) {
  const response = await apiRequest(
    `/voucher/voucher-detail?guid=${encodeURIComponent(
      guid
    )}&fromDate=${encodeURIComponent(
      fromDate
    )}&toDate=${encodeURIComponent(toDate)}`
  );

  if (!response.ok) {
    throw new Error("Unable to fetch voucher details from Tally.");
  }

  return await response.json();
}