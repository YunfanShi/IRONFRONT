@echo off
setlocal
cd /d "%~dp0"
echo IRONFRONT
echo 1. Dedicated server - Mac players connect to this Windows computer
echo 2. Host and play - run the server and open a local browser
choice /c 12 /n /m "Choose mode [1/2]: "
if errorlevel 2 (node scripts\start-lan.cjs --mode host) else (node scripts\start-lan.cjs --mode dedicated)
pause
