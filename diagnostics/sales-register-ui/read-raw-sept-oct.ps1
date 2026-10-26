$ErrorActionPreference='Stop'
$connection=New-Object System.Data.SqlClient.SqlConnection('Server=.\SQLEXPRESS;Database=TallyWebDB;Integrated Security=True;TrustServerCertificate=True')
$connection.Open()
function ReadRows($query){$cmd=$connection.CreateCommand();$cmd.CommandText=$query;$r=$cmd.ExecuteReader();$list=@();while($r.Read()){$row=@{};for($i=0;$i -lt $r.FieldCount;$i++){$row[$r.GetName($i)]=$r.GetValue($i)};$list+=[pscustomobject]$row};$r.Close();return $list}
$company=ReadRows 'SELECT Id,Name,TallyGuid FROM dbo.Companies_tbl WHERE Id=1'
$ledgers=ReadRows 'SELECT Name,TallyGuid FROM dbo.Ledgers_tbl WHERE CompanyId=1'
$stored=ReadRows 'SELECT CompanyId,TallyGuid,Amount FROM dbo.SalesRegister_tbl'
$connection.Close()
$source=Get-Content TallyWebAPI/TallyWebAPI/Services/VoucherService.cs -Raw
$template=[regex]::Match($source,'var xmlRequest = \$"""([\s\S]*?)""";').Groups[1].Value
function Child($node,$name){$element=$node.SelectSingleNode($name);if($element){return $element.InnerText.Trim()};return ''}
foreach($month in 9..10){
 $from=Get-Date -Year 2024 -Month $month -Day 1;$to=$from.AddMonths(1).AddDays(-1)
 $xml=$template.Replace('{safeCompanyName}',[Security.SecurityElement]::Escape($company.Name)).Replace('{fromDate}',$from.ToString('yyyyMMdd')).Replace('{toDate}',$to.ToString('yyyyMMdd'))
 $response=Invoke-WebRequest 'http://127.0.0.1:9000/' -Method Post -ContentType 'text/xml; charset=utf-16' -Body ([Text.Encoding]::Unicode.GetBytes($xml)) -UseBasicParsing -TimeoutSec 60
 $bytes=$response.RawContentStream.ToArray();$raw=if($bytes[1] -eq 0 -or ($bytes[0] -eq 255 -and $bytes[1] -eq 254)){[Text.Encoding]::Unicode.GetString($bytes)}else{[Text.Encoding]::UTF8.GetString($bytes)}
 $raw=$raw.TrimStart([char]0xFEFF)
 $raw|Set-Content -Encoding UTF8 "diagnostics/sales-register-ui/raw-2024-$month.xml"
 $clean=[regex]::Replace($raw,'&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);','');$clean=[regex]::Replace($clean,'(<\/?)UDF:','$1');[xml]$doc=$clean
 $vouchers=@();$invalidNonSales=@();$errors=@();$names=@();$postingCount=0;[decimal]$total=0;$missing=0
 foreach($v in $doc.SelectNodes('//VOUCHER')){
  $type=Child $v 'VOUCHERTYPENAME';$isSales=$type -in @('Sales','GST Sales');$entries=@($v.SelectNodes('ALLLEDGERENTRIES.LIST'));if(!$entries.Count){$entries=@($v.SelectNodes('LEDGERENTRIES.LIST'))}
  [decimal]$sum=0;$partyAmounts=@();$issues=@();$party=Child $v 'PARTYLEDGERNAME';$guid=Child $v 'GUID';$date=Child $v 'DATE'
  foreach($entry in $entries){$name=Child $entry 'LEDGERNAME';$text=Child $entry 'AMOUNT';[decimal]$amount=0;$valid=[decimal]::TryParse($text,[Globalization.NumberStyles]::Any,[Globalization.CultureInfo]::InvariantCulture,[ref]$amount)
   if(!$name -or !$valid){$issues+="Invalid posting: ledger='$name', amount='$text'"}
   if($valid){$sum+=$amount};if($name -eq $party -and $valid){$partyAmounts+=$amount}
   if($isSales){$names+=$name;$mapped=@($ledgers|Where-Object {$_.Name.Trim() -eq $name.Trim() -and $_.TallyGuid});if($mapped.Count -ne 1){$issues+="Non-unique/missing SQL mapping '$name'"}}
  }
  if(!$isSales){if($issues.Count){$invalidNonSales+=[pscustomobject]@{type=$type;number=(Child $v 'VOUCHERNUMBER');guid=$guid;issues=$issues}};continue}
  if(!$entries.Count -or $sum -ne 0){$issues+="Unbalanced or absent postings: $sum"}
  if(!$guid.StartsWith($company.TallyGuid+'-')){$issues+='Invalid company voucher GUID'}
  if(!(Child $v 'VOUCHERNUMBER')){$issues+='Missing voucher number'}
  if($date -lt $from.ToString('yyyyMMdd') -or $date -gt $to.ToString('yyyyMMdd')){$issues+='Date outside range'}
  if(!$party -or $partyAmounts.Count -ne 1){$issues+='Missing/ambiguous party amount'}
  $amount=if($partyAmounts.Count){[Math]::Abs($partyAmounts[0])}else{0};$total+=$amount;$postingCount+=$entries.Count
  $matches=@($stored|Where-Object {$_.TallyGuid -eq $guid});if(!$matches.Count){$missing++}else{$issues+='Unexpected existing GUID requires detailed stored comparison'}
  if($issues.Count){$errors+=[pscustomobject]@{guid=$guid;issues=$issues}}
  $vouchers+=[pscustomobject]@{guid=$guid;date=$date;type=$type;party=$party;amount=$amount;postingCount=$entries.Count;postingSum=$sum}
 }
 $duplicates=@($vouchers|Group-Object guid|Where-Object {$_.Count -gt 1});if($duplicates.Count){$errors+='Duplicate Sales GUIDs'}
 $report=[pscustomobject]@{month=$month;salesVoucherCount=$vouchers.Count;missingSql=$missing;postingCount=$postingCount;total=$total;distinctPostingLedgers=@($names|Sort-Object -Unique).Count;errors=$errors;invalidNonSales=$invalidNonSales;vouchers=$vouchers;safeSalesData=($errors.Count -eq 0);existingPreviewBlocked=$true}
 $report|ConvertTo-Json -Depth 20|Set-Content "diagnostics/sales-register-ui/raw-preview-2024-$month.json"
 $report|Select-Object month,salesVoucherCount,missingSql,postingCount,total,distinctPostingLedgers,safeSalesData,@{n='invalidNonSalesCount';e={$_.invalidNonSales.Count}}|ConvertTo-Json -Compress
}
