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
- Optional Home Assistant dashboard screenshots through an on-demand rendering component

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

Install via the [izBoard Home Assistant Add-on repository](https://github.com/izdev-digital/hass-add-ons/tree/master/e-paper-dashboard). Configure `CLIENT_URL` as a device-reachable endpoint, normally `http://homeassistant.local:8129`. Remote URLs work through a direct port, VPN, or reverse proxy; ingress URLs do not.

### Environment Variables

| Variable | Required | Description |
|---|---|---|
| `CLIENT_URL` | Yes | Absolute HTTP or HTTPS URL reachable from the display network. In standalone/host mode it is also the Home Assistant OAuth client URL; in add-on mode it is the direct device endpoint. |
| `STATE_SIGNING_KEY` | Yes (standalone) | Random secret for signing auth state |
| `SUPERUSER_USERNAME` | Yes (standalone) | Initial superuser account username |
| `SUPERUSER_PASSWORD` | Yes (standalone) | Initial superuser account password |
| `TZ` | Recommended | Timezone (e.g. `Europe/London`) |
| `APP_MODE` | No | Deployment mode: `standalone` (default) or `addon` |
| `HOMEASSISTANT_HOST` | No | Home Assistant URL (auto-detected in add-on mode) |
| `RENDERING_COMPONENT_REPOSITORY` | No | GitHub repository that publishes the optional rendering component (default: `izdev-digital/e-paper-dashboard`) |
| `RENDERING_COMPONENT_RELEASE_TAG` | No | Override the component release tag; normally set by the official image |
| `RENDERING_COMPONENT_BASE_URL` | No | Direct component artifact source for local or CI deployment testing; bypasses GitHub release lookup |

### Ports

| Port | Purpose |
|---|---|
| `8128` | Web UI and API |
| `8129` | Device communication (pairing, firmware image retrieval, and configuration) |

`CLIENT_URL` must describe the externally reachable URL after port mappings or a reverse proxy are applied. If displays are isolated on a VLAN, allow them to reach this URL. In standalone/host mode, expose the same URL to both the browser OAuth callback and the display API (normally through port `8128` or a reverse proxy). In add-on mode, use the exposed device port (`8129` by default), not the browser-only ingress URL.

For HTTPS, the display verifies the server certificate against its embedded trusted root bundle and must be able to obtain network time on its first connection. Deployments using a private certificate authority must add that root to the firmware bundle before flashing.

### Data

Application data (database, uploaded images, firmware cache) is stored in `/data`. Mount this path as a persistent volume.

### Optional rendering component

The default image does not include the browser-rendering runtime or browser binaries. Custom layouts work without them.
To render an existing Home Assistant dashboard, sign in as a superuser, open **System**, and select **Install component**.
izBoard downloads the version-matched component for the current CPU architecture from the project's GitHub release, verifies its
SHA-256 checksum, and activates it immediately. The component is stored in the persistent `/data` volume, so it survives image
updates when `/data` is mounted as documented above. If izBoard is updated while the component is installed, izBoard automatically
updates the component to a compatible version as well. Removing the component deletes all of its files from `/data`.

Before publishing a release, CI serves the newly built component archives from an isolated local HTTP container, installs them
through the application API into a freshly built slim image, runs a browser rendering test, and removes the component again.
Developers can use the same path by setting `RENDERING_COMPONENT_BASE_URL` to a directory served over HTTP that contains the
architecture-specific `.tar.gz` archive and its `.sha256` file.

### Local component deployment test

Docker Compose can build the slim application and an unpublished rendering component for the host architecture, serve the
component inside the Compose network, and configure the application to install from that local source:

```shell
cd software
docker compose -f docker-compose.yml -f docker-compose.rendering.yml up --build app
```

Open `http://localhost:8128`, sign in with the development credentials from `docker-compose.yml`, then open **System** and select
**Install component** followed by **Test component**. Set `IZBOARD_VERSION` to override the default test version; the application
and component builds always receive the same value.

Stop the stack and remove its isolated test data and generated component artifacts with:

```shell
docker compose -f docker-compose.yml -f docker-compose.rendering.yml down --volumes
```

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
