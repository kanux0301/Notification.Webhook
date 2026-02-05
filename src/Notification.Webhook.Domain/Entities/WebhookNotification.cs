using Notification.Webhook.Domain.Enums;
using Notification.Webhook.Domain.ValueObjects;

namespace Notification.Webhook.Domain.Entities;

public class WebhookNotification
{
    public Guid Id { get; private set; }
    public WebhookUrl Url { get; private set; } = null!;
    public WebhookHttpMethod Method { get; private set; }
    public WebhookPayload Payload { get; private set; } = null!;
    public Dictionary<string, string>? Headers { get; private set; }
    public string? Secret { get; private set; }
    public int MaxRetries { get; private set; }
    public int TimeoutSeconds { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private WebhookNotification() { }

    public static WebhookNotification Create(
        Guid id,
        WebhookUrl url,
        WebhookHttpMethod method,
        WebhookPayload payload,
        Dictionary<string, string>? headers = null,
        string? secret = null,
        int maxRetries = 3,
        int timeoutSeconds = 30)
    {
        return new WebhookNotification
        {
            Id = id,
            Url = url,
            Method = method,
            Payload = payload,
            Headers = headers,
            Secret = secret,
            MaxRetries = maxRetries,
            TimeoutSeconds = timeoutSeconds,
            CreatedAt = DateTime.UtcNow
        };
    }
}
