namespace Notification.Webhook.Application.Common.Messaging;

public record SendWebhookMessage(
    Guid NotificationId,
    string WebhookUrl,
    string HttpMethod,
    string Payload,
    string ContentType,
    Dictionary<string, string>? Headers,
    string? Secret,
    int MaxRetries,
    int TimeoutSeconds);
