using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using TozoWindowsApp.Ble;
using System.Windows;
using System.Windows.Threading;
using System;

namespace TozoWindowsApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly TozoDeviceManager _deviceManager;
        private readonly Dispatcher _dispatcher;

        private string _connectionStatus = "Disconnected";
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                _connectionStatus += "\n" + value;
                OnPropertyChanged();
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDisconnected))]
        private bool _isConnected;

        public bool IsDisconnected => !IsConnected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LeftBatteryText), nameof(IsLeftBatteryKnown))]
        private int _leftBattery = -1;

        public string LeftBatteryText => LeftBattery >= 0 ? $"{LeftBattery}%" : "--%";
        public bool IsLeftBatteryKnown => LeftBattery >= 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RightBatteryText), nameof(IsRightBatteryKnown))]
        private int _rightBattery = -1;

        public string RightBatteryText => RightBattery >= 0 ? $"{RightBattery}%" : "--%";
        public bool IsRightBatteryKnown => RightBattery >= 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CaseBatteryText), nameof(IsCaseBatteryKnown))]
        private int _caseBattery = -1;

        public string CaseBatteryText => CaseBattery >= 0 ? $"{CaseBattery}%" : "--%";
        public bool IsCaseBatteryKnown => CaseBattery >= 0;

        public TozoProtocol.AncMode CurrentAncMode
        {
            get => Settings.SettingsManager.Current.CurrentAncMode;
            set
            {
                if (Settings.SettingsManager.Current.CurrentAncMode != value)
                {
                    Settings.SettingsManager.Current.CurrentAncMode = value;
                    Settings.SettingsManager.Save();
                    OnPropertyChanged();
                }
            }
        }

        public bool RememberMySettings
        {
            get => Settings.SettingsManager.Current.RememberMySettings;
            set
            {
                if (Settings.SettingsManager.Current.RememberMySettings != value)
                {
                    Settings.SettingsManager.Current.RememberMySettings = value;
                    Settings.SettingsManager.Save();
                    OnPropertyChanged();
                }
            }
        }

        public enum AppView
        {
            Home,
            Box,
            Eartune,
            Anc
        }

        [ObservableProperty]
        private AppView _currentView = AppView.Home;

        [RelayCommand]
        private void OpenBox()
        {
            CurrentView = AppView.Box;
        }

        [RelayCommand]
        private void OpenEartune()
        {
            CurrentView = AppView.Eartune;
        }

        [RelayCommand]
        private void OpenAnc()
        {
            CurrentView = AppView.Anc;
        }

        [RelayCommand]
        private void CloseView()
        {
            CurrentView = AppView.Home;
        }

        [RelayCommand]
        private async Task SetAncModeAsync(TozoProtocol.AncMode mode)
        {
            CurrentAncMode = mode;
            ConnectionStatus = $"Setting ANC Mode: {mode}...";
            await _deviceManager.SetAncModeAsync(mode, RememberMySettings);
        }

        public MainViewModel()
        {
            _deviceManager = new TozoDeviceManager(new BleService());
            _dispatcher = Application.Current.Dispatcher;

            _deviceManager.OnStatusChanged += status =>
            {
                ConnectionStatus = status;
            };

            _deviceManager.OnConnectedChanged += isConnected =>
            {
                IsConnected = isConnected;
            };

            _deviceManager.OnBatteryLevelReceived += battery =>
            {
                _dispatcher.Invoke(() =>
                {
                    ConnectionStatus = $"Parsed Battery: L={battery.Left} R={battery.Right} C={battery.Case}";
                    if (battery.Left >= 0) LeftBattery = battery.Left;
                    if (battery.Right >= 0) RightBattery = battery.Right;
                    if (battery.Case >= 0) CaseBattery = battery.Case;
                });
            };
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            await _deviceManager.ConnectAsync("TOZO NC20 Pro");
        }

        [RelayCommand]
        private void Disconnect()
        {
            _deviceManager.Disconnect();
        }

    }
}
