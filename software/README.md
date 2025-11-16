# Create Docker image:
- Change directory to software
- Run the following command:
    ```shell
    docker build --platform linux/amd64 --build-arg VERSION=<version> -t izdevdigital/e-paper-dashboard:<version> -t izdevdigital/e-paper-dashboard:latest -f EPaperDashboard/Dockerfile .
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
