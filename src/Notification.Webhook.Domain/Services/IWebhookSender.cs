using Notification.Webhook.Domain.Entities;

namespace Notification.Webhook.Domain.Services;

public interface IWebhookSender
{
    Task<WebhookSendResult> SendAsync(WebhookNotification notification, CancellationToken cancellationToken = default);
}

public record WebhookSendResult(
    bool Success,
    int StatusCode,
    string? ResponseBody = null,
    string? ErrorMessage = null,
    TimeSpan? Duration = null);
