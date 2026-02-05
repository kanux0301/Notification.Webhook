using Microsoft.Extensions.Logging;
using Notification.Webhook.Domain.Entities;
using Notification.Webhook.Domain.Services;

namespace Notification.Webhook.Infrastructure.Providers;

public class ConsoleWebhookSender : IWebhookSender
{
    private readonly ILogger<ConsoleWebhookSender> _logger;

    public ConsoleWebhookSender(ILogger<ConsoleWebhookSender> logger)
    {
        _logger = logger;
    }

    public Task<WebhookSendResult> SendAsync(WebhookNotification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            """
            ========== WEBHOOK NOTIFICATION ==========
            ID: {Id}
            URL: {Url}
            Method: {Method}
            Content-Type: {ContentType}
            Headers: {Headers}
            Secret: {Secret}
            Timeout: {Timeout}s
            Max Retries: {MaxRetries}

            Payload:
            {Payload}
            ==========================================
            """,
            notification.Id,
            notification.Url.Value,
            notification.Method,
            notification.Payload.ContentType,
            notification.Headers != null
                ? string.Join(", ", notification.Headers.Select(h => $"{h.Key}: {h.Value}"))
                : "(none)",
            !string.IsNullOrEmpty(notification.Secret) ? "***" : "(none)",
            notification.TimeoutSeconds,
            notification.MaxRetries,
            notification.Payload.Content);

        return Task.FromResult(new WebhookSendResult(
            Success: true,
            StatusCode: 200,
            ResponseBody: "{\"status\":\"logged\"}",
            Duration: TimeSpan.FromMilliseconds(1)));
    }
}
