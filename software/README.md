# Create Docker image:
- Change directory to software
- Run the following command:

## Single Architecture Build
Build for a specific platform:
```shell
# <arch> can be: amd64 or arm64
docker build --platform linux/<arch> --build-arg VERSION=<version> -t izdevdigital/e-paper-dashboard:<version>-<arch> -f EPaperDashboard/Dockerfile .
```

## Multi-Architecture Build
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

# Use docker compose
```yaml
services:
  app:
    image: izdevdigital/e-paper-dashboard:<tag>
    # user: "${UID}:${GID}" set to match host user
    ports:
      - "<port>:8128"
    volumes:
      - <host-path>/config:/app/config:rw
    environment:
      - CLIENT_URL=<url>:<port>
      - TZ=<time-zone>
    volumes:
      - dataprotection:/home/app/.aspnet/DataProtection-Keys

volumes:
  dataprotection:
```
