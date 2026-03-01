#include "display_manager.h"

DisplayManager::DisplayManager(Logger& logger) : _logger(logger) {}

bool DisplayManager::init()
{
  _hspi.begin(Pin::SpiSck, Pin::SpiMiso, Pin::SpiMosi, Pin::SpiSs);
  _display.epd2.selectSPI(_hspi, SPISettings(20000000, MSBFIRST, SPI_MODE0));
  _display.init(115200);

  _bwBuffer = (uint8_t*)malloc(DisplayConst::FrameBytes);
  _rwBuffer = (uint8_t*)malloc(DisplayConst::FrameBytes);
  return _bwBuffer && _rwBuffer;
}

void DisplayManager::clearBuffers()
{
  memset(_bwBuffer, 0, DisplayConst::FrameBytes);
  memset(_rwBuffer, 0, DisplayConst::FrameBytes);
}

void DisplayManager::writePixelByte(size_t idx, uint8_t value)
{
  if ((idx & 1) == 0)
  {
    _bwBuffer[idx / 2] = value;
  }
  else
  {
    _rwBuffer[idx / 2] = value;
  }
}

void DisplayManager::writeFrame(int16_t x, int16_t y, uint16_t w, uint16_t h)
{
  _display.writeImage(_bwBuffer, _rwBuffer, x, y, w, h);
}

void DisplayManager::beginPartialWindow()
{
  _display.setPartialWindow(0, 0, DisplayConst::Width, DisplayConst::Height);
}

void DisplayManager::refresh()
{
  _display.refresh();
}

void DisplayManager::powerOff()
{
  _display.powerOff();
}

void DisplayManager::drawIcon(int16_t ox, int16_t oy, int16_t size)
{
  const float s = size / 370.0f;
  auto rnd = [](float v) -> int16_t { return (int16_t)roundf(v); };

  // Single pixel gap used everywhere (largest integer that fits the 12-unit SVG gap)
  int16_t gap = max((int16_t)1, (int16_t)(12.0f * s));

  // Item sizes rounded from SVG proportions
  int16_t margin = rnd(20 * s);
  int16_t lcw    = rnd(90 * s);
  int16_t r1h    = rnd(96 * s);
  int16_t r2h    = rnd(96 * s);
  int16_t r3h    = rnd(88 * s);

  // Horizontal grid
  int16_t x0 = margin;
  int16_t x2 = x0 + lcw + gap;
  int16_t xR = rnd(350 * s);
  int16_t rcAvail = xR - x2 - gap;

  int16_t topLw = rnd(134.0f / 216.0f * rcAvail);
  int16_t x3 = x2 + topLw;
  int16_t x4 = x3 + gap;

  int16_t botLw = rnd(84.0f / 216.0f * rcAvail);
  int16_t x5 = x2 + botLw;
  int16_t x6 = x5 + gap;

  // Vertical grid
  int16_t y0 = margin;
  int16_t y2 = y0 + r1h + gap;
  int16_t y3 = y2 + r2h;
  int16_t y4 = y3 + gap;

  // Left column: top tile + tall tile spanning rows 2-3
  _display.fillRect(ox + x0, oy + y0, lcw, r1h, GxEPD_RED);
  _display.fillRect(ox + x0, oy + y2, lcw, r2h + gap + r3h, GxEPD_RED);

  // Top row, right column
  _display.fillRect(ox + x2, oy + y0, topLw, r1h, GxEPD_RED);
  _display.fillRect(ox + x4, oy + y0, xR - x4, r1h, GxEPD_RED);

  // Middle row: diagonal-split trapezoids
  int16_t mx2 = ox + x2,     mx3 = ox + x3 - 1, mx4 = ox + x4;
  int16_t mx5 = ox + x5 - 1, mx6 = ox + x6,     mxR = ox + xR - 1;
  int16_t my2 = oy + y2,     my3 = oy + y3 - 1;

  _display.fillTriangle(mx2, my2, mx3, my2, mx2, my3, GxEPD_RED);
  _display.fillTriangle(mx3, my2, mx5, my3, mx2, my3, GxEPD_RED);
  _display.fillTriangle(mx4, my2, mxR, my2, mxR, my3, GxEPD_RED);
  _display.fillTriangle(mx4, my2, mxR, my3, mx6, my3, GxEPD_RED);

  // Bottom row, right column
  _display.fillRect(ox + x2, oy + y4, botLw, r3h, GxEPD_RED);
  _display.fillRect(ox + x6, oy + y4, xR - x6, r3h, GxEPD_RED);
}

void DisplayManager::showWelcomePage(const IPAddress& ip, const String& mac, const String& apName)
{
  _logger.println("Displaying welcome page...");

  _display.setRotation(0);
  _display.setFullWindow();
  _display.firstPage();
  do
  {
    _display.fillScreen(GxEPD_WHITE);

    drawIcon((DisplayConst::Width - 80) / 2, 10, 80);

    _display.setFont(&FreeSansBold18pt7b);
    _display.setTextColor(GxEPD_BLACK);
    int16_t tbx, tby;
    uint16_t tbw, tbh;
    _display.getTextBounds("izBoard", 0, 0, &tbx, &tby, &tbw, &tbh);
    _display.setCursor((DisplayConst::Width - tbw) / 2, 130);
    _display.print("izBoard");

    _display.setFont(&FreeSans12pt7b);
    _display.getTextBounds("Setup Mode", 0, 0, &tbx, &tby, &tbw, &tbh);
    _display.setCursor((DisplayConst::Width - tbw) / 2, 170);
    _display.print("Setup Mode");

    _display.setFont(&FreeSans12pt7b);
    String ipText = "IP: " + ip.toString();
    _display.setCursor(50, 230);
    _display.print(ipText);

    String macText = "MAC: " + mac;
    _display.setCursor(50, 270);
    _display.print(macText);

    _display.setCursor(50, 330);
    _display.print("1. Connect to WiFi:");
    _display.setCursor(70, 360);
    _display.print(apName);

    _display.setCursor(50, 400);
    _display.print("2. Open browser to:");
    _display.setCursor(70, 430);
    _display.print(ip.toString());

    const char* githubUrl = "https://github.com/izdev-digital/e-paper-dashboard";
    QRCode qrcode;
    uint8_t qrcodeData[qrcode_getBufferSize(3)];
    qrcode_initText(&qrcode, qrcodeData, 3, ECC_LOW, githubUrl);

    int qrX = 550;
    int qrY = 220;
    int moduleSize = 6;

    for (uint8_t y = 0; y < qrcode.size; y++)
    {
      for (uint8_t x = 0; x < qrcode.size; x++)
      {
        if (qrcode_getModule(&qrcode, x, y))
        {
          _display.fillRect(qrX + x * moduleSize, qrY + y * moduleSize, moduleSize, moduleSize, GxEPD_BLACK);
        }
      }
    }

    _display.setFont(&FreeSans12pt7b);
    _display.setCursor(qrX + 10, qrY + qrcode.size * moduleSize + 30);
    _display.print("GitHub");

  } while (_display.nextPage());

  _logger.println("Welcome page displayed");
}

void DisplayManager::showSuccess(const String& title, const String& message, const String& hint)
{
  _logger.println("Displaying success page...");

  _display.setRotation(0);
  _display.setFullWindow();
  _display.firstPage();
  do
  {
    _display.fillScreen(GxEPD_WHITE);

    drawIcon((DisplayConst::Width - 80) / 2, 30, 80);

    _display.setFont(&FreeSansBold18pt7b);
    _display.setTextColor(GxEPD_BLACK);
    int16_t tbx, tby;
    uint16_t tbw, tbh;
    _display.getTextBounds(title.c_str(), 0, 0, &tbx, &tby, &tbw, &tbh);
    _display.setCursor((DisplayConst::Width - tbw) / 2, 160);
    _display.print(title);

    _display.setFont(&FreeSans12pt7b);
    _display.setTextColor(GxEPD_BLACK);

    int yPos = 220;
    int lineStart = 0;
    for (int i = 0; i <= (int)message.length(); i++)
    {
      if (i == (int)message.length() || message[i] == '\n')
      {
        String line = message.substring(lineStart, i);
        _display.getTextBounds(line.c_str(), 0, 0, &tbx, &tby, &tbw, &tbh);
        _display.setCursor((DisplayConst::Width - tbw) / 2, yPos);
        _display.print(line);
        yPos += 35;
        lineStart = i + 1;
      }
    }

    if (hint.length() > 0)
    {
      yPos += 15;
      _display.getTextBounds(hint.c_str(), 0, 0, &tbx, &tby, &tbw, &tbh);
      _display.setCursor((DisplayConst::Width - tbw) / 2, yPos);
      _display.print(hint);
    }

  } while (_display.nextPage());

  _logger.println("Success page displayed");
}
