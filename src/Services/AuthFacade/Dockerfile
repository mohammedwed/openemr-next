ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
# Copy project files first so the restore layer is cached until dependencies change.
COPY OpenEmrNext.AuthFacade.Api/OpenEmrNext.AuthFacade.Api.csproj OpenEmrNext.AuthFacade.Api/
COPY OpenEmrNext.AuthFacade.Composition/OpenEmrNext.AuthFacade.Composition.csproj OpenEmrNext.AuthFacade.Composition/
RUN dotnet restore OpenEmrNext.AuthFacade.Api/OpenEmrNext.AuthFacade.Api.csproj
COPY . ./
RUN dotnet publish OpenEmrNext.AuthFacade.Api/OpenEmrNext.AuthFacade.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# The image ships a non-root "app" user (APP_UID). Port 8080 needs no privileges.
USER $APP_UID
ENTRYPOINT ["dotnet", "OpenEmrNext.AuthFacade.Api.dll"]
