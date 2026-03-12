FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Server/SentinelCrypto.Server.csproj", "src/Server/"]
RUN dotnet restore "src/Server/SentinelCrypto.Server.csproj"
COPY . .
WORKDIR "/src/src/Server"
RUN dotnet build "SentinelCrypto.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SentinelCrypto.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SentinelCrypto.Server.dll"]
