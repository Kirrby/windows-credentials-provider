@echo off
echo Installing Windows Credential Provider...
echo.

REM Register the DLL
regsvr32 /s "%~dp0WindowsCredentialProviderTest.dll"

REM Add to Windows Credential Providers
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers\{298D9F84-9BC5-435C-9FC2-EB3746625954}" /ve /d "HeartMonitor Credential Provider" /f

echo Installation completed.
echo.
echo Please restart your computer to apply changes.
pause