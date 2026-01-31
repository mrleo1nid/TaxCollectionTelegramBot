using Microsoft.EntityFrameworkCore;
using TaxCollectionTelegramBot.Data;
using TaxCollectionTelegramBot.Data.Entities;

namespace TaxCollectionTelegramBot.Services;

public class UserService
{
    private readonly AppDbContext _context;

    public UserService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User> GetOrCreateUserAsync(
        long telegramId,
        string? username,
        string? firstName,
        CancellationToken ct = default
    )
    {
        var user = await _context.Users.FindAsync([telegramId], ct);

        if (user == null)
        {
            user = new User
            {
                TelegramId = telegramId,
                Username = username,
                FirstName = firstName,
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow,
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync(ct);
        }
        else
        {
            // Update user info if changed
            bool updated = false;
            if (user.Username != username)
            {
                user.Username = username;
                updated = true;
            }
            if (user.FirstName != firstName)
            {
                user.FirstName = firstName;
                updated = true;
            }
            if (updated)
            {
                await _context.SaveChangesAsync(ct);
            }
        }

        return user;
    }

    public async Task<bool> IsNewUserAsync(long telegramId, CancellationToken ct = default)
    {
        return !await _context.Users.AnyAsync(u => u.TelegramId == telegramId, ct);
    }

    public async Task<User?> GetUserAsync(long telegramId, CancellationToken ct = default)
    {
        return await _context.Users.FindAsync([telegramId], ct);
    }

    public async Task<List<User>> GetAllUsersExceptAdminAsync(
        long adminId,
        CancellationToken ct = default
    )
    {
        return await _context
            .Users.Where(u => u.TelegramId != adminId)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.Username)
            .ToListAsync(ct);
    }

    public async Task<List<User>> GetAllNonAdminUsersAsync(CancellationToken ct = default)
    {
        return await _context
            .Users.Where(u => !u.IsAdmin)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.Username)
            .ToListAsync(ct);
    }
}
