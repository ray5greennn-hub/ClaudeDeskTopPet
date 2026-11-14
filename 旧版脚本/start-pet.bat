@echo off
start "" powershell -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -STA -File "%~dp0claude-pet.ps1"
