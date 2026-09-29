FROM mcr.microsoft.com/dotnet/sdk:10.0 AS verify
WORKDIR /work
COPY . .
RUN bash scripts/verify.sh

FROM verify AS publish
RUN dotnet publish src/Spectrum.Cli -c Release --no-restore -o /publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=publish /publish/ ./
USER $APP_UID
ENTRYPOINT ["dotnet", "Spectrum.Cli.dll"]
