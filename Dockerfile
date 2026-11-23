# syntax=docker/dockerfile:1

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore in its own layer: only project files and central props invalidate it.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/FiapX.Identity.Domain/FiapX.Identity.Domain.csproj src/FiapX.Identity.Domain/
COPY src/FiapX.Identity.Application/FiapX.Identity.Application.csproj src/FiapX.Identity.Application/
COPY src/FiapX.Identity.Infrastructure/FiapX.Identity.Infrastructure.csproj src/FiapX.Identity.Infrastructure/
COPY src/FiapX.Identity.Api/FiapX.Identity.Api.csproj src/FiapX.Identity.Api/
RUN dotnet restore src/FiapX.Identity.Api/FiapX.Identity.Api.csproj

COPY src/ src/
RUN dotnet publish src/FiapX.Identity.Api/FiapX.Identity.Api.csproj -c Release --no-restore -o /app

# ---- runtime: chiseled image, no shell, non-root (uid 1654) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FiapX.Identity.Api.dll"]
