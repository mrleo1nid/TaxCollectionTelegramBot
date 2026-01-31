# TaxCollectionTelegramBot

Telegram бот для организации сборов средств и управления конфигами пользователей.

## Возможности

### Для пользователей
- Просмотр своих конфигов (текстовые записи, добавленные админом)
- Участие в сборах средств
- Подтверждение/отказ от оплаты

### Для администратора
- Просмотр списка пользователей
- Добавление конфигов для пользователей
- Создание сборов средств с указанием суммы, описания и реквизитов
- Отслеживание статуса сбора
- Завершение сбора с автоматическим расчётом суммы на каждого участника

## Процесс сбора средств

1. Админ создаёт сбор, указывая сумму, цель и реквизиты
2. Все пользователи получают уведомление и выбирают "Участвую" / "Не участвую"
3. Админ завершает сбор - сумма делится на участников
4. Участники подтверждают или отказываются от оплаты
5. При отказе происходит перерасчёт для оставшихся участников
6. После подтверждения всеми - рассылка финальной суммы и реквизитов

## Требования

- .NET 8 SDK (для локальной разработки)
- Docker и Docker Compose (для запуска в контейнере)
- Telegram Bot Token (получить у [@BotFather](https://t.me/BotFather))

## Настройка

### 1. Создание бота

1. Напишите [@BotFather](https://t.me/BotFather) в Telegram
2. Отправьте `/newbot` и следуйте инструкциям
3. Сохраните полученный токен

### 2. Получение вашего Telegram ID

1. Напишите [@userinfobot](https://t.me/userinfobot) или [@getmyid_bot](https://t.me/getmyid_bot)
2. Сохраните ваш ID (число)

### 3. Настройка переменных окружения

```bash
cp .env.example .env
```

Отредактируйте файл `.env`:

```env
BOT_TOKEN=your_bot_token_here
ADMIN_ID=123456789
```

## Запуск

### Docker Compose (рекомендуется)

```bash
docker-compose up -d
```

Просмотр логов:

```bash
docker-compose logs -f
```

Остановка:

```bash
docker-compose down
```

### Локальная разработка

1. Настройте `appsettings.json`:

```json
{
  "BotConfiguration": {
    "Token": "YOUR_BOT_TOKEN",
    "AdminId": 123456789
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=data/bot.db"
  }
}
```

2. Запустите:

```bash
cd src/TaxCollectionTelegramBot
dotnet run
```

## Структура проекта

```
TaxCollectionTelegramBot/
├── src/
│   └── TaxCollectionTelegramBot/
│       ├── Data/
│       │   ├── Entities/          # Entity классы
│       │   └── AppDbContext.cs    # DbContext
│       ├── Handlers/              # Обработчики Telegram
│       ├── Services/              # Бизнес-логика
│       ├── Program.cs             # Точка входа
│       └── appsettings.json       # Конфигурация
├── Dockerfile
├── docker-compose.yml
└── README.md
```

## База данных

Используется SQLite. При запуске в Docker база хранится в volume `bot-data`.

Миграции применяются автоматически при старте приложения.

## Лицензия

MIT
