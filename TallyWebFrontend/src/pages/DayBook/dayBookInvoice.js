export function getDayBookInvoiceContext(row, voucher, companies) {
  if (!row?.tallyGuid || !row?.companyId || !row?.companyName || !voucher?.guid) {
    throw new Error('The selected Day Book voucher is missing its identity.');
  }
  const company = companies.find(item => String(item.id) === String(row.companyId));
  if (!company || company.name?.trim().toLowerCase() !== row.companyName.trim().toLowerCase()) {
    throw new Error('The selected company could not be matched to this voucher. Reload Day Book and try again.');
  }
  if (voucher.guid.trim().toLowerCase() !== row.tallyGuid.trim().toLowerCase()) {
    throw new Error('The returned voucher did not match the selected Day Book entry.');
  }
  if (voucher.companyId != null && String(voucher.companyId) !== String(company.id)) {
    throw new Error('This voucher belongs to a different company. No PDF was prepared.');
  }
  return { company, voucher: { ...voucher, companyId: company.id } };
}
