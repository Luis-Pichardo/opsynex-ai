# syntax=docker/dockerfile:1
# API de Opsynex AI — build multi-stage. Contexto de build: carpeta backend/.
#   docker build -t opsynex-api backend                      → API (imagen final)
#   docker build -t opsynex-migrations --target migrations backend → bundle de migraciones de EF Core

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# Sin analizadores en el build de la imagen (los exige la CI): ver Directory.Build.props.
ENV ContainerBuild=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /src

# Restaurar primero (mejor uso de la caché de capas).
COPY Directory.Build.props Directory.Packages.props Opsynex.sln dotnet-tools.json ./
COPY src/Opsynex.Api/Opsynex.Api.csproj src/Opsynex.Api/
COPY src/Opsynex.Application/Opsynex.Application.csproj src/Opsynex.Application/
COPY src/Opsynex.Domain/Opsynex.Domain.csproj src/Opsynex.Domain/
COPY src/Opsynex.Infrastructure/Opsynex.Infrastructure.csproj src/Opsynex.Infrastructure/
RUN dotnet restore src/Opsynex.Api/Opsynex.Api.csproj -r linux-x64

COPY src/ src/
# Libros sintéticos para el comando seed-demo (sin datos de clientes).
COPY sample-data/libro_sintetico_2a.xls sample-data/libro_sintetico_3a.xls sample-data/
COPY sample-data/industrial/*.xlsx sample-data/industrial/
RUN dotnet publish src/Opsynex.Api/Opsynex.Api.csproj -c Release -r linux-x64 --self-contained false \
    -o /app/publish --no-restore /p:UseAppHost=false

# Bundle de migraciones: un ejecutable que aplica las migraciones pendientes (idempotente).
FROM build AS bundle
RUN dotnet tool restore \
 && dotnet ef migrations bundle -p src/Opsynex.Infrastructure -s src/Opsynex.Api \
      -r linux-x64 --self-contained -o /app/efbundle --configuration Release

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0 AS migrations
WORKDIR /app
COPY --from=bundle /app/efbundle .
USER $APP_UID
# La cadena de conexión llega por la variable ConnectionStrings__Default.
ENTRYPOINT ["/bin/sh", "-c", "./efbundle --connection \"$ConnectionStrings__Default\""]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1
EXPOSE 8080
COPY --from=build /app/publish .
# Usuario sin privilegios incluido en las imágenes oficiales de .NET 8+.
USER $APP_UID
ENTRYPOINT ["dotnet", "Opsynex.Api.dll"]
