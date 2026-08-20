# Stage Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

# 1. Copia o .csproj de dentro da pasta src/ para aproveitar o cache de camadas do Docker
COPY src/*.csproj ./src/
RUN dotnet restore ./src/*.csproj

# 2. Copia todo o código-fonte
COPY src/ ./src/

# 3. Compila a partir do diretório da aplicação
WORKDIR /app/src
RUN dotnet publish -c Release -o /app/publish

# Stage Runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "IpMonitor.dll"]
