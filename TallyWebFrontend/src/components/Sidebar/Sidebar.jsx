import { NavLink } from "react-router-dom";
import "./Sidebar.css";

const sections = [
  {
    title: "Workspace",
    items: [
      {
        to: "/dashboard",
        label: "Dashboard",
        icon: "▦",
      },
    ],
  },
  {
    title: "Company",
    items: [
      {
        to: "/company",
        label: "Company",
        icon: "▣",
      },
    ],
  },
  {
    title: "Masters",
    items: [
      {
        to: "/ledgers",
        label: "Ledgers",
        icon: "▤",
      },
      {
        to: "/stock-items",
        label: "Stock Items",
        icon: "◫",
      },
    ],
  },
  {
    title: "Transactions",
    items: [
      { to: "/vouchers", label: "Vouchers", icon: "⇄" },
      { to: "/day-book", label: "Day Book", icon: "▤" },
    ],
  },

  // =====================================================
  // REPORTS
  // =====================================================
  {
    title: "Reports",
    items: [
      /*
    {
      to: "/reports/sales",
      label: "Sales Report",
      icon: "▥",
    },
    {
      to: "/reports/ledger",
      label: "Ledger Report",
      icon: "▤",
    },
  

      {
        to: "/reports/stock-summary",
        label: "Stock Summary",
        icon: "▦",
      },
        */
      {
        to: "/outstanding",
        label: "Outstanding",
        icon: "▧",
      },
      {
        to: "/payables-receivables",
        label: "Payables & Receivables",
        icon: "↔",
      },
    ],
  },
];

function Sidebar({ user = {}, onLogout, onNavigate }) {
  return (
    <aside className="sidebar">
      <div className="brand">
        <div className="brandIcon" aria-hidden="true">
          T
        </div>

        <div className="brandCopy">
          <h2>Tally Web</h2>
          <span>Business Suite</span>
        </div>
      </div>

      <nav className="sidebarNav" aria-label="Main navigation">
        {sections.map((section) => (
          <div className="menuSection" key={section.title}>
            <p className="menuTitle">{section.title}</p>

            {section.items.map((item) => (
              <NavLink
                to={item.to}
                className="menuItem"
                key={item.to}
                onClick={onNavigate}
              >
                <span className="menuIcon" aria-hidden="true">
                  {item.icon}
                </span>

                <span>{item.label}</span>
              </NavLink>
            ))}
          </div>
        ))}
      </nav>

      <div className="sidebarFooter">
        <div className="sidebarConnection">
          <span className="statusDot" />
          Connected to Tally
        </div>

        <div className="sidebarAccount">
          <div className="sidebarAvatar" aria-hidden="true">
            {(user.fullName || user.username || "U").charAt(0).toUpperCase()}
          </div>

          <div className="sidebarUserInfo">
            <strong>{user.fullName || user.username || "Administrator"}</strong>

            <span>{user.role || "Admin"}</span>
          </div>

          <button className="sidebarLogout" onClick={onLogout}>
            Logout
          </button>
        </div>
      </div>
    </aside>
  );
}

export default Sidebar;
