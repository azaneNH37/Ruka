@echo off
setlocal

echo.
echo  Ruka Framework Setup
echo  ----------------------------------------
echo.

:: Check Node.js
where node >nul 2>&1
if %errorlevel% neq 0 (
    echo  [ERROR] Node.js not found.
    echo.
    echo  Please install Node.js 18 or later, then re-run this script:
    echo  https://nodejs.org
    echo.
    pause
    exit /b 1
)

:: Check Node.js version ^>=18
node -e "process.exit(parseInt(process.versions.node)>=18?0:1)" >nul 2>&1
if %errorlevel% neq 0 (
    echo  [ERROR] Node.js version too old. Requires 18 or later.
    for /f %%v in ('node --version') do echo  Current version: %%v
    echo.
    pause
    exit /b 1
)

:: Move to the SetUp directory
cd /d "%~dp0"

:: npm install on first run
if not exist "node_modules\" (
    echo  First run: installing dependencies...
    echo.
    call npm install --silent
    if %errorlevel% neq 0 (
        echo.
        echo  [ERROR] npm install failed. Check your network connection.
        pause
        exit /b 1
    )
    echo.
)

:: Launch setup script, forwarding all arguments
node ruka-setup.js %*

echo.
pause
