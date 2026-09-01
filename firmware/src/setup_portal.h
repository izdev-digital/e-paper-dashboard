#pragma once

#include "config_store.h"
#include "display_manager.h"
#include "network.h"
#include "device_api.h"
#include "logger.h"

class SetupPortal {
public:
  SetupPortal(Logger& logger, ConfigStore& configStore, DisplayManager& display,
              Network& network, DeviceApi& deviceApi);

  DeviceConfig run();

private:
  Logger& _logger;
  ConfigStore& _configStore;
  DisplayManager& _display;
  Network& _network;
  DeviceApi& _deviceApi;
};
