#include <Preferences.h>
#include <optional>
#include <WiFi.h>
#include <WebServer.h>

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

static const char* CONFIGURATION_NAMESPACE = "config";
static const char* CONFIGURATION_SSID = "ssid";
static const char* CONFIGURATION_PASSWORD = "pwd";

SPIClass hspi(HSPI);

void drawBitmap();
void startDeepSleep();

struct Configuration {
  String ssid;
  String password;
};

std::optional<Configuration> getConfiguration() {
  Preferences preferences{};
  preferences.begin(CONFIGURATION_NAMESPACE, true);
  String ssid = preferences.getString(CONFIGURATION_SSID, "");
  String password = preferences.getString(CONFIGURATION_PASSWORD, "");
  preferences.end();

  if (ssid.length() == 0) {
    return std::nullopt;
  }

  Configuration credentials{ ssid, password };
  return std::make_optional(credentials);
}

void storeConfiguration(const Configuration& config) {
  Preferences preferences{};
  preferences.begin(CONFIGURATION_NAMESPACE, false);
  preferences.putString(CONFIGURATION_SSID, config.ssid);
  preferences.putString(CONFIGURATION_PASSWORD, config.password);
  preferences.end();
}

void createConfiguration() {
  WiFi.softAP("EPaperDashboard-AP");
  IPAddress myIP = WiFi.softAPIP();
  Serial.print("AP IP address: ");
  Serial.println(myIP);

  WebServer server(80);

  server.on("/", [&server]() {
    const char* htmlForm = R"rawliteral(
        <!DOCTYPE HTML><html>
        <head><title>ESP32 Setup</title></head>
        <body>
          <h1>ESP32 Setup</h1>
          <form action="/submit" method="post">
            SSID: <input type="text" name="ssid"><br>
            Password: <input type="password" name="password"><br>
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
      server.arg("password")
    };
    Serial.print("SSID: ");
    Serial.println(config.ssid);
    Serial.print("Password: ");
    Serial.println(config.password);

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

// if(!isWifiConfigured())
// {
//   startAccessPoint();
//   waitForCredentialsToBeEntered();
// }

// connectToWifi();
// fetchBinaryData();
// drawBitmap();
// disconnectFromWifi();
// startDeepSleep();



void setup() {
  Serial.begin(115200);

  // hspi.begin(13, 12, 14, 15);  // remap hspi for EPD (swap pins)

  // display.epd2.selectSPI(hspi, SPISettings(4000000, MSBFIRST, SPI_MODE0));
  // display.init(115200);

  // drawBitmap();
  // display.powerOff();

  // startDeepSleep();
  const auto configuration = getConfiguration();
  if (!configuration.has_value()) {
    createConfiguration();
  }

  Serial.println("Credentials found!");
  Serial.print("SSID: ");
  Serial.println(configuration.value().ssid);
  Serial.print("Password: ");
  Serial.println(configuration.value().password);
}

void loop() {
}

struct bitmap_pair {
  const unsigned char* black;
  const unsigned char* red;
};

// 'R', 800x480px
const unsigned char epd_bitmap_BW[] PROGMEM = {

};

const unsigned char epd_bitmap_RW[] PROGMEM = {

};

void drawBitmap() {
  bitmap_pair frame = { epd_bitmap_BW, epd_bitmap_RW };

  display.setFullWindow();
  display.writeImage(frame.black, frame.red, 0, 0, display.epd2.WIDTH, display.epd2.HEIGHT, false, false, true);
  display.refresh();
}

void startDeepSleep() {
  esp_sleep_enable_timer_wakeup(TIME_TO_SLEEP_SEC * USEC_TO_SEC_FACTOR);
  esp_deep_sleep_start();
}