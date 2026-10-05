FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /app

COPY Directory.Build.props ./
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Learning/ src/Learning/

RUN dotnet restore src/Learning/Learning.Api/Learning.Api.csproj
RUN dotnet publish src/Learning/Learning.Api/Learning.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5003
EXPOSE 5003

ENTRYPOINT ["dotnet", "Learning.Api.dll"]
