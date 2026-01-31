using System.Collections.Concurrent;

namespace TaxCollectionTelegramBot.Services;

public enum UserState
{
    None,

    // Admin states for adding config
    AwaitingConfigUserSelection,
    AwaitingConfigName,
    AwaitingConfigText,

    // Admin states for editing config
    AwaitingConfigEditName,
    AwaitingConfigEditText,

    // Admin states for creating collection
    AwaitingCollectionAmount,
    AwaitingCollectionDescription,
    AwaitingCollectionPaymentDetails,
}

public class UserStateData
{
    public UserState State { get; set; } = UserState.None;
    public long? SelectedUserId { get; set; }
    public string? ConfigName { get; set; }
    public int? ConfigIdForEdit { get; set; }
    public decimal? CollectionAmount { get; set; }
    public string? CollectionDescription { get; set; }
}

public class UserStateService
{
    private readonly ConcurrentDictionary<long, UserStateData> _states = new();

    public UserStateData GetState(long userId)
    {
        return _states.GetOrAdd(userId, _ => new UserStateData());
    }

    public void SetState(long userId, UserState state)
    {
        var data = GetState(userId);
        data.State = state;
    }

    public void ClearState(long userId)
    {
        _states.TryRemove(userId, out _);
    }

    public void UpdateState(long userId, Action<UserStateData> update)
    {
        var data = GetState(userId);
        update(data);
    }
}
