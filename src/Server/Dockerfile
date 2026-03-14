FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

# ── Restore ──────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy both project files so the ProjectReference resolves during restore
COPY ["src/Client/SentinelCrypto.Client.csproj", "src/Client/"]
COPY ["src/Server/SentinelCrypto.Server.csproj", "src/Server/"]
RUN dotnet restore "src/Server/SentinelCrypto.Server.csproj"

# Copy all source and publish
COPY . .
RUN dotnet publish "src/Server/SentinelCrypto.Server.csproj" \
    -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime image ─────────────────────────────────────────────────────────────
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render (and most PaaS providers) set PORT at runtime.
# ASP.NET Core will read ASPNETCORE_URLS; we fall back to 8080 for local Docker.
ENV ASPNETCORE_URLS=http://+:8080
CMD ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet SentinelCrypto.Server.dll
