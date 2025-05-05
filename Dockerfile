FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY ["CarSureBotDotNet/CarSureBotDotNet.csproj", "CarSureBotDotNet/"]

RUN dotnet restore "CarSureBotDotNet/CarSureBotDotNet.csproj"
RUN dotnet publish "CarSureBotDotNet/CarSureBotDotNet.csproj" -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "CarSureBotDotNet.dll"]
