import { useState } from "react";
import { getVouchers, getVoucherDetail } from "../../services/voucherService";
import "./Vouchers.css";

function Vouchers() {
  const [fromDate, setFromDate] = useState("2024-04-01");
  const [toDate, setToDate] = useState("2024-04-30");

  const [vouchers, setVouchers] = useState([]);
  const [voucherType, setVoucherType] = useState("All");

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const [selectedVoucher, setSelectedVoucher] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState("");

  function toTallyDate(date) {
    return date.replaceAll("-", "");
  }

  async function loadVouchers() {
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

      const data = await getVouchers(
        toTallyDate(fromDate),
        toTallyDate(toDate),
      );

      setVouchers(data.vouchers || []);
      setVoucherType("All");
    } catch (err) {
      console.error("VOUCHER ERROR:", err);

      if (vouchers.length === 0) {
        setError("Unable to load vouchers from Tally.");
      }
    } finally {
      setLoading(false);
    }
  }

  async function openVoucherDetail(voucher) {
    if (!voucher.guid) {
      setDetailError("Voucher GUID is not available.");
      setSelectedVoucher(voucher);
      return;
    }

    try {
      setSelectedVoucher(voucher);
      setDetailLoading(true);
      setDetailError("");

      const data = await getVoucherDetail(
        voucher.guid,
        toTallyDate(fromDate),
        toTallyDate(toDate),
      );

      setSelectedVoucher(data.voucher || voucher);
    } catch (err) {
      console.error("VOUCHER DETAIL ERROR:", err);
      setDetailError("Unable to load voucher details.");
    } finally {
      setDetailLoading(false);
    }
  }

  function closeVoucherDetail() {
    setSelectedVoucher(null);
    setDetailError("");
    setDetailLoading(false);
  }

  function showValue(value) {
    if (value === null || value === undefined || String(value).trim() === "") {
      return "--";
    }

    return value;
  }

  function formatDate(value) {
    if (!value || value.length !== 8) {
      return showValue(value);
    }

    return `${value.substring(6, 8)}-${value.substring(
      4,
      6,
    )}-${value.substring(0, 4)}`;
  }

  const filteredVouchers =
    voucherType === "All"
      ? vouchers
      : vouchers.filter(
          (voucher) =>
            voucher.voucherType?.trim().toLowerCase() ===
            voucherType.toLowerCase(),
        );

  return (
    <div className="vouchers-page">
      <div className="vouchers-header">
        <div>
          <h2>Vouchers</h2>
          <p>View voucher transactions directly from Tally</p>
        </div>
      </div>

      <div className="voucher-toolbar">
        <div className="voucher-filter">
          <div>
            <label htmlFor="voucher-from-date">From date</label>
            <input
              id="voucher-from-date"
              type="date"
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
            />
          </div>

          <div>
            <label htmlFor="voucher-to-date">To date</label>
            <input
              id="voucher-to-date"
              type="date"
              value={toDate}
              onChange={(e) => setToDate(e.target.value)}
            />
          </div>

          <div>
            <label htmlFor="voucher-type">Voucher type</label>

            <select
              id="voucher-type"
              value={voucherType}
              onChange={(e) => setVoucherType(e.target.value)}
              disabled={vouchers.length === 0}
            >
              <option value="All">All Voucher Types</option>
              <option value="Sales">Sales</option>
              <option value="Purchase">Purchase</option>
              <option value="Receipt">Receipt</option>
              <option value="Payment">Payment</option>
              <option value="Journal">Journal</option>
            </select>
          </div>

          <button type="button" onClick={loadVouchers} disabled={loading}>
            {loading ? "Loading..." : "Load vouchers"}
          </button>
        </div>

        <div className="voucher-count" aria-live="polite">
          Total vouchers <strong>{filteredVouchers.length}</strong>
        </div>
      </div>

      <div className="vouchers-card">
        {loading && vouchers.length === 0 && (
          <div className="voucher-message">Loading vouchers from Tally...</div>
        )}

        {!loading && error && vouchers.length === 0 && (
          <div className="voucher-error">{error}</div>
        )}

        {!loading && !error && vouchers.length === 0 && (
          <div className="voucher-message">
            Select the date range and click Load Vouchers.
          </div>
        )}

        {vouchers.length > 0 && filteredVouchers.length === 0 && (
          <div className="voucher-message">
            No {voucherType} vouchers found.
          </div>
        )}

        {filteredVouchers.length > 0 && (
          <div className="voucher-table-wrapper">
            <table className="voucher-table">
              <thead>
                <tr>
                  <th>S.No</th>
                  <th>Date</th>
                  <th>Voucher No</th>
                  <th>Voucher Type</th>
                  <th>Party Name</th>
                  <th>GSTIN</th>
                  <th>State</th>
                  <th>Place of Supply</th>
                  <th>Voucher Amount</th>
                </tr>
              </thead>

              <tbody>
                {filteredVouchers.map((voucher, index) => (
                  <tr
                    key={voucher.guid || index}
                    className="voucher-clickable-row"
                    onClick={() => openVoucherDetail(voucher)}
                    title="Click to view voucher details"
                  >
                    <td>{index + 1}</td>
                    <td>{formatDate(voucher.date)}</td>
                    <td>{showValue(voucher.voucherNumber)}</td>
                    <td>{showValue(voucher.voucherType)}</td>
                    <td>{showValue(voucher.partyName)}</td>
                    <td>{showValue(voucher.partyGstin)}</td>
                    <td>{showValue(voucher.state)}</td>
                    <td>{showValue(voucher.placeOfSupply)}</td>
                    <td>{showValue(voucher.amount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {selectedVoucher && (
        <div className="voucher-modal-overlay" onClick={closeVoucherDetail}>
          <div
            className="voucher-detail-modal"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="voucher-detail-header">
              <div>
                <h3>Voucher Details</h3>

                <p>
                  {showValue(selectedVoucher.voucherType)} ·{" "}
                  {showValue(selectedVoucher.voucherNumber)}
                </p>
              </div>

              <button
                type="button"
                className="voucher-modal-close"
                onClick={closeVoucherDetail}
                aria-label="Close voucher details"
              >
                ×
              </button>
            </div>

            {detailLoading && (
              <div className="voucher-detail-loading">
                Loading voucher details from Tally...
              </div>
            )}

            {!detailLoading && detailError && (
              <div className="voucher-error">{detailError}</div>
            )}

            {!detailLoading && !detailError && (
              <div className="voucher-detail-body">
                <div className="voucher-detail-grid">
                  <div>
                    <span>Date</span>
                    <strong>{formatDate(selectedVoucher.date)}</strong>
                  </div>

                  <div>
                    <span>Voucher No</span>
                    <strong>{showValue(selectedVoucher.voucherNumber)}</strong>
                  </div>

                  <div>
                    <span>Voucher Type</span>
                    <strong>{showValue(selectedVoucher.voucherType)}</strong>
                  </div>

                  <div>
                    <span>Reference</span>
                    <strong>{showValue(selectedVoucher.reference)}</strong>
                  </div>

                  <div>
                    <span>Party Name</span>
                    <strong>{showValue(selectedVoucher.partyName)}</strong>
                  </div>

                  <div>
                    <span>GSTIN</span>
                    <strong>{showValue(selectedVoucher.partyGstin)}</strong>
                  </div>

                  <div>
                    <span>State</span>
                    <strong>{showValue(selectedVoucher.state)}</strong>
                  </div>

                  <div>
                    <span>Place of Supply</span>
                    <strong>{showValue(selectedVoucher.placeOfSupply)}</strong>
                  </div>
                </div>

                {selectedVoucher.items?.length > 0 && (
                  <div className="voucher-detail-section">
                    <h4>Item Details</h4>

                    <div className="voucher-detail-table-wrapper">
                      <table className="voucher-detail-table">
                        <thead>
                          <tr>
                            <th>Item Name</th>
                            <th>Quantity</th>
                            <th>Rate</th>
                            <th>Amount</th>
                          </tr>
                        </thead>

                        <tbody>
                          {selectedVoucher.items.map((item, index) => (
                            <tr key={`${item.itemName}-${index}`}>
                              <td>{showValue(item.itemName)}</td>
                              <td>{showValue(item.quantity)}</td>
                              <td>{showValue(item.rate)}</td>
                              <td>{showValue(item.amount)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                )}

                {selectedVoucher.ledgerEntries?.length > 0 && (
                  <div className="voucher-detail-section">
                    <h4>Ledger Details</h4>

                    <div className="voucher-detail-table-wrapper">
                      <table className="voucher-detail-table">
                        <thead>
                          <tr>
                            <th>Ledger Name</th>
                            <th>Amount</th>
                          </tr>
                        </thead>

                        <tbody>
                          {selectedVoucher.ledgerEntries.map((entry, index) => (
                            <tr key={`${entry.ledgerName}-${index}`}>
                              <td>{showValue(entry.ledgerName)}</td>
                              <td>{showValue(entry.amount)}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                )}

                <div className="voucher-detail-section">
                  <h4>Narration</h4>

                  <div className="voucher-narration">
                    {showValue(selectedVoucher.narration)}
                  </div>
                </div>

                <div className="voucher-detail-total">
                  <span>Total Amount</span>

                  <strong>
                    {showValue(
                      selectedVoucher.totalAmount || selectedVoucher.amount,
                    )}
                  </strong>
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

export default Vouchers;
