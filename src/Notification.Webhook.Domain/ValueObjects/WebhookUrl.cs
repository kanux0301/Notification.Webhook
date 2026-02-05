namespace Notification.Webhook.Domain.ValueObjects;

public record WebhookUrl
{
    public string Value { get; }

    private WebhookUrl(string value) => Value = value;

    public static WebhookUrl Create(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Webhook URL cannot be empty", nameof(url));

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Invalid URL format", nameof(url));

        if (uri.Scheme != "https" && uri.Scheme != "http")
            throw new ArgumentException("URL must use HTTP or HTTPS", nameof(url));

        return new WebhookUrl(url);
    }
}
