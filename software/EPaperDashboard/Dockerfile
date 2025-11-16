#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
RUN apt-get update && apt-get install -y \
    libasound2 \
    libglib2.0-0 \
    libnss3 \
    libnspr4 \
    libdbus-1-3 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
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
    && rm -rf /var/lib/apt/lists/*


FROM mcr.microsoft.com/dotnet/sdk:8.0 AS publish
ARG BUILD_CONFIGURATION=Release
ARG VERSION=1.0.0
WORKDIR /src
COPY . .
WORKDIR /src/EPaperDashboard
RUN dotnet restore && dotnet publish -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:Version=$VERSION
RUN dotnet tool install --global Microsoft.Playwright.CLI
ENV PATH="$PATH:/root/.dotnet/tools"
RUN playwright install chromium

FROM base AS final
WORKDIR /app
EXPOSE 8128
COPY --from=publish /app/publish .
COPY --from=publish /root/.cache/ms-playwright /home/app/.cache/ms-playwright
RUN chmod +x /app/.playwright/node/*/node && mkdir -p /app/config && chown app:app /app/config && chmod 770 /app/config
USER app

ENTRYPOINT ["dotnet", "EPaperDashboard.dll"]