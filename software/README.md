# E-Paper Dashboard Software

The server component of the E-Paper Dashboard system. This ASP.NET Core web application renders web-based dashboards into images processed for E-Paper display characteristics. Currently works with Home Assistant dashboards and can be extended to support any other URLs.

## Features

- **Web-based dashboard rendering**: Uses Playwright headless browser to capture dashboard content
- **E-Paper image processing**: Applies color quantization and dithering for E-Paper displays
- **Preview functionality**: View how the rendered dashboard will appear on the E-Paper display
- **REST API**: Provides endpoints for devices to retrieve processed images
- **Schedule management**: Configure when devices should poll for updates
- **Multi-dashboard support**: Manage multiple dashboards and devices
- **User authentication**: Secure access to dashboard configuration

## Deployment

### Docker Compose
```yaml
services:
  app:
    image: izdevdigital/e-paper-dashboard:<tag>
    # user: "${UID}:${GID}" set to match host user
    ports:
      - "<port>:8128"
    volumes:
      - <host-path>/config:/app/config:rw
      - dataprotection:/home/app/.aspnet/DataProtection-Keys
    environment:
      - CLIENT_URL=<url>:<port>
      - TZ=<time-zone>

volumes:
  dataprotection:
```

## Building from Source

### Create Docker image:
- Change directory to software
- Run the following command:

## Single Architecture Build
Build for a specific platform:
```shell
# <arch> can be: amd64, arm64, or arm/v7
docker build --platform linux/<arch> --build-arg VERSION=<version> -t izdevdigital/e-paper-dashboard:<version>-<arch> -f EPaperDashboard/Dockerfile .
```

## Multi-Architecture Build
Build and push for all supported platforms (requires docker buildx):
```shell
# Create a new builder instance (one-time setup)
docker buildx create --name multiarch --use

# Build and push multi-arch image (amd64 + arm64 + arm/v7)
docker buildx build \
  --platform linux/amd64,linux/arm64,linux/arm/v7 \
  --build-arg VERSION=<version> \
  -t izdevdigital/e-paper-dashboard:<version> \
  -t izdevdigital/e-paper-dashboard:latest \
  -f EPaperDashboard/Dockerfile \
  --push .
```
