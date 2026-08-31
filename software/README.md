# izBoard Software

The server component of the izBoard system. An ASP.NET Core web application with an Angular frontend that renders dashboards into images optimized for E-Paper displays. Integrates with Home Assistant and provides a visual dashboard designer, device management, and scheduled updates.

## Features

- Visual dashboard designer with multiple widget types
- AI-powered dashboard generation — describe your ideal layout in natural language and have AI create it using your Home Assistant entities, calendars, weather, and more
- Home Assistant integration
- E-Paper optimized image processing
- Device management and pairing
- Configurable update schedules
- OTA firmware delivery to devices
- Multi-user authentication (standalone mode)
- Deployable as standalone Docker or Home Assistant Add-on

## Deployment

### Docker Compose

```yaml
services:
  app:
    image: izdevdigital/e-paper-dashboard:<tag>
    ports:
      - "<port>:8128"   # Web UI and API
      - "<port>:8129"   # Device communication
    volumes:
      - <host-path>/data:/data:rw
    environment:
      - CLIENT_URL=http://dashboard.local:<port>
      - STATE_SIGNING_KEY=<random-secret>
      - SUPERUSER_USERNAME=<admin-username>
      - SUPERUSER_PASSWORD=<admin-password>
      - TZ=<time-zone>
```

### Home Assistant Add-on

Install via the [izBoard Home Assistant Add-on repository](https://github.com/izdev-digital/hass-add-ons/tree/master/e-paper-dashboard). In add-on mode, authentication is handled through Home Assistant ingress and the server auto-connects to Home Assistant via the Supervisor API. Configure `CLIENT_URL` as the LAN device endpoint, normally `http://homeassistant.local:8129`; do not use an ingress or cloud URL.

### Environment Variables

| Variable | Required | Description |
|---|---|---|
| `CLIENT_URL` | Yes | URL entered during device setup. It must be reachable from the display network. In standalone/host mode it is also the Home Assistant OAuth client URL; in add-on mode OAuth continues to use ingress and this value is the LAN device endpoint. Paths, custom ports, DNS names, and IPv4 addresses are supported by current firmware. |
| `STATE_SIGNING_KEY` | Yes (standalone) | Random secret for signing auth state |
| `SUPERUSER_USERNAME` | Yes (standalone) | Initial superuser account username |
| `SUPERUSER_PASSWORD` | Yes (standalone) | Initial superuser account password |
| `TZ` | Recommended | Timezone (e.g. `Europe/London`) |
| `APP_MODE` | No | Deployment mode: `standalone` (default) or `addon` |
| `HOMEASSISTANT_HOST` | No | Home Assistant URL (auto-detected in add-on mode) |

### Ports

| Port | Purpose |
|---|---|
| `8128` | Web UI and API |
| `8129` | Device communication (pairing, firmware image retrieval, and configuration) |

`CLIENT_URL` must describe the externally reachable URL after port mappings or a reverse proxy are applied. If displays are isolated on a VLAN, allow them to reach this URL. In standalone/host mode, expose the same URL to both the browser OAuth callback and the display API (normally through port `8128` or a reverse proxy). In add-on mode, use the exposed device port (`8129` by default), not the browser-only ingress URL.

### Data

Application data (database, uploaded images, firmware cache) is stored in `/data`. Mount this path as a persistent volume.

## Building from Source

### Single Architecture Build

```shell
# <arch> can be: amd64 or arm64
docker build --platform linux/<arch> --build-arg VERSION=<version> -t izdevdigital/e-paper-dashboard:<version>-<arch> -f EPaperDashboard/Dockerfile .
```

### Multi-Architecture Build

Build and push for all supported platforms (requires docker buildx):

```shell
# Create a new builder instance (one-time setup)
docker buildx create --name multiarch --use

# Build and push multi-arch image (amd64 + arm64)
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --build-arg VERSION=<version> \
  -t izdevdigital/e-paper-dashboard:<version> \
  -t izdevdigital/e-paper-dashboard:latest \
  -f EPaperDashboard/Dockerfile \
  --push .
```
