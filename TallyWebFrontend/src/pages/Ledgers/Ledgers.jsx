import { useEffect, useState } from "react";
import { getDbLedgers } from "../../services/tallyService";
import { getDbCompanies } from "../../services/companyService";
import "./Ledgers.css";

const ledgerColumns = [
  { key: "name", label: "Ledger Name", className: "ledger-name", minWidth: 180 },
  { key: "parent", label: "Group", minWidth: 130 },
  { key: "alias", label: "Alias", optional: true, minWidth: 120 },
  { key: "mailingName", label: "Mailing Name", minWidth: 180 },
  { key: "state", label: "State", optional: true, minWidth: 115 },
  { key: "country", label: "Country", optional: true, minWidth: 110 },
  { key: "pincode", label: "Pincode", optional: true, minWidth: 90 },
  { key: "pan", label: "PAN Number", optional: true, minWidth: 125 },
  { key: "registrationType", label: "Registration Type", optional: true, minWidth: 155 },
  { key: "gstin", label: "GSTIN", minWidth: 140 },
  { key: "billByBill", label: "Bill-wise", optional: true, minWidth: 105 },
  { key: "creditPeriod", label: "Credit Period", optional: true, minWidth: 115 },
  { key: "openingBalance", label: "Opening Balance", className: "ledger-number", minWidth: 135 },
];

function Ledgers() {
  const [selectedColumns, setSelectedColumns] = useState([]);
  const [pendingColumns, setPendingColumns] = useState([]);
  const [companies, setCompanies] = useState([]);
  const [selectedCompanyId, setSelectedCompanyId] = useState("");
  const [ledgers, setLedgers] = useState([]);
  const [selectedGroup, setSelectedGroup] = useState("");
  const [ledgerSearch, setLedgerSearch] = useState("");
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

  const groups = [...new Set(ledgers.map((ledger) => ledger.parent).filter(Boolean))]
    .sort((a, b) => a.localeCompare(b));
  const normalizedLedgerSearch = ledgerSearch.trim().toLocaleLowerCase();
  const filteredLedgers = ledgers.filter((ledger) => {
    const matchesGroup = !selectedGroup || ledger.parent === selectedGroup;
    const ledgerName = String(ledger.name || "").toLocaleLowerCase();
    const matchesName = !normalizedLedgerSearch || ledgerName.includes(normalizedLedgerSearch);
    return matchesGroup && matchesName;
  });
  const visibleColumns = ledgerColumns.filter(
    (column) => !column.optional || selectedColumns.includes(column.key),
  );

  return (
    <div className="ledgers-page">
      <div className="ledgers-header">
        <div>
          <span className="ledgers-eyebrow">Accounts master</span>
          <h1>Ledgers</h1>
          <p>View company-wise account masters and balances</p>
        </div>

        <div className="ledger-count">
          <span>{selectedGroup || normalizedLedgerSearch ? "Matching ledgers" : "Total ledgers"}</span>
          <strong>{filteredLedgers.length}</strong>
        </div>
      </div>

      <div className="ledger-toolbar">
        <div className="ledger-company-filter">
          <label htmlFor="companySelect">Company</label>

          <select
            id="companySelect"
            value={selectedCompanyId}
            onChange={(e) => {
              setSelectedCompanyId(Number(e.target.value));
              setSelectedGroup("");
            }}
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
          <label htmlFor="ledgerGroupSelect">Group</label>
          <select
            id="ledgerGroupSelect"
            value={selectedGroup}
            onChange={(e) => setSelectedGroup(e.target.value)}
            disabled={loading || Boolean(error) || groups.length === 0}
          >
            <option value="">All groups</option>
            {groups.map((group) => (
              <option key={group} value={group}>{group}</option>
            ))}
          </select>
          <label htmlFor="ledgerNameSearch">Search</label>
          <input
            id="ledgerNameSearch"
            className="ledger-name-search"
            type="search"
            placeholder="Search Ledger Name..."
            value={ledgerSearch}
            onChange={(event) => setLedgerSearch(event.target.value)}
            aria-label="Search ledger names"
          />
        </div>

        <details className="ledger-column-picker">
        <summary aria-label="Choose visible columns" title="Choose visible columns" onClick={() => setPendingColumns(selectedColumns)}>
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M4 6h16M4 12h16M4 18h16" />
            <path d="M8 4v4M15 10v4M11 16v4" />
          </svg>
          <span className="ledger-column-count">{selectedColumns.length || ""}</span>
        </summary>
        <fieldset>
          <legend>Visible columns</legend>
          {ledgerColumns.filter((column) => column.optional).map((column) => (
            <label key={column.key}>
              <input
                type="checkbox"
                checked={pendingColumns.includes(column.key)}
                onChange={(event) => {
                  const checked = event.target.checked;
                  setPendingColumns((previous) => checked
                    ? [...previous, column.key]
                    : previous.filter((key) => key !== column.key));
                }}
              />
              {column.label}
            </label>
          ))}
          <div className="ledger-column-actions">
            <button
              type="button"
              className="ledger-columns-clear"
              onClick={(event) => {
                setPendingColumns([]);
                setSelectedColumns([]);
                event.currentTarget.closest("details")?.removeAttribute("open");
              }}
            >
              Clear
            </button>
            <button
              type="button"
              className="ledger-columns-save"
              onClick={(event) => {
                setSelectedColumns(pendingColumns);
                event.currentTarget.closest("details")?.removeAttribute("open");
              }}
            >
              Save
            </button>
          </div>
        </fieldset>
        </details>
      </div>

      {loading && <div className="ledger-message">Loading ledgers...</div>}

      {error && <div className="ledger-error">{error}</div>}

      {!loading && !error && (
        <div className="ledger-table-wrapper">
          <table
            className="ledger-table"
            style={{ minWidth: `${Math.max(760, 52 + visibleColumns.reduce((width, column) => width + column.minWidth, 0))}px` }}
          >
            <thead>
              <tr>
                <th>S.No</th>
                {visibleColumns.map((column) => (
                  <th key={column.key} scope="col" data-column={column.key}>{column.label}</th>
                ))}
              </tr>
            </thead>

            <tbody>
              {filteredLedgers.length === 0 ? (
                <tr>
                  <td colSpan={visibleColumns.length + 1} className="no-ledgers">
                    {normalizedLedgerSearch
                      ? "No matching ledgers found."
                      : selectedGroup
                        ? "No ledgers found for this group."
                        : "No ledgers found for this company."}
                  </td>
                </tr>
              ) : (
                filteredLedgers.map((ledger, index) => (
                  <tr key={ledger.id || `${ledger.name}-${index}`}>
                    <td>{index + 1}</td>
                    {visibleColumns.map((column) => (
                      <td key={column.key} className={column.className} data-column={column.key}>
                        {ledger[column.key] || (column.key === "openingBalance" ? "0" : "--")}
                      </td>
                    ))}
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
