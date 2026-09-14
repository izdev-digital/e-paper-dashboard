# Trusted certificate bundle

`x509_crt_bundle.bin` is generated from the Mozilla root certificate set distributed by Espressif IDF 5.2.

To replace it, run Espressif's `gen_crt_bundle.py` against the required public or private root certificates, name the output `x509_crt_bundle.bin`, and rebuild the firmware. The file is embedded by `platformio.ini` and used for HTTPS server authentication.

Sources:

- https://github.com/espressif/esp-idf/tree/v5.2/components/mbedtls/esp_crt_bundle
- https://docs.espressif.com/projects/esp-idf/en/v5.2/esp32/api-reference/protocols/esp_crt_bundle.html
