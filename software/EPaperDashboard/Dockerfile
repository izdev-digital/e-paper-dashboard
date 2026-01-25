# Multi-architecture support: linux/amd64, linux/arm64

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
LABEL org.opencontainers.image.licenses="Apache-2.0" \
  org.opencontainers.image.source="https://github.com/izdev-digital/e-paper-dashboard" \
  org.opencontainers.image.title="izBoard" \
  org.opencontainers.image.description="Application for managing the dashboards for e-paper display"
RUN apt-get update && apt-get install -y --no-install-recommends \
  ca-certificates \
  curl \
  wget \
  fonts-liberation \
  libasound2 \
  libgtk-3-0 \
  libgbm1 \
  libx11-6 \
  libx11-xcb1 \
  libxcb1 \
  libxcomposite1 \
  libxdamage1 \
  libxext6 \
  libxfixes3 \
  libxrandr2 \
  libxshmfence1 \
  libxss1 \
  libxtst6 \
  libnss3 \
  libnspr4 \
  libdbus-1-3 \
  libcups2 \
  && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /home/app/.aspnet/DataProtection-Keys \
  && chown -R app:app /home/app/.aspnet/DataProtection-Keys \
  && chmod -R 700 /home/app/.aspnet/DataProtection-Keys
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS playwright

ENV DEBIAN_FRONTEND=noninteractive
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

RUN curl -fsSL https://deb.nodesource.com/setup_22.x | bash - && \
  apt-get install -y --no-install-recommends nodejs ca-certificates curl wget && \
  rm -rf /var/lib/apt/lists/*

RUN npm install -g playwright@1.52.0 && \
  PLAYWRIGHT_BROWSERS_PATH=/ms-playwright playwright install chromium

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS publish
ARG TARGETARCH
ARG BUILD_CONFIGURATION=Release
ARG VERSION=1.0.0

# Install Node.js for Angular build
RUN curl -fsSL https://deb.nodesource.com/setup_22.x | bash - && \
  apt-get install -y nodejs && \
  npm install -g npm@latest

WORKDIR /src
COPY . .
WORKDIR /src/EPaperDashboard

# Map Docker build args to .NET architecture and Runtime Identifier (RID)
RUN case "$TARGETARCH" in \
  amd64) RID=linux-x64 ;; \
  arm64) RID=linux-arm64 ;; \
  arm*) RID=linux-arm ;; \
  *) RID=linux-$TARGETARCH ;; \
  esac && \
  dotnet restore && \
  dotnet build -c $BUILD_CONFIGURATION && \
  dotnet publish -c $BUILD_CONFIGURATION -o /app/publish -r $RID \
  /p:UseAppHost=false \
  /p:Version=$VERSION \
  /p:DebugType=none \
  /p:DebugSymbols=false

# Copy license files to publish directory
RUN cp /src/LICENSE /app/publish/ && \
  cp /src/NOTICE /app/publish/ && \
  cp /src/THIRD-PARTY-NOTICES.txt /app/publish/

FROM base AS final
WORKDIR /app
EXPOSE 8128
COPY --from=publish /app/publish .
COPY --from=playwright /ms-playwright/ /home/app/.cache/ms-playwright/
ENV PLAYWRIGHT_BROWSERS_PATH=/home/app/.cache/ms-playwright
RUN chmod +x /app/.playwright/node/*/node
RUN mkdir -p /app/config && chown app:app /app/config && chmod 770 /app/config
USER app

ENTRYPOINT ["dotnet", "EPaperDashboard.dll"]