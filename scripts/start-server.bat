@echo off
REM Kiko's Mayhem — start the SCP:SL dedicated server.
REM Usage: start-server.bat [ServerDir] [Port]
set SERVERDIR=%~1
if "%SERVERDIR%"=="" set SERVERDIR=C:\SCPSL-Server
set PORT=%~2
if "%PORT%"=="" set PORT=7777

cd /d "%SERVERDIR%"
echo Starting SCP:SL server on port %PORT% (Kiko's Mayhem)...
LocalAdmin.exe %PORT%
pause
