import fs from 'node:fs';
import assert from 'node:assert/strict';
import { salesRegisterTotals } from '../../TallyWebFrontend/src/pages/Reports/SalesRegister/salesRegisterModel.js';
const data=JSON.parse(fs.readFileSync(new URL('./april-api-current.json',import.meta.url),'utf8').replace(/^\uFEFF/,''));
assert.equal(data.totalRecords,103);
assert.equal(data.vouchers.length,103);
assert.equal(data.vouchers.reduce((n,v)=>n+v.postings.length,0),250);
assert.equal(Math.round(data.vouchers.reduce((n,v)=>n+Math.abs(v.amount),0)*100),424192910);
assert.deepEqual([0,1,2].map(p=>data.vouchers.slice(p*50,(p+1)*50).length),[50,50,3]);
console.log('Current API: April summary, total, postings and drill-down pagination assertions passed.',salesRegisterTotals(data.vouchers));
const src=fs.readFileSync(new URL('../../TallyWebFrontend/src/pages/Reports/SalesRegister/SalesRegister.jsx',import.meta.url),'utf8');
const helpers=src.slice(src.indexOf('const pad ='),src.indexOf('const esc ='));
const bounds=new Function(helpers+';return bounds;')();
const c={startingFrom:'20210401'};
for(const [now,period,expected] of [
['2026-10-22','This Year',{from:'2026-04-01',to:'2027-03-31'}],
['2026-10-22','Last Year',{from:'2025-04-01',to:'2026-03-31'}],
['2027-03-31','This Year',{from:'2026-04-01',to:'2027-03-31'}],
['2027-04-01','This Year',{from:'2027-04-01',to:'2028-03-31'}],
['2026-10-22','Custom Date Range',{from:'2024-04-01',to:'2024-04-30'}],
['2026-10-22','All Available Data',{from:'',to:''}]
]) assert.deepEqual(bounds(period,new Date(now+'T12:00:00'),c,'2024-04-01','2024-04-30'),expected);
console.log('Production date-filter functions: current/last year, financial-year rollover, custom, all-available passed.');
