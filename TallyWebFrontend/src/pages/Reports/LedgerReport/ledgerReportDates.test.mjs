import assert from "node:assert/strict";
import test from "node:test";
import { resolveLedgerReportRange } from "./ledgerReportDates.js";
import { formatTallySignedBalance } from "./ledgerReportBalance.js";

const history = ["20240401", "20240715", "20250331"];

test("blank dates resolve to the full available company history", () => {
  assert.deepEqual(resolveLedgerReportRange("", "", history), { fromDate: "20240401", toDate: "20250331" });
});

test("From-only keeps the selected start and resolves the latest available date", () => {
  assert.deepEqual(resolveLedgerReportRange("2024-07-01", "", history), { fromDate: "20240701", toDate: "20250331" });
});

test("To-only resolves the earliest available date and keeps the selected end", () => {
  assert.deepEqual(resolveLedgerReportRange("", "2024-12-31", history), { fromDate: "20240401", toDate: "20241231" });
});

test("two dates remain the exact inclusive range", () => {
  assert.deepEqual(resolveLedgerReportRange("2024-08-01", "2024-08-31", history), { fromDate: "20240801", toDate: "20240831" });
});

test("missing synchronized dates and reversed ranges produce clear errors", () => {
  assert.throws(() => resolveLedgerReportRange("", "", []), /No synchronized voucher dates/);
  assert.throws(() => resolveLedgerReportRange("2025-01-01", "2024-01-01", history), /later than the To Date/);
});

test("Tally negative signed balance is displayed as debit", () => {
  assert.equal(formatTallySignedBalance(-1103283), "11,03,283.00 Dr");
  assert.equal(formatTallySignedBalance(-7058539), "70,58,539.00 Dr");
  assert.equal(formatTallySignedBalance(5000), "5,000.00 Cr");
});
