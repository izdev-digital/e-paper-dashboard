# E-Paper Dashboard

A comprehensive solution for displaying Home Assistant dashboards (and other dashboards in the future) on E-Paper displays. The system renders dashboards as images suitable for E-Paper screens. Dashboard design and content can be changed dynamically without re-flashing the firmware.

## Overview

This project enables E-Paper displays to show dynamic dashboard content by polling a server that renders web-based dashboards into images for E-Paper displays. The rendering pipeline processes the dashboard content with color quantization and dithering techniques specifically designed for E-Paper display characteristics.

## Architecture

The project consists of three main components:

### 1. [Firmware](firmware/)
ESP32-based firmware that manages the E-Paper display hardware and periodically polls the software server for updated dashboard images. The firmware handles display refresh cycles and deep sleep modes for power efficiency.

### 2. [Software](software/)
ASP.NET Core web application that:
- Renders Home Assistant dashboards (or custom URLs) using headless browser technology
- Processes rendered content with E-Paper-specific image optimization (color quantization, dithering)
- Provides RESTful API endpoints for firmware to retrieve processed images
- Manages scheduling and configuration for multiple dashboards and devices
- Supports user authentication and dashboard management

### 3. [Packaging](packaging/)
Hardware enclosure designs and assembly instructions for creating physical dashboard devices.

## Key Features

- **Schedule-based polling**: Devices poll the server based on configurable schedules defined in the software
- **E-Paper optimized rendering**: Images are processed with color palette reduction and dithering algorithms tailored for E-Paper displays
- **No firmware updates required**: Dashboard content and layout changes are handled server-side
- **Home Assistant integration**: Designed to work with Home Assistant dashboards
  > **Note**: For best results, use kiosk mode for your dashboards and custom themes optimized for E-Paper displays based on color palette constraints.

## Quick Start

1. **Deploy the software server** using Docker:

2. **Flash the firmware** to your ESP32 device with E-Paper display

3. **Configure** your dashboard URLs and schedules through the web interface

See individual component READMEs for detailed setup instructions.

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.