FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY NeverfadePos.Api/NeverfadePos.Api.csproj NeverfadePos.Api/

RUN dotnet restore NeverfadePos.Api/NeverfadePos.Api.csproj

COPY . .

RUN dotnet publish \
  NeverfadePos.Api/NeverfadePos.Api.csproj \
  --configuration Release \
  --output /app/publish \
  /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
ENV DOTNET_EnableDiagnostics=0

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "NeverfadePos.Api.dll"]
