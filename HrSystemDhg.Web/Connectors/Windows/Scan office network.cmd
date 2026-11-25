@echo off
cd /d "%~dp0"
py -3 --version >nul 2>&1
if not errorlevel 1 (
  py -3 attendance_connector.py --scan
) else (
  python --version >nul 2>&1
  if errorlevel 1 (
    echo Install Python 3 from python.org/downloads/windows/ and run this file again.
  ) else (
    python attendance_connector.py --scan
  )
)
pause
