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

#define USEC_TO_SEC_FACTOR 1000000
#define TIME_TO_SLEEP_SEC 120
#define RESET_WAKEUP_PIN GPIO_NUM_33
#define RESET_REQUEST_TIMEOUT 10

static const char* CONFIGURATION_NAMESPACE = "config";
static const char* CONFIGURATION_SSID = "ssid";
static const char* CONFIGURATION_PASSWORD = "pwd";
static const char* CONFIGURATION_DASHBOARD_URL = "url";

static uint8_t epd_bitmap_BW[800 * 32 / 8] = { 0 };
static uint8_t epd_bitmap_RW[800 * 32 / 8] = { 0 };

SPIClass hspi(HSPI);

struct Configuration {
  String ssid;
  String password;
  String dashboardUrl;
};

void fetchBinaryData();
void startDeepSleep();
std::optional<Configuration> getConfiguration();
void storeConfiguration(const Configuration& config);
void clearConfiguration();
void createConfiguration();
bool connectToWiFi(const Configuration& config);
bool isResetRequested();
void resetDevice();

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
  }

  if (connectToWiFi(configuration.value())) {
    fetchBinaryData();
    // Frame frame{ epd_bitmap_BW, epd_bitmap_RW };
    // drawBitmap(frame);
    // disconnectFromWifi();
  }

  Serial.println("Refresh screen");
  display.refresh();
  Serial.println("Power off screen");
  display.powerOff();

  startDeepSleep();
}

void loop() {
  //This function will not be reached
}

void fetchBinaryData() {
  Serial.print("Connecting to the remote server");
  WiFiClient client;
  IPAddress server(192, 168, 2, 2);
  if (client.connect(server, 8128)) {
    Serial.println("connected");
    client.println("GET /api/render/binary?width=800&height=480 HTTP/1.0");
    client.println();

    unsigned long long int counter = 0;
    // **Skip headers**
    while (client.connected() || client.available()) {
      String line = client.readStringUntil('\n');  // TODO: check the status code
      Serial.println(line);
      if (line == "\r") {  // Headers end with an empty line
        break;
      }
    }
    Serial.printf("Counter before: %d", counter);
    Serial.println();

    int16_t x = 0;
    int16_t y = 0;

    for (int16_t y = 0; y < 480; y += 32) {
      Serial.println("Next frame");
      display.setPartialWindow(x, y, 800, 32);
      display.writeImage(epd_bitmap_BW, epd_bitmap_RW, x, y, 800, 32);
    }

    while (client.connected() || client.available()) {
      if (client.available()) {
        uint8_t value = client.read();
        
        counter++;
      }
    }
    Serial.println();

    Serial.print("read bytes: ");
    Serial.println(counter);
  }

  client.stop();
}

// void drawBitmap(const Frame& frame) {
//   hspi.begin(13, 12, 14, 15);  // remap hspi for EPD (swap pins)
//   display.epd2.selectSPI(hspi, SPISettings(4000000, MSBFIRST, SPI_MODE0));
//   display.init(115200);
//   display.setFullWindow();
//   display.writeImage(frame.black, frame.red, 0, 0, display.epd2.WIDTH, display.epd2.HEIGHT, false, false, true);
//   display.refresh();
//   display.powerOff();
// }

void startDeepSleep() {
  esp_sleep_enable_timer_wakeup(TIME_TO_SLEEP_SEC * USEC_TO_SEC_FACTOR);
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
    preferences.getString(CONFIGURATION_DASHBOARD_URL, "")
  };
  preferences.end();

  return configuration.ssid.length() == 0  // TODO: || configuration.dashboardUrl.length() == 0
           ? std::nullopt
           : std::make_optional(configuration);
}

void storeConfiguration(const Configuration& config) {
  Preferences preferences{};
  preferences.begin(CONFIGURATION_NAMESPACE, false);
  preferences.putString(CONFIGURATION_SSID, config.ssid);
  preferences.putString(CONFIGURATION_PASSWORD, config.password);
  preferences.putString(CONFIGURATION_DASHBOARD_URL, config.dashboardUrl);
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

  server.on("/", [&server]() {
    const char* htmlForm = R"rawliteral(
        <!DOCTYPE HTML><html>
        <head><title>E-Paper Dashboard Setup</title></head>
        <body>
          <h1>E-Paper Dashboard Setup</h1>
          <form action="/submit" method="post">
            SSID: <input type="text" name="ssid"><br>
            Password: <input type="password" name="password"><br>
            Dashboard url: <input type="text" name="dashboard_url"><br>
            <input type="submit" value="Submit">
          </form>
        </body>
        </html>
      )rawliteral";
    server.send(200, "text/html", htmlForm);
  });

  server.on("/submit", HTTP_POST, [&server]() {
    Configuration config{
      server.arg("ssid"),
      server.arg("password"),
      server.arg("dashboard_url")
    };
    Serial.print("SSID: ");
    Serial.println(config.ssid);
    Serial.print("Password: ");
    Serial.println(config.password);
    Serial.print("Password: ");
    Serial.println(config.dashboardUrl);

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

bool connectToWiFi(const Configuration& config) {
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