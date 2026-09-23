import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";

import Login from "./pages/Login/Login";
import Dashboard from "./pages/Dashboard/Dashboard";
import DashboardLayout from "./layouts/DashboardLayout";
import Ledgers from "./pages/Ledgers/Ledgers";
import StockItems from "./pages/StockItems/StockItems";

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
          <Route path="/company" element={<Placeholder title="Company" />} />
          <Route path="ledgers" element={<Ledgers />} />
          <Route path="stock-items" element={<StockItems />} />
          <Route path="/sales" element={<Placeholder title="Sales" />} />
          <Route path="/purchase" element={<Placeholder title="Purchase" />} />
          <Route path="/receipt" element={<Placeholder title="Receipt" />} />
          <Route path="/payment" element={<Placeholder title="Payment" />} />
          <Route path="/journal" element={<Placeholder title="Journal" />} />
          <Route
            path="/outstanding"
            element={<Placeholder title="Outstanding" />}
          />
        </Route>

        <Route path="/" element={<Navigate to="/login" replace />} />
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
