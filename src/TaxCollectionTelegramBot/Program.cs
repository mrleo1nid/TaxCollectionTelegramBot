using Microsoft.EntityFrameworkCore;
using TaxCollectionTelegramBot;
using TaxCollectionTelegramBot.Data;
using TaxCollectionTelegramBot.Handlers;
using TaxCollectionTelegramBot.Services;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services.Configure<BotConfiguration>(builder.Configuration.GetSection("BotConfiguration"));

var botConfig =
    builder.Configuration.GetSection("BotConfiguration").Get<BotConfiguration>()
    ?? throw new InvalidOperationException("BotConfiguration is missing");

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// Telegram Bot Client
builder.Services.AddSingleton<ITelegramBotClient>(sp => new TelegramBotClient(botConfig.Token));

// Services
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ConfigService>();
builder.Services.AddScoped<CollectionService>();
builder.Services.AddSingleton<UserStateService>();

// Handlers
builder.Services.AddScoped<UpdateHandler>();
builder.Services.AddScoped<MessageHandler>();
builder.Services.AddScoped<CallbackQueryHandler>();

// Background service
builder.Services.AddHostedService<BotService>();

var host = builder.Build();

// Apply migrations on startup
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

await host.RunAsync();
