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

  const unsigned long pressStartTime = millis();
  const unsigned long requiredWaitingTime = Timing::ResetRequestTimeoutSec * 1000;
  while (digitalRead(Pin::ResetWakeup) == HIGH)
  {
    if (millis() - pressStartTime >= requiredWaitingTime)
    {
      return true;
    }
  }

  return false;
}

void Hardware::resetDevice(ConfigStore& configStore)
{
  configStore.clear();
  ESP.restart();
}
