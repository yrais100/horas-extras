# Imagen de la aplicación (web Blazor + API). Sirve igual para Render, Google Cloud Run, Azure Container Apps,
# Koyeb o Railway: todos construyen este Dockerfile y le pasan la cadena de conexión por variable de entorno
# (ConnectionStrings__Default). Ver docs/despliegue-render.md.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Primero solo los proyectos, para que la restauración de paquetes quede en caché entre builds.
COPY HorasExtras.slnx dotnet-tools.json ./
COPY src/HorasExtras.Domain/HorasExtras.Domain.csproj src/HorasExtras.Domain/
COPY src/HorasExtras.Application/HorasExtras.Application.csproj src/HorasExtras.Application/
COPY src/HorasExtras.Infrastructure/HorasExtras.Infrastructure.csproj src/HorasExtras.Infrastructure/
COPY src/HorasExtras.Web/HorasExtras.Web.csproj src/HorasExtras.Web/
RUN dotnet restore src/HorasExtras.Web/HorasExtras.Web.csproj
COPY src/ src/
RUN dotnet publish src/HorasExtras.Web -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# QuestPDF (reportes PDF) dibuja el texto con fontconfig; la imagen base no lo trae.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
# El hosting termina HTTPS y reenvía por HTTP: así la app conoce el esquema y la IP originales.
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
# Recolector de basura de estación de trabajo: usa menos memoria en instancias pequeñas (512 MB).
ENV DOTNET_gcServer=0
EXPOSE 8080
USER $APP_UID
# Escucha en $PORT si el hosting lo define (Render, Cloud Run, Railway...), o en 8080.
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet HorasExtras.Web.dll --urls \"http://+:${PORT:-8080}\""]
