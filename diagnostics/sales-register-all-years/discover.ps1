param([int]$CompanyId = 0, [switch]$ReplaySavedExports)
$ErrorActionPreference = 'Stop'
$out = Join-Path $PSScriptRoot 'results'
[void](New-Item -ItemType Directory -Force -Path $out)
$settings = Get-Content TallyWebAPI/TallyWebAPI/appsettings.json -Raw | ConvertFrom-Json
$sqlSettings = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $settings.ConnectionStrings.DefaultConnection
if ($sqlSettings.DataSource -notmatch '^(\.|localhost|127\.0\.0\.1|\(local\)|\(localdb\))([\\,]|$)' -and $sqlSettings.DataSource.Split('\')[0] -ne $env:COMPUTERNAME) { throw 'Local SQL only.' }
$uri = [uri]$settings.Tally.BaseUrl
if (!$uri.IsLoopback) { throw 'Local Tally only.' }
$connection = New-Object System.Data.SqlClient.SqlConnection $settings.ConnectionStrings.DefaultConnection
function Read-Sql([string]$query) {
    if ($query -notmatch '^SELECT\s' -or $query -match ';') { throw 'Only a single SELECT is allowed.' }
    $command = $connection.CreateCommand(); $command.CommandText = $query
    $table = New-Object System.Data.DataTable
    $table.Load($command.ExecuteReader())
    return ,$table
}
function Snapshot {
    $table = Read-Sql 'SELECT (SELECT * FROM dbo.SalesRegister_tbl ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES) + (SELECT * FROM dbo.SalesRegisterEntries_tbl ORDER BY Id FOR JSON PATH, INCLUDE_NULL_VALUES) AS Snapshot'
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes([string]$table.Rows[0].Snapshot))).Replace('-','') } finally { $sha.Dispose() }
}
function Export-Tally([string]$id, [string]$type, [string]$fetch, [string]$variables) {
    $cache = if ($id -eq 'DiscoveryHistory') { Join-Path $out 'history-metadata.xml' } elseif ($id -match '^Discovery(\d{6})$') { Join-Path $out ('source-'+$Matches[1]+'.xml') } else { $null }
    if ($ReplaySavedExports -and $cache -and (Test-Path -LiteralPath $cache)) {
        $cached=New-Object System.Xml.XmlDocument; $cached.Load($cache); return ,$cached
    }
    $request = '<ENVELOPE><HEADER><VERSION>1</VERSION><TALLYREQUEST>Export</TALLYREQUEST><TYPE>Collection</TYPE><ID>'+ $id +'</ID></HEADER><BODY><DESC><STATICVARIABLES><SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT>'+ $variables +'</STATICVARIABLES><TDL><TDLMESSAGE><COLLECTION NAME="'+ $id +'"><TYPE>'+ $type +'</TYPE><FETCH>'+ $fetch +'</FETCH></COLLECTION></TDLMESSAGE></TDL></DESC></BODY></ENVELOPE>'
    $response = Invoke-WebRequest $uri -Method Post -ContentType 'text/xml; charset=utf-16' -Body ([Text.Encoding]::Unicode.GetBytes($request)) -UseBasicParsing -TimeoutSec 60
    $bytes = $response.RawContentStream.ToArray()
    $raw = if ($bytes.Length -gt 1 -and ($bytes[0] -eq 255 -or $bytes[1] -eq 0)) { [Text.Encoding]::Unicode.GetString($bytes) } else { [Text.Encoding]::UTF8.GetString($bytes) }
    $raw = $raw.TrimStart([char]0xFEFF) -replace '&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);',''
    # Match the existing voucher reader's handling of Tally's undeclared UDF prefix.
    $raw = $raw -replace '(<\/?)UDF:', '$1'
    $xmlSettings=New-Object System.Xml.XmlReaderSettings
    $xmlSettings.MaxCharactersInDocument=0
    $xmlSettings.DtdProcessing=[System.Xml.DtdProcessing]::Prohibit
    $stringReader=New-Object System.IO.StringReader $raw
    $reader=[System.Xml.XmlReader]::Create($stringReader,$xmlSettings)
    $doc=New-Object System.Xml.XmlDocument
    try {$doc.Load($reader)} catch {throw ('Tally XML parsing failed: '+$_.Exception.GetBaseException().Message)} finally {$reader.Dispose();$stringReader.Dispose()}
    if ($doc.SelectNodes('//LINEERROR').Count -gt 0 -or $doc.SelectNodes('//STATUS[text()="0"]').Count -gt 0) { throw 'Tally export failed.' }
    return ,$doc
}
function Val($node, [string]$name) { $child = $node.SelectSingleNode($name); if ($null -eq $child) { return '' }; return $child.InnerText.Trim() }
function Money([string]$value) { return [decimal]::Parse($value, [Globalization.NumberStyles]::Number, [Globalization.CultureInfo]::InvariantCulture) }
function Sum-Decimal($items, [string]$property) { $sum=[decimal]0; foreach ($item in $items) {$sum += [decimal]$item.$property}; return $sum }
$connection.Open()
try {
    $companies = Read-Sql 'SELECT Id, Name, TallyGuid FROM dbo.Companies_tbl'
    $companyDoc = Export-Tally 'DiscoveryCompanies' 'Company' 'GUID,Name,BooksFrom,StartingFrom,EndingAt' ''
    $companyDoc.Save((Join-Path $out 'companies.xml'))
    $matches = @()
    foreach ($node in $companyDoc.SelectNodes('//COMPANY')) {
        foreach ($row in $companies.Rows) { if ($row.TallyGuid -eq (Val $node 'GUID') -and ($CompanyId -eq 0 -or $row.Id -eq $CompanyId)) { $matches += @{ Sql=$row; Xml=$node } } }
    }
    if ($matches.Count -ne 1) { throw 'Exactly one loaded Tally/SQL company match is required. No company was selected automatically.' }
    $company = $matches[0].Sql; $companyId = [int]$company.Id
    $booksFrom = Val $matches[0].Xml 'BOOKSFROM'
    $booksDate = [datetime]::ParseExact($booksFrom,'yyyyMMdd',[Globalization.CultureInfo]::InvariantCulture)
    $scope = '<SVCURRENTCOMPANY>'+[Security.SecurityElement]::Escape([string]$company.Name)+'</SVCURRENTCOMPANY>'
    $typeDoc = Export-Tally 'DiscoveryTypes' 'Voucher Type' 'Name,Parent' $scope
    $types = @($typeDoc.SelectNodes('//VOUCHERTYPE') | ForEach-Object { @{ Name=$_.GetAttribute('NAME'); Parent=(Val $_ 'PARENT') } })
    $salesTypes = @('Sales')
    do { $previous = $salesTypes.Count; foreach ($type in $types) { if ($salesTypes -contains $type.Parent -and $salesTypes -notcontains $type.Name) { $salesTypes += $type.Name } } } while ($salesTypes.Count -gt $previous)
    $endingAt = Val $matches[0].Xml 'ENDINGAT'
    $historyEnd = [datetime]::ParseExact($endingAt,'yyyyMMdd',[Globalization.CultureInfo]::InvariantCulture)
    if ($historyEnd -lt $booksDate) {throw 'Company EndingAt precedes BooksFrom.'}
    $range = '<SVFROMDATE TYPE="Date">'+$booksFrom+'</SVFROMDATE><SVTODATE TYPE="Date">'+$endingAt+'</SVTODATE>'
    $metaDoc = Export-Tally 'DiscoveryHistory' 'Voucher' 'GUID,Date,VoucherTypeName,VoucherNumber,IsCancelled' ($scope+$range)
    $metaDoc.Save((Join-Path $out 'history-metadata.xml'))
    $metadata = @($metaDoc.SelectNodes('//VOUCHER') | Where-Object { $salesTypes -contains (Val $_ 'VOUCHERTYPENAME') })
    if ($metadata.Count -eq 0) { throw 'No Sales history returned; completeness cannot be established.' }
    $dates = @($metadata | ForEach-Object { [datetime]::ParseExact((Val $_ 'DATE'),'yyyyMMdd',[Globalization.CultureInfo]::InvariantCulture) } | Sort-Object)
    $latest = $dates[-1]
    $before = Snapshot
    $headers = Read-Sql ('SELECT * FROM dbo.SalesRegister_tbl WHERE CompanyId = '+$companyId)
    $entries = Read-Sql ('SELECT e.* FROM dbo.SalesRegisterEntries_tbl e INNER JOIN dbo.SalesRegister_tbl h ON h.Id = e.SalesRegisterId WHERE h.CompanyId = '+$companyId)
    $ledgers = Read-Sql ('SELECT Name, TallyGuid FROM dbo.Ledgers_tbl WHERE CompanyId = '+$companyId)
    $ledgerMap = @{}; foreach ($ledger in $ledgers.Rows) { $name=([string]$ledger.Name).Trim(); if (!$ledgerMap.ContainsKey($name)) { $ledgerMap[$name]=@() }; $ledgerMap[$name] += $ledger }
    $storedMap = @{}; foreach ($header in $headers.Rows) { $guid=([string]$header.TallyGuid).Trim(); if (!$storedMap.ContainsKey($guid)) { $storedMap[$guid]=@() }; $storedMap[$guid] += $header }
    $entryMap = @{}; foreach ($entry in $entries.Rows) { $key=[string]$entry.SalesRegisterId; if (!$entryMap.ContainsKey($key)) { $entryMap[$key]=@() }; $entryMap[$key] += $entry }
    $errors = New-Object 'Collections.Generic.List[string]'
    $active = New-Object 'Collections.Generic.List[object]'
    $cancelled = New-Object 'Collections.Generic.List[object]'
    $monthly = New-Object 'Collections.Generic.List[object]'
    $mappingErrors = New-Object 'Collections.Generic.List[object]'
    $unbalanced = New-Object 'Collections.Generic.List[object]'
    $allGuids = @{}
    $start=$booksDate
    $closing=[decimal]0
    while ($start -le $latest) {
        $end = (New-Object datetime $start.Year,$start.Month,1).AddMonths(1).AddDays(-1); if ($end -gt $latest) {$end=$latest}
        $month=$start.ToString('yyyyMM'); Write-Output ('Read-only export '+$month)
        $range='<SVFROMDATE TYPE="Date">'+$start.ToString('yyyyMMdd')+'</SVFROMDATE><SVTODATE TYPE="Date">'+$end.ToString('yyyyMMdd')+'</SVTODATE>'
        $doc=Export-Tally ('Discovery'+$month) 'Voucher' 'GUID,Date,VoucherTypeName,VoucherNumber,PartyLedgerName,IsCancelled,AllLedgerEntries.LedgerName,AllLedgerEntries.Amount,LedgerEntries.LedgerName,LedgerEntries.Amount' ($scope+$range)
        $doc.Save((Join-Path $out ('source-'+$month+'.xml')))
        $source=@($doc.SelectNodes('//VOUCHER') | Where-Object { $salesTypes -contains (Val $_ 'VOUCHERTYPENAME') })
        foreach ($v in $source) {
            $guid=Val $v 'GUID'; $date=Val $v 'DATE'; $number=Val $v 'VOUCHERNUMBER'; $type=Val $v 'VOUCHERTYPENAME'; $flag=Val $v 'ISCANCELLED'; $party=Val $v 'PARTYLEDGERNAME'
            if (!$allGuids.ContainsKey($guid)) {$allGuids[$guid]=0}; $allGuids[$guid]++
            if ($date -lt $start.ToString('yyyyMMdd') -or $date -gt $end.ToString('yyyyMMdd')) { $errors.Add('Out-of-range source voucher '+$guid) }
            if (!$guid -or !$number) { $errors.Add('Missing identity '+$guid) }
            if ($flag -eq 'Yes') { $cancelled.Add([pscustomobject]@{guid=$guid;date=$date;number=$number;type=$type;stored=$storedMap.ContainsKey($guid)}); continue }
            if ($flag -notin @('No','')) { $errors.Add('Unknown cancellation status '+$guid) }
            $postings=@($v.SelectNodes('ALLLEDGERENTRIES.LIST')); if ($postings.Count -eq 0) { $postings=@($v.SelectNodes('LEDGERENTRIES.LIST')) }
            $signed=New-Object 'Collections.Generic.List[object]'; $sum=[decimal]0; $partySum=[decimal]0; $index=0
            foreach ($p in $postings) {
                $name=Val $p 'LEDGERNAME'; $amountText=Val $p 'AMOUNT'
                try {$amount=Money $amountText} catch {$errors.Add('Invalid posting amount '+$guid); continue}
                $sum+=$amount; if ($name -eq $party) { $partySum+=$amount }
                $ledgerGuid=''
                if (!$ledgerMap.ContainsKey($name) -or $ledgerMap[$name].Count -ne 1 -or !$ledgerMap[$name][0].TallyGuid) { $mappingErrors.Add([pscustomobject]@{guid=$guid;ledger=$name}) } else { $ledgerGuid=[string]$ledgerMap[$name][0].TallyGuid }
                $signed.Add([pscustomobject]@{index=$index;name=$name;guid=$ledgerGuid;amount=$amount}); $index++
            }
            if (!$ledgerMap.ContainsKey($party) -or $ledgerMap[$party].Count -ne 1) {$mappingErrors.Add([pscustomobject]@{guid=$guid;ledger=$party;party=$true})}
            if ($signed.Count -eq 0 -or $sum -ne 0) {$unbalanced.Add([pscustomobject]@{guid=$guid;sum=$sum})}
            if (@($signed | Where-Object {$_.name -eq $party}).Count -eq 0) {$errors.Add('Missing party posting '+$guid)}
            $status='missing'; $amount=[math]::Abs($partySum)
            if ($storedMap.ContainsKey($guid)) {
                $status='unchanged'
                if ($storedMap[$guid].Count -ne 1) {$errors.Add('Duplicate SQL GUID '+$guid); $status='modified'} else {
                    $h=$storedMap[$guid][0]; $storedEntries=@($entryMap[[string]$h.Id] | Sort-Object EntrySequence)
                    if ($h.VoucherNumber -cne $number -or $h.VoucherType -cne $type -or $h.VoucherDate.ToString('yyyyMMdd') -ne $date -or $h.Amount -ne $amount -or $h.PartyLedgerName -cne $party -or $storedEntries.Count -ne $signed.Count) {$status='modified'}
                    if ($ledgerMap.ContainsKey($party) -and $ledgerMap[$party].Count -eq 1 -and $h.PartyLedgerGuid -ne $ledgerMap[$party][0].TallyGuid) {$status='modified'}
                    for ($i=0;$i -lt [math]::Min($storedEntries.Count,$signed.Count);$i++) { $e=$storedEntries[$i]; $p=$signed[$i]; if ($e.EntrySequence -ne $p.index -or $e.LedgerGuid -ne $p.guid -or $e.LedgerName -cne $p.name -or $e.SignedAmount -ne $p.amount) {$status='modified'} }
                }
            }
            $active.Add([pscustomobject]@{guid=$guid;date=$date;number=$number;type=$type;party=$party;amount=$amount;movement=-$partySum;postings=$signed.Count;status=$status})
        }
        $items=@($active | Where-Object {$_.date.StartsWith($month)})
        $monthly.Add([pscustomobject]@{month=$month;financialYear= $(if($start.Month -ge 4){$start.Year}else{$start.Year-1});active=$items.Count;cancelled=@($cancelled | Where-Object {$_.date.StartsWith($month)}).Count;postings=($items|Measure-Object postings -Sum).Sum;missing=@($items|Where-Object status -eq missing).Count;modified=@($items|Where-Object status -eq modified).Count;unchanged=@($items|Where-Object status -eq unchanged).Count;absoluteTotal=($items|Measure-Object amount -Sum).Sum;signedSalesMovement=($items|Measure-Object movement -Sum).Sum})
        $m=$monthly[$monthly.Count-1]
        $m.absoluteTotal=Sum-Decimal $items 'amount'; $m.signedSalesMovement=Sum-Decimal $items 'movement'
        $debit=[decimal]0; $credit=[decimal]0
        foreach ($item in $items) {if ($item.movement -lt 0) {$debit -= $item.movement} else {$credit += $item.movement}}
        $closing += $m.signedSalesMovement
        $m|Add-Member NoteProperty debit $debit; $m|Add-Member NoteProperty credit $credit
        $m|Add-Member NoteProperty allHistoryClosing ([math]::Abs($closing)); $m|Add-Member NoteProperty closingSide $(if($closing -gt 0){'Cr'}elseif($closing -lt 0){'Dr'}else{''})
        $start=$end.AddDays(1)
    }
    $duplicates=@($allGuids.GetEnumerator()|Where-Object Value -gt 1|ForEach-Object {[pscustomobject]@{guid=$_.Key;count=$_.Value}})
    $unexpected=@($headers.Rows|Where-Object {$guid=[string]$_.TallyGuid; @($active|Where-Object guid -eq $guid).Count -eq 0}|ForEach-Object {[pscustomobject]@{guid=$_.TallyGuid;date=$_.VoucherDate.ToString('yyyyMMdd');amount=$_.Amount}})
    if ($active.Count+$cancelled.Count -ne $metadata.Count) {$errors.Add('Metadata/batched count mismatch')}
    $metaIdentities=@($metadata|ForEach-Object {(Val $_ 'GUID')+'|'+(Val $_ 'DATE')+'|'+(Val $_ 'VOUCHERTYPENAME')+'|'+(Val $_ 'ISCANCELLED')}|Sort-Object)
    $batchIdentities=@(@($active|ForEach-Object {$_.guid+'|'+$_.date+'|'+$_.type+'|No'})+@($cancelled|ForEach-Object {$_.guid+'|'+$_.date+'|'+$_.type+'|Yes'})|Sort-Object)
    if (@(Compare-Object $metaIdentities $batchIdentities).Count -gt 0) {$errors.Add('Metadata/batched identity or cancellation mismatch')}
    if ($mappingErrors.Count -gt 0) {$errors.Add('Ledger mapping errors')}; if ($unbalanced.Count -gt 0) {$errors.Add('Unbalanced vouchers')}; if ($duplicates.Count -gt 0) {$errors.Add('Duplicate source GUIDs')}
    $after=Snapshot; if ($before -ne $after) {$errors.Add('SQL snapshot changed')}
    $fy=@($monthly|Group-Object financialYear|ForEach-Object {$g=$_.Group;[pscustomobject]@{financialYear=$_.Name+'-'+([int]$_.Name+1);active=($g|Measure-Object active -Sum).Sum;cancelled=($g|Measure-Object cancelled -Sum).Sum;postings=($g|Measure-Object postings -Sum).Sum;missing=($g|Measure-Object missing -Sum).Sum;modified=($g|Measure-Object modified -Sum).Sum;unchanged=($g|Measure-Object unchanged -Sum).Sum;absoluteTotal=($g|Measure-Object absoluteTotal -Sum).Sum;signedSalesMovement=($g|Measure-Object signedSalesMovement -Sum).Sum}})
    foreach ($year in $fy) { $months=@($monthly|Where-Object {([string]$_.financialYear) -eq $year.financialYear.Split('-')[0]}); $year.absoluteTotal=Sum-Decimal $months 'absoluteTotal'; $year.signedSalesMovement=Sum-Decimal $months 'signedSalesMovement' }
    $report=[ordered]@{generatedAt=(Get-Date).ToString('o');preview=$true;applied=$false;replayedSavedExports=[bool]$ReplaySavedExports;companyId=$companyId;companyName=[string]$company.Name;booksFrom=$booksFrom;companyEndingAt=(Val $matches[0].Xml 'ENDINGAT');earliestSalesDate=$dates[0].ToString('yyyyMMdd');latestSalesDate=$latest.ToString('yyyyMMdd');salesTypes=$salesTypes;metadataSalesCount=$metadata.Count;activeCount=$active.Count;cancelledCount=$cancelled.Count;financialYears=$fy;monthly=$monthly.ToArray();existingSql=@{headers=$headers.Rows.Count;postings=$entries.Rows.Count;total=($headers.Rows|Measure-Object Amount -Sum).Sum};missing=@($active|Where-Object status -eq missing);modified=@($active|Where-Object status -eq modified);cancelled=$cancelled.ToArray();unexpectedStored=$unexpected;duplicateGuids=$duplicates;ledgerMappingErrors=$mappingErrors.ToArray();unbalanced=$unbalanced.ToArray();validationErrors=$errors.ToArray();dataValidationPassed=($errors.Count -eq 0);beforeSha256=$before;afterSha256=$after;unchanged=($before -eq $after);safeToApply=$false;blockers=@('Current apply only inserts; modified voucher updates are not supported.','No approved stored deletion/cancellation reconciliation policy.','Apply is not bound to an immutable approved preview or resumable batches.','Full-history metadata is one lightweight export; source snapshot consistency is not guaranteed.','Voucher drill-down currently retrieves live Tally detail rather than independent stored postings.','Live website visual verification is outstanding.');approvalRequired=$true}
    $report|ConvertTo-Json -Depth 20|Set-Content -Encoding UTF8 (Join-Path $out 'discovery.json')
    $fy|Format-Table
    Write-Output ('Data validation passed: '+$report.dataValidationPassed+'; safeToApply: False; SQL unchanged: '+$report.unchanged)
} finally { $connection.Dispose() }

