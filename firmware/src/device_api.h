#pragma once

#include "types.h"
#include "network.h"
#include "logger.h"
#include "display_manager.h"

class DeviceApi {
public:
  DeviceApi(Logger& logger, Network& network);

  DeviceStatus fetchDeviceStatus(const DeviceConfig& config);
  void fetchAndDisplayImage(const DeviceConfig& config, DisplayManager& display);
  bool pairWithDashboard(const String& pairingCode, const String& dashboardUrl, int devicePort, String& confirmationPin);
  bool pollForApiKey(const String& pairingCode, const String& dashboardUrl, int devicePort, String& apiKey);

private:
  Logger& _logger;
  Network& _network;
};
