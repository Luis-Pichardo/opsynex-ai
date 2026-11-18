# syntax=docker/dockerfile:1
# API de Opsynex AI — build multi-stage. Contexto de build: carpeta backend/.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restaurar primero (mejor uso de la caché de capas).
COPY Directory.Build.props Directory.Packages.props Opsynex.sln ./
COPY src/Opsynex.Api/Opsynex.Api.csproj src/Opsynex.Api/
COPY src/Opsynex.Application/Opsynex.Application.csproj src/Opsynex.Application/
COPY src/Opsynex.Domain/Opsynex.Domain.csproj src/Opsynex.Domain/
COPY src/Opsynex.Infrastructure/Opsynex.Infrastructure.csproj src/Opsynex.Infrastructure/
RUN dotnet restore src/Opsynex.Api/Opsynex.Api.csproj

COPY src/ src/
RUN dotnet publish src/Opsynex.Api/Opsynex.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
# Usuario sin privilegios incluido en las imágenes oficiales de .NET 8+.
USER $APP_UID
ENTRYPOINT ["dotnet", "Opsynex.Api.dll"]
