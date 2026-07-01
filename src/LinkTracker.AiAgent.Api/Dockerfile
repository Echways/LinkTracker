FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["linktracker.sln", "./"]

COPY ["src/LinkTracker.AiAgent.Api", "src/LinkTracker.AiAgent.Api/"]
COPY ["src/LinkTracker.AiAgent.Application/LinkTracker.AiAgent.Application.csproj", "src/LinkTracker.AiAgent.Application/"]
COPY ["src/LinkTracker.AiAgent.Infrastructure/LinkTracker.AiAgent.Infrastructure.csproj", "src/LinkTracker.AiAgent.Infrastructure/"]
COPY ["src/LinkTracker.EnvReader/LinkTracker.EnvReader.csproj", "src/LinkTracker.EnvReader/"]
COPY ["src/LinkTracker.Shared/LinkTracker.Shared.csproj", "src/LinkTracker.Shared/"]

RUN dotnet restore "src/LinkTracker.AiAgent.Api/LinkTracker.AiAgent.Api.csproj" 
COPY . .
WORKDIR "/src/src/LinkTracker.AiAgent.Api"
RUN dotnet publish "LinkTracker.AiAgent.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LinkTracker.AiAgent.Api.dll"]