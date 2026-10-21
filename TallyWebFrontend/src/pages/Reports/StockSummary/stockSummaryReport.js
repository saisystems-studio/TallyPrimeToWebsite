const movements = ["inward", "outward", "closing"];

export function formatNumber(value, digits = 2) {
  if (value == null || !Number.isFinite(Number(value))) return "";
  const number = Number(value);
  const text = Math.abs(number).toLocaleString("en-IN", {
    minimumFractionDigits: digits, maximumFractionDigits: digits,
  });
  return number < 0 ? "(-)" + text : text;
}

export function rowCells(data) {
  return movements.flatMap((name) => [
    data[name + "QuantityText"] ?? (data[name + "Quantity"] == null ? "" :
      formatNumber(data[name + "Quantity"], 3) + (data.unit ? " " + data.unit : "")),
    data[name + "RateText"] ?? formatNumber(data[name + "Rate"]),
    formatNumber(data[name + "Value"]),
  ]);
}

export const movementNames = movements;

export function flattenRows(groups = [], collapsed = new Set(), parents = []) {
  return groups.flatMap(group => {
    const row = { kind: "group", key: "group:" + group.groupName, data: group, depth: parents.length, parents };
    if (collapsed.has(group.groupName)) return [row];
    const ancestors = [...parents, row];
    return [row, ...flattenRows(group.children, collapsed, ancestors),
      ...(group.items || []).map(item => ({
        kind: "item", key: "item:" + item.stockItemName, data: item, depth: ancestors.length, parents: ancestors,
      }))];
  });
}

export function flattenItemRows(groups = []) {
  return groups.flatMap(group => [
    ...flattenItemRows(group.children || []),
    ...(group.items || []).map(item => ({
      kind: "item", key: "item:" + item.stockItemName, data: item, depth: 0, parents: [],
    })),
  ]);
}

export function pageRows(rows, page, size) {
  const selected = rows.slice((page - 1) * size, page * size);
  const output = [];
  const included = new Set();
  for (const row of selected) {
    for (const parent of row.parents) {
      if (!included.has(parent.key)) {
        output.push({ ...parent, continued: true });
        included.add(parent.key);
      }
    }
    if (!included.has(row.key)) { output.push(row); included.add(row.key); }
  }
  return output;
}

export function displayDate(value = "") {
  const compact = value.replaceAll("-", "");
  return compact.length === 8 ? compact.slice(6) + "/" + compact.slice(4, 6) + "/" + compact.slice(0, 4) : value;
}

const escapeXml = (value) => String(value ?? "").replaceAll("&", "&amp;")
  .replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&apos;");

function exportRows(report, itemWise = false) {
  const rows = itemWise ? flattenItemRows(report.groups) : flattenRows(report.groups);
  return [...rows, { kind: "total", depth: 0, data: { ...report.grandTotal, groupName: "Grand Total" } }];
}

// SpreadsheetML is a native Excel format. Explicit string types prevent formula execution
// for item/company names beginning with '=', '+', '-' or '@'.
export function spreadsheetXml(report, { itemWise = false } = {}) {
  const cell = (value, type = "String", style = "Text", merge = 0) =>
    '<Cell ss:StyleID="' + style + '"' + (merge ? ' ss:MergeAcross="' + merge + '"' : "") +
    '><Data ss:Type="' + type + '">' + escapeXml(value) + "</Data></Cell>";
  const row = cells => "<Row>" + cells.join("") + "</Row>";
  const title = report.companyName + " � Stock Summary";
  const body = exportRows(report, itemWise).map(({ data, depth, kind }) => {
    const text = rowCells(data);
    const cells = [cell("  ".repeat(depth) + (data.stockItemName ?? data.groupName), "String", kind === "item" ? "Text" : "Bold")];
    movements.forEach((name, index) => {
      cells.push(cell(text[index * 3], "String", "Right"));
      cells.push(data[name + "Rate"] == null ? cell(text[index * 3 + 1], "String", "Right") : cell(data[name + "Rate"], "Number", "Number"));
      cells.push(data[name + "Value"] == null
        ? cell(text[index * 3 + 2], "String", "Right")
        : cell(data[name + "Value"], "Number", kind === "item" ? "Number" : "Total"));
    });
    return row(cells);
  }).join("");
  return '<?xml version="1.0" encoding="UTF-8"?><?mso-application progid="Excel.Sheet"?>' +
    '<Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet" xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet">' +
    '<Styles><Style ss:ID="Text"/><Style ss:ID="Right"><Alignment ss:Horizontal="Right"/></Style>' +
    '<Style ss:ID="Bold"><Font ss:Bold="1"/></Style>' +
    '<Style ss:ID="Number"><Alignment ss:Horizontal="Right"/><NumberFormat ss:Format="#,##0.00;&quot;(-)&quot;#,##0.00"/></Style>' +
    '<Style ss:ID="Total"><Font ss:Bold="1"/><NumberFormat ss:Format="#,##0.00;&quot;(-)&quot;#,##0.00"/></Style></Styles>' +
    '<Worksheet ss:Name="Stock Summary"><Table><Column ss:Width="260"/><Column ss:Span="8" ss:Width="100"/>' +
    row([cell(title, "String", "Bold", 9)]) +
    row([cell(displayDate(report.fromDate) + " to " + displayDate(report.toDate), "String", "Text", 9)]) +
    row([cell("Particulars", "String", "Bold"), ...["Inwards", "Outwards", "Closing Balance"].map(x => cell(x, "String", "Bold", 2))]) +
    row([cell(""), ...Array.from({ length: movements.length }, () => ["Quantity", "Rate", "Value"].map(x => cell(x, "String", "Bold"))).flat()]) +
    body + "</Table></Worksheet></Workbook>";
}

export function printHtml(report, { itemWise = false } = {}) {
  const rows = exportRows(report, itemWise).map(({ data, depth, kind }) =>
    '<tr class="' + kind + '"><td style="padding-left:' + (8 + depth * 14) + 'px">' +
    escapeXml(data.stockItemName ?? data.groupName) + "</td>" +
    rowCells(data).map(text => "<td>" + escapeXml(text) + "</td>").join("") + "</tr>").join("");
  return '<!doctype html><html><head><meta charset="utf-8"><title>Stock Summary</title><style>' +
    '@page{size:A4 landscape;margin:10mm}body{font:11px Arial;color:#172b3a}h1{font-size:18px;margin-bottom:5px}' +
    'table{width:100%;border-collapse:collapse}th,td{padding:6px 8px;border:1px solid #ccd3d9;text-align:right}' +
    'th:first-child,td:first-child{text-align:left}thead{display:table-header-group}tr{break-inside:avoid}' +
    '.group{font-weight:bold;background:#edf3f6}.total{font-weight:bold;border-top:2px solid #172b3a}' +
    '</style></head><body><h1>' + escapeXml(report.companyName) + '</h1><p>Stock Summary � ' +
    escapeXml(displayDate(report.fromDate) + " to " + displayDate(report.toDate)) +
    '</p><table><thead><tr><th rowspan="2">Particulars</th><th colspan="3">Inwards</th><th colspan="3">Outwards</th>' +
    '<th colspan="3">Closing Balance</th></tr><tr>' +
    Array.from({ length: movements.length }, () => ["Quantity", "Rate", "Value"]).flat().map(x => "<th>" + x + "</th>").join("") +
    '</tr></thead><tbody>' + rows + '</tbody></table></body></html>';
}

