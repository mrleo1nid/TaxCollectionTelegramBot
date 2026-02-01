using Microsoft.EntityFrameworkCore;
using TaxCollectionTelegramBot.Data;
using TaxCollectionTelegramBot.Data.Entities;

namespace TaxCollectionTelegramBot.Services;

public class CollectionService
{
    private readonly AppDbContext _context;

    public CollectionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Collection?> GetActiveCollectionAsync(CancellationToken ct = default)
    {
        return await _context
            .Collections.Include(c => c.Participants)
            .ThenInclude(p => p.User)
            .Where(c =>
                c.Status == CollectionStatus.Pending
                || c.Status == CollectionStatus.AwaitingConfirmation
                || c.Status == CollectionStatus.AwaitingPayment
            )
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Collection> CreateCollectionAsync(
        decimal amount,
        string description,
        string paymentDetails,
        CancellationToken ct = default
    )
    {
        var collection = new Collection
        {
            TotalAmount = amount,
            Description = description,
            PaymentDetails = paymentDetails,
            Status = CollectionStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };

        _context.Collections.Add(collection);
        await _context.SaveChangesAsync(ct);

        return collection;
    }

    public async Task AddParticipantsAsync(
        int collectionId,
        IEnumerable<long> userIds,
        CancellationToken ct = default
    )
    {
        foreach (var userId in userIds)
        {
            var participant = new CollectionParticipant
            {
                CollectionId = collectionId,
                UserId = userId,
                Status = ParticipantStatus.Pending,
                AmountToPay = 0,
            };
            _context.CollectionParticipants.Add(participant);
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task AddParticipantWithStatusAsync(
        int collectionId,
        long userId,
        ParticipantStatus status,
        CancellationToken ct = default
    )
    {
        var participant = new CollectionParticipant
        {
            CollectionId = collectionId,
            UserId = userId,
            Status = status,
            AmountToPay = 0,
        };
        _context.CollectionParticipants.Add(participant);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<CollectionParticipant?> GetParticipantAsync(
        int collectionId,
        long userId,
        CancellationToken ct = default
    )
    {
        return await _context
            .CollectionParticipants.Include(p => p.User)
            .FirstOrDefaultAsync(p => p.CollectionId == collectionId && p.UserId == userId, ct);
    }

    public async Task UpdateParticipantStatusAsync(
        int participantId,
        ParticipantStatus status,
        CancellationToken ct = default
    )
    {
        var participant = await _context.CollectionParticipants.FindAsync([participantId], ct);
        if (participant != null)
        {
            participant.Status = status;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<Collection?> FinalizeCollectionAsync(
        int collectionId,
        CancellationToken ct = default
    )
    {
        var collection = await _context
            .Collections.Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == collectionId, ct);

        if (collection == null)
            return null;

        // Get participating users
        var participatingUsers = collection
            .Participants.Where(p => p.Status == ParticipantStatus.Participating)
            .ToList();

        if (participatingUsers.Count == 0)
        {
            collection.Status = CollectionStatus.Cancelled;
            await _context.SaveChangesAsync(ct);
            return collection;
        }

        // Calculate amount per participant
        var amountPerPerson = Math.Round(collection.TotalAmount / participatingUsers.Count, 2);

        foreach (var participant in participatingUsers)
        {
            participant.AmountToPay = amountPerPerson;
        }

        collection.Status = CollectionStatus.AwaitingConfirmation;
        await _context.SaveChangesAsync(ct);

        return collection;
    }

    public async Task<(
        bool allConfirmed,
        List<CollectionParticipant> needsRecalculation
    )> CheckConfirmationsAsync(int collectionId, CancellationToken ct = default)
    {
        var collection = await _context
            .Collections.Include(c => c.Participants)
            .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(c => c.Id == collectionId, ct);

        if (collection == null)
            return (false, new List<CollectionParticipant>());

        var activeParticipants = collection
            .Participants.Where(p =>
                p.Status == ParticipantStatus.Participating
                || p.Status == ParticipantStatus.Confirmed
                || p.Status == ParticipantStatus.Pending
            )
            .ToList();

        // Check if anyone declined payment
        var declinedPayment = collection
            .Participants.Where(p => p.Status == ParticipantStatus.DeclinedPayment)
            .ToList();

        if (declinedPayment.Count > 0)
        {
            // Need to recalculate - mark declined as actually declined
            foreach (var p in declinedPayment)
            {
                p.Status = ParticipantStatus.Declined;
            }

            // Recalculate for remaining participants
            var remainingParticipants = collection
                .Participants.Where(p =>
                    p.Status == ParticipantStatus.Participating
                    || p.Status == ParticipantStatus.Confirmed
                )
                .ToList();

            if (remainingParticipants.Count == 0)
            {
                collection.Status = CollectionStatus.Cancelled;
                await _context.SaveChangesAsync(ct);
                return (false, new List<CollectionParticipant>());
            }

            var newAmount = Math.Round(collection.TotalAmount / remainingParticipants.Count, 2);
            foreach (var p in remainingParticipants)
            {
                p.AmountToPay = newAmount;
                p.Status = ParticipantStatus.Participating; // Reset to wait for confirmation again
            }

            await _context.SaveChangesAsync(ct);
            return (false, remainingParticipants);
        }

        // Check if all active participants confirmed
        var allConfirmed = collection
            .Participants.Where(p =>
                p.Status != ParticipantStatus.Declined
                && p.Status != ParticipantStatus.DeclinedPayment
            )
            .All(p => p.Status == ParticipantStatus.Confirmed);

        return (allConfirmed, new List<CollectionParticipant>());
    }

    public async Task CompleteCollectionAsync(int collectionId, CancellationToken ct = default)
    {
        var collection = await _context.Collections.FindAsync([collectionId], ct);
        if (collection != null)
        {
            collection.Status = CollectionStatus.Completed;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task CancelCollectionAsync(int collectionId, CancellationToken ct = default)
    {
        var collection = await _context.Collections.FindAsync([collectionId], ct);
        if (collection != null)
        {
            collection.Status = CollectionStatus.Cancelled;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<List<CollectionParticipant>> GetParticipatingUsersAsync(
        int collectionId,
        CancellationToken ct = default
    )
    {
        return await _context
            .CollectionParticipants.Include(p => p.User)
            .Where(p =>
                p.CollectionId == collectionId
                && (
                    p.Status == ParticipantStatus.Participating
                    || p.Status == ParticipantStatus.Confirmed
                )
            )
            .ToListAsync(ct);
    }

    public async Task<List<CollectionParticipant>> GetAllParticipantsAsync(
        int collectionId,
        CancellationToken ct = default
    )
    {
        return await _context
            .CollectionParticipants.Include(p => p.User)
            .Where(p => p.CollectionId == collectionId)
            .ToListAsync(ct);
    }

    public async Task<Collection?> MoveToAwaitingPaymentAsync(
        int collectionId,
        CancellationToken ct = default
    )
    {
        var collection = await _context
            .Collections.Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == collectionId, ct);

        if (collection == null || collection.Status != CollectionStatus.AwaitingConfirmation)
            return null;

        collection.Status = CollectionStatus.AwaitingPayment;
        await _context.SaveChangesAsync(ct);
        return collection;
    }

    /// <summary>
    /// Participants who must press "I paid" (Confirmed in AwaitingPayment stage).
    /// </summary>
    public async Task<List<CollectionParticipant>> GetParticipantsAwaitingPaymentAsync(
        int collectionId,
        CancellationToken ct = default
    )
    {
        return await _context
            .CollectionParticipants.Include(p => p.User)
            .Where(p => p.CollectionId == collectionId && p.Status == ParticipantStatus.Confirmed)
            .ToListAsync(ct);
    }

    public async Task<bool> CheckAllPaidAsync(int collectionId, CancellationToken ct = default)
    {
        var collection = await _context
            .Collections.Include(c => c.Participants)
            .FirstOrDefaultAsync(
                c => c.Id == collectionId && c.Status == CollectionStatus.AwaitingPayment,
                ct
            );

        if (collection == null)
            return false;

        var payingParticipants = collection
            .Participants.Where(p =>
                p.Status == ParticipantStatus.Confirmed || p.Status == ParticipantStatus.Paid
            )
            .ToList();

        if (payingParticipants.Count == 0)
            return false;

        return payingParticipants.All(p => p.Status == ParticipantStatus.Paid);
    }

    public async Task<Collection?> GetLastCompletedCollectionAsync(CancellationToken ct = default)
    {
        return await _context
            .Collections.Include(c => c.Participants)
            .ThenInclude(p => p.User)
            .Where(c => c.Status == CollectionStatus.Completed)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// True if every participant has chosen (Participating or Declined); no Pending left.
    /// Only meaningful when collection is Pending.
    /// </summary>
    public async Task<bool> AllParticipantsAnsweredAsync(
        int collectionId,
        CancellationToken ct = default
    )
    {
        var collection = await _context
            .Collections.Include(c => c.Participants)
            .FirstOrDefaultAsync(
                c => c.Id == collectionId && c.Status == CollectionStatus.Pending,
                ct
            );
        if (collection == null)
            return false;
        return collection.Participants.All(p =>
            p.Status == ParticipantStatus.Participating || p.Status == ParticipantStatus.Declined
        );
    }

    /// <summary>
    /// IDs активных сборов (Pending, AwaitingConfirmation, AwaitingPayment), где участвует пользователь.
    /// </summary>
    public async Task<List<int>> GetActiveCollectionIdsByParticipantAsync(
        long userId,
        CancellationToken ct = default
    )
    {
        var activeStatuses = new[]
        {
            CollectionStatus.Pending,
            CollectionStatus.AwaitingConfirmation,
            CollectionStatus.AwaitingPayment,
        };

        return await _context
            .CollectionParticipants.Where(p => p.UserId == userId)
            .Join(
                _context.Collections.Where(c => activeStatuses.Contains(c.Status)),
                p => p.CollectionId,
                c => c.Id,
                (p, _) => p.CollectionId
            )
            .Distinct()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Пересчитывает AmountToPay для оставшихся участников после удаления одного.
    /// Для Pending — не требуется (суммы ещё не назначены).
    /// Для AwaitingConfirmation и AwaitingPayment — amountPerPerson = TotalAmount / count участвующих.
    /// </summary>
    public async Task RecalculateAmountsAfterParticipantRemovalAsync(
        int collectionId,
        CancellationToken ct = default
    )
    {
        var collection = await _context
            .Collections.Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == collectionId, ct);

        if (collection == null)
            return;

        if (collection.Status == CollectionStatus.Pending)
            return;

        var payingParticipants = collection
            .Participants.Where(p =>
                p.Status == ParticipantStatus.Participating
                || p.Status == ParticipantStatus.Confirmed
            )
            .ToList();

        if (payingParticipants.Count == 0)
        {
            collection.Status = CollectionStatus.Cancelled;
            await _context.SaveChangesAsync(ct);
            return;
        }

        var amountPerPerson = Math.Round(collection.TotalAmount / payingParticipants.Count, 2);

        foreach (var participant in payingParticipants)
        {
            participant.AmountToPay = amountPerPerson;
        }

        await _context.SaveChangesAsync(ct);
    }
}
