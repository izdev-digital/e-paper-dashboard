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

void DisplayManager::showWelcomePage(const IPAddress& ip, const String& mac)
{
  _logger.println("Displaying welcome page...");

  _display.setRotation(0);
  _display.setFullWindow();
  _display.firstPage();
  do
  {
    _display.fillScreen(GxEPD_WHITE);

    _display.setFont(&FreeSansBold18pt7b);
    _display.setTextColor(GxEPD_BLACK);
    int16_t tbx, tby;
    uint16_t tbw, tbh;
    _display.getTextBounds("izBoard", 0, 0, &tbx, &tby, &tbw, &tbh);
    _display.setCursor((DisplayConst::Width - tbw) / 2, 60);
    _display.print("izBoard");

    _display.setFont(&FreeSans12pt7b);
    _display.getTextBounds("Setup Mode", 0, 0, &tbx, &tby, &tbw, &tbh);
    _display.setCursor((DisplayConst::Width - tbw) / 2, 100);
    _display.print("Setup Mode");

    _display.setFont(&FreeSans12pt7b);
    String ipText = "IP: " + ip.toString();
    _display.setCursor(50, 160);
    _display.print(ipText);

    String macText = "MAC: " + mac;
    _display.setCursor(50, 200);
    _display.print(macText);

    _display.setCursor(50, 260);
    _display.print("1. Connect to WiFi:");
    _display.setCursor(70, 290);
    _display.print("izBoard-AP");

    _display.setCursor(50, 330);
    _display.print("2. Open browser to:");
    _display.setCursor(70, 360);
    _display.print(ip.toString());

    const char* githubUrl = "https://github.com/izdev-digital/e-paper-dashboard";
    QRCode qrcode;
    uint8_t qrcodeData[qrcode_getBufferSize(3)];
    qrcode_initText(&qrcode, qrcodeData, 3, ECC_LOW, githubUrl);

    int qrX = 550;
    int qrY = 150;
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
