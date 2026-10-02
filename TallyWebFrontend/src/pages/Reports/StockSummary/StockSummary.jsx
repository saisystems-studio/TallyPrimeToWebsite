import { useState } from "react";
import { getStockSummary } from "../../../services/stockSummaryService";
import "./StockSummary.css";

export default function StockSummary() {
  const [fromDate, setFromDate] = useState("2024-04-01");
  const [toDate, setToDate] = useState("2024-12-31");

  const [report, setReport] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const formatApiDate = (date) => {
    return date.replaceAll("-", "");
  };

  const formatQuantity = (value, unit) => {
    const number = Number(value || 0);

    const formatted = Math.abs(number).toLocaleString("en-IN", {
      minimumFractionDigits: 3,
      maximumFractionDigits: 3,
    });

    if (number < 0) {
      return `(-)${formatted}${unit ? ` ${unit}` : ""}`;
    }

    return `${formatted}${unit ? ` ${unit}` : ""}`;
  };

  const loadReport = async () => {
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

      const data = await getStockSummary(
        formatApiDate(fromDate),
        formatApiDate(toDate),
      );

      setReport(data);
    } catch (err) {
      setReport(null);
      setError(err.message || "Unable to load Stock Summary.");
    } finally {
      setLoading(false);
    }
  };

  const totalItems =
    report?.groups?.reduce(
      (total, group) => total + (group.items?.length || 0),
      0,
    ) || 0;

  return (
    <div className="stock-summary-page">
      <div className="stock-summary-header">
        <div>
          <h1>Stock Summary</h1>
          <p>
            View stock opening, inward, outward and closing quantities from
            Tally
          </p>
        </div>
      </div>

      <div className="stock-summary-filter">
        <div className="stock-summary-field">
          <label>From Date</label>

          <input
            type="date"
            value={fromDate}
            onChange={(e) => setFromDate(e.target.value)}
          />
        </div>

        <div className="stock-summary-field">
          <label>To Date</label>

          <input
            type="date"
            value={toDate}
            onChange={(e) => setToDate(e.target.value)}
          />
        </div>

        <button
          type="button"
          className="stock-summary-load-button"
          onClick={loadReport}
          disabled={loading}
        >
          {loading ? "Loading..." : "Load Report"}
        </button>
      </div>

      {error && <div className="stock-summary-error">{error}</div>}

      <div className="stock-summary-card">
        <div className="stock-summary-card-header">
          <div>
            <h2>Stock Summary</h2>

            <span>
              {report
                ? `${totalItems} Stock Items`
                : "Select a date range to view stock"}
            </span>
          </div>
        </div>

        <div className="stock-summary-table-wrapper">
          <table className="stock-summary-table">
            <thead>
              <tr>
                <th>Particulars</th>
                <th>Opening Balance</th>
                <th>Inwards</th>
                <th>Outwards</th>
                <th>Closing Balance</th>
              </tr>
            </thead>

            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="5" className="stock-summary-empty">
                    Loading Stock Summary...
                  </td>
                </tr>
              ) : !report ? (
                <tr>
                  <td colSpan="5" className="stock-summary-empty">
                    Select a date range and click Load Report.
                  </td>
                </tr>
              ) : !report.groups?.length ? (
                <tr>
                  <td colSpan="5" className="stock-summary-empty">
                    No stock data found.
                  </td>
                </tr>
              ) : (
                report.groups.map((group) => (
                  <>
                    <tr
                      className="stock-summary-group-row"
                      key={`group-${group.groupName}`}
                    >
                      <td>{group.groupName}</td>

                      <td>
                        {group.unit
                          ? formatQuantity(group.openingQuantity, group.unit)
                          : ""}
                      </td>

                      <td>
                        {group.unit
                          ? formatQuantity(group.inwardQuantity, group.unit)
                          : ""}
                      </td>

                      <td>
                        {group.unit
                          ? formatQuantity(group.outwardQuantity, group.unit)
                          : ""}
                      </td>

                      <td>
                        {group.unit
                          ? formatQuantity(group.closingQuantity, group.unit)
                          : ""}
                      </td>
                    </tr>

                    {group.items?.map((item, index) => (
                      <tr
                        className="stock-summary-item-row"
                        key={`${group.groupName}-${item.stockItemName}-${index}`}
                      >
                        <td>
                          <span className="stock-item-indent">
                            {item.stockItemName}
                          </span>
                        </td>

                        <td>
                          {formatQuantity(item.openingQuantity, item.unit)}
                        </td>

                        <td>
                          {formatQuantity(item.inwardQuantity, item.unit)}
                        </td>

                        <td>
                          {formatQuantity(item.outwardQuantity, item.unit)}
                        </td>

                        <td>
                          {formatQuantity(item.closingQuantity, item.unit)}
                        </td>
                      </tr>
                    ))}
                  </>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
