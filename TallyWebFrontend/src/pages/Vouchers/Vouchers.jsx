import { useEffect, useState } from "react";

import { getVoucherDetail } from "../../services/voucherService";
import { getDbVouchers } from "../../services/voucherDbService";
import { getDbCompanies } from "../../services/companyService";

import "./Vouchers.css";

function Vouchers() {
  const [companies, setCompanies] = useState([]);
  const [selectedCompanyId, setSelectedCompanyId] = useState("");

  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");

  const [vouchers, setVouchers] = useState([]);
  const [voucherType, setVoucherType] = useState("All");

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const [selectedVoucher, setSelectedVoucher] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState("");

  // ==========================================
  // DATE HELPERS
  // ==========================================

  function toTallyDate(date) {
    return date ? date.replaceAll("-", "") : "";
  }

  function toInputDate(value) {
    if (!value || String(value).length !== 8) {
      return "";
    }

    const date = String(value);

    return `${date.substring(0, 4)}-${date.substring(
      4,
      6,
    )}-${date.substring(6, 8)}`;
  }

  function formatDate(value) {
    if (!value || String(value).length !== 8) {
      return showValue(value);
    }

    const date = String(value);

    return `${date.substring(6, 8)}-${date.substring(
      4,
      6,
    )}-${date.substring(0, 4)}`;
  }

  function showValue(value) {
    if (value === null || value === undefined || String(value).trim() === "") {
      return "--";
    }

    return value;
  }

  // ==========================================
  // COMPANY HELPERS
  // ==========================================

  function getSelectedCompany() {
    return companies.find(
      (company) => String(company.id) === String(selectedCompanyId),
    );
  }

  function applyCompanyFinancialYear(company) {
    if (!company?.startingFrom) {
      setFromDate("");
      setToDate("");
      return;
    }

    const startDate = toInputDate(company.startingFrom);

    if (!startDate) {
      setFromDate("");
      setToDate("");
      return;
    }

    setFromDate(startDate);

    const [year, month, day] = startDate.split("-").map(Number);

    const endDate = new Date(year + 1, month - 1, day);

    endDate.setDate(endDate.getDate() - 1);

    const endYear = endDate.getFullYear();

    const endMonth = String(endDate.getMonth() + 1).padStart(2, "0");

    const endDay = String(endDate.getDate()).padStart(2, "0");

    setToDate(`${endYear}-${endMonth}-${endDay}`);
  }

  // ==========================================
  // LOAD COMPANIES
  // ==========================================

  useEffect(() => {
    async function loadCompanies() {
      try {
        setError("");

        const data = await getDbCompanies();

        const companyList = Array.isArray(data) ? data : data.companies || [];

        setCompanies(companyList);

        if (companyList.length > 0) {
          const firstCompany = companyList[0];

          setSelectedCompanyId(String(firstCompany.id));

          applyCompanyFinancialYear(firstCompany);
        }
      } catch (err) {
        console.error("COMPANY ERROR:", err);

        setError("Unable to load companies from database.");
      }
    }

    loadCompanies();
  }, []);

  // ==========================================
  // COMPANY CHANGE
  // ==========================================

  function handleCompanyChange(event) {
    const companyId = event.target.value;

    setSelectedCompanyId(companyId);

    setVouchers([]);
    setVoucherType("All");

    setSelectedVoucher(null);
    setDetailError("");

    setError("");

    const company = companies.find(
      (item) => String(item.id) === String(companyId),
    );

    applyCompanyFinancialYear(company);
  }

  // ==========================================
  // LOAD VOUCHERS FROM SQL DATABASE
  // ==========================================

  async function loadVouchers() {
    if (!selectedCompanyId) {
      setError("Please select a company.");
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

      const data = await getDbVouchers(
        selectedCompanyId,
        toTallyDate(fromDate),
        toTallyDate(toDate),
        voucherType,
      );

      const voucherList = (data.vouchers || []).map((voucher) => ({
        ...voucher,

        // SQL API returns tallyGuid.
        // Existing detail modal uses guid.
        guid: voucher.guid || voucher.tallyGuid || "",
      }));

      setVouchers(voucherList);
    } catch (err) {
      console.error("VOUCHER DB ERROR:", err);

      setVouchers([]);

      setError("Unable to load vouchers from database.");
    } finally {
      setLoading(false);
    }
  }

  // ==========================================
  // VOUCHER DETAIL
  // ==========================================

  async function openVoucherDetail(voucher) {
    const guid = voucher.guid || voucher.tallyGuid;

    if (!guid) {
      setSelectedVoucher(voucher);

      setDetailError("Voucher GUID is not available.");

      return;
    }

    const company = getSelectedCompany();

    if (!company?.name) {
      setSelectedVoucher(voucher);

      setDetailError("Selected company is not available.");

      return;
    }

    try {
      setSelectedVoucher(voucher);

      setDetailLoading(true);
      setDetailError("");

      const data = await getVoucherDetail(
        guid,
        toTallyDate(fromDate),
        toTallyDate(toDate),
        company.name,
      );

      setSelectedVoucher(data.voucher || voucher);
    } catch (err) {
      console.error("VOUCHER DETAIL ERROR:", err);

      setDetailError("Unable to load voucher details from Tally.");
    } finally {
      setDetailLoading(false);
    }
  }

  function closeVoucherDetail() {
    setSelectedVoucher(null);
    setDetailError("");
    setDetailLoading(false);
  }

  // ==========================================
  // CLIENT FILTER
  // ==========================================

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
      {/* HEADER */}

      <div className="vouchers-header">
        <div>
          <h2>Vouchers</h2>

          <p>View company-wise voucher transactions</p>
        </div>
      </div>

      {/* TOOLBAR */}

      <div className="voucher-toolbar">
        <div className="voucher-filter">
          {/* COMPANY */}

          <div>
            <label htmlFor="voucher-company">Company</label>

            <select
              id="voucher-company"
              value={selectedCompanyId}
              onChange={handleCompanyChange}
              disabled={companies.length === 0}
            >
              {companies.length === 0 && (
                <option value="">No companies available</option>
              )}

              {companies.map((company) => (
                <option key={company.id} value={company.id}>
                  {company.name}
                </option>
              ))}
            </select>
          </div>

          {/* FROM DATE */}

          <div>
            <label htmlFor="voucher-from-date">From date</label>

            <input
              id="voucher-from-date"
              type="date"
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
            />
          </div>

          {/* TO DATE */}

          <div>
            <label htmlFor="voucher-to-date">To date</label>

            <input
              id="voucher-to-date"
              type="date"
              value={toDate}
              onChange={(e) => setToDate(e.target.value)}
            />
          </div>

          {/* VOUCHER TYPE */}

          <div>
            <label htmlFor="voucher-type">Voucher type</label>

            <select
              id="voucher-type"
              value={voucherType}
              onChange={(e) => setVoucherType(e.target.value)}
            >
              <option value="All">All Voucher Types</option>

              <option value="Sales">Sales</option>

              <option value="Purchase">Purchase</option>

              <option value="Receipt">Receipt</option>

              <option value="Payment">Payment</option>

              <option value="Journal">Journal</option>
            </select>
          </div>

          {/* LOAD */}

          <button
            type="button"
            onClick={loadVouchers}
            disabled={loading || !selectedCompanyId}
          >
            {loading ? "Loading..." : "Load vouchers"}
          </button>
        </div>

        <div className="voucher-count" aria-live="polite">
          Total vouchers <strong>{filteredVouchers.length}</strong>
        </div>
      </div>

      {/* VOUCHER TABLE */}

      <div className="vouchers-card">
        {loading && vouchers.length === 0 && (
          <div className="voucher-message">Loading vouchers...</div>
        )}

        {!loading && error && vouchers.length === 0 && (
          <div className="voucher-error">{error}</div>
        )}

        {!loading && !error && vouchers.length === 0 && (
          <div className="voucher-message">
            Select company and date range, then click Load Vouchers.
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
                    key={
                      voucher.tallyGuid || voucher.guid || voucher.id || index
                    }
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

      {/* VOUCHER DETAIL MODAL */}

      {selectedVoucher && (
        <div className="voucher-modal-overlay" onClick={closeVoucherDetail}>
          <div
            className="voucher-detail-modal"
            onClick={(e) => e.stopPropagation()}
          >
            {/* DETAIL HEADER */}

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

            {/* DETAIL LOADING */}

            {detailLoading && (
              <div className="voucher-detail-loading">
                Loading voucher details from Tally...
              </div>
            )}

            {/* DETAIL ERROR */}

            {!detailLoading && detailError && (
              <div className="voucher-error">{detailError}</div>
            )}

            {/* DETAIL BODY */}

            {!detailLoading && !detailError && (
              <div className="voucher-detail-body">
                {/* BASIC DETAILS */}

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

                {/* ITEM DETAILS */}

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

                {/* LEDGER DETAILS */}

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

                {/* NARRATION */}

                <div className="voucher-detail-section">
                  <h4>Narration</h4>

                  <div className="voucher-narration">
                    {showValue(selectedVoucher.narration)}
                  </div>
                </div>

                {/* TOTAL */}

                <div className="voucher-detail-total">
                  <span>Total Amount</span>

                  <strong>
                    {showValue(
                      selectedVoucher.totalAmount ?? selectedVoucher.amount,
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
