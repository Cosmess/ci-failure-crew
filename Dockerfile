# Prebuilt image used by the GitHub Action on Linux runners, so an explanation does not wait for the .NET SDK and a build.
# The build stage runs on the build machine's platform: the output is framework-dependent and works on amd64 and arm64.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ENV DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
WORKDIR /source
COPY Directory.Build.props ./
COPY src/ src/
RUN dotnet publish src/CiFailureCrew.Cli -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
COPY --from=build /app /app
ENTRYPOINT ["dotnet", "/app/cfc.dll"]
