import { apiRequest } from "./api";

export async function getVouchersRaw(
  fromDate,
  toDate,
  companyName
) {
  const response = await apiRequest(
    `/voucher/vouchers-raw?fromDate=${encodeURIComponent(
      fromDate
    )}&toDate=${encodeURIComponent(
      toDate
    )}&companyName=${encodeURIComponent(companyName)}`
  );

  if (!response.ok) {
    throw new Error("Unable to fetch vouchers from Tally.");
  }

  return await response.text();
}

export async function getVouchers(
  fromDate,
  toDate,
  companyName
) {
  const response = await apiRequest(
    `/voucher/vouchers?fromDate=${encodeURIComponent(
      fromDate
    )}&toDate=${encodeURIComponent(
      toDate
    )}&companyName=${encodeURIComponent(companyName)}`
  );

  if (!response.ok) {
    throw new Error("Unable to fetch vouchers from Tally.");
  }

  return await response.json();
}

export async function getVoucherDetail(
  guid,
  fromDate,
  toDate,
  companyName
) {
  const response = await apiRequest(
    `/voucher/voucher-detail?guid=${encodeURIComponent(
      guid
    )}&fromDate=${encodeURIComponent(
      fromDate
    )}&toDate=${encodeURIComponent(
      toDate
    )}&companyName=${encodeURIComponent(companyName)}`
  );

  if (!response.ok) {
    let message = "Unable to fetch voucher details from Tally.";
    try {
      const body = await response.json();
      message = body.error || body.message || message;
    } catch {
      // Leave the default message when the API returns a non-JSON response.
    }
    throw new Error(message);
  }

  return await response.json();
}
