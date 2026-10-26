import { Fragment, useEffect, useMemo, useRef, useState } from "react";
import { getDbCompanies } from "../../../services/companyService";
import {
  getSalesRegister,
  getStoredSalesVoucher,
  getStoredSalesPeriods,
} from "../../../services/salesRegisterService";
import {
  salesRegisterTotals,
  salesMovementAmount,
  parseSalesDate,
  salesRegisterMonths,
  salesClosingBalance,
} from "./salesRegisterModel";
import {
  storedSalesDetail,
  storedFinancialYears,
  storedVoucherHtml,
  storedPostingAmount,
} from "./salesRegisterStored";
import "./SalesRegister.css";

const PERIODS = [
  "Financial Year",
  "This Month",
  "Last Month",
  "This Week",
  "Last Week",
  "This Year",
  "Last Year",
  "Custom Date Range",
  "All Available Data",
];
const pad = (n) => String(n).padStart(2, "0");
const iso = (d) =>
  `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const tallyDate = (d) => d.replaceAll("-", "");
const parseDate = parseSalesDate;
const dateLabel = (value) => {
  const d = parseDate(value);
  return d ? d.toLocaleDateString("en-IN") : value || "—";
};
const money = (value) =>
  value == null
    ? "—"
    : `₹ ${Number(value).toLocaleString("en-IN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
const closingMoney = (value) => {
  const balance = salesClosingBalance(value);
  return balance
    ? [money(balance.amount), balance.side].filter(Boolean).join(" ")
    : money(null);
};
const hasAmount = (row) =>
  row.amount !== null &&
  row.amount !== undefined &&
  row.amount !== "" &&
  Number.isFinite(Number(row.amount));

function bounds(period, now = new Date(), company, customFrom, customTo) {
  let from, to;
  if (period === "This Month") {
    from = new Date(now.getFullYear(), now.getMonth(), 1);
    to = new Date(now.getFullYear(), now.getMonth() + 1, 0);
  } else if (period === "Last Month") {
    from = new Date(now.getFullYear(), now.getMonth() - 1, 1);
    to = new Date(now.getFullYear(), now.getMonth(), 0);
  } else if (period === "This Week") {
    from = new Date(now);
    from.setDate(now.getDate() - ((now.getDay() + 6) % 7));
    to = new Date(from);
    to.setDate(from.getDate() + 6);
  } else if (period === "Last Week") {
    to = new Date(now);
    to.setDate(now.getDate() - ((now.getDay() + 6) % 7) - 1);
    from = new Date(to);
    from.setDate(to.getDate() - 6);
  } else if (period === "This Year" || period === "Last Year") {
    const configuredStart = parseDate(company?.startingFrom);
    if (!configuredStart)
      return {
        from: "",
        to: "",
        error:
          "This company's financial-year start is unavailable. Choose a Custom Date Range or sync its company settings.",
      };
    const annualDate = (year) =>
      new Date(
        year,
        configuredStart.getMonth(),
        Math.min(
          configuredStart.getDate(),
          new Date(year, configuredStart.getMonth() + 1, 0).getDate(),
        ),
      );
    let startYear = now.getFullYear();
    if (now < annualDate(startYear)) startYear--;
    if (period === "Last Year") startYear--;
    from = annualDate(startYear);
    to = new Date(annualDate(startYear + 1));
    to.setDate(to.getDate() - 1);
  } else if (period === "Custom Date Range")
    return { from: customFrom || "", to: customTo || "" };
  else return { from: "", to: "" };
  return { from: iso(from), to: iso(to) };
}
const esc = (value) =>
  String(value ?? "").replace(
    /[&<>"']/g,
    (c) =>
      ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[
        c
      ],
  );

// Parent ledger row + inline voucher-level tree. Child rows keep the original View action.
function FragmentLedgerGroup({ group, serial, expanded, onToggle, onView }) {
  return (
    <Fragment>
      <tr className="sr-ledger-parent">
        <td>{serial}</td>
        <td>
          <button
            type="button"
            className="sr-ledger-toggle"
            onClick={onToggle}
            aria-expanded={expanded}
          >
            <span className="sr-ledger-chevron" aria-hidden="true">
              {expanded ? "▾" : "▸"}
            </span>
            <strong>{group.name}</strong>
          </button>
        </td>
        <td>{group.entries.length}</td>
        <td>{money(group.totals.debit)}</td>
        <td>{money(group.totals.credit)}</td>
        <td>
          <button
            type="button"
            className="sr-view"
            onClick={onToggle}
            aria-label={`${expanded ? "Hide" : "Show"} voucher details for ${group.name}`}
            title={expanded ? "Hide voucher details" : "Show voucher details"}
            aria-expanded={expanded}
          >
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12Z" />
              {expanded ? <path d="m4 4 16 16" /> : <circle cx="12" cy="12" r="2.5" />}
            </svg>
          </button>
        </td>
      </tr>
      {expanded && (
        <tr className="sr-ledger-child">
          <td colSpan={6}>
            <div className="sr-ledger-details">
              <table>
                <thead>
                  <tr>
                    <th>Date</th>
                    <th>Voucher Type</th>
                    <th>Voucher No</th>
                    <th>Debit Amount</th>
                    <th>Credit Amount</th>
                    <th>View</th>
                  </tr>
                </thead>
                <tbody>
                  {group.entries.map((v, i) => {
                    const amount = salesMovementAmount(v);
                    return (
                      <tr key={`${v.id ?? v.guid ?? "voucher"}-${i}`}>
                        <td>{dateLabel(v.date)}</td>
                        <td>{v.voucherType || "—"}</td>
                        <td>{v.voucherNumber || "—"}</td>
                        <td>
                          {amount === null
                            ? "—"
                            : amount < 0
                              ? money(Math.abs(amount))
                              : "—"}
                        </td>
                        <td>
                          {amount === null
                            ? "—"
                            : amount >= 0
                              ? money(amount)
                              : "—"}
                        </td>
                        <td>
                          <button
                            type="button"
                            className="sr-view"
                            aria-label={`View voucher ${v.voucherNumber || "details"}`}
                            title="View voucher details"
                            onClick={() => onView(v)}
                          >
                            <svg viewBox="0 0 24 24" aria-hidden="true">
                              <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12Z" />
                              <circle cx="12" cy="12" r="2.5" />
                            </svg>
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </td>
        </tr>
      )}
    </Fragment>
  );
}

export default function SalesRegister() {
  const [companies, setCompanies] = useState([]);
  const [companyId, setCompanyId] = useState("");
  const [search, setSearch] = useState("");
  const [period, setPeriod] = useState("All Available Data");
  const [financialYears, setFinancialYears] = useState([]);
  const [financialYear, setFinancialYear] = useState("");
  const [customFrom, setCustomFrom] = useState("");
  const [customTo, setCustomTo] = useState("");
  const [availableRange, setAvailableRange] = useState(null);
  const [syncNote, setSyncNote] = useState("");
  const [rows, setRows] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [month, setMonth] = useState(null);
  const [page, setPage] = useState(1);
  const [expandedLedgers, setExpandedLedgers] = useState({});
  const [selectedRow, setSelectedRow] = useState(null);
  const [voucher, setVoucher] = useState(null);
  const [detailError, setDetailError] = useState("");
  const [detailLoading, setDetailLoading] = useState(false);
  const scrollRef = useRef(null);
  const detailRequest = useRef(0);
  const company = companies.find((c) => String(c.id) === String(companyId));
  const dates = useMemo(
    () =>
      period === "Financial Year"
        ? financialYears.find((y) => y.label === financialYear) || {
            from: "",
            to: "",
            error: "No stored financial year is available for this company.",
          }
        : bounds(period, new Date(), company, customFrom, customTo),
    [period, company, customFrom, customTo, financialYears, financialYear],
  );
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const dbData = await getDbCompanies();
        if (cancelled) return;
        const list = dbData.companies || dbData || [];
        setCompanies(list);
        if (list.length) {
          setCompanyId(String(list[0].id));
          setSearch(list[0].name);
        }
      } catch (e) {
        if (!cancelled) setError(e.message || "Unable to load companies.");
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);
  useEffect(() => {
    let cancelled = false;
    const loadPeriods = async () => {
      await Promise.resolve();
      if (cancelled) return;
      setFinancialYears([]);
      setFinancialYear("");
      if (!companyId) return;
      try {
        const data = await getStoredSalesPeriods(companyId);
        if (cancelled) return;
        const years = storedFinancialYears(data);
        setFinancialYears(years);
        setFinancialYear(years[0]?.label || "");
      } catch (e) {
        // Period metadata only powers the optional stored financial-year filter.
        // Keep a failed metadata request from hiding an otherwise successful
        // Sales Register response (for example, All Available Data).
        if (!cancelled && period === "Financial Year") {
          setSyncNote(e.message || "Unable to load stored financial years.");
        }
      }
    };
    loadPeriods();
    return () => {
      cancelled = true;
    };
  }, [companyId, period]);
  useEffect(() => {
    let cancelled = false;
    detailRequest.current++;
    const load = async () => {
      await Promise.resolve();
      if (cancelled) return;
      setMonth(null);
      setPage(1);
      setRows([]);
      setAvailableRange(null);
      setExpandedLedgers({});
      setSelectedRow(null);
      setVoucher(null);
      setDetailError("");
      setLoading(false);
      setError("");
      setSyncNote("");
      if (!companyId) {
        setSyncNote("Select a company to view Sales data.");
        return;
      }
      if (dates.error) {
        setSyncNote(dates.error);
        return;
      }
      if (period === "Custom Date Range" && (!dates.from || !dates.to)) {
        setSyncNote("Choose both dates to load Sales vouchers.");
        return;
      }
      if (dates.from && dates.to && dates.from > dates.to) {
        setSyncNote("From Date must be on or before To Date.");
        return;
      }
      setLoading(true);
      try {
        const data = await getSalesRegister(
          companyId,
          dates.from ? tallyDate(dates.from) : "",
          dates.to ? tallyDate(dates.to) : "",
        );
        if (cancelled) return;
        const sourceRows = data.vouchers || [];
        setRows(sourceRows);
        const availableFrom = parseDate(data.availableFrom),
          availableTo = parseDate(data.availableTo);
        setAvailableRange(
          availableFrom && availableTo
            ? { from: iso(availableFrom), to: iso(availableTo) }
            : null,
        );
        // Keep the report focused on the data grid after a successful load.
        setSyncNote("");
      } catch (e) {
        if (!cancelled) setError(e.message || "Unable to load Sales vouchers.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    load();
    return () => {
      cancelled = true;
    };
  }, [companyId, company?.name, dates.from, dates.to, dates.error, period]);
  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = 0;
      scrollRef.current.scrollLeft = 0;
    }
  }, [companyId, dates.from, dates.to, period, month, page]);
  const isYear = period.endsWith("Year");
  const months = useMemo(() => salesRegisterMonths(rows, dates), [rows, dates]);
  const activeRows = month
    ? month.entries
    : months.flatMap((item) => item.entries);
  // Group the complete selected period, before pagination. Never sum only a page.
  const ledgerGroups = useMemo(() => {
    if (!month) return [];
    const groups = new Map();
    for (const voucherRow of activeRows) {
      const label =
        String(voucherRow.particulars || "Unknown Ledger").trim() ||
        "Unknown Ledger";
      const key = label.toLocaleLowerCase();
      if (!groups.has(key)) groups.set(key, { key, name: label, entries: [] });
      groups.get(key).entries.push(voucherRow);
    }
    return [...groups.values()]
      .map((group) => ({
        ...group,
        totals: salesRegisterTotals(group.entries),
      }))
      .sort((a, b) =>
        a.name.localeCompare(b.name, undefined, { sensitivity: "base" }),
      );
  }, [month, activeRows]);
  const pageSize = 50;
  const pageCount = Math.max(1, Math.ceil(ledgerGroups.length / pageSize));
  const visibleGroups = ledgerGroups.slice(
    (page - 1) * pageSize,
    page * pageSize,
  );
  const toggleLedger = (key) =>
    setExpandedLedgers((previous) => ({ ...previous, [key]: !previous[key] }));
  const summaryTotals = salesRegisterTotals(activeRows);
  const total = summaryTotals.closing;
  const debitTotal = (entries) => salesRegisterTotals(entries).debit;
  const creditTotal = (entries) => salesRegisterTotals(entries).credit;
  const balanceTotal = (entries) => salesRegisterTotals(entries).closing;
  const exportRows = () =>
    month
      ? activeRows.map((v) => {
          const amount = salesMovementAmount(v);
          return [
            dateLabel(v.date),
            v.particulars,
            v.voucherType,
            v.voucherNumber,
            amount === null ? "—" : amount < 0 ? money(Math.abs(amount)) : "—",
            amount === null ? "—" : amount >= 0 ? money(amount) : "—",
          ];
        })
      : months.length || isYear || period === "All Available Data"
        ? months.map((m) => [
            m.label,
            money(debitTotal(m.entries)),
            money(creditTotal(m.entries)),
            closingMoney(
              balanceTotal(
                months
                  .slice(0, months.indexOf(m) + 1)
                  .flatMap((x) => x.entries),
              ),
            ),
          ])
        : [
            [
              period,
              money(debitTotal(rows)),
              money(creditTotal(rows)),
              closingMoney(balanceTotal(rows)),
            ],
          ];
  const exportHeaders = month
    ? [
        "Date",
        "Particulars",
        "Voucher Type",
        "Voucher No",
        "Debit Amount",
        "Credit Amount",
      ]
    : ["Particulars (Month)", "Debit", "Credit", "Closing Balance"];
  const exportHtml = () =>
    `<html><meta charset="utf-8"><title>Sales Register</title><table><caption>${esc(company?.name)} · ${esc(period)}</caption>${[exportHeaders, ...exportRows()].map((row, i) => `<tr>${row.map((x) => `<${i === 0 ? "th" : "td"}>${esc(x)}</${i === 0 ? "th" : "td"}>`).join("")}</tr>`).join("")}</table><p>Total ${esc(closingMoney(total))}</p></html>`;
  const exportExcel = () => {
    const blob = new Blob(["\ufeff", exportHtml()], {
      type: "application/vnd.ms-excel;charset=utf-8",
    });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `Sales-Register-${(company?.name || "Company").replace(/[^\w-]+/g, "_")}.xls`;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 60000);
  };
  const printReport = () => {
    const w = window.open("", "_blank");
    if (!w) {
      setError("Allow pop-ups to print this report.");
      return;
    }
    w.document.write(
      `${exportHtml()}<style>body{font:12px Arial;padding:24px}table{border-collapse:collapse;width:100%}th,td{border:1px solid #bbb;padding:7px;text-align:left}th{background:#eee}</style><script>window.onload=()=>window.print()</script>`,
    );
    w.document.close();
  };
  const openVoucher = async (row) => {
    const request = ++detailRequest.current;
    const selectedCompany = companyId;
    setSelectedRow(row);
    setVoucher(null);
    setDetailLoading(true);
    setDetailError("");
    try {
      const result = await getStoredSalesVoucher(selectedCompany, row.id);
      if (request === detailRequest.current)
        setVoucher(storedSalesDetail(result, row, selectedCompany));
    } catch (e) {
      if (request === detailRequest.current)
        setDetailError(e.message || "Unable to load stored voucher details.");
    } finally {
      if (request === detailRequest.current) setDetailLoading(false);
    }
  };
  const closeVoucher = () => {
    detailRequest.current++;
    setSelectedRow(null);
    setVoucher(null);
    setDetailError("");
  };
  const printStoredVoucher = () => {
    if (!voucher) return;
    const w = window.open("", "_blank");
    if (!w) {
      setDetailError("Allow pop-ups to print this stored voucher.");
      return;
    }
    w.document.write(
      `${storedVoucherHtml(voucher, company?.name)}<style>body{font:13px Arial;padding:24px}table{border-collapse:collapse;width:100%}th,td{border:1px solid #bbb;padding:8px;text-align:left}</style><script>window.onload=()=>window.print()</script>`,
    );
    w.document.close();
  };
  const shareStoredVoucher = async () => {
    if (!voucher) return;
    const text = [
      company?.name,
      `${voucher.voucherType} ${voucher.voucherNumber} | ${dateLabel(voucher.date)} | ${voucher.partyName}`,
      ...voucher.ledgerEntries.map((p) => `${p.ledgerName} | ${p.amount}`),
    ].join("\n");
    try {
      if (navigator.share)
        await navigator.share({ title: "Stored Sales Voucher", text });
      else await navigator.clipboard.writeText(text);
    } catch (e) {
      if (e.name !== "AbortError") setDetailError(e.message);
    }
  };
  const share = async () => {
    const text = month
      ? activeRows
          .map(
            (v) =>
              `${dateLabel(v.date)} | ${v.particulars} | ${v.voucherNumber} | ${hasAmount(v) ? money(Math.abs(Number(v.amount))) : "—"}`,
          )
          .join("\n")
      : [exportHeaders, ...exportRows()]
          .map((row) => row.join(" | "))
          .join("\n");
    if (navigator.share)
      await navigator.share({ title: "Sales Register", text });
    else
      await navigator.clipboard.writeText(
        `${company?.name} · ${period}\n${text}`,
      );
  };
  return (
    <main className="sales-register">
      <header className="sr-head">
        <div>
          <span>Reports</span>
          <h1>Sales Register</h1>
          <p>
            {company?.name || "Select a company"} ·{" "}
            {period === "All Available Data"
              ? availableRange
                ? `${availableRange.from} to ${availableRange.to}`
                : "All synchronized Sales dates"
              : period === "Custom Date Range"
                ? `${dates.from || "From date"} to ${dates.to || "To date"}`
                : `${dates.from} to ${dates.to}`}
          </p>
        </div>
      </header>
      <section className="sr-filters">
        <label>
          Company
          <div className="sr-company-control">
            <input
              list="sr-companies"
              value={search}
              onChange={(e) => {
                const value = e.target.value;
                setSearch(value);
                const c = companies.find(
                  (x) => x.name.toLowerCase() === value.toLowerCase(),
                );
                setCompanyId(c ? String(c.id) : "");
                setMonth(null);
                setPage(1);
              }}
              placeholder="Search company"
            />
          </div>
          <datalist id="sr-companies">
            {companies.map((c) => (
              <option key={c.id} value={c.name} />
            ))}
          </datalist>
        </label>
        <label>
          Period
          <select
            value={period}
            onChange={(e) => {
              setPeriod(e.target.value);
              setMonth(null);
              setPage(1);
            }}
          >
            {PERIODS.map((p) => (
              <option key={p}>{p}</option>
            ))}
          </select>
        </label>
        {period === "Financial Year" && (
          <label>
            Stored Financial Year
            <select
              value={financialYear}
              onChange={(e) => setFinancialYear(e.target.value)}
            >
              {financialYears.map((y) => (
                <option key={y.label}>{y.label}</option>
              ))}
            </select>
          </label>
        )}
        {period === "Custom Date Range" && (
          <>
            <label>
              From Date
              <input
                type="date"
                value={customFrom}
                onChange={(e) => setCustomFrom(e.target.value)}
              />
            </label>
            <label>
              To Date
              <input
                type="date"
                value={customTo}
                onChange={(e) => setCustomTo(e.target.value)}
              />
            </label>
          </>
        )}
        <div className="sr-filter-actions">
          <button
            type="button"
            className="sr-clear"
            onClick={() => {
              setSearch("");
              setCompanyId("");
              setRows([]);
              setMonth(null);
              setPage(1);
            }}
          >
            Clear
          </button>
          {month && (
            <button
              type="button"
              className="sr-back"
              aria-label="Back to summary"
              title="Back to summary"
              onClick={() => {
                setMonth(null);
                setPage(1);
              }}
            >
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M19 12H5m7 7-7-7 7-7" /></svg>
            </button>
          )}
        </div>
      </section>
      {error && <div className="sr-error">{error}</div>}
      {syncNote && (
        <div className="sr-warning" role="status">
          {syncNote}
        </div>
      )}
      <section className="sr-grid">
        <div className="sr-scroll" ref={scrollRef}>
          <table>
            <thead>
              <tr>
                {month ? (
                  <>
                    <th>S.No</th>
                    <th>Particulars</th>
                    <th>Voucher Count</th>
                    <th>Debit Amount</th>
                    <th>Credit Amount</th>
                    <th>Details</th>
                  </>
                ) : (
                  <>
                    <th>Particulars (Month)</th>
                    <th>Debit</th>
                    <th>Credit</th>
                    <th>Closing Balance</th>
                  </>
                )}
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={month ? 6 : 4} className="sr-empty">
                    Loading Sales vouchers…
                  </td>
                </tr>
              ) : month ? (
                visibleGroups.length ? (
                  visibleGroups.map((group, index) => (
                    <FragmentLedgerGroup
                      key={group.key}
                      group={group}
                      serial={(page - 1) * pageSize + index + 1}
                      expanded={!!expandedLedgers[group.key]}
                      onToggle={() => toggleLedger(group.key)}
                      onView={openVoucher}
                    />
                  ))
                ) : (
                  <tr>
                    <td colSpan="6" className="sr-empty">
                      No Sales vouchers for this period.
                    </td>
                  </tr>
                )
              ) : months.length || isYear || period === "All Available Data" ? (
                months.length ? (
                  months.map((m) => {
                    const debit = debitTotal(m.entries);
                    const credit = creditTotal(m.entries);
                    const closing = balanceTotal(
                      months
                        .slice(0, months.indexOf(m) + 1)
                        .flatMap((x) => x.entries),
                    );
                    return (
                      <tr
                        key={m.key}
                        className="sr-drill"
                        onClick={() => {
                          setMonth(m);
                          setPage(1);
                          setExpandedLedgers({});
                        }}
                      >
                        <td>
                          <button type="button">
                            {m.label} <span>›</span>
                          </button>
                        </td>
                        <td>{money(debit)}</td>
                        <td>{money(credit)}</td>
                        <td>{closingMoney(closing)}</td>
                      </tr>
                    );
                  })
                ) : (
                  <tr>
                    <td colSpan="4" className="sr-empty">
                      No synchronized Sales voucher dates are available.
                    </td>
                  </tr>
                )
              ) : (
                <tr
                  className="sr-drill"
                  onClick={() => {
                    setMonth({ label: period, entries: rows });
                    setPage(1);
                    setExpandedLedgers({});
                  }}
                >
                  <td>
                    <button type="button">
                      {period} <span>›</span>
                    </button>
                  </td>
                  <td>{money(debitTotal(rows))}</td>
                  <td>{money(creditTotal(rows))}</td>
                  <td>{closingMoney(balanceTotal(rows))}</td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        <footer className="sr-footer">
          <strong>
            {month ? `${month.label} Total` : "Grand Total"}
            <span>Debit {money(summaryTotals.debit)}</span>
            <span>Credit {money(summaryTotals.credit)}</span>
            <span>Closing {closingMoney(total)}</span>
          </strong>
          {month && (
            <div>
              <span>
                {activeRows.length} vouchers · {ledgerGroups.length} ledgers ·
                Page {page} of {pageCount}
              </span>
              <button
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
              >
                Previous
              </button>
              <button
                disabled={page >= pageCount}
                onClick={() => setPage((p) => p + 1)}
              >
                Next
              </button>
            </div>
          )}
        </footer>
      </section>
      {selectedRow && (
        <div className="sr-modal-shade" onClick={closeVoucher}>
          <section className="sr-modal" onClick={(e) => e.stopPropagation()}>
            <header>
              <div>
                <h2>Stored Sales Voucher</h2>
                <p>
                  {voucher?.voucherType || selectedRow.voucherType} |{" "}
                  {voucher?.voucherNumber || selectedRow.voucherNumber}
                </p>
              </div>
              {voucher && !detailLoading && !detailError && (
                <div className="sr-modal-toolbar" aria-label="Voucher actions">
                  <button type="button" aria-label="Save voucher as PDF" title="Save as PDF" onClick={printStoredVoucher}>
                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><path d="M14 2v6h6M8 16h2a1.5 1.5 0 0 0 0-3H8v5m6 0v-5h1.5a2.5 2.5 0 0 1 0 5H14"/></svg>
                  </button>
                  <button type="button" aria-label="Print voucher" title="Print" onClick={printStoredVoucher}>
                    <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M7 8V3h10v5M7 17H5a2 2 0 0 1-2-2v-4a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v4a2 2 0 0 1-2 2h-2M7 14h10v7H7z"/><path d="M18 11h.01"/></svg>
                  </button>
                  <button type="button" aria-label="Share voucher" title="Share" onClick={shareStoredVoucher}>
                    <svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="18" cy="5" r="3"/><circle cx="6" cy="12" r="3"/><circle cx="18" cy="19" r="3"/><path d="m8.7 10.6 6.6-4.2M8.7 13.4l6.6 4.2"/></svg>
                  </button>
                </div>
              )}
              <button type="button" aria-label="Close voucher" title="Close" onClick={closeVoucher}>
                <svg viewBox="0 0 24 24" aria-hidden="true"><path d="m6 6 12 12M18 6 6 18" /></svg>
              </button>
            </header>
            {detailLoading ? (
              <p>Loading stored ledger details...</p>
            ) : detailError ? (
              <p className="sr-error">{detailError}</p>
            ) : (
              voucher && (
                <>
                  <div className="sr-modal-body">
                    <p>
                      <b>Date:</b> {dateLabel(voucher.date)} | <b>Party:</b>{" "}
                      {voucher.partyName} | <b>Amount:</b>{" "}
                      {money(voucher.totalAmount)}
                    </p>
                    <h3>Stored Ledger Entries</h3>
                    <table>
                      <thead>
                        <tr>
                          <th>Sequence</th>
                          <th>Ledger</th>
                          <th>Debit</th>
                          <th>Credit</th>
                          <th>Signed Amount</th>
                        </tr>
                      </thead>
                      <tbody>
                        {voucher.ledgerEntries.map((entry) => (
                          <tr key={entry.entryIndex}>
                            <td>{entry.entryIndex + 1}</td>
                            <td>{entry.ledgerName}</td>
                            <td>
                              {entry.amount < 0
                                ? storedPostingAmount(-entry.amount)
                                : ""}
                            </td>
                            <td>
                              {entry.amount > 0
                                ? storedPostingAmount(entry.amount)
                                : ""}
                            </td>
                            <td>{storedPostingAmount(entry.amount)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </>
              )
            )}
          </section>
        </div>
      )}
    </main>
  );
}
