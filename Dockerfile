FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["TwitchChatParser.csproj", "."]
RUN dotnet restore "./TwitchChatParser.csproj"
COPY . .
RUN dotnet publish "TwitchChatParser.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TwitchChatParser.dll"]
