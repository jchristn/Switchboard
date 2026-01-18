#
# Switchboard Dashboard
# Multi-stage build: Node.js build + nginx serve
#
# Build from the dashboard directory:
#   docker build -t jchristn77/switchboard-ui:v4.0.2 .
#

#
# Build stage
#
FROM node:20-alpine AS build
WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm ci

# Copy source
COPY . .

# Build the application
RUN npm run build


#
# Production stage
#
FROM nginx:alpine AS production

# Install network troubleshooting tools and useful utilities
RUN apk add --no-cache \
    curl \
    wget \
    bind-tools \
    iputils \
    net-tools \
    busybox-extras \
    vim \
    jq \
    openssl \
    ca-certificates

# Copy custom nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy built assets from build stage
COPY --from=build /app/dist /usr/share/nginx/html

# Expose port
EXPOSE 80

# Health check
HEALTHCHECK --interval=10s --timeout=5s --start-period=5s --retries=2 \
    CMD curl -f http://localhost/ || exit 1

# Start nginx
CMD ["nginx", "-g", "daemon off;"]
