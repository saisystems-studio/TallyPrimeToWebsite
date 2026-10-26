import test from "node:test";
import assert from "node:assert/strict";
import { salesRegisterTotals, salesVoucherTypes } from "./salesRegisterModel.js";

test("Sales classification includes custom voucher types by Sales parent", () => {
  assert.deepEqual(salesVoucherTypes([
    { name: "Sales", parent: "Sales" },
    { name: "Retail Invoice", parent: "Sales" },
    { name: "Purchase", parent: "Purchase" },
  ]), ["Sales", "Retail Invoice"]);
});

test("register totals use signed party postings for debit, credit, and closing", () => {
  assert.deepEqual(salesRegisterTotals([
    { partyAmount: -100.25 },
    { partyAmount: 20 },
  ]), { debit: 20, credit: 100.25, closing: 80.25 });
});

test("incomplete party postings remain unavailable instead of becoming zero", () => {
  assert.deepEqual(salesRegisterTotals([{ partyAmount: null }]), {
    debit: null, credit: null, closing: null,
  });
});
