import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { useLocation } from "react-router-dom";
import "./PageExport.css";

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function PageExport() {
  const { pathname } = useLocation();
  const [header, setHeader] = useState(null);
  const [shareMenuOpen, setShareMenuOpen] = useState(false);

  useEffect(() => {
    if (pathname === "/dashboard" || pathname === "/reports/stock-summary") {
      return;
    }

    const pageHeading = document.querySelector(".mainContent h1, .mainContent h2");
    let current = pageHeading;
    while (current && current !== document.querySelector(".mainContent")) {
      if ([...current.classList].some((name) => name.endsWith("-header"))) {
        current.classList.add("page-export-header");
        setHeader(current);
        return;
      }
      current = current.parentElement;
    }
  }, [pathname]);

  if (pathname === "/dashboard" || pathname === "/reports/stock-summary" || !header) return null;

  const exportExcel = () => {
    const table = document.querySelector(".mainContent table");
    let tableHtml = table?.outerHTML;

    if (!tableHtml && pathname === "/company") {
      const rows = [...document.querySelectorAll(".company-row-summary")];
      tableHtml = `<table><thead><tr><th>Company</th><th>GSTIN</th><th>Location</th><th>Financial year from</th></tr></thead><tbody>${rows.map((row) => {
        const cells = [...row.querySelectorAll("h2, .company-row-value")]
          .map((cell) => `<td>${escapeHtml(cell.textContent.trim())}</td>`)
          .join("");
        return `<tr>${cells}</tr>`;
      }).join("")}</tbody></table>`;
    }

    if (!tableHtml) {
      window.alert("There is no table data to export on this page yet.");
      return;
    }

    const workbook = `<!doctype html><html><head><meta charset="utf-8"></head><body>${tableHtml}</body></html>`;
    const blob = new Blob(["\ufeff", workbook], { type: "application/vnd.ms-excel;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `${document.querySelector(".mainContent h1")?.textContent.trim() || "report"}.xls`;
    document.body.append(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  };

  const getShareDetails = () => {
    const title = document.querySelector(".mainContent h1")?.textContent.trim() || "Tally Web";
    const table = document.querySelector(".mainContent table");
    const details = table
      ? [...table.querySelectorAll("tr")]
        .map((row) => [...row.querySelectorAll("th, td")]
          .map((cell) => cell.innerText.trim().replaceAll(/\s+/g, " "))
          .filter(Boolean)
          .join(" | "))
        .filter(Boolean)
        .join("\n")
      : [...document.querySelectorAll(".company-row-summary")]
        .map((row) => row.innerText.trim().replaceAll(/\s+/g, " | "))
        .join("\n");

    const pageUrl = window.location.href;
    const content = `${title}\n\n${details || "No page details are currently available."}${details ? "\n\n" : "\n"}Open in Tally Web: ${pageUrl}`;
    return { title, content, pageUrl };
  };

  const shareToWhatsApp = () => {
    const { content } = getShareDetails();
    window.open(`https://wa.me/?text=${encodeURIComponent(content)}`, "_blank", "noopener,noreferrer");
    setShareMenuOpen(false);
  };

  const shareToEmail = () => {
    const { title, content } = getShareDetails();
    // Keep the mailto URL small enough for desktop mail clients and browsers.
    const maxBodyLength = 5000;
    const emailBody = content.length > maxBodyLength
      ? `${content.slice(0, maxBodyLength)}\n\n[Details shortened. Open the page link above to view the full report.]`
      : content;
    const mailLink = document.createElement("a");
    mailLink.href = `mailto:?subject=${encodeURIComponent(title)}&body=${encodeURIComponent(emailBody)}`;
    mailLink.style.display = "none";
    document.body.append(mailLink);
    mailLink.click();
    mailLink.remove();
    setShareMenuOpen(false);
  };

  const copyShareDetails = async () => {
    const { content } = getShareDetails();
    try {
      await navigator.clipboard.writeText(content);
      setShareMenuOpen(false);
    } catch {
      window.prompt("Copy these page details to share:", content);
    }
  };

  return createPortal(
    <div className="page-export" aria-label="Export page data">
      <button
        type="button"
        className="page-export-icon-button"
        aria-label="Refresh website"
        title="Refresh"
        onClick={() => window.location.reload()}
      >
        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M20 7v5h-5M4.8 9a7 7 0 0 1 11.8-2L20 12M4 17v-5h5m10.2 3a7 7 0 0 1-11.8 2L4 12" /></svg>
      </button>
      <div className="page-share">
        <button
          type="button"
          className="page-export-icon-button"
          aria-label="Share page details"
          title="Share"
          aria-haspopup="menu"
          aria-expanded={shareMenuOpen}
          onClick={() => setShareMenuOpen((open) => !open)}
        >
          <svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="18" cy="5" r="3" /><circle cx="6" cy="12" r="3" /><circle cx="18" cy="19" r="3" /><path d="m8.7 10.6 6.6-4.2M8.7 13.4l6.6 4.2" /></svg>
        </button>
        {shareMenuOpen && (
          <div className="page-share-menu" role="menu" aria-label="Share page details using">
            <button type="button" role="menuitem" onClick={shareToWhatsApp}>WhatsApp</button>
            <button type="button" role="menuitem" onClick={shareToEmail}>Email</button>
            <button type="button" role="menuitem" onClick={copyShareDetails}>Copy details</button>
          </div>
        )}
      </div>
      <button type="button" className="page-export-icon-button" aria-label="Export Excel" title="Export Excel" onClick={exportExcel}>
        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" /><path d="M14 2v6h6M8 13l4 6m0-6-4 6m7-6v6m0-6 3 6m0-6-3 6" /></svg>
      </button>
      <button type="button" className="page-export-icon-button" aria-label="Export PDF" title="Export PDF" onClick={() => window.print()}>
        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" /><path d="M14 2v6h6M8 16h2a1.5 1.5 0 0 0 0-3H8v5m6 0v-5h1.5a2.5 2.5 0 0 1 0 5H14m5-5h3m-3 2h2" /></svg>
      </button>
    </div>,
    header,
  );
}

export default PageExport;
