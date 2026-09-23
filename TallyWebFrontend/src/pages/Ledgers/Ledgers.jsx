import { useEffect, useState } from "react";
import { getLedgers, getLedgersRaw } from "../../services/tallyService";
import "./Ledgers.css";

function Ledgers() {
  const [ledgers, setLedgers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    getLedgers()
      .then((data) => setLedgers(data.ledgers || []))
      .catch((err) => {
        console.error("Ledger Error:", err);
        setError(err.message || "Unable to load ledgers.");
      })
      .finally(() => setLoading(false));

    getLedgersRaw()
      .then((xml) => {
        const ledgerName = "Ilakkiya Mariyappan";

        const position = xml.indexOf(ledgerName);

        if (position !== -1) {
          console.log("===== ILAKKIYA LEDGER RAW XML =====");
          console.log(
            xml.substring(Math.max(0, position - 500), position + 5000),
          );
        } else {
          console.log("Ilakkiya Mariyappan not found in raw XML");
        }
      })
      .catch((error) => {
        console.error("LEDGER RAW ERROR:", error);
      });
  }, []);

  return (
    <div className="ledgers-page">
      <div className="ledgers-header">
        <div>
          <span className="ledgers-eyebrow">Accounts master</span>
          <h1>Ledgers</h1>
          <p>View account masters and balances from Tally</p>
        </div>

        <div className="ledger-count">
          <span>Total ledgers</span>
          <strong>{ledgers.length}</strong>
        </div>
      </div>

      {loading && (
        <div className="ledger-message">Loading ledgers from Tally...</div>
      )}

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
                    No ledgers found in Tally.
                  </td>
                </tr>
              ) : (
                ledgers.map((ledger, index) => (
                  <tr key={`${ledger.name}-${index}`}>
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
                    <td className="ledger-number">{ledger.openingBalance || "0"}</td>
                    <td className="ledger-number">{ledger.closingBalance || "0"}</td>
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
