#pragma once

#include <Arduino.h>

struct DeviceConfig {
  String ssid;
  String password;
  String dashboardUrl;
  int devicePort;
  String dashboardApiKey;
  String pairingCode;
  bool useHttps;
};

struct ResponseHeaders {
  bool isSuccess = false;
  String firmwareVersion;
  long contentLength = -1;
};

struct DeviceStatus {
  uint64_t waitSeconds = 0;
  String latestFirmwareVersion;
};
