FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Erp.sln ./
COPY src/Erp.Api/Erp.Api.csproj src/Erp.Api/
COPY src/Erp.Application/Erp.Application.csproj src/Erp.Application/
COPY src/Erp.Domain/Erp.Domain.csproj src/Erp.Domain/
COPY src/Erp.Infrastructure/Erp.Infrastructure.csproj src/Erp.Infrastructure/
COPY src/Erp.SharedKernel/Erp.SharedKernel.csproj src/Erp.SharedKernel/
RUN dotnet restore src/Erp.Api/Erp.Api.csproj

COPY src/ src/
RUN dotnet publish src/Erp.Api/Erp.Api.csproj --configuration Release --output /app/publish --no-restore

FROM build AS migrations
RUN dotnet tool install dotnet-ef --version 8.0.30 --tool-path /tools
ENTRYPOINT ["/tools/dotnet-ef"]
CMD ["database", "update", "--project", "src/Erp.Infrastructure", "--startup-project", "src/Erp.Api", "--configuration", "Release", "--no-build"]

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/data/attachments && chown -R app:app /app/data
WORKDIR /app
ENV ENTRYPOINT ["sh", "-c", "dotnet Erp.Api.dll --urls http://0.0.0.0:${PORT:-10000}"]
COPY --from=build /app/publish .
USER app
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 CMD curl --fail --silent http://localhost:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "Erp.Api.dll"]
