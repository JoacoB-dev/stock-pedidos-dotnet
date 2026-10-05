# ---------- 1) Interfaz React ----------
FROM node:22-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build -- --outDir /web/dist

# ---------- 2) API .NET ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json StockPedidos.slnx ./
COPY src/Stock.Dominio/Stock.Dominio.csproj src/Stock.Dominio/
COPY src/Stock.Datos/Stock.Datos.csproj src/Stock.Datos/
COPY src/Stock.Api/Stock.Api.csproj src/Stock.Api/
# Restore en una capa aparte: si no cambian los .csproj, Docker la reutiliza
RUN dotnet restore src/Stock.Api/Stock.Api.csproj
COPY src/ src/
RUN dotnet publish src/Stock.Api/Stock.Api.csproj -c Release -o /app --no-restore

# ---------- 3) Imagen final (solo runtime) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
COPY --from=web /web/dist ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
# Usuario sin privilegios que trae la imagen oficial
USER $APP_UID
ENTRYPOINT ["dotnet", "Stock.Api.dll"]
