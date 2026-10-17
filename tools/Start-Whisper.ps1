param([switch]$Build)
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Python = Join-Path $RepoRoot '.venv\Scripts\python.exe'
$Dotnet = Join-Path $RepoRoot '.dotnet\dotnet.exe'
$Candidate = 'qwen3-vl:2b-instruct-q4_K_M'
$ClientDirectory = Join-Path $RepoRoot 'artifacts\account-client'
$Client = Join-Path $ClientDirectory 'Whisper.exe'
if ($Build -or -not (Test-Path -LiteralPath $Client)) {
    & $Dotnet build (Join-Path $RepoRoot 'WhisperClient\WhisperClient.csproj') --no-restore `
        ('-p:IntermediateOutputPath=' + (Join-Path $RepoRoot 'artifacts\account-client-obj\')) `
        ('-p:OutputPath=' + $ClientDirectory + '\')
    if ($LASTEXITCODE -ne 0) { throw 'Client build failed; nothing started.' }
}
$Inventory = Invoke-RestMethod 'http://127.0.0.1:11435/api/tags' -TimeoutSec 5
if (@($Inventory.models | Where-Object { $_.name -eq $Candidate }).Count -ne 1) {
    throw 'Start the D-backed Ollama service and install the tested Qwen model first. No download was started.'
}
if (Get-Process -Name Whisper -ErrorAction SilentlyContinue) { throw 'Whisper is already running. Quit from its tray before starting another build.' }
$existingListener = @(Get-NetTCPConnection -LocalPort 8765 -State Listen -ErrorAction SilentlyContinue)
$reuseBrain = $existingListener.Count -gt 0
if ($reuseBrain) {
    foreach ($ownerId in @($existingListener.OwningProcess | Select-Object -Unique)) {
        $ownerProcess = Get-CimInstance Win32_Process -Filter "ProcessId=$ownerId"
        if ($ownerProcess.Name -ne 'python.exe' -or
            $ownerProcess.CommandLine -notmatch [regex]::Escape((Join-Path $RepoRoot 'WhisperBrain\server.py'))) {
            throw 'Port 8765 belongs to another service. Nothing was stopped.'
        }
    }
}
# This launcher is explicitly for the local developer build, not paid distribution.
$env:OLLAMA_URL = 'http://127.0.0.1:11435'
$env:WHISPER_TEXT_MODEL = $Candidate
$env:WHISPER_VISION_MODEL = $Candidate
$env:WHISPER_REQUIRE_ACCOUNT = '0'
$env:DOTNET_ROOT = Join-Path $RepoRoot '.dotnet'
$env:PYTHONUTF8 = '1'
$env:PYTHONIOENCODING = 'utf-8'
$env:WHISPER_STT_MODELS_DIR = Join-Path $RepoRoot 'artifacts\models\stt'
if ([string]::IsNullOrWhiteSpace($env:WHISPER_AUTH_TOKEN_FILE)) {
    $env:WHISPER_AUTH_TOKEN_FILE = Join-Path $env:LOCALAPPDATA 'Whisper\orchestrator.token'
}
if ($reuseBrain) {
    if (-not (Test-Path -LiteralPath $env:WHISPER_AUTH_TOKEN_FILE)) {
        throw 'The running brain token is missing. Nothing was restarted or regenerated.'
    }
    $clientProcess = Start-Process -FilePath $Client -WorkingDirectory $ClientDirectory -WindowStyle Normal -PassThru
    Write-Host "Reused the running Whisper brain. Client PID $($clientProcess.Id)."
    Write-Host 'Ctrl+Shift+H: controls/help. Ctrl+Shift+Q: quit the desktop app.'
    exit 0
}
$run = [Guid]::NewGuid().ToString('N')
$outLog = Join-Path $RepoRoot "artifacts\brain-$run.out.log"
$errLog = Join-Path $RepoRoot "artifacts\brain-$run.err.log"
$brain = Start-Process -FilePath $Python -ArgumentList @(('"' + (Join-Path $RepoRoot 'WhisperBrain\server.py') + '"')) `
    -WorkingDirectory $RepoRoot -WindowStyle Hidden -RedirectStandardOutput $outLog -RedirectStandardError $errLog -PassThru
$ready = $false
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    $brain.Refresh()
    if ($brain.HasExited) { throw "Brain failed to start. Diagnostics: $errLog" }
    $listener = Get-NetTCPConnection -LocalPort 8765 -State Listen -ErrorAction SilentlyContinue
    # Windows venv python.exe may delegate to its base interpreter child.
    $ownedIds = @($brain.Id) + @(Get-CimInstance Win32_Process -Filter "Name='python.exe'" |
        Where-Object { $_.ParentProcessId -eq $brain.Id } | Select-Object -ExpandProperty ProcessId)
    if (@($listener | Where-Object { $_.OwningProcess -in $ownedIds }).Count -gt 0) { $ready = $true; break }
    Start-Sleep -Milliseconds 500
}
if (-not $ready) { throw "Brain readiness timed out. Process $($brain.Id) was left intact; inspect $errLog before retrying." }
$clientProcess = Start-Process -FilePath $Client -WorkingDirectory $ClientDirectory -WindowStyle Normal -PassThru
Write-Host "Local developer build started. Brain PID $($brain.Id), client PID $($clientProcess.Id)."
Write-Host "Ctrl+Shift+Space: typed question. Hold Ctrl+Space: voice. Escape: dismiss/cancel."
Write-Host "Ctrl+Shift+H: controls/help. Ctrl+Shift+Q: quit the desktop app."
Write-Host "Brain logs: $outLog and $errLog"
