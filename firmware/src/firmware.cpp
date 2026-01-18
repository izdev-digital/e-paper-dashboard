#include <Arduino.h>
#include <Preferences.h>
#include <optional>
#include <WiFi.h>
#include <WebServer.h>
#include <DNSServer.h>
#include <driver/rtc_io.h>
#include "version.h"

#define ENABLE_GxEPD2_GFX 1

#include <GxEPD2_3C.h>
#include <qrcode.h>
#include <Fonts/FreeSansBold18pt7b.h>
#include <Fonts/FreeSans12pt7b.h>

#define GxEPD2_DISPLAY_CLASS GxEPD2_3C
#define GxEPD2_DRIVER_CLASS GxEPD2_750c_Z08 // GDEW075Z08  800x480, EK79655 (GD7965), (WFT0583CZ61)

#define GxEPD2_3C_IS_GxEPD2_3C true
#define IS_GxEPD(c, x) (c##x)
#define IS_GxEPD2_BW(x) IS_GxEPD(GxEPD2_BW_IS_, x)
#define IS_GxEPD2_3C(x) IS_GxEPD(GxEPD2_3C_IS_, x)
#define IS_GxEPD2_7C(x) IS_GxEPD(GxEPD2_7C_IS_, x)

#define MAX_DISPLAY_BUFFER_SIZE 65536ul
#if IS_GxEPD2_BW(GxEPD2_DISPLAY_CLASS)
#define MAX_HEIGHT(EPD) (EPD::HEIGHT <= MAX_DISPLAY_BUFFER_SIZE / (EPD::WIDTH / 8) ? EPD::HEIGHT : MAX_DISPLAY_BUFFER_SIZE / (EPD::WIDTH / 8))
#elif IS_GxEPD2_3C(GxEPD2_DISPLAY_CLASS)
#define MAX_HEIGHT(EPD) (EPD::HEIGHT <= (MAX_DISPLAY_BUFFER_SIZE / 2) / (EPD::WIDTH / 8) ? EPD::HEIGHT : (MAX_DISPLAY_BUFFER_SIZE / 2) / (EPD::WIDTH / 8))
#elif IS_GxEPD2_7C(GxEPD2_DISPLAY_CLASS)
#define MAX_HEIGHT(EPD) (EPD::HEIGHT <= (MAX_DISPLAY_BUFFER_SIZE) / (EPD::WIDTH / 2) ? EPD::HEIGHT : (MAX_DISPLAY_BUFFER_SIZE) / (EPD::WIDTH / 2))
#endif

GxEPD2_DISPLAY_CLASS<GxEPD2_DRIVER_CLASS, MAX_HEIGHT(GxEPD2_DRIVER_CLASS)> display(GxEPD2_DRIVER_CLASS(/*CS=*/15, /*DC=*/27, /*RST=*/26, /*BUSY=*/25));

#define SEC_TO_USEC_FACTOR 1000000
#define RESET_WAKEUP_PIN GPIO_NUM_33
#define RESET_REQUEST_TIMEOUT 10
#define LED_PIN 2

static const char *CONFIGURATION_NAMESPACE = "config";
static const char *CONFIGURATION_SSID = "ssid";
static const char *CONFIGURATION_PASSWORD = "pwd";
static const char *CONFIGURATION_DASHBOARD_URL = "url";
static const char *CONFIGURATION_DASHBOARD_PORT = "port";
static const char *CONFIGURATION_DASHBOARD_RATE = "rate";
static const char *CONFIGURATION_DASHBOARD_API_KEY = "apikey";

static const uint16_t displayWidth = 800;
static const uint16_t displayHeight = 480;
static const uint16_t frameWidth = displayWidth;
static const uint16_t frameHeight = 160;
static const uint16_t frameBytes = frameWidth * frameHeight / 8;
static uint8_t *epd_bitmap_BW = nullptr;
static uint8_t *epd_bitmap_RW = nullptr;

SPIClass hspi(HSPI);

struct Configuration
{
  String ssid;
  String password;
  String dashboardUrl;
  int dashboardPort;
  uint64_t dashboardRate;
  String dashboardApiKey;
};

void startDeepSleep(const Configuration &config);
std::optional<Configuration> getConfiguration();
void storeConfiguration(const Configuration &config);
void clearConfiguration();
void createConfiguration();
void showWelcomePage(const IPAddress &ip, const String &mac);
bool isResetRequested();
void resetDevice();

bool connectToWiFi(const Configuration &config);
bool hasSuccessfulStatusCode(WiFiClient &client);
void fetchBinaryData(const Configuration &config);
std::optional<uint64_t> fetchNextWaitSeconds(const Configuration &config);
bool trySendGetRequest(WiFiClient &client, const String &url, const Configuration &config);

void setup()
{
  Serial.begin(115200);
  hspi.begin(13, 12, 14, 15); // remap hspi for EPD (swap pins)
  display.epd2.selectSPI(hspi, SPISettings(20000000, MSBFIRST, SPI_MODE0));
  display.init(115200);

  Serial.print("izBoard Firmware v");
  Serial.println(FIRMWARE_VERSION);

  epd_bitmap_BW = (uint8_t *)malloc(frameBytes);
  epd_bitmap_RW = (uint8_t *)malloc(frameBytes);
  
  if (!epd_bitmap_BW || !epd_bitmap_RW)
  {
    Serial.println("Failed to allocate frame buffers!");
    ESP.restart();
  }

  if (isResetRequested())
  {
    Serial.println("Resetting device");
    resetDevice();
  }

  const auto configuration = getConfiguration();
  if (!configuration.has_value())
  {
    createConfiguration();
    return;
  }

  if (connectToWiFi(configuration.value()))
  {
    fetchBinaryData(configuration.value());
  }

  display.refresh();
  display.powerOff();

  startDeepSleep(configuration.value());
}

void loop()
{
  // This function should not be reached. Restarting in case this happened.
  ESP.restart();
}

void fetchBinaryData(const Configuration &config)
{
  Serial.println("Connecting to the remote server...");

  WiFiClient client;
  client.setTimeout(5000);
  if (!trySendGetRequest(client, "/api/render/binary?width=800&height=480", config))
  {
    Serial.println("Failed to connect to the remote server...");
    return;
  }

  if (!hasSuccessfulStatusCode(client))
  {
    Serial.println("The request was not successful...");
    return;
  }

  Serial.println("Reading image content...");
  
  display.setPartialWindow(0, 0, displayWidth, displayHeight);
  
  int16_t x = 0;
  int16_t y = 0;

  while ((client.connected() || client.available()) && y < displayHeight)
  {
    const size_t bytesNeeded = static_cast<size_t>(frameBytes) * 2;
    size_t idx = 0;

    memset(epd_bitmap_BW, 0, frameBytes);
    memset(epd_bitmap_RW, 0, frameBytes);

    while (idx < bytesNeeded && (client.connected() || client.available()))
    {
      size_t available = client.available();
      if (available > 0)
      {
        size_t toRead = min(available, bytesNeeded - idx);
        uint8_t buffer[1024];
        size_t chunkSize = min(toRead, sizeof(buffer));
        size_t bytesRead = client.read(buffer, chunkSize);
        
        for (size_t i = 0; i < bytesRead; i++)
        {
          if ((idx & 1) == 0)
          {
            epd_bitmap_BW[idx / 2] = buffer[i];
          }
          else
          {
            epd_bitmap_RW[idx / 2] = buffer[i];
          }
          ++idx;
        }
      }
      else
      {
        if (!client.connected())
        {
          break;
        }
        yield();
      }
    }

    if (idx < bytesNeeded)
    {
      Serial.println("Incomplete frame data received, stopping.");
      break;
    }

    display.writeImage(epd_bitmap_BW, epd_bitmap_RW, x, y, frameWidth, frameHeight);
    y += frameHeight;
  }
  Serial.println();

  client.stop();
}

std::optional<uint64_t> fetchNextWaitSeconds(const Configuration &config)
{
  Serial.println("Connecting to the remote server...");

  WiFiClient client;
  if (!trySendGetRequest(client, "/api/configuration/next-update-wait-seconds", config))
  {
    return std::nullopt;
  }

  if (!hasSuccessfulStatusCode(client))
  {
    Serial.println("The request was not successful...");
    return std::nullopt;
  }

  Serial.println("Reading content...");
  String delayString{};
  if (client.connected() || client.available())
  {
    delayString = client.readStringUntil('\n');
    Serial.println(delayString);
  }

  client.stop();
  return delayString.length() > 0
             ? std::make_optional(strtoull(delayString.c_str(), nullptr, 10))
             : std::nullopt;
}

bool hasSuccessfulStatusCode(WiFiClient &client)
{
  Serial.println("Reading headers...");
  bool isStatusOk = false;
  while (client.connected() || client.available())
  {
    String line = client.readStringUntil('\n');
    Serial.println(line);

    if (!isStatusOk)
    {
      isStatusOk = line.startsWith("HTTP/1.1 200 OK");
    }

    if (line == "\r")
    { // Headers end with an empty line
      break;
    }
  }

  return isStatusOk;
}

bool trySendGetRequest(WiFiClient &client, const String &url, const Configuration &config)
{
  if (!client.connect(config.dashboardUrl.c_str(), config.dashboardPort))
  {
    Serial.println("Failed to connect to the remote server...");
    return false;
  }
  Serial.println("Successfully connected to the remote server!");
  Serial.println("Sending request...");

  client.println("GET " + url + " HTTP/1.1");
  client.print("X-Api-Key: ");
  client.println(config.dashboardApiKey);
  client.print("Host: ");
  client.print(config.dashboardUrl);
  client.print(":");
  client.println(config.dashboardPort);
  client.println("Connection: close");
  client.println();
  return true;
}

void startDeepSleep(const Configuration &config)
{
  uint64_t waitSeconds = fetchNextWaitSeconds(config).value_or(config.dashboardRate);
  uint64_t waitMicroseconds = waitSeconds * SEC_TO_USEC_FACTOR;
  esp_sleep_enable_timer_wakeup(waitMicroseconds);
  esp_sleep_enable_ext0_wakeup(RESET_WAKEUP_PIN, 1);
  rtc_gpio_pullup_dis(RESET_WAKEUP_PIN);
  rtc_gpio_pulldown_en(RESET_WAKEUP_PIN);
  esp_deep_sleep_start();
}

std::optional<Configuration> getConfiguration()
{
  Preferences preferences{};
  preferences.begin(CONFIGURATION_NAMESPACE, true);
  Configuration configuration{
      preferences.getString(CONFIGURATION_SSID, ""),
      preferences.getString(CONFIGURATION_PASSWORD, ""),
      preferences.getString(CONFIGURATION_DASHBOARD_URL, ""),
      preferences.getInt(CONFIGURATION_DASHBOARD_PORT, 80),
      preferences.getULong64(CONFIGURATION_DASHBOARD_RATE, 60),
      preferences.getString(CONFIGURATION_DASHBOARD_API_KEY, "")};
  preferences.end();

  return configuration.ssid.length() == 0 || configuration.dashboardUrl.length() == 0
             ? std::nullopt
             : std::make_optional(configuration);
}

void storeConfiguration(const Configuration &config)
{
  Preferences preferences{};
  preferences.begin(CONFIGURATION_NAMESPACE, false);
  preferences.putString(CONFIGURATION_SSID, config.ssid);
  preferences.putString(CONFIGURATION_PASSWORD, config.password);
  preferences.putString(CONFIGURATION_DASHBOARD_URL, config.dashboardUrl);
  preferences.putInt(CONFIGURATION_DASHBOARD_PORT, config.dashboardPort);
  preferences.putULong64(CONFIGURATION_DASHBOARD_RATE, config.dashboardRate);
  preferences.putString(CONFIGURATION_DASHBOARD_API_KEY, config.dashboardApiKey);
  preferences.end();
}

void clearConfiguration()
{
  Preferences preferences{};
  preferences.begin(CONFIGURATION_NAMESPACE, false);
  preferences.clear();
  preferences.end();
}

void showWelcomePage(const IPAddress &ip, const String &mac)
{
  Serial.println("Displaying welcome page...");
  
  display.setRotation(0);
  display.setFullWindow();
  display.firstPage();
  do
  {
    display.fillScreen(GxEPD_WHITE);
    
    // Title
    display.setFont(&FreeSansBold18pt7b);
    display.setTextColor(GxEPD_BLACK);
    int16_t tbx, tby; uint16_t tbw, tbh;
    display.getTextBounds("izBoard", 0, 0, &tbx, &tby, &tbw, &tbh);
    display.setCursor((displayWidth - tbw) / 2, 60);
    display.print("izBoard");
    
    // Setup mode text
    display.setFont(&FreeSans12pt7b);
    display.getTextBounds("Setup Mode", 0, 0, &tbx, &tby, &tbw, &tbh);
    display.setCursor((displayWidth - tbw) / 2, 100);
    display.print("Setup Mode");
    
    // IP Address
    display.setFont(&FreeSans12pt7b);
    String ipText = "IP: " + ip.toString();
    display.setCursor(50, 160);
    display.print(ipText);
    
    // MAC Address
    String macText = "MAC: " + mac;
    display.setCursor(50, 200);
    display.print(macText);
    
    // Instructions
    display.setCursor(50, 260);
    display.print("1. Connect to WiFi:");
    display.setCursor(70, 290);
    display.print("izBoard-AP");
    
    display.setCursor(50, 330);
    display.print("2. Open browser to:");
    display.setCursor(70, 360);
    display.print(ip.toString());
    
    // Generate QR code for GitHub repo
    const char* githubUrl = "https://github.com/izdev-digital/e-paper-dashboard";
    QRCode qrcode;
    uint8_t qrcodeData[qrcode_getBufferSize(3)];
    qrcode_initText(&qrcode, qrcodeData, 3, ECC_LOW, githubUrl);
    
    // Draw QR code in bottom right corner
    int qrX = 550;
    int qrY = 150;
    int moduleSize = 6;
    
    for (uint8_t y = 0; y < qrcode.size; y++) {
      for (uint8_t x = 0; x < qrcode.size; x++) {
        if (qrcode_getModule(&qrcode, x, y)) {
          display.fillRect(qrX + x * moduleSize, qrY + y * moduleSize, moduleSize, moduleSize, GxEPD_BLACK);
        }
      }
    }
    
    // QR code label
    display.setFont(&FreeSans12pt7b);
    display.setCursor(qrX + 10, qrY + qrcode.size * moduleSize + 30);
    display.print("GitHub");
    
  }
  while (display.nextPage());
  
  Serial.println("Welcome page displayed");
}

void createConfiguration()
{
  pinMode(LED_PIN, OUTPUT);
  digitalWrite(LED_PIN, LOW);
  
  IPAddress apIP(192, 168, 4, 1);
  IPAddress gateway(192, 168, 4, 1);
  IPAddress subnet(255, 255, 255, 0);
  WiFi.softAPConfig(apIP, gateway, subnet);
  WiFi.softAP("izBoard-AP");
  apIP = WiFi.softAPIP();
  String macAddress = WiFi.macAddress();
  Serial.print("AP IP address: ");
  Serial.println(apIP);
  Serial.print("MAC address: ");
  Serial.println(macAddress);

  // Display welcome page on e-paper
  showWelcomePage(apIP, macAddress);

  // DNS server setup: redirect all domains to ESP32 AP IP
  const byte DNS_PORT = 53;
  DNSServer dnsServer;
  dnsServer.start(DNS_PORT, "*", apIP);

  WebServer server(80);
  const char *htmlForm = R"rawliteral(
    <!DOCTYPE html>
    <html lang="en">

    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>izBoard Setup</title>
        <style>
            /* Minimal Bootstrap 5 styles for used classes */
            .container { max-width: 960px; margin-right: auto; margin-left: auto; padding-right: 12px; padding-left: 12px; }
            .mt-5 { margin-top: 3rem !important; }
            .text-center { text-align: center !important; }
            .card { position: relative; display: flex; flex-direction: column; min-width: 0; word-wrap: break-word; background-color: #fff; background-clip: border-box; border: 1px solid rgba(0,0,0,.125); border-radius: .25rem; }
            .card-header { padding: .75rem 1.25rem; margin-bottom: 0; background-color: rgba(0,0,0,.03); border-bottom: 1px solid rgba(0,0,0,.125); font-weight: 500; }
            .card-body { flex: 1 1 auto; padding: 1.25rem; }
            .mb-3 { margin-bottom: 1rem !important; }
            .form-label { margin-bottom: .5rem; font-weight: 500; display: inline-block; }
            .form-control { display: block; width: 100%; padding: .375rem .75rem; font-size: 1rem; line-height: 1.5; color: #212529; background-color: #fff; background-clip: padding-box; border: 1px solid #ced4da; border-radius: .25rem; transition: border-color .15s ease-in-out,box-shadow .15s ease-in-out; }
            .form-control:focus { color: #212529; background-color: #fff; border-color: #86b7fe; outline: 0; box-shadow: 0 0 0 .25rem rgba(13,110,253,.25); }
            .form-select { display: block; width: 100%; padding: .375rem 2.25rem .375rem .75rem; font-size: 1rem; line-height: 1.5; color: #212529; background-color: #fff; border: 1px solid #ced4da; border-radius: .25rem; transition: border-color .15s ease-in-out,box-shadow .15s ease-in-out; }
            .input-group { position: relative; display: flex; flex-wrap: wrap; align-items: stretch; width: 100%; }
            .btn { display: inline-block; font-weight: 400; line-height: 1.5; color: #fff; text-align: center; text-decoration: none; vertical-align: middle; cursor: pointer; background-color: #0d6efd; border: 1px solid #0d6efd; padding: .375rem .75rem; font-size: 1rem; border-radius: .25rem; transition: color .15s ease-in-out,background-color .15s ease-in-out,border-color .15s ease-in-out,box-shadow .15s ease-in-out; }
            .btn-primary { color: #fff; background-color: #0d6efd; border-color: #0d6efd; }
            .btn-primary:hover { color: #fff; background-color: #0b5ed7; border-color: #0a58ca; }
            .w-100 { width: 100% !important; }
        </style>
    </head>

    <body>

        <div class="container mt-5">
            <h2 class="text-center">izBoard Setup</h2>

            <form action="/submit" method="post">
                <div class="card mb-3">
                    <div class="card-header">WLAN Setup</div>
                    <div class="card-body">
                        <div class="mb-3">
                            <label for="ssid" class="form-label">SSID</label>
                            <input type="text" class="form-control" name="ssid" id="ssid" placeholder="Enter SSID ...">
                        </div>
                        <div class="mb-3">
                            <label for="password" class="form-label">Password</label>
                            <input type="password" class="form-control" name="password" id="password"
                                placeholder="Enter password ...">
                        </div>
                    </div>
                </div>

                <div class="card mb-3">
                    <div class="card-header">Dashboard Provider</div>
                    <div class="card-body">
                        <div class="mb-3">
                            <label for="dashboard_url" class="form-label">Url</label>
                            <input type="text" class="form-control" name="dashboard_url" id="dashboard_url"
                                placeholder="Enter url ...">
                        </div>
                        <div class="mb-3">
                            <label for="dashboard_port" class="form-label">Port</label>
                            <input type="text" class="form-control" name="dashboard_port" id="dashboard_port"
                                placeholder="Enter port ...">
                        </div>
                        <div class="mb-3">
                            <label for="dashboard_apikey" class="form-label">API Key</label>
                            <input type="text" class="form-control" name="dashboard_apikey" id="dashboard_apikey"
                                placeholder="Enter API key ...">
                        </div>
                        <div class="mb-3">
                            <label for="time-period" class="form-label">Select Refresh Rate:</label>
                            <div class="input-group" id="time-period">
                                <input type="number" class="form-control" name="dashboard_rate" id="dashboard_rate" min="1" max="60"
                                    placeholder="Enter number ...">
                                <select class="form-select" name="dashboard_rate_unit" id="dashboard_rate_unit">
                                    <option value="s">Seconds</option>
                                    <option value="m">Minutes</option>
                                    <option value="h">Hours</option>
                                    <option value="d">Days</option>
                                </select>
                            </div>
                        </div>
                    </div>
                </div>

                <button type="submit" class="btn btn-primary w-100">Apply</button>
            </form>
        </div>
    </body>

    </html>
    )rawliteral";
  server.on("/", [&server, htmlForm]()
            { server.send(200, "text/html", htmlForm); });

  server.on("/submit", HTTP_POST, [&server, htmlForm]()
            {
    const String ssidParam{ "ssid" };
    const String passParam{ "password" };
    const String urlParam{ "dashboard_url" };
    const String portParam{ "dashboard_port" };
    const String rateParam{ "dashboard_rate" };
    const String rateUnitParam{ "dashboard_rate_unit" };
    const String apiKeyParam{ "dashboard_apikey" };
    if (
      !server.hasArg(ssidParam) || !server.hasArg(passParam) || !server.hasArg(urlParam) || !server.hasArg(portParam) || !server.hasArg(rateParam) || !server.hasArg(rateUnitParam) || !server.hasArg(apiKeyParam)) {
      server.send(400, "text/html", htmlForm);
      return;
    }

    const String ssid{ server.arg(ssidParam) };
    const String pass{ server.arg(passParam) };
    const String url{ server.arg(urlParam) };
    const int port{ server.arg(portParam).toInt() };
    const uint64_t rate{ static_cast<uint64_t>(server.arg(rateParam).toInt()) };
    const String unit{ server.arg(rateUnitParam) };
    const String apiKey{ server.arg(apiKeyParam) };

    int32_t unitMultiplier{ 1 };
    if (unit.equals("m")) {
      unitMultiplier *= 60;
    } else if (unit.equals("h")) {
      unitMultiplier *= 3600;
    } else if (unit.equals("d")) {
      unitMultiplier *= (3600 * 24);
    }

    const uint64_t dashboardRefreshRate = (rate + 1) * unitMultiplier;

    Configuration config{
      ssid,
      pass,
      url,
      port,
      dashboardRefreshRate,
      apiKey
    };
    Serial.println("Received configuration...");
    storeConfiguration(config);

    server.send(200, "text/html", "Settings saved. Rebooting...");
    digitalWrite(LED_PIN, LOW);
    delay(1000);
    ESP.restart(); });

  auto redirectToRoot = [&server]()
  {
    server.sendHeader("Location", "/", true);
    server.send(302, "text/plain", "Redirecting to setup page...");
  };

  server.on("/generate_204", redirectToRoot);
  server.on("/hotspot-detect.html", redirectToRoot);
  server.on("/ncsi.txt", redirectToRoot);
  server.onNotFound([&server, htmlForm, &redirectToRoot]()
                    {
    if (server.uri() == "/submit") {
      server.send(404, "text/plain", "Not found");
      return;
    }
    redirectToRoot(); });

  server.begin();
  Serial.println("HTTP server started");

  unsigned long lastBlinkTime = 0;
  bool ledState = false;
  const unsigned long blinkInterval = 500;

  while (true)
  {
    dnsServer.processNextRequest();
    server.handleClient();
    
    unsigned long currentTime = millis();
    if (currentTime - lastBlinkTime >= blinkInterval)
    {
      lastBlinkTime = currentTime;
      ledState = !ledState;
      digitalWrite(LED_PIN, ledState ? HIGH : LOW);
    }
    
    delay(2);
  }
}

bool connectToWiFi(const Configuration &config)
{
  Serial.println("Found stored configuration!");
  Serial.println("Connecting to WiFi");
  const auto maxConnectionTestRetries = 20;
  auto connectionTestRetry = 0;
  WiFi.begin(config.ssid.c_str(), config.password.c_str());
  while (WiFi.status() != WL_CONNECTED && connectionTestRetry < maxConnectionTestRetries)
  {
    ++connectionTestRetry;
    delay(500);
  }
  if (WiFi.status() != WL_CONNECTED)
  {
    return false;
  }

  Serial.println("WiFi connected");
  Serial.print("IP address: ");
  Serial.println(WiFi.localIP());
  return true;
}

bool isResetRequested()
{
  pinMode(RESET_WAKEUP_PIN, INPUT_PULLDOWN);
  if (digitalRead(RESET_WAKEUP_PIN) != HIGH)
  {
    return false;
  }

  const unsigned long pressStartTime = millis();
  const unsigned long requiredWaitingTime = RESET_REQUEST_TIMEOUT * 1000;
  while (digitalRead(RESET_WAKEUP_PIN) == HIGH)
  {
    if (millis() - pressStartTime >= requiredWaitingTime)
    {
      return true;
    }
  }

  return false;
}

void resetDevice()
{
  clearConfiguration();
  ESP.restart();
}