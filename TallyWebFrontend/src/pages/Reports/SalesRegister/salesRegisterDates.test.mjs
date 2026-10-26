import test from 'node:test';
import assert from 'node:assert/strict';
import * as model from './salesRegisterModel.js';

test('ISO April range groups compact SQL voucher dates into April only', () => {
  const rows=[{date:'20240401',amount:10,partyAmount:-10},{date:'20240430',amount:20,partyAmount:-20}];
  const months=model.salesRegisterMonths(rows,{from:'2024-04-01',to:'2024-04-30'});
  assert.deepEqual(months.map(m=>m.label),['April 2024']);
  assert.equal(months[0].entries.length,2);
  assert.equal(months[0].total,30);
  assert.deepEqual(model.salesRegisterTotals(months[0].entries),{debit:0,credit:30,closing:30});
});

test('partial months, year rollover, leap days and invalid dates',()=>{
  const rows=['20231230','20240101','20240228','20240229','20240301','20240230'].map(date=>({date,amount:1,partyAmount:-1}));
  assert.deepEqual(model.salesRegisterMonths(rows,{from:'2023-12-31',to:'2024-02-29'}).map(m=>[m.key,m.entries.length]),[['202312',0],['202401',1],['202402',2]]);
  assert.deepEqual(model.salesRegisterMonths(rows,{from:'2024-02-29',to:'2024-02-29'}).map(m=>m.entries.length),[1]);
  assert.deepEqual(model.salesRegisterMonths(rows,{from:'2024-04-30',to:'2024-04-01'}),[]);
  assert.deepEqual(model.salesRegisterMonths(rows,{from:'2023-02-29',to:'2023-03-01'}),[]);
  assert.deepEqual(model.salesRegisterMonths(rows,{from:'2024-04-01',to:''}),[]);
  assert.deepEqual(model.salesRegisterMonths(rows,{}).map(m=>m.key),['202312','202401','202402','202403']);
});

test('empty selected months have zero totals; mixed posting signs remain signed',()=>{
  assert.deepEqual(model.salesRegisterTotals([]),{debit:0,credit:0,closing:0});
  assert.deepEqual(model.salesRegisterTotals([{partyAmount:-12},{partyAmount:2}]),{debit:2,credit:12,closing:10});
});
import fs from 'node:fs';

test('verified April API fixture yields one month, all 103 drill-down vouchers and original posting signs',()=>{
  const fixture=new URL('../../../../../diagnostics/sales-register-ui/april-api-current.json',import.meta.url);
  const data=JSON.parse(fs.readFileSync(fixture,'utf8').replace(/^\uFEFF/,''));
  const months=model.salesRegisterMonths(data.vouchers,{from:'2024-04-01',to:'2024-04-30'});
  assert.equal(months.length,1);
  const april=months[0];
  assert.equal(april.label,'April 2024');
  assert.equal(april.entries.length,103);
  assert.equal(new Set(april.entries.map(v=>v.guid)).size,103);
  assert.equal(Math.round(april.total*100),424192910);
  assert.deepEqual([0,1,2].map(page=>april.entries.slice(page*50,(page+1)*50).length),[50,50,3]);
  let postings=0;
  for(const voucher of april.entries){
    postings+=voucher.postings.length;
    const party=voucher.postings.filter(p=>p.ledgerGuid===voucher.partyLedgerGuid).reduce((sum,p)=>sum+p.amount,0);
    assert.ok(Math.abs(party-voucher.partyAmount)<0.00001);
    assert.ok(Math.abs(voucher.postings.reduce((sum,p)=>sum+p.amount,0))<0.00001);
  }
  assert.equal(postings,250);
  const totals=model.salesRegisterTotals(april.entries);
  assert.equal(Math.round(totals.credit*100),424192910);
  assert.equal(totals.debit,0);
  assert.equal(Math.round(totals.closing*100),424192910);
  assert.equal(model.salesRegisterMonths(data.vouchers,{from:'2024-05-01',to:'2024-05-31'})[0].entries.length,0);
});

test('production presets produce calendar months and company financial years dynamically',()=>{
  const source=fs.readFileSync(new URL('./SalesRegister.jsx',import.meta.url),'utf8');
  const helpers=source.slice(source.indexOf('const pad ='),source.indexOf('const esc ='));
  const bounds=new Function('parseSalesDate',helpers+'; return bounds;')(model.parseSalesDate);
  const company={startingFrom:'20210401'};
  for(const [period,now,from,to,count] of [
    ['This Month','2024-02-15','2024-02-01','2024-02-29',1],
    ['Last Month','2024-01-15','2023-12-01','2023-12-31',1],
    ['This Year','2026-10-22','2026-04-01','2027-03-31',12],
    ['Last Year','2026-10-22','2025-04-01','2026-03-31',12],
    ['This Year','2027-04-01','2027-04-01','2028-03-31',12],
    ['Custom Date Range','2026-10-22','2024-04-01','2024-04-30',1]
  ]) {
    const range=bounds(period,new Date(now+'T12:00:00'),company,'2024-04-01','2024-04-30');
    assert.deepEqual(range,{from,to});
    const months=model.salesRegisterMonths([],range);
    assert.equal(months.length,count);
    assert.equal(months[0].key,from.replaceAll('-','').slice(0,6));
    assert.equal(months.at(-1).key,to.replaceAll('-','').slice(0,6));
  }
  assert.deepEqual(bounds('All Available Data',new Date(),company),{from:'',to:''});
});

test('Sales Register movement opposes party postings without mutating them; closing sides follow net movement',()=>{
  const voucher={partyAmount:-125,postings:[{amount:-125},{amount:125}]};
  const before=JSON.stringify(voucher);
  assert.equal(model.salesMovementAmount(voucher),125);
  assert.equal(JSON.stringify(voucher),before);
  assert.deepEqual(model.salesClosingBalance(125),{amount:125,side:'Cr'});
  assert.deepEqual(model.salesClosingBalance(-25),{amount:25,side:'Dr'});
  assert.deepEqual(model.salesClosingBalance(0),{amount:0,side:''});
  assert.equal(model.salesClosingBalance(null),null);
  const rows=[{date:'20240101',amount:125,partyAmount:-125},{date:'20240201',amount:25,partyAmount:25}];
  const months=model.salesRegisterMonths(rows,{from:'2024-01-01',to:'2024-03-31'});
  assert.deepEqual(months.map((m,i)=>model.salesRegisterTotals(months.slice(0,i+1).flatMap(x=>x.entries)).closing),[125,100,100]);
});
