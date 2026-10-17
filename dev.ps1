param(
    [ValidateSet('check', 'build', 'test', 'test-accounts', 'doctor', 'run-brain', 'run-client', 'run-accounts', 'package-client')]
    [string]$Task = 'check'
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$LocalDotnet = Join-Path $RepoRoot '.dotnet\dotnet.exe'
$VenvPython = Join-Path $RepoRoot '.venv\Scripts\python.exe'

if (Test-Path -LiteralPath $LocalDotnet) {
    $Dotnet = $LocalDotnet
} else {
    $DotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $DotnetCommand) {
        throw 'The .NET 8 SDK is required. Install it or place it in .dotnet.'
    }
    $Dotnet = $DotnetCommand.Source
}

if (Test-Path -LiteralPath $VenvPython) {
    $Python = $VenvPython
} else {
    $PythonCommand = Get-Command python -ErrorAction SilentlyContinue
    if (-not $PythonCommand) {
        throw 'Python 3.11 is required. Create .venv or install Python.'
    }
    $Python = $PythonCommand.Source
}

$ClientProject = Join-Path $RepoRoot 'WhisperClient\WhisperClient.csproj'
$BrainDirectory = Join-Path $RepoRoot 'WhisperBrain'
. (Join-Path $RepoRoot 'tools\ModelReadiness.ps1')

function Invoke-Build {
    & $Dotnet build $ClientProject
    if ($LASTEXITCODE -ne 0) { throw 'Client build failed.' }
}

function Invoke-Tests {
    & (Join-Path $RepoRoot 'tests\Test-ModelReadiness.ps1')
    & $Dotnet run --project (Join-Path $RepoRoot 'tests\WhisperClient.CoreTests\WhisperClient.CoreTests.csproj')
    if ($LASTEXITCODE -ne 0) { throw 'Client core tests failed.' }
    & $Python -m py_compile `
        (Join-Path $BrainDirectory 'server.py') `
        (Join-Path $BrainDirectory 'protocol.py') `
        (Join-Path $BrainDirectory 'local_auth.py') `
        (Join-Path $BrainDirectory 'structured_logging.py') `
        (Join-Path $BrainDirectory 'stt.py') `
        (Join-Path $BrainDirectory 'ollama_client.py') `
        (Join-Path $BrainDirectory 'highlights.py') `
        (Join-Path $BrainDirectory 'app_profiles.py')
    if ($LASTEXITCODE -ne 0) { throw 'Python compilation failed.' }

    & $Python -m unittest discover -s $BrainDirectory -p 'test_*.py' -v
    if ($LASTEXITCODE -ne 0) { throw 'Python tests failed.' }
}

function Invoke-Doctor {
    # Client environment selects the dedicated service, not its storage location.
    # Never silently inspect or download through the C:-backed default instance.
    $env:OLLAMA_HOST = '127.0.0.1:11435'
    Write-Host "Python: $(& $Python --version)"
    Write-Host ".NET: $(& $Dotnet --version)"
    & $Python -c "import av, ctranslate2, faster_whisper, httpx, websockets; print('Python runtime: ready')"
    if ($LASTEXITCODE -ne 0) { throw 'Python runtime is incomplete.' }

    $Ollama = Get-Command ollama -ErrorAction SilentlyContinue
    if (-not $Ollama) { throw 'Ollama is not installed or not on PATH.' }
    & $Ollama.Source --version
    if ($LASTEXITCODE -ne 0) { throw 'Cannot query the Ollama version.' }
    $ModelList = & $Ollama.Source list
    if ($LASTEXITCODE -ne 0) { throw 'Cannot list Ollama models. Verify the service is running.' }
    $ModelList | Write-Host

    $RequiredModels = @(
        $(if ($env:WHISPER_TEXT_MODEL) { $env:WHISPER_TEXT_MODEL } else { 'qwen3-vl:2b-instruct-q4_K_M' }),
        $(if ($env:WHISPER_VISION_MODEL) { $env:WHISPER_VISION_MODEL } else { 'qwen3-vl:2b-instruct-q4_K_M' })
    )
    $MissingModels = @(Get-MissingOllamaModels -ModelList $ModelList -RequiredModels $RequiredModels)
    if ($MissingModels) { throw "Missing Ollama models: $($MissingModels -join ', ')" }
    Write-Host 'Whisper runtime: ready'
}

function Invoke-AccountTests {
    & $Python -m unittest discover -s (Join-Path $RepoRoot 'WhisperAccounts\tests') -p 'test_*.py' -v
    if ($LASTEXITCODE -ne 0) { throw 'Account security tests failed. Install WhisperAccounts/requirements.lock.txt for the full developer check.' }
}

Push-Location $RepoRoot
try {
    switch ($Task) {
        'build' { Invoke-Build }
        'package-client' { & (Join-Path $RepoRoot 'tools\Publish-DeveloperClient.ps1') }
        'test' { Invoke-Tests }
        'test-accounts' { Invoke-AccountTests }
        'doctor' { Invoke-Doctor }
        'run-brain' {
            if (-not $env:WHISPER_STT_MODELS_DIR) {
                $env:WHISPER_STT_MODELS_DIR = Join-Path $RepoRoot 'artifacts\models\stt'
            }
            & $Python (Join-Path $BrainDirectory 'server.py')
        }
        'run-client' { & $Dotnet run --project $ClientProject }
        'run-accounts' {
            & $Python -m uvicorn WhisperAccounts.api:from_environment --factory --host 127.0.0.1 --port 8770 --no-access-log
        }
        'check' {
            Invoke-Tests
            Invoke-AccountTests
            Invoke-Build
            git diff --check
            if ($LASTEXITCODE -ne 0) { throw 'Git whitespace check failed.' }
        }
    }
} finally {
    Pop-Location
}
