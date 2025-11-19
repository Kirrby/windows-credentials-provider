// Uncomment for autologin
// #define AUTOLOGIN

using CredentialProvider.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using WindowsCredentialProviderTest.OnDemandLogon;

namespace WindowsCredentialProviderTest
{
    [ComVisible(true)]
    [Guid(Constants.CredentialProviderTileUID)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class TestWindowsCredentialProviderTile : ITestWindowsCredentialProviderTile
    {
        // Field IDs for our credential provider
        private const uint FIELD_USER_NAME = 0;    // 显示用户名（大字体）
        private const uint FIELD_WELCOME_MESSAGE = 1;  // 显示欢迎语
        private const uint FIELD_STATUS_INFO = 2;  // 显示解锁状态和时间

        public List<_CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR> CredentialProviderFieldDescriptorList = new List<_CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR> {
            new _CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR
            {
                cpft = _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_LARGE_TEXT,  // 大字体
                dwFieldID = FIELD_USER_NAME,
                pszLabel = "", // 初始化为空，稍后从配置加载
            },
            new _CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR
            {
                cpft = _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_SMALL_TEXT,  // 普通字体
                dwFieldID = FIELD_WELCOME_MESSAGE,
                pszLabel = "", // 初始化为空，稍后从配置加载
            },
            new _CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR
            {
                cpft = _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_SMALL_TEXT,  // 小字体状态信息
                dwFieldID = FIELD_STATUS_INFO,
                pszLabel = "", // 初始化为空
            },
            new _CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR  // 提交按钮
            {
                cpft = _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_SUBMIT_BUTTON,  // 提交按钮
                dwFieldID = 3,  // 提交按钮ID
                pszLabel = "Unlock", // 提交按钮标签
            }
        };

        private readonly TestWindowsCredentialProvider testWindowsCredentialProvider;
        private readonly _CREDENTIAL_PROVIDER_USAGE_SCENARIO usageScenario;
        private ICredentialProviderCredentialEvents credentialProviderCredentialEvents;
        private TimerOnDemandLogon timerOnDemandLogon;
        private bool shouldAutoLogin = false;
        private System.Threading.Timer statePollingTimer;
        private readonly int POLLING_INTERVAL_MS = CredentialProviderConfig.LoadConfig().StatePollingIntervalMs;
        private bool lastAllowUnlockState = false;

        public TestWindowsCredentialProviderTile(
            TestWindowsCredentialProvider testWindowsCredentialProvider,
            _CREDENTIAL_PROVIDER_USAGE_SCENARIO usageScenario
        )
        {
            this.testWindowsCredentialProvider = testWindowsCredentialProvider;
            this.usageScenario = usageScenario;

            // Initialize state polling timer
            StartStatePolling();
        }

        public int Advise(ICredentialProviderCredentialEvents pcpce)
        {
            Log.LogMethodCall();

            if (pcpce != null)
            {
                credentialProviderCredentialEvents = pcpce;
                var intPtr = Marshal.GetIUnknownForObject(pcpce);
                Marshal.AddRef(intPtr);
            }

            return HResultValues.S_OK;
        }

        private void StartStatePolling()
        {
            // Create a timer that checks the HeartMonitor state periodically
            statePollingTimer = new System.Threading.Timer(
                callback: (state) => PollHeartMonitorState(),
                state: null,
                dueTime: TimeSpan.FromMilliseconds(POLLING_INTERVAL_MS),
                period: TimeSpan.FromMilliseconds(POLLING_INTERVAL_MS)
            );
        }

        private void PollHeartMonitorState()
        {
            try
            {
                var currentState = HeartMonitorStateManager.ReadState();
                bool currentAllowUnlockState = currentState != null && currentState.allow_unlock;

                var config = CredentialProviderConfig.LoadConfig();

                // Always update the status info field to show current state (including timestamp and symbol)
                if (credentialProviderCredentialEvents != null)
                {
                    try
                    {
                        // Update the status info field with current state and time
                        string statusInfo;
                        if (currentState != null)
                        {
                            // 根据allow_unlock状态选择符号
                            string statusSymbol = currentState.allow_unlock ? config.AvailableSymbol : config.UnavailableSymbol;

                            // 格式化时间
                            string timeString = DateTime.Now.ToString(config.TimeFormat);

                            // 格式化状态信息
                            statusInfo = string.Format(config.StatusFormat, statusSymbol, timeString);
                        }
                        else
                        {
                            statusInfo = "No state file";
                        }

                        credentialProviderCredentialEvents.SetFieldString(this, FIELD_STATUS_INFO, statusInfo);

                        // Update submit button visibility based on unlock status
                        if (currentState != null && currentState.allow_unlock)
                        {
                            credentialProviderCredentialEvents.SetFieldState(this, 3,
                                _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_BOTH);
                        }
                        else
                        {
                            credentialProviderCredentialEvents.SetFieldState(this, 3,
                                _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_HIDDEN);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.LogText($"Error updating status info field: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogText($"Error polling HeartMonitor state: {ex.Message}");
            }
        }

        public int UnAdvise()
        {
            Log.LogMethodCall();

            if (credentialProviderCredentialEvents != null)
            {
                var intPtr = Marshal.GetIUnknownForObject(credentialProviderCredentialEvents);
                Marshal.Release(intPtr);
                credentialProviderCredentialEvents = null;
            }

            // Dispose of the polling timer
            statePollingTimer?.Dispose();
            statePollingTimer = null;

            return HResultValues.S_OK;
        }

        ~TestWindowsCredentialProviderTile()
        {
            // Finalizer to ensure timer is disposed
            statePollingTimer?.Dispose();
        }

        public int SetSelected(out int pbAutoLogon)
        {
            // Check if phone unlock is allowed when the tile is selected
            bool isPhoneUnlockAllowed = HeartMonitorStateManager.IsPhoneUnlockAllowed();

#if AUTOLOGIN
            if (!shouldAutoLogin)
            {
                timerOnDemandLogon = new TimerOnDemandLogon(
                    testWindowsCredentialProvider.CredentialProviderEvents,
                    credentialProviderCredentialEvents,
                    this,
                    CredentialProviderFieldDescriptorList[0].dwFieldID,
                    testWindowsCredentialProvider.CredentialProviderEventsAdviseContext);

                timerOnDemandLogon.TimerEnded += TimerOnDemandLogon_TimerEnded;

                pbAutoLogon = 0;
            }
            else
            {
                // We got the info from the async timer
                pbAutoLogon = 1;
            }
#else
            pbAutoLogon = 0; // Auto-logon when the tile is selected
#endif

            return HResultValues.S_OK;
        }

        private void TimerOnDemandLogon_TimerEnded()
        {
            // Sync other data from your async service here
            shouldAutoLogin = true;
        }

        public int SetDeselected()
        {
            Log.LogMethodCall();

            timerOnDemandLogon?.Dispose();
            timerOnDemandLogon = null;

            // We don't dispose the polling timer here since we want to keep monitoring
            // the HeartMonitor state even when the tile is not selected

            return HResultValues.E_NOTIMPL;
        }

        public int GetFieldState(uint dwFieldID, out _CREDENTIAL_PROVIDER_FIELD_STATE pcpfs,
            out _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE pcpfis)
        {
            // Check the current HeartMonitor state
            bool isPhoneUnlockAllowed = HeartMonitorStateManager.IsPhoneUnlockAllowed();

            if (dwFieldID == FIELD_USER_NAME || dwFieldID == FIELD_WELCOME_MESSAGE)
            {
                // Always show user name and welcome message
                pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_BOTH;
                pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_NONE;
            }
            else if (dwFieldID == FIELD_STATUS_INFO)
            {
                // Always show the status info field
                pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_BOTH;
                pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_NONE;
            }
            else if (dwFieldID == 3)  // Submit button
            {
                // Check the current HeartMonitor state

                // Show submit button only when unlock is allowed, but make it invisible
                // We still need it for Enter key functionality
                if (isPhoneUnlockAllowed)
                {
                    pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_BOTH;
                    pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_NONE;
                }
                else
                {
                    pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_HIDDEN;
                    pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_NONE;
                }
            }
            else
            {
                // Default behavior for other fields
                pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_NONE;
                pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_BOTH;
            }

            return HResultValues.S_OK;
        }

        public int GetStringValue(uint dwFieldID, out string ppsz)
        {
            var config = CredentialProviderConfig.LoadConfig();

            if (dwFieldID == FIELD_USER_NAME)
            {
                // 显示自定义用户名
                ppsz = config.CustomUsername;
                return HResultValues.S_OK;
            }
            else if (dwFieldID == FIELD_WELCOME_MESSAGE)
            {
                // 显示欢迎语
                ppsz = config.WelcomeMessage;
                return HResultValues.S_OK;
            }
            else if (dwFieldID == FIELD_STATUS_INFO)
            {
                // 显示解锁状态和时间信息
                var state = HeartMonitorStateManager.ReadState();
                if (state != null)
                {
                    // 根据allow_unlock状态选择符号
                    string statusSymbol = state.allow_unlock ? config.AvailableSymbol : config.UnavailableSymbol;

                    // 格式化时间
                    string timeString = DateTime.Now.ToString(config.TimeFormat);

                    // 格式化状态信息
                    ppsz = string.Format(config.StatusFormat, statusSymbol, timeString);
                }
                else
                {
                    ppsz = "No state file";
                }
                return HResultValues.S_OK;
            }
            else if (dwFieldID == 3)  // Submit button
            {
                // 返回解锁按钮文本
                ppsz = "Unlock";
                return HResultValues.S_OK;
            }

            var searchFunction = FieldSearchFunctionGenerator(dwFieldID, new[]
            {
                _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_SMALL_TEXT,
                _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_LARGE_TEXT,
            });

            if (!CredentialProviderFieldDescriptorList.Any(searchFunction))
            {
                ppsz = string.Empty;
                return HResultValues.E_NOTIMPL;
            }

            var descriptor = CredentialProviderFieldDescriptorList.First(searchFunction);

            ppsz = descriptor.pszLabel;
            return HResultValues.S_OK;
        }

        public int GetBitmapValue(uint dwFieldID, IntPtr phbmp)
        {
            Log.LogMethodCall();

            var searchFunction = FieldSearchFunctionGenerator(dwFieldID, new[] { _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_TILE_IMAGE });

            if (!CredentialProviderFieldDescriptorList.Any(searchFunction))
            {
                phbmp = IntPtr.Zero;
                return HResultValues.E_NOTIMPL;
            }

            var descriptor = CredentialProviderFieldDescriptorList.First(searchFunction);
            phbmp = IntPtr.Zero; // TODO: show a bitmap

            return HResultValues.E_NOTIMPL;
        }

        public int GetCheckboxValue(uint dwFieldID, out int pbChecked, out string ppszLabel)
        {
            Log.LogMethodCall();

            var searchFunction = FieldSearchFunctionGenerator(dwFieldID, new[] { _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_CHECKBOX });

            if (!CredentialProviderFieldDescriptorList.Any(searchFunction))
            {
                pbChecked = 0;
                ppszLabel = string.Empty;
                return HResultValues.E_NOTIMPL;
            }

            var descriptor = CredentialProviderFieldDescriptorList.First(searchFunction);
            pbChecked = 0; // TODO: selection state
            ppszLabel = descriptor.pszLabel;

            return HResultValues.E_NOTIMPL;
        }

        public int GetSubmitButtonValue(uint dwFieldID, out uint pdwAdjacentTo)
        {
            var searchFunction = FieldSearchFunctionGenerator(dwFieldID, new[] { _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_SUBMIT_BUTTON });

            if (!CredentialProviderFieldDescriptorList.Any(searchFunction))
            {
                pdwAdjacentTo = 0;
                return HResultValues.E_NOTIMPL;
            }

            // For the submit button (dwFieldID == 3), link it with the status info field
            // so that Enter key functionality works when status allows unlock
            pdwAdjacentTo = FIELD_STATUS_INFO;

            return HResultValues.S_OK;
        }


        public int GetComboBoxValueCount(uint dwFieldID, out uint pcItems, out uint pdwSelectedItem)
        {
            Log.LogMethodCall();

            var searchFunction = FieldSearchFunctionGenerator(dwFieldID, new[] { _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_COMBOBOX });

            if (!CredentialProviderFieldDescriptorList.Any(searchFunction))
            {
                pcItems = 0;
                pdwSelectedItem = 0;
                return HResultValues.E_NOTIMPL;
            }

            var descriptor = CredentialProviderFieldDescriptorList.First(searchFunction);
            pcItems = 0; // TODO: selection state
            pdwSelectedItem = 0;

            return HResultValues.E_NOTIMPL;
        }

        public int GetComboBoxValueAt(uint dwFieldID, uint dwItem, out string ppszItem)
        {
            Log.LogMethodCall();
            ppszItem = string.Empty;
            return HResultValues.E_NOTIMPL;
        }

        public int SetStringValue(uint dwFieldID, string psz)
        {
            Log.LogMethodCall();

            // TODO: change state

            return HResultValues.E_NOTIMPL;
        }

        public int SetCheckboxValue(uint dwFieldID, int bChecked)
        {
            Log.LogMethodCall();

            // TODO: change state

            return HResultValues.E_NOTIMPL;
        }

        public int SetComboBoxSelectedValue(uint dwFieldID, uint dwSelectedItem)
        {
            Log.LogMethodCall();

            // TODO: change state

            return HResultValues.E_NOTIMPL;
        }

        public int CommandLinkClicked(uint dwFieldID)
        {
            Log.LogMethodCall();

            // TODO: change state

            return HResultValues.E_NOTIMPL;
        }

        // Note: GetSubmitButtonValue is implemented above (returns adjacency for submit button).

        public int GetSerialization(out _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE pcpgsr,
            out _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION pcpcs, out string ppszOptionalStatusText,
            out _CREDENTIAL_PROVIDER_STATUS_ICON pcpsiOptionalStatusIcon)
        {
            try
            {
                // Check HeartMonitor state before allowing phone unlock
                bool isPhoneUnlockAllowed = HeartMonitorStateManager.IsPhoneUnlockAllowed();

                if (!isPhoneUnlockAllowed)
                {
                    // If phone unlock is not allowed, return error
                    pcpgsr = _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE.CPGSR_NO_CREDENTIAL_NOT_FINISHED;
                    pcpcs = new _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION();
                    ppszOptionalStatusText = "Phone not available for unlock";
                    pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_ERROR;
                    return HResultValues.S_OK;
                }

                // Phone is available, proceed with phone unlock
                pcpgsr = _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE.CPGSR_RETURN_CREDENTIAL_FINISHED;
                pcpcs = new _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION();

                // Use current user for phone unlock
                var config = CredentialProviderConfig.LoadConfig();
                var username = config.DefaultUsername;
                var password = config.DefaultPassword;
                var inCredSize = 0;
                var inCredBuffer = Marshal.AllocCoTaskMem(0);

                if (!PInvoke.CredPackAuthenticationBuffer(0, username, password, inCredBuffer, ref inCredSize))
                {
                    Marshal.FreeCoTaskMem(inCredBuffer);
                    inCredBuffer = Marshal.AllocCoTaskMem(inCredSize);

                    if (PInvoke.CredPackAuthenticationBuffer(0, username, password, inCredBuffer, ref inCredSize))
                    {
                        ppszOptionalStatusText = "Phone unlock successful";
                        pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_SUCCESS;

                        pcpcs.clsidCredentialProvider = Guid.Parse(Constants.CredentialProviderUID);
                        pcpcs.rgbSerialization = inCredBuffer;
                        pcpcs.cbSerialization = (uint)inCredSize;

                        RetrieveNegotiateAuthPackage(out var authPackage);
                        pcpcs.ulAuthenticationPackage = authPackage;

                        return HResultValues.S_OK;
                    }
                    else
                    {
                        ppszOptionalStatusText = "Failed to pack credentials";
                        pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_ERROR;
                        return HResultValues.E_FAIL;
                    }
                }
                else
                {
                    // If the first call succeeded
                    ppszOptionalStatusText = "Phone unlock successful";
                    pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_SUCCESS;

                    pcpcs.clsidCredentialProvider = Guid.Parse(Constants.CredentialProviderUID);
                    pcpcs.rgbSerialization = inCredBuffer;
                    pcpcs.cbSerialization = (uint)inCredSize;

                    RetrieveNegotiateAuthPackage(out var authPackage);
                    pcpcs.ulAuthenticationPackage = authPackage;

                    return HResultValues.S_OK;
                }
            }
            catch (Exception)
            {
                // In case of any error, do not bring down winlogon
            }
            finally
            {
                shouldAutoLogin = false; // Block auto-login from going full-retard
            }

            pcpgsr = _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE.CPGSR_NO_CREDENTIAL_NOT_FINISHED;
            pcpcs = new _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION();
            ppszOptionalStatusText = string.Empty;
            pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_NONE;
            return HResultValues.E_NOTIMPL;
        }

        public int ReportResult(int ntsStatus, int ntsSubstatus, out string ppszOptionalStatusText,
            out _CREDENTIAL_PROVIDER_STATUS_ICON pcpsiOptionalStatusIcon)
        {
            Log.LogMethodCall();
            ppszOptionalStatusText = string.Empty;
            pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_NONE;
            return HResultValues.E_NOTIMPL;
        }

        private int RetrieveNegotiateAuthPackage(out uint authPackage)
        {
            // TODO: better checking on the return codes

            var status = PInvoke.LsaConnectUntrusted(out var lsaHandle);

            using (var name = new PInvoke.LsaStringWrapper("Negotiate"))
            {
                status = PInvoke.LsaLookupAuthenticationPackage(lsaHandle, ref name._string, out authPackage);
            }

            PInvoke.LsaDeregisterLogonProcess(lsaHandle);

            return (int)status;
        }

        private Func<_CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR, bool> FieldSearchFunctionGenerator(uint dwFieldID, _CREDENTIAL_PROVIDER_FIELD_TYPE[] allowedFieldTypes)
        {
            return x =>
                x.dwFieldID == dwFieldID
                && allowedFieldTypes.Contains(x.cpft);
        }
    }
}