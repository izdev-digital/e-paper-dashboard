#pragma once

#include <Arduino.h>
#include <SPI.h>

#define ENABLE_GxEPD2_GFX 1

#include <GxEPD2_3C.h>
#include <qrcode.h>
#include <Fonts/FreeSansBold18pt7b.h>
#include <Fonts/FreeSans12pt7b.h>

#include "constants.h"
#include "logger.h"

#define GxEPD2_DISPLAY_CLASS GxEPD2_3C
#define GxEPD2_DRIVER_CLASS GxEPD2_750c_Z08

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

using EpaperDisplay = GxEPD2_DISPLAY_CLASS<GxEPD2_DRIVER_CLASS, MAX_HEIGHT(GxEPD2_DRIVER_CLASS)>;

class DisplayManager {
public:
  explicit DisplayManager(Logger& logger);

  bool init();
  void clearBuffers();
  void writePixelByte(size_t idx, uint8_t value);
  void writeFrame(int16_t x, int16_t y, uint16_t w, uint16_t h);
  void beginPartialWindow();
  void refresh();
  void powerOff();
  void showWelcomePage(const IPAddress& ip, const String& mac, const String& apName);
  void showConfirmationPin(const String& pin);

private:
  void drawIcon(int16_t ox, int16_t oy, int16_t size);

  Logger& _logger;
  SPIClass _hspi{HSPI};
  EpaperDisplay _display{GxEPD2_DRIVER_CLASS(Pin::DisplayCs, Pin::DisplayDc, Pin::DisplayRst, Pin::DisplayBusy)};
  uint8_t* _bwBuffer = nullptr;
  uint8_t* _rwBuffer = nullptr;
};
