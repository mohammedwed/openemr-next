ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
# Copy project files first so the restore layer is cached until dependencies change.
COPY OpenEmrNext.AuthFacade.Api/OpenEmrNext.AuthFacade.Api.csproj OpenEmrNext.AuthFacade.Api/
COPY OpenEmrNext.AuthFacade.Application/OpenEmrNext.AuthFacade.Application.csproj OpenEmrNext.AuthFacade.Application/
COPY OpenEmrNext.AuthFacade.Application.Ports/OpenEmrNext.AuthFacade.Application.Ports.csproj OpenEmrNext.AuthFacade.Application.Ports/
COPY OpenEmrNext.AuthFacade.Composition/OpenEmrNext.AuthFacade.Composition.csproj OpenEmrNext.AuthFacade.Composition/
COPY OpenEmrNext.AuthFacade.Domain/OpenEmrNext.AuthFacade.Domain.csproj OpenEmrNext.AuthFacade.Domain/
COPY OpenEmrNext.AuthFacade.Infrastructure/OpenEmrNext.AuthFacade.Infrastructure.csproj OpenEmrNext.AuthFacade.Infrastructure/
RUN dotnet restore OpenEmrNext.AuthFacade.Api/OpenEmrNext.AuthFacade.Api.csproj
COPY . ./
RUN dotnet publish OpenEmrNext.AuthFacade.Api/OpenEmrNext.AuthFacade.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "OpenEmrNext.AuthFacade.Api.dll"]
