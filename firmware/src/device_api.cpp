#include "device_api.h"
#include "constants.h"

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

bool DeviceApi::registerWithDashboard(const String& pairingCode, const String& dashboardUrl, int devicePort, bool useHttps, String& apiKey)
{
  _logger.println("Starting device registration...");

  _network.setTimeout(10000);

  if (!_network.connectTo(dashboardUrl, devicePort, useHttps))
  {
    _logger.println("Failed to connect to server for registration");
    return false;
  }

  String macAddress = WiFi.macAddress();
  String deviceName = "izBoard-" + macAddress.substring(macAddress.length() - 8);

  String jsonBody = "{\"code\":\"" + pairingCode + "\",\"deviceIdentifier\":\"" + macAddress + "\",\"deviceName\":\"" + deviceName + "\",\"screenWidth\":" + String(DisplayConst::Width) + ",\"screenHeight\":" + String(DisplayConst::Height) + "}";

  String postRequest = "POST /api/pairing/register HTTP/1.1\r\n";
  postRequest += "Host: " + dashboardUrl + ":" + String(devicePort) + "\r\n";
  postRequest += "Content-Type: application/json\r\n";
  postRequest += "Content-Length: " + String(jsonBody.length()) + "\r\n";
  postRequest += "Connection: close\r\n\r\n";
  postRequest += jsonBody;

  _network.send(postRequest);

  bool statusOk = false;
  while (_network.connected() || _network.available())
  {
    String line = _network.readStringUntil('\n');
    _logger.println(line);

    if (!statusOk)
    {
      statusOk = line.startsWith("HTTP/1.1 200");
    }

    if (line == "\r")
    {
      break;
    }
  }

  if (!statusOk)
  {
    _logger.println("Registration request failed");
    _network.close();
    return false;
  }

  String response = "";
  while (_network.connected() || _network.available())
  {
    String line = _network.readStringUntil('\n');
    line.trim();
    if (line.startsWith("{"))
    {
      response = line;
      break;
    }
  }
  _network.close();

  _logger.println("Response: " + response);

  int apiKeyStart = response.indexOf("\"apiKey\":\"");
  if (apiKeyStart == -1)
  {
    _logger.println("API key not found in response");
    return false;
  }

  apiKeyStart += 10;
  int apiKeyEnd = response.indexOf("\"", apiKeyStart);
  if (apiKeyEnd == -1)
  {
    _logger.println("API key end not found");
    return false;
  }

  apiKey = response.substring(apiKeyStart, apiKeyEnd);
  _logger.println("Received API key: " + apiKey);

  return true;
}
