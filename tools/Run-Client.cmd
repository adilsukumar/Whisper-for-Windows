@echo off
title Whisper - Windows client
cd /d "%~dp0.."
if not exist "WhisperClient\bin\Debug\net8.0-windows\Whisper.exe" (
  echo Client build missing. Run dev.ps1 build first.
  exit /b 1
)
if exist ".dotnet\dotnet.exe" (
  set "DOTNET_ROOT=%CD%\.dotnet"
  set "DOTNET_ROOT_X64=%CD%\.dotnet"
)
echo Starting Whisper. Look for its tray icon and cursor companion.
echo Ctrl+Shift+Space opens text input; Ctrl+Space is hold-to-talk.
echo Screen sharing is off unless explicitly enabled.
"WhisperClient\bin\Debug\net8.0-windows\Whisper.exe"
echo Whisper has exited.
