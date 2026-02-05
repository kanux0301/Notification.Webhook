namespace Notification.Webhook.Application.Common.Messaging;

public interface IMessageConsumer
{
    Task StartAsync(Func<SendWebhookMessage, CancellationToken, Task> handler, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
