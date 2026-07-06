@echo off
setlocal

dotnet publish -c Release -o publish
if errorlevel 1 exit /b %errorlevel%

echo.
echo Published: %CD%\publish\Launcher.exe
