$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Net.Http
$client=[Net.Http.HttpClient]::new()
foreach($probe in @(@('company-object','<HEADER><VERSION>1</VERSION><TALLYREQUEST>Export</TALLYREQUEST><TYPE>Object</TYPE><SUBTYPE>Company</SUBTYPE><ID TYPE="Name">JAYA JOTHI MALIGAI</ID></HEADER>','<FETCHLIST><FETCH>*</FETCH><FETCH>Address</FETCH><FETCH>Address.*</FETCH></FETCHLIST>'),@('taxunits','<HEADER><VERSION>1</VERSION><TALLYREQUEST>Export</TALLYREQUEST><TYPE>Collection</TYPE><ID>InvoiceMasterTax</ID></HEADER>','<TDL><TDLMESSAGE><COLLECTION NAME="InvoiceMasterTax"><TYPE>TaxUnit</TYPE><FETCH>*</FETCH><FETCH>Address.*,Address,CMPMAILINGDETAILS.*</FETCH></COLLECTION></TDLMESSAGE></TDL>'),@('addresses','<HEADER><VERSION>1</VERSION><TALLYREQUEST>Export</TALLYREQUEST><TYPE>Collection</TYPE><ID>InvoiceMasterAddress</ID></HEADER>','<TDL><TDLMESSAGE><COLLECTION NAME="InvoiceMasterAddress"><TYPE>Address : Company</TYPE><CHILDOF>JAYA JOTHI MALIGAI</CHILDOF><FETCH>*</FETCH></COLLECTION></TDLMESSAGE></TDL>'))) {
 $xml='<ENVELOPE>'+$probe[1]+'<BODY><DESC><STATICVARIABLES><SVCURRENTCOMPANY>JAYA JOTHI MALIGAI</SVCURRENTCOMPANY><SVEXPORTFORMAT>$$SysName:XML</SVEXPORTFORMAT></STATICVARIABLES>'+$probe[2]+'</DESC></BODY></ENVELOPE>'
 $content=[Net.Http.StringContent]::new($xml,[Text.Encoding]::Unicode,'text/xml')
 $response=$client.PostAsync('http://127.0.0.1:9000',$content).GetAwaiter().GetResult()
 $text=$response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
 [IO.File]::WriteAllText((Join-Path $PSScriptRoot ($probe[0]+'.xml')),$text,[Text.UTF8Encoding]::new($false))
 $clean=[regex]::Replace($text,'&#(?:0?[0-8]|0?1[0-9]|0?2[0-9]|3[01]);','')
 $clean=[regex]::Replace($clean,'(</?)UDF:','$1')
 $doc=[xml]$clean
 Write-Output $probe[0]
 $doc.SelectNodes('//*[contains(name(),"ADDRESS") or name()="LINEERROR" or name()="GSTREGNUMBER" or name()="STATECODE" or name()="STATENAME"]') | Where-Object { $_.InnerText.Trim() } | ForEach-Object { [pscustomobject]@{Tag=$_.Name;Value=$_.InnerText} } | ConvertTo-Json -Compress
}
$client.Dispose()
