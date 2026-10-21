import { invoiceStateCode } from './invoiceMasterData.js';

const clean = (v) => v == null ? '' : String(v).trim();
export function parseNumber(input) {
  const text = clean(input).replace(/,/g, '').replace(/^₹\s*/, '');
  if (!/^[+-]?\d+(?:\.\d+)?$/.test(text)) return null;
  const number = Number(text);
  return Number.isFinite(number) ? number : null;
}
const decimal = (v) => {
  if (parseNumber(v) == null) return clean(v);
  const source = clean(v).replace(/,/g,'').replace(/^₹\s*/,'');
  const [integer, fraction = ''] = source.replace(/^[+-]/,'').split('.');
  const grouped = integer.replace(/\B(?=(\d{2})*\d{3}(?!\d))/g,',');
  return `${source.startsWith('-') ? '-' : ''}${grouped}.${fraction.padEnd(2,'0')}`;
};
const money = (v) => parseNumber(v) == null ? clean(v) : `₹${decimal(v)}`;
const taxMoney = (v) => {
  const amount = parseNumber(v);
  return amount == null ? clean(v) : `${amount < 0 ? '-' : ''}₹${decimal(Math.abs(amount))}`;
};
export const invoiceDate = (v) => {
  const text = clean(v); const match = text.match(/^(\d{4})-?(\d{2})-?(\d{2})(?:T.*)?$/);
  return match ? `${match[3]}-${['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'][Number(match[2])-1] || match[2]}-${match[1]}` : text;
};
const small = ['Zero','One','Two','Three','Four','Five','Six','Seven','Eight','Nine','Ten','Eleven','Twelve','Thirteen','Fourteen','Fifteen','Sixteen','Seventeen','Eighteen','Nineteen'];
function words(n) {
  if (n < 20) return small[n];
  if (n < 100) return ['', '', 'Twenty','Thirty','Forty','Fifty','Sixty','Seventy','Eighty','Ninety'][Math.floor(n/10)] + (n%10 ? ` ${words(n%10)}` : '');
  for (const [unit, label] of [[10000000,'Crore'],[100000,'Lakh'],[1000,'Thousand'],[100,'Hundred']]) {
    if (n >= unit) return `${words(Math.floor(n/unit))} ${label}${n%unit ? ` ${words(n%unit)}` : ''}`;
  }
}
export function amountWords(amount) {
  const n = parseNumber(amount);
  if (n == null || Math.abs(n) > 999999999999) return '';
  const paise = Math.round(Math.abs(n)*100); const integer = Math.floor(paise/100);
  return `INR ${n < 0 ? 'Minus ' : ''}${words(integer)}${paise%100 ? ` and ${words(paise%100)} Paise` : ''} Only`;
}

export function invoiceModel(voucher, company = {}, ledger = {}, { includeTaxLedgerEntries = false } = {}) {
  if (voucher.companyId != null && company.id != null && Number(voucher.companyId) !== Number(company.id)) throw new Error('The voucher belongs to a different company. Reopen it from the correct company.');
  const type = clean(voucher.voucherType);
  const purchase = type.toLowerCase() === 'purchase';
  const isInvoice = purchase || type.toLowerCase() === 'sales';
  const quantities = new Map(); let itemSum = 0; let validAmounts = true; let validQuantities = true;
  const rows = (voucher.items || []).map((item, index) => {
    const quantity = clean(item.quantity); const match = quantity.match(/^([+-]?[\d,]+(?:\.\d+)?)\s*(.*)$/);
    if (match && parseNumber(match[1]) != null) quantities.set(match[2], (quantities.get(match[2]) || 0) + parseNumber(match[1]));
    else validQuantities = false;
    const rate = clean(item.rate); const slash = rate.lastIndexOf('/');
    const amount = parseNumber(item.amount); if (amount == null) validAmounts = false; else itemSum += amount;
    return { serial: String(index+1), description: clean(item.itemName), quantity,
      rate: decimal(slash > -1 ? rate.slice(0,slash) : rate), per: slash > -1 ? rate.slice(slash+1).trim() : '',
      discount: parseNumber(item.discount) === 0 ? '' : clean(item.discount), amount: decimal(item.amount) };
  });
  const party = { name: clean(voucher.partyName), address: clean(voucher.partyAddress || ledger.address), city: clean(voucher.partyCity || ledger.city), gstin: clean(voucher.partyGstin || ledger.gstin), state: clean(voucher.state || ledger.state), stateCode: invoiceStateCode(voucher.partyStateCode || ledger.stateCode, voucher.partyGstin || ledger.gstin), pincode: clean(voucher.partyPincode || ledger.pincode), country: clean(voucher.partyCountry || ledger.country) };
  const total = voucher.totalAmount ?? voucher.amount;
  const taxEntries = includeTaxLedgerEntries && isInvoice
    ? (voucher.ledgerEntries || []).filter(entry => /(?:^|[^A-Z0-9])(?:GST|CGST|SGST|IGST|CESS)(?:[^A-Z0-9]|$)/i.test(clean(entry.ledgerName))).map(entry => ({ name: clean(entry.ledgerName), amount: taxMoney(entry.amount) }))
    : [];
  const consignee = { name: '' };
  if (voucher.consignee && typeof voucher.consignee === 'object') for (const field of ['name','address','city','gstin','state','stateCode','pincode','country','phone','mobile','email']) consignee[field] = clean(voucher.consignee[field]);
  return { isInvoice, purchase, heading: isInvoice ? 'INVOICE' : `${type || 'General'} Voucher`.toUpperCase(),
    company, party, consignee, taxEntries, partyLabel: purchase ? 'Supplier (Bill From)' : isInvoice ? 'Buyer (Bill To)' : 'Party / Account',
    number: clean(voucher.voucherNumber), date: invoiceDate(voucher.date), supplierNumber: purchase ? clean(voucher.reference) : '', supplierDate: purchase ? invoiceDate(voucher.referenceDate) : '',
    reference: clean(voucher.otherReferences || (purchase ? '' : voucher.reference)), narration: clean(voucher.narration),
    rows: isInvoice ? rows : (voucher.ledgerEntries || []).map((entry, index) => ({ serial: String(index+1), description: clean(entry.ledgerName), amount: decimal(entry.amount) })),
    quantityTotal: validQuantities && quantities.size ? [...quantities].map(([unit, amount]) => `${amount.toLocaleString('en-IN', { minimumFractionDigits: 3, maximumFractionDigits: 3 })}${unit ? ` ${unit}` : ''}`).join(' + ') : '',
    total: money(total), words: amountWords(total), itemTotal: validAmounts ? money(itemSum) : '',
    totalsDiffer: rows.length > 0 && validAmounts && parseNumber(total) != null && Math.abs(itemSum - parseNumber(total)) > 0.01 };
}

// Whole rows only; the final page reserves its totals/signature space.
export function paginateRows(rows, firstCapacity, nextCapacity, footerHeight) {
  if (footerHeight > nextCapacity) throw new Error('The invoice footer is too tall for an A4 page.');
  const pages = [[]]; let used = 0;
  for (const row of rows) {
    if (row.height > nextCapacity) throw new Error('An invoice row is too tall for an A4 page. Shorten its description before exporting.');
    const capacity = pages.length === 1 ? firstCapacity : nextCapacity;
    if (used + row.height > capacity) { pages.push([]); used = 0; }
    pages.at(-1).push(row); used += row.height;
  }
  if (used + footerHeight > (pages.length === 1 ? firstCapacity : nextCapacity)) {
    const tail = []; let height = 0;
    const last = pages.at(-1);
    while (last.length && height + last.at(-1).height <= nextCapacity-footerHeight) {
      const row = last.pop(); tail.unshift(row); height += row.height;
    }
    pages.push(tail);
  }
  return pages;
}
