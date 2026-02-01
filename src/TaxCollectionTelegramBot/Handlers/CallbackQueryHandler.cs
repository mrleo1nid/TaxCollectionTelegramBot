using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TaxCollectionTelegramBot;
using TaxCollectionTelegramBot.Data.Entities;
using TaxCollectionTelegramBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TaxCollectionTelegramBot.Handlers;

public class CallbackQueryHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly UserService _userService;
    private readonly ConfigService _configService;
    private readonly CollectionService _collectionService;
    private readonly UserStateService _stateService;
    private readonly long _adminId;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly InstructionOptions _instructionOptions;
    private readonly ILogger<CallbackQueryHandler> _logger;

    public CallbackQueryHandler(
        ITelegramBotClient bot,
        UserService userService,
        ConfigService configService,
        CollectionService collectionService,
        UserStateService stateService,
        IOptions<BotConfiguration> config,
        IHostEnvironment hostEnvironment,
        IOptions<InstructionOptions> instructionOptions,
        ILogger<CallbackQueryHandler> logger
    )
    {
        _bot = bot;
        _userService = userService;
        _configService = configService;
        _collectionService = collectionService;
        _stateService = stateService;
        _adminId = config.Value.AdminId;
        _hostEnvironment = hostEnvironment;
        _instructionOptions = instructionOptions.Value;
        _logger = logger;
    }

    public async Task HandleAsync(CallbackQuery query, CancellationToken ct)
    {
        if (query.Message == null || query.Data == null)
            return;

        var userId = query.From.Id;
        var chatId = query.Message.Chat.Id;
        var data = query.Data;
        var isAdmin = userId == _adminId;

        _logger.LogInformation("Callback from {UserId}: {Data}", userId, data);

        try
        {
            await _bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);

            var message = query.Message;

            if (data == "cancel")
            {
                _stateService.ClearState(userId);
                await _bot.SendMessage(
                    message.Chat.Id,
                    "Действие отменено.",
                    replyMarkup: KeyboardBuilder.RemoveReplyKeyboard(),
                    cancellationToken: ct
                );
                await ShowMainMenu(message.Chat.Id, isAdmin, ct, message);
                return;
            }

            var parts = data.Split(':');
            var category = parts[0];

            switch (category)
            {
                case "user":
                    await HandleUserCallback(chatId, userId, parts, message, ct);
                    break;
                case "admin" when isAdmin:
                    await HandleAdminCallback(chatId, userId, parts, message, ct);
                    break;
                case "config":
                    await HandleConfigCallback(chatId, userId, isAdmin, parts, message, ct);
                    break;
                case "collection":
                    await HandleCollectionCallback(chatId, userId, parts, message, ct);
                    break;
                case "selectuser" when isAdmin:
                    await HandleUserSelectionCallback(chatId, userId, parts, message, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling callback {Data}", data);
        }
    }

    private async Task EditMenuAsync(
        ChatId chatId,
        int messageId,
        string text,
        InlineKeyboardMarkup? replyMarkup,
        ParseMode? parseMode = null,
        CancellationToken ct = default
    )
    {
        try
        {
            await _bot.EditMessageText(
                chatId,
                messageId,
                text,
                parseMode: parseMode ?? default,
                replyMarkup: replyMarkup,
                cancellationToken: ct
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EditMessageText failed, messageId={MessageId}", messageId);
        }
    }

    private async Task HandleUserCallback(
        long chatId,
        long userId,
        string[] parts,
        Message message,
        CancellationToken ct
    )
    {
        if (parts.Length < 2)
            return;

        var chatIdTyped = message.Chat.Id;
        var messageId = message.MessageId;

        switch (parts[1])
        {
            case "menu":
                await ShowMainMenu(chatIdTyped, false, ct, message);
                break;
            case "configs":
                var configs = await _configService.GetUserConfigsAsync(userId, ct);
                if (configs.Count == 0)
                {
                    await EditMenuAsync(
                        chatIdTyped,
                        messageId,
                        "У вас пока нет конфигов.",
                        KeyboardBuilder.BackToMainMenu(false),
                        null,
                        ct
                    );
                }
                else
                {
                    await EditMenuAsync(
                        chatIdTyped,
                        messageId,
                        "Ваши конфиги:",
                        KeyboardBuilder.ConfigList(configs, false),
                        null,
                        ct
                    );
                }
                break;
            case "instruction":
                var instructionPath =
                    string.IsNullOrWhiteSpace(_instructionOptions.FilePath)
                        ? Path.Combine(_hostEnvironment.ContentRootPath, "instruction.txt")
                    : Path.IsPathRooted(_instructionOptions.FilePath) ? _instructionOptions.FilePath
                    : Path.Combine(_hostEnvironment.ContentRootPath, _instructionOptions.FilePath);
                string instructionText;
                try
                {
                    instructionText = await File.ReadAllTextAsync(instructionPath, ct);
                    if (string.IsNullOrWhiteSpace(instructionText))
                        instructionText = "Инструкция временно недоступна.";
                    else if (instructionText.Length > 4096)
                        instructionText = instructionText[..4096];
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Could not read instruction file from {Path}",
                        instructionPath
                    );
                    instructionText = "Инструкция временно недоступна.";
                }
                await EditMenuAsync(
                    chatIdTyped,
                    messageId,
                    instructionText,
                    KeyboardBuilder.BackToMainMenu(false),
                    null,
                    ct
                );
                break;
        }
    }

    private async Task HandleAdminCallback(
        long chatId,
        long userId,
        string[] parts,
        Message message,
        CancellationToken ct
    )
    {
        if (parts.Length < 2)
            return;

        var chatIdTyped = message.Chat.Id;
        var messageId = message.MessageId;

        switch (parts[1])
        {
            case "menu":
                _stateService.ClearState(userId);
                await ShowMainMenu(chatIdTyped, true, ct, message);
                break;

            case "users":
                var users = await _userService.GetAllUsersExceptAdminAsync(_adminId, ct);
                if (users.Count == 0)
                {
                    await EditMenuAsync(
                        chatIdTyped,
                        messageId,
                        "Пользователей пока нет.",
                        KeyboardBuilder.BackToMainMenu(true),
                        null,
                        ct
                    );
                }
                else
                {
                    var userList = string.Join(
                        "\n",
                        users.Select(u =>
                            $"• {u.FirstName ?? "Без имени"} (@{u.Username ?? "нет"}) - ID: {u.TelegramId}"
                        )
                    );
                    await EditMenuAsync(
                        chatIdTyped,
                        messageId,
                        $"📋 Пользователи:\n\n{userList}",
                        KeyboardBuilder.BackToMainMenu(true),
                        null,
                        ct
                    );
                }
                break;

            case "add_config":
                var usersForConfig = await _userService.GetAllUsersExceptAdminAsync(_adminId, ct);
                if (usersForConfig.Count == 0)
                {
                    await EditMenuAsync(
                        chatIdTyped,
                        messageId,
                        "Нет пользователей для добавления конфига.",
                        KeyboardBuilder.BackToMainMenu(true),
                        null,
                        ct
                    );
                }
                else
                {
                    _stateService.SetState(userId, UserState.AwaitingConfigUserSelection);
                    await EditMenuAsync(
                        chatIdTyped,
                        messageId,
                        "Выберите пользователя:",
                        KeyboardBuilder.UserList(usersForConfig, "selectuser"),
                        null,
                        ct
                    );
                }
                break;

            case "broadcast":
                _stateService.SetState(userId, UserState.AwaitingBroadcastMessage);
                await _bot.SendMessage(
                    chatIdTyped,
                    "Введите текст уведомления для всех пользователей.",
                    replyMarkup: KeyboardBuilder.CancelReplyKeyboard(),
                    cancellationToken: ct
                );
                break;

            case "user_configs":
                if (parts.Length == 2)
                {
                    var usersForView = await _userService.GetAllUsersExceptAdminAsync(_adminId, ct);
                    if (usersForView.Count == 0)
                    {
                        await EditMenuAsync(
                            chatIdTyped,
                            messageId,
                            "Пользователей пока нет.",
                            KeyboardBuilder.BackToMainMenu(true),
                            null,
                            ct
                        );
                    }
                    else
                    {
                        await EditMenuAsync(
                            chatIdTyped,
                            messageId,
                            "Выберите пользователя для просмотра конфигов:",
                            KeyboardBuilder.UserList(usersForView, "admin:user_configs"),
                            null,
                            ct
                        );
                    }
                }
                else if (parts.Length >= 3 && long.TryParse(parts[2], out var targetUserId))
                {
                    var userConfigs = await _configService.GetUserConfigsAsync(targetUserId, ct);
                    var targetUser = await _userService.GetUserAsync(targetUserId, ct);
                    var displayName =
                        targetUser?.FirstName ?? targetUser?.Username ?? targetUserId.ToString();
                    if (userConfigs.Count == 0)
                    {
                        await EditMenuAsync(
                            chatIdTyped,
                            messageId,
                            $"У пользователя {displayName} нет конфигов.",
                            KeyboardBuilder.BackToMainMenu(true),
                            null,
                            ct
                        );
                    }
                    else
                    {
                        await EditMenuAsync(
                            chatIdTyped,
                            messageId,
                            $"📋 Конфиги пользователя {displayName}:",
                            KeyboardBuilder.ConfigList(userConfigs, true, targetUserId),
                            null,
                            ct
                        );
                    }
                }
                break;

            case "start_collection":
                var activeCollection = await _collectionService.GetActiveCollectionAsync(ct);
                if (activeCollection != null)
                {
                    await EditMenuAsync(
                        chatIdTyped,
                        messageId,
                        "Уже есть активный сбор. Завершите его перед созданием нового.",
                        KeyboardBuilder.BackToMainMenu(true),
                        null,
                        ct
                    );
                    return;
                }
                _stateService.SetState(userId, UserState.AwaitingCollectionAmount);
                await _bot.SendMessage(
                    chatIdTyped,
                    "💰 Введите сумму сбора:",
                    replyMarkup: KeyboardBuilder.CancelReplyKeyboard(),
                    cancellationToken: ct
                );
                break;

            case "collection_status":
                await ShowCollectionStatus(chatIdTyped, ct, message);
                break;

            case "finalize_collection":
                await FinalizeCollection(chatIdTyped, ct, message);
                break;

            case "move_to_payment":
                await MoveToPaymentStage(chatIdTyped, ct, message);
                break;

            case "last_collection_results":
                await ShowLastCollectionResults(chatIdTyped, ct, message);
                break;

            case "cancel_collection":
                await CancelCollection(chatIdTyped, ct, message);
                break;
        }
    }

    private async Task CancelCollection(
        ChatId chatId,
        CancellationToken ct,
        Message? messageToEdit = null
    )
    {
        var collection = await _collectionService.GetActiveCollectionAsync(ct);
        if (collection == null)
        {
            var noCollectionText = "Нет активного сбора.";
            if (messageToEdit != null)
            {
                await EditMenuAsync(
                    messageToEdit.Chat.Id,
                    messageToEdit.MessageId,
                    noCollectionText,
                    KeyboardBuilder.BackToMainMenu(true),
                    null,
                    ct
                );
            }
            else
            {
                await _bot.SendMessage(
                    chatId,
                    noCollectionText,
                    replyMarkup: KeyboardBuilder.BackToMainMenu(true),
                    cancellationToken: ct
                );
            }
            return;
        }

        var participants = await _collectionService.GetAllParticipantsAsync(collection.Id, ct);
        await _collectionService.CancelCollectionAsync(collection.Id, ct);

        foreach (var participant in participants)
        {
            try
            {
                var isAdmin = participant.UserId == _adminId;
                await _bot.SendMessage(
                    participant.UserId,
                    $"❌ Сбор \"{EscapeMarkdown(collection.Description)}\" отменён администратором.",
                    parseMode: ParseMode.Markdown,
                    replyMarkup: isAdmin
                        ? KeyboardBuilder.MainMenuAdmin(hasActiveCollection: false)
                        : KeyboardBuilder.MainMenuUser(),
                    cancellationToken: ct
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to notify user {UserId} about collection cancellation",
                    participant.UserId
                );
            }
        }

        var successText = "✅ Сбор отменён.";
        if (messageToEdit != null)
        {
            await EditMenuAsync(
                messageToEdit.Chat.Id,
                messageToEdit.MessageId,
                successText,
                KeyboardBuilder.BackToMainMenu(true),
                null,
                ct
            );
        }
        else
        {
            await _bot.SendMessage(
                chatId,
                successText,
                replyMarkup: KeyboardBuilder.BackToMainMenu(true),
                cancellationToken: ct
            );
        }
    }

    private async Task MoveToPaymentStage(
        ChatId chatId,
        CancellationToken ct,
        Message? messageToEdit = null
    )
    {
        var collection = await _collectionService.GetActiveCollectionAsync(ct);
        if (collection == null || collection.Status != CollectionStatus.AwaitingConfirmation)
        {
            var msg =
                collection == null
                    ? "Нет активного сбора."
                    : "Сбор не на этапе подтверждения. Дождитесь подтверждений всех участников.";
            if (messageToEdit != null)
            {
                await EditMenuAsync(
                    messageToEdit.Chat.Id,
                    messageToEdit.MessageId,
                    msg,
                    KeyboardBuilder.BackToMainMenu(true),
                    null,
                    ct
                );
            }
            else
            {
                await _bot.SendMessage(
                    chatId,
                    msg,
                    replyMarkup: KeyboardBuilder.BackToMainMenu(true),
                    cancellationToken: ct
                );
            }
            return;
        }

        var moved = await _collectionService.MoveToAwaitingPaymentAsync(collection.Id, ct);
        if (moved == null)
            return;

        var participants = await _collectionService.GetParticipatingUsersAsync(collection.Id, ct);
        foreach (var participant in participants)
        {
            if (participant.UserId == _adminId)
                continue;
            try
            {
                await _bot.SendMessage(
                    participant.UserId,
                    $"💳 *Реквизиты для оплаты*\n\n"
                        + $"Сбор: {EscapeMarkdown(collection.Description)}\n"
                        + $"Ваша сумма: *{participant.AmountToPay:F2}₽*\n\n"
                        + $"Реквизиты:\n`{EscapeMarkdown(collection.PaymentDetails)}`\n\n"
                        + $"Нажмите «Я оплатил», когда оплатите.",
                    parseMode: ParseMode.Markdown,
                    replyMarkup: KeyboardBuilder.CollectionPaid(collection.Id),
                    cancellationToken: ct
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify user {UserId}", participant.UserId);
            }
        }

        var adminCollection = await _collectionService.GetActiveCollectionAsync(ct);
        var successText = "💳 Сбор перешёл к этапу оплаты. Реквизиты отправлены участникам.";
        if (messageToEdit != null)
        {
            await EditMenuAsync(
                messageToEdit.Chat.Id,
                messageToEdit.MessageId,
                successText,
                KeyboardBuilder.MainMenuAdmin(true, adminCollection),
                null,
                ct
            );
        }
        else
        {
            await _bot.SendMessage(
                chatId,
                successText,
                replyMarkup: KeyboardBuilder.MainMenuAdmin(true, adminCollection),
                cancellationToken: ct
            );
        }
    }

    private async Task ShowLastCollectionResults(
        ChatId chatId,
        CancellationToken ct,
        Message? messageToEdit = null
    )
    {
        var collection = await _collectionService.GetLastCompletedCollectionAsync(ct);
        if (collection == null)
        {
            var text = "Нет завершённых сборов.";
            if (messageToEdit != null)
            {
                await EditMenuAsync(
                    messageToEdit.Chat.Id,
                    messageToEdit.MessageId,
                    text,
                    KeyboardBuilder.BackToMainMenu(true),
                    null,
                    ct
                );
            }
            else
            {
                await _bot.SendMessage(
                    chatId,
                    text,
                    replyMarkup: KeyboardBuilder.BackToMainMenu(true),
                    cancellationToken: ct
                );
            }
            return;
        }

        var participants = await _collectionService.GetAllParticipantsAsync(collection.Id, ct);
        var participantsList = string.Join(
            "\n",
            participants.Select(p =>
            {
                var statusText = p.Status switch
                {
                    ParticipantStatus.Pending => "⏳ ожидал",
                    ParticipantStatus.Participating => "✅ участвовал",
                    ParticipantStatus.Declined => "❌ отказался",
                    ParticipantStatus.Confirmed => "💰 подтвердил",
                    ParticipantStatus.DeclinedPayment => "🚫 отказался от оплаты",
                    ParticipantStatus.Paid => "✅ оплатил",
                    _ => "❓",
                };
                var amount = p.AmountToPay > 0 ? $" — {p.AmountToPay:F2}₽" : "";
                return $"• {p.User.FirstName ?? p.User.Username ?? p.UserId.ToString()}{amount} — {statusText}";
            })
        );

        var resultMessage =
            $"📋 *Результаты последнего сбора*\n\n"
            + $"📝 {EscapeMarkdown(collection.Description)}\n"
            + $"💰 Сумма: {collection.TotalAmount:F2}₽\n"
            + $"📅 {collection.CreatedAt:dd.MM.yyyy HH:mm}\n\n"
            + $"👥 Участники:\n{participantsList}";

        if (messageToEdit != null)
        {
            await EditMenuAsync(
                messageToEdit.Chat.Id,
                messageToEdit.MessageId,
                resultMessage,
                KeyboardBuilder.BackToMainMenu(true),
                ParseMode.Markdown,
                ct
            );
        }
        else
        {
            await _bot.SendMessage(
                chatId,
                resultMessage,
                parseMode: ParseMode.Markdown,
                replyMarkup: KeyboardBuilder.BackToMainMenu(true),
                cancellationToken: ct
            );
        }
    }

    private async Task HandleConfigCallback(
        long chatId,
        long userId,
        bool isAdmin,
        string[] parts,
        Message message,
        CancellationToken ct
    )
    {
        if (parts.Length < 3)
            return;

        var action = parts[1];
        if (!int.TryParse(parts[2], out var configId))
            return;

        long? ownerUserId = null;
        if (parts.Length >= 4 && long.TryParse(parts[3], out var ou))
            ownerUserId = ou;

        var chatIdTyped = message.Chat.Id;
        var messageId = message.MessageId;

        switch (action)
        {
            case "view":
                var config = await _configService.GetConfigByIdAsync(configId, ct);
                if (config == null)
                {
                    await EditMenuAsync(
                        chatIdTyped,
                        messageId,
                        "Конфиг не найден.",
                        KeyboardBuilder.BackToMainMenu(isAdmin),
                        null,
                        ct
                    );
                    return;
                }

                // Check access
                if (!isAdmin && config.UserId != userId)
                {
                    await EditMenuAsync(
                        chatIdTyped,
                        messageId,
                        "У вас нет доступа к этому конфигу.",
                        KeyboardBuilder.BackToMainMenu(isAdmin),
                        null,
                        ct
                    );
                    return;
                }

                await EditMenuAsync(
                    chatIdTyped,
                    messageId,
                    $"📄 *{EscapeMarkdown(config.Name)}*\n\n`{EscapeMarkdown(config.ConfigText)}`",
                    KeyboardBuilder.ConfigActions(configId, isAdmin, ownerUserId),
                    ParseMode.Markdown,
                    ct
                );
                break;

            case "delete" when isAdmin:
                var configToDelete = await _configService.GetConfigByIdAsync(configId, ct);
                long? deletedOwnerId = configToDelete != null ? configToDelete.UserId : ownerUserId;
                await _configService.DeleteConfigAsync(configId, ct);
                if (deletedOwnerId.HasValue)
                {
                    var remainingConfigs = await _configService.GetUserConfigsAsync(
                        deletedOwnerId.Value,
                        ct
                    );
                    var targetUser = await _userService.GetUserAsync(deletedOwnerId.Value, ct);
                    var displayName =
                        targetUser?.FirstName
                        ?? targetUser?.Username
                        ?? deletedOwnerId.Value.ToString();
                    await EditMenuAsync(
                        chatIdTyped,
                        messageId,
                        $"✅ Конфиг удалён.\n\n📋 Конфиги пользователя {displayName}:",
                        remainingConfigs.Count > 0
                            ? KeyboardBuilder.ConfigList(
                                remainingConfigs,
                                true,
                                deletedOwnerId.Value
                            )
                            : KeyboardBuilder.BackToMainMenu(true),
                        null,
                        ct
                    );
                }
                else
                {
                    await EditMenuAsync(
                        chatIdTyped,
                        messageId,
                        "✅ Конфиг удалён.",
                        KeyboardBuilder.BackToMainMenu(true),
                        null,
                        ct
                    );
                }
                break;

            case "edit_name" when isAdmin:
                _stateService.UpdateState(
                    userId,
                    s =>
                    {
                        s.ConfigIdForEdit = configId;
                        s.State = UserState.AwaitingConfigEditName;
                    }
                );
                await _bot.SendMessage(
                    chatIdTyped,
                    "📝 Введите новое название конфига:",
                    replyMarkup: KeyboardBuilder.CancelReplyKeyboard(),
                    cancellationToken: ct
                );
                break;

            case "edit_text" when isAdmin:
                _stateService.UpdateState(
                    userId,
                    s =>
                    {
                        s.ConfigIdForEdit = configId;
                        s.State = UserState.AwaitingConfigEditText;
                    }
                );
                await _bot.SendMessage(
                    chatIdTyped,
                    "📄 Введите новый текст конфига:",
                    replyMarkup: KeyboardBuilder.CancelReplyKeyboard(),
                    cancellationToken: ct
                );
                break;
        }
    }

    private async Task HandleCollectionCallback(
        long chatId,
        long userId,
        string[] parts,
        Message message,
        CancellationToken ct
    )
    {
        if (parts.Length < 3)
            return;

        var action = parts[1];
        if (!int.TryParse(parts[2], out var collectionId))
            return;

        var chatIdTyped = message.Chat.Id;
        var messageId = message.MessageId;

        var participant = await _collectionService.GetParticipantAsync(collectionId, userId, ct);
        if (participant == null)
        {
            await EditMenuAsync(
                chatIdTyped,
                messageId,
                "Вы не участвуете в этом сборе.",
                null,
                null,
                ct
            );
            return;
        }

        var collection = await _collectionService.GetActiveCollectionAsync(ct);
        if (collection == null || collection.Id != collectionId)
        {
            await EditMenuAsync(
                chatIdTyped,
                messageId,
                "Этот сбор уже завершён или отменён.",
                null,
                null,
                ct
            );
            return;
        }

        switch (action)
        {
            case "join":
                if (collection.Status != CollectionStatus.Pending)
                {
                    await EditMenuAsync(
                        chatIdTyped,
                        messageId,
                        "Сбор уже завершён.",
                        null,
                        null,
                        ct
                    );
                    return;
                }
                await _collectionService.UpdateParticipantStatusAsync(
                    participant.Id,
                    ParticipantStatus.Participating,
                    ct
                );
                await EditMenuAsync(
                    chatIdTyped,
                    messageId,
                    "✅ Вы записаны как участник сбора.",
                    null,
                    null,
                    ct
                );
                await NotifyAdminAboutParticipation(userId, true, ct);
                await TryAutoAdvanceFromPendingAsync(collectionId, ct);
                break;

            case "decline":
                if (collection.Status != CollectionStatus.Pending)
                {
                    await EditMenuAsync(
                        chatIdTyped,
                        messageId,
                        "Сбор уже завершён.",
                        null,
                        null,
                        ct
                    );
                    return;
                }
                await _collectionService.UpdateParticipantStatusAsync(
                    participant.Id,
                    ParticipantStatus.Declined,
                    ct
                );
                await EditMenuAsync(
                    chatIdTyped,
                    messageId,
                    "❌ Вы отказались от участия в сборе.",
                    null,
                    null,
                    ct
                );
                await NotifyAdminAboutParticipation(userId, false, ct);
                await TryAutoAdvanceFromPendingAsync(collectionId, ct);
                break;

            case "confirm":
                if (collection.Status != CollectionStatus.AwaitingConfirmation)
                {
                    await EditMenuAsync(
                        chatIdTyped,
                        messageId,
                        "Сбор не ожидает подтверждения.",
                        null,
                        null,
                        ct
                    );
                    return;
                }
                await _collectionService.UpdateParticipantStatusAsync(
                    participant.Id,
                    ParticipantStatus.Confirmed,
                    ct
                );
                await EditMenuAsync(
                    chatIdTyped,
                    messageId,
                    "✅ Вы подтвердили оплату.",
                    null,
                    null,
                    ct
                );
                await CheckAndCompleteCollection(collectionId, ct);
                break;

            case "reject":
                if (collection.Status != CollectionStatus.AwaitingConfirmation)
                {
                    await EditMenuAsync(
                        chatIdTyped,
                        messageId,
                        "Сбор не ожидает подтверждения.",
                        null,
                        null,
                        ct
                    );
                    return;
                }
                await _collectionService.UpdateParticipantStatusAsync(
                    participant.Id,
                    ParticipantStatus.DeclinedPayment,
                    ct
                );
                await EditMenuAsync(
                    chatIdTyped,
                    messageId,
                    "❌ Вы отказались от оплаты.",
                    null,
                    null,
                    ct
                );
                await CheckAndCompleteCollection(collectionId, ct);
                break;

            case "paid":
                if (collection.Status != CollectionStatus.AwaitingPayment)
                {
                    await EditMenuAsync(
                        chatIdTyped,
                        messageId,
                        "Сбор не на этапе ожидания оплаты.",
                        null,
                        null,
                        ct
                    );
                    return;
                }
                if (participant.Status != ParticipantStatus.Confirmed)
                {
                    await EditMenuAsync(
                        chatIdTyped,
                        messageId,
                        participant.Status == ParticipantStatus.Paid
                            ? "Вы уже отметили оплату."
                            : "Вы не подтверждали оплату в этом сборе.",
                        null,
                        null,
                        ct
                    );
                    return;
                }
                await _collectionService.UpdateParticipantStatusAsync(
                    participant.Id,
                    ParticipantStatus.Paid,
                    ct
                );
                await EditMenuAsync(
                    chatIdTyped,
                    messageId,
                    "✅ Вы отметили: Я оплатил.",
                    null,
                    null,
                    ct
                );
                await CheckAndCompleteFromAwaitingPaymentAsync(collectionId, ct);
                break;
        }
    }

    private async Task HandleUserSelectionCallback(
        long chatId,
        long userId,
        string[] parts,
        Message message,
        CancellationToken ct
    )
    {
        if (parts.Length < 2)
            return;

        if (!long.TryParse(parts[1], out var selectedUserId))
            return;

        var state = _stateService.GetState(userId);
        if (state.State != UserState.AwaitingConfigUserSelection)
            return;

        _stateService.UpdateState(
            userId,
            s =>
            {
                s.SelectedUserId = selectedUserId;
                s.State = UserState.AwaitingConfigText;
            }
        );

        await _bot.SendMessage(
            message.Chat.Id,
            "📄 Введите текст конфига (имя будет взято из части после #):",
            replyMarkup: KeyboardBuilder.CancelReplyKeyboard(),
            cancellationToken: ct
        );
    }

    private async Task ShowMainMenu(
        ChatId chatId,
        bool isAdmin,
        CancellationToken ct,
        Message? messageToEdit = null
    )
    {
        Collection? activeCollection = null;
        if (isAdmin)
            activeCollection = await _collectionService.GetActiveCollectionAsync(ct);
        var hasActiveCollection = activeCollection != null;
        var keyboard = isAdmin
            ? KeyboardBuilder.MainMenuAdmin(hasActiveCollection, activeCollection)
            : KeyboardBuilder.MainMenuUser();
        var greeting = isAdmin ? "👋 Панель администратора" : "👋 Главное меню";

        if (messageToEdit != null)
        {
            await EditMenuAsync(
                messageToEdit.Chat.Id,
                messageToEdit.MessageId,
                greeting,
                keyboard,
                null,
                ct
            );
        }
        else
        {
            await _bot.SendMessage(chatId, greeting, replyMarkup: keyboard, cancellationToken: ct);
        }
    }

    private async Task ShowCollectionStatus(
        ChatId chatId,
        CancellationToken ct,
        Message? messageToEdit = null
    )
    {
        var collection = await _collectionService.GetActiveCollectionAsync(ct);
        if (collection == null)
        {
            if (messageToEdit != null)
            {
                await EditMenuAsync(
                    messageToEdit.Chat.Id,
                    messageToEdit.MessageId,
                    "Нет активного сбора.",
                    KeyboardBuilder.BackToMainMenu(true),
                    null,
                    ct
                );
            }
            else
            {
                await _bot.SendMessage(
                    chatId,
                    "Нет активного сбора.",
                    replyMarkup: KeyboardBuilder.BackToMainMenu(true),
                    cancellationToken: ct
                );
            }
            return;
        }

        var statusText = collection.Status switch
        {
            CollectionStatus.Pending => "⏳ Идёт опрос участников",
            CollectionStatus.AwaitingConfirmation => "💳 Ожидание подтверждения оплаты",
            CollectionStatus.AwaitingPayment => "💵 Ожидание отметки «Я оплатил»",
            _ => collection.Status.ToString(),
        };

        var participants = await _collectionService.GetAllParticipantsAsync(collection.Id, ct);
        var participantsList = string.Join(
            "\n",
            participants.Select(p =>
            {
                var statusEmoji = p.Status switch
                {
                    ParticipantStatus.Pending => "⏳",
                    ParticipantStatus.Participating => "✅",
                    ParticipantStatus.Declined => "❌",
                    ParticipantStatus.Confirmed => "💰",
                    ParticipantStatus.DeclinedPayment => "🚫",
                    ParticipantStatus.Paid => "💵",
                    _ => "❓",
                };
                var amount = p.AmountToPay > 0 ? $" - {p.AmountToPay:F2}₽" : "";
                return $"{statusEmoji} {p.User.FirstName ?? p.User.Username ?? p.UserId.ToString()}{amount}";
            })
        );

        var statusMessage =
            $"📊 *Статус сбора*\n\n"
            + $"💰 Сумма: {collection.TotalAmount:F2}₽\n"
            + $"📝 Описание: {EscapeMarkdown(collection.Description)}\n"
            + $"📌 Статус: {statusText}\n\n"
            + $"👥 Участники:\n{participantsList}";

        if (messageToEdit != null)
        {
            await EditMenuAsync(
                messageToEdit.Chat.Id,
                messageToEdit.MessageId,
                statusMessage,
                KeyboardBuilder.BackToMainMenu(true),
                ParseMode.Markdown,
                ct
            );
        }
        else
        {
            await _bot.SendMessage(
                chatId,
                statusMessage,
                parseMode: ParseMode.Markdown,
                replyMarkup: KeyboardBuilder.BackToMainMenu(true),
                cancellationToken: ct
            );
        }
    }

    private async Task FinalizeCollection(
        ChatId chatId,
        CancellationToken ct,
        Message? messageToEdit = null
    )
    {
        var collection = await _collectionService.GetActiveCollectionAsync(ct);
        if (collection == null)
        {
            if (messageToEdit != null)
            {
                await EditMenuAsync(
                    messageToEdit.Chat.Id,
                    messageToEdit.MessageId,
                    "Нет активного сбора.",
                    KeyboardBuilder.BackToMainMenu(true),
                    null,
                    ct
                );
            }
            else
            {
                await _bot.SendMessage(
                    chatId,
                    "Нет активного сбора.",
                    replyMarkup: KeyboardBuilder.BackToMainMenu(true),
                    cancellationToken: ct
                );
            }
            return;
        }

        if (collection.Status == CollectionStatus.AwaitingPayment)
        {
            var allParts = await _collectionService.GetAllParticipantsAsync(collection.Id, ct);
            var participantsToNotify = allParts
                .Where(p =>
                    p.Status != ParticipantStatus.Declined
                    && p.Status != ParticipantStatus.DeclinedPayment
                )
                .ToList();
            await _collectionService.CompleteCollectionAsync(collection.Id, ct);
            foreach (var p in participantsToNotify)
            {
                try
                {
                    await _bot.SendMessage(
                        p.UserId,
                        "✅ *Сбор завершён!* Администратор закрыл сбор.",
                        parseMode: ParseMode.Markdown,
                        cancellationToken: ct
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify user {UserId}", p.UserId);
                }
            }
            var completionMessage =
                $"✅ Сбор \"{collection.Description}\" завершён администратором.";
            if (messageToEdit != null)
            {
                await EditMenuAsync(
                    messageToEdit.Chat.Id,
                    messageToEdit.MessageId,
                    completionMessage,
                    KeyboardBuilder.MainMenuAdmin(false, null),
                    null,
                    ct
                );
            }
            else
            {
                await _bot.SendMessage(
                    chatId,
                    completionMessage,
                    replyMarkup: KeyboardBuilder.MainMenuAdmin(false, null),
                    cancellationToken: ct
                );
            }
            return;
        }

        if (collection.Status != CollectionStatus.Pending)
        {
            if (messageToEdit != null)
            {
                await EditMenuAsync(
                    messageToEdit.Chat.Id,
                    messageToEdit.MessageId,
                    "Сбор уже завершён или ожидает подтверждения.",
                    KeyboardBuilder.BackToMainMenu(true),
                    null,
                    ct
                );
            }
            else
            {
                await _bot.SendMessage(
                    chatId,
                    "Сбор уже завершён или ожидает подтверждения.",
                    replyMarkup: KeyboardBuilder.BackToMainMenu(true),
                    cancellationToken: ct
                );
            }
            return;
        }

        var finalized = await _collectionService.FinalizeCollectionAsync(collection.Id, ct);
        if (finalized == null || finalized.Status == CollectionStatus.Cancelled)
        {
            if (messageToEdit != null)
            {
                await EditMenuAsync(
                    messageToEdit.Chat.Id,
                    messageToEdit.MessageId,
                    "❌ Сбор отменён - нет участников.",
                    KeyboardBuilder.BackToMainMenu(true),
                    null,
                    ct
                );
            }
            else
            {
                await _bot.SendMessage(
                    chatId,
                    "❌ Сбор отменён - нет участников.",
                    replyMarkup: KeyboardBuilder.BackToMainMenu(true),
                    cancellationToken: ct
                );
            }
            return;
        }

        // Admin is considered always confirmed — set status so we don't wait for admin's confirmation
        var adminParticipant = await _collectionService.GetParticipantAsync(
            collection.Id,
            _adminId,
            ct
        );
        if (adminParticipant != null)
        {
            await _collectionService.UpdateParticipantStatusAsync(
                adminParticipant.Id,
                ParticipantStatus.Confirmed,
                ct
            );
        }

        // Notify all participating users (except admin) about the amount they need to pay
        var participants = await _collectionService.GetParticipatingUsersAsync(collection.Id, ct);
        foreach (var participant in participants)
        {
            if (participant.UserId == _adminId)
                continue;
            try
            {
                await _bot.SendMessage(
                    participant.UserId,
                    $"💳 *Подтверждение оплаты*\n\n"
                        + $"Сбор: {EscapeMarkdown(collection.Description)}\n"
                        + $"Ваша сумма: *{participant.AmountToPay:F2}₽*\n\n"
                        + $"Подтвердите готовность оплатить:",
                    parseMode: ParseMode.Markdown,
                    replyMarkup: KeyboardBuilder.CollectionConfirmation(collection.Id),
                    cancellationToken: ct
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify user {UserId}", participant.UserId);
            }
        }

        var successText =
            $"✅ Сбор завершён!\n\nСумма {collection.TotalAmount:F2}₽ распределена между {participants.Count} участниками.\nКаждый должен заплатить: {participants.FirstOrDefault()?.AmountToPay:F2}₽\n\nОжидание подтверждений...";

        var activeCollectionAfterFinalize = await _collectionService.GetActiveCollectionAsync(ct);
        if (messageToEdit != null)
        {
            await EditMenuAsync(
                messageToEdit.Chat.Id,
                messageToEdit.MessageId,
                successText,
                KeyboardBuilder.MainMenuAdmin(
                    activeCollectionAfterFinalize != null,
                    activeCollectionAfterFinalize
                ),
                null,
                ct
            );
        }
        else
        {
            await _bot.SendMessage(
                chatId,
                successText,
                replyMarkup: KeyboardBuilder.MainMenuAdmin(
                    activeCollectionAfterFinalize != null,
                    activeCollectionAfterFinalize
                ),
                cancellationToken: ct
            );
        }
    }

    private async Task NotifyAdminAboutParticipation(long userId, bool joined, CancellationToken ct)
    {
        var user = await _userService.GetUserAsync(userId, ct);
        var name = user?.FirstName ?? user?.Username ?? userId.ToString();
        var action = joined ? "✅ участвует" : "❌ отказался";
        var activeCollection = await _collectionService.GetActiveCollectionAsync(ct);

        await _bot.SendMessage(
            _adminId,
            $"📢 {name} {action} в сборе.",
            replyMarkup: KeyboardBuilder.MainMenuAdmin(activeCollection != null, activeCollection),
            cancellationToken: ct
        );
    }

    private async Task CheckAndCompleteCollection(int collectionId, CancellationToken ct)
    {
        var (allConfirmed, needsRecalculation) = await _collectionService.CheckConfirmationsAsync(
            collectionId,
            ct
        );

        if (needsRecalculation.Count > 0)
        {
            var adminCollection = await _collectionService.GetActiveCollectionAsync(ct);
            await _bot.SendMessage(
                _adminId,
                "🔄 Происходит перерасчёт суммы из-за отказа участника.",
                replyMarkup: KeyboardBuilder.MainMenuAdmin(
                    adminCollection != null,
                    adminCollection
                ),
                cancellationToken: ct
            );

            foreach (var participant in needsRecalculation)
            {
                try
                {
                    var collection = await _collectionService.GetActiveCollectionAsync(ct);
                    if (collection == null)
                        continue;

                    await _bot.SendMessage(
                        participant.UserId,
                        $"🔄 *Перерасчёт суммы*\n\n"
                            + $"Кто-то отказался от участия.\n"
                            + $"Новая сумма: *{participant.AmountToPay:F2}₽*\n\n"
                            + $"Подтвердите готовность оплатить:",
                        parseMode: ParseMode.Markdown,
                        replyMarkup: KeyboardBuilder.CollectionConfirmation(collectionId),
                        cancellationToken: ct
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to notify user {UserId} about recalculation",
                        participant.UserId
                    );
                }
            }
        }
        else if (allConfirmed)
        {
            // All confirmed — move to AwaitingPayment, send requisites and "Я оплатил" button
            var collection = await _collectionService.GetActiveCollectionAsync(ct);
            if (collection == null)
                return;

            var moved = await _collectionService.MoveToAwaitingPaymentAsync(collectionId, ct);
            if (moved == null)
                return;

            var participants = await _collectionService.GetParticipatingUsersAsync(
                collectionId,
                ct
            );
            foreach (var participant in participants)
            {
                if (participant.UserId == _adminId)
                    continue;
                try
                {
                    await _bot.SendMessage(
                        participant.UserId,
                        $"💳 *Реквизиты для оплаты*\n\n"
                            + $"Сбор: {EscapeMarkdown(collection.Description)}\n"
                            + $"Ваша сумма: *{participant.AmountToPay:F2}₽*\n\n"
                            + $"Реквизиты:\n`{EscapeMarkdown(collection.PaymentDetails)}`\n\n"
                            + $"Нажмите «Я оплатил», когда оплатите.",
                        parseMode: ParseMode.Markdown,
                        replyMarkup: KeyboardBuilder.CollectionPaid(collectionId),
                        cancellationToken: ct
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to send payment details to user {UserId}",
                        participant.UserId
                    );
                }
            }

            var adminCollection = await _collectionService.GetActiveCollectionAsync(ct);
            await _bot.SendMessage(
                _adminId,
                $"💳 Сбор перешёл в этап ожидания оплаты.\nВсе подтвердили — реквизиты отправлены. Ожидайте отметок «Я оплатил».",
                replyMarkup: KeyboardBuilder.MainMenuAdmin(true, adminCollection),
                cancellationToken: ct
            );
        }
    }

    private async Task CheckAndCompleteFromAwaitingPaymentAsync(
        int collectionId,
        CancellationToken ct
    )
    {
        var allPaid = await _collectionService.CheckAllPaidAsync(collectionId, ct);
        if (!allPaid)
            return;

        var collection = await _collectionService.GetActiveCollectionAsync(ct);
        if (collection == null)
            return;

        var allParticipants = await _collectionService.GetAllParticipantsAsync(collectionId, ct);
        var paidParticipants = allParticipants
            .Where(p => p.Status == ParticipantStatus.Paid)
            .ToList();
        await _collectionService.CompleteCollectionAsync(collectionId, ct);

        foreach (var participant in paidParticipants)
        {
            try
            {
                await _bot.SendMessage(
                    participant.UserId,
                    $"✅ *Сбор завершён!*\n\nВсе участники отметили оплату.",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: ct
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to notify user {UserId} about collection completion",
                    participant.UserId
                );
            }
        }

        await _bot.SendMessage(
            _adminId,
            $"🎉 Сбор \"{collection.Description}\" полностью завершён!\nВсе участники отметили оплату.",
            replyMarkup: KeyboardBuilder.MainMenuAdmin(false, null),
            cancellationToken: ct
        );
    }

    private async Task TryAutoAdvanceFromPendingAsync(int collectionId, CancellationToken ct)
    {
        var allAnswered = await _collectionService.AllParticipantsAnsweredAsync(collectionId, ct);
        if (!allAnswered)
            return;

        await FinalizeCollection(new ChatId(_adminId), ct, null);
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
