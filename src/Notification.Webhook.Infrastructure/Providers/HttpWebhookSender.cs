using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification.Webhook.Domain.Entities;
using Notification.Webhook.Domain.Enums;
using Notification.Webhook.Domain.Services;
using Notification.Webhook.Infrastructure.Configuration;

namespace Notification.Webhook.Infrastructure.Providers;

public class HttpWebhookSender : IWebhookSender
{
    private readonly HttpClient _httpClient;
    private readonly WebhookOptions _options;
    private readonly ILogger<HttpWebhookSender> _logger;

    public HttpWebhookSender(
        HttpClient httpClient,
        IOptions<WebhookOptions> options,
        ILogger<HttpWebhookSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WebhookSendResult> SendAsync(WebhookNotification notification, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var lastException = default(Exception);
        var lastStatusCode = 0;
        var lastResponseBody = string.Empty;

        for (int attempt = 0; attempt <= notification.MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                var delay = _options.RetryDelayMilliseconds * (int)Math.Pow(2, attempt - 1);
                _logger.LogInformation(
                    "Retrying webhook {NotificationId}, attempt {Attempt}/{MaxRetries} after {Delay}ms",
                    notification.Id,
                    attempt,
                    notification.MaxRetries,
                    delay);
                await Task.Delay(delay, cancellationToken);
            }

            try
            {
                using var request = CreateRequest(notification);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(notification.TimeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

                var response = await _httpClient.SendAsync(request, linkedCts.Token);
                lastStatusCode = (int)response.StatusCode;
                lastResponseBody = await response.Content.ReadAsStringAsync(linkedCts.Token);

                if (response.IsSuccessStatusCode)
                {
                    stopwatch.Stop();
                    return new WebhookSendResult(
                        Success: true,
                        StatusCode: lastStatusCode,
                        ResponseBody: lastResponseBody,
                        Duration: stopwatch.Elapsed);
                }

                _logger.LogWarning(
                    "Webhook {NotificationId} returned non-success status {StatusCode}",
                    notification.Id,
                    lastStatusCode);
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
            {
                lastException = ex;
                _logger.LogWarning(
                    "Webhook {NotificationId} timed out after {Timeout}s",
                    notification.Id,
                    notification.TimeoutSeconds);
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                _logger.LogWarning(
                    ex,
                    "Webhook {NotificationId} request failed",
                    notification.Id);
            }
        }

        stopwatch.Stop();
        return new WebhookSendResult(
            Success: false,
            StatusCode: lastStatusCode,
            ResponseBody: lastResponseBody,
            ErrorMessage: lastException?.Message ?? $"Failed after {notification.MaxRetries + 1} attempts",
            Duration: stopwatch.Elapsed);
    }

    private HttpRequestMessage CreateRequest(WebhookNotification notification)
    {
        var method = notification.Method switch
        {
            WebhookHttpMethod.POST => HttpMethod.Post,
            WebhookHttpMethod.PUT => HttpMethod.Put,
            WebhookHttpMethod.PATCH => HttpMethod.Patch,
            _ => HttpMethod.Post
        };

        var request = new HttpRequestMessage(method, notification.Url.Value)
        {
            Content = new StringContent(
                notification.Payload.Content,
                Encoding.UTF8,
                notification.Payload.ContentType)
        };

        // Add custom headers
        if (notification.Headers != null)
        {
            foreach (var header in notification.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        // Add HMAC signature if secret is provided
        if (!string.IsNullOrEmpty(notification.Secret))
        {
            var signature = ComputeHmacSignature(notification.Payload.Content, notification.Secret);
            request.Headers.TryAddWithoutValidation("X-Webhook-Signature", $"sha256={signature}");
        }

        // Add notification ID for tracing
        request.Headers.TryAddWithoutValidation("X-Notification-Id", notification.Id.ToString());

        return request;
    }

    private static string ComputeHmacSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
