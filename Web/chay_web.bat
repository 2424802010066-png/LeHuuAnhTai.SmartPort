@echo off
chcp 65001 >nul
title SmartPort - He thong dieu hanh cang container
cd /d "%~dp0SmartPort.Api"

echo.
echo  ============================================================
echo   SmartPort dang khoi dong...
echo   Website:      http://localhost:5200
echo   Khu dieu hanh: http://localhost:5200/admin
echo   Tai lieu API:  http://localhost:5200/swagger
echo  ============================================================
echo   (Dong cua so nay hoac bam Ctrl+C de tat may chu)
echo.

rem Mo trinh duyet sau 12 giay, cho may chu kip khoi dong
start "" cmd /c "timeout /t 12 >nul & start http://localhost:5200"

dotnet run --launch-profile SmartPort.Api --no-launch-browser
pause
