ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
# Copy the project file first so the restore layer is cached until dependencies change.
COPY OpenEmrNext.LoginUi/OpenEmrNext.LoginUi.csproj OpenEmrNext.LoginUi/
RUN dotnet restore OpenEmrNext.LoginUi/OpenEmrNext.LoginUi.csproj
COPY . ./
# No --no-restore: the restore above only saw the .csproj, before any .razor files existed, so MSBuild's
# static-web-assets evaluation didn't yet know this is a Razor Components project and never pulled in
# blazor.web.js. Publish re-restores (fast; packages are already cached from the layer above) now that the
# full source is present, which resolves it correctly.
RUN dotnet publish OpenEmrNext.LoginUi/OpenEmrNext.LoginUi.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# The image ships a non-root "app" user (APP_UID). Port 8080 needs no privileges.
USER $APP_UID
ENTRYPOINT ["dotnet", "OpenEmrNext.LoginUi.dll"]
