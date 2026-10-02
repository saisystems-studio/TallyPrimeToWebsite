import { useEffect, useState } from "react";
import { getDbLedgers } from "../../services/tallyService";
import { getDbCompanies } from "../../services/companyService";
import "./Ledgers.css";

function Ledgers() {
  const [companies, setCompanies] = useState([]);
  const [selectedCompanyId, setSelectedCompanyId] = useState("");
  const [ledgers, setLedgers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  // Load companies from database
  useEffect(() => {
    const loadCompanies = async () => {
      try {
        setLoading(true);
        setError("");

        const data = await getDbCompanies();
        const companyList = data.companies || [];

        setCompanies(companyList);

        if (companyList.length > 0) {
          setSelectedCompanyId(companyList[0].id);
        } else {
          setLoading(false);
        }
      } catch (err) {
        console.error("Company Error:", err);
        setError(err.message || "Unable to load companies.");
        setLoading(false);
      }
    };

    loadCompanies();
  }, []);

  // Load ledgers for selected company
  useEffect(() => {
    if (!selectedCompanyId) {
      return;
    }

    const loadLedgers = async () => {
      try {
        setLoading(true);
        setError("");

        const data = await getDbLedgers(selectedCompanyId);
        setLedgers(data.ledgers || []);
      } catch (err) {
        console.error("Ledger Error:", err);
        setLedgers([]);
        setError(err.message || "Unable to load ledgers.");
      } finally {
        setLoading(false);
      }
    };

    loadLedgers();
  }, [selectedCompanyId]);

  return (
    <div className="ledgers-page">
      <div className="ledgers-header">
        <div>
          <span className="ledgers-eyebrow">Accounts master</span>
          <h1>Ledgers</h1>
          <p>View company-wise account masters and balances</p>
        </div>

        <div className="ledger-count">
          <span>Total ledgers</span>
          <strong>{ledgers.length}</strong>
        </div>
      </div>

      <div className="ledger-company-filter">
        <label htmlFor="companySelect">Company</label>

        <select
          id="companySelect"
          value={selectedCompanyId}
          onChange={(e) => setSelectedCompanyId(Number(e.target.value))}
          disabled={companies.length === 0}
        >
          {companies.length === 0 ? (
            <option value="">No companies available</option>
          ) : (
            companies.map((company) => (
              <option key={company.id} value={company.id}>
                {company.name}
              </option>
            ))
          )}
        </select>
      </div>

      {loading && <div className="ledger-message">Loading ledgers...</div>}

      {error && <div className="ledger-error">{error}</div>}

      {!loading && !error && (
        <div className="ledger-table-wrapper">
          <table className="ledger-table">
            <thead>
              <tr>
                <th>S.No</th>
                <th>Ledger Name</th>
                <th>Parent Group</th>
                <th>Alias</th>
                <th>Mailing Name</th>
                <th>State</th>
                <th>Country</th>
                <th>Pincode</th>
                <th>PAN</th>
                <th>GSTIN</th>
                <th>Registration Type</th>
                <th>Bill-wise</th>
                <th>Credit Period</th>
                <th>Opening Balance</th>
                <th>Closing Balance</th>
              </tr>
            </thead>

            <tbody>
              {ledgers.length === 0 ? (
                <tr>
                  <td colSpan="15" className="no-ledgers">
                    No ledgers found for this company.
                  </td>
                </tr>
              ) : (
                ledgers.map((ledger, index) => (
                  <tr key={ledger.id || `${ledger.name}-${index}`}>
                    <td>{index + 1}</td>
                    <td className="ledger-name">{ledger.name || "--"}</td>
                    <td>{ledger.parent || "--"}</td>
                    <td>{ledger.alias || "--"}</td>
                    <td>{ledger.mailingName || "--"}</td>
                    <td>{ledger.state || "--"}</td>
                    <td>{ledger.country || "--"}</td>
                    <td>{ledger.pincode || "--"}</td>
                    <td>{ledger.pan || "--"}</td>
                    <td>{ledger.gstin || "--"}</td>
                    <td>{ledger.registrationType || "--"}</td>
                    <td>{ledger.billByBill || "--"}</td>
                    <td>{ledger.creditPeriod || "--"}</td>
                    <td className="ledger-number">
                      {ledger.openingBalance || "0"}
                    </td>
                    <td className="ledger-number">
                      {ledger.closingBalance || "0"}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

export default Ledgers;
