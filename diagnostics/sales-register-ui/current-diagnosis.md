# Sales Register diagnosis — 22 October 2026

## Verified database identity
Both `.\SQLEXPRESS` and `SAI_DEV_2\SQLEXPRESS`, queried independently with Windows authentication, returned `@@SERVERNAME = Sai_Dev_2\SQLEXPRESS` and `DB_NAME() = TallyWebDB`. MachineName is Sai_Dev_2, InstanceName SQLEXPRESS. sys.tables joined to sys.schemas confirms dbo.SalesRegister_tbl and dbo.SalesRegisterEntries_tbl exist.

Whole-table counts: 103 headers; 250 postings. All headers span 2024-04-01 through 2024-04-30, total INR 4,241,929.10. Company-filtered April counts independently match 103/250 and that total.

Company Id 1 maps to JAYA JOTHI MALIGAI, GUID 5bd36bbb-820e-4784-b692-fe9891019a11. Company StartingFrom is 20210401.

## Running API and frontend
API PID 13760 runs TallyWebAPI/TallyWebAPI/bin/Debug/net10.0/TallyWebAPI.exe, listening on HTTPS 7108 and HTTP 5228. SQL sys.dm_exec_sessions associates host_process_id 13760 with TallyWebDB, confirming the running process's actual database. Assembly version 1.0.0.0; DLL modified 22 October 2026 18:01:54 local time, SHA256 DDAABB19C37D49370F380CBCF42E6958226497E52E5C071BFC92A21A1DC6B412. Swagger exposes the SQL-backed Sales Register endpoint; current authenticated GET responses match SQL.

Source Program.cs reads ConnectionStrings:DefaultConnection using the standard ASP.NET configuration pipeline. Source and runtime-output appsettings.json specify Server=.\SQLEXPRESS;Database=TallyWebDB;Trusted_Connection=True;TrustServerCertificate=True;. appsettings.Development.json has no connection override. Effective database independently proven through the running PID's SQL sessions; process environment overrides were not inspected.

At investigation start, the prior temporary frontend 5174 and API 5230 were not listening. Existing frontend 5173 serves current workspace code and defaults to https://localhost:7108/api. Started an additional hidden Vite frontend on 5174, explicitly setting VITE_API_BASE_URL=https://localhost:7108/api. GET /reports/sales-register is HTTP 200; served /src/services/api.js confirms the effective URL. No API restart or rebuild was performed. Running Debug API binary is newer than the previous temporary API binary (15:58:44); no byte-for-byte fresh-build comparison is claimed.

## Current authenticated API results
GET https://localhost:7108/api/sales-register?companyId=1&fromDate=20240401&toDate=20240430 returns HTTP 200, totalRecords 103, 250 nested postings, amount sum INR 4,241,929.10, availableFrom 20240401, availableTo 20240430. Full response saved as april-api-current.json; no authentication token is saved.

This Year (20260401–20270331) returns HTTP 200, 0 records. Last Year (20250401–20260331) returns HTTP 200, 0 records. All Available Data (dates omitted) returns HTTP 200, 103 records, 250 postings, same total and April bounds. See current-api-summary.json.

## Root cause and safe correction
The screenshot selects financial year 2026–27 while all stored Sales Register vouchers belong to April 2024. Empty results for that selected year are correct. Select Custom Date Range, From 2024-04-01, To 2024-04-30, or All Available Data. Click April 2024 to drill down; current pagination makes all vouchers accessible over 50 + 50 + 3 rows.

No SSMS process or active SSMS SQL session was present. The exact database previously selected in SSMS therefore cannot be established. A stale Object Explorer listing, table filter, or different selected database remain possibilities, not proven causes. Connect SSMS to Sai_Dev_2\SQLEXPRESS, expand Databases > TallyWebDB > Tables, refresh Tables and clear any Object Explorer filter. Both tables are independently verified in that exact database; recreation or reinsertion is unnecessary.

## Verification and limitations
Executed verify-current.mjs against the fresh API response and actual frontend date-filter function. Passed April count/posting/total checks, three-page pagination, This Year, Last Year, financial-year rollover, Custom Date Range and unbounded All Available Data checks. No production company ID, count, amount or date was hardcoded or changed.

Browser inventory is empty and opening the in-app browser failed with “Browser is not available: iab”. Could not select dates or visually confirm April summary/drill-down, white container, internal scrolling or anchored footer. Existing scoped CSS preserves the flex-height container, internal overflow and non-shrinking footer; CSS was not modified. Data and API are verified; end-to-end visual verification remains pending.

No SQL schema/data writes, table creation, synchronization requests, Windows Sync Agent restarts, VPS access, or production-source modifications were performed. Only diagnostic artifacts and an additional local frontend process were created. Prior working-tree changes were retained.
