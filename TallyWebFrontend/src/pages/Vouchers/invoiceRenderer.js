import { paginateRows } from './invoiceDocument.js';
import { imagePdf } from './voucherExport.js';

export const PAGE_WIDTH = 1190;
export const PAGE_HEIGHT = 1684;
const FONT = '"Nirmala UI", "Noto Sans Tamil", Latha, Arial, sans-serif';
const X = 60, WIDTH = 1070, BOTTOM = 1600, LINE = 25;

export function renderInvoice(model, createCanvas = () => document.createElement('canvas')) {
  const measure = createCanvas(); measure.width = PAGE_WIDTH; measure.height = PAGE_HEIGHT;
  const m = measure.getContext('2d');
  if (!m) throw new Error('Invoice rendering is unavailable in this browser.');
  const font = (ctx, bold = false, size = 18) => { ctx.font = `${bold ? 'bold ' : ''}${size}px ${FONT}`; };
  const segmenter = new Intl.Segmenter(undefined, { granularity: 'grapheme' });
  const wrap = (text, width, bold = false, size = 18) => {
    font(m, bold, size); const lines = [];
    for (const paragraph of String(text || '').split('\n')) {
      let line = '';
      for (const { segment } of segmenter.segment(paragraph)) {
        if (line && m.measureText(line + segment).width > width) {
          const space = line.lastIndexOf(' ');
          if (space > 0) { lines.push(line.slice(0,space)); line = line.slice(space+1); }
          else { lines.push(line); line = ''; }
        }
        line += segment;
      }
      lines.push(line);
    }
    return lines;
  };
  const partyLines = (party) => [party.name, party.address, party.city && !String(party.address || '').split('\n').some(line => line.trim() === party.city) ? party.city : '', party.pincode ? `Pincode: ${party.pincode}` : '', party.gstin ? `GSTIN/UIN: ${party.gstin}` : '', party.state || party.stateCode ? `State Name: ${party.state || ''}${party.stateCode ? `, Code: ${party.stateCode}` : ''}` : '', party.country ? `Country: ${party.country}` : '', party.phone ? `Phone: ${party.phone}` : '', party.mobile && party.mobile !== party.phone ? `Mobile: ${party.mobile}` : '', party.email ? `E-Mail: ${party.email}` : '', party.website ? `Website: ${party.website}` : ''].filter(Boolean);
  const companyLines = partyLines(model.company);
  const billLines = partyLines(model.party);
  const blockHeight = (lines, min) => Math.max(min, lines.reduce((height, line, i) => height + wrap(line, WIDTH/2-16, i === 0).length*LINE, 12)+LINE);
  const companyHeight = blockHeight(companyLines, 145);
  const consigneeLines = partyLines(model.consignee);
  const showConsignee = model.isInvoice && consigneeLines.length > 0;
  const shipHeight = showConsignee ? blockHeight(consigneeLines, 115) : 0;
  const billHeight = blockHeight(billLines, 135);
  const headerHeight = companyHeight + shipHeight + billHeight;
  const firstTableY = 94 + headerHeight;
  if (firstTableY > 1220) throw new Error('Company or party details are too long for the invoice header. Review the master address before exporting.');
  const columns = model.isInvoice ? [32, 500, 140, 100, 45, 75, 178] : [52, 780, 238];
  const labels = model.isInvoice ? ['Sl\nNo.', 'Description of Goods', 'Quantity', 'Rate', 'per', 'Disc. %', 'Amount'] : ['Sl No.', 'Ledger / Account', 'Amount'];
  const prepared = model.rows.map((row) => {
    const cells = model.isInvoice ? [row.serial, row.description, row.quantity, row.rate, row.per, row.discount, row.amount] : [row.serial, row.description, row.amount];
    const lines = cells.map((cell, i) => wrap(cell, columns[i]-12, i === 1 || i === cells.length-1));
    return { ...row, lines, height: Math.max(35, ...lines.map(lines => lines.length*LINE+12)) };
  });
  const noteLines = model.narration ? wrap(`Narration: ${model.narration}`, WIDTH-20) : [];
  const taxLines = model.isInvoice ? (model.taxEntries || []).flatMap(entry => wrap(`${entry.name}: ${entry.amount}`, WIDTH-24, false, 16)) : [];
  const taxSummaryHeight = taxLines.length ? (taxLines.length + 1) * LINE + 12 : 0;
  const wordLines = wrap(model.words, WIDTH-155, true);
  const quantityLines = wrap(model.isInvoice ? model.quantityTotal : '', columns[2]-12, true,16);
  const totalLines = wrap(model.total,columns.at(-1)-14,true,22);
  const totalHeight = Math.max(38, quantityLines.length*LINE+10,totalLines.length*LINE+10);
  const signer = model.purchase ? model.party.name : model.company.name;
  const signerLines = wrap(signer ? `for ${signer}` : '',WIDTH/2-16,true,17);
  const signatureHeight = Math.max(92,signerLines.length*LINE+45);
  const gstLines = model.company.gstin ? wrap(`Company's GSTIN/UIN: ${model.company.gstin}`,WIDTH-16,false,17) : [];
  const footerHeight = totalHeight + 60 + signatureHeight + gstLines.length*LINE + Math.max(35,wordLines.length*LINE) + noteLines.length*LINE;
  const tableHeader = 46;
  const pages = paginateRows(prepared, BOTTOM-firstTableY-tableHeader, BOTTOM-170-tableHeader, footerHeight + taxSummaryHeight);
  const results = [];
  pages.forEach((rows, pageIndex) => {
    const canvas = createCanvas(); canvas.width = PAGE_WIDTH; canvas.height = PAGE_HEIGHT;
    const ctx = canvas.getContext('2d');
    ctx.fillStyle = '#fff'; ctx.fillRect(0,0,PAGE_WIDTH,PAGE_HEIGHT); ctx.fillStyle = '#000'; ctx.strokeStyle = '#000'; ctx.lineWidth = 1;
    const line = (x1,y1,x2,y2) => { ctx.beginPath(); ctx.moveTo(x1,y1); ctx.lineTo(x2,y2); ctx.stroke(); };
    const text = (value, x, y, { bold = false, size = 18, align = 'left' } = {}) => { font(ctx,bold,size); ctx.textAlign = align; ctx.fillText(value || '',x,y); ctx.textAlign = 'left'; };
    const drawLines = (lines, x, y, options = {}) => lines.forEach((l,i) => text(l,x,y+i*LINE,options));
    const block = (title, lines, top, height) => {
      text(title,X+7,top+22);
      let y = top + (title ? 48 : 27);
      lines.forEach((content, i) => { const wrapped = wrap(content,WIDTH/2-16,i === 0); drawLines(wrapped,X+7,y,{bold:i === 0}); y += wrapped.length*LINE; });
      line(X,top+height,X+WIDTH/2,top+height);
    };
    text(model.heading, PAGE_WIDTH/2,57,{bold:true,size:26,align:'center'});
    let tableY;
    if (pageIndex === 0) {
      const mid = X+WIDTH/2;
      ctx.strokeRect(X,94,WIDTH,headerHeight); line(mid,94,mid,94+headerHeight);
      block('',companyLines,94,companyHeight);
      let top = 94+companyHeight;
      if (showConsignee) { block('Consignee (Ship To)',consigneeLines,top,shipHeight); top += shipHeight; }
      block(model.partyLabel,billLines,top,billHeight);
      const fields = [[model.isInvoice ? 'Invoice No.' : 'Voucher No.',model.number],['Dated',model.date],[model.isInvoice ? 'Supplier Invoice No. & Date' : 'Reference',model.isInvoice ? [model.supplierNumber,model.supplierDate].filter(Boolean).join('  ') : model.reference],['Other References',model.isInvoice ? model.reference : '']];
      const fieldWidth = WIDTH/4;
      const heights = fields.map((field) => 32+wrap(field[1],fieldWidth-14,true).length*LINE);
      const h1 = Math.max(65,heights[0],heights[1]); const h2 = Math.max(65,heights[2],heights[3]);
      if (h1+h2 > headerHeight) throw new Error('Invoice references are too long for the header.');
      fields.forEach(([label,value],i) => {
        const left = mid+(i%2)*fieldWidth; const top = i<2 ? 94 : 94+h1;
        text(label,left+6,top+21,{size:16}); drawLines(wrap(value,fieldWidth-14,true),left+6,top+47,{bold:true});
      });
      line(mid+fieldWidth,94,mid+fieldWidth,94+h1+h2); line(mid,94+h1,X+WIDTH,94+h1); line(mid,94+h1+h2,X+WIDTH,94+h1+h2);
      tableY = firstTableY;
    } else {
      text(model.company.name,X,99,{bold:true,size:22});
      text(`${model.isInvoice ? 'Invoice' : 'Voucher'} No. ${model.number}   |   ${model.date}`,X,130);
      text('Continued',X+WIDTH,130,{align:'right'}); tableY = 170;
    }
    const last = pageIndex === pages.length-1;
    const tableBottom = last ? BOTTOM-footerHeight : BOTTOM;
    ctx.strokeRect(X,tableY,WIDTH,tableBottom-tableY);
    let left = X;
    labels.forEach((label,i) => { drawLines(label.split('\n'),left+columns[i]/2,tableY+20,{size:16,align:'center'}); left += columns[i]; if (i<columns.length-1) line(left,tableY,left,tableBottom); });
    line(X,tableY+tableHeader,X+WIDTH,tableY+tableHeader);
    let y = tableY+tableHeader;
    rows.forEach(row => {
      let x = X;
      row.lines.forEach((lines,i) => { const numeric = i !== 1; drawLines(lines, numeric ? x+columns[i]-6 : x+6,y+25,{bold:i === 1 || i === row.lines.length-1,align:numeric?'right':'left'}); x += columns[i]; });
      y += row.height;
    });
    if (last && taxLines.length) {
      text('GST / Tax Ledger Entries',X+7,y+20,{bold:true,size:16});
      drawLines(taxLines,X+12,y+45,{size:16});
    }
    if (last) {
      const totalY = tableBottom;
      ctx.strokeRect(X,totalY,WIDTH,totalHeight);
      const descriptionEnd = X+columns[0]+columns[1];
      text('Total',descriptionEnd-8,totalY+25,{align:'right',bold:true});
      if (model.isInvoice) drawLines(quantityLines,descriptionEnd+columns[2]-6,totalY+25,{bold:true,size:16,align:'right'});
      let border = X;
      columns.slice(0,-1).forEach(width => { border += width; line(border,totalY,border,totalY+totalHeight); });
      drawLines(totalLines,X+WIDTH-7,totalY+25,{bold:true,size:22,align:'right'});
      const footerY = totalY+totalHeight;
      ctx.strokeRect(X,footerY,WIDTH,BOTTOM-footerY);
      text('Amount Chargeable (in words)',X+7,footerY+24,{size:17});
      text('E. & O.E.',X+WIDTH-7,footerY+24,{align:'right',size:17});
      drawLines(wordLines,X+7,footerY+51,{bold:true});
      drawLines(noteLines,X+7,footerY+65+wordLines.length*LINE,{size:17});
      drawLines(gstLines,X+7,BOTTOM-signatureHeight-gstLines.length*LINE-3,{size:17});
      ctx.strokeRect(X+WIDTH/2,BOTTOM-signatureHeight,WIDTH/2,signatureHeight);
      drawLines(signerLines,X+WIDTH-7,BOTTOM-signatureHeight+25,{bold:true,size:17,align:'right'});
      text('Authorised Signatory',X+WIDTH-7,BOTTOM-12,{align:'right',size:17});
    } else text('Continued on next page',X+WIDTH,BOTTOM+24,{align:'right',size:15});
    text(`Page ${pageIndex+1} of ${pages.length}`,PAGE_WIDTH/2,1640,{align:'center',size:15});
    results.push(canvas);
  });
  return results;
}

export function invoicePdf(canvases) {
  return imagePdf(canvases.map(canvas => {
    const raw = atob(canvas.toDataURL('image/jpeg',0.98).split(',')[1]);
    return Uint8Array.from(raw,c => c.charCodeAt(0));
  }));
}

export function invoicePdfFilename(model) {
  const number = String(model.number || 'details').replace(/[^\p{L}\p{N}._-]/gu, '_');
  return `${model.isInvoice ? 'Invoice' : 'Voucher'}-${number}.pdf`;
}

export async function shareInvoicePdf(file, {
  navigatorObject = globalThis.navigator,
  fallbackDownload,
} = {}) {
  let canShareFile = false;
  try {
    canShareFile = typeof navigatorObject?.share === 'function'
      && typeof navigatorObject?.canShare === 'function'
      && navigatorObject.canShare({ files: [file] });
  } catch {
    canShareFile = false;
  }

  if (canShareFile) {
    try {
      await navigatorObject.share({ files: [file], title: file.name });
      return 'shared';
    } catch (error) {
      if (error?.name === 'AbortError') return 'cancelled';
    }
  }

  fallbackDownload?.(file);
  return 'downloaded';
}

export async function printInvoicePages(sources, title, open = () => window.open('', '_blank', 'width=960,height=800')) {
  const popup = open();
  if (!popup) throw new Error('Allow popups to open the invoice print dialog.');
  popup.document.title = title;
  const style = popup.document.createElement('style');
  style.textContent = '@page{size:A4 portrait;margin:0}html,body{padding:0;margin:0}img{display:block;width:210mm;height:297mm;break-after:page;page-break-after:always}img:last-child{break-after:auto;page-break-after:auto}';
  popup.document.head.append(style);
  const images = sources.map((src,index) => {
    const image = popup.document.createElement('img'); image.alt = `Document page ${index+1}`; image.src = src; popup.document.body.append(image); return image;
  });
  popup.document.close();
  await Promise.all(images.map(image => image.decode()));
  if (!popup.closed) { popup.focus(); popup.print(); }
}
