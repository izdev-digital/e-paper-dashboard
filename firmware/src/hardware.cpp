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

void Hardware::startResetMonitor(ConfigStore& configStore)
{
  static ConfigStore* pConfigStore = &configStore;
  xTaskCreatePinnedToCore(
      resetMonitorTask,
      "resetMonitor",
      4096,
      pConfigStore,
      1,       // low priority — just polls the button
      nullptr,
      0        // run on core 0 so it stays responsive even if core 1 is busy
  );
}

void Hardware::resetMonitorTask(void* param)
{
  auto* configStore = static_cast<ConfigStore*>(param);
  pinMode(Pin::ResetWakeup, INPUT_PULLDOWN);
  pinMode(Pin::Led, OUTPUT);

  const unsigned long requiredHoldMs = Timing::ResetRequestTimeoutSec * 1000;

  unsigned long pressStart = 0;
  bool pressing = false;

  unsigned long lastToggle = 0;
  bool ledState = false;
  bool resetReady = false;

  for (;;)
  {
    if (digitalRead(Pin::ResetWakeup) == HIGH)
    {
      unsigned long now = millis();
      if (!pressing)
      {
        pressing = true;
        resetReady = false;
        pressStart = now;
        lastToggle = now;
        ledState = false;
      }
      if (now - pressStart >= requiredHoldMs)
      {
        resetReady = true;
        // Keep LED solid ON to indicate reset is armed
        if (!ledState)
        {
          ledState = true;
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
    else
    {
      if (pressing && resetReady)
      {
        // Button released after holding long enough — perform reset
        digitalWrite(Pin::Led, LOW);
        configStore->clear();
        vTaskDelay(pdMS_TO_TICKS(1000));
        ESP.restart();
      }
      if (pressing)
      {
        digitalWrite(Pin::Led, LOW);
      }
      pressing = false;
      resetReady = false;
    }

    vTaskDelay(pdMS_TO_TICKS(50));
  }
}
