#include "config_store.h"
#include "constants.h"
#include <Preferences.h>

ConfigStore::ConfigStore(Logger& logger) : _logger(logger) {}

std::optional<DeviceConfig> ConfigStore::load() const
{
  Preferences prefs;
  prefs.begin(StorageKey::Namespace, true);
  DeviceConfig config{
      prefs.getString(StorageKey::Ssid, ""),
      prefs.getString(StorageKey::Password, ""),
      prefs.getString(StorageKey::DashboardUrl, ""),
      prefs.getInt(StorageKey::DevicePort, 8129),
      prefs.getString(StorageKey::DashboardApiKey, ""),
      prefs.getString(StorageKey::PairingCode, "")};
  prefs.end();

  if (config.ssid.length() == 0 || config.dashboardUrl.length() == 0)
  {
    return std::nullopt;
  }
  return config;
}

void ConfigStore::save(const DeviceConfig& config) const
{
  Preferences prefs;
  prefs.begin(StorageKey::Namespace, false);
  prefs.putString(StorageKey::Ssid, config.ssid);
  prefs.putString(StorageKey::Password, config.password);
  prefs.putString(StorageKey::DashboardUrl, config.dashboardUrl);
  prefs.putInt(StorageKey::DevicePort, config.devicePort);
  prefs.putString(StorageKey::DashboardApiKey, config.dashboardApiKey);
  prefs.putString(StorageKey::PairingCode, config.pairingCode);
  prefs.end();
}

void ConfigStore::clear() const
{
  Preferences prefs;
  prefs.begin(StorageKey::Namespace, false);
  prefs.clear();
  prefs.end();
}

int ConfigStore::getOtaFailCount(const String& version) const
{
  Preferences prefs;
  prefs.begin(StorageKey::Namespace, true);
  String failedVersion = prefs.getString(StorageKey::OtaFailVersion, "");
  int count = 0;
  if (failedVersion == version)
  {
    count = prefs.getInt(StorageKey::OtaFailCount, 0);
  }
  prefs.end();
  return count;
}

void ConfigStore::recordOtaFailure(const String& version) const
{
  Preferences prefs;
  prefs.begin(StorageKey::Namespace, false);
  String failedVersion = prefs.getString(StorageKey::OtaFailVersion, "");
  int count = 0;
  if (failedVersion == version)
  {
    count = prefs.getInt(StorageKey::OtaFailCount, 0);
  }
  prefs.putString(StorageKey::OtaFailVersion, version);
  prefs.putInt(StorageKey::OtaFailCount, count + 1);
  prefs.end();
  _logger.printf("OTA: Recorded failure %d/%d for v%s\n", count + 1, Ota::MaxRetries, version.c_str());
}

void ConfigStore::clearOtaFailCount() const
{
  Preferences prefs;
  prefs.begin(StorageKey::Namespace, false);
  prefs.remove(StorageKey::OtaFailVersion);
  prefs.remove(StorageKey::OtaFailCount);
  prefs.end();
}
