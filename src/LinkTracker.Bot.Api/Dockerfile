FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["linktracker.sln", "./"]

COPY ["src/LinkTracker.Bot.Api/LinkTracker.Bot.Api.csproj", "src/LinkTracker.Bot.Api/"]
COPY ["src/LinkTracker.Bot.Application/LinkTracker.Bot.Application.csproj", "src/LinkTracker.Bot.Application/"]
COPY ["src/LinkTracker.Bot.Infrastructure/LinkTracker.Bot.Infrastructure.csproj", "src/LinkTracker.Bot.Infrastructure/"]
COPY ["src/LinkTracker.Bot.Presentation/LinkTracker.Bot.Presentation.csproj", "src/LinkTracker.Bot.Presentation/"]
COPY ["src/LinkTracker.EnvReader/LinkTracker.EnvReader.csproj", "src/LinkTracker.EnvReader/"]
COPY ["src/LinkTracker.Shared/LinkTracker.Shared.csproj", "src/LinkTracker.Shared/"]

RUN dotnet restore "src/LinkTracker.Bot.Api/LinkTracker.Bot.Api.csproj" 
COPY . .
WORKDIR "/src/src/LinkTracker.Bot.Api"
RUN dotnet publish "LinkTracker.Bot.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LinkTracker.Bot.Api.dll"]