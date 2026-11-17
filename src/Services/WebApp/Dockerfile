# Build context is the repository root (see docker-compose.yml), not this directory: the app
# references OpenEmrNext.AuthFacade.Client (ADR 0031), OpenEmrNext.PatientManagement.Client (ADR 0059)
# and OpenEmrNext.ClinicalCare.Client (ADR 0067), which live under sibling service directories Docker
# cannot reach from a context scoped to src/Services/WebApp alone.
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src
# Copy project files first so the restore layer is cached until dependencies change.
COPY src/Services/WebApp/OpenEmrNext.WebApp/OpenEmrNext.WebApp.csproj src/Services/WebApp/OpenEmrNext.WebApp/
COPY src/Services/AuthFacade/OpenEmrNext.AuthFacade.Client/OpenEmrNext.AuthFacade.Client.csproj src/Services/AuthFacade/OpenEmrNext.AuthFacade.Client/
COPY src/Services/PatientManagement/OpenEmrNext.PatientManagement.Client/OpenEmrNext.PatientManagement.Client.csproj src/Services/PatientManagement/OpenEmrNext.PatientManagement.Client/
COPY src/Services/ClinicalCare/OpenEmrNext.ClinicalCare.Client/OpenEmrNext.ClinicalCare.Client.csproj src/Services/ClinicalCare/OpenEmrNext.ClinicalCare.Client/
RUN dotnet restore src/Services/WebApp/OpenEmrNext.WebApp/OpenEmrNext.WebApp.csproj
COPY src/Services/WebApp/OpenEmrNext.WebApp/ src/Services/WebApp/OpenEmrNext.WebApp/
COPY src/Services/AuthFacade/OpenEmrNext.AuthFacade.Client/ src/Services/AuthFacade/OpenEmrNext.AuthFacade.Client/
COPY src/Services/PatientManagement/OpenEmrNext.PatientManagement.Client/ src/Services/PatientManagement/OpenEmrNext.PatientManagement.Client/
COPY src/Services/ClinicalCare/OpenEmrNext.ClinicalCare.Client/ src/Services/ClinicalCare/OpenEmrNext.ClinicalCare.Client/
RUN dotnet publish src/Services/WebApp/OpenEmrNext.WebApp/OpenEmrNext.WebApp.csproj -c Release -o /app/publish --no-restore

# A standalone Blazor WebAssembly app is just static files: nginx serves them directly, no .NET runtime
# needed at all in this stage (unlike AuthFacade or the Gateway, which are real ASP.NET Core apps).
FROM nginx:1.27-alpine AS runtime
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY src/Services/WebApp/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 8080
