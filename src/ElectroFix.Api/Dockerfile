# Сборка образа для Render. Собирать из корня репозитория:
#   docker build -f src/ElectroFix.Api/Dockerfile -t electrofix-api .

# 1) сборка на полном SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# сначала только файлы проектов: пока они не менялись, Docker берёт restore из кэша
COPY src/ElectroFix.Shared/ElectroFix.Shared.csproj src/ElectroFix.Shared/
COPY src/ElectroFix.Api/ElectroFix.Api.csproj src/ElectroFix.Api/
RUN dotnet restore src/ElectroFix.Api/ElectroFix.Api.csproj

COPY src/ElectroFix.Shared/ src/ElectroFix.Shared/
COPY src/ElectroFix.Api/ src/ElectroFix.Api/
RUN dotnet publish src/ElectroFix.Api/ElectroFix.Api.csproj -c Release -o /app --no-restore

# 2) запуск на лёгком образе без SDK
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production
# порт задаёт Render через PORT (его читает Program.cs), 8080 — для запуска у себя
ENV PORT=8080
EXPOSE 8080

# не root: в образе .NET уже есть пользователь app
USER $APP_UID
ENTRYPOINT ["dotnet", "ElectroFix.Api.dll"]
