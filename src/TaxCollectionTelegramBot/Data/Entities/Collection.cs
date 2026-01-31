using System.ComponentModel.DataAnnotations;

namespace TaxCollectionTelegramBot.Data.Entities;

public class Collection
{
    [Key]
    public int Id { get; set; }

    public decimal TotalAmount { get; set; }

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string PaymentDetails { get; set; } = string.Empty;

    public CollectionStatus Status { get; set; } = CollectionStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CollectionParticipant> Participants { get; set; } =
        new List<CollectionParticipant>();
}
