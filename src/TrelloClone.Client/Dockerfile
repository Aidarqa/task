# Этап 1: Сборка WASM
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем весь исходный код (чтобы захватить Shared проекты, если они есть)
COPY . .

# Собираем клиент
RUN dotnet restore "src/TrelloClone.Client/TrelloClone.Client.csproj"
RUN dotnet publish "src/TrelloClone.Client/TrelloClone.Client.csproj" -c Release -o /app/publish

# Этап 2: Nginx
FROM nginx:alpine
WORKDIR /usr/share/nginx/html

# Копируем скомпилированные статические файлы
COPY --from=build /app/publish/wwwroot .
# Копируем наш кастомный конфиг Nginx
COPY src/TrelloClone.Client/nginx.conf /etc/nginx/nginx.conf

EXPOSE 80
