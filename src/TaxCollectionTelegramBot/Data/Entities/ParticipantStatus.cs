namespace TaxCollectionTelegramBot.Data.Entities;

public enum ParticipantStatus
{
    Pending = 0,
    Participating = 1,
    Declined = 2,
    Confirmed = 3,
    DeclinedPayment = 4,
    Paid = 5,
}
