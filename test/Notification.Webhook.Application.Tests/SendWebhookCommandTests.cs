using Microsoft.Extensions.Logging;
using Moq;
using Notification.Webhook.Application.Commands;
using Notification.Webhook.Application.Common.Messaging;
using Notification.Webhook.Domain.Entities;
using Notification.Webhook.Domain.Services;

namespace Notification.Webhook.Application.Tests;

public class SendWebhookCommandTests
{
    private readonly Mock<IWebhookSender> _mockSender;
    private readonly Mock<ILogger<SendWebhookCommand>> _mockLogger;
    private readonly SendWebhookCommand _command;

    public SendWebhookCommandTests()
    {
        _mockSender = new Mock<IWebhookSender>();
        _mockLogger = new Mock<ILogger<SendWebhookCommand>>();
        _command = new SendWebhookCommand(_mockSender.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidMessage_ShouldCallSender()
    {
        var message = new SendWebhookMessage(
            NotificationId: Guid.NewGuid(),
            WebhookUrl: "https://api.example.com/webhook",
            HttpMethod: "POST",
            Payload: "{\"event\":\"test\"}",
            ContentType: "application/json",
            Headers: null,
            Secret: null,
            MaxRetries: 3,
            TimeoutSeconds: 30);

        _mockSender
            .Setup(s => s.SendAsync(It.IsAny<WebhookNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookSendResult(
                Success: true,
                StatusCode: 200,
                ResponseBody: "OK",
                Duration: TimeSpan.FromMilliseconds(100)));

        var result = await _command.ExecuteAsync(message, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        _mockSender.Verify(s => s.SendAsync(It.IsAny<WebhookNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSenderFails_ShouldReturnError()
    {
        var message = new SendWebhookMessage(
            NotificationId: Guid.NewGuid(),
            WebhookUrl: "https://api.example.com/webhook",
            HttpMethod: "POST",
            Payload: "{\"event\":\"test\"}",
            ContentType: "application/json",
            Headers: null,
            Secret: null,
            MaxRetries: 3,
            TimeoutSeconds: 30);

        _mockSender
            .Setup(s => s.SendAsync(It.IsAny<WebhookNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookSendResult(
                Success: false,
                StatusCode: 500,
                ErrorMessage: "Internal Server Error"));

        var result = await _command.ExecuteAsync(message, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(500, result.StatusCode);
    }
}
