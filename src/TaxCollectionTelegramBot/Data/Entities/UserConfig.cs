using System.ComponentModel.DataAnnotations;

namespace TaxCollectionTelegramBot.Data.Entities;

public class UserConfig
{
    [Key]
    public int Id { get; set; }

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string ConfigText { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
