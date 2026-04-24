# TaskFlow — Trello Clone

Полнофункциональный аналог Trello, построенный на **.NET 10 + Blazor WebAssembly + MudBlazor + PostgreSQL**.

## Архитектура

```
TrelloClone/
├── src/
│   ├── TrelloClone.Shared/       # Общие модели и DTO
│   ├── TrelloClone.Api/          # REST API + SignalR (ASP.NET Core)
│   └── TrelloClone.Client/       # Blazor WASM + MudBlazor (SPA)
├── docker-compose.yml            # PostgreSQL
└── TrelloClone.sln
```

## Функциональность

### Доски (Boards)
- Создание, редактирование, удаление досок
- Выбор цвета фона доски
- Список всех досок пользователя

### Колонки (Columns)
- Добавление, переименование, удаление колонок
- Перетаскивание колонок для изменения порядка
- Автоматическое создание колонок "To Do / In Progress / Done"

### Карточки (Cards)
- **Drag & Drop** перетаскивание между колонками
- Приоритеты: Low / Medium / High / Critical (цветовая индикация)
- Дата выполнения (подсветка просроченных)
- Редактирование описания

### Метки и теги (Labels)
- Создание цветных меток для доски
- Назначение/снятие меток на карточках
- Визуальные чипы на карточках

### Комментарии (Comments)
- Добавление комментариев к карточкам
- Имя автора и дата
- Удаление своих комментариев

### Чеклисты (Checklists)
- Добавление пунктов чеклиста к карточкам
- Отметка выполненных пунктов
- Прогресс-бар завершения

### Авторизация (Auth)
- Регистрация и вход по email/пароль
- JWT-токены (7 дней)
- Защищённые роуты

### Real-time обновления
- SignalR хаб для обновлений доски в реальном времени
- Синхронизация перемещений карточек между пользователями

---

## Быстрый старт

### 1. Требования
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (preview)
- [Docker](https://docs.docker.com/get-docker/) (для PostgreSQL)
- Или PostgreSQL установленный локально

### 2. Запуск PostgreSQL

```bash
docker-compose up -d
```

### 3. Применение миграций

```bash
cd src/TrelloClone.Api

# Установить EF Core tools если не установлены
dotnet tool install --global dotnet-ef

# Создать и применить миграцию
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 4. Запуск API

```bash
cd src/TrelloClone.Api
dotnet run
# → https://localhost:5001
# → Swagger: https://localhost:5001/swagger
```

### 5. Запуск клиента

```bash
# В отдельном терминале
cd src/TrelloClone.Client
dotnet run
# → https://localhost:5002
```

### 6. Готово!
Откройте `https://localhost:5002` в браузере.

---

## API Endpoints

### Auth
| Method | Endpoint             | Описание          |
|--------|---------------------|--------------------|
| POST   | /api/auth/register  | Регистрация        |
| POST   | /api/auth/login     | Авторизация        |

### Boards
| Method | Endpoint             | Описание                |
|--------|---------------------|-------------------------|
| GET    | /api/boards          | Список досок           |
| GET    | /api/boards/{id}     | Доска с колонками       |
| POST   | /api/boards          | Создать доску           |
| PUT    | /api/boards/{id}     | Обновить доску          |
| DELETE | /api/boards/{id}     | Удалить доску           |

### Columns
| Method | Endpoint                  | Описание            |
|--------|--------------------------|----------------------|
| POST   | /api/columns              | Создать колонку     |
| PUT    | /api/columns/{id}         | Переименовать       |
| PUT    | /api/columns/{id}/move    | Переместить         |
| DELETE | /api/columns/{id}         | Удалить             |

### Cards
| Method | Endpoint                                    | Описание              |
|--------|--------------------------------------------|-----------------------|
| POST   | /api/cards                                  | Создать карточку     |
| GET    | /api/cards/{id}                             | Получить карточку    |
| PUT    | /api/cards/{id}                             | Обновить             |
| PUT    | /api/cards/{id}/move                        | Переместить (D&D)    |
| DELETE | /api/cards/{id}                             | Удалить              |
| POST   | /api/cards/{id}/comments                    | Добавить комментарий |
| DELETE | /api/cards/{id}/comments/{commentId}        | Удалить комментарий  |
| POST   | /api/cards/{id}/checklist                   | Добавить пункт       |
| PUT    | /api/cards/{id}/checklist/{itemId}/toggle   | Переключить пункт    |

### Labels
| Method | Endpoint                    | Описание                |
|--------|-----------------------------|-------------------------|
| GET    | /api/labels/board/{boardId} | Метки доски             |
| POST   | /api/labels                 | Создать метку           |
| DELETE | /api/labels/{id}            | Удалить метку           |

---

## Технологии

| Слой        | Технология                        |
|-------------|----------------------------------|
| Frontend    | Blazor WebAssembly, MudBlazor 8  |
| Backend     | ASP.NET Core 10 Web API          |
| Real-time   | SignalR                          |
| Database    | PostgreSQL 16 + EF Core 10       |
| Auth        | JWT Bearer Tokens + BCrypt       |
| Storage     | Blazored.LocalStorage            |

---

## Конфигурация

### API (`src/TrelloClone.Api/appsettings.json`)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=trelloclone;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Key": "YourSecretKeyAtLeast32CharsLong!!",
    "Issuer": "TrelloClone.Api",
    "Audience": "TrelloClone.Client"
  },
  "ClientUrl": "https://localhost:5002"
}
```

### Client (`src/TrelloClone.Client/wwwroot/appsettings.json`)
```json
{
  "ApiBaseUrl": "https://localhost:5001"
}
```

---

## Структура базы данных

```
AppUser          1───∞  Board
Board            1───∞  BoardColumn
Board            1───∞  BoardMember
Board            1───∞  CardLabel
BoardColumn      1───∞  CardItem
CardItem         ∞───∞  CardLabel
CardItem         1───∞  CardComment
CardItem         1───∞  ChecklistItem
```

---

## Лицензия

MIT
