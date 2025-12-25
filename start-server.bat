@echo off
echo ============================================
echo  Starting Collaborative Drawing Server
echo ============================================
echo.

cd /d "%~dp0DrawingServer"
dotnet run

pause
