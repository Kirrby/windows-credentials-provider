HeartMonitor Windows Credential Provider
========================================

This package contains:
1. WindowsCredentialProviderTest.dll - The credential provider DLL
2. HeartMonitor application - Monitors phone status
3. Configuration files
4. Installation/uninstallation scripts

Installation:
1. Run install.bat as Administrator
2. Restart your computer

Uninstallation:
1. Run uninstall.bat as Administrator
2. Restart your computer

Configuration:
- Config file: config.xml
- Default settings are pre-configured
- Modify as needed before installation

Requirements:
- Windows 10/11 (64-bit)
- Administrator privileges
- .NET Framework 4.8 or later

Notes:
- This credential provider allows phone-based unlocking when HeartMonitor detects the phone is nearby
- The provider shows user name, welcome message, and unlock status with timestamp
- When unlock is available, you can press Enter to unlock