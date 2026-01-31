using System.ComponentModel.DataAnnotations;

namespace TaxCollectionTelegramBot.Data.Entities;

public class User
{
    [Key]
    public long TelegramId { get; set; }

    public string? Username { get; set; }

    public string? FirstName { get; set; }

    public bool IsAdmin { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserConfig> Configs { get; set; } = new List<UserConfig>();

    public ICollection<CollectionParticipant> Participations { get; set; } =
        new List<CollectionParticipant>();
}
