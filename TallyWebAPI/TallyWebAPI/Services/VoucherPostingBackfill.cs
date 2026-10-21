using TallyWebAPI.DTOs;
using TallyWebAPI.Models;

namespace TallyWebAPI.Services;

// Add-only repair: never replace existing financial postings on a best guess.
public static class VoucherPostingBackfill
{
    public static int Apply(VoucherEntity voucher, List<VoucherPostingDto> postings, List<LedgerEntity> ledgers)
    {
        if (postings.Count == 0 || postings.Sum(x => x.Amount) != 0)
            throw new InvalidOperationException("Voucher postings are missing or unbalanced; backfill stopped.");
        var mapped = postings.Select(p =>
        {
            // Voucher XML supplies ledger names; resolve against this company's
            // GUID-bearing master records only. Ambiguity must never pick First().
            var matches = ledgers.Where(l => l.CompanyId == voucher.CompanyId &&
                string.Equals(l.Name.Trim(), p.LedgerName.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count != 1 || string.IsNullOrWhiteSpace(matches[0].TallyGuid))
                throw new InvalidOperationException($"Ledger mapping missing or ambiguous for '{p.LedgerName}'. Backfill stopped.");
            return new VoucherLedgerEntryEntity {
                LedgerId = matches[0].Id, LedgerName = matches[0].Name,
                Amount = p.Amount, EntryIndex = p.EntryIndex
            };
        }).ToList();
        if (voucher.LedgerEntries.Count > 0)
        {
            var existing = voucher.LedgerEntries.OrderBy(x => x.EntryIndex).ToList();
            var incoming = mapped.OrderBy(x => x.EntryIndex).ToList();
            if (existing.Count != incoming.Count || existing.Where((entry, i) =>
                    entry.EntryIndex != incoming[i].EntryIndex || entry.LedgerId != incoming[i].LedgerId ||
                    entry.Amount != incoming[i].Amount || entry.LedgerName != incoming[i].LedgerName).Any())
                throw new InvalidOperationException("Existing voucher postings differ from Tally; no financial data was overwritten.");
            return 0;
        }
        voucher.LedgerEntries.AddRange(mapped);
        return mapped.Count;
    }
}
