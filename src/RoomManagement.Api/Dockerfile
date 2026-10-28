# Builds the API image. The build context is the repository root (the API needs the sibling projects):
#
#   docker build -f src/RoomManagement.Api/Dockerfile -t roommanagement-api .
#
# docker-compose.yml does this for you.

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the project files alone first, so the restore layer stays cached until a dependency changes.
COPY src/RoomManagement.Domain/RoomManagement.Domain.csproj src/RoomManagement.Domain/
COPY src/RoomManagement.Application/RoomManagement.Application.csproj src/RoomManagement.Application/
COPY src/RoomManagement.Infrastructure/RoomManagement.Infrastructure.csproj src/RoomManagement.Infrastructure/
COPY src/RoomManagement.Api/RoomManagement.Api.csproj src/RoomManagement.Api/
RUN dotnet restore src/RoomManagement.Api/RoomManagement.Api.csproj

COPY src/ src/
RUN dotnet publish src/RoomManagement.Api/RoomManagement.Api.csproj \
    --configuration Release --no-restore --output /app/publish -p:UseAppHost=false

# ---- run ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# The base image provides an unprivileged user; the API never needs root.
USER $APP_UID

ENTRYPOINT ["dotnet", "RoomManagement.Api.dll"]
