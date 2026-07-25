using System;
using System.Threading.Tasks;

namespace TozoWindowsApp.Ble
{
    public class TozoDeviceManager
    {
        private readonly IBleService _bleService;

        public event Action<string>? OnStatusChanged;
        public event Action<bool>? OnConnectedChanged;
        public event Action<TozoProtocol.BatteryInfo>? OnBatteryLevelReceived;

        public bool IsConnected => _bleService.IsConnected;

        public TozoDeviceManager(IBleService bleService)
        {
            _bleService = bleService;
            _bleService.OnStatusMessage += msg => OnStatusChanged?.Invoke(msg);
            _bleService.OnConnectionStateChanged += state => 
            {
                if (state)
                {
                    _ = InitializeDeviceAsync();
                }
                OnConnectedChanged?.Invoke(state);
            };
            _bleService.OnDataReceived += HandleDataReceived;
        }

        public async Task ConnectAsync(string deviceName = "TOZO NC20 Pro")
        {
            await _bleService.ConnectAsync(deviceName);
        }

        public void Disconnect()
        {
            _bleService.Disconnect();
        }

        private async Task InitializeDeviceAsync()
        {
            await Task.Delay(500);
            OnStatusChanged?.Invoke("Broadcasting Init Handshakes...");
            await _bleService.WriteDataAsync(TozoProtocol.GetAirohaInit());
            await _bleService.WriteDataAsync(TozoProtocol.GetJuXinInit());

            await Task.Delay(500);
            OnStatusChanged?.Invoke("Pushing Saved Settings...");
            
            // Push "Remember My Settings" preference
            bool remember = Settings.SettingsManager.Current.RememberMySettings;
            await _bleService.WriteDataAsync(TozoProtocol.GetAirohaRememberSettings(remember));
            await _bleService.WriteDataAsync(TozoProtocol.GetJuXinRememberSettings(remember));
            await Task.Delay(200);

            // Push ANC Mode preference
            var mode = Settings.SettingsManager.Current.CurrentAncMode;
            await _bleService.WriteDataAsync(TozoProtocol.GetAirohaAncCommand(mode));
            await _bleService.WriteDataAsync(TozoProtocol.GetJuXinAncCommand(mode));
            await Task.Delay(200);

            OnStatusChanged?.Invoke("Broadcasting Battery Requests...");
            await _bleService.WriteDataAsync(TozoProtocol.GetAirohaBatteryRequest());
            await _bleService.WriteDataAsync(TozoProtocol.GetJuXinBatteryRequest());
            
            StartBatteryPolling();
        }

        private void HandleDataReceived(byte[] rawData)
        {
            var batteryInfo = TozoProtocol.ParseBatteryPacket(rawData);
            if (batteryInfo != null)
            {
                OnBatteryLevelReceived?.Invoke(batteryInfo);
            }
        }

        private System.Windows.Threading.DispatcherTimer? _batteryTimer;

        public async Task SetAncModeAsync(TozoProtocol.AncMode mode, bool rememberSettings)
        {
            // Update remember settings preference
            await _bleService.WriteDataAsync(TozoProtocol.GetAirohaRememberSettings(rememberSettings));
            await _bleService.WriteDataAsync(TozoProtocol.GetJuXinRememberSettings(rememberSettings));

            // Set ANC Mode
            await _bleService.WriteDataAsync(TozoProtocol.GetAirohaAncCommand(mode));
            await _bleService.WriteDataAsync(TozoProtocol.GetJuXinAncCommand(mode));
        }

        private void StartBatteryPolling()
        {
            if (_batteryTimer == null)
            {
                _batteryTimer = new System.Windows.Threading.DispatcherTimer();
                _batteryTimer.Interval = TimeSpan.FromSeconds(10);
                _batteryTimer.Tick += async (s, e) =>
                {
                    if (IsConnected)
                    {
                        await _bleService.WriteDataAsync(TozoProtocol.GetAirohaBatteryRequest());
                        await _bleService.WriteDataAsync(TozoProtocol.GetJuXinBatteryRequest());
                    }
                    else
                    {
                        StopBatteryPolling();
                    }
                };
            }
            _batteryTimer.Start();
        }

        private void StopBatteryPolling()
        {
            _batteryTimer?.Stop();
        }
    }
}
