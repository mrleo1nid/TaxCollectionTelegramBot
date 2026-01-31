using Microsoft.Extensions.Options;
using TaxCollectionTelegramBot.Data.Entities;
using TaxCollectionTelegramBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TaxCollectionTelegramBot.Handlers;

public class MessageHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly UserService _userService;
    private readonly ConfigService _configService;
    private readonly CollectionService _collectionService;
    private readonly UserStateService _stateService;
    private readonly long _adminId;
    private readonly ILogger<MessageHandler> _logger;

    public MessageHandler(
        ITelegramBotClient bot,
        UserService userService,
        ConfigService configService,
        CollectionService collectionService,
        UserStateService stateService,
        IOptions<BotConfiguration> config,
        ILogger<MessageHandler> logger
    )
    {
        _bot = bot;
        _userService = userService;
        _configService = configService;
        _collectionService = collectionService;
        _stateService = stateService;
        _adminId = config.Value.AdminId;
        _logger = logger;
    }

    public async Task HandleAsync(Message message, CancellationToken ct)
    {
        if (message.Text == null)
            return;

        var userId = message.From!.Id;
        var chatId = message.Chat.Id;
        var text = message.Text;
        var isAdmin = userId == _adminId;

        _logger.LogInformation("Message from {UserId}: {Text}", userId, text);

        // Handle /start command
        if (text.StartsWith("/start"))
        {
            await HandleStartCommand(message, ct);
            return;
        }

        // Handle state-based input
        var state = _stateService.GetState(userId);
        if (state.State != UserState.None)
        {
            // Reply keyboard "Cancel" button sends this text
            if (text.Trim() == "❌ Отмена")
            {
                _stateService.ClearState(userId);
                await _bot.SendMessage(
                    chatId,
                    "Действие отменено.",
                    replyMarkup: KeyboardBuilder.RemoveReplyKeyboard(),
                    cancellationToken: ct
                );
                await ShowMainMenu(chatId, isAdmin, ct);
                return;
            }
            await HandleStateInput(chatId, userId, text, state, isAdmin, ct);
            return;
        }

        // Default: show menu
        await ShowMainMenu(chatId, isAdmin, ct);
    }

    private async Task HandleStartCommand(Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var chatId = message.Chat.Id;
        var username = message.From.Username;
        var firstName = message.From.FirstName;
        var isAdmin = userId == _adminId;

        var isNewUser = await _userService.IsNewUserAsync(userId, ct);
        await _userService.GetOrCreateUserAsync(userId, username, firstName, ct);

        if (isNewUser && !isAdmin)
        {
            // Notify admin about new user
            try
            {
                var userInfo = firstName ?? username ?? userId.ToString();
                var usernameInfo = username != null ? $" (@{username})" : "";
                await _bot.SendMessage(
                    _adminId,
                    $"👤 Новый пользователь: {userInfo}{usernameInfo}\nID: {userId}",
                    cancellationToken: ct
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify admin about new user");
            }
        }

        await ShowMainMenu(chatId, isAdmin, ct);
    }

    private async Task HandleStateInput(
        long chatId,
        long userId,
        string text,
        UserStateData state,
        bool isAdmin,
        CancellationToken ct
    )
    {
        switch (state.State)
        {
            case UserState.AwaitingConfigName:
                await HandleConfigNameInput(chatId, userId, text, state, ct);
                break;

            case UserState.AwaitingConfigText:
                await HandleConfigTextInput(chatId, userId, text, state, ct);
                break;

            case UserState.AwaitingConfigEditName:
                await HandleConfigEditNameInput(chatId, userId, text, state, ct);
                break;

            case UserState.AwaitingConfigEditText:
                await HandleConfigEditTextInput(chatId, userId, text, state, ct);
                break;

            case UserState.AwaitingCollectionAmount:
                await HandleCollectionAmountInput(chatId, userId, text, state, ct);
                break;

            case UserState.AwaitingCollectionDescription:
                await HandleCollectionDescriptionInput(chatId, userId, text, state, ct);
                break;

            case UserState.AwaitingCollectionPaymentDetails:
                await HandleCollectionPaymentDetailsInput(chatId, userId, text, state, ct);
                break;
        }
    }

    private async Task HandleConfigNameInput(
        long chatId,
        long userId,
        string text,
        UserStateData state,
        CancellationToken ct
    )
    {
        _stateService.UpdateState(
            userId,
            s =>
            {
                s.ConfigName = text.Trim();
                s.State = UserState.AwaitingConfigText;
            }
        );

        await _bot.SendMessage(
            chatId,
            "📄 Теперь введите текст конфига:",
            replyMarkup: KeyboardBuilder.CancelReplyKeyboard(),
            cancellationToken: ct
        );
    }

    private async Task HandleConfigTextInput(
        long chatId,
        long userId,
        string text,
        UserStateData state,
        CancellationToken ct
    )
    {
        if (state.SelectedUserId == null || state.ConfigName == null)
        {
            _stateService.ClearState(userId);
            await _bot.SendMessage(
                chatId,
                "❌ Ошибка. Попробуйте снова.",
                replyMarkup: KeyboardBuilder.RemoveReplyKeyboard(),
                cancellationToken: ct
            );
            await ShowMainMenu(chatId, userId == _adminId, ct);
            return;
        }

        await _configService.AddConfigAsync(
            state.SelectedUserId.Value,
            state.ConfigName,
            text.Trim(),
            ct
        );
        _stateService.ClearState(userId);

        var user = await _userService.GetUserAsync(state.SelectedUserId.Value, ct);
        var userName = user?.FirstName ?? user?.Username ?? state.SelectedUserId.Value.ToString();

        await _bot.SendMessage(
            chatId,
            $"✅ Конфиг \"{state.ConfigName}\" добавлен для {userName}.",
            replyMarkup: KeyboardBuilder.RemoveReplyKeyboard(),
            cancellationToken: ct
        );
        await ShowMainMenu(chatId, true, ct);

        // Notify the user about new config
        try
        {
            await _bot.SendMessage(
                state.SelectedUserId.Value,
                $"📢 Вам добавлен новый конфиг: {state.ConfigName}",
                replyMarkup: KeyboardBuilder.MainMenuUser(),
                cancellationToken: ct
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify user about new config");
        }
    }

    private async Task HandleConfigEditNameInput(
        long chatId,
        long userId,
        string text,
        UserStateData state,
        CancellationToken ct
    )
    {
        if (userId != _adminId || state.ConfigIdForEdit == null)
        {
            _stateService.ClearState(userId);
            await _bot.SendMessage(
                chatId,
                "❌ Ошибка. Попробуйте снова.",
                replyMarkup: KeyboardBuilder.RemoveReplyKeyboard(),
                cancellationToken: ct
            );
            await ShowMainMenu(chatId, true, ct);
            return;
        }

        var name = text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            await _bot.SendMessage(
                chatId,
                "❌ Введите непустое название.",
                replyMarkup: KeyboardBuilder.CancelReplyKeyboard(),
                cancellationToken: ct
            );
            return;
        }

        var updated = await _configService.UpdateConfigAsync(
            state.ConfigIdForEdit.Value,
            name,
            null,
            ct
        );
        _stateService.ClearState(userId);

        if (updated)
        {
            await _bot.SendMessage(
                chatId,
                "✅ Название конфига обновлено.",
                replyMarkup: KeyboardBuilder.RemoveReplyKeyboard(),
                cancellationToken: ct
            );
            await ShowMainMenu(chatId, true, ct);
        }
        else
        {
            await _bot.SendMessage(
                chatId,
                "❌ Конфиг не найден.",
                replyMarkup: KeyboardBuilder.RemoveReplyKeyboard(),
                cancellationToken: ct
            );
            await ShowMainMenu(chatId, true, ct);
        }
    }

    private async Task HandleConfigEditTextInput(
        long chatId,
        long userId,
        string text,
        UserStateData state,
        CancellationToken ct
    )
    {
        if (userId != _adminId || state.ConfigIdForEdit == null)
        {
            _stateService.ClearState(userId);
            await _bot.SendMessage(
                chatId,
                "❌ Ошибка. Попробуйте снова.",
                replyMarkup: KeyboardBuilder.RemoveReplyKeyboard(),
                cancellationToken: ct
            );
            await ShowMainMenu(chatId, true, ct);
            return;
        }

        var updated = await _configService.UpdateConfigAsync(
            state.ConfigIdForEdit.Value,
            null,
            text.Trim(),
            ct
        );
        _stateService.ClearState(userId);

        if (updated)
        {
            await _bot.SendMessage(
                chatId,
                "✅ Текст конфига обновлён.",
                replyMarkup: KeyboardBuilder.RemoveReplyKeyboard(),
                cancellationToken: ct
            );
            await ShowMainMenu(chatId, true, ct);
        }
        else
        {
            await _bot.SendMessage(
                chatId,
                "❌ Конфиг не найден.",
                replyMarkup: KeyboardBuilder.RemoveReplyKeyboard(),
                cancellationToken: ct
            );
            await ShowMainMenu(chatId, true, ct);
        }
    }

    private async Task HandleCollectionAmountInput(
        long chatId,
        long userId,
        string text,
        UserStateData state,
        CancellationToken ct
    )
    {
        if (
            !decimal.TryParse(
                text.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amount
            )
            || amount <= 0
        )
        {
            await _bot.SendMessage(
                chatId,
                "❌ Введите корректную сумму (например: 1500 или 1500.50):",
                replyMarkup: KeyboardBuilder.CancelReplyKeyboard(),
                cancellationToken: ct
            );
            return;
        }

        _stateService.UpdateState(
            userId,
            s =>
            {
                s.CollectionAmount = amount;
                s.State = UserState.AwaitingCollectionDescription;
            }
        );

        await _bot.SendMessage(
            chatId,
            "📝 Введите описание сбора (цель):",
            replyMarkup: KeyboardBuilder.CancelReplyKeyboard(),
            cancellationToken: ct
        );
    }

    private async Task HandleCollectionDescriptionInput(
        long chatId,
        long userId,
        string text,
        UserStateData state,
        CancellationToken ct
    )
    {
        _stateService.UpdateState(
            userId,
            s =>
            {
                s.CollectionDescription = text.Trim();
                s.State = UserState.AwaitingCollectionPaymentDetails;
            }
        );

        await _bot.SendMessage(
            chatId,
            "💳 Введите реквизиты для оплаты:",
            replyMarkup: KeyboardBuilder.CancelReplyKeyboard(),
            cancellationToken: ct
        );
    }

    private async Task HandleCollectionPaymentDetailsInput(
        long chatId,
        long userId,
        string text,
        UserStateData state,
        CancellationToken ct
    )
    {
        if (state.CollectionAmount == null || state.CollectionDescription == null)
        {
            _stateService.ClearState(userId);
            await _bot.SendMessage(
                chatId,
                "❌ Ошибка. Попробуйте снова.",
                replyMarkup: KeyboardBuilder.RemoveReplyKeyboard(),
                cancellationToken: ct
            );
            await ShowMainMenu(chatId, true, ct);
            return;
        }

        // Create collection
        var collection = await _collectionService.CreateCollectionAsync(
            state.CollectionAmount.Value,
            state.CollectionDescription,
            text.Trim(),
            ct
        );

        // Get all non-admin users and add them as participants (Pending)
        var users = await _userService.GetAllUsersExceptAdminAsync(_adminId, ct);
        if (users.Count > 0)
        {
            await _collectionService.AddParticipantsAsync(
                collection.Id,
                users.Select(u => u.TelegramId),
                ct
            );
        }

        // Admin is always a participant and counts as "participating" (included in sum split, auto-confirmed later)
        await _collectionService.AddParticipantWithStatusAsync(
            collection.Id,
            _adminId,
            ParticipantStatus.Participating,
            ct
        );

        // Notify all users about the collection
        var notifiedCount = 0;
        foreach (var user in users)
        {
            try
            {
                await _bot.SendMessage(
                    user.TelegramId,
                    $"💰 *Новый сбор!*\n\n"
                        + $"📝 Цель: {EscapeMarkdown(collection.Description)}\n"
                        + $"💵 Общая сумма: {collection.TotalAmount:F2}₽\n\n"
                        + $"Хотите участвовать?",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: KeyboardBuilder.CollectionParticipation(collection.Id),
                    cancellationToken: ct
                );
                notifiedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to notify user {UserId} about collection",
                    user.TelegramId
                );
            }
        }

        _stateService.ClearState(userId);
        await _bot.SendMessage(
            chatId,
            $"✅ Сбор создан!\n\n"
                + $"💰 Сумма: {collection.TotalAmount:F2}₽\n"
                + $"📝 Цель: {collection.Description}\n\n"
                + $"📢 Уведомлено пользователей: {notifiedCount}\n\n"
                + $"Когда все ответят, нажмите \"Завершить сбор\" для распределения суммы.",
            replyMarkup: KeyboardBuilder.RemoveReplyKeyboard(),
            cancellationToken: ct
        );
        await ShowMainMenu(chatId, true, ct);
    }

    private async Task ShowMainMenu(long chatId, bool isAdmin, CancellationToken ct)
    {
        Collection? activeCollection = null;
        if (isAdmin)
            activeCollection = await _collectionService.GetActiveCollectionAsync(ct);
        var hasActiveCollection = activeCollection != null;
        var keyboard = isAdmin
            ? KeyboardBuilder.MainMenuAdmin(hasActiveCollection, activeCollection)
            : KeyboardBuilder.MainMenuUser();
        var greeting = isAdmin ? "👋 Панель администратора" : "👋 Главное меню";
        await _bot.SendMessage(chatId, greeting, replyMarkup: keyboard, cancellationToken: ct);
    }

    private static string EscapeMarkdown(string text)
    {
        return text.Replace("_", "\\_")
            .Replace("*", "\\*")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("~", "\\~")
            .Replace("`", "\\`")
            .Replace(">", "\\>")
            .Replace("#", "\\#")
            .Replace("+", "\\+")
            .Replace("-", "\\-")
            .Replace("=", "\\=")
            .Replace("|", "\\|")
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace(".", "\\.")
            .Replace("!", "\\!");
    }
}
