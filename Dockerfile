FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["CarSureBot.sln", "./"]
COPY ["CarSureBot/CarSureBot.csproj", "CarSureBot/"]

RUN dotnet restore "CarSureBot.sln"

RUN dotnet publish "CarSureBot/CarSureBot.csproj" -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "CarSureBot.dll"]
