const clean = value => value == null ? '' : String(value).trim();
const key = value => clean(value).toUpperCase();
const fields = ['address','city','state','stateCode','pincode','country','phone','mobile','email','website','gstin'];

export const invoiceStateCode = (explicit, gstin) => clean(explicit) || (/^\d{2}[A-Z0-9]{13}$/.test(clean(gstin)) ? clean(gstin).slice(0,2) : '');

export function mergeInvoiceCompany(company, masters, registrations) {
  const result = { ...company };
  if (masters.some(master => clean(master.name) === clean(company.name) && master.tallyGuid && company.tallyGuid && key(master.tallyGuid) !== key(company.tallyGuid))) return result;
  const matches = masters.filter(master => clean(master.name) === clean(company.name) && !(master.tallyGuid && company.tallyGuid && key(master.tallyGuid) !== key(company.tallyGuid)));
  if (matches.length === 1) for (const field of fields) if (clean(matches[0][field])) result[field] = matches[0][field];
  // A GST unit is company-scoped by the read-only request. Never pick an arbitrary branch.
  const units = result.gstin ? registrations.filter(unit => key(unit.gstin) === key(result.gstin)) : registrations;
  if (units.length === 1) for (const field of fields) if (!clean(result[field]) && clean(units[0][field])) result[field] = units[0][field];
  return result;
}

export function parseInvoiceMasters(source, elementName) {
  const xml = new DOMParser().parseFromString(source, 'application/xml');
  if (xml.getElementsByTagName('parsererror').length) return [];
  return [...xml.getElementsByTagName(elementName)].filter(node => node.hasAttribute('NAME') && (elementName !== 'TAXUNIT' || !node.getAttribute('TAXTYPE') || key(node.getAttribute('TAXTYPE')) === 'GST')).map(node => {
    // Restrict address extraction to actual address fields on this master, excluding
    // unrelated nested master names, tax registrations, state and contact fields.
    const children = [...node.children];
    const read = tag => children.find(child => child.tagName === tag)?.textContent?.trim() || '';
    const address = children.flatMap(child => child.tagName === 'ADDRESS' ? [child.textContent] : child.tagName === 'ADDRESS.LIST' ? [...child.children].filter(line => line.tagName === 'ADDRESS').map(line => line.textContent) : []).map(clean).filter(Boolean).join('\n');
    return { name: clean(node.getAttribute('NAME')), tallyGuid: read('GUID'), address,
      gstin: clean(node.getAttribute('TAXREGISTRATION')) || read('GSTREGISTRATIONNUMBER') || read('GSTREGNUMBER'),
      city: read('CITY'), state: read('STATENAME'), stateCode: read('STATECODE'), country: read('COUNTRYNAME'), pincode: read('PINCODE'),
      phone: read('PHONENUMBER'), mobile: read('MOBILENUMBER'), email: read('EMAIL'), website: read('WEBSITE') };
  });
}
