using System;
using System.Threading.Tasks;

namespace TozoWindowsApp.Ble
{
    public interface IBleService
    {
        event Action<string>? OnStatusMessage;
        event Action<bool>? OnConnectionStateChanged;
        event Action<byte[]>? OnDataReceived;

        bool IsConnected { get; }

        Task ConnectAsync(string targetDeviceName);
        Task WriteDataAsync(byte[] data);
        void Disconnect();
    }
}
