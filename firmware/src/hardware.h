#pragma once

#include <Arduino.h>
#include "config_store.h"

class Hardware {
public:
  void startDeepSleep(uint64_t waitSeconds);
  bool isResetRequested();
  void resetDevice(ConfigStore& configStore);
};
