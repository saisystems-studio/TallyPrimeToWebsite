import { useEffect, useState } from "react";
import { getSalesReport } from "../../../services/salesReportService";
import { getLedgers } from "../../../services/tallyService";
import "./SalesReport.css";

export default function SalesReport() {
  const [fromDate, setFromDate] = useState("2024-04-01");
  const [toDate, setToDate] = useState("2024-04-30");

  const [sales, setSales] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [ledgerSearch, setLedgerSearch] = useState("");
  const [ledgers, setLedgers] = useState([]);
  const [showLedgerDropdown, setShowLedgerDropdown] = useState(false);

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

  const loadSalesReport = async () => {
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

      const data = await getSalesReport(
        formatApiDate(fromDate),
        formatApiDate(toDate),
      );

      setSales(Array.isArray(data) ? data : []);
    } catch (err) {
      setSales([]);
      setError(err.message || "Unable to load Sales Report.");
    } finally {
      setLoading(false);
    }
  };

  const ledgerSuggestions = ledgers
    .filter((ledger) =>
      (ledger.name || "")
        .toLowerCase()
        .includes(ledgerSearch.trim().toLowerCase()),
    )
    .slice(0, 10);

  // Ledger name search
  const filteredSales = sales.filter((row) =>
    (row.ledgerName || "")
      .toLowerCase()
      .includes(ledgerSearch.trim().toLowerCase()),
  );

  return (
    <div className="sales-report-page">
      <div className="sales-report-header">
        <div>
          <h1>Sales Report</h1>
          <p>View date-wise sales transactions from Tally</p>
        </div>
      </div>

      <div className="sales-filter-card">
        <div className="sales-filter-fields">
          {/* Ledger Search */}
          <div className="sales-filter-field sales-ledger-search">
            <label>Ledger Name</label>

            <div className="sales-search-container">
              <div className="sales-search-box">
                <span>⌕</span>

                <input
                  type="text"
                  placeholder="Search ledger..."
                  value={ledgerSearch}
                  onFocus={() => setShowLedgerDropdown(true)}
                  onChange={(e) => {
                    setLedgerSearch(e.target.value);
                    setShowLedgerDropdown(true);
                  }}
                  autoComplete="off"
                />
              </div>

              {showLedgerDropdown && ledgerSuggestions.length > 0 && (
                <div className="ledger-suggestions">
                  {ledgerSuggestions.map((ledger) => (
                    <button
                      type="button"
                      key={ledger.name}
                      className="ledger-suggestion-item"
                      onClick={() => {
                        setLedgerSearch(ledger.name);
                        setShowLedgerDropdown(false);
                      }}
                    >
                      {ledger.name}
                    </button>
                  ))}
                </div>
              )}
            </div>
          </div>

          {/* From Date */}
          <div className="sales-filter-field">
            <label htmlFor="sales-from-date">From Date</label>

            <input
              id="sales-from-date"
              type="date"
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
            />
          </div>

          {/* To Date */}
          <div className="sales-filter-field">
            <label htmlFor="sales-to-date">To Date</label>

            <input
              id="sales-to-date"
              type="date"
              value={toDate}
              onChange={(e) => setToDate(e.target.value)}
            />
          </div>

          {/* Load */}
          <button
            className="sales-load-button"
            onClick={loadSalesReport}
            disabled={loading}
          >
            {loading ? "Loading..." : "Load Report"}
          </button>
        </div>

        {/* Filtered count */}
        <div className="sales-count" aria-live="polite">
          Total records <strong>{filteredSales.length}</strong>
        </div>
      </div>

      {error && <div className="sales-error">{error}</div>}

      <div className="sales-table-card">
        <div className="sales-table-wrapper">
          <table className="sales-report-table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Ledger Name</th>
                <th>Voucher Type</th>
                <th>Voucher No</th>
                <th>Debit</th>
                <th>Credit</th>
                <th>Opening Balance</th>
                <th>Closing Balance</th>
              </tr>
            </thead>

            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="8" className="sales-empty">
                    Loading Sales Report...
                  </td>
                </tr>
              ) : filteredSales.length === 0 ? (
                <tr>
                  <td colSpan="8" className="sales-empty">
                    No sales records found.
                  </td>
                </tr>
              ) : (
                filteredSales.map((row, index) => (
                  <tr key={`${row.date}-${row.voucherNumber}-${index}`}>
                    <td>{formatDate(row.date)}</td>

                    <td className="sales-ledger">{row.ledgerName || "--"}</td>

                    <td>{row.voucherType || "--"}</td>

                    <td>{row.voucherNumber || "--"}</td>

                    <td className="sales-amount">
                      {row.debit ? formatAmount(row.debit) : "--"}
                    </td>

                    <td className="sales-amount">
                      {row.credit ? formatAmount(row.credit) : "--"}
                    </td>

                    <td className="sales-amount">
                      {formatAmount(row.openingBalance)}
                    </td>

                    <td className="sales-amount">
                      {formatAmount(row.closingBalance)}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
