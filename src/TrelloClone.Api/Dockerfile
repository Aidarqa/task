# Этап 1: Сборка
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем весь исходный код в контейнер
COPY . .

# Восстанавливаем зависимости и собираем проект с указанием пути от корня
RUN dotnet restore "src/TrelloClone.Api/TrelloClone.Api.csproj"
RUN dotnet publish "src/TrelloClone.Api/TrelloClone.Api.csproj" -c Release -o /out

# Этап 2: Запуск
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .

# Открываем порт
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "TrelloClone.Api.dll"]
