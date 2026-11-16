ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
# Copy project files first so the restore layer is cached until dependencies change.
COPY OpenEmrNext.ClinicalCare.Domain/OpenEmrNext.ClinicalCare.Domain.csproj OpenEmrNext.ClinicalCare.Domain/
COPY OpenEmrNext.ClinicalCare.Domain.ReadModels/OpenEmrNext.ClinicalCare.Domain.ReadModels.csproj OpenEmrNext.ClinicalCare.Domain.ReadModels/
COPY OpenEmrNext.ClinicalCare.Application.Ports/OpenEmrNext.ClinicalCare.Application.Ports.csproj OpenEmrNext.ClinicalCare.Application.Ports/
COPY OpenEmrNext.ClinicalCare.Application/OpenEmrNext.ClinicalCare.Application.csproj OpenEmrNext.ClinicalCare.Application/
COPY OpenEmrNext.ClinicalCare.Infrastructure/OpenEmrNext.ClinicalCare.Infrastructure.csproj OpenEmrNext.ClinicalCare.Infrastructure/
COPY OpenEmrNext.ClinicalCare.Composition/OpenEmrNext.ClinicalCare.Composition.csproj OpenEmrNext.ClinicalCare.Composition/
COPY OpenEmrNext.ClinicalCare.Api/OpenEmrNext.ClinicalCare.Api.csproj OpenEmrNext.ClinicalCare.Api/
RUN dotnet restore OpenEmrNext.ClinicalCare.Api/OpenEmrNext.ClinicalCare.Api.csproj
COPY . ./
RUN dotnet publish OpenEmrNext.ClinicalCare.Api/OpenEmrNext.ClinicalCare.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# The image ships a non-root "app" user (APP_UID). Port 8080 needs no privileges.
USER $APP_UID
ENTRYPOINT ["dotnet", "OpenEmrNext.ClinicalCare.Api.dll"]
