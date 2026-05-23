@echo off
setlocal

echo.
echo  Ruka -- NuGet Restore
echo  ----------------------------------------
echo.

where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo  [ERROR] dotnet CLI not found.
    echo  Install .NET 6 or later: https://dotnet.microsoft.com/download
    echo.
    pause
    exit /b 1
)

dotnet tool list -g | findstr /i "nugetforunity" >nul 2>&1
if %errorlevel% neq 0 (
    echo  Installing NuGetForUnity.Cli...
    dotnet tool install --global NuGetForUnity.Cli
    if %errorlevel% neq 0 (
        echo  [ERROR] Installation failed.
        pause
        exit /b 1
    )
)

nugetforunity restore "%~dp0"
if %errorlevel% neq 0 (
    echo  [ERROR] NuGet restore failed.
    pause
    exit /b 1
)

echo.
echo  Done.
pause
