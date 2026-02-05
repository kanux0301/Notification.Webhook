using Microsoft.Extensions.Logging;
using Notification.Webhook.Application.Common.Messaging;
using Notification.Webhook.Domain.Entities;
using Notification.Webhook.Domain.Enums;
using Notification.Webhook.Domain.Services;
using Notification.Webhook.Domain.ValueObjects;

namespace Notification.Webhook.Application.Commands;

public class SendWebhookCommand
{
    private readonly IWebhookSender _webhookSender;
    private readonly ILogger<SendWebhookCommand> _logger;

    public SendWebhookCommand(IWebhookSender webhookSender, ILogger<SendWebhookCommand> logger)
    {
        _webhookSender = webhookSender;
        _logger = logger;
    }

    public async Task<WebhookSendResult> ExecuteAsync(SendWebhookMessage message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing webhook notification {NotificationId} to {Url}",
            message.NotificationId,
            message.WebhookUrl);

        var url = WebhookUrl.Create(message.WebhookUrl);
        var payload = WebhookPayload.Create(message.Payload, message.ContentType);
        var method = Enum.Parse<WebhookHttpMethod>(message.HttpMethod, ignoreCase: true);

        var notification = WebhookNotification.Create(
            message.NotificationId,
            url,
            method,
            payload,
            message.Headers,
            message.Secret,
            message.MaxRetries,
            message.TimeoutSeconds);

        var result = await _webhookSender.SendAsync(notification, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation(
                "Webhook {NotificationId} sent successfully. Status: {StatusCode}, Duration: {Duration}ms",
                message.NotificationId,
                result.StatusCode,
                result.Duration?.TotalMilliseconds);
        }
        else
        {
            _logger.LogError(
                "Failed to send webhook {NotificationId}. Status: {StatusCode}, Error: {Error}",
                message.NotificationId,
                result.StatusCode,
                result.ErrorMessage);
        }

        return result;
    }
}
