#
# Switchboard Dashboard
# Multi-stage build: Node.js build + nginx serve
#

#
# Build stage
#
FROM node:20-alpine AS build
WORKDIR /app

# Copy package files
COPY dashboard/package*.json ./

# Install dependencies
RUN npm ci

# Copy source
COPY dashboard/ .

# Build the application
RUN npm run build


#
# Production stage
#
FROM nginx:alpine AS production

# Copy custom nginx config
COPY Docker/dashboard/nginx.conf /etc/nginx/conf.d/default.conf

# Copy built assets from build stage
COPY --from=build /app/dist /usr/share/nginx/html

# Expose port
EXPOSE 80

# Start nginx
CMD ["nginx", "-g", "daemon off;"]
