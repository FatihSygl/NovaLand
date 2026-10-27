@echo off
setlocal
cd /d "%~dp0"
title NovaLand - Windows Build

echo =============================================
echo        NOVALAND - BUILD FOR WINDOWS
 echo =============================================
where dotnet >nul 2>nul
if errorlevel 1 (
    echo.
    echo ERROR: .NET 10 SDK is not installed.
    echo Install the official SDK from: https://dotnet.microsoft.com/en-us/download/dotnet/10.0
    echo Then double-click this file again.
    pause
    exit /b 1
)
echo.
echo Building the Windows x64 release (requires .NET Desktop Runtime 10)...
dotnet publish "NovaLand.csproj" -c Release -r win-x64 -p:SelfContained=false -p:PublishSelfContained=false -p:PublishSingleFile=false -p:DebugType=None -o "%~dp0publish"
if errorlevel 1 (
    echo.
    echo ERROR: Build failed. Please copy the errors above.
    pause
    exit /b 1
)
echo.
echo SUCCESS: %~dp0publish\NovaLand.exe
explorer "%~dp0publish"
pause
