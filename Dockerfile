# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first to leverage layer caching on restore
COPY ProductGallery.sln ./
COPY ProductGallery/UCSProductGallery/UCSProductGallery.csproj ProductGallery/UCSProductGallery/
COPY ProductGallery.Tests/UCSProductGallery.Tests.csproj ProductGallery.Tests/

RUN dotnet restore ProductGallery/UCSProductGallery/UCSProductGallery.csproj

# Copy the rest of the source and publish
COPY . .
RUN dotnet publish ProductGallery/UCSProductGallery/UCSProductGallery.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Azure App Service (multi-container) probes port 80 by default, so listen there
EXPOSE 80
ENV ASPNETCORE_HTTP_PORTS=80 \
    ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "UCSProductGallery.dll"]
