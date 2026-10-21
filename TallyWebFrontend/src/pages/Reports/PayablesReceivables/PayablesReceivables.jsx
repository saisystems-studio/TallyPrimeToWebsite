import { useEffect, useMemo, useState } from "react";
import { useLocation } from "react-router-dom";

import { getDbCompanies } from "../../../services/companyService";
import { getDbOutstanding } from "../../../services/outstandingDbService";

import "./PayablesReceivables.css";

function PayablesReceivables() {
  const { pathname } = useLocation();
  const activeTab = pathname === "/payables" ? "Payable" : "Receivable";
  const pageTitle = activeTab === "Receivable" ? "Receivables" : "Payables";
  const [companies, setCompanies] = useState([]);
  const [selectedCompanyId, setSelectedCompanyId] = useState("");

  const [rows, setRows] = useState([]);
  const [selectedLedger, setSelectedLedger] = useState("");
  const [searchText, setSearchText] = useState("");
  const [ledgerWise, setLedgerWise] = useState(false);
  const [expandedLedgers, setExpandedLedgers] = useState(() => new Set());

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const [selectedRow, setSelectedRow] = useState(null);

  // =========================================================
  // LOAD COMPANIES
  // =========================================================

  useEffect(() => {
    async function loadCompanies() {
      try {
        const data = await getDbCompanies();

        const companyList = Array.isArray(data)
          ? data
          : Array.isArray(data?.companies)
            ? data.companies
            : [];

        setCompanies(companyList);
      } catch (err) {
        console.error(err);
        setError("Unable to load companies.");
      }
    }

    loadCompanies();
  }, []);

  // =========================================================
  // LOAD SQL OUTSTANDING
  // =========================================================

  useEffect(() => {
    async function loadOutstanding() {
      try {
        setLoading(true);
        setError("");

        // includeSettled = false
        // Only current non-zero outstanding rows required here.
        const data = await getDbOutstanding(selectedCompanyId, "", false);

        setRows(Array.isArray(data) ? data : []);
      } catch (err) {
        console.error(err);
        setRows([]);
        setError(`Unable to load ${pageTitle.toLowerCase()}.`);
      } finally {
        setLoading(false);
      }
    }

    loadOutstanding();
  }, [selectedCompanyId, pageTitle]);

  // =========================================================
  // RESET FILTERS WHEN COMPANY CHANGES
  // =========================================================

  useEffect(() => {
    setSelectedLedger("");
    setSearchText("");
    setSelectedRow(null);
  }, [selectedCompanyId]);

  // =========================================================
  // PARTY OPTIONS
  // =========================================================

  const ledgerOptions = useMemo(() => {
    return [
      ...new Set(
        rows
          .filter((row) => row.balanceType === activeTab)
          .map((row) => row.ledgerName)
          .filter(Boolean),
      ),
    ].sort((a, b) => a.localeCompare(b));
  }, [rows, activeTab]);

  // =========================================================
  // SUMMARY CALCULATION
  // =========================================================

  const summary = useMemo(() => {
    const partyRows = selectedLedger
      ? rows.filter((row) => row.ledgerName === selectedLedger)
      : rows;

    // Summary must represent the same CURRENT PENDING BILLS
    // displayed in the Payables & Receivables table.
    // Do not include On Account/history rows here.
    const pendingBills = partyRows.filter(
      (row) => row.rowType === "Bill" && row.status === "Pending",
    );

    const receivableRows = pendingBills.filter(
      (row) => row.balanceType === "Receivable",
    );

    const payableRows = pendingBills.filter(
      (row) => row.balanceType === "Payable",
    );

    const receivable = receivableRows.reduce(
      (total, row) => total + Number(row.pendingAmount || 0),
      0,
    );

    const payable = payableRows.reduce(
      (total, row) => total + Number(row.pendingAmount || 0),
      0,
    );

    const receivableParties = new Set(
      receivableRows.map((row) => `${row.companyId}|${row.ledgerName}`),
    ).size;

    const payableParties = new Set(
      payableRows.map((row) => `${row.companyId}|${row.ledgerName}`),
    ).size;

    return {
      receivable,
      payable,
      receivableParties,
      payableParties,
    };
  }, [rows, selectedLedger]);

  // =========================================================
  // TABLE FILTER
  // =========================================================

  const filteredRows = useMemo(() => {
    const search = searchText.trim().toLowerCase();

    return rows.filter((row) => {
      // Only actual bills in the main table.
      // On Account adjustments still participate in summary totals.
      if (row.rowType !== "Bill") {
        return false;
      }

      if (row.status !== "Pending") {
        return false;
      }

      if (row.balanceType !== activeTab) {
        return false;
      }

      if (selectedLedger && row.ledgerName !== selectedLedger) {
        return false;
      }

      if (search) {
        const searchableText = [
          row.companyName,
          row.ledgerName,
          row.billReference,
          row.voucherNumber,
          row.voucherType,
        ]
          .filter(Boolean)
          .join(" ")
          .toLowerCase();

        if (!searchableText.includes(search)) {
          return false;
        }
      }

      return true;
    });
  }, [rows, activeTab, selectedLedger, searchText]);

  // =========================================================
  // TABLE TOTAL
  // =========================================================

  const tableTotal = useMemo(() => {
    return filteredRows.reduce(
      (total, row) => total + Number(row.pendingAmount || 0),
      0,
    );
  }, [filteredRows]);

  const ledgerGroups = useMemo(() => {
    const groups = new Map();

    filteredRows.forEach((row) => {
      const ledgerIdentity = row.ledgerId ?? `name:${row.ledgerName || ""}`;
      const key = `${row.companyId}|${ledgerIdentity}`;
      let group = groups.get(key);

      if (!group) {
        group = {
          key,
          companyId: row.companyId,
          companyName: row.companyName,
          ledgerId: row.ledgerId,
          ledgerName: row.ledgerName || "--",
          bills: [],
          totalPending: 0,
        };
        groups.set(key, group);
      }

      group.bills.push(row);
      group.totalPending += Number(row.pendingAmount || 0);
    });

    return [...groups.values()];
  }, [filteredRows]);

  const ledgerGrandTotal = useMemo(
    () => ledgerGroups.reduce((total, group) => total + group.totalPending, 0),
    [ledgerGroups],
  );

  function toggleLedger(key) {
    setExpandedLedgers((current) => {
      const next = new Set(current);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  // =========================================================
  // HELPERS
  // =========================================================

  function formatAmount(value) {
    return Number(value || 0).toLocaleString("en-IN", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    });
  }

  function formatDate(value) {
    if (!value || value.length !== 8) {
      return "--";
    }

    const year = value.substring(0, 4);
    const month = value.substring(4, 6);
    const day = value.substring(6, 8);

    return `${day}/${month}/${year}`;
  }

  function getDueDate(row) {
    // API currently gives bill date + Tally credit-period text,
    // but not a separately calculated due-date field.
    // Do not invent a date.
    return row.creditPeriod || "--";
  }

  return (
    <div className="pr-page">
      {/* HEADER */}

      <div className="pr-header">
        <div>
          <div className="pr-eyebrow">REPORTS</div>

          <h1>{pageTitle}</h1>

          <p>{activeTab === "Receivable"
            ? "View pending customer receivables."
            : "View pending supplier payables."}</p>
        </div>
      </div>

      {/* FILTERS */}

      <div className="pr-filters">
        <div className="pr-field">
          <label>Company</label>

          <select
            value={selectedCompanyId}
            onChange={(e) => setSelectedCompanyId(e.target.value)}
          >
            <option value="">All Companies</option>

            {companies.map((company) => (
              <option key={company.id} value={company.id}>
                {company.name}
              </option>
            ))}
          </select>
        </div>

        <div className="pr-field pr-party-field">
          <label>Party / Ledger</label>

          <div className="pr-party-filter-controls">
            <select
              value={selectedLedger}
              onChange={(e) => setSelectedLedger(e.target.value)}
            >
              <option value="">All Parties</option>

              {ledgerOptions.map((ledger) => (
                <option key={ledger} value={ledger}>
                  {ledger}
                </option>
              ))}
            </select>

          </div>
        </div>

        <div className="pr-field pr-search-field">
          <label>Search</label>

          <input
            type="text"
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
            placeholder="Search party or bill reference..."
          />
        </div>
      </div>

      {/* SUMMARY */}

      <div className="pr-summary">
        {activeTab === "Receivable" ? (
          <>
            <div className="pr-summary-card receivable">
              <span>Total Receivable</span>
              <strong>₹ {formatAmount(summary.receivable)}</strong>
              <small>{summary.receivableParties} Parties</small>
            </div>
            <div className="pr-summary-card">
              <span>Receivable Parties</span>
              <strong>{summary.receivableParties}</strong>
              <small>Pending customer balances</small>
            </div>
          </>
        ) : (
          <>
            <div className="pr-summary-card payable">
              <span>Total Payable</span>
              <strong>₹ {formatAmount(summary.payable)}</strong>
              <small>{summary.payableParties} Parties</small>
            </div>
            <div className="pr-summary-card">
              <span>Payable Parties</span>
              <strong>{summary.payableParties}</strong>
              <small>Pending supplier balances</small>
            </div>
          </>
        )}
      </div>

      {/* ERROR */}

      {error && <div className="pr-error">{error}</div>}

      {/* TABLE */}

      <div className="pr-table-card">
        <div className="pr-table-header">
          <div>
            <h3>{pageTitle}</h3>

            <p>
              {ledgerWise
                ? `${ledgerGroups.length} ledger${ledgerGroups.length === 1 ? "" : "s"} · ${filteredRows.length} pending bill${filteredRows.length === 1 ? "" : "s"}`
                : `${filteredRows.length} pending bill${filteredRows.length === 1 ? "" : "s"}`}
            </p>
          </div>

          {activeTab === "Receivable" && (
            <div className="pr-view-toggle-slot">
              <label className="pr-ledger-wise-toggle">
                <input
                  type="checkbox"
                  checked={ledgerWise}
                  onChange={(event) => setLedgerWise(event.target.checked)}
                />
                <span>Ledger Wise</span>
              </label>
            </div>
          )}

          <div className="pr-table-total">
            <span>Total</span>

            <strong>₹ {formatAmount(ledgerWise ? ledgerGrandTotal : tableTotal)}</strong>
          </div>
        </div>

        <div className="pr-table-wrapper">
          {ledgerWise ? (
          <table className="pr-table pr-ledger-table">
            <thead>
              <tr>
                <th>S.No</th><th>Company</th><th>Ledger / Party Name</th>
                <th className="pr-days">Total Bills</th>
                <th className="pr-amount">Total Pending Amount</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr><td colSpan="5" className="pr-message">Loading...</td></tr>
              ) : ledgerGroups.length === 0 ? (
                <tr><td colSpan="5" className="pr-message">No pending receivables found.</td></tr>
              ) : ledgerGroups.map((group, index) => (
                <LedgerGroupRows
                  key={group.key}
                  group={group}
                  index={index}
                  expanded={expandedLedgers.has(group.key)}
                  onToggle={() => toggleLedger(group.key)}
                  formatAmount={formatAmount}
                  formatDate={formatDate}
                  getDueDate={getDueDate}
                />
              ))}
            </tbody>
            {!loading && ledgerGroups.length > 0 && (
              <tfoot>
                <tr>
                  <td colSpan="4">Grand Total</td>
                  <td className="pr-amount">₹ {formatAmount(ledgerGrandTotal)}</td>
                </tr>
              </tfoot>
            )}
          </table>
          ) : (
          <table className="pr-table">
            <thead>
              <tr>
                <th>S.No</th>
                <th>Company</th>
                <th>Party</th>
                <th>Bill Ref</th>
                <th>Bill Date</th>
                <th>Credit Period</th>
                <th className="pr-days">Overdue Days</th>
                <th className="pr-amount">Pending Amount</th>
              </tr>
            </thead>

            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="8" className="pr-message">
                    Loading...
                  </td>
                </tr>
              ) : filteredRows.length === 0 ? (
                <tr>
                  <td colSpan="8" className="pr-message">
                    No pending{" "}
                    {activeTab === "Receivable" ? "receivables" : "payables"}{" "}
                    found.
                  </td>
                </tr>
              ) : (
                filteredRows.map((row, index) => (
                  <tr
                    key={`${row.companyId}-${row.ledgerName}-${row.internalBillReference || row.billReference}-${index}`}
                    onClick={() => setSelectedRow(row)}
                    className="pr-clickable-row"
                  >
                    <td>{index + 1}</td>

                    <td>{row.companyName || "--"}</td>

                    <td className="pr-party">{row.ledgerName || "--"}</td>

                    <td>{row.billReference || "--"}</td>

                    <td>{formatDate(row.billDate || row.voucherDate)}</td>

                    <td>{getDueDate(row)}</td>

                    <td className="pr-days">{row.pendingDays ?? 0}</td>

                    <td className="pr-amount">
                      ₹ {formatAmount(row.pendingAmount)}
                    </td>
                  </tr>
                ))
              )}
            </tbody>

            {!loading && filteredRows.length > 0 && (
              <tfoot>
                <tr>
                  <td colSpan="7">Total</td>

                  <td className="pr-amount">₹ {formatAmount(tableTotal)}</td>
                </tr>
              </tfoot>
            )}
          </table>
          )}
        </div>
      </div>

      {/* DETAIL MODAL */}

      {selectedRow && !ledgerWise && (
        <div className="pr-modal-backdrop" onClick={() => setSelectedRow(null)}>
          <div className="pr-modal" onClick={(e) => e.stopPropagation()}>
            <div className="pr-modal-header">
              <div>
                <span className="pr-modal-type">{selectedRow.balanceType}</span>

                <h2>{selectedRow.ledgerName}</h2>

                <p>
                  {selectedRow.companyName || "--"} · Bill Ref:{" "}
                  <strong>{selectedRow.billReference || "--"}</strong>
                </p>
              </div>

              <button type="button" onClick={() => setSelectedRow(null)}>
                ×
              </button>
            </div>

            <div className="pr-modal-summary">
              <div>
                <span>Bill Amount</span>

                <strong>₹ {formatAmount(selectedRow.originalAmount)}</strong>
              </div>

              <div>
                <span>Pending Amount</span>

                <strong>₹ {formatAmount(selectedRow.pendingAmount)}</strong>
              </div>

              <div>
                <span>Bill Date</span>

                <strong>
                  {formatDate(selectedRow.billDate || selectedRow.voucherDate)}
                </strong>
              </div>

              <div>
                <span>Pending Days</span>

                <strong>{selectedRow.pendingDays ?? 0}</strong>
              </div>
            </div>

            <div className="pr-allocation-title">Allocation History</div>

            <div className="pr-allocation-wrapper">
              <table className="pr-allocation-table">
                <thead>
                  <tr>
                    <th>Date</th>
                    <th>Voucher No</th>
                    <th>Voucher Type</th>
                    <th>Bill Type</th>
                    <th className="pr-amount">Amount</th>
                  </tr>
                </thead>

                <tbody>
                  {selectedRow.allocations?.length > 0 ? (
                    selectedRow.allocations.map((allocation, index) => (
                      <tr key={index}>
                        <td>{formatDate(allocation.voucherDate)}</td>

                        <td>{allocation.voucherNumber || "--"}</td>

                        <td>{allocation.voucherType || "--"}</td>

                        <td>{allocation.billType || "--"}</td>

                        <td className="pr-amount">
                          ₹ {formatAmount(allocation.amount)}
                        </td>
                      </tr>
                    ))
                  ) : (
                    <tr>
                      <td colSpan="5" className="pr-message">
                        No allocation history.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function LedgerGroupRows({
  group,
  index,
  expanded,
  onToggle,
  formatAmount,
  formatDate,
  getDueDate,
}) {
  return (
    <>
      <tr className="pr-ledger-row">
        <td>{index + 1}</td>
        <td>{group.companyName || "--"}</td>
        <td className="pr-party">
          <button
            type="button"
            className="pr-expand-toggle"
            onClick={onToggle}
            aria-expanded={expanded}
            aria-label={`${expanded ? "Collapse" : "Expand"} bills for ${group.ledgerName}`}
          >
            {expanded ? "−" : "+"}
          </button>
          {group.ledgerName}
        </td>
        <td className="pr-days">{group.bills.length}</td>
        <td className="pr-amount">₹ {formatAmount(group.totalPending)}</td>
      </tr>
      {expanded && (
        <tr className="pr-ledger-details-row">
          <td colSpan="5">
            <table className="pr-ledger-details-table">
              <thead>
                <tr>
                  <th>Bill Ref</th>
                  <th>Bill Date</th>
                  <th>Credit Period</th>
                  <th className="pr-days">Overdue Days</th>
                  <th className="pr-amount">Pending Amount</th>
                </tr>
              </thead>
              <tbody>
                {group.bills.map((bill, billIndex) => (
                  <tr key={`${bill.internalBillReference || bill.billReference}-${billIndex}`}>
                    <td>{bill.billReference || "--"}</td>
                    <td>{formatDate(bill.billDate || bill.voucherDate)}</td>
                    <td>{getDueDate(bill)}</td>
                    <td className="pr-days">{bill.pendingDays ?? 0}</td>
                    <td className="pr-amount">₹ {formatAmount(bill.pendingAmount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </td>
        </tr>
      )}
    </>
  );
}

export default PayablesReceivables;
