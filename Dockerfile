FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY ["CarSureBotDotNet.sln", "./"]
COPY ["CarSureBotDotNet/CarSureBotDotNet.csproj", "CarSureBotDotNet/"]

RUN dotnet restore "CarSureBotDotNet.sln"

RUN dotnet publish "CarSureBotDotNet/CarSureBotDotNet.csproj" -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

COPY --from=build /app .

ENTRYPOINT ["dotnet", "CarSureBotDotNet.dll"]
