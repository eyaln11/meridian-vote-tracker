FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/MeridianVoteTracker.Core/*.csproj src/MeridianVoteTracker.Core/
COPY src/MeridianVoteTracker.Infrastructure/*.csproj src/MeridianVoteTracker.Infrastructure/
COPY src/MeridianVoteTracker.Api/*.csproj src/MeridianVoteTracker.Api/
RUN dotnet restore src/MeridianVoteTracker.Api/MeridianVoteTracker.Api.csproj

COPY src/ src/
RUN dotnet publish src/MeridianVoteTracker.Api/MeridianVoteTracker.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "MeridianVoteTracker.Api.dll"]