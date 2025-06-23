#include <Preferences.h>
#include <optional>
#include <WiFi.h>
#include <WebServer.h>
#include <driver/rtc_io.h>

#define ENABLE_GxEPD2_GFX 0

#include <GxEPD2_3C.h>

#define GxEPD2_DISPLAY_CLASS GxEPD2_3C
#define GxEPD2_DRIVER_CLASS GxEPD2_750c_Z08  // GDEW075Z08  800x480, EK79655 (GD7965), (WFT0583CZ61)

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
static const uint16_t frameHeight = 32;
static const uint16_t frameBytes = frameWidth * frameHeight / 8;
static uint8_t epd_bitmap_BW[frameBytes] = { 0 };
static uint8_t epd_bitmap_RW[frameBytes] = { 0 };

SPIClass hspi(HSPI);

struct Configuration {
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
bool isResetRequested();
void resetDevice();

bool connectToWiFi(const Configuration &config);
bool hasSuccessfulStatusCode(WiFiClient &client);
void fetchBinaryData(const Configuration &config);
std::optional<uint64_t> fetchNextWaitSeconds(const Configuration &config);
bool trySendGetRequest(WiFiClient &client, const String &url, const Configuration &config);

void setup() {
  Serial.begin(115200);
  hspi.begin(13, 12, 14, 15);  // remap hspi for EPD (swap pins)
  display.epd2.selectSPI(hspi, SPISettings(4000000, MSBFIRST, SPI_MODE0));
  display.init(115200);

  if (isResetRequested()) {
    Serial.println("Resetting device");
    resetDevice();
  }

  const auto configuration = getConfiguration();
  if (!configuration.has_value()) {
    createConfiguration();
    return;
  }

  if (connectToWiFi(configuration.value())) {
    fetchBinaryData(configuration.value());
  }

  display.refresh();
  display.powerOff();

  startDeepSleep(configuration.value());
}

void loop() {
  // This function should not be reached. Restarting in case this happened.
  ESP.restart();
}

void fetchBinaryData(const Configuration &config) {
  Serial.println("Connecting to the remote server...");

  WiFiClient client;
  if (!trySendGetRequest(client, "/api/render/binary?width=800&height=480", config)) {
    Serial.println("Failed to connect to the remote server...");
    return;
  }

  if (!hasSuccessfulStatusCode(client)) {
    Serial.println("The request was not successful...");
    return;
  }

  Serial.println("Reading image content...");
  int16_t x = 0;
  int16_t y = 0;

  while ((client.connected() || client.available()) && y < displayHeight) {
    for (int16_t pixelCount = 0; pixelCount < frameBytes && client.available(); pixelCount++) {
      uint8_t blackPixel = client.read();
      uint8_t redPixel = client.read();
      epd_bitmap_BW[pixelCount] = blackPixel;
      epd_bitmap_RW[pixelCount] = redPixel;
    }

    display.setPartialWindow(x, y, frameWidth, frameHeight);
    display.writeImage(epd_bitmap_BW, epd_bitmap_RW, x, y, frameWidth, frameHeight);
    y += frameHeight;
  }
  Serial.println();

  client.stop();
}

std::optional<uint64_t> fetchNextWaitSeconds(const Configuration &config) {
  Serial.println("Connecting to the remote server...");

  WiFiClient client;
  if (!trySendGetRequest(client, "/api/configuration/next-update-wait-seconds", config)) {
    Serial.println("Failed to connect to the remote server...");
    return std::nullopt;
  }

  if (!hasSuccessfulStatusCode(client)) {
    Serial.println("The request was not successful...");
    return std::nullopt;
  }

  Serial.println("Reading content...");
  String delayString{};
  if (client.connected() || client.available()) {
    delayString = client.readStringUntil('\n');
    Serial.println(delayString);
  }
  client.stop();
  return delayString.length() > 0 
    ? std::make_optional(strtoull(delayString.c_str(), nullptr, 10))
    : std::nullopt;
}

bool hasSuccessfulStatusCode(WiFiClient &client) {
  Serial.println("Reading headers...");
  bool isStatusOk = false;
  while (client.connected() || client.available()) {
    String line = client.readStringUntil('\n');
    Serial.println(line);

    if (!isStatusOk) {
      isStatusOk = line.startsWith("HTTP/1.1 200 OK");
    }

    if (line == "\r") {  // Headers end with an empty line
      break;
    }
  }

  return isStatusOk;
}

bool trySendGetRequest(WiFiClient &client, const String &url, const Configuration &config) {
  if (!client.connect(config.dashboardUrl.c_str(), config.dashboardPort)) {
    Serial.println("Failed to connect to the remote server...");
    return false;
  }
  Serial.println("Successfully connected to the remote server!");
  Serial.println("Sending request...");

  client.println("GET " + url + " HTTP/1.0");
  client.print("X-Api-Key: ");
  client.println(config.dashboardApiKey);
  client.println();  // This line sends the request
  return true;
}

void startDeepSleep(const Configuration &config) {
  uint64_t waitSeconds = fetchNextWaitSeconds(config).value_or(config.dashboardRate);
  uint64_t waitMicroseconds = waitSeconds * SEC_TO_USEC_FACTOR;
  esp_sleep_enable_timer_wakeup(waitMicroseconds);
  esp_sleep_enable_ext0_wakeup(RESET_WAKEUP_PIN, 1);
  rtc_gpio_pullup_dis(RESET_WAKEUP_PIN);
  rtc_gpio_pulldown_en(RESET_WAKEUP_PIN);
  esp_deep_sleep_start();
}

std::optional<Configuration> getConfiguration() {
  Preferences preferences{};
  preferences.begin(CONFIGURATION_NAMESPACE, true);
  Configuration configuration{
    preferences.getString(CONFIGURATION_SSID, ""),
    preferences.getString(CONFIGURATION_PASSWORD, ""),
    preferences.getString(CONFIGURATION_DASHBOARD_URL, ""),
    preferences.getInt(CONFIGURATION_DASHBOARD_PORT, 80),
    preferences.getULong64(CONFIGURATION_DASHBOARD_RATE, 60),
    preferences.getString(CONFIGURATION_DASHBOARD_API_KEY, "")
  };
  preferences.end();

  return configuration.ssid.length() == 0 || configuration.dashboardUrl.length() == 0
           ? std::nullopt
           : std::make_optional(configuration);
}

void storeConfiguration(const Configuration &config) {
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

void clearConfiguration() {
  Preferences preferences{};
  preferences.begin(CONFIGURATION_NAMESPACE, false);
  preferences.clear();
  preferences.end();
}

void createConfiguration() {
  WiFi.softAP("EPaperDashboard-AP");
  Serial.print("AP IP address: ");
  Serial.println(WiFi.softAPIP());

  WebServer server(80);
  const char *htmlForm = R"rawliteral(
    <!DOCTYPE html>
    <html lang="en">

    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>E-Paper Dashboard Setup</title>
        <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css" rel="stylesheet">
    </head>

    <body>

        <div class="container mt-5">
            <h2 class="text-center">E-Paper Dashboard Setup</h2>

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

        <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js"></script>
    </body>

    </html>
    )rawliteral";
  server.on("/", [&server, htmlForm]() {
    server.send(200, "text/html", htmlForm);
  });

  server.on("/submit", HTTP_POST, [&server, htmlForm]() {
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
    const uint64_t rate{ server.arg(rateParam).toInt() };
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
    delay(1000);
    ESP.restart();
  });

  server.begin();
  Serial.println("HTTP server started");

  while (true) {
    server.handleClient();
    delay(2);
  }
}

bool connectToWiFi(const Configuration &config) {
  Serial.println("Found stored configuration!");
  Serial.println("Connecting to WiFi");
  const auto maxConnectionTestRetries = 20;
  auto connectionTestRetry = 0;
  WiFi.begin(config.ssid.c_str(), config.password.c_str());
  while (WiFi.status() != WL_CONNECTED && connectionTestRetry < maxConnectionTestRetries) {
    ++connectionTestRetry;
    delay(500);
  }
  if (WiFi.status() != WL_CONNECTED) {
    return false;
  }

  Serial.println("WiFi connected");
  Serial.print("IP address: ");
  Serial.println(WiFi.localIP());
  return true;
}

bool isResetRequested() {
  pinMode(RESET_WAKEUP_PIN, INPUT_PULLDOWN);
  if (digitalRead(RESET_WAKEUP_PIN) != HIGH) {
    return false;
  }

  const unsigned long pressStartTime = millis();
  const unsigned long requiredWaitingTime = RESET_REQUEST_TIMEOUT * 1000;
  while (digitalRead(RESET_WAKEUP_PIN) == HIGH) {
    if (millis() - pressStartTime >= requiredWaitingTime) {
      return true;
    }
  }

  return false;
}

void resetDevice() {
  clearConfiguration();
  ESP.restart();
}