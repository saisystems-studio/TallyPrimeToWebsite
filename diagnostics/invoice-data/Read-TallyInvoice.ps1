param([string]$CompanyName = 'JAYA JOTHI MALIGAI')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [TimeSpan]::FromSeconds(60)
function Export-Tally([string]$Xml, [string]$Name) {
    if ($Xml -notmatch '<TALLYREQUEST>Export</TALLYREQUEST>') { throw 'Only Export is allowed' }
    $content = [System.Net.Http.StringContent]::new($Xml,[Text.Encoding]::Unicode,'text/xml')
    try {
        $response = $client.PostAsync('http://127.0.0.1:9000',$content).GetAwaiter().GetResult()
        $response.EnsureSuccessStatusCode() | Out-Null
        $bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        $text = if ($bytes.Length -gt 1 -and ($bytes[0] -eq 255 -or $bytes[1] -eq 0)) { [Text.Encoding]::Unicode.GetString($bytes).TrimStart([char]0xFEFF) } else { [Text.Encoding]::UTF8.GetString($bytes).TrimStart([char]0xFEFF) }
        [IO.File]::WriteAllText((Join-Path $PSScriptRoot $Name),$text,[Text.UTF8Encoding]::new($false))
        $clean = [regex]::Replace($text,'&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);','')
        $clean = [regex]::Replace($clean,'(</?)UDF:','$1')
        return [xml]$clean
    } finally { $content.Dispose() }
}
$safeCompany = [Security.SecurityElement]::Escape($CompanyName)
$companies = Export-Tally '<ENVELOPE><HEADER><VERSION>1</VERSION><TALLYREQUEST>Export</TALLYREQUEST><TYPE>Collection</TYPE><ID>InvoiceAuditCompany</ID></HEADER><BODY><DESC><STATICVARIABLES><SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT></STATICVARIABLES><TDL><TDLMESSAGE><COLLECTION NAME="InvoiceAuditCompany"><TYPE>Company</TYPE><FETCH>*</FETCH><FETCH>Address,Address.*,BasicCompanyAddress,MailingName,CMPMAILINGDETAILS.*,CMPGSTREGDETAILS.*</FETCH></COLLECTION></TDLMESSAGE></TDL></DESC></BODY></ENVELOPE>' 'companies.xml'
$companies.SelectNodes('//COMPANY[@NAME]') | ForEach-Object { [pscustomobject]@{Name=$_.GetAttribute('NAME'); Guid=$_.GUID; Address=($_.SelectNodes('./ADDRESS.LIST/ADDRESS') | ForEach-Object { $_.InnerText }) -join ' | '; State=$_.STATENAME; Email=$_.EMAIL} } | ConvertTo-Json -Depth 3
$request = @"
<ENVELOPE><HEADER><VERSION>1</VERSION><TALLYREQUEST>Export</TALLYREQUEST><TYPE>Collection</TYPE><ID>InvoiceAuditVouchers</ID></HEADER><BODY><DESC><STATICVARIABLES><SVEXPORTFORMAT>`$`$SysName:XML</SVEXPORTFORMAT><SVCURRENTCOMPANY>$safeCompany</SVCURRENTCOMPANY><SVFROMDATE TYPE="Date">20240401</SVFROMDATE><SVTODATE TYPE="Date">20261231</SVTODATE></STATICVARIABLES><TDL><TDLMESSAGE><COLLECTION NAME="InvoiceAuditVouchers"><TYPE>Voucher</TYPE><FETCH>Date,GUID,VoucherTypeName,VoucherNumber,PartyLedgerName,Reference</FETCH></COLLECTION></TDLMESSAGE></TDL></DESC></BODY></ENVELOPE>
"@
$vouchers = Export-Tally $request 'voucher-index.xml'
$selected = $vouchers.SelectNodes('//VOUCHER') | Where-Object { $_.VOUCHERTYPENAME -in @('Purchase','Sales') }
"Invoice count: $($selected.Count)"
$selected | Select-Object -First 8 | ForEach-Object { [pscustomobject]@{Type=$_.VOUCHERTYPENAME;Number=$_.VOUCHERNUMBER;Date=$_.DATE;Party=$_.PARTYLEDGERNAME;Guid=$_.GUID} } | ConvertTo-Json
$purchase = $selected | Where-Object { $_.VOUCHERTYPENAME -eq 'Purchase' -and $_.VOUCHERNUMBER -eq '661' } | Select-Object -First 1
if (-not $purchase) { $purchase = $selected | Where-Object { $_.VOUCHERTYPENAME -eq 'Purchase' } | Select-Object -First 1 }
$sale = $selected | Where-Object { $_.VOUCHERTYPENAME -eq 'Sales' } | Select-Object -First 1
foreach ($entry in @(@('purchase',$purchase),@('sales',$sale))) {
    $kind = $entry[0]; $voucher = $entry[1]
    if (-not $voucher) { continue }
    $safeGuid = [Security.SecurityElement]::Escape($voucher.GUID)
    $detailRequest = @"
<ENVELOPE><HEADER><VERSION>1</VERSION><TALLYREQUEST>Export</TALLYREQUEST><TYPE>Collection</TYPE><ID>InvoiceAuditDetail</ID></HEADER><BODY><DESC><STATICVARIABLES><SVEXPORTFORMAT>`$`$SysName:XML</SVEXPORTFORMAT><SVCURRENTCOMPANY>$safeCompany</SVCURRENTCOMPANY><SVFROMDATE TYPE="Date">20240401</SVFROMDATE><SVTODATE TYPE="Date">20261231</SVTODATE></STATICVARIABLES><TDL><TDLMESSAGE><COLLECTION NAME="InvoiceAuditDetail"><TYPE>Voucher</TYPE><FILTER>InvoiceAuditGuid</FILTER><FETCH>*</FETCH><FETCH>AllInventoryEntries.*,InventoryEntries.*,AllLedgerEntries.*,LedgerEntries.*</FETCH></COLLECTION><SYSTEM TYPE="Formulae" NAME="InvoiceAuditGuid">`$GUID = "$safeGuid"</SYSTEM></TDLMESSAGE></TDL></DESC></BODY></ENVELOPE>
"@
    $detail = Export-Tally $detailRequest "$kind.xml"
    $v = $detail.SelectSingleNode('//VOUCHER[GUID]')
    "${kind}: $($voucher.VOUCHERNUMBER) GUID $($voucher.GUID)"
    $v.ChildNodes | Where-Object { $_.InnerText.Trim() -ne '' -and $_.Name -match 'ADDRESS.LIST|^BASICBUYERNAME$|^CONSIGNEE|^REFERENCE|^PARTYNAME$' } | ForEach-Object { [pscustomobject]@{Tag=$_.Name;Value=$_.InnerText} } | ConvertTo-Json -Depth 3
}
$companyNode = $companies.SelectNodes('//COMPANY[@NAME]') | Where-Object { $_.GetAttribute('NAME') -eq $CompanyName } | Select-Object -First 1
'Company address and state tags:'
$companyNode.ChildNodes | Where-Object { $_.Name -match 'ADDRESS|STATE|GST|PHONE|MOBILE' } | ForEach-Object { [pscustomobject]@{Tag=$_.Name;Value=$_.InnerText} } | ConvertTo-Json -Depth 3
$client.Dispose()
