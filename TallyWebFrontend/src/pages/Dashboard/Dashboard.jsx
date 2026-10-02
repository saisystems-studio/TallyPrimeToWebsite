import { useEffect, useMemo, useState } from "react";

import { getDbCompanies } from "../../services/companyService";
import { getDbLedgers } from "../../services/tallyService";
import { getDbStockItems } from "../../services/stockItemDbService";
import { getDbVouchers } from "../../services/voucherDbService";
import { getDbOutstanding } from "../../services/outstandingDbService";

import "./Dashboard.css";

function Dashboard() {
  const [companies, setCompanies] = useState([]);
  const [selectedCompanyId, setSelectedCompanyId] = useState("");

  const [ledgers, setLedgers] = useState([]);
  const [stockItems, setStockItems] = useState([]);
  const [vouchers, setVouchers] = useState([]);
  const [outstanding, setOutstanding] = useState([]);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // =========================================================
  // LOAD COMPANIES
  // =========================================================

  useEffect(() => {
    async function loadCompanies() {
      try {
        setLoading(true);
        setError("");

        const data = await getDbCompanies();

        const companyList = Array.isArray(data)
          ? data
          : Array.isArray(data?.companies)
            ? data.companies
            : [];

        setCompanies(companyList);
      } catch (err) {
        console.error("Dashboard company error:", err);

        setCompanies([]);
        setError(err.message || "Unable to load companies.");
      } finally {
        setLoading(false);
      }
    }

    loadCompanies();
  }, []);

  // =========================================================
  // LOAD DASHBOARD DATA
  //
  // selectedCompanyId = ""
  // -> All Companies
  //
  // selectedCompanyId = company id
  // -> Selected company only
  // =========================================================

  useEffect(() => {
    if (companies.length === 0) {
      setLedgers([]);
      setStockItems([]);
      setVouchers([]);
      setOutstanding([]);
      return;
    }

    async function loadDashboardData() {
      try {
        setLoading(true);
        setError("");

        const targetCompanies = selectedCompanyId
          ? companies.filter(
              (company) => Number(company.id) === Number(selectedCompanyId),
            )
          : companies;

        // -----------------------------------------------------
        // LEDGERS
        // -----------------------------------------------------

        const ledgerResponses = await Promise.all(
          targetCompanies.map(async (company) => {
            const data = await getDbLedgers(company.id);

            const items = Array.isArray(data)
              ? data
              : Array.isArray(data?.ledgers)
                ? data.ledgers
                : [];

            return items.map((ledger) => ({
              ...ledger,
              companyId: company.id,
              companyName: company.name,
            }));
          }),
        );

        // -----------------------------------------------------
        // STOCK ITEMS
        // -----------------------------------------------------

        const stockResponses = await Promise.all(
          targetCompanies.map(async (company) => {
            const data = await getDbStockItems(company.id);

            const items = Array.isArray(data)
              ? data
              : Array.isArray(data?.stockItems)
                ? data.stockItems
                : Array.isArray(data?.items)
                  ? data.items
                  : [];

            return items.map((item) => ({
              ...item,
              companyId: company.id,
              companyName: company.name,
            }));
          }),
        );

        // -----------------------------------------------------
        // VOUCHERS
        // -----------------------------------------------------

        const voucherResponses = await Promise.all(
          targetCompanies.map(async (company) => {
            const data = await getDbVouchers(company.id);

            const items = Array.isArray(data)
              ? data
              : Array.isArray(data?.vouchers)
                ? data.vouchers
                : [];

            return items.map((voucher) => ({
              ...voucher,
              companyId: company.id,
              companyName: voucher.companyName || company.name,
            }));
          }),
        );

        // -----------------------------------------------------
        // OUTSTANDING
        //
        // Backend already supports:
        // ""  -> All Companies
        // id  -> Selected Company
        // -----------------------------------------------------

        const outstandingData = await getDbOutstanding(
          selectedCompanyId,
          "",
          true,
        );

        setLedgers(ledgerResponses.flat());
        setStockItems(stockResponses.flat());
        setVouchers(voucherResponses.flat());

        setOutstanding(Array.isArray(outstandingData) ? outstandingData : []);
      } catch (err) {
        console.error("Dashboard data error:", err);

        setLedgers([]);
        setStockItems([]);
        setVouchers([]);
        setOutstanding([]);

        setError(err.message || "Unable to load dashboard data.");
      } finally {
        setLoading(false);
      }
    }

    loadDashboardData();
  }, [companies, selectedCompanyId]);

  // =========================================================
  // SELECTED COMPANY
  // =========================================================

  const selectedCompany = useMemo(() => {
    if (!selectedCompanyId) {
      return null;
    }

    return (
      companies.find(
        (company) => Number(company.id) === Number(selectedCompanyId),
      ) || null
    );
  }, [companies, selectedCompanyId]);

  // =========================================================
  // OUTSTANDING SUMMARY
  // =========================================================

  const outstandingSummary = useMemo(() => {
    const receivableSigned = outstanding
      .filter((row) => row.balanceType === "Receivable")
      .reduce((total, row) => total + Number(row.signedPendingAmount || 0), 0);

    const payableSigned = outstanding
      .filter((row) => row.balanceType === "Payable")
      .reduce((total, row) => total + Number(row.signedPendingAmount || 0), 0);

    const receivable = Math.abs(Math.min(receivableSigned, 0));

    const payable = Math.abs(Math.max(payableSigned, 0));

    const pendingBills = outstanding.filter(
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
  }, [outstanding]);

  // =========================================================
  // COMPANY-WISE OUTSTANDING
  // =========================================================

  const companyOutstanding = useMemo(() => {
    return companies
      .filter((company) => {
        if (!selectedCompanyId) {
          return true;
        }

        return Number(company.id) === Number(selectedCompanyId);
      })
      .map((company) => {
        const companyRows = outstanding.filter(
          (row) => Number(row.companyId) === Number(company.id),
        );

        const receivableSigned = companyRows
          .filter((row) => row.balanceType === "Receivable")
          .reduce(
            (total, row) => total + Number(row.signedPendingAmount || 0),
            0,
          );

        const payableSigned = companyRows
          .filter((row) => row.balanceType === "Payable")
          .reduce(
            (total, row) => total + Number(row.signedPendingAmount || 0),
            0,
          );

        const pendingBills = companyRows.filter(
          (row) =>
            row.rowType === "Bill" &&
            row.status === "Pending" &&
            Number(row.signedPendingAmount || 0) !== 0,
        ).length;

        return {
          id: company.id,
          name: company.name,

          receivable: Math.abs(Math.min(receivableSigned, 0)),

          payable: Math.abs(Math.max(payableSigned, 0)),

          pendingBills,
        };
      });
  }, [companies, outstanding, selectedCompanyId]);

  // =========================================================
  // RECENT VOUCHERS
  // =========================================================

  const recentVouchers = useMemo(() => {
    return [...vouchers]
      .sort((a, b) => {
        const dateA = String(a.voucherDate || a.date || "");

        const dateB = String(b.voucherDate || b.date || "");

        return dateB.localeCompare(dateA);
      })
      .slice(0, 5);
  }, [vouchers]);

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
    if (!value) {
      return "--";
    }

    const clean = String(value);

    if (clean.length !== 8) {
      return clean;
    }

    const year = clean.substring(0, 4);
    const month = clean.substring(4, 6);
    const day = clean.substring(6, 8);

    return `${day}/${month}/${year}`;
  }

  // =========================================================
  // UI
  // =========================================================

  return (
    <div className="dashboard-page">
      {/* =====================================================
          TOP BAR
          ===================================================== */}

      <section className="companyBar">
        <div className="dashboard-company-info">
          <span className="companyLabel">DASHBOARD</span>

          <h2>{selectedCompany ? selectedCompany.name : "All Companies"}</h2>

          <p>Live business summary from synced Tally data</p>
        </div>

        <div className="dashboard-top-actions">
          <div className="dashboard-company-select">
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

          <div className="tallyStatus">
            <span className="statusDot"></span>

            <div>
              <strong>Auto Sync Active</strong>

              <small>Tally → SQL</small>
            </div>
          </div>
        </div>
      </section>

      {/* ERROR */}

      {error && <div className="dashboard-error">{error}</div>}

      {/* =====================================================
          PRIMARY STATS
          ===================================================== */}

      <section className="statsGrid">
        <div className="statCard">
          <span>COMPANIES</span>

          <h3>{loading ? "--" : selectedCompanyId ? 1 : companies.length}</h3>

          <p>
            {selectedCompanyId ? "Selected company" : "Available companies"}
          </p>
        </div>

        <div className="statCard">
          <span>LEDGERS</span>

          <h3>{loading ? "--" : ledgers.length}</h3>

          <p>Available ledgers</p>
        </div>

        <div className="statCard">
          <span>STOCK ITEMS</span>

          <h3>{loading ? "--" : stockItems.length}</h3>

          <p>Inventory items</p>
        </div>

        <div className="statCard">
          <span>VOUCHERS</span>

          <h3>{loading ? "--" : vouchers.length}</h3>

          <p>Total transactions</p>
        </div>
      </section>

      {/* =====================================================
          OUTSTANDING CARDS
          ===================================================== */}

      <section className="dashboard-finance-grid">
        <div className="finance-card">
          <div>
            <span>TOTAL RECEIVABLE</span>

            <h3>₹ {formatAmount(outstandingSummary.receivable)}</h3>
          </div>

          <p>Amount to receive</p>
        </div>

        <div className="finance-card">
          <div>
            <span>TOTAL PAYABLE</span>

            <h3>₹ {formatAmount(outstandingSummary.payable)}</h3>
          </div>

          <p>Amount to pay</p>
        </div>

        <div className="finance-card">
          <div>
            <span>PENDING BILLS</span>

            <h3>{outstandingSummary.pendingBills}</h3>
          </div>

          <p>Open bill references</p>
        </div>
      </section>

      {/* =====================================================
          LOWER SECTION
          ===================================================== */}

      <section className="dashboard-content-grid">
        {/* RECENT VOUCHERS */}

        <div className="dashboard-panel">
          <div className="dashboard-panel-header">
            <div>
              <h3>Recent Vouchers</h3>

              <p>Latest synced transactions</p>
            </div>
          </div>

          <div className="dashboard-table-wrapper">
            <table className="dashboard-table">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Company</th>
                  <th>Voucher</th>
                  <th>Party</th>
                  <th className="dashboard-amount">Amount</th>
                </tr>
              </thead>

              <tbody>
                {loading ? (
                  <tr>
                    <td colSpan="5" className="dashboard-empty">
                      Loading...
                    </td>
                  </tr>
                ) : recentVouchers.length === 0 ? (
                  <tr>
                    <td colSpan="5" className="dashboard-empty">
                      No vouchers available.
                    </td>
                  </tr>
                ) : (
                  recentVouchers.map((voucher, index) => (
                    <tr key={voucher.id || voucher.tallyGuid || index}>
                      <td>{formatDate(voucher.voucherDate || voucher.date)}</td>

                      <td>{voucher.companyName || "--"}</td>

                      <td>
                        {voucher.voucherType || voucher.voucherTypeName || "--"}
                      </td>

                      <td>
                        {voucher.partyName || voucher.partyLedgerName || "--"}
                      </td>

                      <td className="dashboard-amount">
                        ₹ {formatAmount(voucher.amount)}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>

        {/* COMPANY OUTSTANDING */}

        <div className="dashboard-panel">
          <div className="dashboard-panel-header">
            <div>
              <h3>Outstanding Summary</h3>

              <p>Company-wise pending position</p>
            </div>
          </div>

          <div className="dashboard-table-wrapper">
            <table className="dashboard-table">
              <thead>
                <tr>
                  <th>Company</th>

                  <th className="dashboard-amount">Receivable</th>

                  <th className="dashboard-amount">Payable</th>

                  <th className="dashboard-count">Bills</th>
                </tr>
              </thead>

              <tbody>
                {loading ? (
                  <tr>
                    <td colSpan="4" className="dashboard-empty">
                      Loading...
                    </td>
                  </tr>
                ) : companyOutstanding.length === 0 ? (
                  <tr>
                    <td colSpan="4" className="dashboard-empty">
                      No outstanding data.
                    </td>
                  </tr>
                ) : (
                  companyOutstanding.map((company) => (
                    <tr key={company.id}>
                      <td className="dashboard-company-name">{company.name}</td>

                      <td className="dashboard-amount receivable-value">
                        ₹ {formatAmount(company.receivable)}
                      </td>

                      <td className="dashboard-amount payable-value">
                        ₹ {formatAmount(company.payable)}
                      </td>

                      <td className="dashboard-count">
                        {company.pendingBills}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      </section>
    </div>
  );
}

export default Dashboard;
