#pragma once

#include <Arduino.h>

class Logger {
public:
  void begin(unsigned long baud) {
    Serial.begin(baud);
  }

  template<typename T>
  void print(const T& value) {
    Serial.print(value);
  }

  template<typename T>
  void println(const T& value) {
    Serial.println(value);
  }

  void println() {
    Serial.println();
  }

  template<typename... Args>
  void printf(const char* format, Args... args) {
    Serial.printf(format, args...);
  }
};
