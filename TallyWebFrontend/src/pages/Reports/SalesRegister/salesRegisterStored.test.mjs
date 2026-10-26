import test from "node:test";
import assert from "node:assert/strict";
import { storedSalesDetail, storedFinancialYears, storedVoucherHtml } from "./salesRegisterStored.js";

const row={id:12,companyId:7,guid:"sales-guid",date:"20200402"};
const payload={source:"SalesRegisterSQL",voucher:{...row,voucherNumber:"A<&",voucherType:"Sales",partyName:"Party",partyLedgerGuid:"party",amount:10,postings:[{entryIndex:1,ledgerGuid:"sales",ledgerName:"Sales",amount:10},{entryIndex:0,ledgerGuid:"party",ledgerName:"Party",amount:-10}]}};
test("stored detail preserves signs and sequence without a live Tally dependency",()=>{
  const v=storedSalesDetail(payload,row,7);
  assert.equal(v.ledgerEntries[0].amount,-10);assert.equal(v.ledgerEntries[1].amount,10);assert.equal(v.partyAmount,-10);assert.equal(v.totalAmount,10);
  assert.equal(v.items,undefined);
});
test("stored detail rejects unrelated source, company, identity and invalid postings",()=>{
  for(const changed of [{...payload,source:"Tally"},{...payload,voucher:{...payload.voucher,companyId:8}},{...payload,voucher:{...payload.voucher,guid:"other"}},{...payload,voucher:{...payload.voucher,postings:[]}}])assert.throws(()=>storedSalesDetail(changed,row,7));
  assert.throws(()=>storedSalesDetail({...payload,voucher:{...payload.voucher,postings:payload.voucher.postings.map(p=>({...p,amount:1}))}},row,7));
});
test("stored financial years derive inclusive periods from stored range and company start",()=>{
  assert.deepEqual(storedFinancialYears({availableFrom:"20200331",availableTo:"20210401",startingFrom:"20180401"}),[
    {label:"2021–2022",from:"2021-04-01",to:"2022-03-31"},{label:"2020–2021",from:"2020-04-01",to:"2021-03-31"},{label:"2019–2020",from:"2019-04-01",to:"2020-03-31"}]);
  assert.deepEqual(storedFinancialYears({}),[]);
});
test("stored voucher export escapes content and includes only stored postings",()=>{
  const html=storedVoucherHtml(storedSalesDetail(payload,row,7),"Company & Co");
  assert.ok(html.includes("A&lt;&amp;"));assert.ok(html.includes("Company &amp; Co"));assert.ok(html.includes("-10.00"));assert.ok(!html.includes("INVOICE"));
});
test("stored ledger export retains four-place posting precision",()=>{
  const detailed={...payload,voucher:{...payload.voucher,postings:[{entryIndex:0,ledgerGuid:"party",ledgerName:"Party",amount:-10},{entryIndex:1,ledgerGuid:"sales",ledgerName:"Sales",amount:5.0001},{entryIndex:2,ledgerGuid:"tax",ledgerName:"Tax",amount:4.9999}]}};
  const html=storedVoucherHtml(storedSalesDetail(detailed,row,7),"Company");
  assert.ok(html.includes("5.0001"));assert.ok(html.includes("4.9999"));
});
