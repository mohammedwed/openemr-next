ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
COPY OpenEmrNext.Gateway/OpenEmrNext.Gateway.csproj OpenEmrNext.Gateway/
RUN dotnet restore OpenEmrNext.Gateway/OpenEmrNext.Gateway.csproj
COPY . ./
RUN dotnet publish OpenEmrNext.Gateway/OpenEmrNext.Gateway.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# HTTPS (ADR 0039). docker-compose.yml overrides ASPNETCORE_URLS to bind both and mounts the cert
# scripts/setup/new-dev-cert.sh generates; without that override this port listens on nothing.
EXPOSE 8443
# The image ships a non-root "app" user (APP_UID). Port 8080 needs no privileges.
USER $APP_UID
ENTRYPOINT ["dotnet", "OpenEmrNext.Gateway.dll"]
