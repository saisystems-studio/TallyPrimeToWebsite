import { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { outstandingBillFilename, outstandingBillPdf, renderOutstandingBill } from './outstandingBillDocument';
import { printInvoicePages } from '../Vouchers/invoiceRenderer';
import '../Vouchers/InvoicePreview.css';

export default function OutstandingBillPreview({ model, onClose }) {
  const [pages, setPages] = useState(null);
  const [error, setError] = useState('');
  const [zoom, setZoom] = useState(() => Math.min(1, Math.max(.35, (window.innerWidth - 64) / 794)));
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let active = true;
    const previous = document.activeElement;
    const overflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    (async () => {
      await document.fonts.ready;
      const canvases = renderOutstandingBill(model);
      if (active) setPages(canvases.map((canvas) => canvas.toDataURL('image/png')));
    })().catch((failure) => { if (active) setError(failure.message || 'Unable to prepare the statement.'); });
    return () => { active = false; document.body.style.overflow = overflow; previous?.focus(); };
  }, [model]);

  const download = () => {
    try {
      const pdf = outstandingBillPdf(model);
      const link = document.createElement('a'); const url = URL.createObjectURL(pdf);
      link.href = url; link.download = outstandingBillFilename(model); document.body.append(link); link.click(); link.remove();
      setTimeout(() => URL.revokeObjectURL(url), 60000);
    } catch (failure) { setError(failure.message || 'Unable to download the PDF.'); }
  };
  const print = async () => {
    if (!pages) return;
    setBusy(true);
    try { await printInvoicePages(pages, `${model.documentTitle || 'Outstanding Bill Statement'} ${model.billReference}`); }
    catch (failure) { setError(failure.message || 'Unable to open the print dialog.'); }
    finally { setBusy(false); }
  };
  const keydown = (event) => { if (event.key === 'Escape') { event.stopPropagation(); onClose(); } };

  return createPortal(<div className="invoice-preview-overlay" onClick={(event) => event.stopPropagation()}>
    <section className="invoice-preview" role="dialog" aria-modal="true" aria-labelledby="outstanding-preview-title" onKeyDown={keydown}>
      <header className="invoice-preview-toolbar">
        <div><h2 id="outstanding-preview-title">{model.documentTitle || 'Outstanding Bill Statement'}</h2><small>A4 portrait · {pages ? `${pages.length} page(s)` : 'Preparing document…'}</small></div>
        <div className="invoice-preview-controls">
          <button type="button" disabled={!pages || busy} onClick={download}>Download PDF</button>
          <button type="button" disabled={!pages || busy} onClick={print}>{busy ? 'Preparing…' : 'Print'}</button>
          <label>Zoom <select aria-label="Statement zoom" value={zoom} onChange={(event) => setZoom(Number(event.target.value))}>{[...new Set([.35, .5, .75, 1, 1.25, 1.5, zoom])].sort((a, b) => a - b).map((value) => <option key={value} value={value}>{Math.round(value * 100)}%</option>)}</select></label>
          <button type="button" onClick={onClose} aria-label="Close statement preview">Close ×</button>
        </div>
      </header>
      {error && <div role="alert" className="invoice-preview-error">{error}</div>}
      <div className="invoice-preview-stage" aria-busy={!pages && !error}>
        {!pages && !error && <p role="status">Preparing statement pages…</p>}
        <div className="invoice-preview-sheets">{pages?.map((src, index) => <img key={index} src={src} alt={`Outstanding statement page ${index + 1}`} style={{ width: 794 * zoom, height: 1123 * zoom }} />)}</div>
      </div>
    </section>
  </div>, document.body);
}
