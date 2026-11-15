ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
# Copy project files first so the restore layer is cached until dependencies change.
COPY OpenEmrNext.PatientManagement.Domain/OpenEmrNext.PatientManagement.Domain.csproj OpenEmrNext.PatientManagement.Domain/
COPY OpenEmrNext.PatientManagement.Domain.ReadModels/OpenEmrNext.PatientManagement.Domain.ReadModels.csproj OpenEmrNext.PatientManagement.Domain.ReadModels/
COPY OpenEmrNext.PatientManagement.Application/OpenEmrNext.PatientManagement.Application.csproj OpenEmrNext.PatientManagement.Application/
COPY OpenEmrNext.PatientManagement.Application.Ports/OpenEmrNext.PatientManagement.Application.Ports.csproj OpenEmrNext.PatientManagement.Application.Ports/
COPY OpenEmrNext.PatientManagement.Infrastructure/OpenEmrNext.PatientManagement.Infrastructure.csproj OpenEmrNext.PatientManagement.Infrastructure/
COPY OpenEmrNext.PatientManagement.Composition/OpenEmrNext.PatientManagement.Composition.csproj OpenEmrNext.PatientManagement.Composition/
COPY OpenEmrNext.PatientManagement.Api/OpenEmrNext.PatientManagement.Api.csproj OpenEmrNext.PatientManagement.Api/
RUN dotnet restore OpenEmrNext.PatientManagement.Api/OpenEmrNext.PatientManagement.Api.csproj
COPY . ./
RUN dotnet publish OpenEmrNext.PatientManagement.Api/OpenEmrNext.PatientManagement.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# The image ships a non-root "app" user (APP_UID). Port 8080 needs no privileges.
USER $APP_UID
ENTRYPOINT ["dotnet", "OpenEmrNext.PatientManagement.Api.dll"]
