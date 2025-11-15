# Create Docker image:
- Change directory to software
- Run the following command:
    ```shell
    docker build --platform linux/amd64 -t e-paper-dashboard:<version> -f EPaperDashboard/Dockerfile .
    ```

# Use docker-compose
```yaml
services:
  app:
    image: izdevdigital/e-paper-dashboard:<tag>
    ports:
      - "<port>:8080"
    volumes:
      - <host-path>/config:/app/config:rw
    environment:
      - ASPNETCORE_HTTP_PORTS=8080
      - CLIENT_URL=<url>:<port>
      - TZ=<time-zone>
```
