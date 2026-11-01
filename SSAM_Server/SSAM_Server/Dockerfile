# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY SSAM_Server.csproj ./
RUN dotnet restore SSAM_Server.csproj
COPY . .
RUN dotnet publish SSAM_Server.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

# Migration stage: dùng để áp dụng EF migrations: docker compose run --rm migrate
FROM build AS migrate
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install --global dotnet-ef --version 9.0.2
ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "SSAM_Server.csproj", "-c", "Release", "--no-build"]

# Final runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "SSAM_Server.dll"]
