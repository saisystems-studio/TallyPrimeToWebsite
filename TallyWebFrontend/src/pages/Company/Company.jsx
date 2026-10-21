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
    if (!value) return "--";

    const date = String(value).trim();
    const compactDate = date.match(/^(\d{4})(\d{2})(\d{2})$/);
    const isoDate = date.match(/^(\d{4})-(\d{2})-(\d{2})/);
    const dayFirstDate = date.match(/^(\d{1,2})[/-](\d{1,2})[/-](\d{4})$/);

    if (compactDate || isoDate) {
      const [, year, month, day] = compactDate || isoDate;
      return `${day}/${month}/${year}`;
    }

    if (dayFirstDate) {
      const [, day, month, year] = dayFirstDate;
      return `${day.padStart(2, "0")}/${month.padStart(2, "0")}/${year}`;
    }

    return "--";
  };

  return (
    <div className="company-page" aria-busy={loading}>
      <div className="company-header">
        <div>
          <span className="company-eyebrow">Company Master</span>
          <h1>Companies</h1>
          <p>Companies synchronized from Tally Prime</p>
        </div>

        <div className="company-count">
          <span>Total Companies</span>
          <strong>{loading || error ? "--" : companies.length}</strong>
        </div>
      </div>

      {loading && (
        <div className="company-message" role="status">
          <span className="company-state-icon" aria-hidden="true">...</span>
          <h2>Loading companies</h2>
          <p>Getting your synchronized company details.</p>
        </div>
      )}

      {error && (
        <div className="company-error" role="alert">
          <span className="company-state-icon" aria-hidden="true">!</span>
          <h2>{error}</h2>
          <p>Please try again later.</p>
        </div>
      )}

      {!loading && !error && companies.length === 0 && (
        <div className="company-message" role="status">
          <span className="company-state-icon" aria-hidden="true">C</span>
          <h2>No companies yet</h2>
          <p>Your companies will appear here once synchronized from Tally Prime.</p>
        </div>
      )}

      {!loading && !error && companies.length > 0 && (
        <div className="company-list">
          <div className="company-list-heading" aria-hidden="true">
            <span>Company</span>
            <span>GSTIN</span>
            <span>Location</span>
            <span>Financial year</span>
          </div>
          {companies.map((company) => (
            <div className="company-row" key={company.tallyGuid || company.id}>
              <div className="company-row-summary">
                <div className="company-row-identity">
                  <div className="company-icon" aria-hidden="true">
                    <svg
                      width="20"
                      height="20"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="1.6"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      focusable="false"
                    >
                      <rect x="5" y="3" width="14" height="18" rx="2" />
                      <path d="M9 7h1m4 0h1M9 11h1m4 0h1M10 21v-6h4v6M3 21h18" />
                    </svg>
                  </div>
                  <div>
                    <h2>{showValue(company.name)}</h2>
                    <span className="company-synced">Synced from Tally</span>
                  </div>
                </div>
                <span className="company-row-value"><span className="company-mobile-label">GSTIN</span>{showValue(company.gstin)}</span>
                <span className="company-row-value"><span className="company-mobile-label">Location</span>{showValue(company.state)}</span>
                <span className="company-row-value">
                  <span className="company-mobile-label">Financial year</span>
                  {`${formatDate(company.startingFrom)} - ${formatDate(company.financialYearEnd)}`}
                </span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default Company;
