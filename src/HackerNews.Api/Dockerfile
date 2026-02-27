# 1. BASE STAGE
# Uses the minimal ASP.NET Core runtime image. Used for the final running container.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
# Run as a non-root user for security best practices
USER $APP_UID
WORKDIR /app
# Expose the standard .NET 8+ ports
EXPOSE 8080
EXPOSE 8081

# 2. BUILD STAGE
# Uses the heavier .NET SDK image containing all compiler tools
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy only the csproj file first and restore dependencies. 
# This layer gets cached, speeding up future builds if packages haven't changed.
COPY ["src/HackerNews.Api/HackerNews.Api.csproj", "src/HackerNews.Api/"]
RUN dotnet restore "./src/HackerNews.Api/HackerNews.Api.csproj"

# Copy the rest of the source code and compile the application
COPY . .
WORKDIR "/src/src/HackerNews.Api"
RUN dotnet build "./HackerNews.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 3. PUBLISH STAGE
# Packages the compiled application and its dependencies into a folder for deployment
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./HackerNews.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 4. FINAL STAGE
# Copies the published output from the build container into the minimal base runtime container
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Sets the command that executes when the container starts
ENTRYPOINT ["dotnet", "HackerNews.Api.dll"]