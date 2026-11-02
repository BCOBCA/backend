# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY BCOBCA.API/BCOBCA.API.csproj BCOBCA.API/
COPY BCOBCA.BLL/BCOBCA.BLL.csproj BCOBCA.BLL/
COPY BCOBCA.DAL/BCOBCA.DAL.csproj BCOBCA.DAL/
RUN dotnet restore BCOBCA.API/BCOBCA.API.csproj

COPY BCOBCA.API/ BCOBCA.API/
COPY BCOBCA.BLL/ BCOBCA.BLL/
COPY BCOBCA.DAL/ BCOBCA.DAL/
RUN dotnet publish BCOBCA.API/BCOBCA.API.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

# The SDK and EF CLI are needed only by the one-shot migration service.
FROM build AS migrate
COPY .config/dotnet-tools.json .config/dotnet-tools.json
RUN dotnet tool restore
ENTRYPOINT ["dotnet", "tool", "run", "dotnet-ef", "database", "update", "--project", "BCOBCA.DAL", "--startup-project", "BCOBCA.API", "--configuration", "Release", "--no-build"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "BCOBCA.API.dll"]
