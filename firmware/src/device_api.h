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
  bool registerWithDashboard(const String& pairingCode, const String& registrationToken,
                             const String& dashboardUrl, int devicePort, bool useHttps,
                             const String& dashboardBasePath,
                             String& apiKey, String& errorOut);

private:
  Logger& _logger;
  Network& _network;
  bool postPairingRequest(const String& path, const String& body,
                          const String& dashboardUrl, int devicePort, bool useHttps,
                          const String& dashboardBasePath,
                          int& httpStatus, String& response);
};
