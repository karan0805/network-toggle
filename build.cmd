@echo off
setlocal
set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
set "WPF=%FW%\WPF"
set "SRC=%~dp0source"
set "OUT=%~1"
if "%OUT%"=="" set "OUT=%~dp0Network Toggle.exe"

"%FW%\csc.exe" /nologo /target:winexe /optimize+ "/out:%OUT%" "/win32manifest:%SRC%\app.manifest" "/win32icon:%SRC%\app.ico" "/resource:%SRC%\MainWindow.xaml,MainWindow.xaml" /r:System.dll /r:System.Core.dll /r:System.Xml.dll "/r:%FW%\System.Xaml.dll" "/r:%WPF%\WindowsBase.dll" "/r:%WPF%\PresentationCore.dll" "/r:%WPF%\PresentationFramework.dll" "%SRC%\*.cs"
if errorlevel 1 exit /b 1
echo Built %OUT%
