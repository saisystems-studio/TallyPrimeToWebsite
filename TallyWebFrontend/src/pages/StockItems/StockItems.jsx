import { useEffect, useState } from "react";
import { getStockItems } from "../../services/tallyService";
import "./StockItems.css";

function StockItems() {
  const [stockItems, setStockItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    getStockItems()
      .then((data) => setStockItems(data.stockItems || []))
      .catch((err) => {
        console.error("STOCK ITEMS ERROR:", err);
        setError("Unable to load stock items from Tally.");
      })
      .finally(() => setLoading(false));
  }, []);

  const showValue = (value) => {
    if (value === null || value === undefined || String(value).trim() === "") {
      return "--";
    }

    return value;
  };

  return (
    <div className="stock-items-page">
      <div className="stock-items-header">
        <div>
          <span className="stock-items-eyebrow">Inventory master</span>
          <h1>Stock Items</h1>
          <p>View stock items and stock group details from Tally</p>
        </div>

        <div className="stock-items-count">
          <span>Total stock items</span>
          <strong>{stockItems.length}</strong>
        </div>
      </div>

      <div className="stock-items-card">
        {loading && (
          <div className="stock-items-message">
            Loading stock items from Tally...
          </div>
        )}

        {!loading && error && <div className="stock-items-error">{error}</div>}

        {!loading && !error && stockItems.length === 0 && (
          <div className="stock-items-message">
            No stock items found in Tally.
          </div>
        )}

        {!loading && !error && stockItems.length > 0 && (
          <div className="stock-items-table-wrapper">
            <table className="stock-items-table">
              <thead>
                <tr>
                  <th>S.No</th>
                  <th>Stock Item</th>
                  <th>Stock Group</th>
                  <th>Unit</th>
                  <th>HSN / SAC</th>
                  <th>GST Applicable</th>
                  <th>Type of Supply</th>
                  <th>GST Source</th>
                  <th>Opening Balance</th>
                  <th>Opening Rate</th>
                  <th>Opening Value</th>
                </tr>
              </thead>

              <tbody>
                {stockItems.map((item, index) => (
                  <tr key={`${item.name}-${index}`}>
                    <td>{index + 1}</td>

                    <td className="stock-item-name">{showValue(item.name)}</td>

                    <td>{showValue(item.stockGroup)}</td>

                    <td>{showValue(item.unit)}</td>

                    <td>{showValue(item.hsnCode)}</td>

                    <td>{showValue(item.gstApplicable)}</td>

                    <td>{showValue(item.typeOfSupply)}</td>

                    <td>{showValue(item.gstSource)}</td>

                    <td className="stock-items-number">
                      {showValue(item.openingBalance)}
                    </td>

                    <td className="stock-items-number">
                      {showValue(item.openingRate)}
                    </td>

                    <td className="stock-items-number">
                      {showValue(item.openingValue)}
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
