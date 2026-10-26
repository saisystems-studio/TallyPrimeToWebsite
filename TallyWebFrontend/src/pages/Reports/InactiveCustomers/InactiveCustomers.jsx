import { useEffect, useMemo, useState } from "react";
import { getDbCompanies } from "../../../services/companyService";
import { getInactiveCustomers } from "../../../services/inactiveCustomersService";
import "./InactiveCustomers.css";

function formatDate(value) {
  if (!value) return "—";

  const text = String(value);
  const iso = /^\d{8}$/.test(text)
    ? `${text.slice(0, 4)}-${text.slice(4, 6)}-${text.slice(6, 8)}`
    : text;

  const parts = iso.split("-");
  if (parts.length !== 3) return text;

  return `${parts[2]}-${parts[1]}-${parts[0]}`;
}

export default function InactiveCustomers() {
  const [companies, setCompanies] = useState([]);
  const [companyId, setCompanyId] = useState("");
  const [customers, setCustomers] = useState([]);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [asOfDate, setAsOfDate] = useState("");

  // Load existing company master.
  useEffect(() => {
    let cancelled = false;

    async function loadCompanies() {
      try {
        const result = await getDbCompanies();
        if (cancelled) return;

        const list = Array.isArray(result) ? result : result.companies || [];

        setCompanies(list);

        const selected =
          list.find((item) => String(item.id) === "1") || list[0];

        if (selected) {
          setCompanyId(String(selected.id));
        }
      } catch (err) {
        if (!cancelled) {
          setError(err.message || "Unable to load companies.");
        }
      }
    }

    loadCompanies();

    return () => {
      cancelled = true;
    };
  }, []);

  // Load inactive customers for selected company.
  useEffect(() => {
    if (!companyId) return;

    let cancelled = false;

    async function loadCustomers() {
      setLoading(true);
      setError("");
      setCustomers([]);

      try {
        const result = await getInactiveCustomers(companyId);

        if (cancelled) return;

        setCustomers(result.customers || []);
        setAsOfDate(result.asOfDate || "");
      } catch (err) {
        if (!cancelled) {
          setError(err.message || "Unable to load customers.");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    loadCustomers();

    return () => {
      cancelled = true;
    };
  }, [companyId]);

  // Search and descending inactive-days sorting.
  const visibleCustomers = useMemo(() => {
    const keyword = search.trim().toLowerCase();

    return customers
      .filter((customer) =>
        String(customer.customerName || "")
          .toLowerCase()
          .includes(keyword),
      )
      .sort(
        (a, b) =>
          Number(b.inactiveDays) - Number(a.inactiveDays) ||
          String(a.customerName).localeCompare(String(b.customerName)),
      );
  }, [customers, search]);

  const company = companies.find(
    (item) => String(item.id) === String(companyId),
  );

  function exportExcel() {
    if (!visibleCustomers.length) return;

    const rows = [
      ["S.No", "Customer Name", "Last Sales Date", "Inactive Days"],
      ...visibleCustomers.map((customer, index) => [
        index + 1,
        customer.customerName,
        formatDate(customer.lastSalesDate),
        customer.inactiveDays,
      ]),
    ];

    const csv = rows
      .map((row) =>
        row
          .map((value) => {
            let cell = String(value ?? "");
            if (/^[\s]*[=+\-@]/.test(cell)) cell = "'" + cell;
            return `"${cell.replace(/"/g, '""')}"`;
          })
          .join(","),
      )
      .join("\r\n");

    const blob = new Blob(["\ufeff", csv], {
      type: "text/csv;charset=utf-8;",
    });

    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "Inactive_Customers.csv";
    link.click();

    URL.revokeObjectURL(url);
  }

  function printReport() {
    if (!visibleCustomers.length) return;

    const printWindow = window.open("", "_blank");

    if (!printWindow) {
      setError("Please allow pop-ups to print the report.");
      return;
    }

    const doc = printWindow.document;
    doc.title = "Inactive Customers";
    const style = doc.createElement("style");
    style.textContent = `
      body { font-family: Arial; padding: 24px; color: #24334b; }
      h2 { margin-bottom: 6px; }
      p { color: #64748b; }
      table { width: 100%; border-collapse: collapse; }
      th, td { border: 1px solid #cbd5e1; padding: 9px; text-align: left; }
      th { background: #f1f5f9; }
      td:last-child, th:last-child { text-align: right; }
    `;
    doc.head.appendChild(style);

    const heading = doc.createElement("h2");
    heading.textContent = "Inactive Customers";
    doc.body.appendChild(heading);

    const subtitle = doc.createElement("p");
    subtitle.textContent = `${company?.name || "Company"} | As of ${formatDate(asOfDate)}`;
    doc.body.appendChild(subtitle);

    const table = doc.createElement("table");
    const thead = table.createTHead();
    const header = thead.insertRow();
    ["S.No", "Customer Name", "Last Sales Date", "Inactive Days"].forEach(
      (label) => {
        const th = doc.createElement("th");
        th.textContent = label;
        header.appendChild(th);
      },
    );

    const tbody = table.createTBody();
    visibleCustomers.forEach((customer, index) => {
      const tr = tbody.insertRow();
      [
        index + 1,
        customer.customerName,
        formatDate(customer.lastSalesDate),
        customer.inactiveDays,
      ].forEach((value) => {
        const td = tr.insertCell();
        td.textContent = String(value ?? "");
      });
    });

    doc.body.appendChild(table);
    printWindow.focus();
    printWindow.print();
  }

  return (
    <main className="inactive-customers">
      <header className="inactive-customers__header">
        <div>
          <span className="inactive-customers__eyebrow">REPORTS</span>
          <h1>Inactive Customers</h1>
          <p>
            {company?.name || "Select a company"}
            {asOfDate ? ` · As of ${formatDate(asOfDate)}` : ""}
          </p>
        </div>

      </header>

      <section className="inactive-customers__filters">
        <label>
          Company
          <select
            value={companyId}
            onChange={(event) => setCompanyId(event.target.value)}
          >
            {!companyId && <option value="">Select Company</option>}
            {companies.map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Search Customer
          <input
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search customer name"
          />
        </label>

        <label>
          Sort By
          <select value="desc" disabled>
            <option value="desc">Inactive Days - High to Low</option>
          </select>
        </label>
      </section>

      {error && (
        <div className="inactive-customers__error" role="alert">
          {error}
        </div>
      )}

      <section className="inactive-customers__table-area">
        <div className="inactive-customers__scroll">
          <table className="inactive-customers__table">
            <thead>
              <tr>
                <th>S.No</th>
                <th>Customer Name</th>
                <th>Last Sales Date</th>
                <th>Inactive Days</th>
              </tr>
            </thead>

            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="4" className="inactive-customers__empty">
                    Loading inactive customers...
                  </td>
                </tr>
              ) : visibleCustomers.length ? (
                visibleCustomers.map((customer, index) => (
                  <tr key={`${customer.customerName}-${index}`}>
                    <td>{index + 1}</td>
                    <td>{customer.customerName}</td>
                    <td>{formatDate(customer.lastSalesDate)}</td>
                    <td className="inactive-customers__days">
                      {customer.inactiveDays.toLocaleString("en-IN")}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan="4" className="inactive-customers__empty">
                    {error
                      ? "Unable to display customers."
                      : "No customers found."}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        <footer className="inactive-customers__footer">
          <span>
            Showing {visibleCustomers.length} of {customers.length} customers
          </span>
          <strong>Total Customers: {visibleCustomers.length}</strong>
        </footer>
      </section>
    </main>
  );
}
