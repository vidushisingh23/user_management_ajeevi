FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# MSSQL configuration
ENV IsMSSQL=true
#ENV ConnectionString="Server=localhost\\SQLEXPRESS,1434;Database=DemoAuthDB;User Id=dockeruser;Password=Docker@123;TrustServerCertificate=True;"
ENV ConnectionString="Server=localhost\\SQLEXPRESS;Database=DemoAuthDB;Trusted_Connection=True;TrustServerCertificate=True"
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["DemoAuth.csproj", "."]
RUN dotnet restore "./DemoAuth.csproj"

COPY . .
RUN dotnet publish "DemoAuth.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "DemoAuth.dll"]