FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY AmassOrchestrator.Web/AmassOrchestrator.Web.csproj AmassOrchestrator.Web/
RUN dotnet restore AmassOrchestrator.Web/AmassOrchestrator.Web.csproj
COPY AmassOrchestrator.Web/ AmassOrchestrator.Web/
RUN dotnet publish AmassOrchestrator.Web/AmassOrchestrator.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "AmassOrchestrator.Web.dll"]
