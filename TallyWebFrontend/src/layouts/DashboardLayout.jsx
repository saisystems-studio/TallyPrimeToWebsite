import { useEffect, useRef } from "react";
import { Outlet, Navigate, useNavigate } from "react-router-dom";
import Sidebar from "../components/Sidebar/Sidebar";
import "./Layout.css";

function DashboardLayout() {
  const navigate = useNavigate();
  const menuRef = useRef(null);
  const triggerRef = useRef(null);
  const previousOverflow = useRef(null);

  const restoreScroll = () => {
    if (previousOverflow.current !== null) {
      document.body.style.overflow = previousOverflow.current;
      previousOverflow.current = null;
    }
  };

  const closeMenu = () => menuRef.current?.close();

  const openMenu = () => {
    previousOverflow.current = document.body.style.overflow;
    menuRef.current.showModal();
    document.body.style.overflow = "hidden";
    triggerRef.current?.setAttribute("aria-expanded", "true");
  };

  useEffect(() => {
    const desktop = window.matchMedia("(min-width: 1025px)");
    const handleResize = () => {
      if (desktop.matches) menuRef.current?.close();
    };
    desktop.addEventListener("change", handleResize);
    return () => {
      desktop.removeEventListener("change", handleResize);
      restoreScroll();
    };
  }, []);
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
      <header className="mobileHeader">
        <button
          ref={triggerRef}
          type="button"
          className="mobileMenuButton"
          aria-label="Open navigation menu"
          aria-controls="mobile-navigation"
          aria-expanded="false"
          onClick={openMenu}
        >
          <span aria-hidden="true">☰</span>
        </button>
        <strong>Tally Web</strong>
      </header>

      <div className="desktopNavigation">
        <Sidebar user={user} onLogout={logout} />
      </div>

      <dialog
        ref={menuRef}
        id="mobile-navigation"
        className="mobileDrawer"
        aria-label="Navigation menu"
        onClick={(event) => {
          if (event.target === event.currentTarget) closeMenu();
        }}
        onClose={() => {
          restoreScroll();
          triggerRef.current?.setAttribute("aria-expanded", "false");
        }}
      >
        <div className="mobileDrawerPanel">
          <button type="button" className="mobileMenuClose" aria-label="Close navigation menu" onClick={closeMenu}>
            <span aria-hidden="true">×</span>
          </button>
          <Sidebar user={user} onLogout={() => { closeMenu(); logout(); }} onNavigate={closeMenu} />
        </div>
      </dialog>

      <main className="mainContent">
        <Outlet />
      </main>
    </div>
  );
}

export default DashboardLayout;
