import { NavLink } from "react-router-dom";
import "./Sidebar.css";

const sections = [
  {
    title: "Workspace",
    items: [{ to: "/dashboard", label: "Dashboard", icon: "▦" }],
  },
  {
    title: "Company",
    items: [{ to: "/company", label: "Company", icon: "▣" }],
  },
  {
    title: "Masters",
    items: [
      { to: "/ledgers", label: "Ledgers", icon: "▤" },
      { to: "/stock-items", label: "Stock Items", icon: "◫" },
    ],
  },
  {
    title: "Transactions",
    items: [{ to: "/vouchers", label: "Vouchers", icon: "⇄" }],
  },
];

function Sidebar() {
  return (
    <aside className="sidebar">
      <div className="brand">
        <div className="brandIcon" aria-hidden="true">T</div>
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
              <NavLink to={item.to} className="menuItem" key={item.to}>
                <span className="menuIcon" aria-hidden="true">{item.icon}</span>
                <span>{item.label}</span>
              </NavLink>
            ))}
          </div>
        ))}
      </nav>
      <div className="sidebarFooter"><span className="statusDot" /> Connected to Tally</div>
    </aside>
  );
}

export default Sidebar;
