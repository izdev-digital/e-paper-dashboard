#include "hardware.h"
#include "constants.h"
#include <driver/rtc_io.h>

void Hardware::startDeepSleep(uint64_t waitSeconds)
{
  uint64_t waitMicroseconds = waitSeconds * Timing::SecToUsec;
  esp_sleep_enable_timer_wakeup(waitMicroseconds);
  esp_sleep_enable_ext0_wakeup(Pin::ResetWakeup, 1);
  rtc_gpio_pullup_dis(Pin::ResetWakeup);
  rtc_gpio_pulldown_en(Pin::ResetWakeup);
  esp_deep_sleep_start();
}

bool Hardware::isResetRequested()
{
  pinMode(Pin::ResetWakeup, INPUT_PULLDOWN);
  if (digitalRead(Pin::ResetWakeup) != HIGH)
  {
    return false;
  }

  pinMode(Pin::Led, OUTPUT);
  const unsigned long pressStartTime = millis();
  const unsigned long requiredWaitingTime = Timing::ResetRequestTimeoutSec * 1000;
  unsigned long lastToggle = pressStartTime;
  bool ledState = false;
  bool thresholdReached = false;

  while (digitalRead(Pin::ResetWakeup) == HIGH)
  {
    unsigned long now = millis();
    if (now - pressStartTime >= requiredWaitingTime)
    {
      // Hold threshold reached — keep LED solid ON, wait for release
      if (!thresholdReached)
      {
        thresholdReached = true;
        digitalWrite(Pin::Led, HIGH);
      }
    }
    else if (now - lastToggle >= 1000)
    {
      lastToggle = now;
      ledState = !ledState;
      digitalWrite(Pin::Led, ledState ? HIGH : LOW);
    }
  }

  // Button released
  digitalWrite(Pin::Led, LOW);
  return thresholdReached;
}

void Hardware::resetDevice(ConfigStore& configStore)
{
  configStore.clear();
  delay(1000); // Allow preferences to flush to NVS before restart
  ESP.restart();
}


