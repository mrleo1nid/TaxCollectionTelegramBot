FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY src/TaxCollectionTelegramBot/*.csproj ./TaxCollectionTelegramBot/
RUN dotnet restore TaxCollectionTelegramBot/TaxCollectionTelegramBot.csproj

# Copy source code and build
COPY src/TaxCollectionTelegramBot/ ./TaxCollectionTelegramBot/
WORKDIR /src/TaxCollectionTelegramBot
RUN dotnet publish -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create data directory for SQLite
RUN mkdir -p /app/data

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "TaxCollectionTelegramBot.dll"]
