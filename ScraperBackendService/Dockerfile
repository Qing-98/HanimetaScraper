FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS build
WORKDIR /src

COPY ScraperBackendService/ScraperBackendService.csproj ./
RUN dotnet restore ScraperBackendService.csproj

COPY ScraperBackendService/ ./
RUN dotnet publish ScraperBackendService.csproj -c Release \
    --no-restore -p:UseAppHost=false -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim AS final
WORKDIR /app
EXPOSE 8585

ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright \
    ServiceConfig__PreferSystemChromeForHeadless=false \
    ServiceConfig__EnableManualChallengeResolution=false

COPY --from=build /app/publish/ ./
COPY ScraperBackendService/Browser/StealthInit.js ./Browser/StealthInit.js

# Use the CLI and Node included in the published Playwright package.
RUN set -eux; \
    case "$(dpkg --print-architecture)" in \
        amd64) node_arch=linux-x64 ;; \
        arm64) node_arch=linux-arm64 ;; \
        *) echo "Unsupported Playwright container architecture" >&2; exit 1 ;; \
    esac; \
    "/app/.playwright/node/$node_arch/node" /app/.playwright/package/cli.js \
        install --with-deps chromium; \
    rm -rf /var/lib/apt/lists/*; \
    groupadd --gid 1001 appuser; \
    useradd --uid 1001 --gid 1001 --create-home appuser; \
    mkdir -p /app/Data/Cookies; \
    chown -R appuser:appuser /app/Data; \
    chmod -R a+rX /app/.playwright /ms-playwright

ENV HOME=/home/appuser
USER appuser

ENTRYPOINT ["dotnet", "ScraperBackendService.dll"]
