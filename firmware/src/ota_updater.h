#pragma once

#include "types.h"
#include "config_store.h"
#include "network.h"
#include "logger.h"

class OtaUpdater {
public:
  OtaUpdater(Logger& logger, Network& network, ConfigStore& configStore);

  bool isNewerVersion(const char* current, const String& available) const;
  bool shouldAttempt(const String& version) const;
  bool perform(const DeviceConfig& config);

private:
  Logger& _logger;
  Network& _network;
  ConfigStore& _configStore;
};
