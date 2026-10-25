@echo off
setlocal
title Facial Treatment Website

echo Starting Facial Treatment Website...
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: .NET SDK was not found. Install .NET 8 SDK, then reopen this script.
    echo Download: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

where sqllocaldb >nul 2>nul
if errorlevel 1 (
    echo ERROR: SQL Server LocalDB was not found.
    echo Install SQL Server Express with the LocalDB feature, then reopen this script.
    pause
    exit /b 1
)

sqllocaldb start MSSQLLocalDB
if errorlevel 1 (
    echo ERROR: Could not start the MSSQLLocalDB database instance.
    pause
    exit /b 1
)

cd /d "%~dp0Assignment"
if not exist "Assignment.csproj" (
    echo ERROR: Could not find Assignment\Assignment.csproj beside this script.
    pause
    exit /b 1
)
if not exist "DB.mdf" (
    echo ERROR: Assignment\DB.mdf was not found.
    echo Follow the database steps in Setup.md, then run this script again.
    pause
    exit /b 1
)

echo.
echo Website URL: http://localhost:5043
echo Keep this window open while using the website. Press Ctrl+C to stop it.
echo.
dotnet run --launch-profile http

echo.
echo The website process has stopped.
pause
