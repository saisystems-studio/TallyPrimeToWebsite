import { useEffect, useRef, useState } from "react";
import { getDbCompanies, getDbLedgers } from "../../../services/companyService";
import { getLedgerReport } from "../../../services/ledgerReportService";
import { formatTallySignedBalance } from "./ledgerReportBalance";
import "./LedgerReport.css";

const displayDate = (value) => {
  const text = String(value || "");
  const match = text.match(/^(\d{4})(\d{2})(\d{2})$/);
  return match ? `${match[3]}/${match[2]}/${match[1]}` : text || "—";
};
const amount = (value) =>
  Math.abs(Number(value || 0)).toLocaleString("en-IN", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });
const drCr = formatTallySignedBalance;

function SearchSelect({
  label,
  value,
  options,
  placeholder,
  disabled,
  onSelect,
}) {
  const [search, setSearch] = useState("");
  const [open, setOpen] = useState(false);
  const query = value?.name || search;
  const matches = options
    .filter((x) => x.name.toLowerCase().includes(query.trim().toLowerCase()))
    .slice(0, 100);
  return (
    <label className="ledger-filter-field">
      <span>{label}</span>
      <div className="ledger-search-wrap">
        <input
          value={query}
          placeholder={placeholder}
          disabled={disabled}
          autoComplete="off"
          onFocus={() => setOpen(true)}
          onChange={(e) => {
            setSearch(e.target.value);
            setOpen(true);
            if (value) onSelect(null);
          }}
          onBlur={() => setTimeout(() => setOpen(false), 120)}
        />
        {open && !disabled && matches.length > 0 && (
          <div className="ledger-search-options">
            {matches.map((x) => (
              <button
                key={x.id}
                type="button"
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => {
                  onSelect(x);
                  setSearch("");
                  setOpen(false);
                }}
              >
                {x.name}
              </button>
            ))}
          </div>
        )}
      </div>
    </label>
  );
}

export default function LedgerReport() {
  const [companies, setCompanies] = useState([]);
  const [ledgers, setLedgers] = useState([]);
  const [company, setCompany] = useState(null);
  const [ledger, setLedger] = useState(null);
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [report, setReport] = useState(null);
  const [loading, setLoading] = useState(false);
  const [loadingOptions, setLoadingOptions] = useState(true);
  const [error, setError] = useState("");
  const [page, setPage] = useState(1);
  const requestId = useRef(0);
  const pageSize = 50;

  useEffect(() => {
    let current = true;
    getDbCompanies()
      .then((data) => {
        if (current)
          setCompanies(
            (data.companies || [])
              .map((x) => ({
                ...x,
                id: x.id ?? x.Id,
                name: x.name ?? x.Name ?? "",
              }))
              .filter((x) => x.name),
          );
      })
      .catch((e) => {
        if (current) setError(e.message || "Unable to load companies.");
      })
      .finally(() => {
        if (current) setLoadingOptions(false);
      });
    return () => {
      current = false;
    };
  }, []);

  const fetchReport = async (
    targetCompany = company,
    targetLedger = ledger,
    start = fromDate,
    end = toDate,
  ) => {
    if (!targetCompany || !targetLedger) {
      setError("Select a company and ledger to view the report.");
      return;
    }
    if (start && end && start > end) {
      setError("From Date cannot be later than To Date.");
      return;
    }
    const id = ++requestId.current;
    setLoading(true);
    setError("");
    setReport(null);
    setPage(1);
    try {
      const formatApiDate = (value) => (value ? value.replaceAll("-", "") : "");

      const data = await getLedgerReport({
        companyId: targetCompany.id,
        ledgerId: targetLedger.id,
        fromDate: formatApiDate(start),
        toDate: formatApiDate(end),
      });
      if (id === requestId.current)
        setReport({
          ...data,
          companyId: targetCompany.id,
          companyName: targetCompany.name,
        });
    } catch (e) {
      if (id === requestId.current)
        setError(e.message || "Unable to load Ledger Report.");
    } finally {
      if (id === requestId.current) setLoading(false);
    }
  };

  const selectCompany = async (next) => {
    const id = ++requestId.current;
    setCompany(next);
    setLedger(null);
    setLedgers([]);
    setReport(null);
    setError("");
    setLoading(false);
    if (!next) return;
    setLoadingOptions(true);
    try {
      const data = await getDbLedgers(next.id);
      if (id === requestId.current) {
        if (Number(data.companyId) !== Number(next.id))
          throw new Error(
            "The ledger list belongs to a different company. Select the company again.",
          );
        setLedgers(
          (data.ledgers || [])
            .map((x) => ({
              ...x,
              id: x.id ?? x.Id,
              companyId: x.companyId ?? x.CompanyId,
              name: x.name ?? x.Name ?? "",
            }))
            .filter(
              (x) =>
                x.name &&
                (!x.companyId || Number(x.companyId) === Number(next.id)),
            ),
        );
      }
    } catch (e) {
      if (id === requestId.current)
        setError(e.message || "Unable to load ledgers for this company.");
    } finally {
      if (id === requestId.current) setLoadingOptions(false);
    }
  };
  const selectLedger = (next) => {
    ++requestId.current;
    setLedger(next);
    setReport(null);
    setError("");
    if (next && company) {
      if (next.companyId && Number(next.companyId) !== Number(company.id)) {
        setLedger(null);
        setError(
          "The selected ledger does not belong to the selected company.",
        );
        return;
      }
      void fetchReport(company, next);
    }
  };
  const reset = () => {
    ++requestId.current;
    setCompany(null);
    setLedger(null);
    setLedgers([]);
    setFromDate("");
    setToDate("");
    setReport(null);
    setError("");
    setLoading(false);
    setPage(1);
  };
  const rows = report?.transactions || [];
  const pageCount = Math.max(1, Math.ceil(rows.length / pageSize));
  const pageRows = rows.slice((page - 1) * pageSize, page * pageSize);
  const exportExcel = () => {
    if (!report) return;
    const escape = (cell) =>
      String(cell ?? "").replace(
        /[&<>"']/g,
        (char) =>
          ({
            "&": "&amp;",
            "<": "&lt;",
            ">": "&gt;",
            '"': "&quot;",
            "'": "&#39;",
          })[char],
      );
    const data = [
      [
        "S.No",
        "Date",
        "Particulars",
        "Voucher Type",
        "Voucher No",
        "Debit",
        "Credit",
      ],
      ...rows.map((x, i) => [
        i + 1,
        displayDate(x.date),
        x.particulars,
        x.voucherType,
        x.voucherNumber,
        x.debit,
        x.credit,
      ]),
    ];
    const table = data
      .map(
        (line, index) =>
          `<tr>${line.map((cell) => (index ? `<td>${escape(cell)}</td>` : `<th>${escape(cell)}</th>`)).join("")}</tr>`,
      )
      .join("");
    const html = `<html><meta charset="utf-8"><table><caption>${escape(report.companyName)} · ${escape(report.ledgerName)} · ${escape(displayDate(report.fromDate))} to ${escape(displayDate(report.toDate))}</caption>${table}<tr><td colspan="7">${escape(report.historyNote)}</td></tr><tr><th colspan="5">Opening Balance</th><td colspan="2">${escape(drCr(report.openingBalance))}</td></tr><tr><th colspan="5">Total Debit</th><td colspan="2">${escape(amount(report.totalDebit))}</td></tr><tr><th colspan="5">Total Credit</th><td colspan="2">${escape(amount(report.totalCredit))}</td></tr><tr><th colspan="5">Closing Balance</th><td colspan="2">${escape(drCr(report.closingBalance))}</td></tr></table></html>`;
    const url = URL.createObjectURL(
      new Blob(["\uFEFF", html], {
        type: "application/vnd.ms-excel;charset=utf-8",
      }),
    );
    const a = document.createElement("a");
    a.href = url;
    a.download = `Ledger-${report.ledgerName.replace(/[^\w-]+/g, "_")}.xls`;
    a.click();
    URL.revokeObjectURL(url);
  };
  const printReport = () => {
    if (!report) return;
    const popup = window.open("", "_blank", "width=1000,height=760");
    if (!popup) {
      window.print();
      return;
    }
    const escape = (cell) =>
      String(cell ?? "").replace(
        /[&<>"']/g,
        (char) =>
          ({
            "&": "&amp;",
            "<": "&lt;",
            ">": "&gt;",
            '"': "&quot;",
            "'": "&#39;",
          })[char],
      );
    const body = rows
      .map(
        (x, i) =>
          `<tr><td>${i + 1}</td><td>${escape(displayDate(x.date))}</td><td>${escape(x.particulars)}</td><td>${escape(x.voucherType)}</td><td>${escape(x.voucherNumber)}</td><td>${escape(amount(x.debit))}</td><td>${escape(amount(x.credit))}</td></tr>`,
      )
      .join("");
    popup.opener = null;
    popup.document.write(
      `<html><head><title>Ledger Report</title><style>body{font:12px Arial;padding:24px}h1{font-size:19px}table{border-collapse:collapse;width:100%}th,td{border:1px solid #bbb;padding:6px;text-align:left}th{background:#eee}.num{text-align:right}</style></head><body><h1>${escape(report.companyName)} · ${escape(report.ledgerName)}</h1><p>${escape(displayDate(report.fromDate))} – ${escape(displayDate(report.toDate))}</p><p>${escape(report.historyNote)}</p><table><thead><tr><th>S.No</th><th>Date</th><th>Particulars</th><th>Voucher Type</th><th>Voucher No</th><th>Debit</th><th>Credit</th></tr></thead><tbody>${body}</tbody><tfoot><tr><th colspan="5">Opening Balance</th><td colspan="2">${escape(drCr(report.openingBalance))}</td></tr><tr><th colspan="5">Total Debit</th><td colspan="2">${escape(amount(report.totalDebit))}</td></tr><tr><th colspan="5">Total Credit</th><td colspan="2">${escape(amount(report.totalCredit))}</td></tr><tr><th colspan="5">Closing Balance</th><td colspan="2">${escape(drCr(report.closingBalance))}</td></tr></tfoot></table><script>window.onload=()=>window.print()</script></body></html>`,
    );
    popup.document.close();
  };

  return (
    <main className="ledger-report-page">
      <header className="ledger-report-title">
        <div>
          <h1>Ledger Report</h1>
          <p>Company-wise ledger statement · Date range optional</p>
        </div>
      </header>
      <section className="ledger-report-filters">
        <SearchSelect
          label="Company Name"
          value={company}
          options={companies}
          placeholder={loadingOptions ? "Loading companies…" : "Search company"}
          onSelect={selectCompany}
        />
        <SearchSelect
          label="Ledger Name"
          value={ledger}
          options={ledgers}
          disabled={!company || loadingOptions}
          placeholder={!company ? "Choose company first" : "Search ledger"}
          onSelect={selectLedger}
        />
        <label className="ledger-filter-field">
          <span>From Date</span>
          <input
            type="date"
            value={fromDate}
            onChange={(e) => setFromDate(e.target.value)}
          />
        </label>
        <label className="ledger-filter-field">
          <span>To Date</span>
          <input
            type="date"
            min={fromDate || undefined}
            value={toDate}
            onChange={(e) => setToDate(e.target.value)}
          />
        </label>
        <div className="ledger-filter-buttons">
          <button
            className="ledger-view-button"
            type="button"
            disabled={loading || !company || !ledger}
            onClick={() => fetchReport()}
          >
            {loading ? "Loading…" : "View Report"}
          </button>
          <button type="button" onClick={reset}>
            Reset
          </button>
        </div>
      </section>
      {error && (
        <div className="ledger-message ledger-error" role="alert">
          {error}
        </div>
      )}
      {report && (
        <>
          <div className="ledger-report-meta">
            <strong>{report.companyName}</strong>
            <span>{report.ledgerName}</span>
            <span>
              {displayDate(report.fromDate)} – {displayDate(report.toDate)}
            </span>
          </div>
          <div className="ledger-message ledger-warning">
            {report.historyNote ||
              "Synchronized history completeness against Tally is unverified."}
          </div>
        </>
      )}
      <section className="ledger-report-grid">
        <div className="ledger-grid-scroll">
          <table>
            <thead>
              <tr>
                <th>S.No</th>
                <th>Date</th>
                <th>Particulars</th>
                <th>Voucher Type</th>
                <th>Voucher No</th>
                <th className="ledger-numeric">Debit</th>
                <th className="ledger-numeric">Credit</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="7" className="ledger-empty">
                    Loading ledger transactions…
                  </td>
                </tr>
              ) : error ? (
                <tr>
                  <td colSpan="7" className="ledger-empty">
                    The report could not be loaded. Review the error above.
                  </td>
                </tr>
              ) : !report ? (
                <tr>
                  <td colSpan="7" className="ledger-empty">
                    Select a company and ledger. Dates are optional.
                  </td>
                </tr>
              ) : rows.length === 0 ? (
                <tr>
                  <td colSpan="7" className="ledger-empty">
                    No synchronized transactions are available for the selected
                    ledger and period.
                  </td>
                </tr>
              ) : (
                pageRows.map((x, i) => (
                  <tr
                    key={`${x.date}-${x.voucherNumber}-${(page - 1) * pageSize + i}`}
                  >
                    <td>{(page - 1) * pageSize + i + 1}</td>
                    <td>{displayDate(x.date)}</td>
                    <td>{x.particulars || "—"}</td>
                    <td>{x.voucherType || "—"}</td>
                    <td>{x.voucherNumber || "—"}</td>
                    <td className="ledger-numeric">
                      {Number(x.debit) ? amount(x.debit) : "—"}
                    </td>
                    <td className="ledger-numeric">
                      {Number(x.credit) ? amount(x.credit) : "—"}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
            {report && (
              <tfoot>
                <tr>
                  <th colSpan="5">Opening Balance</th>
                  <td colSpan="2" className="ledger-numeric">
                    {drCr(report.openingBalance)}
                  </td>
                </tr>
                <tr>
                  <th colSpan="5">Total Debit</th>
                  <td colSpan="2" className="ledger-numeric">
                    {amount(report.totalDebit)}
                  </td>
                </tr>
                <tr>
                  <th colSpan="5">Total Credit</th>
                  <td colSpan="2" className="ledger-numeric">
                    {amount(report.totalCredit)}
                  </td>
                </tr>
                <tr>
                  <th colSpan="5">Closing Balance</th>
                  <td colSpan="2" className="ledger-numeric">
                    {drCr(report.closingBalance)}
                  </td>
                </tr>
              </tfoot>
            )}
          </table>
        </div>
        <footer className="ledger-grid-footer">
          <span>
            {rows.length
              ? `${(page - 1) * pageSize + 1}–${Math.min(page * pageSize, rows.length)} of ${rows.length} records`
              : `${rows.length} records`}
          </span>
          <div>
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => setPage((x) => x - 1)}
            >
              Previous
            </button>
            <span>
              Page {page} of {pageCount}
            </span>
            <button
              type="button"
              disabled={page >= pageCount}
              onClick={() => setPage((x) => x + 1)}
            >
              Next
            </button>
          </div>
        </footer>
      </section>
    </main>
  );
}
