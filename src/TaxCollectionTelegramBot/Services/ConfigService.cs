using Microsoft.EntityFrameworkCore;
using TaxCollectionTelegramBot.Data;
using TaxCollectionTelegramBot.Data.Entities;

namespace TaxCollectionTelegramBot.Services;

public class ConfigService
{
    private readonly AppDbContext _context;

    public ConfigService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UserConfig> AddConfigAsync(
        long userId,
        string name,
        string configText,
        CancellationToken ct = default
    )
    {
        var config = new UserConfig
        {
            UserId = userId,
            Name = name,
            ConfigText = configText,
            CreatedAt = DateTime.UtcNow,
        };

        _context.UserConfigs.Add(config);
        await _context.SaveChangesAsync(ct);

        return config;
    }

    public async Task<List<UserConfig>> GetUserConfigsAsync(
        long userId,
        CancellationToken ct = default
    )
    {
        return await _context
            .UserConfigs.Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<UserConfig?> GetConfigByIdAsync(int configId, CancellationToken ct = default)
    {
        return await _context.UserConfigs.FindAsync([configId], ct);
    }

    public async Task<bool> DeleteConfigAsync(int configId, CancellationToken ct = default)
    {
        var config = await _context.UserConfigs.FindAsync([configId], ct);
        if (config == null)
            return false;

        _context.UserConfigs.Remove(config);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateConfigAsync(
        int configId,
        string? name,
        string? configText,
        CancellationToken ct = default
    )
    {
        var config = await _context.UserConfigs.FindAsync([configId], ct);
        if (config == null)
            return false;

        if (!string.IsNullOrWhiteSpace(name))
            config.Name = name.Trim();
        if (configText != null)
            config.ConfigText = configText;

        await _context.SaveChangesAsync(ct);
        return true;
    }
}
