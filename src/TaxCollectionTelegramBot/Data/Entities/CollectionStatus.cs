namespace TaxCollectionTelegramBot.Data.Entities;

public enum CollectionStatus
{
    Pending = 0,
    AwaitingConfirmation = 1,
    Completed = 2,
    Cancelled = 3,
    AwaitingPayment = 4,
}
