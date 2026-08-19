FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["linktracker.sln", "./"]
COPY ["src/Directory.Build.props", "src/"]

COPY ["src/LinkTracker.Scrapper.Api/LinkTracker.Scrapper.Api.csproj", "src/LinkTracker.Scrapper.Api/"]
COPY ["src/LinkTracker.Scrapper.Application/LinkTracker.Scrapper.Application.csproj", "src/LinkTracker.Scrapper.Application/"]
COPY ["src/LinkTracker.Scrapper.Contracts/LinkTracker.Scrapper.Contracts.csproj", "src/LinkTracker.Scrapper.Contracts/"]
COPY ["src/LinkTracker.Scrapper.Infrastructure/LinkTracker.Scrapper.Infrastructure.csproj", "src/LinkTracker.Scrapper.Infrastructure/"]
COPY ["src/LinkTracker.Scrapper.Presentation/LinkTracker.Scrapper.Presentation.csproj", "src/LinkTracker.Scrapper.Presentation/"]
COPY ["src/LinkTracker.Scrapper.Storage.Abstractions/LinkTracker.Scrapper.Storage.Abstractions.csproj", "src/LinkTracker.Scrapper.Storage.Abstractions/"]
COPY ["src/LinkTracker.Scrapper.Storage.Orm/LinkTracker.Scrapper.Storage.Orm.csproj", "src/LinkTracker.Scrapper.Storage.Orm/"]
COPY ["src/LinkTracker.Scrapper.Storage.Sql/LinkTracker.Scrapper.Storage.Sql.csproj", "src/LinkTracker.Scrapper.Storage.Sql/"]
COPY ["src/LinkTracker.EnvReader/LinkTracker.EnvReader.csproj", "src/LinkTracker.EnvReader/"]
COPY ["src/LinkTracker.Shared/LinkTracker.Shared.csproj", "src/LinkTracker.Shared/"]

RUN dotnet restore "src/LinkTracker.Scrapper.Api/LinkTracker.Scrapper.Api.csproj" 

COPY . .
WORKDIR "/src/src/LinkTracker.Scrapper.Api"
RUN dotnet publish "LinkTracker.Scrapper.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY migrations ./migrations
ENTRYPOINT ["dotnet", "LinkTracker.Scrapper.Api.dll"]