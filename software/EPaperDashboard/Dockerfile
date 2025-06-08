#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/playwright/dotnet:v1.51.0-noble AS base
RUN apt-get update && apt-get install -y aspnetcore-runtime-8.0
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["EPaperDashboard/EPaperDashboard.csproj", "EPaperDashboard/"]
RUN dotnet restore "./EPaperDashboard/./EPaperDashboard.csproj"
COPY . .
WORKDIR "/src/EPaperDashboard"
RUN dotnet build "./EPaperDashboard.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./EPaperDashboard.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
RUN chmod +x /app/.playwright/node/*/node
USER app
ENTRYPOINT ["dotnet", "EPaperDashboard.dll"]