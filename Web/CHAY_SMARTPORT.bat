@echo off
chcp 65001 >nul
title SmartPort - Khoi dong toan bo he thong
cd /d "%~dp0"
echo ============================================================
echo   SMARTPORT - CHAY TOAN BO HE THONG
echo   Docker/RabbitMQ + 13 dich vu .NET + AI Python + API gateway
echo   (go "CHAY_SMARTPORT.bat build" de build lai ma nguon)
echo ============================================================
where pwsh >nul 2>&1
if errorlevel 1 (
  echo Can PowerShell 7 ^(pwsh^): winget install Microsoft.PowerShell
  pause
  exit /b 1
)
if /i "%~1"=="build" (
  pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0MicroServices\khoi_dong_tat_ca.ps1" -Build
) else (
  pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0MicroServices\khoi_dong_tat_ca.ps1"
)
echo.
echo Cua so tu dong sau 30 giay (he thong van chay nen).
timeout /t 30
