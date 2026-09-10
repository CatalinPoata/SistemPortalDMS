# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER root
RUN mkdir -p /app/uploads/.tmp /ms-playwright \
    && chown -R ${APP_UID}:${APP_UID} /app/uploads
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["API-PORTAL/API-PORTAL.csproj", "API-PORTAL/"]
COPY ["Shared.Reporting/Shared.Reporting.csproj", "Shared.Reporting/"]
RUN dotnet restore "./API-PORTAL/API-PORTAL.csproj"
COPY . .
WORKDIR "/src/API-PORTAL"
RUN dotnet build "./API-PORTAL.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./API-PORTAL.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
USER root
WORKDIR /app
COPY --from=publish /app/publish .

RUN dotnet API-PORTAL.dll --install-pdf-browser \
    && apt-get update \
    && apt-get install -y --no-install-recommends fonts-dejavu-core fontconfig \
    && fc-cache -f \
    && find /app/.playwright /ms-playwright -type f \
        \( -name node -o -name chrome -o -name chrome-headless-shell \) \
        -exec chmod a+rx {} + \
    && chmod -R a+rX /ms-playwright \
    && rm -rf /var/lib/apt/lists/*

COPY API-PORTAL/docker-entrypoint.sh /usr/local/bin/docker-entrypoint
RUN chmod 755 /usr/local/bin/docker-entrypoint

ENTRYPOINT ["/usr/local/bin/docker-entrypoint"]
