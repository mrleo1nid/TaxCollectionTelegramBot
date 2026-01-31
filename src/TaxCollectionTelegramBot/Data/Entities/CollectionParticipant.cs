using System.ComponentModel.DataAnnotations;

namespace TaxCollectionTelegramBot.Data.Entities;

public class CollectionParticipant
{
    [Key]
    public int Id { get; set; }

    public int CollectionId { get; set; }

    public Collection Collection { get; set; } = null!;

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    public ParticipantStatus Status { get; set; } = ParticipantStatus.Pending;

    public decimal AmountToPay { get; set; }
}
