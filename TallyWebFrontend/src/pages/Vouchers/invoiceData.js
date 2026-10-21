import { apiRequest } from '../../services/api';
import { mergeInvoiceCompany, parseInvoiceMasters, invoiceStateCode } from './invoiceMasterData';
import { invoiceModel } from './invoiceDocument';
import { renderInvoice } from './invoiceRenderer';

// Enrichment is read-only and scoped to the explicitly selected company.
export async function loadInvoiceData(company, voucher, signal) {
  const warnings = [];
  const fetchData = async (url, xml = false) => {
    const response = await apiRequest(url, { signal });
    if (!response.ok) throw new Error('Master data is unavailable.');
    return xml ? response.text() : response.json();
  };
  const results = await Promise.allSettled([
    fetchData(`/voucher/invoice-company?companyName=${encodeURIComponent(company.name)}`),
    fetchData(`/tally/db/ledgers?companyId=${encodeURIComponent(company.id)}`),
    fetchData(`/tally/gst-registrations-raw?companyName=${encodeURIComponent(company.name)}`, true),
  ]);
  if (signal?.aborted) throw new DOMException('Cancelled', 'AbortError');
  const master = results[0].status === 'fulfilled' ? results[0].value.company : null;
  const masters = master ? [master] : [];
  const registrations = results[2].status === 'fulfilled' ? parseInvoiceMasters(results[2].value, 'TAXUNIT') : [];
  const selectedCompany = { ...company, gstin: voucher.companyGstin || company.gstin };
  if (voucher.companyGstin && master) master.gstin = voucher.companyGstin;
  const details = mergeInvoiceCompany(selectedCompany, masters, registrations);
  details.stateCode = invoiceStateCode(details.stateCode, details.gstin);
  let ledger = {};
  if (results[1].status === 'fulfilled') {
    const matches = (results[1].value.ledgers || []).filter(item => item.name?.trim() === (voucher.partyLedgerName || voucher.partyName)?.trim() && Number(item.companyId) === Number(company.id));
    if (matches.length === 1) ledger = matches[0];
    else warnings.push('The party master was not uniquely matched; its address is left blank.');
  } else warnings.push('Party master details could not be loaded; its address is left blank.');
  return { company: details, ledger, warnings };
}

export async function prepareInvoiceDocument(company, voucher, signal, { includeTaxLedgerEntries = false } = {}) {
  if (!company?.id) throw new Error('The selected company is unavailable.');
  const data = await loadInvoiceData(company, voucher, signal);
  await document.fonts.ready;
  if (signal?.aborted) throw new DOMException('Cancelled', 'AbortError');
  const model = invoiceModel(voucher, data.company, data.ledger, { includeTaxLedgerEntries });
  const canvases = renderInvoice(model);
  return { model, canvases, images: canvases.map(canvas => canvas.toDataURL('image/png')) };
}
