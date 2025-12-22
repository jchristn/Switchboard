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
RUN dotnet build "Switchboard.Server.csproj" -c Release -o /app/build /p:GeneratePackageOnBuild=false


#
#
# Publish stage
#
#
FROM build AS publish
RUN dotnet publish "Switchboard.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false /p:GeneratePackageOnBuild=false /p:ErrorOnDuplicatePublishOutputFiles=false


#
#
# Runtime stage
#
#
FROM viewio/view-server-base:v1.4.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8000
ENTRYPOINT ["dotnet", "Switchboard.Server.dll"]