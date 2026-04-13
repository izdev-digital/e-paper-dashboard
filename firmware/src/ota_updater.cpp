#include "ota_updater.h"
#include "constants.h"
#include <Update.h>

OtaUpdater::OtaUpdater(Logger& logger, Network& network, ConfigStore& configStore)
    : _logger(logger), _network(network), _configStore(configStore) {}

bool OtaUpdater::isNewerVersion(const char* current, const String& available) const
{
  int curMajor = 0, curMinor = 0, curPatch = 0;
  int avMajor = 0, avMinor = 0, avPatch = 0;

  sscanf(current, "%d.%d.%d", &curMajor, &curMinor, &curPatch);
  sscanf(available.c_str(), "%d.%d.%d", &avMajor, &avMinor, &avPatch);

  if (avMajor != curMajor) return avMajor > curMajor;
  if (avMinor != curMinor) return avMinor > curMinor;
  return avPatch > curPatch;
}

bool OtaUpdater::shouldAttempt(const String& version) const
{
  return _configStore.getOtaFailCount(version) < Ota::MaxRetries;
}

bool OtaUpdater::perform(const DeviceConfig& config)
{
  _logger.println("Starting OTA firmware update...");

  _network.setTimeout(10000);

  if (!_network.sendGetRequest("/api/firmware/download", config))
  {
    _logger.println("Failed to connect for OTA update");
    return false;
  }

  auto headers = _network.readResponseHeaders();
  if (!headers.isSuccess)
  {
    _logger.println("OTA: Server returned an error");
    _network.close();
    return false;
  }

  if (headers.contentLength <= 0)
  {
    _logger.println("OTA: Invalid or missing Content-Length header");
    _network.close();
    return false;
  }

  _logger.printf("OTA: Firmware size: %ld bytes\n", headers.contentLength);

  if (!Update.begin(headers.contentLength))
  {
    _logger.println("OTA: Not enough space for update");
    _network.close();
    return false;
  }

  _logger.println("OTA: Writing firmware...");
  size_t written = Update.writeStream(_network.client());
  _logger.printf("OTA: Written %u bytes\n", written);

  if (Update.end())
  {
    if (Update.isFinished())
    {
      _logger.println("OTA: Update successful! Rebooting...");
      _configStore.clearOtaFailCount();
      _network.close();
      delay(1000);
      ESP.restart();
      return true;
    }
  }

  _logger.printf("OTA: Update failed: %s\n", Update.errorString());
  _network.close();
  return false;
}
