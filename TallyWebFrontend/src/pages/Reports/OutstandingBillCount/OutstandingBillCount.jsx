import { useEffect, useMemo, useState } from "react";
import { getDbCompanies } from "../../../services/companyService";
import {
  getOutstandingBillCount,
  getOutstandingBillDetails,
  getGroupWiseOutstandingBillCount,
} from "../../../services/outstandingBillCountService";
import "./OutstandingBillCount.css";

const formatAmount = (value) =>
  Number(value || 0).toLocaleString("en-IN", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });

function OutstandingBillCount() {
  const [companies, setCompanies] = useState([]);
  const [companyId, setCompanyId] = useState("");
  const [view, setView] = useState("party");
  const [search, setSearch] = useState("");

  const [partyData, setPartyData] = useState(null);
  const [groupData, setGroupData] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const [expandedGroups, setExpandedGroups] = useState([]);

  const [selectedParty, setSelectedParty] = useState(null);
  const [billDetails, setBillDetails] = useState(null);
  const [detailsLoading, setDetailsLoading] = useState(false);
  const [detailsError, setDetailsError] = useState("");

  // LOAD COMPANIES
  useEffect(() => {
    let active = true;

    async function loadCompanies() {
      try {
        const result = await getDbCompanies();
        if (!active) return;

        const list = Array.isArray(result)
          ? result
          : Array.isArray(result?.companies)
            ? result.companies
            : [];

        setCompanies(list);
      } catch (err) {
        if (active) {
          setError(err.message || "Unable to load companies.");
        }
      }
    }

    loadCompanies();

    return () => {
      active = false;
    };
  }, []);

  // LOAD SELECTED VIEW
  useEffect(() => {
    let active = true;

    setError("");
    setLoading(Boolean(companyId));

    if (view === "party") {
      setPartyData(null);
    } else {
      setGroupData(null);
    }

    if (!companyId) {
      setLoading(false);
      return () => {
        active = false;
      };
    }

    async function loadReport() {
      try {
        if (view === "party") {
          const result = await getOutstandingBillCount(companyId);
          if (active) setPartyData(result);
        } else {
          const result = await getGroupWiseOutstandingBillCount(companyId);
          if (active) setGroupData(result);
        }
      } catch (err) {
        if (active) {
          setError(err.message || "Unable to load outstanding report.");
        }
      } finally {
        if (active) setLoading(false);
      }
    }

    loadReport();

    return () => {
      active = false;
    };
  }, [companyId, view]);

  const selectedCompany = companies.find(
    (company) => String(company.id) === String(companyId),
  );

  const keyword = search.trim().toLowerCase();

  const filteredParties = useMemo(() => {
    return (partyData?.parties || []).filter((party) =>
      String(party.ledgerName || "")
        .toLowerCase()
        .includes(keyword),
    );
  }, [partyData, keyword]);

  // SEARCH GROUP NAME OR A PARTY WITHIN GROUP
  const filteredGroups = useMemo(() => {
    return (groupData?.groups || []).filter((group) => {
      const groupMatches = String(group.groupName || "")
        .toLowerCase()
        .includes(keyword);

      const partyMatches = (group.parties || []).some((party) =>
        String(party.ledgerName || "")
          .toLowerCase()
          .includes(keyword),
      );

      return groupMatches || partyMatches;
    });
  }, [groupData, keyword]);

  const activeData = view === "party" ? partyData : groupData;

  function closeBillDetails() {
    setSelectedParty(null);
    setBillDetails(null);
    setDetailsError("");
  }

  function changeCompany(event) {
    setCompanyId(event.target.value);
    setSearch("");
    setExpandedGroups([]);
    closeBillDetails();
  }

  function changeView(nextView) {
    setView(nextView);
    setSearch("");
    setExpandedGroups([]);
    closeBillDetails();
  }

  function toggleGroup(groupName) {
    setExpandedGroups((previous) =>
      previous.includes(groupName)
        ? previous.filter((name) => name !== groupName)
        : [...previous, groupName],
    );
  }

  // BILL DETAILS POPUP
  async function openBillDetails(party) {
    if (!companyId) return;

    const requestedCompanyId = companyId;
    const requestedLedger = party.ledgerName;

    setSelectedParty({
      ...party,
      companyId: requestedCompanyId,
      requestLedger: requestedLedger,
    });
    setBillDetails(null);
    setDetailsError("");
    setDetailsLoading(true);
  }

  // FETCH DETAILS WHEN POPUP SELECTION CHANGES
  useEffect(() => {
    if (!selectedParty) return;

    let active = true;

    async function loadDetails() {
      try {
        const result = await getOutstandingBillDetails(
          selectedParty.companyId,
          selectedParty.requestLedger,
        );

        if (active) setBillDetails(result);
      } catch (err) {
        if (active) {
          setDetailsError(
            err.message || "Unable to fetch outstanding bill details.",
          );
        }
      } finally {
        if (active) setDetailsLoading(false);
      }
    }

    loadDetails();

    return () => {
      active = false;
    };
  }, [selectedParty]);

  useEffect(() => {
    if (!selectedParty) return;

    function onEscape(event) {
      if (event.key === "Escape") closeBillDetails();
    }

    document.addEventListener("keydown", onEscape);

    return () => {
      document.removeEventListener("keydown", onEscape);
    };
  }, [selectedParty]);

  const totalBills = activeData?.totalBills ?? 0;
  const totalAmount = activeData?.totalOutstanding ?? 0;

  return (
    <div className="obc-page">
      {/* HEADER */}
      <div className="obc-header">
        <div>
          <h1>Outstanding Bill Count</h1>
          <p>Company, party and group-wise pending bill summary</p>
        </div>
      </div>

      {/* VIEW SELECTOR */}
      <div className="obc-view-switch" role="group" aria-label="Report view">
        <button
          type="button"
          className={view === "party" ? "active" : ""}
          aria-pressed={view === "party"}
          onClick={() => changeView("party")}
        >
          Party / Ledger-wise
        </button>
        <button
          type="button"
          className={view === "group" ? "active" : ""}
          aria-pressed={view === "group"}
          onClick={() => changeView("group")}
        >
          Group-wise
        </button>
      </div>

      {/* FILTERS */}
      <div className="obc-filters">
        <div className="obc-field">
          <label htmlFor="obc-company">Company</label>
          <select id="obc-company" value={companyId} onChange={changeCompany}>
            <option value="">Select Company</option>
            {companies.map((company) => (
              <option key={company.id} value={company.id}>
                {company.name}
              </option>
            ))}
          </select>
        </div>

        <div className="obc-field">
          <label htmlFor="obc-search">
            {view === "party" ? "Party Search" : "Group / Party Search"}
          </label>
          <input
            id="obc-search"
            type="search"
            value={search}
            placeholder={
              view === "party"
                ? "Search party name..."
                : "Search group or party..."
            }
            onChange={(event) => setSearch(event.target.value)}
          />
        </div>
      </div>

      {/* SUMMARY */}
      <div className="obc-summary">
        <div className="obc-summary-card">
          <span>Total Outstanding Bills</span>
          <strong>{loading ? "—" : totalBills}</strong>
        </div>
        <div className="obc-summary-card">
          <span>Total Outstanding Amount</span>
          <strong>{loading ? "—" : `₹ ${formatAmount(totalAmount)}`}</strong>
        </div>
      </div>

      {error && (
        <div className="obc-error" role="alert">
          {error}
        </div>
      )}

      {/* PARTY-WISE TABLE */}
      {view === "party" && (
        <div className="obc-table-container">
          <div className="obc-table-scroll">
            <table className="obc-table">
              <thead>
                <tr>
                  <th>S.No</th>
                  <th>Party / Ledger Name</th>
                  <th className="obc-number">Outstanding Bills</th>
                  <th className="obc-number">Outstanding Amount</th>
                </tr>
              </thead>

              <tbody>
                {loading || !companyId || filteredParties.length === 0 ? (
                  <tr>
                    <td colSpan="4" className="obc-empty">
                      {loading
                        ? "Loading outstanding bills..."
                        : !companyId
                          ? "Please select a company."
                          : error
                            ? "Unable to load outstanding bills."
                            : "No outstanding bills found."}
                    </td>
                  </tr>
                ) : (
                  filteredParties.map((party, index) => (
                    <tr key={party.ledgerName}>
                      <td>{index + 1}</td>
                      <td className="obc-party-name">{party.ledgerName}</td>
                      <td className="obc-number">
                        <button
                          type="button"
                          className="obc-count-button"
                          onClick={() => openBillDetails(party)}
                        >
                          {party.outstandingBillCount}
                        </button>
                      </td>
                      <td className="obc-number">
                        ₹ {formatAmount(party.outstandingAmount)}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <div className="obc-footer">
            <span>Showing {filteredParties.length} parties</span>
            <strong>Total Bills: {totalBills}</strong>
          </div>
        </div>
      )}

      {/* GROUP-WISE TABLE */}
      {view === "group" && (
        <div className="obc-table-container">
          <div className="obc-table-scroll">
            <table className="obc-table obc-group-table">
              <thead>
                <tr>
                  <th>S.No</th>
                  <th>Group / Party Name</th>
                  <th className="obc-number">Total Parties</th>
                  <th className="obc-number">Outstanding Bills</th>
                  <th className="obc-number">Outstanding Amount</th>
                </tr>
              </thead>

              <tbody>
                {loading || !companyId || filteredGroups.length === 0 ? (
                  <tr>
                    <td colSpan="5" className="obc-empty">
                      {loading
                        ? "Loading group-wise outstanding..."
                        : !companyId
                          ? "Please select a company."
                          : error
                            ? "Unable to load group-wise outstanding."
                            : "No groups found."}
                    </td>
                  </tr>
                ) : (
                  filteredGroups.map((group, index) => {
                    const expanded = expandedGroups.includes(group.groupName);

                    const matchingParties =
                      keyword &&
                      !group.groupName.toLowerCase().includes(keyword)
                        ? (group.parties || []).filter((party) =>
                            party.ledgerName.toLowerCase().includes(keyword),
                          )
                        : group.parties || [];

                    return (
                      <FragmentRow
                        key={group.groupName}
                        group={group}
                        index={index}
                        expanded={expanded}
                        matchingParties={matchingParties}
                        onToggle={() => toggleGroup(group.groupName)}
                        onPartyClick={openBillDetails}
                      />
                    );
                  })
                )}
              </tbody>
            </table>
          </div>

          <div className="obc-footer">
            <span>Showing {filteredGroups.length} groups</span>
            <strong>Total Bills: {totalBills}</strong>
          </div>
        </div>
      )}

      {/* BILL DETAILS MODAL */}
      {selectedParty && (
        <div
          className="obc-modal-overlay"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) {
              closeBillDetails();
            }
          }}
        >
          <div
            className="obc-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="obc-modal-title"
          >
            <div className="obc-modal-header">
              <div>
                <h2 id="obc-modal-title">Outstanding Bill Details</h2>
                <p>
                  {selectedParty.ledgerName}
                  {selectedCompany ? ` · ${selectedCompany.name}` : ""}
                </p>
              </div>
              <button
                type="button"
                className="obc-modal-close"
                aria-label="Close"
                onClick={closeBillDetails}
              >
                ×
              </button>
            </div>

            <div className="obc-modal-summary">
              <div>
                <span>Total Pending Bills</span>
                <strong>
                  {billDetails?.totalBills ??
                    selectedParty.outstandingBillCount}
                </strong>
              </div>
              <div>
                <span>Total Pending Amount</span>
                <strong>
                  ₹{" "}
                  {formatAmount(
                    billDetails?.totalPending ??
                      selectedParty.outstandingAmount,
                  )}
                </strong>
              </div>
            </div>

            <div className="obc-modal-table-wrap">
              <table className="obc-modal-table">
                <thead>
                  <tr>
                    <th>S.No</th>
                    <th>Name</th>
                    <th>Voucher Number</th>
                    <th className="obc-number">Opening Amount</th>
                    <th className="obc-number">Pending Amount</th>
                    <th className="obc-number">Overdue Days</th>
                  </tr>
                </thead>
                <tbody>
                  {detailsLoading ||
                  detailsError ||
                  !billDetails?.bills?.length ? (
                    <tr>
                      <td colSpan="6" className="obc-empty">
                        {detailsLoading
                          ? "Loading bill details..."
                          : detailsError || "No pending bill details found."}
                      </td>
                    </tr>
                  ) : (
                    billDetails.bills.map((bill, index) => (
                      <tr key={`${bill.billReference}-${index}`}>
                        <td>{index + 1}</td>
                        <td>{bill.ledgerName}</td>
                        <td>{bill.voucherNumber || "—"}</td>
                        <td className="obc-number">
                          ₹ {formatAmount(bill.openingAmount)}
                        </td>
                        <td className="obc-number">
                          ₹ {formatAmount(bill.pendingAmount)}
                        </td>
                        <td className="obc-number">
                          {bill.overdueDays ?? "—"}
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>

            <div className="obc-modal-footer">
              <span>{billDetails?.bills?.length ?? 0} bill records</span>
              <button
                type="button"
                className="obc-modal-done"
                onClick={closeBillDetails}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// EXPANDABLE GROUP AND PARTY ROWS
function FragmentRow({
  group,
  index,
  expanded,
  matchingParties,
  onToggle,
  onPartyClick,
}) {
  return (
    <>
      <tr className="obc-group-row">
        <td>{index + 1}</td>
        <td>
          <button
            type="button"
            className="obc-expand-button"
            onClick={onToggle}
            aria-expanded={expanded}
          >
            <span className="obc-expand-icon">{expanded ? "▾" : "▸"}</span>
            {group.groupName}
          </button>
        </td>
        <td className="obc-number">{group.totalParties}</td>
        <td className="obc-number">
          <button
            type="button"
            className="obc-count-button"
            onClick={onToggle}
            title="Show parties in this group"
          >
            {group.outstandingBillCount}
          </button>
        </td>
        <td className="obc-number">
          ₹ {formatAmount(group.outstandingAmount)}
        </td>
      </tr>

      {expanded &&
        matchingParties.map((party, partyIndex) => (
          <tr
            className="obc-child-row"
            key={`${group.groupName}-${party.ledgerName}`}
          >
            <td>{`${index + 1}.${partyIndex + 1}`}</td>
            <td className="obc-child-name">↳ {party.ledgerName}</td>
            <td className="obc-number">—</td>
            <td className="obc-number">
              <button
                type="button"
                className="obc-count-button"
                onClick={() => onPartyClick(party)}
                title="View pending bills"
              >
                {party.outstandingBillCount}
              </button>
            </td>
            <td className="obc-number">
              ₹ {formatAmount(party.outstandingAmount)}
            </td>
          </tr>
        ))}
    </>
  );
}

export default OutstandingBillCount;
