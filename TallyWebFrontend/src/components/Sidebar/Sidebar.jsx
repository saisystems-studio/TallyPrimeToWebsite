import { NavLink } from "react-router-dom";
import "./Sidebar.css";

function Sidebar() {
  return (
    <aside className="sidebar">
      <div className="brand">
        <div className="brandIcon">T</div>

        <div>
          <h2>Tally Web</h2>
          <span>Business Suite</span>
        </div>
      </div>

      <div className="menuSection">
        <p className="menuTitle">MAIN</p>

        <NavLink to="/dashboard" className="menuItem">
          ▦ <span>Dashboard</span>
        </NavLink>
      </div>

      <div className="menuSection">
        <p className="menuTitle">COMPANY</p>

        <NavLink to="/company" className="menuItem">
          ▣ <span>Company</span>
        </NavLink>
      </div>

      <div className="menuSection">
        <p className="menuTitle">MASTERS</p>

        <NavLink to="/groups" className="menuItem">
          ◫ <span>Groups</span>
        </NavLink>

        <NavLink to="/ledgers" className="menuItem">
          ▤ <span>Ledgers</span>
        </NavLink>

        <NavLink to="/stock-groups" className="menuItem">
          ◈ <span>Stock Groups</span>
        </NavLink>

        <NavLink to="/stock-items" className="menuItem">
          □ <span>Stock Items</span>
        </NavLink>
      </div>

      <div className="menuSection">
        <p className="menuTitle">TRANSACTIONS</p>

        <NavLink to="/sales" className="menuItem">
          Sales
        </NavLink>
        <NavLink to="/purchase" className="menuItem">
          Purchase
        </NavLink>
        <NavLink to="/receipt" className="menuItem">
          Receipt
        </NavLink>
        <NavLink to="/payment" className="menuItem">
          Payment
        </NavLink>
        <NavLink to="/journal" className="menuItem">
          Journal
        </NavLink>
      </div>

      <div className="menuSection">
        <p className="menuTitle">REPORTS</p>

        <NavLink to="/outstanding" className="menuItem">
          Outstanding
        </NavLink>
      </div>
    </aside>
  );
}

export default Sidebar;
