using System.Collections.Concurrent;
using System.Threading.Channels;
using SSAM_Server.DTOs.Notifications;

namespace SSAM_Server.Services;

public sealed class NotificationBroadcaster
{
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<Guid, Channel<NotificationDto>>> _subscribers = new();

    public (Guid SubscriptionId, ChannelReader<NotificationDto> Reader) Subscribe(long userId)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<NotificationDto>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        var userSubs = _subscribers.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, Channel<NotificationDto>>());
        userSubs[id] = channel;
        return (id, channel.Reader);
    }

    public void Unsubscribe(long userId, Guid subscriptionId)
    {
        if (_subscribers.TryGetValue(userId, out var userSubs) && userSubs.TryRemove(subscriptionId, out var ch))
        {
            ch.Writer.TryComplete();
            if (userSubs.IsEmpty)
            {
                _subscribers.TryRemove(userId, out _);
            }
        }
    }

    public void Publish(long userId, NotificationDto dto)
    {
        if (!_subscribers.TryGetValue(userId, out var userSubs)) return;
        foreach (var ch in userSubs.Values)
        {
            ch.Writer.TryWrite(dto);
        }
    }
}
