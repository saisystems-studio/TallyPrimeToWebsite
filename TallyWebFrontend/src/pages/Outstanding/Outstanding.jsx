import { useEffect, useMemo, useState } from "react";

import { getDbCompanies } from "../../services/companyService";
import { getDbOutstanding } from "../../services/outstandingDbService";
import { findOutstandingBill, outstandingBillFilename, outstandingBillModel, outstandingBillPdf } from "./outstandingBillDocument";
import OutstandingBillPreview from "./OutstandingBillPreview";
import { shareInvoicePdf } from "../Vouchers/invoiceRenderer";

import "./Outstanding.css";

function Outstanding() {
  const [companies, setCompanies] = useState([]);
  const [selectedCompanyId, setSelectedCompanyId] = useState("");

  const [rows, setRows] = useState([]);
  const [selectedLedger, setSelectedLedger] = useState("");
  const [statusFilter, setStatusFilter] = useState("Pending");

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const [selectedRow, setSelectedRow] = useState(null);
  const [previewModel, setPreviewModel] = useState(null);
  const [billActionError, setBillActionError] = useState("");
  const [billActionStatus, setBillActionStatus] = useState("");
  const [billActionBusy, setBillActionBusy] = useState(false);

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
  // LOAD OUTSTANDING
  //
  // selectedCompanyId = ""
  //      -> All Companies
  //
  // selectedCompanyId = "1", "2", ...
  //      -> Selected Company only
  //
  // Always fetch settled rows too.
  // Frontend handles Pending / Settled / All filter.
  // =========================================================

  useEffect(() => {
    async function loadOutstanding() {
      try {
        setLoading(true);
        setError("");

        const data = await getDbOutstanding(selectedCompanyId, "", true);

        setRows(Array.isArray(data) ? data : []);
      } catch (err) {
        console.error(err);
        setRows([]);
        setError("Unable to load outstanding data.");
      } finally {
        setLoading(false);
      }
    }

    loadOutstanding();
  }, [selectedCompanyId]);

  // =========================================================
  // RESET PARTY / MODAL WHEN COMPANY CHANGES
  // =========================================================

  useEffect(() => {
    setSelectedLedger("");
    setSelectedRow(null);
  }, [selectedCompanyId]);

  // =========================================================
  // PARTY / LEDGER OPTIONS
  // =========================================================

  const ledgerOptions = useMemo(() => {
    return [...new Set(rows.map((x) => x.ledgerName).filter(Boolean))].sort(
      (a, b) => a.localeCompare(b),
    );
  }, [rows]);

  // =========================================================
  // FILTERED DATA
  // =========================================================

  const filteredRows = useMemo(() => {
    return rows.filter((row) => {
      const ledgerMatches =
        !selectedLedger || row.ledgerName === selectedLedger;

      const statusMatches =
        statusFilter === "All" ||
        (statusFilter === "Pending" &&
          (row.status === "Pending" || row.rowType === "OnAccount")) ||
        (statusFilter === "Settled" && row.status === "Settled");

      return ledgerMatches && statusMatches;
    });
  }, [rows, selectedLedger, statusFilter]);

  // =========================================================
  // SUMMARY
  //
  // Summary uses all loaded rows for the selected company.
  // Party selection narrows the summary to that party.
  //
  // On Account adjustments participate in net balance.
  // =========================================================

  const summary = useMemo(() => {
    const relevantRows = selectedLedger
      ? rows.filter((row) => row.ledgerName === selectedLedger)
      : rows;

    // ---------------------------------------------------------
    // RECEIVABLE
    //
    // Sundry Debtors:
    // Bill / Debit     = negative
    // Receipt / Credit = positive
    //
    // Example:
    // -5000 + 243 = -4757
    // Display receivable = 4757
    // ---------------------------------------------------------

    const receivableSigned = relevantRows
      .filter((row) => row.balanceType === "Receivable")
      .reduce((total, row) => total + Number(row.signedPendingAmount || 0), 0);

    const receivable = Math.abs(Math.min(receivableSigned, 0));

    // ---------------------------------------------------------
    // PAYABLE
    //
    // Keep current Tally signed accounting calculation.
    // ---------------------------------------------------------

    const payableSigned = relevantRows
      .filter((row) => row.balanceType === "Payable")
      .reduce((total, row) => total + Number(row.signedPendingAmount || 0), 0);

    const payable = Math.abs(Math.max(payableSigned, 0));

    // ---------------------------------------------------------
    // PENDING BILL COUNT
    //
    // Only actual bills.
    // On Account adjustment is not counted as a bill.
    // ---------------------------------------------------------

    const pendingBills = relevantRows.filter(
      (row) =>
        row.rowType === "Bill" &&
        row.status === "Pending" &&
        Number(row.signedPendingAmount || 0) !== 0,
    ).length;

    return {
      receivable,
      payable,
      pendingBills,
    };
  }, [rows, selectedLedger]);

  // =========================================================
  // HELPERS
  // =========================================================

  function formatAmount(value) {
    const amount = Number(value || 0);

    return amount.toLocaleString("en-IN", {
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

  async function fetchSelectedBill() {
    if (!selectedRow?.companyId || !selectedRow?.ledgerId || !selectedRow?.ledgerName || !selectedRow?.internalBillReference) {
      throw new Error("The selected bill is missing its company, party, or bill identity.");
    }
    const data = await getDbOutstanding(selectedRow.companyId, selectedRow.ledgerName, true);
    const match = findOutstandingBill(data, selectedRow);
    if (!match) throw new Error("The selected bill could not be uniquely matched in the current outstanding data. Reload the report and try again.");
    return match;
  }

  async function openBillPreview() {
    setBillActionBusy(true); setBillActionError(""); setBillActionStatus("");
    try {
      const bill = await fetchSelectedBill();
      setSelectedRow(bill);
      setPreviewModel(outstandingBillModel(bill));
    } catch (failure) { setBillActionError(failure.message || "Unable to prepare the selected bill."); }
    finally { setBillActionBusy(false); }
  }

  async function shareSelectedBill() {
    setBillActionBusy(true); setBillActionError(""); setBillActionStatus("Preparing the selected bill PDF...");
    try {
      const bill = await fetchSelectedBill();
      setSelectedRow(bill);
      const model = outstandingBillModel(bill);
      await document.fonts.ready;
      const file = new File([outstandingBillPdf(model)], outstandingBillFilename(model), { type: "application/pdf" });
      let fallbackUsed = false;
      const result = await shareInvoicePdf(file, { fallbackDownload: (sharedFile) => {
        fallbackUsed = true;
        const url = URL.createObjectURL(sharedFile); const link = document.createElement("a");
        link.href = url; link.download = sharedFile.name; document.body.append(link); link.click(); link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 60000);
      } });
      if (result === "shared") setBillActionStatus("The selected bill PDF was shared.");
      else if (result === "cancelled") setBillActionStatus("Sharing was cancelled.");
      else if (fallbackUsed) setBillActionStatus("Native file sharing is unavailable. The selected bill PDF was downloaded instead.");
    } catch (failure) { setBillActionError(failure.message || "Unable to share or download the selected bill PDF."); setBillActionStatus(""); }
    finally { setBillActionBusy(false); }
  }

  function closeBillDetails() {
    setSelectedRow(null); setPreviewModel(null); setBillActionError(""); setBillActionStatus("");
  }

  return (
    <div className="outstanding-page">
      {/* HEADER */}

      <div className="outstanding-header">
        <div>
          <h1>Outstanding Report</h1>
          <p>Bill-wise receivables and payables</p>
        </div>
      </div>

      {/* =====================================================
          FILTERS
          ===================================================== */}

      <div className="outstanding-filters">
        {/* COMPANY */}

        <div className="outstanding-field">
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

        {/* PARTY / LEDGER */}

        <div className="outstanding-field">
          <label>Party / Ledger</label>

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

        {/* STATUS */}

        <div className="outstanding-field">
          <label>Status</label>

          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
          >
            <option value="Pending">Pending</option>

            <option value="Settled">Settled</option>

            <option value="All">All</option>
          </select>
        </div>
      </div>

      {/* =====================================================
          SUMMARY
          ===================================================== */}

      <div className="outstanding-summary">
        <div className="outstanding-summary-card">
          <span>Total Receivable</span>

          <strong>₹ {formatAmount(summary.receivable)}</strong>
        </div>

        <div className="outstanding-summary-card">
          <span>Total Payable</span>

          <strong>₹ {formatAmount(summary.payable)}</strong>
        </div>

        <div className="outstanding-summary-card">
          <span>Pending Bills</span>

          <strong>{summary.pendingBills}</strong>
        </div>
      </div>

      {/* ERROR */}

      {error && <div className="outstanding-error">{error}</div>}

      {/* =====================================================
          MAIN TABLE
          ===================================================== */}

      <div className="outstanding-table-card">
        <div className="outstanding-table-wrapper">
          <table className="outstanding-table">
            <thead>
              <tr>
                <th>S.No</th>

                <th>Company</th>

                <th>Bill Date</th>

                <th>Party</th>

                <th>Bill Ref</th>

                <th>Type</th>

                <th className="amount-column">Bill Amount</th>

                <th className="amount-column">Adjustment</th>

                <th className="amount-column">Pending</th>

                <th className="days-column">Pending Days</th>

                <th>Status</th>
              </tr>
            </thead>

            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="11" className="table-message">
                    Loading outstanding...
                  </td>
                </tr>
              ) : filteredRows.length === 0 ? (
                <tr>
                  <td colSpan="11" className="table-message">
                    No outstanding records found.
                  </td>
                </tr>
              ) : (
                filteredRows.map((row, index) => (
                  <tr
                    key={`${row.companyId}-${row.ledgerName}-${row.internalBillReference || row.billReference}-${index}`}
                    onClick={() => setSelectedRow(row)}
                    className="clickable-row"
                  >
                    {/* S.NO */}

                    <td>{index + 1}</td>

                    {/* COMPANY */}

                    <td>{row.companyName || "--"}</td>

                    {/* BILL DATE */}

                    <td>{formatDate(row.billDate || row.voucherDate)}</td>

                    {/* PARTY */}

                    <td className="party-column">{row.ledgerName || "--"}</td>

                    {/* BILL REFERENCE */}

                    <td>{row.billReference || "--"}</td>

                    {/* TYPE */}

                    <td>
                      <span
                        className={`balance-type ${
                          row.balanceType === "Receivable"
                            ? "receivable"
                            : row.balanceType === "Payable"
                              ? "payable"
                              : ""
                        }`}
                      >
                        {row.rowType === "OnAccount"
                          ? "Adjustment"
                          : row.balanceType || "--"}
                      </span>
                    </td>

                    {/* BILL AMOUNT */}

                    <td className="amount-column">
                      {row.rowType === "OnAccount"
                        ? "--"
                        : `₹ ${formatAmount(row.originalAmount)}`}
                    </td>

                    {/* ADJUSTMENT */}

                    <td className="amount-column">
                      {row.rowType === "OnAccount"
                        ? `₹ ${formatAmount(row.pendingAmount)}`
                        : "--"}
                    </td>

                    {/* PENDING */}

                    <td className="amount-column">
                      {row.rowType === "OnAccount"
                        ? "--"
                        : `₹ ${formatAmount(row.pendingAmount)}`}
                    </td>

                    {/* PENDING DAYS */}

                    <td className="days-column">{row.pendingDays ?? 0}</td>

                    {/* STATUS */}

                    <td>
                      <span
                        className={`outstanding-status ${
                          row.status === "Pending" ? "pending" : "settled"
                        }`}
                      >
                        {row.status}
                      </span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* =====================================================
          ALLOCATION DETAIL MODAL
          ===================================================== */}

      {selectedRow && (
        <div
          className="outstanding-modal-backdrop"
          onClick={closeBillDetails}
        >
          <div
            className="outstanding-modal"
            onClick={(e) => e.stopPropagation()}
          >
            {/* MODAL HEADER */}

            <div className="outstanding-modal-header">
              <div>
                <h2>{selectedRow.ledgerName}</h2>

                <p>
                  Bill Reference:{" "}
                  <strong>{selectedRow.billReference || "--"}</strong>
                </p>

                <p>
                  Company: <strong>{selectedRow.companyName || "--"}</strong>
                </p>
              </div>

              <div className="outstanding-modal-controls">
                <button type="button" className="outstanding-modal-action" onClick={openBillPreview} disabled={billActionBusy}>Export PDF</button>
                <button type="button" className="outstanding-modal-action" onClick={shareSelectedBill} disabled={billActionBusy}>{billActionBusy ? "Preparing..." : "Share"}</button>
                <button type="button" className="outstanding-modal-close" onClick={closeBillDetails} aria-label="Close">×</button>
              </div>
            </div>

            {billActionError && <div className="outstanding-action-error" role="alert">{billActionError}</div>}
            {billActionStatus && <div className="outstanding-action-status" role="status">{billActionStatus}</div>}

            {/* MODAL SUMMARY */}

            <div className="outstanding-modal-summary">
              <div>
                <span>Original Amount</span>

                <strong>₹ {formatAmount(selectedRow.originalAmount)}</strong>
              </div>

              <div>
                <span>Pending Amount</span>

                <strong>₹ {formatAmount(selectedRow.pendingAmount)}</strong>
              </div>

              <div>
                <span>Pending Days</span>

                <strong>{selectedRow.pendingDays ?? 0}</strong>
              </div>

              <div>
                <span>Type</span>

                <strong>
                  {selectedRow.rowType === "OnAccount"
                    ? "Adjustment"
                    : selectedRow.balanceType || "--"}
                </strong>
              </div>
            </div>

            {/* TRANSACTION TITLE */}

            <div className="allocation-title">
              Transaction / Allocation History
            </div>

            {/* TRANSACTION TABLE */}

            <div className="allocation-table-wrapper">
              <table className="allocation-table">
                <thead>
                  <tr>
                    <th>Date</th>

                    <th>Voucher No</th>

                    <th>Voucher Type</th>

                    <th>Bill Type</th>

                    <th className="amount-column">Debit</th>

                    <th className="amount-column">Credit</th>
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

                        {/* DEBIT */}

                        <td className="amount-column">
                          {Number(allocation.signedAmount || 0) < 0
                            ? `₹ ${formatAmount(
                                Math.abs(allocation.signedAmount),
                              )}`
                            : "--"}
                        </td>

                        {/* CREDIT */}

                        <td className="amount-column">
                          {Number(allocation.signedAmount || 0) > 0
                            ? `₹ ${formatAmount(allocation.signedAmount)}`
                            : "--"}
                        </td>
                      </tr>
                    ))
                  ) : (
                    <tr>
                      <td colSpan="6" className="table-message">
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
      {previewModel && <OutstandingBillPreview model={previewModel} onClose={() => setPreviewModel(null)} />}
    </div>
  );
}

export default Outstanding;
