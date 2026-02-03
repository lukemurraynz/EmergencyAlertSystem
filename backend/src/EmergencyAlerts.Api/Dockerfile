# Multi-stage Dockerfile for Emergency Alerts API (.NET 10 ASP.NET Core)

# OCI Image Labels (standardized container metadata)
ARG BUILD_DATE
ARG VCS_REF
ARG VERSION=1.0.0

# Stage 1: Build dependencies
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS deps
WORKDIR /src

# Copy project files for dependency restoration
COPY src/EmergencyAlerts.Domain/EmergencyAlerts.Domain.csproj ./EmergencyAlerts.Domain/
COPY src/EmergencyAlerts.Application/EmergencyAlerts.Application.csproj ./EmergencyAlerts.Application/
COPY src/EmergencyAlerts.Infrastructure/EmergencyAlerts.Infrastructure.csproj ./EmergencyAlerts.Infrastructure/
COPY src/EmergencyAlerts.Api/EmergencyAlerts.Api.csproj ./EmergencyAlerts.Api/

# Restore dependencies (this layer is cached until .csproj files change)
RUN dotnet restore EmergencyAlerts.Api/EmergencyAlerts.Api.csproj

# Stage 2: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy dependency cache from previous stage for faster restoration
COPY --from=deps /root/.nuget /root/.nuget

# Copy project files first for dependency restoration
COPY src/EmergencyAlerts.Domain/EmergencyAlerts.Domain.csproj ./EmergencyAlerts.Domain/
COPY src/EmergencyAlerts.Application/EmergencyAlerts.Application.csproj ./EmergencyAlerts.Application/
COPY src/EmergencyAlerts.Infrastructure/EmergencyAlerts.Infrastructure.csproj ./EmergencyAlerts.Infrastructure/
COPY src/EmergencyAlerts.Api/EmergencyAlerts.Api.csproj ./EmergencyAlerts.Api/

# Restore dependencies (uses cached packages from deps stage)
RUN dotnet restore EmergencyAlerts.Api/EmergencyAlerts.Api.csproj

# Copy all source code
COPY src/ .

# Build in Release mode (allow implicit restore to catchmissing packages)
RUN dotnet build EmergencyAlerts.Api/EmergencyAlerts.Api.csproj -c Release

# Stage 3: Publish
FROM build AS publish
WORKDIR /src

# Publish to output directory
RUN dotnet publish EmergencyAlerts.Api/EmergencyAlerts.Api.csproj -c Release -o /app/publish --no-build

# Stage 4: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# OCI Labels for container registry metadata
LABEL org.opencontainers.image.title="Emergency Alerts API" \
    org.opencontainers.image.description="Emergency Alert Core System - Backend API Service" \
    org.opencontainers.image.vendor="Emergency Alerts Team" \
    org.opencontainers.image.source="https://github.com/emergency-alerts/probable-octo-rotary-phone" \
    org.opencontainers.image.documentation="https://github.com/emergency-alerts/probable-octo-rotary-phone/blob/main/README.md" \
    org.opencontainers.image.licenses="MIT" \
    org.opencontainers.image.base.name="mcr.microsoft.com/dotnet/aspnet:10.0-alpine"

# Install curl for health checks (minimal impact on Alpine)
RUN apk add --no-cache curl

# Copy published application
COPY --from=publish /app/publish .

# Create non-root user for security
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
RUN chown -R appuser:appgroup /app
USER appuser

# Expose ASP.NET Core port
EXPOSE 5000

# Health check for container orchestration
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:5000/health/live || exit 1

# Configure ASP.NET Core to listen on port 5000
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Start application
ENTRYPOINT ["dotnet", "EmergencyAlerts.Api.dll"]
