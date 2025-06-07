# Create Docker image:
- Change directory to software
- Run the following command:
    ```shell
    docker build --platform linux/amd64 -t e-paper-dashboard:<version> -f EPaperDashboard/Dockerfile .
    ```

# Use docker-compose
```yaml
version: '3.8'

services:
  pixashot:
    image: gpriday/pixashot:latest
    environment:
      - WORKERS=4
      - USE_POPUP_BLOCKER=true
      - USE_COOKIE_BLOCKER=true
    
  dashboard:
    image: epaperdisplay:dev-0.0.1
    ports:
      - "8128:8080"
    environment:
      - RENDERER_URL=http://pixashot:8080
      - DASHBOARD_URL=<dashboard-url>
```
