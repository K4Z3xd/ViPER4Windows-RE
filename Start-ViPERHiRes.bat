@echo off
title ViPERHiRes Audio DSP Console
cd /d "%~dp0ui"
echo Starting ViPERHiRes Console at http://127.0.0.1:48192 ...
start "" "http://127.0.0.1:48192"
"C:\Program Files\nodejs\node.exe" server.mjs
pause
