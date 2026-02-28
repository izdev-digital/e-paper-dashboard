# izBoard Firmware

## Overview

ESP32 firmware for the izBoard E-Paper dashboard device. Connects to an izBoard server, fetches rendered dashboard images on a configurable schedule, displays them on a 7.5" E-Paper screen, and deep-sleeps between updates to maximize battery life.

## Hardware

- **ESP32 Board**: [Waveshare E-Paper ESP32 Driver Board](https://www.waveshare.com/e-Paper-ESP32-Driver-Board.htm)
- **E-Paper Display**: [Waveshare 7.5inch e-Paper HAT (B)](https://www.waveshare.com/wiki/7.5inch_e-Paper_HAT_(B)_Manual) — 800×480, black/white/red

> **Note**: Other E-Paper displays or ESP32 boards can be supported with pin configuration and display driver adjustments.

## Features

- Scheduled dashboard image updates from the server
- Deep sleep between updates for long battery life
- Secure device pairing via captive portal
- OTA firmware updates
- Manual refresh and factory reset via reset button

## Setup

### First Boot

1. Power on the ESP32 board
2. The device creates a WiFi access point named `izBoard-XXXX`
3. Connect to the access point and use the captive portal to configure WiFi and pair with the server
4. Once paired, the device begins fetching dashboard images automatically

### Device Controls

| Action | How |
|---|---|
| Manual refresh | Press the reset button once |
| Factory reset | Hold the reset button for 10 seconds |

## Building and Flashing

This project uses [PlatformIO](https://platformio.org/) for building and flashing.

### Prerequisites

- PlatformIO (VS Code extension or CLI)
- ESP32 board connected via USB

### Build and Upload

Using PlatformIO CLI:

```bash
pio run -t upload
```

Using VS Code:

1. Open the `firmware` folder in VS Code
2. Click **Upload** in the PlatformIO toolbar

## Dependencies

Managed automatically by PlatformIO — see `platformio.ini` for the full list.

- **GxEPD2** — E-Paper display driver
- **QRCode** — QR code generation for the setup portal
