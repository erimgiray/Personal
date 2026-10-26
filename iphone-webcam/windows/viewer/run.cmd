@echo off
rem Usage: run.cmd <phone-ip>
rem First run installs the Python packages, later runs start straight away.
cd /d "%~dp0"
if "%~1"=="" (
  echo Usage: run.cmd ^<phone IP shown in the iPhoneCam app^>
  exit /b 1
)
py -c "import av, cv2" 2>nul || py -m pip install -r requirements.txt
py viewer.py %*
