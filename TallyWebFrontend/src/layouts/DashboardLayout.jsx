import { Outlet, Navigate, useNavigate } from "react-router-dom";
import Sidebar from "../components/Sidebar/Sidebar";
import "./Layout.css";

function DashboardLayout() {
  const navigate = useNavigate();
  const token = localStorage.getItem("token");
  const user = JSON.parse(localStorage.getItem("user") || "{}");

  if (!token) {
    return <Navigate to="/login" replace />;
  }
  const logout = () => {
    localStorage.removeItem("token");
    localStorage.removeItem("user");

    navigate("/login");
  };

  return (
    <div className="appShell">
      <Sidebar />

      <main className="mainContent">
        <header className="topbar">
          <div>
            <h1>Tally Web</h1>
            <p>Business Data Management</p>
          </div>

          <div className="headerRight">
            <div className="avatar">
              {(user.fullName || user.username || "U").charAt(0).toUpperCase()}
            </div>

            <div className="userInfo">
              <div>
                <strong>{user.fullName || user.username}</strong>
                <span>{user.role}</span>
              </div>
            </div>

            <button className="logoutButton" onClick={logout}>
              Logout
            </button>
          </div>
        </header>

        <Outlet />
      </main>
    </div>
  );
}

export default DashboardLayout;
