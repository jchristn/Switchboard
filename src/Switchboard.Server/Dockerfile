#
#
# Run the docker build command from src, not from the project directory
#
#


#
#
# Build stage
#
#
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy source
COPY . .

# Restore dependencies
RUN dotnet restore "Switchboard.Server/Switchboard.Server.csproj"

# Build the application
WORKDIR /src/Switchboard.Server
RUN dotnet build "Switchboard.Server.csproj" -c Release -f net10.0 -o /app/build /p:GeneratePackageOnBuild=false


#
#
# Publish stage
#
#
FROM build AS publish
RUN dotnet publish "Switchboard.Server.csproj" -c Release -f net10.0 -o /app/publish /p:UseAppHost=false /p:GeneratePackageOnBuild=false /p:ErrorOnDuplicatePublishOutputFiles=false


#
#
# Runtime stage
#
#
FROM viewio/view-server-base:v1.4.0 AS final

# Install network troubleshooting tools and useful utilities
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    wget \
    dnsutils \
    iputils-ping \
    net-tools \
    iproute2 \
    telnet \
    vim \
    jq \
    openssl \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8000

# Health check
HEALTHCHECK --interval=10s --timeout=5s --start-period=10s --retries=2 \
    CMD curl -f http://localhost:8000/_sb/v1.0/health || exit 1

ENTRYPOINT ["dotnet", "Switchboard.Server.dll"]