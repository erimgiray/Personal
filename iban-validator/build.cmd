@echo off
REM Builds iban.exe using the C# compiler that ships with Windows.
REM No SDK, no NuGet, no downloads.
setlocal
pushd "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Could not find csc.exe under %WINDIR%\Microsoft.NET
  popd
  exit /b 1
)

"%CSC%" /nologo /target:exe /optimize+ /out:iban.exe src\Program.cs src\Iban.cs src\Registry.cs src\SelfTest.cs
if errorlevel 1 (
  popd
  exit /b 1
)

echo Built iban.exe
echo.
".\iban.exe" --selftest
set RESULT=%ERRORLEVEL%
popd
exit /b %RESULT%
