import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";

import Login from "./pages/Login/Login";
import Dashboard from "./pages/Dashboard/Dashboard";
import DashboardLayout from "./layouts/DashboardLayout";
import Ledgers from "./pages/Ledgers/Ledgers";
import StockItems from "./pages/StockItems/StockItems";
import Vouchers from "./pages/Vouchers/Vouchers";
import SalesReport from "./pages/Reports/SalesReport/SalesReport";
import LedgerReport from "./pages/Reports/LedgerReport/LedgerReport";
import StockSummary from "./pages/Reports/StockSummary/StockSummary";
import Company from "./pages/Company/Company";
import Outstanding from "./pages/Outstanding/Outstanding";
import DayBook from "./pages/DayBook/DayBook";
import PayablesReceivables from "./pages/Reports/PayablesReceivables/PayablesReceivables";

function Placeholder({ title }) {
  return (
    <div className="panel">
      <h2>{title}</h2>
      <p>{title} data will be loaded from Tally.</p>
    </div>
  );
}

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<Login />} />

        <Route element={<DashboardLayout />}>
          <Route path="/dashboard" element={<Dashboard />} />
          <Route path="/company" element={<Company />} />
          <Route path="/ledgers" element={<Ledgers />} />
          <Route path="/stock-items" element={<StockItems />} />

          <Route path="/sales" element={<Placeholder title="Sales" />} />
          <Route path="/purchase" element={<Placeholder title="Purchase" />} />
          <Route path="/receipt" element={<Placeholder title="Receipt" />} />
          <Route path="/payment" element={<Placeholder title="Payment" />} />
          <Route path="/journal" element={<Placeholder title="Journal" />} />

          <Route path="/vouchers" element={<Vouchers />} />
          <Route path="/day-book" element={<DayBook />} />

          <Route path="/reports/sales" element={<SalesReport />} />
          <Route path="/reports/ledger" element={<LedgerReport />} />
          <Route path="/reports/stock-summary" element={<StockSummary />} />
          <Route
            path="/payables-receivables"
            element={<PayablesReceivables />}
          />

          <Route path="/outstanding" element={<Outstanding />} />
        </Route>

        <Route path="/" element={<Navigate to="/login" replace />} />
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
