@echo off
setlocal
set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
set "OUT=%~dp0tests\NetworkTests.exe"

"%FW%\csc.exe" /nologo /target:exe "/out:%OUT%" /r:System.dll /r:System.Core.dll "%~dp0source\Network.cs" "%~dp0tests\NetworkTests.cs"
if errorlevel 1 exit /b 1
"%OUT%"
