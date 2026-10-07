FROM node:24-bookworm-slim AS web-build
WORKDIR /src/web
COPY src/Statik.Web/package*.json ./
RUN npm ci
COPY src/Statik.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server-build
WORKDIR /src
COPY src/Statik.Server/Statik.Server.csproj Statik.Server/
RUN dotnet restore Statik.Server/Statik.Server.csproj
COPY src/Statik.Server/ Statik.Server/
RUN dotnet publish Statik.Server/Statik.Server.csproj -c Release --no-restore -o /app
COPY --from=web-build /src/web/dist/ /app/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=server-build /app/ ./
# Management commands: docker compose exec statik statik list-users
RUN printf '#!/bin/sh\nexec dotnet /app/Statik.Server.dll "$@"\n' > /usr/local/bin/statik \
    && chmod 755 /usr/local/bin/statik
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Statik.Server.dll"]
