#include "device_api.h"
#include "constants.h"
#include <ArduinoJson.h>

namespace {
String pairingErrorMessage(const String& response, const String& fallback)
{
  JsonDocument errorDoc;
  if (!deserializeJson(errorDoc, response))
  {
    const char* detail = errorDoc["detail"];
    if (detail && strlen(detail) > 0) return String(detail);
  }
  return response.length() > 0 && response.length() <= 120 ? response : fallback;
}
}

DeviceApi::DeviceApi(Logger& logger, Network& network) : _logger(logger), _network(network) {}

DeviceStatus DeviceApi::fetchDeviceStatus(const DeviceConfig& config)
{
  _logger.println("Fetching device configuration...");

  DeviceStatus result;

  if (!_network.sendGetRequest("/api/configuration/next-update-wait-seconds", config))
  {
    _logger.println("Failed to connect to fetch device config");
    return result;
  }

  auto headers = _network.readResponseHeaders();
  if (!headers.isSuccess)
  {
    _logger.println("Device config request was not successful");
    _network.close();
    return result;
  }

  result.latestFirmwareVersion = headers.firmwareVersion;

  String delayString{};
  if (_network.connected() || _network.available())
  {
    delayString = _network.readStringUntil('\n');
    _logger.print("Next update wait: ");
    _logger.println(delayString);
  }

  _network.close();

  if (delayString.length() > 0)
  {
    result.waitSeconds = strtoull(delayString.c_str(), nullptr, 10);
  }

  if (result.latestFirmwareVersion.length() > 0)
  {
    _logger.print("Latest firmware version from server: ");
    _logger.println(result.latestFirmwareVersion);
  }

  return result;
}

void DeviceApi::fetchAndDisplayImage(const DeviceConfig& config, DisplayManager& display)
{
  _logger.println("Connecting to the remote server...");

  _network.setTimeout(5000);
  if (!_network.sendGetRequest("/api/render/binary", config))
  {
    _logger.println("Failed to connect to the remote server...");
    return;
  }

  auto headers = _network.readResponseHeaders();
  if (!headers.isSuccess)
  {
    _logger.println("The request was not successful...");
    _network.close();
    return;
  }

  _logger.println("Reading image content...");

  display.beginPartialWindow();

  int16_t y = 0;

  while ((_network.connected() || _network.available()) && y < DisplayConst::Height)
  {
    const size_t bytesNeeded = static_cast<size_t>(DisplayConst::FrameBytes) * 2;
    size_t idx = 0;

    display.clearBuffers();

    while (idx < bytesNeeded && (_network.connected() || _network.available()))
    {
      size_t avail = _network.available();
      if (avail > 0)
      {
        size_t toRead = min(avail, bytesNeeded - idx);
        uint8_t buffer[1024];
        size_t chunkSize = min(toRead, sizeof(buffer));
        size_t bytesRead = _network.read(buffer, chunkSize);

        for (size_t i = 0; i < bytesRead; i++)
        {
          display.writePixelByte(idx, buffer[i]);
          ++idx;
        }
      }
      else
      {
        if (!_network.connected())
        {
          break;
        }
        yield();
      }
    }

    if (idx < bytesNeeded)
    {
      _logger.println("Incomplete frame data received, stopping.");
      break;
    }

    display.writeFrame(0, y, DisplayConst::FrameWidth, DisplayConst::FrameHeight);
    y += DisplayConst::FrameHeight;
  }
  _logger.println();

  _network.close();
}

bool DeviceApi::postPairingRequest(const String& path, const String& body,
                                   const String& dashboardUrl, int devicePort, bool useHttps,
                                   const String& dashboardBasePath,
                                   int& httpStatus, String& response)
{
  if (!_network.connectTo(dashboardUrl, devicePort, useHttps))
  {
    return false;
  }
  _network.setTimeout(10000);

  String postRequest = "POST " + dashboardBasePath + path + " HTTP/1.1\r\n";
  postRequest += "Host: " + dashboardUrl + ":" + String(devicePort) + "\r\n";
  postRequest += "Content-Type: application/json\r\n";
  postRequest += "Content-Length: " + String(body.length()) + "\r\n";
  postRequest += "Connection: close\r\n\r\n";
  postRequest += body;

  _network.send(postRequest);

  httpStatus = 0;
  while (_network.connected() || _network.available())
  {
    String line = _network.readStringUntil('\n');
    if (httpStatus == 0 && line.startsWith("HTTP/"))
    {
      int spaceIdx = line.indexOf(' ');
      if (spaceIdx > 0)
      {
        httpStatus = line.substring(spaceIdx + 1).toInt();
      }
    }

    if (line == "\r")
    {
      break;
    }
  }

  String raw = "";
  while (_network.connected() || _network.available())
  {
    if (_network.available())
    {
      raw += (char)_network.client().read();
    }
    else
    {
      delay(5);
    }
  }

  _network.close();

  int jsonStart = raw.indexOf('{');
  int jsonEnd = raw.lastIndexOf('}');
  response = (jsonStart >= 0 && jsonEnd > jsonStart)
                 ? raw.substring(jsonStart, jsonEnd + 1)
                 : raw;
  response.trim();

  _logger.print("HTTP status: ");
  _logger.println(httpStatus);
  return true;
}

bool DeviceApi::registerWithDashboard(const String& pairingCode, const String& registrationToken,
                                      const String& dashboardUrl, int devicePort, bool useHttps,
                                      const String& dashboardBasePath,
                                      String& apiKey, String& errorOut)
{
  _logger.println("Announcing device to dashboard...");

  String macAddress = WiFi.macAddress();
  String deviceName = "izBoard-" + macAddress.substring(macAddress.length() - 8);

  JsonDocument announceDoc;
  announceDoc["code"] = pairingCode;
  announceDoc["registrationToken"] = registrationToken;
  announceDoc["deviceIdentifier"] = macAddress;
  announceDoc["deviceName"] = deviceName;
  announceDoc["screenWidth"] = DisplayConst::Width;
  announceDoc["screenHeight"] = DisplayConst::Height;

  String announceBody;
  serializeJson(announceDoc, announceBody);

  int httpStatus = 0;
  String response;
  if (!postPairingRequest("/api/pairing/announce", announceBody,
                          dashboardUrl, devicePort, useHttps, dashboardBasePath, httpStatus, response))
  {
    errorOut = "Could not connect to server";
    return false;
  }

  if (httpStatus != 202)
  {
    errorOut = pairingErrorMessage(response, "Server rejected device announcement");
    return false;
  }

  _logger.println("Device announced. Waiting for claim in the dashboard...");
  constexpr unsigned long claimTimeoutMs = 10UL * 60UL * 1000UL;
  const unsigned long startedAt = millis();

  JsonDocument statusRequestDoc;
  statusRequestDoc["code"] = pairingCode;
  statusRequestDoc["registrationToken"] = registrationToken;
  String statusBody;
  serializeJson(statusRequestDoc, statusBody);

  while (millis() - startedAt < claimTimeoutMs)
  {
    delay(2000);
    response = "";
    httpStatus = 0;
    if (!postPairingRequest("/api/pairing/device-status", statusBody,
                            dashboardUrl, devicePort, useHttps, dashboardBasePath, httpStatus, response))
    {
      _logger.println("Pairing status temporarily unavailable; retrying...");
      continue;
    }

    if (httpStatus == 410)
    {
      errorOut = "Claim code expired";
      return false;
    }
    if (httpStatus != 200)
    {
      errorOut = pairingErrorMessage(response, "Server rejected pairing status request");
      return false;
    }

    JsonDocument responseDoc;
    if (deserializeJson(responseDoc, response))
    {
      _logger.println("Invalid pairing status response; retrying...");
      continue;
    }

    const char* status = responseDoc["status"] | "pending";
    if (strcmp(status, "completed") == 0)
    {
      const char* key = responseDoc["apiKey"];
      if (!key || strlen(key) == 0)
      {
        errorOut = "Server response missing API key";
        return false;
      }

      apiKey = key;
      _logger.println("Device claim completed");
      return true;
    }
  }

  errorOut = "Claim code expired before it was entered";
  return false;
}
