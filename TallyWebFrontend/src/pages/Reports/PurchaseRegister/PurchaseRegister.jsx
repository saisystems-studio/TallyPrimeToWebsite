import { useEffect, useMemo, useRef, useState } from "react";
import { getDbCompanies } from "../../../services/companyService";
import { getVoucherDetail } from "../../../services/voucherService";
import { getPurchaseVoucherPeriods, getPurchaseVouchers } from "../../../services/purchaseVouchersService";
import "./PurchaseRegister.css";

function inputDate(value) {
  const text = String(value || "").trim();
  if (/^\d{8}$/.test(text)) return `${text.slice(0, 4)}-${text.slice(4, 6)}-${text.slice(6, 8)}`;
  return /^\d{4}-\d{2}-\d{2}$/.test(text) ? text : "";
}

function savedDateRange(companyId) {
  try {
    const saved = JSON.parse(sessionStorage.getItem("purchase-register-date-range") || "null");
    if (String(saved?.companyId) === String(companyId)
      && /^\d{4}-\d{2}-\d{2}$/.test(saved.fromDate)
      && /^\d{4}-\d{2}-\d{2}$/.test(saved.toDate)
      && saved.fromDate <= saved.toDate) return saved;
  } catch {
    // Use the available database range when session storage is unavailable.
  }
  return null;
}

const apiDate = value => value.replaceAll("-", "");
const readableDate = value => {
  const text = String(value || "");
  if (/^\d{8}$/.test(text)) return `${text.slice(6, 8)}-${text.slice(4, 6)}-${text.slice(0, 4)}`;
  if (/^\d{4}-\d{2}-\d{2}$/.test(text)) return `${text.slice(8, 10)}-${text.slice(5, 7)}-${text.slice(0, 4)}`;
  return text || "—";
};

const money = value => {
  if (value === null || value === undefined || value === "" || !Number.isFinite(Number(value))) return "—";
  return `₹ ${Number(value).toLocaleString("en-IN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
};

const escapeHtml = value => String(value ?? "").replace(/[&<>"']/g, character => ({
  "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;",
}[character]));

function reportHtml(companyName, fromDate, toDate, rows, total) {
  const body = rows.map((row, index) => `<tr>
    <td>${index + 1}</td><td>${escapeHtml(readableDate(row.date))}</td>
    <td>${escapeHtml(row.partyName || "—")}</td><td>${escapeHtml(row.voucherType || "—")}</td>
    <td>${escapeHtml(row.voucherNumber || "—")}</td><td class="amount">${escapeHtml(row.amount == null ? "—" : money(row.amount))}</td>
  </tr>`).join("");

  return `<html><head><meta charset="utf-8"><title>Purchase Register</title></head><body>
    <h1>Purchase Register</h1><p>${escapeHtml(companyName)} · ${escapeHtml(readableDate(apiDate(fromDate)))} to ${escapeHtml(readableDate(apiDate(toDate)))}</p>
    <table><thead><tr><th>S.No</th><th>Date</th><th>Particulars</th><th>Voucher Type</th><th>Voucher No.</th><th>Amount</th></tr></thead>
    <tbody>${body}</tbody><tfoot><tr><th colspan="5">Total Purchase Amount</th><th class="amount">${escapeHtml(money(total))}</th></tr></tfoot></table>
    <style>body{font:12px Arial,sans-serif;padding:24px;color:#26344c}h1{font-size:20px}p{color:#66748a}table{border-collapse:collapse;width:100%}th,td{border:1px solid #cbd3df;padding:7px 9px;text-align:left}th{background:#f1f4f8}.amount{text-align:right}tfoot th{font-weight:700}</style>
  </body></html>`;
}

export default function PurchaseRegister() {
  const [companies, setCompanies] = useState([]);
  const [companyId, setCompanyId] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [search, setSearch] = useState("");
  const [rows, setRows] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [selectedRow, setSelectedRow] = useState(null);
  const [details, setDetails] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState("");
  const detailRequest = useRef(0);
  const periodRequest = useRef(0);
  const companyChangedByUser = useRef(false);

  const company = companies.find(item => String(item.id) === String(companyId));

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const data = await getDbCompanies();
        if (cancelled) return;
        const list = data.companies || data || [];
        setCompanies(list);
        const selected = list.find(item => String(item.id) === "1") || list[0];
        if (selected) {
          setCompanyId(String(selected.id));
        }
      } catch (requestError) {
        if (!cancelled) setError(requestError.message || "Unable to load companies.");
      }
    })();
    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    if (!companyId) return undefined;
    let cancelled = false;
    const requestId = ++periodRequest.current;
    setFromDate("");
    setToDate("");
    setRows([]);
    setError("");

    (async () => {
      try {
        const range = await getPurchaseVoucherPeriods(companyId);
        if (cancelled || requestId !== periodRequest.current) return;
        const start = inputDate(range.fromDate);
        const end = inputDate(range.toDate);
        if (range.hasVouchers && start && end) {
          const saved = companyChangedByUser.current ? null : savedDateRange(companyId);
          companyChangedByUser.current = false;
          setFromDate(saved?.fromDate || start);
          setToDate(saved?.toDate || end);
        } else {
          companyChangedByUser.current = false;
        }
      } catch (requestError) {
        if (!cancelled && requestId === periodRequest.current) {
          setError(requestError.message || "Unable to load available Purchase voucher dates.");
        }
      }
    })();

    return () => { cancelled = true; };
  }, [companyId]);

  useEffect(() => {
    let cancelled = false;
    const load = async () => {
      setError("");
      if (!companyId || !fromDate || !toDate) {
        setRows([]);
        setLoading(false);
        return;
      }
      if (fromDate > toDate) {
        setRows([]);
        setError("From Date must be on or before To Date.");
        return;
      }

      setLoading(true);
      try {
        const data = await getPurchaseVouchers(companyId, apiDate(fromDate), apiDate(toDate));
        if (!cancelled) setRows(data.vouchers || []);
      } catch (requestError) {
        if (!cancelled) {
          setRows([]);
          setError(requestError.message || "Unable to load Purchase vouchers.");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    load();
    return () => { cancelled = true; };
  }, [companyId, fromDate, toDate]);

  const visibleRows = useMemo(() => {
    const needle = search.trim().toLocaleLowerCase();
    if (!needle) return rows;
    return rows.filter(row => `${row.partyName || ""} ${row.voucherNumber || ""}`.toLocaleLowerCase().includes(needle));
  }, [rows, search]);

  const totalAmount = useMemo(() => visibleRows.reduce((total, row) => total + (Number.isFinite(Number(row.amount)) ? Number(row.amount) : 0), 0), [visibleRows]);

  const changeCompany = event => {
    const nextId = event.target.value;
    companyChangedByUser.current = true;
    setCompanyId(nextId);
    setFromDate("");
    setToDate("");
  };

  const changeDateRange = (nextFrom, nextTo) => {
    setFromDate(nextFrom);
    setToDate(nextTo);
    if (companyId && nextFrom && nextTo && nextFrom <= nextTo) {
      try {
        sessionStorage.setItem("purchase-register-date-range", JSON.stringify({ companyId, fromDate: nextFrom, toDate: nextTo }));
      } catch {
        // The current page state remains usable when session storage is unavailable.
      }
    }
  };

  const exportExcel = () => {
    const content = reportHtml(company?.name || "Company", fromDate, toDate, visibleRows, totalAmount);
    const blob = new Blob(["\ufeff", content], { type: "application/vnd.ms-excel;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `Purchase-Register-${(company?.name || "Company").replace(/[^\w-]+/g, "_")}.xls`;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 60_000);
  };

  const printReport = () => {
    const printWindow = window.open("", "_blank");
    if (!printWindow) {
      setError("Allow pop-ups to print or save this report as PDF.");
      return;
    }
    printWindow.document.write(`${reportHtml(company?.name || "Company", fromDate, toDate, visibleRows, totalAmount)}<script>window.onload=()=>window.print()<\/script>`);
    printWindow.document.close();
  };

  const openDetails = async row => {
    const requestId = ++detailRequest.current;
    setSelectedRow(row);
    setDetails(null);
    setDetailError("");
    if (!row.tallyGuid || !company?.name) {
      setDetailError("Voucher detail is unavailable because this row has no Tally GUID or company name.");
      return;
    }
    setDetailLoading(true);
    try {
      const data = await getVoucherDetail(row.tallyGuid, apiDate(fromDate), apiDate(toDate), company.name);
      if (requestId === detailRequest.current) setDetails(data.voucher || data);
    } catch (requestError) {
      if (requestId === detailRequest.current) setDetailError(requestError.message || "Unable to load voucher details from Tally.");
    } finally {
      if (requestId === detailRequest.current) setDetailLoading(false);
    }
  };

  const closeDetails = () => {
    detailRequest.current += 1;
    setSelectedRow(null);
    setDetails(null);
    setDetailError("");
    setDetailLoading(false);
  };

  return (
    <main className="purchase-register">
      <header className="purchase-register__header">
        <div>
          <span className="purchase-register__eyebrow">Reports</span>
          <h1>Purchase Register</h1>
          <p>{company?.name || "Select a company"} · {readableDate(apiDate(fromDate))} to {readableDate(apiDate(toDate))}</p>
        </div>
      </header>

      <section className="purchase-register__filters" aria-label="Purchase Register filters">
        <label>Company
          <select value={companyId} onChange={changeCompany}>
            {companies.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </label>
        <label>From Date<input type="date" value={fromDate} onChange={event => changeDateRange(event.target.value, toDate)} /></label>
        <label>To Date<input type="date" value={toDate} onChange={event => changeDateRange(fromDate, event.target.value)} /></label>
        <label className="purchase-register__search">Search
          <input type="search" value={search} onChange={event => setSearch(event.target.value)} placeholder="Party name or voucher no." />
        </label>
      </section>

      {error && <div className="purchase-register__error" role="alert">{error}</div>}

      <section className="purchase-register__table-area" aria-label="Purchase vouchers">
        <div className="purchase-register__scroll">
          <table className="purchase-register__table">
            <thead><tr><th>S.No</th><th>Date</th><th>Particulars</th><th>Voucher Type</th><th>Voucher No.</th><th className="is-amount">Amount</th></tr></thead>
            <tbody>
              {loading ? <tr><td colSpan="6" className="purchase-register__empty">Loading Purchase vouchers…</td></tr>
                : visibleRows.length ? visibleRows.map((row, index) => (
                  <tr key={row.id} title="Double-click to view voucher details" onDoubleClick={() => openDetails(row)} onKeyDown={event => { if (event.key === "Enter") openDetails(row); }} tabIndex={0}>
                    <td>{index + 1}</td><td>{readableDate(row.date)}</td><td>{row.partyName || "—"}</td><td>{row.voucherType || "Purchase"}</td><td>{row.voucherNumber || "—"}</td><td className="is-amount">{money(row.amount)}</td>
                  </tr>
                )) : <tr><td colSpan="6" className="purchase-register__empty">{companyId && !error ? "No Purchase vouchers match the selected filters." : "Select a company and date range to view Purchase vouchers."}</td></tr>}
            </tbody>
          </table>
        </div>
        <footer className="purchase-register__footer">
          <span>{visibleRows.length.toLocaleString("en-IN")} voucher{visibleRows.length === 1 ? "" : "s"}</span>
          <strong>Total Purchase Amount <span>{money(totalAmount)}</span></strong>
        </footer>
      </section>

      {selectedRow && <div className="purchase-register__modal-shade" role="presentation" onMouseDown={event => { if (event.target === event.currentTarget) closeDetails(); }}>
        <section className="purchase-register__modal" role="dialog" aria-modal="true" aria-labelledby="purchase-voucher-title">
          <header><div><h2 id="purchase-voucher-title">Voucher Details</h2><p>{selectedRow.voucherType} · {selectedRow.voucherNumber}</p></div><button type="button" onClick={closeDetails} aria-label="Close voucher details">×</button></header>
          {detailLoading ? <p className="purchase-register__detail-state">Loading voucher details…</p>
            : detailError ? <p className="purchase-register__detail-error">{detailError}</p>
              : details && <div className="purchase-register__detail-content">
                <dl><div><dt>Date</dt><dd>{readableDate(details.date || selectedRow.date)}</dd></div><div><dt>Party</dt><dd>{details.partyName || details.partyLedgerName || selectedRow.partyName || "—"}</dd></div><div><dt>Voucher No.</dt><dd>{details.voucherNumber || selectedRow.voucherNumber || "—"}</dd></div><div><dt>Voucher Type</dt><dd>{details.voucherType || selectedRow.voucherType || "Purchase"}</dd></div><div><dt>Total</dt><dd>{money(details.totalAmount || selectedRow.amount)}</dd></div><div><dt>Reference</dt><dd>{details.reference || "—"}</dd></div></dl>
                {details.items?.length > 0 && <><h3>Items</h3><div className="purchase-register__detail-table"><table><thead><tr><th>Item</th><th>Quantity</th><th>Rate</th><th>Amount</th></tr></thead><tbody>{details.items.map((item, index) => <tr key={`${item.itemName}-${index}`}><td>{item.itemName}</td><td>{item.quantity}</td><td>{item.rate}</td><td>{item.amount}</td></tr>)}</tbody></table></div></>}
                {details.ledgerEntries?.length > 0 && <><h3>Ledger Entries</h3><div className="purchase-register__detail-table"><table><thead><tr><th>Ledger</th><th>Amount</th></tr></thead><tbody>{details.ledgerEntries.map((entry, index) => <tr key={`${entry.ledgerName}-${index}`}><td>{entry.ledgerName}</td><td>{entry.amount}</td></tr>)}</tbody></table></div></>}
                <h3>Narration</h3><p>{details.narration || selectedRow.narration || "—"}</p>
              </div>}
        </section>
      </div>}
    </main>
  );
}
