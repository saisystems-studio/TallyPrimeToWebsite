import { useEffect, useState } from "react";
import { getDbStockItems } from "../../services/stockItemDbService";
import { getDbCompanies } from "../../services/companyService";
import "./StockItems.css";

const optionalStockColumns = [
  { key: "partNumber", label: "Part No", minWidth: 125 },
  { key: "standardSellingRate", label: "Standard Selling Rate", numeric: true, minWidth: 175 },
  { key: "standardCostRate", label: "Standard Selling Cost", numeric: true, minWidth: 175 },
  { key: "mrp", label: "MRP", numeric: true, minWidth: 115 },
];

function StockItems() {
  const [companies, setCompanies] = useState([]);
  const [selectedCompanyId, setSelectedCompanyId] = useState("");
  const [stockItems, setStockItems] = useState([]);
  const [selectedStockGroup, setSelectedStockGroup] = useState("");
  const [stockItemSearch, setStockItemSearch] = useState("");
  const [selectedColumns, setSelectedColumns] = useState([]);
  const [pendingColumns, setPendingColumns] = useState([]);

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

  const stockGroups = [...new Set(
    stockItems
      .map((item) => item.stockGroup?.trim())
      .filter(Boolean)
  )].sort((a, b) => a.localeCompare(b));

  const normalizedStockItemSearch = stockItemSearch.trim().toLocaleLowerCase();
  const filteredStockItems = stockItems.filter((item) => {
    const matchesGroup = !selectedStockGroup
      || item.stockGroup?.trim() === selectedStockGroup;
    const itemName = String(item.name || "").toLocaleLowerCase();
    const matchesName = !normalizedStockItemSearch
      || itemName.includes(normalizedStockItemSearch);
    return matchesGroup && matchesName;
  });

  const showRate = (value) => {
    if (value === null || value === undefined || value === "") return "--";
    const number = Number(value);
    return Number.isFinite(number)
      ? number.toLocaleString("en-IN", { minimumFractionDigits: 2, maximumFractionDigits: 2 })
      : showValue(value);
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
          <strong>{filteredStockItems.length}</strong>
        </div>
      </div>

      {/* Company Filter */}
      <div className="stock-items-filters">
        <div className="stock-company-filter">
          <label htmlFor="stock-items-company">Company</label>

          <select
            id="stock-items-company"
            value={selectedCompanyId}
            onChange={(e) => {
              setSelectedCompanyId(e.target.value);
              setSelectedStockGroup("");
            }}
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

        <div className="stock-company-filter">
          <label htmlFor="stock-items-group">Stock Group</label>

          <select
            id="stock-items-group"
            value={selectedStockGroup}
            onChange={(e) => setSelectedStockGroup(e.target.value)}
            disabled={loading || stockGroups.length === 0}
          >
            <option value="">All groups</option>
            {stockGroups.map((group) => (
              <option key={group} value={group}>
                {group}
              </option>
            ))}
          </select>
        </div>

        <div className="stock-company-filter stock-item-search-filter">
          <label htmlFor="stock-item-name-search">Search Stock Item</label>
          <input
            id="stock-item-name-search"
            className="stock-item-name-search"
            type="search"
            placeholder="Search Stock Item Name..."
            value={stockItemSearch}
            onChange={(event) => setStockItemSearch(event.target.value)}
            aria-label="Search stock item names"
          />
        </div>

        <details className="stock-items-column-picker">
          <summary aria-label="Choose visible columns" title="Choose visible columns" onClick={() => setPendingColumns(selectedColumns)}>
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M4 6h16M4 12h16M4 18h16" />
              <path d="M8 4v4M15 10v4M11 16v4" />
            </svg>
            <span className="stock-items-column-count">{selectedColumns.length || ""}</span>
          </summary>
          <fieldset>
            <legend>Visible columns</legend>
            {optionalStockColumns.map((column) => (
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
            <div className="stock-items-column-actions">
              <button
                type="button"
                className="stock-items-columns-clear"
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
                className="stock-items-columns-save"
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

        {!loadingCompanies && !loading && !error && normalizedStockItemSearch && filteredStockItems.length === 0 && (
          <div className="stock-items-message">
            No matching stock items found
          </div>
        )}

        {!loadingCompanies && !loading && !error && !normalizedStockItemSearch && stockItems.length > 0 && filteredStockItems.length === 0 && (
          <div className="stock-items-message">
            No stock items found in this stock group.
          </div>
        )}

        {!loadingCompanies && !loading && !error && filteredStockItems.length > 0 && (
          <div className="stock-items-table-wrapper">
            <table
              className="stock-items-table"
              style={{ minWidth: `${Math.max(
                760,
                52 + 155 + 130 + 100 + 145 + 125 + optionalStockColumns
                  .filter((column) => selectedColumns.includes(column.key))
                  .reduce((width, column) => width + column.minWidth, 0),
              )}px` }}
            >
              <thead>
                <tr>
                  <th>S.No</th>
                  <th data-column="name">Stock Item</th>
                  <th data-column="stockGroup">Stock Group</th>
                  <th data-column="unit">Unit</th>
                  <th data-column="openingQuantity">Opening Quantity</th>
                  <th data-column="openingRate">Opening Rate</th>
                  {optionalStockColumns.filter((column) => selectedColumns.includes(column.key)).map((column) => (
                    <th key={column.key} data-column={column.key} className={column.numeric ? "stock-items-number" : undefined}>
                      {column.label}
                    </th>
                  ))}
                </tr>
              </thead>

              <tbody>
                {filteredStockItems.map((item, index) => (
                  <tr key={item.tallyGuid || item.id}>
                    <td>{index + 1}</td>

                    <td data-column="name" className="stock-item-name">{showValue(item.name)}</td>

                    <td data-column="stockGroup">{showValue(item.stockGroup)}</td>

                    <td data-column="unit">{showValue(item.unit)}</td>

                    <td data-column="openingQuantity" className="stock-items-number">
                      {showQuantity(item.openingQuantity, item.unit)}
                    </td>

                    <td data-column="openingRate" className="stock-items-number">
                      {showRate(item.openingRate)}
                    </td>

                    {optionalStockColumns.filter((column) => selectedColumns.includes(column.key)).map((column) => (
                      <td key={column.key} data-column={column.key} className={column.numeric ? "stock-items-number" : undefined}>
                        {column.key === "partNumber" ? showValue(item.partNumber) : showRate(item[column.key])}
                      </td>
                    ))}

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
