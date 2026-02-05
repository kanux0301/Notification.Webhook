namespace Notification.Webhook.Domain.ValueObjects;

public record WebhookPayload
{
    public string Content { get; }
    public string ContentType { get; }

    private WebhookPayload(string content, string contentType)
    {
        Content = content;
        ContentType = contentType;
    }

    public static WebhookPayload Create(string content, string contentType = "application/json")
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Payload content cannot be empty", nameof(content));

        return new WebhookPayload(content, contentType);
    }
}
