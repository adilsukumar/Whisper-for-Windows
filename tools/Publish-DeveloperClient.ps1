#Requires -Version 7.0
param([switch]$SkipChecks, [switch]$FrameworkDependent)
$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Dotnet = Join-Path $RepoRoot '.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $Dotnet)) { $Dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
if (-not $SkipChecks) {
    & (Join-Path $RepoRoot 'dev.ps1') check
    if ($LASTEXITCODE -ne 0) { throw 'Pre-publication checks failed.' }
}
$ArtifactRoot = Join-Path $RepoRoot 'artifacts\developer-client'
New-Item -ItemType Directory -Path $ArtifactRoot -Force | Out-Null
$Drive = Get-PSDrive -Name ([IO.Path]::GetPathRoot($ArtifactRoot).Substring(0, 1))
if ($Drive.Free -lt 2GB) { throw 'At least 2 GB free space is required for publication.' }
$RunId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
$Stage = Join-Path $ArtifactRoot $RunId
$Client = Join-Path $Stage 'client'
New-Item -ItemType Directory -Path $Client | Out-Null
$RestoreArguments = @(('-p:RestorePackagesPath=' + (Join-Path $RepoRoot 'artifacts\nuget-packages')),
    ('-p:RestoreFallbackFolders=' + (Join-Path $env:USERPROFILE '.nuget\packages')))
$LocalFeed = Join-Path $RepoRoot 'artifacts\nuget-feed'
if (-not $FrameworkDependent -and (Test-Path -LiteralPath $LocalFeed)) {
    $RestoreArguments += @('--source', $LocalFeed, '--source', 'https://api.nuget.org/v3/index.json')
}
& $Dotnet publish (Join-Path $RepoRoot 'WhisperClient\WhisperClient.csproj') `
    --configuration Release --runtime win-x64 --self-contained $(-not [bool]$FrameworkDependent) `
    -p:PublishSingleFile=false -p:PublishTrimmed=false --output $Client @RestoreArguments
if ($LASTEXITCODE -ne 0) { throw "Publish failed. Diagnostic output retained at $Stage" }
if (-not (Test-Path -LiteralPath (Join-Path $Client 'Whisper.exe'))) { throw 'Published executable missing.' }
# Copy an explicitly selected instruction file, never the repository or environment files.
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DEVELOPER_CLIENT_README.md') -Destination (Join-Path $Stage 'README.md')
$Files = @(Get-ChildItem -LiteralPath $Client -Recurse -File | Sort-Object FullName | ForEach-Object {
    [ordered]@{ path = [IO.Path]::GetRelativePath($Stage, $_.FullName).Replace('\','/');
        bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
})
$Manifest = [ordered]@{ schema_version = 1; artifact = 'unsigned-developer-client';
    platform = 'win-x64'; created_utc = [DateTime]::UtcNow.ToString('o'); checks_skipped = [bool]$SkipChecks;
    self_contained_dotnet = (-not [bool]$FrameworkDependent); includes_brain = $false; includes_models = $false;
    commercial_release = $false; files = $Files }
$Manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $Stage 'manifest.json') -Encoding utf8
$Zip = "$Stage.zip"
Compress-Archive -LiteralPath $Stage -DestinationPath $Zip -CompressionLevel Optimal
$ZipHash = (Get-FileHash -LiteralPath $Zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$ZipHash  $([IO.Path]::GetFileName($Zip))" | Set-Content -LiteralPath "$Zip.sha256" -Encoding ascii
Write-Host "Unsigned developer client: $Zip"
Write-Host "SHA-256: $ZipHash"
Write-Host 'Not an installer or a commercial release. Local brain and Ollama are still required.'
if ($FrameworkDependent) { Write-Host '.NET 8 Windows Desktop Runtime must already be installed.' }
