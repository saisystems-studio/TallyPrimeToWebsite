param([int]$ExpectedProcessId = 3536)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$original=Join-Path $workspace 'TallyWebAPI/TallyWebAPI/bin/Debug/net10.0/TallyWebAPI.exe'
$verified=Join-Path $workspace 'TallyWebAPI/TallyWebAPI/bin/invoice-data-verified/TallyWebAPI.dll'
$process=Get-Process -Id $ExpectedProcessId -ErrorAction Stop
if ($process.ProcessName -ne 'TallyWebAPI' -or $process.Path -ne $original) { throw 'The expected local development API identity changed. No process was stopped.' }
if (-not (Test-Path -LiteralPath $verified)) { throw 'Verified API build is missing.' }
$arguments=@('--environment','Development','--urls','"https://localhost:7108;http://localhost:5228"','--Startup:RunLegacyDatabaseInitialization=false')
Stop-Process -Id $ExpectedProcessId
try {
    $new=Start-Process -FilePath 'C:\Program Files\dotnet\dotnet.exe' -ArgumentList (@($verified)+$arguments) -WorkingDirectory (Split-Path $verified) -WindowStyle Hidden -RedirectStandardOutput (Join-Path $PSScriptRoot 'local-api.log') -RedirectStandardError (Join-Path $PSScriptRoot 'local-api-error.log') -PassThru
    $new.Id | Set-Content (Join-Path $PSScriptRoot 'local-api.pid')
    $ready=$false
    for($i=0;$i -lt 15;$i++) {
        if ($new.HasExited) { throw 'The new API exited during startup.' }
        try { $schema=Invoke-RestMethod 'http://localhost:5228/swagger/v1/swagger.json' -TimeoutSec 2; if ($schema.paths.'/api/Voucher/invoice-company') { $ready=$true;break } } catch { Start-Sleep -Milliseconds 400 }
    }
    if (-not $ready) { throw 'The new invoice endpoint did not become available.' }
    Write-Output "Local API reloaded with invoice-company endpoint. PID $($new.Id). Development; startup database initialization disabled."
} catch {
    if ($new -and -not $new.HasExited) { Stop-Process -Id $new.Id }
    Start-Process -FilePath $original -ArgumentList $arguments -WorkingDirectory (Split-Path $original) -WindowStyle Hidden | Out-Null
    throw 'The updated API could not start; the original local API was restored with database initialization disabled.'
}
