@echo off
title RoomRevive Admin (port 4173) - keep this window open
cd /d "C:\Unity-Git\RoomRevive\HTML_Editor\admin"
echo ============================================================
echo  RoomRevive Admin dashboard
echo  This PC:  http://localhost:4173
echo  Phone (same Wi-Fi):  http://192.168.1.222:4173
echo  Keep this window open. Close it to stop the server.
echo ============================================================
node server.js
echo.
echo Server stopped. Press a key to close.
pause >nul
