using Telegram.Bot;
using Telegram.Bot.Types;

namespace TaxCollectionTelegramBot.Handlers;

public class UpdateHandler
{
    private readonly MessageHandler _messageHandler;
    private readonly CallbackQueryHandler _callbackHandler;
    private readonly ILogger<UpdateHandler> _logger;

    public UpdateHandler(
        MessageHandler messageHandler,
        CallbackQueryHandler callbackHandler,
        ILogger<UpdateHandler> logger
    )
    {
        _messageHandler = messageHandler;
        _callbackHandler = callbackHandler;
        _logger = logger;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        try
        {
            var handler = update switch
            {
                { Message: { } message } => _messageHandler.HandleAsync(message, ct),
                { CallbackQuery: { } callbackQuery } => _callbackHandler.HandleAsync(
                    callbackQuery,
                    ct
                ),
                _ => Task.CompletedTask,
            };

            await handler;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update {UpdateId}", update.Id);
        }
    }

    public Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Telegram Bot error");
        return Task.CompletedTask;
    }
}
