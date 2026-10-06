FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY FieldSuite.slnx ./
COPY src/FieldSuite.Domain/FieldSuite.Domain.csproj src/FieldSuite.Domain/
COPY src/FieldSuite.Infrastructure/FieldSuite.Infrastructure.csproj src/FieldSuite.Infrastructure/
COPY src/FieldSuite.Web/FieldSuite.Web.csproj src/FieldSuite.Web/
COPY tests/FieldSuite.Tests/FieldSuite.Tests.csproj tests/FieldSuite.Tests/
RUN dotnet restore FieldSuite.slnx

COPY . .
RUN dotnet publish src/FieldSuite.Web/FieldSuite.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FieldSuite.Web.dll"]