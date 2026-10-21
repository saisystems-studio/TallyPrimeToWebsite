import { useEffect, useRef, useState } from 'react';
import { shareUrl, voucherText } from './voucherExport';
import InvoicePreview from './InvoicePreview';

function Icon({ share = false }) {
  return <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">{share ? <><circle cx="18" cy="5" r="3"/><circle cx="6" cy="12" r="3"/><circle cx="18" cy="19" r="3"/><path d="m8.6 10.5 6.8-4M8.6 13.5l6.8 4"/></> : <><path d="M12 3v12m-4-4 4 4 4-4M4 15v5h16v-5"/></>}</svg>;
}

export default function VoucherActions({ voucher, company, disabled }) {
  const companyName = company?.name;
  const [preview, setPreview] = useState(false);
  const [menu, setMenu] = useState(null);
  const [message, setMessage] = useState('');
  const root = useRef(null);
  const exportButton = useRef(null); const shareButton = useRef(null);
  useEffect(() => {
    const outside = (event) => { if (!root.current?.contains(event.target)) setMenu(null); };
    document.addEventListener('pointerdown', outside);
    return () => document.removeEventListener('pointerdown', outside);
  }, []);
  useEffect(() => {
    if (menu) root.current?.querySelector('[role="menuitem"]')?.focus();
  }, [menu]);

  const run = (action) => {
    setMenu(null); setMessage('');
    try {
      const text = voucherText(voucher, companyName);
      if (action === 'pdf' || action === 'print') {
        setPreview(true);
      } else if (action === 'whatsapp') {
        const popup = window.open(shareUrl('whatsapp', '', text), '_blank');
        if (!popup) throw new Error('Allow popups to open WhatsApp.');
        popup.opener = null;
        setMessage('WhatsApp opened with voucher details. Review and send there.');
      } else {
        const subject = `${companyName || 'Voucher'} — ${voucher.voucherType || ''} ${voucher.voucherNumber || ''}`;
        window.location.href = shareUrl('email', subject, text);
        setMessage('Email draft requested in your email app. No PDF is attached.');
      }
    } catch (error) { setMessage(error.message || 'Unable to complete this action.'); }
    (menu === 'export' ? exportButton : shareButton).current?.focus();
  };

  const keys = (event) => {
    if (event.key === 'Escape' && menu) {
      event.stopPropagation(); setMenu(null);
      (menu === 'export' ? exportButton : shareButton).current?.focus();
    }
    if (event.key === 'Tab') setMenu(null);
    if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key) && menu) {
      event.preventDefault();
      const items = [...root.current.querySelectorAll('[role="menuitem"]')];
      const index = items.indexOf(document.activeElement);
      const next = event.key === 'Home' ? 0 : event.key === 'End' ? items.length-1 : (index + (event.key === 'ArrowUp' ? -1 : 1) + items.length) % items.length;
      items[next]?.focus();
    }
  };

  return <div className="voucher-actions" ref={root} onKeyDown={keys}>
    {preview && <InvoicePreview voucher={voucher} company={company} onClose={() => setPreview(false)} />}
    <div className="voucher-action-anchor">
      <button ref={exportButton} type="button" className="voucher-action-button" aria-label="Export voucher" title="Export" disabled={disabled} aria-haspopup="menu" aria-expanded={menu === 'export'} onClick={() => setMenu(menu === 'export' ? null : 'export')}><Icon/><span>Export</span></button>
      {menu === 'export' && <div className="voucher-action-menu" role="menu" aria-label="Export voucher"><button type="button" role="menuitem" onClick={() => run('pdf')}>Download PDF</button><button type="button" role="menuitem" onClick={() => run('print')}>Print</button></div>}
    </div>
    <div className="voucher-action-anchor">
      <button ref={shareButton} type="button" className="voucher-action-button" aria-label="Share voucher" title="Share" disabled={disabled} aria-haspopup="menu" aria-expanded={menu === 'share'} onClick={() => setMenu(menu === 'share' ? null : 'share')}><Icon share/><span>Share</span></button>
      {menu === 'share' && <div className="voucher-action-menu" role="menu" aria-label="Share voucher"><button type="button" role="menuitem" onClick={() => run('whatsapp')}>WhatsApp</button><button type="button" role="menuitem" onClick={() => run('email')}>Email</button></div>}
    </div>
    {message && <div role="status" className="voucher-action-status"><span>{message}</span><button type="button" aria-label="Dismiss action message" onClick={() => setMessage('')}>×</button></div>}
  </div>;
}
