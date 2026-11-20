
# Build ASP.NET Core API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src
COPY . .

RUN dotnet restore src/JanitorialManagementDashboard.Api/JanitorialManagementDashboard.Api.csproj

RUN dotnet publish src/JanitorialManagementDashboard.Api/JanitorialManagementDashboard.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Run ASP.NET Core API
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 10000

ENTRYPOINT ["dotnet", "JanitorialManagementDashboard.Api.dll"]
