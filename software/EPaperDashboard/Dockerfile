#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/playwright/dotnet:v1.52.0-noble AS base
RUN apt-get update && apt-get install -y aspnetcore-runtime-8.0

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS publish
ARG BUILD_CONFIGURATION=Release
ARG VERSION=1.0.0
WORKDIR /src
COPY ["EPaperDashboard/EPaperDashboard.csproj", "EPaperDashboard/"]
RUN dotnet restore "./EPaperDashboard/./EPaperDashboard.csproj"
COPY . .
WORKDIR /src/EPaperDashboard
RUN dotnet publish "./EPaperDashboard.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:Version=$VERSION

FROM base AS final
WORKDIR /app
EXPOSE 8128
COPY --from=publish /app/publish .
RUN chmod +x /app/.playwright/node/*/node && mkdir -p /app/config && chown app:app /app/config && chmod 770 /app/config
USER app

ENTRYPOINT ["dotnet", "EPaperDashboard.dll"]