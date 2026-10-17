$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\tools\ModelReadiness.ps1')

function Assert-Missing([string[]]$Installed, [string[]]$Required, [string[]]$Expected) {
    $Actual = @(Get-MissingOllamaModels -ModelList $Installed -RequiredModels $Required)
    if (($Actual -join '|') -cne ($Expected -join '|')) {
        throw "Expected missing [$($Expected -join ', ')]; got [$($Actual -join ', ')]."
    }
}

$Table = @('NAME ID SIZE MODIFIED', 'llama3.1:latest abc 4.9 GB 1 minute ago')
Assert-Missing $Table @('llama3.1', 'llama3.2-vision') @('llama3.2-vision')
Assert-Missing $Table @('llama3.1:latest') @()
Assert-Missing $Table @('llama3.1:8b') @('llama3.1:8b')
Assert-Missing @('NAME ID SIZE MODIFIED', 'llama3.1-extra:latest abc 1 GB now') @('llama3.1') @('llama3.1')
Assert-Missing @('NAME ID SIZE MODIFIED') @('llama3.1') @('llama3.1')
Assert-Missing @('localhost:5000/team/model:latest abc 1 GB now') @('localhost:5000/team/model') @()
Write-Host 'Model readiness: 6 regression cases passed.'
