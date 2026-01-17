
# izPanel Firmware

## Overview

izPanel Firmware is designed for ESP32-based devices equipped with E-Paper displays. The firmware integrates with a remote izPanel server to provide scheduled dashboard updates and display management. Updates are performed at configurable intervals, allowing the device to enter deep sleep between updates to conserve energy. Features include WiFi connectivity, scheduled polling, power-saving modes, and support for display refresh cycles.

## Hardware

This firmware is currently intended for Waveshare ESP32 E-Paper boards and 7.5-inch E-Paper displays, but can be adapted to other compatible hardware with minor modifications:

- **ESP32 Board**: [Waveshare E-Paper ESP32 Driver Board](https://www.waveshare.com/e-Paper-ESP32-Driver-Board.htm)
- **E-Paper Display**: [Waveshare 7.5inch e-Paper HAT (B)](https://www.waveshare.com/wiki/7.5inch_e-Paper_HAT_(B)_Manual)

> **Note**: Supporting other E-Paper displays or ESP32 boards would require code adjustments, including pin configuration and display driver modifications.

## Features

- **Scheduled polling**: Retrieves dashboard images from the server based on configured schedules
- **Deep sleep mode**: Conserves power between dashboard updates
- **WiFi connectivity**: Connects to the server via WiFi
- **Display management**: Handles E-Paper refresh cycles and display updates

## Building and Flashing

This project uses PlatformIO for building and flashing the firmware.

### Prerequisites

- [PlatformIO](https://platformio.org/) (VS Code extension or CLI)
- ESP32 board connected via USB

### Initial Configuration

When powered up for the first time, the firmware will create a WiFi access point for initial setup. Connect to this access point and configure:

- **WiFi credentials**: SSID and password for your network
- **Server URL**: The URL of your running izPanel server
- **API Key**: Dashboard API key (found in the running backend server's dashboard management interface)

Once configured, the device will connect to your WiFi network and begin polling the server for dashboard updates.

### Device Controls

- **Manual refresh**: Press the reset button once to manually refresh the screen
- **Factory reset**: Press and hold the reset button for 10 seconds to reset the device and return to initial configuration mode

### Build and Upload

Using PlatformIO CLI:
```bash
pio run -t upload
```

Using VS Code PlatformIO extension:
1. Open the firmware folder in VS Code
2. Click "Upload" in the PlatformIO toolbar

## Dependencies

The firmware uses the following libraries (automatically installed by PlatformIO):

- **GxEPD2**: E-Paper display driver library

See `platformio.ini` for the complete list of dependencies.
