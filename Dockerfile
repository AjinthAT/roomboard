# syntax=docker/dockerfile:1

# --- 1. Front : buildé d'abord, sa sortie est servie par le Core ---
FROM node:24-alpine AS web
WORKDIR /build/web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-fund --no-audit
COPY web/ ./
# vite.config.ts écrit dans ../src/RoomOS.Core/wwwroot
RUN npm run build

# --- 2. Core ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS core
WORKDIR /build
COPY Directory.Build.props ./
COPY src/RoomOS.Domain/ src/RoomOS.Domain/
COPY src/RoomOS.Core/ src/RoomOS.Core/
RUN dotnet restore src/RoomOS.Core/RoomOS.Core.csproj
COPY --from=web /build/src/RoomOS.Core/wwwroot/ src/RoomOS.Core/wwwroot/
RUN dotnet publish src/RoomOS.Core/RoomOS.Core.csproj -c Release -o /app --no-restore

# --- 3. Runtime ---
# InvariantGlobalization=true dans Directory.Build.props : pas besoin d'ICU ici.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=core /app ./
# Le conteneur tourne en utilisateur non privilégié : /data doit lui appartenir
# dans l'image, sinon le volume nommé est créé en root et SQLite ne peut pas écrire.
RUN mkdir -p /data && chown $APP_UID:$APP_UID /data
USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENTRYPOINT ["dotnet", "RoomOS.Core.dll"]
