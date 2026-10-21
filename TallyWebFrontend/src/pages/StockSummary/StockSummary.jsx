import { useEffect, useMemo, useRef, useState } from "react";
import { getDbCompanies } from "../../services/companyService";
import { getStockSummary } from "../../services/stockSummaryService";
import {
  displayDate,
  flattenRows,
  flattenItemRows,
  pageRows,
  rowCells,
  spreadsheetXml,
  printHtml,
} from "../Reports/StockSummary/stockSummaryReport";
import "./StockSummary.css";

const ChevronDown = ({ size = 16 }) => (
  <svg
    width={size}
    height={size}
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth="2"
    aria-hidden="true"
  >
    <path d="m6 9 6 6 6-6" />
  </svg>
);
const ChevronRight = ({ size = 16 }) => (
  <svg
    width={size}
    height={size}
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth="2"
    aria-hidden="true"
  >
    <path d="m9 18 6-6-6-6" />
  </svg>
);
const ChevronLeft = ({ size = 16 }) => (
  <svg
    width={size}
    height={size}
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth="2"
    aria-hidden="true"
  >
    <path d="m15 18-6-6 6-6" />
  </svg>
);

function toInputDate(value) {
  if (!value) return "";
  const text = String(value).trim();
  if (/^\d{8}$/.test(text))
    return `${text.slice(0, 4)}-${text.slice(4, 6)}-${text.slice(6, 8)}`;
  const iso = text.match(/^(\d{4}-\d{2}-\d{2})/);
  if (iso) return iso[1];
  const local = text.match(/^(\d{1,2})[/-](\d{1,2})[/-](\d{4})$/);
  return local
    ? `${local[3]}-${local[2].padStart(2, "0")}-${local[1].padStart(2, "0")}`
    : "";
}

export default function StockSummary() {
  const [companies, setCompanies] = useState([]);
  const [companyId, setCompanyId] = useState("");
  const [companySearch, setCompanySearch] = useState("");
  const [companyMenu, setCompanyMenu] = useState(false);
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [report, setReport] = useState(null);
  const [loadingCompanies, setLoadingCompanies] = useState(true);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [companyError, setCompanyError] = useState("");
  const [collapsed, setCollapsed] = useState(new Set());
  const [itemWiseView, setItemWiseView] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(50);
  const request = useRef(null);
  const generation = useRef(0);
  const tableWrapper = useRef(null);

  useEffect(() => {
    let active = true;
    getDbCompanies()
      .then((data) => {
        if (active)
          setCompanies(
            data.companies || data.data || (Array.isArray(data) ? data : []),
          );
      })
      .catch((err) => {
        if (active) setCompanyError(err.message || "Unable to load companies.");
      })
      .finally(() => {
        if (active) setLoadingCompanies(false);
      });
    return () => {
      active = false;
      generation.current += 1;
      request.current?.abort();
    };
  }, []);

  const invalidateReport = () => {
    generation.current += 1;
    request.current?.abort();
    setReport(null);
    setLoading(false);
    setError("");
    setPage(1);
    setCollapsed(new Set());
  };

  const selectCompany = (company) => {
    invalidateReport();
    setCompanyId(String(company.id ?? company.Id));
    setCompanySearch(company.name ?? company.Name ?? "");
    setFromDate("");
    setToDate("");
    setCompanyMenu(false);
  };

  const reset = () => {
    invalidateReport();
    setCompanyId("");
    setCompanySearch("");
    setCompanyMenu(false);
    setFromDate("");
    setToDate("");
  };

  const canView = Boolean(
    companyId && (!fromDate || !toDate || fromDate <= toDate),
  );
  const runReport = async (
    selectedCompanyId,
    selectedFromDate,
    selectedToDate,
  ) => {
    if (
      !selectedCompanyId ||
      (selectedFromDate && selectedToDate && selectedFromDate > selectedToDate)
    ) {
      setError(
        selectedFromDate && selectedToDate && selectedFromDate > selectedToDate
          ? "From Date cannot be after To Date."
          : "Select a company and enter a valid date range.",
      );
      return;
    }
    request.current?.abort();
    const controller = new AbortController();
    request.current = controller;
    const version = ++generation.current;
    setLoading(true);
    setReport(null);
    setError("");
    try {
      const data = await getStockSummary(
        {
          companyId: selectedCompanyId,
          fromDate: selectedFromDate || null,
          toDate: selectedToDate || null,
        },
        controller.signal,
      );
      if (version !== generation.current) return;
      if (String(data.companyId) !== selectedCompanyId)
        throw new Error("The report company does not match your selection.");
      setReport(data);
      setCollapsed(new Set());
      setPage(1);
    } catch (err) {
      if (version === generation.current && err.name !== "AbortError")
        setError(err.message || "Unable to load Stock Summary.");
    } finally {
      if (version === generation.current) setLoading(false);
    }
  };

  useEffect(() => {
    if (companyId) void runReport(companyId, "", "");
  }, [companyId]);

  const loadReport = async (event) => {
    event.preventDefault();
    setCompanyMenu(false);
    if (!canView) {
      setError(
        fromDate && toDate && fromDate > toDate
          ? "From Date cannot be after To Date."
          : "Select a company and enter a valid date range.",
      );
      return;
    }
    await runReport(companyId, fromDate, toDate);
  };

  const rows = useMemo(
    () =>
      itemWiseView
        ? flattenItemRows(report?.groups)
        : flattenRows(report?.groups, collapsed),
    [report, collapsed, itemWiseView],
  );
  const pages = Math.max(1, Math.ceil(rows.length / pageSize));
  const currentPage = Math.min(page, pages);
  const visibleRows = pageRows(rows, currentPage, pageSize);
  const filteredCompanies = companies.filter((company) =>
    (company.name ?? company.Name ?? "")
      .toLocaleLowerCase()
      .includes(companySearch.toLocaleLowerCase()),
  );
  const navigatePage = (value) => {
    setPage(value);
    tableWrapper.current?.scrollTo({ top: 0 });
  };
  const toggleGroup = (name) => {
    setCollapsed((previous) => {
      const next = new Set(previous);
      next.has(name) ? next.delete(name) : next.add(name);
      return next;
    });
    setPage(1);
  };
  const exportExcel = () => {
    const blob = new Blob(
      [spreadsheetXml(report, { itemWise: itemWiseView })],
      {
        type: "application/vnd.ms-excel;charset=utf-8",
      },
    );
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `Stock-Summary-${report.fromDate}-${report.toDate}.xml`;
    document.body.append(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  };
  const printReport = () => {
    const popup = window.open("", "_blank", "width=1200,height=800");
    if (!popup) {
      setError("Allow pop-ups to print or save the full report as PDF.");
      return;
    }
    popup.opener = null;
    popup.document.write(printHtml(report, { itemWise: itemWiseView }));
    popup.document.close();
    popup.focus();
    popup.setTimeout(() => popup.print(), 250);
  };
  const numericCells = (data) =>
    rowCells(data).map((value, index) => (
      <td key={index} className={index % 3 === 0 ? "ss-column-start" : ""}>
        {value}
      </td>
    ));

  return (
    <main className="stock-summary-page">
      <header className="stock-summary-header">
        <div>
          <span className="ss-eyebrow">Reports</span>
          <h1>Stock Summary</h1>
          <p>Stock movements and closing valuation</p>
        </div>
        <div className="ss-export-actions" aria-label="Export full report">
          <button
            type="button"
            disabled={!report || loading}
            onClick={exportExcel}
          >
            Excel
          </button>
          <button
            type="button"
            disabled={!report || loading}
            onClick={printReport}
          >
            PDF
          </button>
          <button
            type="button"
            disabled={!report || loading}
            onClick={printReport}
          >
            Print
          </button>
        </div>
      </header>

      <form className="stock-summary-filter" onSubmit={loadReport}>
        <div
          className="ss-field ss-company-field"
          onBlur={(event) => {
            if (!event.currentTarget.contains(event.relatedTarget))
              setCompanyMenu(false);
          }}
        >
          <label htmlFor="ss-company">Company Name</label>
          <div className="ss-company-input">
            <input
              id="ss-company"
              role="combobox"
              autoComplete="off"
              aria-autocomplete="list"
              aria-expanded={companyMenu}
              aria-controls="ss-company-options"
              value={companySearch}
              placeholder={
                loadingCompanies
                  ? "Loading companies…"
                  : "Search and select company"
              }
              disabled={loadingCompanies || !!companyError}
              onFocus={() => setCompanyMenu(true)}
              onChange={(event) => {
                invalidateReport();
                setCompanyId("");
                setCompanySearch(event.target.value);
                setCompanyMenu(true);
              }}
              onKeyDown={(event) => {
                if (event.key === "Escape") setCompanyMenu(false);
                if (event.key === "Enter" && companyMenu) {
                  event.preventDefault();
                  if (filteredCompanies.length === 1)
                    selectCompany(filteredCompanies[0]);
                }
              }}
            />
            <button
              type="button"
              aria-label="Show companies"
              onClick={() => setCompanyMenu((value) => !value)}
            >
              <ChevronDown size={16} />
            </button>
          </div>
          {companyMenu && !loadingCompanies && (
            <div
              id="ss-company-options"
              role="listbox"
              className="ss-company-options"
            >
              {(companyId ? companies : filteredCompanies).map((item) => (
                <button
                  type="button"
                  role="option"
                  key={item.id ?? item.Id}
                  aria-selected={String(item.id ?? item.Id) === companyId}
                  onClick={() => selectCompany(item)}
                >
                  {item.name ?? item.Name}
                </button>
              ))}
              {!filteredCompanies.length && !companyId && (
                <span>No matching companies</span>
              )}
            </div>
          )}
        </div>
        <div className="ss-field">
          <label htmlFor="ss-from">From Date</label>
          <input
            id="ss-from"
            type="date"
            value={fromDate}
            onChange={(event) => {
              invalidateReport();
              setFromDate(event.target.value);
            }}
          />
        </div>
        <div className="ss-field">
          <label htmlFor="ss-to">To Date</label>
          <input
            id="ss-to"
            type="date"
            value={toDate}
            min={fromDate || undefined}
            onChange={(event) => {
              invalidateReport();
              setToDate(event.target.value);
            }}
          />
        </div>
        <button
          className="ss-view-button"
          type="submit"
          disabled={!canView || loading}
        >
          {loading ? "Loading…" : "View Report"}
        </button>
        <button className="ss-reset" type="button" onClick={reset}>
          Reset
        </button>
      </form>
      {(companyError || error) && (
        <div role="alert" className="ss-error">
          {companyError || error}
        </div>
      )}

      <section
        className="stock-summary-card"
        aria-label="Stock Summary report"
        aria-busy={loading}
      >
        <div className="ss-card-header">
          <div>
            <h2>{report?.companyName || "Stock Summary"}</h2>
            <span>
              {report
                ? `${displayDate(report.fromDate)} to ${displayDate(report.toDate)}`
                : "Select a company and period to view the report"}
            </span>
          </div>
          <div className="ss-report-controls">
            <label className="ss-item-view-toggle">
              <input
                type="checkbox"
                checked={itemWiseView}
                onChange={(event) => {
                  setItemWiseView(event.target.checked);
                  setPage(1);
                  tableWrapper.current?.scrollTo({ top: 0 });
                }}
              />
              Stock Item-wise View
            </label>
            {report && !itemWiseView && (
              <div className="ss-group-actions">
                <button
                  type="button"
                  onClick={() => {
                    setCollapsed(new Set());
                    setPage(1);
                  }}
                >
                  Expand all
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setCollapsed(
                      new Set(
                        flattenRows(report.groups)
                          .filter((row) => row.kind === "group")
                          .map((row) => row.data.groupName),
                      ),
                    );
                    setPage(1);
                  }}
                >
                  Collapse all
                </button>
              </div>
            )}
          </div>
        </div>
        <div
          ref={tableWrapper}
          className="stock-summary-table-wrapper"
          tabIndex={0}
          aria-label="Scrollable stock summary"
        >
          <table className="stock-summary-table">
            <colgroup>
              <col className="ss-particulars" />
              {Array.from({ length: 9 }, (_, index) => (
                <col key={index} />
              ))}
            </colgroup>
            <thead>
              <tr>
                <th rowSpan={2} scope="col">
                  Particulars
                </th>
                <th colSpan={3} scope="colgroup">
                  Inwards
                </th>
                <th colSpan={3} scope="colgroup">
                  Outwards
                </th>
                <th colSpan={3} scope="colgroup">
                  Closing Balance
                </th>
              </tr>
              <tr>
                {[
                  "Quantity",
                  "Rate",
                  "Value",
                  "Quantity",
                  "Rate",
                  "Value",
                  "Quantity",
                  "Rate",
                  "Value",
                ].map((label, index) => (
                  <th
                    key={`${label}-${index}`}
                    className={index % 3 === 0 ? "ss-column-start" : ""}
                  >
                    {label}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {loading || !report || !rows.length ? (
                <tr>
                  <td
                    colSpan={10}
                    className="stock-summary-empty"
                    role="status"
                  >
                    {loading
                      ? "Loading Stock Summary from TallyPrime…"
                      : !report
                        ? "Select a company and optionally enter a date range, then click View Report."
                        : "No stock data for the selected period."}
                  </td>
                </tr>
              ) : (
                visibleRows.map((row) => (
                  <tr
                    key={row.key}
                    className={
                      row.kind === "group"
                        ? "stock-summary-group-row"
                        : "stock-summary-item-row"
                    }
                  >
                    <td style={{ paddingLeft: 10 + row.depth * 16 }}>
                      {row.kind === "group" ? (
                        <button
                          className="ss-group-toggle"
                          type="button"
                          aria-expanded={!collapsed.has(row.data.groupName)}
                          onClick={() => toggleGroup(row.data.groupName)}
                        >
                          {collapsed.has(row.data.groupName) ? (
                            <ChevronRight size={14} />
                          ) : (
                            <ChevronDown size={14} />
                          )}
                          {row.data.groupName}
                          {row.continued ? " (continued)" : ""}
                        </button>
                      ) : (
                        row.data.stockItemName
                      )}
                    </td>
                    {numericCells(row.data)}
                  </tr>
                ))
              )}
            </tbody>
            {report && (
              <tfoot>
                <tr>
                  <th scope="row">Grand Total</th>
                  {numericCells(report.grandTotal)}
                </tr>
              </tfoot>
            )}
          </table>
        </div>
        <footer className="ss-pagination">
          <span aria-live="polite">
            {report
              ? `${report.totalItems} stock items · ${rows.length ? `${(currentPage - 1) * pageSize + 1}–${Math.min(currentPage * pageSize, rows.length)} of ${rows.length} visible rows` : "0 rows"}`
              : "0 stock items"}
          </span>
          <div>
            <label htmlFor="ss-page-size">Rows</label>
            <select
              id="ss-page-size"
              value={pageSize}
              onChange={(event) => {
                setPageSize(Number(event.target.value));
                setPage(1);
              }}
            >
              {[25, 50, 100].map((size) => (
                <option key={size}>{size}</option>
              ))}
            </select>
            <button
              type="button"
              aria-label="Previous page"
              disabled={currentPage === 1 || !report}
              onClick={() => navigatePage(currentPage - 1)}
            >
              <ChevronLeft size={16} />
            </button>
            <span>
              {currentPage} / {pages}
            </span>
            <button
              type="button"
              aria-label="Next page"
              disabled={currentPage === pages || !report}
              onClick={() => navigatePage(currentPage + 1)}
            >
              <ChevronRight size={16} />
            </button>
          </div>
        </footer>
      </section>
    </main>
  );
}
