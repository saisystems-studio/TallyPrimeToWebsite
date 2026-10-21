import test from 'node:test';
import assert from 'node:assert/strict';

// Synthetic fixtures are used only by tests, never by the report UI.
const report = {
  companyName: 'A & B <Ltd>', fromDate: '20240401', toDate: '20240430',
  groups: [{ groupName: 'Goods', closingValue: 24, children: [{ groupName: 'Parts', children: [], items: [
    { stockItemName: '=Widget', openingQuantity: 0, openingQuantityText: '0 nos', openingValue: 0, closingQuantity: -3, closingQuantityText: '(-)3 nos', closingValue: -36, unit: 'nos' },
  ] }], items: [{ stockItemName: 'Bulk', closingQuantityText: '3.000 kg', closingValue: 60 }] }],
  grandTotal: { closingValue: 24 },
};

test('collapse hides descendants and exports still include all items', async () => {
  const { flattenRows, spreadsheetXml } = await import('./stockSummaryReport.js');
  assert.equal(flattenRows(report.groups, new Set(['Goods'])).length, 1);
  assert.equal(flattenRows(report.groups).filter(row => row.kind === 'item').length, 2);
  const xml = spreadsheetXml(report);
  assert.match(xml, /A &amp; B &lt;Ltd&gt;/);
  assert.match(xml, /ss:Type="String">\s*=Widget/);
  assert.match(xml, /ss:Type="Number">-36/);
  assert.match(xml, /Grand Total/);
});

test('negative and native unit formatting survive display and print', async () => {
  const { formatNumber, rowCells, printHtml } = await import('./stockSummaryReport.js');
  assert.equal(formatNumber(-1234.5), '(-)1,234.50');
  assert.equal(formatNumber(null), '');
  const item = report.groups[0].children[0].items[0];
  assert.equal(rowCells(item)[6], '(-)3 nos');
  const html = printHtml(report);
  assert.match(html, /A &amp; B &lt;Ltd&gt;/);
  assert.match(html, /\(-\)3 nos/);
  assert.match(html, /Inwards/);
  assert.match(html, /Grand Total/);
});

test('pagination retains the group context for items on later pages', async () => {
  const { flattenRows, pageRows } = await import('./stockSummaryReport.js');
  const rows = flattenRows(report.groups);
  const page = pageRows(rows, 2, 2);
  assert.equal(page[0].data.groupName, 'Goods');
  assert.equal(page[0].continued, true);
  assert.equal(page[1].data.groupName, 'Parts');
  assert.equal(page[2].data.stockItemName, '=Widget');
});

test('item-wise view is flat and exports only loaded report items with unchanged totals', async () => {
  const { flattenItemRows, spreadsheetXml, printHtml } = await import('./stockSummaryReport.js');
  const rows = flattenItemRows(report.groups);
  assert.equal(rows.length, 2);
  assert.ok(rows.every(row => row.kind === 'item' && row.depth === 0));
  const xml = spreadsheetXml(report, { itemWise: true });
  assert.match(xml, /Grand Total/);
  assert.match(xml, /-36/);
  assert.doesNotMatch(xml, />Goods</);
  const html = printHtml(report, { itemWise: true });
  assert.match(html, /Grand Total/);
  assert.doesNotMatch(html, />Parts</);
});
