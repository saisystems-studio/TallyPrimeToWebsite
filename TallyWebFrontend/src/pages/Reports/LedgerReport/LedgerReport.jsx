import { useEffect, useState } from "react";
import { getLedgers } from "../../../services/tallyService";
import { getLedgerReport } from "../../../services/ledgerReportService";
import "./LedgerReport.css";

export default function LedgerReport() {
  const [ledgers, setLedgers] = useState([]);
  const [ledgerSearch, setLedgerSearch] = useState("");
  const [showDropdown, setShowDropdown] = useState(false);

  const [fromDate, setFromDate] = useState("2024-12-01");
  const [toDate, setToDate] = useState("2024-12-31");

  const [report, setReport] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    const loadLedgers = async () => {
      try {
        const data = await getLedgers();
        setLedgers(data.ledgers || []);
      } catch (err) {
        console.error("Unable to load ledgers:", err);
        setLedgers([]);
      }
    };

    loadLedgers();
  }, []);

  const ledgerSuggestions = ledgers
    .filter((ledger) =>
      (ledger.name || "")
        .toLowerCase()
        .includes(ledgerSearch.trim().toLowerCase()),
    )
    .slice(0, 10);

  const formatApiDate = (date) => {
    return date.replaceAll("-", "");
  };

  const formatDate = (date) => {
    if (!date || date.length !== 8) return date || "--";

    return `${date.substring(6, 8)}/${date.substring(
      4,
      6,
    )}/${date.substring(0, 4)}`;
  };

  const formatAmount = (amount) => {
    const value = Number(amount);

    if (Number.isNaN(value)) return "--";

    return Math.abs(value).toLocaleString("en-IN", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    });
  };

  const loadReport = async () => {
    if (!ledgerSearch.trim()) {
      setError("Please select a Ledger.");
      return;
    }

    if (!fromDate || !toDate) {
      setError("Please select From Date and To Date.");
      return;
    }

    if (fromDate > toDate) {
      setError("From Date cannot be greater than To Date.");
      return;
    }

    try {
      setLoading(true);
      setError("");

      const data = await getLedgerReport(
        ledgerSearch.trim(),
        formatApiDate(fromDate),
        formatApiDate(toDate),
      );

      setReport(data);
    } catch (err) {
      setReport(null);
      setError(err.message || "Unable to load Ledger Report.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="ledger-report-page">
      <div className="ledger-report-header">
        <h1>Ledger Report</h1>
        <p>View ledger transactions directly from Tally</p>
      </div>

      <div className="ledger-report-filter">
        <div className="ledger-report-filters">
        <div className="ledger-report-field ledger-select-field">
          <label htmlFor="ledger-report-search">Ledger Name</label>

          <div className="ledger-search-container">
            <div className="ledger-search-input">
              <span>⌕</span>

              <input
                id="ledger-report-search"
                type="text"
                placeholder="Search ledger..."
                value={ledgerSearch}
                autoComplete="off"
                onFocus={() => setShowDropdown(true)}
                onChange={(e) => {
                  setLedgerSearch(e.target.value);
                  setShowDropdown(true);
                  setReport(null);
                }}
              />
            </div>

            {showDropdown && ledgerSuggestions.length > 0 && (
              <div className="ledger-report-dropdown">
                {ledgerSuggestions.map((ledger, index) => (
                  <button
                    type="button"
                    key={`${ledger.name}-${index}`}
                    onClick={() => {
                      setLedgerSearch(ledger.name);
                      setShowDropdown(false);
                      setReport(null);
                    }}
                  >
                    {ledger.name}
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>

        <div className="ledger-report-field">
          <label htmlFor="ledger-report-from">From Date</label>
          <input
            id="ledger-report-from"
            type="date"
            value={fromDate}
            onChange={(e) => setFromDate(e.target.value)}
          />
        </div>

        <div className="ledger-report-field">
          <label htmlFor="ledger-report-to">To Date</label>
          <input
            id="ledger-report-to"
            type="date"
            value={toDate}
            onChange={(e) => setToDate(e.target.value)}
          />
        </div>

        <button
          type="button"
          className="ledger-load-button"
          onClick={loadReport}
          disabled={loading}
        >
          {loading ? "Loading..." : "Load Report"}
        </button>
        </div>

        <div className="ledger-report-count" aria-live="polite">
          Total records <strong>{report?.transactions?.length || 0}</strong>
        </div>
      </div>

      {error && <div className="ledger-report-error">{error}</div>}

      <div className="ledger-report-card">
        {report && (
          <div className="ledger-report-card-header">
            <h2>{report.ledgerName || "Ledger Transactions"}</h2>
          </div>
        )}

        <div className="ledger-report-table-wrapper">
          <table className="ledger-report-table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Particulars</th>
                <th>Voucher Type</th>
                <th>Voucher No</th>
                <th>Debit</th>
                <th>Credit</th>
              </tr>
            </thead>

            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="6" className="ledger-report-empty">
                    Loading Ledger Report...
                  </td>
                </tr>
              ) : !report ? (
                <tr>
                  <td colSpan="6" className="ledger-report-empty">
                    Select a ledger and date range, then click Load Report.
                  </td>
                </tr>
              ) : !report.transactions?.length ? (
                <tr>
                  <td colSpan="6" className="ledger-report-empty">
                    No transactions found.
                  </td>
                </tr>
              ) : (
                report.transactions.map((row, index) => (
                  <tr
                    key={`${row.date}-${row.voucherType}-${row.voucherNumber}-${index}`}
                  >
                    <td>{formatDate(row.date)}</td>

                    <td className="ledger-particular">
                      {row.particulars || "--"}
                    </td>

                    <td>{row.voucherType || "--"}</td>

                    <td>{row.voucherNumber || "--"}</td>

                    <td className="ledger-report-amount">
                      {row.debit ? formatAmount(row.debit) : "--"}
                    </td>

                    <td className="ledger-report-amount">
                      {row.credit ? formatAmount(row.credit) : "--"}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {report && (
          <div className="ledger-report-summary">
            <div>
              <span>Opening Balance</span>
              <strong>{formatAmount(report.openingBalance)}</strong>
            </div>

            <div>
              <span>Total Debit</span>
              <strong>{formatAmount(report.totalDebit)}</strong>
            </div>

            <div>
              <span>Total Credit</span>
              <strong>{formatAmount(report.totalCredit)}</strong>
            </div>

            <div>
              <span>Closing Balance</span>
              <strong>{formatAmount(report.closingBalance)}</strong>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
