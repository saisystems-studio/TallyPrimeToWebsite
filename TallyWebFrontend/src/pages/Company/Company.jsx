import { useEffect, useState } from "react";
import { getDbCompanies } from "../../services/companyService";
import "./Company.css";

function Company() {
  const [companies, setCompanies] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    getDbCompanies()
      .then((data) => {
        setCompanies(data.companies || []);
      })
      .catch((err) => {
        console.error("COMPANY DB ERROR:", err);
        setError("Unable to load companies.");
      })
      .finally(() => {
        setLoading(false);
      });
  }, []);

  const showValue = (value) => {
    if (!value || String(value).trim() === "") return "--";
    return value;
  };

  const formatDate = (value) => {
    if (!value || String(value).length !== 8) return "--";

    const date = String(value);

    return `${date.substring(6, 8)}/${date.substring(
      4,
      6,
    )}/${date.substring(0, 4)}`;
  };

  if (loading) {
    return (
      <div className="company-page">
        <div className="company-message">Loading companies...</div>
      </div>
    );
  }

  return (
    <div className="company-page">
      <div className="company-header">
        <div>
          <span className="company-eyebrow">Company Master</span>
          <h1>Company</h1>
          <p>Companies synchronized from Tally Prime</p>
        </div>

        <div className="company-count">
          <span>Total Companies</span>
          <strong>{companies.length}</strong>
        </div>
      </div>

      {error && <div className="company-error">{error}</div>}

      {!error && companies.length === 0 && (
        <div className="company-message">No synchronized companies found.</div>
      )}

      {!error && companies.length > 0 && (
        <div className="company-grid">
          {companies.map((company) => (
            <div className="company-card" key={company.tallyGuid || company.id}>
              <div className="company-card-header">
                <div className="company-icon">C</div>

                <div>
                  <h2>{showValue(company.name)}</h2>
                  <span className="company-synced">● Synced from Tally</span>
                </div>
              </div>

              <div className="company-details">
                <div>
                  <span>Formal Name</span>
                  <strong>{showValue(company.formalName)}</strong>
                </div>

                <div>
                  <span>GSTIN</span>
                  <strong>{showValue(company.gstin)}</strong>
                </div>

                <div>
                  <span>State</span>
                  <strong>{showValue(company.state)}</strong>
                </div>

                <div>
                  <span>Country</span>
                  <strong>{showValue(company.country)}</strong>
                </div>

                <div>
                  <span>Financial Year From</span>
                  <strong>{formatDate(company.startingFrom)}</strong>
                </div>

                <div>
                  <span>Books From</span>
                  <strong>{formatDate(company.booksFrom)}</strong>
                </div>

                <div>
                  <span>Email</span>
                  <strong>{showValue(company.email)}</strong>
                </div>

                <div>
                  <span>Phone</span>
                  <strong>{showValue(company.phone)}</strong>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default Company;
