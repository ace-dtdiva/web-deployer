@echo off
setlocal
cd /d "%~dp0"

set "DOTNET=dotnet"
if exist "%ProgramFiles%\dotnet\dotnet.exe" set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"

"%DOTNET%" publish src\WebDeployer\WebDeployer.csproj -c Release -o publish --nologo
if errorlevel 1 (
  echo.
  echo BUILD FALLITO
  exit /b 1
)

echo.
echo OK: publish\WebDeployer.exe
