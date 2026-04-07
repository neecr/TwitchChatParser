FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["TwitchChatParser.Worker/TwitchChatParser.Worker.csproj", "TwitchChatParser.Worker/"]
COPY ["TwitchChatParser.Domain/TwitchChatParser.Domain.csproj", "TwitchChatParser.Domain/"]
COPY ["TwitchChatParser.Application/TwitchChatParser.Application.csproj", "TwitchChatParser.Application/"]
COPY ["TwitchChatParser.Infrastructure/TwitchChatParser.Infrastructure.csproj", "TwitchChatParser.Infrastructure/"]
RUN dotnet restore "TwitchChatParser.Worker/TwitchChatParser.Worker.csproj"
COPY . .
WORKDIR "/src/TwitchChatParser.Worker"
RUN dotnet publish "TwitchChatParser.Worker.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TwitchChatParser.Worker.dll"]
