namespace TallyWebAPI.Services
{
    /// <summary>
    /// Applies ledger activity to Tally's signed balance convention:
    /// debit balances are negative and credit balances are positive.
    /// </summary>
    public static class LedgerBalanceMath
    {
        public static decimal CalculateClosingBalance(
            decimal openingBalance,
            decimal totalDebit,
            decimal totalCredit) => openingBalance - totalDebit + totalCredit;
    }
}
