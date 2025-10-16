# Use the official .NET 8 runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8585

# Use the official .NET 8 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["ScraperBackendService/ScraperBackendService.csproj", "ScraperBackendService/"]
RUN dotnet restore "ScraperBackendService/ScraperBackendService.csproj"

# Copy source code
COPY . .
WORKDIR "/src/ScraperBackendService"

# Build and publish
RUN dotnet build "ScraperBackendService.csproj" -c Release -o /app/build
RUN dotnet publish "ScraperBackendService.csproj" -c Release -o /app/publish --no-restore

# Final stage
FROM base AS final
WORKDIR /app

# Install system dependencies for Playwright
RUN apt-get update && apt-get install -y \
    # Basic system tools
    wget \
    gnupg \
    ca-certificates \
    # Playwright browser dependencies
    libnss3 \
    libnspr4 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libxss1 \
    libxcomposite1 \
    libxdamage1 \
    libxrandr2 \
    libgbm1 \
    libxkbcommon0 \
    libgtk-3-0 \
    libgdk-pixbuf2.0-0 \
    libasound2 \
    # Font support
    fonts-liberation \
    fonts-noto-color-emoji \
    # Additional dependencies
    xdg-utils \
    && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=build /app/publish .

# Install Playwright CLI and browsers
RUN dotnet tool install --global Microsoft.Playwright.CLI --version 1.55.0
ENV PATH="$PATH:/root/.dotnet/tools"

# Install Playwright browsers (Chromium only for efficiency)
RUN playwright install chromium --with-deps

# Verify Playwright installation
RUN playwright --version

# Create non-root user for security (after Playwright installation)
RUN addgroup --system --gid 1001 appuser \
    && adduser --system --uid 1001 --gid 1001 --home /app --shell /bin/false appuser

# Set proper permissions for Playwright cache
RUN mkdir -p /app/.cache/ms-playwright \
    && chown -R appuser:appuser /app \
    && chown -R appuser:appuser /root/.cache/ms-playwright 2>/dev/null || true

# Switch to non-root user
USER appuser

# Set Playwright environment variables
ENV PLAYWRIGHT_BROWSERS_PATH=/root/.cache/ms-playwright
ENV PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1

ENTRYPOINT ["dotnet", "ScraperBackendService.dll"]