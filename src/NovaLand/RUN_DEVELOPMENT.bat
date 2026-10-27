@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
    echo Install the .NET 10 SDK from https://dotnet.microsoft.com/en-us/download/dotnet/10.0
    pause
    exit /b 1
)
dotnet run --project NovaLand.csproj
if errorlevel 1 pause
