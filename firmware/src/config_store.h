#pragma once

#include <optional>
#include "types.h"
#include "logger.h"

class ConfigStore {
public:
  explicit ConfigStore(Logger& logger);

  std::optional<DeviceConfig> load() const;
  void save(const DeviceConfig& config) const;
  void clear() const;

  int getOtaFailCount(const String& version) const;
  void recordOtaFailure(const String& version) const;
  void clearOtaFailCount() const;

private:
  Logger& _logger;
};
