FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble AS base

USER root
WORKDIR /app

ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

RUN mkdir -p /app/uploads/.tmp \
    && chown -R ${APP_UID}:${APP_UID} /app/uploads

EXPOSE 8080
EXPOSE 8081

USER $APP_UID


FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["API-DMS/API-DMS.csproj", "API-DMS/"]
COPY ["Shared.Reporting/Shared.Reporting.csproj", "Shared.Reporting/"]

RUN dotnet restore "API-DMS/API-DMS.csproj"

COPY . .

WORKDIR /src/API-DMS

RUN dotnet build "API-DMS.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    --no-restore


FROM build AS publish

ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "API-DMS.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false


FROM base AS final

USER root
WORKDIR /app

COPY --from=publish /app/publish .

RUN dotnet API-DMS.dll --install-pdf-browser \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        fonts-dejavu-core \
        fontconfig \
    && fc-cache -f \
    && find /app/.playwright /ms-playwright \
        -type f \
        \( \
            -name node \
            -o -name chrome \
            -o -name chrome-headless-shell \
        \) \
        -exec chmod a+rx {} + \
    && chmod -R a+rX /ms-playwright \
    && rm -rf /var/lib/apt/lists/*

COPY API-DMS/docker-entrypoint.sh /usr/local/bin/docker-entrypoint
RUN chmod 755 /usr/local/bin/docker-entrypoint

ENTRYPOINT ["/usr/local/bin/docker-entrypoint"]
