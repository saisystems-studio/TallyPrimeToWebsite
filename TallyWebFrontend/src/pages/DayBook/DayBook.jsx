import { useEffect, useMemo, useRef, useState } from "react";
import { getDbCompanies } from "../../services/companyService";
import { getDayBook } from "../../services/dayBookService";
import { getVoucherDetail } from "../../services/voucherService";
import { prepareInvoiceDocument } from "../Vouchers/invoiceData";
import { invoicePdf, invoicePdfFilename, shareInvoicePdf } from "../Vouchers/invoiceRenderer";
import InvoicePreview from "../Vouchers/InvoicePreview";
import { getDayBookInvoiceContext } from "./dayBookInvoice";
import "./DayBook.css";

function DayBook() {
  const [companies, setCompanies] = useState([]);
  const [companyId, setCompanyId] = useState("");

  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [voucherType, setVoucherType] = useState("All");

  const [vouchers, setVouchers] = useState([]);
  const [voucherTypes, setVoucherTypes] = useState([]);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  // Voucher detail modal
  const [selectedVoucher, setSelectedVoucher] = useState(null);
  const [selectedRow, setSelectedRow] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState("");
  const [invoicePreview, setInvoicePreview] = useState(null);
  const [invoiceActionError, setInvoiceActionError] = useState("");
  const [invoiceActionStatus, setInvoiceActionStatus] = useState("");
  const [sharingPdf, setSharingPdf] = useState(false);
  const detailRequestId = useRef(0);
  const shareRequestId = useRef(0);

  // ---------------------------------------------------------
  // Companies
  // ---------------------------------------------------------

  useEffect(() => {
    const loadCompanies = async () => {
      try {
        const data = await getDbCompanies();
        setCompanies(data.companies || []);
      } catch (err) {
        console.error("Day Book Company Error:", err);

        setError(err.message || "Unable to load companies.");
      }
    };

    loadCompanies();
  }, []);

  // ---------------------------------------------------------
  // Day Book
  // ---------------------------------------------------------

  const loadDayBook = async () => {
    try {
      setLoading(true);
      setError("");

      const data = await getDayBook({
        companyId,

        fromDate: fromDate ? fromDate.replaceAll("-", "") : "",

        toDate: toDate ? toDate.replaceAll("-", "") : "",

        voucherType,
      });

      setVouchers(data.vouchers || []);
      setVoucherTypes(data.voucherTypes || []);
    } catch (err) {
      console.error("Day Book Error:", err);

      setVouchers([]);

      setError(err.message || "Unable to load Day Book.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadDayBook();
  }, []);

  // ---------------------------------------------------------
  // Company change
  // ---------------------------------------------------------

  const handleCompanyChange = async (event) => {
    const value = event.target.value;

    setCompanyId(value);

    try {
      setLoading(true);
      setError("");

      const data = await getDayBook({
        companyId: value,

        fromDate: fromDate ? fromDate.replaceAll("-", "") : "",

        toDate: toDate ? toDate.replaceAll("-", "") : "",

        voucherType,
      });

      setVouchers(data.vouchers || []);
      setVoucherTypes(data.voucherTypes || []);
    } catch (err) {
      console.error("Day Book Company Filter Error:", err);

      setVouchers([]);

      setError(err.message || "Unable to load Day Book.");
    } finally {
      setLoading(false);
    }
  };

  // ---------------------------------------------------------
  // Voucher Detail
  // ---------------------------------------------------------

  const handleVoucherClick = async (voucher) => {
    const requestId = ++detailRequestId.current;
    try {
      setSelectedRow(voucher);
      setSelectedVoucher(null);
      setInvoicePreview(null);
      setInvoiceActionError("");
      setInvoiceActionStatus("");
      setDetailLoading(true);
      setDetailError("");

      if (!voucher.tallyGuid || !voucher.companyName || !voucher.companyId) {
        throw new Error("This voucher is missing its company or Tally identity.");
      }

      const data = await getVoucherDetail(
        voucher.tallyGuid,
        voucher.date,
        voucher.date,
        voucher.companyName,
      );

      if (requestId !== detailRequestId.current) return;
      const detail = data?.voucher;
      if (!detail?.guid || detail.guid.trim().toLowerCase() !== voucher.tallyGuid.trim().toLowerCase()) {
        throw new Error("The returned voucher did not match the selected Day Book entry.");
      }
      setSelectedVoucher({ ...detail, companyId: voucher.companyId });
    } catch (err) {
      if (requestId !== detailRequestId.current) return;
      console.error("Voucher Detail Error:", err);

      setSelectedVoucher(null);

      setDetailError(err.message || "Unable to load voucher details.");
    } finally {
      if (requestId === detailRequestId.current) setDetailLoading(false);
    }
  };

  const closeVoucherModal = () => {
    ++detailRequestId.current;
    ++shareRequestId.current;
    setSelectedRow(null);
    setSelectedVoucher(null);
    setInvoicePreview(null);
    setInvoiceActionError("");
    setInvoiceActionStatus("");
    setSharingPdf(false);
    setDetailLoading(false);
    setDetailError("");
  };

  const openInvoicePreview = () => {
    try {
      const context = getDayBookInvoiceContext(selectedRow, selectedVoucher, companies);
      setInvoiceActionError("");
      setInvoiceActionStatus("");
      setInvoicePreview(context);
    } catch (error) {
      setInvoiceActionError(error.message || "Unable to prepare this voucher.");
    }
  };

  const handleSharePdf = async () => {
    let requestId;
    try {
      const context = getDayBookInvoiceContext(selectedRow, selectedVoucher, companies);
      requestId = ++shareRequestId.current;
      setInvoiceActionError("");
      setInvoiceActionStatus("Preparing the selected voucher PDF...");
      setSharingPdf(true);
      const prepared = await prepareInvoiceDocument(context.company, context.voucher, undefined, { includeTaxLedgerEntries: true });
      const file = new File([invoicePdf(prepared.canvases)], invoicePdfFilename(prepared.model), { type: "application/pdf" });
      let fallbackUsed = false;
      const result = await shareInvoicePdf(file, {
        fallbackDownload: sharedFile => {
          fallbackUsed = true;
          const url = URL.createObjectURL(sharedFile);
          const link = document.createElement("a");
          link.href = url;
          link.download = sharedFile.name;
          document.body.append(link);
          link.click();
          link.remove();
          setTimeout(() => URL.revokeObjectURL(url), 60000);
        },
      });
      if (requestId !== shareRequestId.current) return;
      if (result === "shared") setInvoiceActionStatus("The selected voucher PDF was shared.");
      else if (result === "cancelled") setInvoiceActionStatus("Sharing was cancelled. The selected voucher PDF is ready to export.");
      else if (fallbackUsed) setInvoiceActionStatus("Native PDF sharing is unavailable. The selected voucher PDF was downloaded instead.");
    } catch (error) {
      if (!requestId || requestId === shareRequestId.current) {
        setInvoiceActionError(error.message || "Unable to share or download the selected voucher PDF.");
        setInvoiceActionStatus("");
      }
    } finally {
      if (!requestId || requestId === shareRequestId.current) setSharingPdf(false);
    }
  };

  // ---------------------------------------------------------
  // Total
  // ---------------------------------------------------------

  const totalAmount = useMemo(() => {
    return vouchers.reduce(
      (total, voucher) => total + Math.abs(Number(voucher.amount || 0)),
      0,
    );
  }, [vouchers]);

  // ---------------------------------------------------------
  // Helpers
  // ---------------------------------------------------------

  const formatAmount = (value) => {
    return Number(value || 0).toLocaleString("en-IN", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    });
  };

  const formatDate = (value) => {
    if (!value || value.length !== 8) {
      return value || "--";
    }

    return `${value.slice(6, 8)}/${value.slice(4, 6)}/${value.slice(0, 4)}`;
  };

  const debitAmount = (value) => {
    const amount = Number(value || 0);

    if (amount >= 0) {
      return "--";
    }

    return `₹ ${formatAmount(Math.abs(amount))}`;
  };

  const creditAmount = (value) => {
    const amount = Number(value || 0);

    if (amount <= 0) {
      return "--";
    }

    return `₹ ${formatAmount(amount)}`;
  };

  return (
    <div className="daybook-page">
      {/* HEADER */}

      <div className="daybook-header">
        <div>
          <span className="daybook-eyebrow">Transactions</span>

          <h1>Day Book</h1>

          <p>View synced voucher transactionxs date-wise</p>
        </div>

        <div className="daybook-count">
          <span>Total Records</span>
          <strong>{vouchers.length}</strong>
        </div>
      </div>

      {/* FILTERS */}

      <div className="daybook-filters">
        <div className="daybook-field">
          <label>Company</label>

          <select value={companyId} onChange={handleCompanyChange}>
            <option value="">All Companies</option>

            {companies.map((company) => (
              <option key={company.id} value={company.id}>
                {company.name}
              </option>
            ))}
          </select>
        </div>

        <div className="daybook-field">
          <label>From Date</label>

          <input
            type="date"
            value={fromDate}
            onChange={(e) => setFromDate(e.target.value)}
          />
        </div>

        <div className="daybook-field">
          <label>To Date</label>

          <input
            type="date"
            value={toDate}
            onChange={(e) => setToDate(e.target.value)}
          />
        </div>

        <div className="daybook-field">
          <label>Voucher Type</label>

          <select
            value={voucherType}
            onChange={(e) => setVoucherType(e.target.value)}
          >
            <option value="All">All</option>

            {voucherTypes.map((type) => (
              <option key={type} value={type}>
                {type}
              </option>
            ))}
          </select>
        </div>

        <button
          className="daybook-load-button"
          onClick={loadDayBook}
          disabled={loading}
        >
          {loading ? "Loading..." : "Load"}
        </button>
      </div>

      {/* ERROR */}

      {error && <div className="daybook-error">{error}</div>}

      {/* TABLE */}

      <div className="daybook-table-card">
        <div className="daybook-table-wrapper">
          <table className="daybook-table">
            <thead>
              <tr>
                <th>S.No</th>
                <th>Date</th>
                <th>Company</th>
                <th>Particulars</th>
                <th>Voucher Type</th>
                <th>Voucher No</th>
                <th className="amount-column">Amount</th>
              </tr>
            </thead>

            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="7" className="daybook-empty">
                    Loading Day Book...
                  </td>
                </tr>
              ) : vouchers.length === 0 ? (
                <tr>
                  <td colSpan="7" className="daybook-empty">
                    No voucher transactions found.
                  </td>
                </tr>
              ) : (
                vouchers.map((voucher, index) => (
                  <tr
                    key={voucher.id}
                    className="daybook-clickable-row"
                    onClick={() => handleVoucherClick(voucher)}
                    title="Click to view voucher details"
                  >
                    <td>{index + 1}</td>

                    <td>{formatDate(voucher.date)}</td>

                    <td>{voucher.companyName || "--"}</td>

                    <td className="daybook-party">
                      {voucher.particulars || "--"}
                    </td>

                    <td>{voucher.voucherType || "--"}</td>

                    <td>{voucher.voucherNumber || "--"}</td>

                    <td className="amount-column">
                      ₹ {formatAmount(voucher.amount)}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
        {vouchers.length > 0 && (
          <div className="daybook-table-footer">
            <span>Total</span>
            <strong>{`₹ ${formatAmount(totalAmount)}`}</strong>
          </div>
        )}
      </div>

      {/* =====================================================
          VOUCHER DETAIL MODAL
          ===================================================== */}

      {selectedRow && (
        <div className="daybook-modal-overlay" onClick={closeVoucherModal}>
          <div className="daybook-modal" onClick={(e) => e.stopPropagation()}>
            {/* MODAL HEADER */}

            <div className="daybook-modal-header">
              <div>
                <span>Voucher Details</span>

                <h2>
                  {selectedVoucher?.voucherType ||
                    selectedRow.voucherType ||
                    "Voucher"}
                </h2>

                <p>{selectedRow.companyName || "--"}</p>
              </div>

              <div className="daybook-modal-controls">
                <div className="daybook-modal-actions">
                  <button type="button" className="daybook-modal-action" onClick={openInvoicePreview} disabled={detailLoading || !selectedVoucher || Boolean(detailError) || sharingPdf}>Export PDF</button>
                  <button type="button" className="daybook-modal-action" onClick={handleSharePdf} disabled={detailLoading || !selectedVoucher || Boolean(detailError) || sharingPdf}>{sharingPdf ? "Preparing..." : "Share"}</button>
                </div>
                <button
                  type="button"
                  className="daybook-modal-close"
                  onClick={closeVoucherModal}
                  aria-label="Close"
                >
                  ×

                </button>
              </div>
            </div>

            {invoiceActionError && <div className="daybook-export-error" role="alert">{invoiceActionError}</div>}
            {invoiceActionStatus && <div className="daybook-export-status" role="status">{invoiceActionStatus}</div>}

            {invoicePreview && (
              <InvoicePreview
                key={`${invoicePreview.company.id}:${invoicePreview.voucher.guid}`}
                voucher={invoicePreview.voucher}
                company={invoicePreview.company}
                showTaxLedgerEntries
                onClose={() => setInvoicePreview(null)}
              />
            )}

            {/* LOADING */}

            {detailLoading && (
              <div className="daybook-modal-state">
                Loading voucher details...
              </div>
            )}

            {/* ERROR */}

            {!detailLoading && detailError && (
              <div className="daybook-modal-error">{detailError}</div>
            )}

            {/* DETAIL */}

            {!detailLoading && !detailError && selectedVoucher && (
              <div className="daybook-modal-body">
                {/* BASIC INFO */}

                <div className="daybook-detail-grid">
                  <div>
                    <span>Date</span>
                    <strong>{formatDate(selectedVoucher.date)}</strong>
                  </div>

                  <div>
                    <span>Voucher No</span>
                    <strong>{selectedVoucher.voucherNumber || "--"}</strong>
                  </div>

                  <div>
                    <span>Party</span>
                    <strong>{selectedVoucher.partyName || "--"}</strong>
                  </div>

                  <div>
                    <span>Reference</span>
                    <strong>{selectedVoucher.reference || "--"}</strong>
                  </div>

                  <div>
                    <span>GSTIN</span>
                    <strong>{selectedVoucher.partyGstin || "--"}</strong>
                  </div>

                  <div>
                    <span>State</span>
                    <strong>{selectedVoucher.state || "--"}</strong>
                  </div>

                  <div>
                    <span>Place of Supply</span>
                    <strong>{selectedVoucher.placeOfSupply || "--"}</strong>
                  </div>

                  <div>
                    <span>Total Amount</span>
                    <strong className="daybook-detail-total">
                      ₹ {formatAmount(selectedVoucher.totalAmount)}
                    </strong>
                  </div>
                </div>

                {/* ITEMS */}

                <div className="daybook-detail-section">
                  <div className="daybook-detail-title">
                    <h3>Items</h3>

                    <span>{selectedVoucher.items?.length || 0} item(s)</span>
                  </div>

                  <div className="daybook-detail-table-wrap">
                    <table className="daybook-detail-table">
                      <thead>
                        <tr>
                          <th>S.No</th>
                          <th>Item</th>
                          <th>Quantity</th>
                          <th>Rate</th>
                          <th className="detail-amount">Amount</th>
                        </tr>
                      </thead>

                      <tbody>
                        {selectedVoucher.items?.length > 0 ? (
                          selectedVoucher.items.map((item, index) => (
                            <tr key={`${item.itemName}-${index}`}>
                              <td>{index + 1}</td>

                              <td className="detail-name">
                                {item.itemName || "--"}
                              </td>

                              <td>{item.quantity || "--"}</td>

                              <td>{item.rate || "--"}</td>

                              <td className="detail-amount">
                                ₹ {formatAmount(item.amount)}
                              </td>
                            </tr>
                          ))
                        ) : (
                          <tr>
                            <td colSpan="5" className="detail-empty">
                              No item details.
                            </td>
                          </tr>
                        )}
                      </tbody>
                    </table>
                  </div>
                </div>

                {/* LEDGER ENTRIES */}

                <div className="daybook-detail-section">
                  <div className="daybook-detail-title">
                    <h3>Ledger Entries</h3>

                    <span>
                      {selectedVoucher.ledgerEntries?.length || 0} entry(s)
                    </span>
                  </div>

                  <div className="daybook-detail-table-wrap">
                    <table className="daybook-detail-table">
                      <thead>
                        <tr>
                          <th>S.No</th>
                          <th>Ledger</th>
                          <th className="detail-amount">Debit</th>
                          <th className="detail-amount">Credit</th>
                        </tr>
                      </thead>

                      <tbody>
                        {selectedVoucher.ledgerEntries?.length > 0 ? (
                          selectedVoucher.ledgerEntries.map((entry, index) => (
                            <tr key={`${entry.ledgerName}-${index}`}>
                              <td>{index + 1}</td>

                              <td className="detail-name">
                                {entry.ledgerName || "--"}
                              </td>

                              <td className="detail-amount debit-value">
                                {debitAmount(entry.amount)}
                              </td>

                              <td className="detail-amount credit-value">
                                {creditAmount(entry.amount)}
                              </td>
                            </tr>
                          ))
                        ) : (
                          <tr>
                            <td colSpan="4" className="detail-empty">
                              No ledger entries.
                            </td>
                          </tr>
                        )}
                      </tbody>
                    </table>
                  </div>
                </div>

                {/* NARRATION */}

                <div className="daybook-narration">
                  <span>Narration</span>

                  <p>{selectedVoucher.narration || "No narration"}</p>
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

export default DayBook;
