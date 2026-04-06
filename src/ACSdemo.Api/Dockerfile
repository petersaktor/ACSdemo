FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/ACSdemo.Api/ACSdemo.Api.csproj", "src/ACSdemo.Api/"]
RUN dotnet restore "src/ACSdemo.Api/ACSdemo.Api.csproj"
COPY . .
WORKDIR "/src/src/ACSdemo.Api"
RUN dotnet build "ACSdemo.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ACSdemo.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ACSdemo.Api.dll"]
