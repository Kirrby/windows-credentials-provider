@echo off
echo Uninstalling Windows Credential Provider...
echo.

REM Remove from Windows Credential Providers
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers\{298D9F84-9BC5-435C-9FC2-EB3746625954}" /f

REM Unregister the DLL
regsvr32 /u /s "%~dp0WindowsCredentialProviderTest.dll"

REM Remove the DLL file (after unregistering)
REM Note: The DLL file will need to be manually deleted after restart
echo DLL file will be removed after restart.

echo Uninstallation completed.
echo.
echo Please restart your computer to complete removal.
pause