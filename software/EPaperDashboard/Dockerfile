# Multi-architecture support: linux/amd64, linux/arm64

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
LABEL org.opencontainers.image.licenses="Apache-2.0" \
      org.opencontainers.image.source="https://github.com/izdev-digital/e-paper-dashboard" \
      org.opencontainers.image.title="E-Paper Dashboard" \
      org.opencontainers.image.description="Application for managing the dashboards for e-paper display"
RUN apt-get update && apt-get install -y \
  libasound2 \
  libglib2.0-0 \
  libnss3 \
  libnspr4 \
  libdbus-1-3 \
  libatk1.0-0 \
  libatk-bridge2.0-0 \
  libcups2 \
  libexpat1 \
  libatspi2.0-0 \
  libx11-6 \
  libxcomposite1 \
  libxdamage1 \
  libxext6 \
  libxfixes3 \
  libxrandr2 \
  libgbm1 \
  libxcb1 \
  libxkbcommon0 \
  libpango-1.0-0 \
  libcairo2 \
  && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /home/app/.aspnet/DataProtection-Keys \
  && chown -R app:app /home/app/.aspnet/DataProtection-Keys \
  && chmod -R 700 /home/app/.aspnet/DataProtection-Keys
VOLUME ["/home/app/.aspnet/DataProtection-Keys"]
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS publish
ARG TARGETARCH
ARG BUILD_CONFIGURATION=Release
ARG VERSION=1.0.0
WORKDIR /src
COPY . .
WORKDIR /src/EPaperDashboard


# Map Docker build args to .NET architecture and Runtime Identifier (RID)
# TARGETARCH is set by buildkit; TARGETVARIANT may be used for specific variants.
RUN case "$TARGETARCH" in \
      amd64) RID=linux-x64 ;; \
      arm64) RID=linux-arm64 ;; \
      arm*) RID=linux-arm ;; \
      *) RID=linux-$TARGETARCH ;; \
    esac && \
    dotnet restore && \
    dotnet build -c $BUILD_CONFIGURATION && \
    dotnet tool install --global Microsoft.Playwright.CLI && \
    /root/.dotnet/tools/playwright install chromium && \
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
COPY --from=publish /root/.cache/ms-playwright /home/app/.cache/ms-playwright
RUN chmod +x /app/.playwright/node/*/node
RUN mkdir -p /app/config && chown app:app /app/config && chmod 770 /app/config
VOLUME ["/app/config"]
USER app

ENTRYPOINT ["dotnet", "EPaperDashboard.dll"]