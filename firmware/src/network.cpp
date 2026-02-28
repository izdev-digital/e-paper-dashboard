#include "network.h"
#include "version.h"

Network::Network(Logger& logger) : _logger(logger) {}

WiFiClient& Network::activeClient()
{
  return _useSecure ? static_cast<WiFiClient&>(_secureClient) : _client;
}

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
  if (!connectTo(config.dashboardUrl, config.devicePort, config.useHttps))
  {
    _logger.println("Failed to connect to the remote server...");
    return false;
  }
  _logger.println("Successfully connected to the remote server!");
  _logger.println("Sending request...");

  auto& c = activeClient();
  c.println("GET " + url + " HTTP/1.1");
  c.print("X-Api-Key: ");
  c.println(config.dashboardApiKey);
  c.print("Host: ");
  c.print(config.dashboardUrl);
  c.print(":");
  c.println(config.devicePort);
  c.print("X-Device-Firmware-Version: ");
  c.println(FIRMWARE_VERSION);
  c.print("X-Device-Id: ");
  c.println(WiFi.macAddress());
  c.println("Connection: close");
  c.println();
  return true;
}

ResponseHeaders Network::readResponseHeaders()
{
  _logger.println("Reading headers...");
  auto& c = activeClient();
  ResponseHeaders headers;
  while (c.connected() || c.available())
  {
    String line = c.readStringUntil('\n');
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

bool Network::connectTo(const String& host, int port, bool useHttps)
{
  _useSecure = useHttps;
  if (_useSecure)
  {
    _secureClient.setInsecure();
    return _secureClient.connect(host.c_str(), port);
  }
  return _client.connect(host.c_str(), port);
}

void Network::send(const String& data)
{
  activeClient().print(data);
}

void Network::setTimeout(int timeoutMs)
{
  activeClient().setTimeout(timeoutMs);
}

void Network::close()
{
  activeClient().stop();
  _useSecure = false;
}

bool Network::connected()
{
  return activeClient().connected();
}

int Network::available()
{
  return activeClient().available();
}

size_t Network::read(uint8_t* buffer, size_t size)
{
  return activeClient().read(buffer, size);
}

String Network::readStringUntil(char terminator)
{
  return activeClient().readStringUntil(terminator);
}

String Network::readString()
{
  return activeClient().readString();
}

WiFiClient& Network::client()
{
  return activeClient();
}
