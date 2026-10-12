FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY PersonalCabinetEducationProgram/PersonalCabinetEducationProgram.csproj PersonalCabinetEducationProgram/
RUN dotnet restore PersonalCabinetEducationProgram/PersonalCabinetEducationProgram.csproj
COPY PersonalCabinetEducationProgram/ PersonalCabinetEducationProgram/
RUN dotnet publish PersonalCabinetEducationProgram/PersonalCabinetEducationProgram.csproj \
    -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    FileStorageSettings__StoragePath=/app/storage \
    DataProtection__KeysPath=/app/keys
COPY --from=build /out/ ./
RUN rm -f appsettings.Development.json \
    && mkdir -p /app/storage /app/keys \
    && chown -R app:app /app/storage /app/keys
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "PersonalCabinetEducationProgram.dll"]
