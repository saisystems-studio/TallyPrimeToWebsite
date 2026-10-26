# Sales Register verification — 22 October 2026

## Local runtime

- Latest API built successfully into `tmp/sales-register-ui-api` and started at http://localhost:5230 in Development, with `Startup:RunLegacyDatabaseInitialization=false`. Development excludes automatic synchronization.
- Latest frontend started at http://localhost:5174/reports/sales-register with `VITE_API_BASE_URL=http://localhost:5230/api`. Route returns HTTP 200; served API module confirms this URL.
- Existing API on port 5228 returned 404 for the independent Sales Register endpoint. Existing services were left running.
- Frontend production build passed after the CSS change (86 modules).

## Read-only data verification

Company: JAYA JOTHI MALIGAI (company ID 1). Period: 1–30 April 2024.

| Check | Result |
| --- | --- |
| API voucher count | 103 |
| Distinct voucher GUIDs | 103 |
| Ledger posting count | 250 |
| Sales total | INR 4,241,929.10 |
| Missing party names / voucher numbers | 0 / 0 |
| Dates within April | Pass |
| SQL/API field comparison | 103 records; 0 mismatches |
| Frontend totals model | Debit 4,241,929.10; Credit 0.00; Closing -4,241,929.10 |
| Frontend pagination arithmetic | 50 + 50 + 3 vouchers |

SQL comparison used only SELECT and matched ID, GUID, date, voucher number, party name and amount. Evidence: `april-api.json` and `sql-comparison.json`. The frontend totals model was executed against the saved API response with Node assertions. This verifies the model, not rendered browser output. Accounting signs were preserved.

Company/date filtering: company 2 for April returned 0 vouchers; company 1 for May returned 0; company 1 for 1 April returned 8. Source loads company choices from `/api/db/companies`, derives financial-year presets from company settings, and passes selected IDs and date ranges to the API. No target company or April values are hardcoded in the Sales Register component/service.

## Change made

Only `TallyWebFrontend/src/pages/Reports/SalesRegister/SalesRegister.css` was edited by this verification task. The page's viewport-based minimum height conflicted with the dashboard's constrained content space, especially with mobile navigation. Scoped rules now fit the page to its parent, allow the grid to shrink and scroll, and preserve control/footer height. Browser visual confirmation remains pending.

## Export inspection and remaining checks

No browser surfaces were available: browser inventory was empty, and opening the in-app browser reported it unavailable. No screenshots or interactive browser pass are claimed.

- Open April summary and verify its displayed total; click April and traverse all three pages.
- Visually check desktop/mobile alignment, white container, grid scrolling and footer accessibility, including short screens.
- Compare Debit/Credit/Closing presentation with the actual TallyPrime Sales Register. Negative party postings are currently presented as Debit; no signs were changed to force agreement.
- Excel currently downloads an HTML table with `.xls` extension, rather than a native Excel workbook. Test opening it in Excel and any format warning.
- PDF and Print both use the browser print dialog. PDF requires Save as PDF; no direct PDF generation exists here. Test dialog content and all 103 voucher rows after drilling down.
- Share uses `navigator.share`, falling back to clipboard. Test success/cancellation and clipboard permissions locally; no recipient was contacted.

No SQL data/schema was changed, no synchronization endpoint was called, no Windows Sync Agent was restarted, and no VPS was accessed. Pre-existing working-tree changes were retained.
