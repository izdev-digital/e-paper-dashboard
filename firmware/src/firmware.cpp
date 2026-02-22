#include <Arduino.h>
#include "version.h"
#include "constants.h"
#include "logger.h"
#include "config_store.h"
#include "display_manager.h"
#include "network.h"
#include "ota_updater.h"
#include "device_api.h"
#include "setup_portal.h"
#include "hardware.h"

static Logger logger;
static ConfigStore configStore(logger);
static DisplayManager displayManager(logger);
static Network network(logger);
static OtaUpdater otaUpdater(logger, network, configStore);
static DeviceApi deviceApi(logger, network);
static Hardware hardware;

void setup()
{
  logger.begin(115200);

  if (!displayManager.init())
  {
    logger.println("Failed to initialize display!");
    ESP.restart();
  }

  logger.print("izBoard Firmware v");
  logger.println(FIRMWARE_VERSION);

  if (hardware.isResetRequested())
  {
    logger.println("Resetting device");
    hardware.resetDevice(configStore);
  }

  auto configuration = configStore.load();
  if (!configuration.has_value())
  {
    SetupPortal portal(logger, configStore, displayManager);
    portal.run();
    return;
  }

  DeviceConfig config = configuration.value();
  uint64_t waitSeconds = Timing::FallbackRefreshSeconds;

  if (!network.connectToWiFi(config.ssid, config.password))
  {
    logger.println("WiFi connection failed, retrying after sleep");
    hardware.startDeepSleep(waitSeconds);
    return;
  }

  if (config.dashboardApiKey.length() == 0 && config.pairingCode.length() > 0)
  {
    logger.println("API key not set, attempting pairing...");
    String apiKey;
    if (deviceApi.pairWithDashboard(config.pairingCode, config.dashboardUrl, config.devicePort, apiKey))
    {
      config.dashboardApiKey = apiKey;
      config.pairingCode = "";
      configStore.save(config);
      logger.println("Pairing successful!");
    }
    else
    {
      logger.println("Pairing failed, clearing configuration and restarting setup portal");
      hardware.resetDevice(configStore);
      return;
    }
  }

  auto status = deviceApi.fetchDeviceStatus(config);
  if (status.waitSeconds > 0)
  {
    waitSeconds = status.waitSeconds;
  }

  if (status.latestFirmwareVersion.length() > 0 &&
      otaUpdater.isNewerVersion(FIRMWARE_VERSION, status.latestFirmwareVersion))
  {
    int failCount = configStore.getOtaFailCount(status.latestFirmwareVersion);
    if (failCount >= Ota::MaxRetries)
    {
      logger.printf("OTA: Skipping v%s — failed %d times already\n",
                    status.latestFirmwareVersion.c_str(), failCount);
    }
    else
    {
      logger.print("New firmware v");
      logger.print(status.latestFirmwareVersion);
      logger.printf(" available (attempt %d/%d). Starting OTA update...\n", failCount + 1, Ota::MaxRetries);
      if (otaUpdater.perform(config))
      {
        configStore.clearOtaFailCount();
        return;
      }
      logger.println("OTA update failed, continuing with current firmware");
      configStore.recordOtaFailure(status.latestFirmwareVersion);
    }
  }

  deviceApi.fetchAndDisplayImage(config, displayManager);

  displayManager.refresh();
  displayManager.powerOff();
  hardware.startDeepSleep(waitSeconds);
}

void loop()
{
  ESP.restart();
}