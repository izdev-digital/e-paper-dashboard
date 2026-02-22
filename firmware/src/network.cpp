#include "network.h"
#include "version.h"

Network::Network(Logger& logger) : _logger(logger) {}

bool Network::connectToWiFi(const String& ssid, const String& password)
{
  _logger.println("Found stored configuration!");
  _logger.println("Connecting to WiFi");
  constexpr int maxRetries = 20;
  int retries = 0;
  WiFi.begin(ssid.c_str(), password.c_str());
  while (WiFi.status() != WL_CONNECTED && retries < maxRetries)
  {
    ++retries;
    delay(500);
  }
  if (WiFi.status() != WL_CONNECTED)
  {
    return false;
  }
  _logger.println("WiFi connected");
  _logger.print("IP address: ");
  _logger.println(WiFi.localIP());
  return true;
}

bool Network::sendGetRequest(const String& url, const DeviceConfig& config)
{
  if (!_client.connect(config.dashboardUrl.c_str(), config.devicePort))
  {
    _logger.println("Failed to connect to the remote server...");
    return false;
  }
  _logger.println("Successfully connected to the remote server!");
  _logger.println("Sending request...");

  _client.println("GET " + url + " HTTP/1.1");
  _client.print("X-Api-Key: ");
  _client.println(config.dashboardApiKey);
  _client.print("Host: ");
  _client.print(config.dashboardUrl);
  _client.print(":");
  _client.println(config.devicePort);
  _client.print("X-Device-Firmware-Version: ");
  _client.println(FIRMWARE_VERSION);
  _client.print("X-Device-Id: ");
  _client.println(WiFi.macAddress());
  _client.println("Connection: close");
  _client.println();
  return true;
}

ResponseHeaders Network::readResponseHeaders()
{
  _logger.println("Reading headers...");
  ResponseHeaders headers;
  while (_client.connected() || _client.available())
  {
    String line = _client.readStringUntil('\n');
    _logger.println(line);

    if (!headers.isSuccess)
    {
      headers.isSuccess = line.startsWith("HTTP/1.1 200");
    }

    if (line.startsWith("X-Firmware-Version:"))
    {
      headers.firmwareVersion = line.substring(strlen("X-Firmware-Version:"));
      headers.firmwareVersion.trim();
    }

    if (line.startsWith("Content-Length:"))
    {
      headers.contentLength = line.substring(strlen("Content-Length:")).toInt();
    }

    if (line == "\r")
    {
      break;
    }
  }
  return headers;
}

bool Network::connectTo(const String& host, int port)
{
  return _client.connect(host.c_str(), port);
}

void Network::send(const String& data)
{
  _client.print(data);
}

void Network::setTimeout(int timeoutMs)
{
  _client.setTimeout(timeoutMs);
}

void Network::close()
{
  _client.stop();
}

bool Network::connected()
{
  return _client.connected();
}

int Network::available()
{
  return _client.available();
}

size_t Network::read(uint8_t* buffer, size_t size)
{
  return _client.read(buffer, size);
}

String Network::readStringUntil(char terminator)
{
  return _client.readStringUntil(terminator);
}

String Network::readString()
{
  return _client.readString();
}

WiFiClient& Network::client()
{
  return _client;
}
