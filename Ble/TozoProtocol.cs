using System;
using System.Linq;

namespace TozoWindowsApp.Ble
{
    public static class TozoProtocol
    {
        public enum AncMode : byte
        {
            AncOff = 0,
            AncOn = 1,
            Transparency = 2,
            ReduceWindNoise = 3,
            Leisure = 4,
            Adaptive = 6
        }

        public class BatteryInfo
        {
            public int Left { get; set; }
            public int Right { get; set; }
            public int Case { get; set; }
        }

        // --- Commands to Send ---
        
        public static byte[] GetAirohaInit() => new byte[] { 0x00, 0x06, 0x00, 0x00 };
        public static byte[] GetJuXinInit() => new byte[] { 0x01, 0x01, 0x00, 0x00 };
        
        public static byte[] GetAirohaBatteryRequest() => new byte[] { 0x00, 0x02, 0x00, 0x00 };
        public static byte[] GetJuXinBatteryRequest() => new byte[] { 0x01, 0x02, 0x00, 0x00 };

        public static byte[] GetAirohaRememberSettings(bool remember) => new byte[] { 0x10, 0x22, 0x01, (byte)(remember ? 0x01 : 0x00), (byte)(remember ? 0x01 : 0x00) };
        public static byte[] GetJuXinRememberSettings(bool remember) => new byte[] { 0x11, 0x0B, 0x01, 0x01, (byte)(remember ? 0x01 : 0x00) };

        public static byte[] GetAirohaAncCommand(AncMode mode)
        {
            switch (mode)
            {
                case AncMode.AncOn: return new byte[] { 0x10, 0x04, 0x01, 0x01, 0x01 };
                case AncMode.Transparency: return new byte[] { 0x10, 0x05, 0x01, 0x01, 0x01 };
                case AncMode.ReduceWindNoise: return new byte[] { 0x10, 0x07, 0x01, 0x01, 0x01 };
                case AncMode.Leisure: return new byte[] { 0x10, 0x08, 0x01, 0x01, 0x01 };
                case AncMode.Adaptive: return new byte[] { 0x10, 0x11, 0x01, 0x01, 0x01 };
                case AncMode.AncOff: return new byte[] { 0x10, 0x04, 0x01, 0x00, 0x00 };
                default: return new byte[] { 0x10, 0x04, 0x01, 0x00, 0x00 };
            }
        }

        public static byte[] GetJuXinAncCommand(AncMode mode)
        {
            // The Box just seems to want a ping when ANC changes
            return new byte[] { 0x11, 0x0B, 0x01, 0x01, 0x01 };
        }

        // --- Packet Parsing ---

        public static BatteryInfo? ParseBatteryPacket(byte[] payload)
        {
            if (payload == null || payload.Length < 2) return null;

            // JuXin Battery
            if (payload[0] == 0x01 && payload[1] == 0x02)
            {
                if (payload.Length == 5 && payload[3] != 0xFF && payload[4] != 0xFF)
                {
                    // 5-byte packet is the Box. Left/Right are unknown.
                    return new BatteryInfo
                    {
                        Left = -1,
                        Right = -1,
                        Case = (int)payload[3]
                    };
                }
                else if (payload.Length >= 6 && payload[3] != 0xFF && payload[4] != 0xFF)
                {
                    // 6-byte packet is from earbuds.
                    return new BatteryInfo
                    {
                        Left = (int)payload[3],
                        Right = (int)payload[4],
                        Case = (int)payload[5]
                    };
                }
            }
            // Airoha Battery (00-02 response, or AA-01/CC-01 push)
            else if ((payload[0] == 0x00 && payload[1] == 0x02) || 
                     ((payload[0] == 0xAA || payload[0] == 0xCC) && payload[1] == 0x01 && payload[2] == 0x02))
            {
                if (payload.Length >= 6)
                {
                    return new BatteryInfo
                    {
                        Left = (int)(payload[3] & 0x7F),
                        Right = (int)(payload[4] & 0x7F),
                        Case = -1 // The 6th byte is a sequence number/checksum, not the case battery!
                    };
                }
            }

            return null;
        }
        private static int ParseBcd(byte b)
        {
            try
            {
                return int.Parse(b.ToString("X2"));
            }
            catch
            {
                return (int)b;
            }
        }
}
}