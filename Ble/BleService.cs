using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using System.Windows;
using System.Linq;

namespace TozoWindowsApp.Ble
{
    public class BleService : IBleService
    {
        public event Action<string>? OnStatusMessage;
        public event Action<bool>? OnConnectionStateChanged;
        public event Action<byte[]>? OnDataReceived;

        public bool IsConnected => _devices.Any(d => d.ConnectionStatus == BluetoothConnectionStatus.Connected);

        private List<BluetoothLEDevice> _devices = new List<BluetoothLEDevice>();
        private HashSet<string> _connectedDeviceIds = new HashSet<string>();
        private DeviceWatcher? _deviceWatcher;
        private List<GattCharacteristic> _txCharacteristics = new List<GattCharacteristic>();
        private List<GattCharacteristic> _rxCharacteristics = new List<GattCharacteristic>();
        private string _targetDeviceName = "";

        public Task ConnectAsync(string targetDeviceName)
        {
            if (_connectedDeviceIds.Count > 0) return Task.CompletedTask;

            _targetDeviceName = targetDeviceName;

            ReportStatus($"Scanning for BLE Endpoints containing '{targetDeviceName}'...");

            string[] requestedProperties = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };
            _deviceWatcher = DeviceInformation.CreateWatcher(
                BluetoothLEDevice.GetDeviceSelectorFromPairingState(false),
                requestedProperties,
                DeviceInformationKind.AssociationEndpoint);

            _deviceWatcher.Added += DeviceWatcher_Added;
            _deviceWatcher.Start();

            return Task.CompletedTask;
        }

        private void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation deviceInfo)
        {
            if (_connectedDeviceIds.Contains(deviceInfo.Id)) return;
            
            // Allow connection to both "TOZO NC20 Pro" and "TOZO NC20 Pro Box"
            if (deviceInfo.Name.StartsWith(_targetDeviceName, StringComparison.OrdinalIgnoreCase))
            {
                _connectedDeviceIds.Add(deviceInfo.Id);
                ReportStatus($"Found Target: {deviceInfo.Name}. Attempting BLE connection...");
                _ = ConnectToDeviceInternalAsync(deviceInfo.Id);
            }
        }

        private async Task ConnectToDeviceInternalAsync(string deviceId)
        {
            try
            {
                var device = await BluetoothLEDevice.FromIdAsync(deviceId);
                if (device == null)
                {
                    ReportStatus("Failed to get BluetoothLEDevice.");
                    return;
                }

                _devices.Add(device);
                ReportStatus($"BLE Connected to {device.Name}!");

                var servicesResult = await device.GetGattServicesAsync(BluetoothCacheMode.Cached);
                if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
                {
                    ReportStatus($"Failed to get GATT services: {servicesResult.Status}. Try unpairing earbuds from phone!");
                    return;
                }

                foreach (var service in servicesResult.Services)
                {
                    if (service.Uuid.ToString().StartsWith("000018", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var characteristicsResult = await service.GetCharacteristicsAsync(BluetoothCacheMode.Cached);
                    if (characteristicsResult.Status == GattCommunicationStatus.Success)
                    {
                        foreach (var characteristic in characteristicsResult.Characteristics)
                        {
                            if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify) ||
                                characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
                            {
                                var status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                                    GattClientCharacteristicConfigurationDescriptorValue.Notify);
                                
                                if (status == GattCommunicationStatus.Success)
                                {
                                    characteristic.ValueChanged += RxCharacteristic_ValueChanged;
                                    _rxCharacteristics.Add(characteristic);
                                }
                            }
                            
                            if (characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write) ||
                                characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse))
                            {
                                _txCharacteristics.Add(characteristic);
                                ReportStatus($"Found TX Char: {characteristic.Uuid.ToString().Substring(4,4)}");
                            }
                        }
                    }
                }

                if (_txCharacteristics.Count == 0 || _rxCharacteristics.Count == 0)
                {
                    ReportStatus("No custom TX/RX characteristics found.");
                    return;
                }

                ReportStatus($"Found {_txCharacteristics.Count} TX and {_rxCharacteristics.Count} RX characteristics.");
                
                Application.Current.Dispatcher.Invoke(() => { OnConnectionStateChanged?.Invoke(true); });
            }
            catch (Exception ex)
            {
                ReportStatus($"Connection error: {ex.Message}");
            }
        }

        private void RxCharacteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            var reader = Windows.Storage.Streams.DataReader.FromBuffer(args.CharacteristicValue);
            byte[] rawData = new byte[reader.UnconsumedBufferLength];
            reader.ReadBytes(rawData);
            
            var hex = BitConverter.ToString(rawData);
            ReportStatus($"RX [{sender.Uuid.ToString().Substring(4,4)}]: {hex}");

            Application.Current.Dispatcher.Invoke(() => { OnDataReceived?.Invoke(rawData); });
        }

        public async Task WriteDataAsync(byte[] data)
        {
            if (_txCharacteristics.Count == 0 || data == null || data.Length == 0) return;
            
            try 
            {
                var hex = BitConverter.ToString(data);
                ReportStatus("TX: " + hex);
            } catch { }

            var buffer = Windows.Security.Cryptography.CryptographicBuffer.CreateFromByteArray(data);
            
            foreach (var txChar in _txCharacteristics)
            {
                string uuid = txChar.Uuid.ToString().ToLower();

                try
                {
                    var writeOption = txChar.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse) ? 
                        GattWriteOption.WriteWithoutResponse : GattWriteOption.WriteWithResponse;
                        
                    await txChar.WriteValueAsync(buffer, writeOption);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to write to {txChar.Uuid}: {ex.Message}");
                }
            }
        }

        public void Disconnect()
        {
            try
            {
                foreach (var device in _devices)
                {
                    device?.Dispose();
                }
                _devices.Clear();
                _connectedDeviceIds.Clear();
                _txCharacteristics.Clear();
                _rxCharacteristics.Clear();
                Application.Current.Dispatcher.Invoke(() => { OnConnectionStateChanged?.Invoke(false); });
            }
            catch { }
        }

        private void ReportStatus(string message)
        {
            Application.Current.Dispatcher.Invoke(() => { OnStatusMessage?.Invoke(message); });
        }
    }
}
