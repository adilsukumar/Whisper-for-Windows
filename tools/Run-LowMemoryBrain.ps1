$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Candidate = 'qwen3-vl:2b-instruct-q4_K_M'
# Explicit opt-in candidate profile. Never downloads or silently falls back.
$Inventory = Invoke-RestMethod 'http://127.0.0.1:11435/api/tags' -TimeoutSec 5
if (@($Inventory.models | Where-Object { $_.name -eq $Candidate }).Count -ne 1) {
    throw "Candidate $Candidate is not installed. No download was started. Install and image-test it before using this profile."
}
$env:OLLAMA_URL = 'http://127.0.0.1:11435'
# One small multimodal model handles both paths, avoiding an 8B text model load.
$env:WHISPER_VISION_MODEL = $Candidate
$env:WHISPER_TEXT_MODEL = $Candidate
& (Join-Path $RepoRoot '.venv\Scripts\python.exe') (Join-Path $RepoRoot 'WhisperBrain\server.py')
if ($LASTEXITCODE -ne 0) { throw 'Low-memory brain exited with an error.' }
