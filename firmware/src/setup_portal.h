#pragma once

#include "config_store.h"
#include "display_manager.h"
#include "logger.h"

class SetupPortal {
public:
  SetupPortal(Logger& logger, ConfigStore& configStore, DisplayManager& display);

  [[noreturn]] void run();

private:
  Logger& _logger;
  ConfigStore& _configStore;
  DisplayManager& _display;
};
