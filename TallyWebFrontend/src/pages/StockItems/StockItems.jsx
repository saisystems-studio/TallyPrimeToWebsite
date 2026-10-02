import { useEffect, useState } from "react";
import { getDbStockItems } from "../../services/stockItemDbService";
import { getDbCompanies } from "../../services/companyService";
import "./StockItems.css";

function StockItems() {
  const [companies, setCompanies] = useState([]);
  const [selectedCompanyId, setSelectedCompanyId] = useState("");
  const [stockItems, setStockItems] = useState([]);

  const [loadingCompanies, setLoadingCompanies] = useState(true);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  // Load companies from database
  useEffect(() => {
    getDbCompanies()
      .then((data) => {
        const companyList = data.companies || [];

        setCompanies(companyList);

        if (companyList.length > 0) {
          setSelectedCompanyId(String(companyList[0].id));
        }
      })
      .catch((err) => {
        console.error("COMPANY DB ERROR:", err);
        setError("Unable to load companies from database.");
      })
      .finally(() => {
        setLoadingCompanies(false);
      });
  }, []);

  // Load Stock Items for selected company
  useEffect(() => {
    if (!selectedCompanyId) {
      setStockItems([]);
      return;
    }

    setLoading(true);
    setError("");

    getDbStockItems(selectedCompanyId)
      .then((data) => {
        setStockItems(data.stockItems || []);
      })
      .catch((err) => {
        console.error("STOCK ITEMS DB ERROR:", err);
        setError("Unable to load stock items from database.");
        setStockItems([]);
      })
      .finally(() => {
        setLoading(false);
      });
  }, [selectedCompanyId]);

  const showValue = (value) => {
    if (value === null || value === undefined || String(value).trim() === "") {
      return "--";
    }

    return value;
  };

  const showQuantity = (value, unit) => {
    if (value === null || value === undefined) {
      return "--";
    }

    const number = Number(value);

    if (Number.isNaN(number)) {
      return showValue(value);
    }

    const formatted = number.toLocaleString("en-IN", {
      minimumFractionDigits: 3,
      maximumFractionDigits: 3,
    });

    if (!unit || unit === "Not Applicable") {
      return formatted;
    }

    return `${formatted} ${unit}`;
  };

  return (
    <div className="stock-items-page">
      <div className="stock-items-header">
        <div>
          <span className="stock-items-eyebrow">Inventory master</span>

          <h1>Stock Items</h1>

          <p>View synchronized stock items from database</p>
        </div>

        <div className="stock-items-count">
          <span>Total stock items</span>
          <strong>{stockItems.length}</strong>
        </div>
      </div>

      {/* Company Filter */}
      <div className="stock-company-filter">
        <label>Company</label>

        <select
          value={selectedCompanyId}
          onChange={(e) => setSelectedCompanyId(e.target.value)}
          disabled={loadingCompanies || companies.length === 0}
        >
          {companies.length === 0 && (
            <option value="">No companies found</option>
          )}

          {companies.map((company) => (
            <option key={company.id} value={company.id}>
              {company.name}
            </option>
          ))}
        </select>
      </div>

      <div className="stock-items-card">
        {(loadingCompanies || loading) && (
          <div className="stock-items-message">Loading stock items...</div>
        )}

        {!loadingCompanies && !loading && error && (
          <div className="stock-items-error">{error}</div>
        )}

        {!loadingCompanies && !loading && !error && stockItems.length === 0 && (
          <div className="stock-items-message">
            No synchronized stock items found for this company.
          </div>
        )}

        {!loadingCompanies && !loading && !error && stockItems.length > 0 && (
          <div className="stock-items-table-wrapper">
            <table className="stock-items-table">
              <thead>
                <tr>
                  <th>S.No</th>
                  <th>Stock Item</th>
                  <th>Stock Group</th>
                  <th>Unit</th>
                  <th>Opening Balance</th>
                  <th>Closing Balance</th>
                  <th>Last Synced</th>
                </tr>
              </thead>

              <tbody>
                {stockItems.map((item, index) => (
                  <tr key={item.tallyGuid || item.id}>
                    <td>{index + 1}</td>

                    <td className="stock-item-name">{showValue(item.name)}</td>

                    <td>{showValue(item.stockGroup)}</td>

                    <td>{showValue(item.unit)}</td>

                    <td className="stock-items-number">
                      {showQuantity(item.openingQuantity, item.unit)}
                    </td>

                    <td className="stock-items-number">
                      {showQuantity(item.closingQuantity, item.unit)}
                    </td>

                    <td>
                      {item.lastSyncedAt
                        ? new Date(item.lastSyncedAt).toLocaleString("en-IN")
                        : "--"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}

export default StockItems;
