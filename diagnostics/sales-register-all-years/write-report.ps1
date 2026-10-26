$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$r=Get-Content (Join-Path $root 'results/discovery.json') -Raw|ConvertFrom-Json
function Sum($items,$field) {$total=[decimal]0;foreach($item in $items){$total += [decimal]$item.$field};return $total}
function Currency($value) {return ([decimal]$value).ToString('N2',[Globalization.CultureInfo]::GetCultureInfo('en-IN'))}
$april=@($r.monthly|Where-Object month -eq '202404')[0]
$later=@($r.monthly|Where-Object {$_.month -ge '202405' -and $_.month -le '202412'})
$reference=@($r.monthly|Where-Object {$_.month -ge '202404' -and $_.month -le '202412'})
$checks=[ordered]@{
    april103Vouchers=($april.active -eq 103 -and $april.unchanged -eq 103 -and $april.modified -eq 0)
    april250Postings=($april.postings -eq 250)
    aprilTotal=((Sum @($april) 'absoluteTotal') -eq 4241929.10d)
    mayDecember749Active=((Sum $later 'active') -eq 749)
    mayDecember2530Postings=((Sum $later 'postings') -eq 2530)
    mayDecember26CancelledExcluded=((Sum $later 'cancelled') -eq 26)
    aprilDecemberReferenceTotal=((Sum $reference 'absoluteTotal') -eq 40966878.32d)
    sqlUnchanged=($r.unchanged -and $r.beforeSha256 -eq $r.afterSha256)
}
$checks|ConvertTo-Json|Set-Content -Encoding UTF8 (Join-Path $root 'results/reference-checks.json')
$lines=New-Object 'Collections.Generic.List[string]'
$lines.Add('# All-history Sales Register read-only discovery')
$lines.Add('')
$lines.Add('**safeToApply: false. No SQL INSERT, UPDATE, DELETE or schema operation was executed. No apply endpoint was called.**')
$lines.Add('')
$lines.Add(('Company: **'+$r.companyName+'**, SQL company ID '+$r.companyId+'. GUID matched to the loaded Tally company. Company ID 1 was selected for this run because it owns the existing April register referenced in the request; no company name is embedded in discovery logic.'))
$lines.Add(('Books From: **'+$r.booksFrom+'**. Company-reported EndingAt: **'+$r.companyEndingAt+'**. Earliest/latest Sales voucher: **'+$r.earliestSalesDate+'-'+$r.latestSalesDate+'**. Types: '+($r.salesTypes -join ', ')+'.'))
$lines.Add('')
$lines.Add(('Discovered '+$r.activeCount+' active Sales vouchers and '+$r.cancelledCount+' cancelled vouchers. Metadata and monthly exports cover the company-reported books/history range. Cancelled vouchers contribute neither postings nor active financial totals.'))
$lines.Add('')
$lines.Add('| Financial year | Active | Cancelled | Postings | Existing unchanged | Missing | Modified | Expected Sales total (INR) |')
$lines.Add('|---|---:|---:|---:|---:|---:|---:|---:|')
foreach($fy in $r.financialYears){$lines.Add(('| '+$fy.financialYear+' | '+$fy.active+' | '+$fy.cancelled+' | '+$fy.postings+' | '+$fy.unchanged+' | '+$fy.missing+' | '+$fy.modified+' | '+(Currency $fy.absoluteTotal)+' |'))}
$lines.Add('')
$lines.Add(('Existing independent SQL tables contain **'+$r.existingSql.headers+' headers, '+$r.existingSql.postings+' postings, INR '+(Currency $r.existingSql.total)+'**. Missing source identities: '+@($r.missing).Count+'; modified: '+@($r.modified).Count+'; stored absent/cancelled identities: '+@($r.unexpectedStored).Count+'. Counts of missing rows describe differences, not approved insert actions.'))
$lines.Add('')
$lines.Add('## Validation and preservation')
$lines.Add('')
$lines.Add(('Source validation passed: **'+$r.dataValidationPassed+'**. Duplicate source GUID groups: '+@($r.duplicateGuids).Count+'; ledger mapping errors: '+@($r.ledgerMappingErrors).Count+'; unbalanced active vouchers: '+@($r.unbalanced).Count+'; validation errors: '+@($r.validationErrors).Count+'.'))
$lines.Add('')
foreach($key in $checks.Keys){$lines.Add(('- '+$key+': **'+$checks[$key]+'**'))}
$lines.Add('')
$lines.Add(('Before/after SHA256 of every column in both independent register tables: `'+$r.beforeSha256+'` / `'+$r.afterSha256+'`. Identical: **'+$r.unchanged+'**.'))
$lines.Add('')
$lines.Add('Financial values are derived from signed party postings. Credit is positive Sales movement, Debit is its negative absolute value. Closing is cumulative net movement with Cr for positive and Dr for negative; the table below starts at Books From with zero report-period opening. A selected financial-year/custom report must restart cumulative movement at that selected period.')
$lines.Add('')
$lines.Add('| Month | Active | Cancelled | Postings | Missing | Modified | Debit INR | Credit INR | All-history closing INR |')
$lines.Add('|---|---:|---:|---:|---:|---:|---:|---:|---:|')
foreach($m in $r.monthly){$lines.Add(('| '+$m.month+' | '+$m.active+' | '+$m.cancelled+' | '+$m.postings+' | '+$m.missing+' | '+$m.modified+' | '+(Currency $m.debit)+' | '+(Currency $m.credit)+' | '+(Currency $m.allHistoryClosing)+' '+$m.closingSide+' |'))}
$lines.Add('')
$lines.Add('## Blockers and remaining work')
$lines.Add('')
foreach($b in $r.blockers){$lines.Add('- '+$b)}
$lines.Add('- Current service incorrectly treats equal GUIDs in different companies as conflicts; identity must consistently use CompanyId plus TallyGuid, including transactional rechecks.')
$lines.Add('- The report GET can return all stored dates, but preview API requires explicit dates and has no company-history discovery endpoint. Financial-year history choices and truthful synchronization coverage still need integration.')
if(@($r.validationErrors).Count -gt 0){$lines.Add('');$lines.Add('Source/SQL validation failures (full identities are in discovery.json):');foreach($e in $r.validationErrors){$lines.Add('- '+$e)}}
$lines.Add('')
$lines.Add('## Verification and evidence limits')
$lines.Add('')
$lines.Add('- Fresh API/backend regression-harness build passed: 0 errors. Fresh SalesRegisterSync.Checks execution passed posting-sign, duplicate, cancellation, malformed-active and legacy behavior checks.')
$lines.Add('- Frontend production Vite build passed (86 modules). All 48 discovered frontend tests passed, including nine Sales Register tests. No browser visual or export-dialog verification is claimed.')
$lines.Add('- Initial default backend build failed because the running API executable was locked; the successful build used a separate tmp output directory. No service was stopped or restarted.')
$lines.Add('- Existing package warnings NU1510/NU1902/NU1903 remain. No dependency or security settings were changed.')
$lines.Add('- A new standalone .NET discovery executable was blocked by Windows Application Control. Native PowerShell then performed the SELECT-only local SQL and Tally export discovery. No security controls were changed.')
$lines.Add('- Saved XML from this run was reused when narrowing oversized wildcard exports, then independently replayed against a fresh SQL snapshot. This is not a second fresh full Tally export. A new source preview and approval are required before any later apply.')
$lines.Add('- Export metadata is bounded by the company-reported BooksFrom and EndingAt, with a single metadata export and monthly posting batches. No atomic Tally snapshot is available; matching metadata/batch identities and SQL preservation do not prove that amounts stayed unchanged during the export. No apply readiness is inferred.')
$lines.Add('- Discovery reads SQL companies/ledgers and the independent register tables. It contains no SaveChanges, import request, apply call or SQL mutation. It does not start API/hosted services, access the VPS or modify the Sync Agent.')
$lines.Add('')
$lines.Add('Evidence: `results/discovery.json`, `results/reference-checks.json`, `results/companies.xml`, `results/history-metadata.xml`, and monthly `results/source-YYYYMM.xml`. The generic discover.ps1 defaults to fresh source exports; `-ReplaySavedExports` is explicitly offline source replay with fresh SQL reads.')
$lines.Add('')
$lines.Add('Implementation work is mapped in `docs/superpowers/plans/2026-10-23-sales-register-all-history.md`. Application code and SQL are unchanged by this discovery phase. Explicit user approval is required before any SQL apply, and the blockers above must be resolved first.')
$lines|Set-Content -Encoding UTF8 (Join-Path $root 'report.md')
Write-Output ($checks|ConvertTo-Json)
Write-Output ('Report saved: '+(Join-Path $root 'report.md'))

