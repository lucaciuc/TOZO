# TOZO NC20 Pro - Open Source Windows Manager

![TOZO Manager](https://img.shields.io/badge/Status-Active-brightgreen.svg)
![WPF](https://img.shields.io/badge/UI-WPF%20%7C%20MaterialDesign-deepblue.svg)

An open-source, fully-featured Windows 10/11 manager for TOZO earbuds, built from scratch by reverse-engineering the official Android app's Bluetooth Low Energy (BLE) protocol.

## The Reverse-Engineering Journey

This project started with a simple question: *Why is there no Windows app to control TOZO earbuds?* 

We decided to build one ourselves. Here is an exhaustive deep dive into how we bypassed the lack of public documentation and achieved full control over the earbuds.

### 1. Decompiling the Android APK
The first step was extracting the official TOZO Android app (`TOZO-Tech_Around_You_5_1_3_APKPure.xapk`). We unpacked the `xapk` and orchestrated a batch decompilation workflow using `apktool` and `jadx`. Scanning through the obfuscated Java source code, we found references to Bluetooth GATT characteristics, confirming that the app communicates with the earbuds entirely via BLE (Bluetooth Low Energy) rather than classic Bluetooth SPP.

### 2. Sniffing the BLE Protocol
To figure out the exact payloads being sent to the earbuds, we enabled "Bluetooth HCI snoop log" in Android Developer Options. By capturing `btsnoop_hci.log` files while clicking buttons in the official app, we extracted the hex streams.

We discovered the initialization handshake:
- `00-06-00-00`
- `01-01-00-00`

And the exact commands for Active Noise Cancellation (ANC):
- **Normal Mode:** `10 04 01 00 00`
- **Noise Cancel:** `10 04 01 01 01`
- **Transparency:** `10 05 01 01 01`
- **Wind Noise:** `10 07 01 01 01`
- **Leisure Mode:** `10 08 01 01 01`
- **Adaptive:** `10 11 01 01 01`

### 3. The Dual-Chip Architecture Dilemma
When we began building the Windows backend using `Windows.Devices.Bluetooth`, we hit a massive roadblock. 
Scanning for the device yielded **two** separate BLE MAC addresses: `TOZO NC20 Pro` (the earbuds) and `TOZO NC20 Pro Box` (the charging case). 

When querying the GATT services of the earbuds, we found characteristics belonging to two entirely different chipsets crammed inside:
- **Airoha (a101):** Handles audio routing and ANC.
- **JuXin (b611):** Handles the charging case status and bridge logic.

### 4. The Windows BLE Stack Bug & The "Master Bridge" Breakthrough
We initially tried to send ANC commands exclusively to the Airoha chip (`a101`) and battery requests to the JuXin chip (`b611`). However, Windows' Bluetooth stack is notoriously flaky, and we kept hitting random `Unreachable` GATT timeout exceptions when trying to talk to the Airoha chip. The UI would freeze, and commands would drop.

**Then we discovered an incredible engineering secret:**
The JuXin chip (`b611`) acts as a "master bridge"! If you send an Airoha command to the JuXin chip, it seamlessly intercepts it, routes it to the Airoha chip internally, and relays the response back to us over the air.

We removed all strict UUID filtering. By blindly broadcasting our payloads to all available RX characteristics, the JuXin chip successfully caught our battery requests and ANC commands, completely bypassing the Windows Bluetooth stack timeouts and guaranteeing 100% reliability.

### 5. Finalizing the Experience
With the reverse-engineering complete and the backend stabilized, we wrapped everything in a stunning, hardware-accelerated WPF UI utilizing `MaterialDesignThemes`. The app now automatically syncs your ANC preferences by caching them locally (`SettingsManager`), actively parses battery levels (`L=55% R=37% C=58%`), and handles dynamic UI updates flawlessly.

## Features
- **Real-time Battery Monitoring:** Live updates for Left Earbud, Right Earbud, and the Charging Case.
- **ANC Control:** Seamlessly switch between Noise Cancellation, Transparency, Normal, Wind Noise, Leisure, and Adaptive modes.
- **Auto-Sync:** Caches your ANC preferences and automatically pushes them to the earbuds upon connection, replicating the official app's behavior.
- **Modern UI:** A beautiful, responsive Dark Mode interface.

## Download & Installation
Go to the [Releases](../../releases) tab to download the latest `.exe`.

## 🛠 Building from Source
This project requires the .NET 8.0 SDK.
```cmd
git clone https://github.com/lucaciuc/TOZO.git
cd TOZO
dotnet build -c Release
```
