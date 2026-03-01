#include "device_api.h"
#include "constants.h"
#include <ArduinoJson.h>

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

bool DeviceApi::registerWithDashboard(const String& pairingCode, const String& dashboardUrl, int devicePort, bool useHttps, String& apiKey, String& errorOut)
{
  _logger.println("Starting device registration...");

  _network.setTimeout(10000);

  if (!_network.connectTo(dashboardUrl, devicePort, useHttps))
  {
    _logger.println("Failed to connect to server for registration");
    errorOut = "Could not connect to server";
    return false;
  }

  String macAddress = WiFi.macAddress();
  String deviceName = "izBoard-" + macAddress.substring(macAddress.length() - 8);

  JsonDocument requestDoc;
  requestDoc["code"] = pairingCode;
  requestDoc["deviceIdentifier"] = macAddress;
  requestDoc["deviceName"] = deviceName;
  requestDoc["screenWidth"] = DisplayConst::Width;
  requestDoc["screenHeight"] = DisplayConst::Height;

  String jsonBody;
  serializeJson(requestDoc, jsonBody);

  String postRequest = "POST /api/pairing/register HTTP/1.1\r\n";
  postRequest += "Host: " + dashboardUrl + ":" + String(devicePort) + "\r\n";
  postRequest += "Content-Type: application/json\r\n";
  postRequest += "Content-Length: " + String(jsonBody.length()) + "\r\n";
  postRequest += "Connection: close\r\n\r\n";
  postRequest += jsonBody;

  _network.send(postRequest);

  int httpStatus = 0;
  while (_network.connected() || _network.available())
  {
    String line = _network.readStringUntil('\n');
    _logger.println(line);

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
  String response = (jsonStart >= 0 && jsonEnd > jsonStart)
                        ? raw.substring(jsonStart, jsonEnd + 1)
                        : "";

  _logger.print("HTTP status: ");
  _logger.println(httpStatus);
  _logger.println("Response: " + response);

  if (httpStatus != 200)
  {
    raw.trim();
    if (response.length() > 0)
    {
      errorOut = response;
    }
    else if (raw.length() > 0)
    {
      errorOut = raw;
    }
    else
    {
      errorOut = "Server returned HTTP " + String(httpStatus);
    }
    return false;
  }

  JsonDocument responseDoc;
  DeserializationError error = deserializeJson(responseDoc, response);
  if (error)
  {
    _logger.print("JSON parse error: ");
    _logger.println(error.c_str());
    errorOut = "Invalid response from server";
    return false;
  }

  const char* key = responseDoc["apiKey"];
  if (!key)
  {
    _logger.println("API key not found in response");
    errorOut = "Server response missing API key";
    return false;
  }

  apiKey = key;
  _logger.println("Received API key: " + apiKey);

  return true;
}
