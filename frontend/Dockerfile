# Multi-stage Dockerfile for Emergency Alerts Frontend (React 19 + Vite)

# OCI Image Labels (standardized container metadata)
ARG BUILD_DATE
ARG VCS_REF
ARG VERSION=1.0.0

# Stage 1: Dependencies
FROM node:24-alpine AS deps
WORKDIR /app

# Copy package files
COPY package*.json ./

# Install production and development dependencies
RUN npm ci

# Stage 2: Build
FROM node:24-alpine AS build
WORKDIR /app

# Copy dependency cache from previous stage
COPY --from=deps /app/node_modules ./node_modules

# Copy application source
COPY . .

# Build production bundle (Vite outputs to dist/)
# VITE_API_URL: Empty string = relative paths (for K8s ingress where frontend/backend share host)
# Override with --build-arg VITE_API_URL=https://api.example.com for absolute URLs
ARG VITE_API_URL=
ENV VITE_API_URL=$VITE_API_URL
ARG VITE_SIGNALR_URL=
ENV VITE_SIGNALR_URL=$VITE_SIGNALR_URL
RUN chmod -R +x /app/node_modules/.bin
RUN npm run build

# Stage 3: Test (optional - can be skipped in production pipeline)
FROM build AS test
WORKDIR /app
RUN npm run lint && npm test -- --run

# Stage 4: Production runtime
FROM node:24-alpine AS production
WORKDIR /app

# OCI Labels for container registry metadata
LABEL org.opencontainers.image.title="Emergency Alerts Frontend" \
    org.opencontainers.image.description="Emergency Alert Core System - React Frontend Application" \
    org.opencontainers.image.vendor="Emergency Alerts Team" \
    org.opencontainers.image.source="https://github.com/emergency-alerts/probable-octo-rotary-phone" \
    org.opencontainers.image.documentation="https://github.com/emergency-alerts/probable-octo-rotary-phone/blob/main/frontend/README.md" \
    org.opencontainers.image.licenses="MIT" \
    org.opencontainers.image.base.name="node:24-alpine"

# Install lightweight HTTP server for serving static files
RUN npm install -g serve

# Copy built application from build stage
COPY --from=build /app/dist ./dist

# Create non-root user for security
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
RUN chown -R appuser:appgroup /app
USER appuser

# Expose port for HTTP server
EXPOSE 3000

# Health check for container orchestration
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD wget -q -O- http://localhost:3000/index.html || exit 1

# Serve static files on port 3000
CMD ["serve", "-s", "dist", "-l", "3000"]
