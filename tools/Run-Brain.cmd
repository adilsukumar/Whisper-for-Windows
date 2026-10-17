@echo off
title Whisper - local brain
chcp 65001 >nul
set "PYTHONUTF8=1"
set "PYTHONIOENCODING=utf-8"
cd /d "%~dp0.."
if not exist ".venv\Scripts\python.exe" (
  echo Missing Python environment. Follow the developer setup instructions.
  exit /b 1
)
if not defined WHISPER_STT_MODELS_DIR set "WHISPER_STT_MODELS_DIR=%CD%\artifacts\models\stt"
echo Starting the local brain. Keep this window open while using Whisper.
echo The vision model may still be downloading; text questions do not require it.
".venv\Scripts\python.exe" -u "WhisperBrain\server.py"
