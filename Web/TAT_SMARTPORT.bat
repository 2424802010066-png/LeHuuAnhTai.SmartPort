@echo off
chcp 65001 >nul
title SmartPort - Tat he thong
cd /d "%~dp0"
echo Dang tat 13 dich vu .NET + AI Python + API gateway + RabbitMQ...
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0MicroServices\dung_microservices.ps1" -TatRabbitMQ
echo.
echo Da tat SmartPort. (Docker Desktop va SQL Server van chay - tat rieng neu muon.)
timeout /t 10
