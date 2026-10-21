import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { prepareInvoiceDocument } from './invoiceData';
import { invoicePdf, invoicePdfFilename, printInvoicePages } from './invoiceRenderer';
import './InvoicePreview.css';

export default function InvoicePreview({ voucher, company, onClose, showTaxLedgerEntries = false }) {
  const [documentData, setDocumentData] = useState(null);
  const [error, setError] = useState('');
  const [zoom, setZoom] = useState(() => Math.min(1, Math.max(.35, (window.innerWidth-64)/794)));
  const [busy, setBusy] = useState(false);
  const dialog = useRef(null); const close = useRef(null);
  useEffect(() => {
    const controller = new AbortController(); let active = true;
    const previous = document.activeElement; const overflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden'; close.current?.focus();
    (async () => {
      const prepared = await prepareInvoiceDocument(company,voucher,controller.signal,{ includeTaxLedgerEntries: showTaxLedgerEntries });
      if (!active) return;
      setDocumentData(prepared);
    })().catch(e => { if (active && e.name !== 'AbortError') setError(e.message || 'Unable to prepare the invoice.'); });
    return () => { active = false; controller.abort(); document.body.style.overflow = overflow; previous?.focus(); };
  }, [company,voucher]);

  const download = () => {
    setError('');
    try {
      const file = new File([invoicePdf(documentData.canvases)], invoicePdfFilename(documentData.model), { type: 'application/pdf' });
      const url = URL.createObjectURL(file);
      const link = document.createElement('a'); link.href = url;
      link.download = file.name;
      document.body.append(link); link.click(); link.remove();
      setTimeout(() => URL.revokeObjectURL(url),60000);
    } catch (e) { setError(e.message || 'Unable to download the PDF.'); }
  };
  const print = async () => {
    setError('');
    setBusy(true);
    try {
      await printInvoicePages(documentData.images,`${documentData.model.heading} ${documentData.model.number}`);
    } catch (e) { setError(e.message || 'Unable to open the print dialog.'); }
    finally { setBusy(false); }
  };
  const keydown = (event) => {
    if (event.key === 'Escape') { event.stopPropagation(); onClose(); }
    if (event.key === 'Tab') {
      const controls = [...dialog.current.querySelectorAll('button:not(:disabled),input:not(:disabled),select:not(:disabled)')];
      const first = controls[0], last = controls.at(-1);
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
    }
  };
  return createPortal(<div className="invoice-preview-overlay" onClick={e => e.stopPropagation()}>
    <section className="invoice-preview" role="dialog" aria-modal="true" aria-labelledby="invoice-preview-title" ref={dialog} onKeyDown={keydown}>
      <header className="invoice-preview-toolbar">
        <div><h2 id="invoice-preview-title">{documentData?.model.isInvoice === false ? 'Voucher' : 'Invoice'} preview</h2><small>A4 portrait · {documentData ? `${documentData.images.length} page(s)` : 'Preparing document…'}</small></div>
        <div className="invoice-preview-controls">
          <button type="button" disabled={!documentData || busy} onClick={download}>Download PDF</button>
          <button type="button" disabled={!documentData || busy} onClick={print}>{busy ? 'Preparing…' : 'Print'}</button>
          <label>Zoom <select aria-label="Invoice zoom" value={zoom} onChange={e => setZoom(Number(e.target.value))}>{[...new Set([.35,.5,.75,1,1.25,1.5,zoom])].sort((a,b) => a-b).map(value => <option key={value} value={value}>{Math.round(value*100)}%</option>)}</select></label>
          <button type="button" ref={close} onClick={onClose} aria-label="Close invoice preview">Close ×</button>
        </div>
      </header>
      {error && <div role="alert" className="invoice-preview-error">{error}</div>}
      <div className="invoice-preview-stage" aria-busy={!documentData && !error}>
        {!documentData && !error && <p role="status">Preparing invoice pages…</p>}
        <div className="invoice-preview-sheets">{documentData?.images.map((src,index) => <img key={index} src={src} alt={`${documentData.model.heading} page ${index+1}`} style={{ width: 794*zoom, height: 1123*zoom }} />)}</div>
      </div>
    </section>
  </div>, document.body);
}
