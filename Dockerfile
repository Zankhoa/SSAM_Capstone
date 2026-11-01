# Base stage - Runtime ASP.NET Core 9.0
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Build stage - .NET SDK 9.0
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy .csproj và restore dependencies trước để tận dụng Docker layer cache
COPY ["SSAM_Server/SSAM_Server/SSAM_Server.csproj", "SSAM_Server/SSAM_Server/"]
RUN dotnet restore "SSAM_Server/SSAM_Server/SSAM_Server.csproj"

# Copy toàn bộ mã nguồn của backend và publish
COPY SSAM_Server/ SSAM_Server/
WORKDIR "/src/SSAM_Server/SSAM_Server"
RUN dotnet publish "SSAM_Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --no-restore

# Migration stage - Dùng để chạy migration thủ công nếu cần
FROM build AS migrate
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install --global dotnet-ef --version 9.0.2
WORKDIR "/src/SSAM_Server/SSAM_Server"
ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "SSAM_Server.csproj", "-c", "Release", "--no-build"]

# Final runtime stage
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "SSAM_Server.dll"]
