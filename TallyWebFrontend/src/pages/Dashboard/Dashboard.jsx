import "./Dashboard.css";
import { useEffect, useState } from "react";
import {
  getCurrentCompany,
  getCompanyRaw,
  getGstRegistrationsRaw,
} from "../../services/tallyService";

function Dashboard() {
  const [company, setCompany] = useState(null);
  const [tallyLoading, setTallyLoading] = useState(true);
  const [tallyError, setTallyError] = useState("");

  useEffect(() => {
    const loadCurrentCompany = async () => {
      try {
        setTallyLoading(true);

        const data = await getCurrentCompany();

        console.log("Current Company:", data);

        setCompany(data.company);
        setTallyError("");
      } catch (error) {
        console.error("Tally Error:", error);

        setCompany(null);
        setTallyError(error.message);
      } finally {
        setTallyLoading(false);
      }
    };

    loadCurrentCompany();

    getGstRegistrationsRaw()
      .then((xml) => {
        console.log("===== GST REGISTRATION RAW XML =====");
        console.log(xml);
      })
      .catch((error) => {
        console.error("GST RAW ERROR:", error);
      });

    getCompanyRaw()
      .then((xml) => {
        console.log("===== TALLY COMPANY RAW XML =====");
        console.log(xml);
      })
      .catch((error) => {
        console.error("RAW XML ERROR:", error);
      });
  }, []);

  const companyName = tallyLoading ? "Loading..." : company?.name || "--";

  const companyLocation =
    company?.state && company?.country
      ? `${company.state}, ${company.country}`
      : company?.state || company?.country || "--";

  return (
    <>
      <section className="companyBar">
        <div>
          <span className="companyLabel">CURRENT COMPANY</span>

          <h2>{companyName}</h2>

          <p>{companyLocation}</p>
        </div>

        <div className="tallyStatus">
          <span className="statusDot"></span>

          <div>
            <strong>
              {tallyError
                ? "Tally Not Connected"
                : tallyLoading
                  ? "Connecting..."
                  : "Tally Connected"}
            </strong>

            <small>{tallyError ? tallyError : "127.0.0.1:9000"}</small>
          </div>
        </div>
      </section>

      <section className="statsGrid">
        <div className="statCard">
          <span>COMPANY</span>

          <h3>{companyName}</h3>

          <p>Active company</p>
        </div>

        <div className="statCard">
          <span>LEDGERS</span>
          <h3>--</h3>
          <p>Available ledgers</p>
        </div>

        <div className="statCard">
          <span>STOCK ITEMS</span>
          <h3>--</h3>
          <p>Inventory items</p>
        </div>

        <div className="statCard">
          <span>VOUCHERS</span>
          <h3>--</h3>
          <p>Total transactions</p>
        </div>
      </section>
    </>
  );
}

export default Dashboard;
