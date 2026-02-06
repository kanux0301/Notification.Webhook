using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Notification.Webhook.Application.Commands;
using Notification.Webhook.Application.Common.Messaging;
using Notification.Webhook.Domain.Services;

namespace Notification.Webhook.Worker.Tests;

public class WorkerTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IMessageConsumer> _mockMessageConsumer;
    private readonly Mock<ILogger<global::Worker>> _mockLogger;

    public WorkerTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockMessageConsumer = new Mock<IMessageConsumer>();
        _mockLogger = new Mock<ILogger<global::Worker>>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStartMessageConsumer()
    {
        // Arrange
        _mockMessageConsumer
            .Setup(c => c.StartAsync(It.IsAny<Func<SendWebhookMessage, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new global::Worker(_mockServiceProvider.Object, _mockMessageConsumer.Object, _mockLogger.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        // Act
        try
        {
            await worker.StartAsync(cts.Token);
            await Task.Delay(50);
            await worker.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        _mockMessageConsumer.Verify(
            c => c.StartAsync(It.IsAny<Func<SendWebhookMessage, CancellationToken, Task>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_ShouldStopMessageConsumer()
    {
        // Arrange
        _mockMessageConsumer
            .Setup(c => c.StartAsync(It.IsAny<Func<SendWebhookMessage, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockMessageConsumer
            .Setup(c => c.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var worker = new global::Worker(_mockServiceProvider.Object, _mockMessageConsumer.Object, _mockLogger.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        try
        {
            await worker.StartAsync(cts.Token);
            await Task.Delay(50);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Act
        await worker.StopAsync(CancellationToken.None);

        // Assert
        _mockMessageConsumer.Verify(c => c.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMessageReceived_ShouldExecuteCommand()
    {
        // Arrange
        var messageReceived = new TaskCompletionSource<bool>();
        Func<SendWebhookMessage, CancellationToken, Task>? capturedHandler = null;

        _mockMessageConsumer
            .Setup(c => c.StartAsync(It.IsAny<Func<SendWebhookMessage, CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Callback<Func<SendWebhookMessage, CancellationToken, Task>, CancellationToken>((handler, _) =>
            {
                capturedHandler = handler;
            })
            .Returns(Task.CompletedTask);

        var mockScope = new Mock<IServiceScope>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockScopedProvider = new Mock<IServiceProvider>();
        var mockWebhookSender = new Mock<IWebhookSender>();
        var mockCommandLogger = new Mock<ILogger<SendWebhookCommand>>();

        var command = new SendWebhookCommand(mockWebhookSender.Object, mockCommandLogger.Object);

        mockWebhookSender
            .Setup(s => s.SendAsync(It.IsAny<Domain.Entities.WebhookNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookSendResult(
                Success: true,
                StatusCode: 200,
                ResponseBody: "OK",
                Duration: TimeSpan.FromMilliseconds(100)));

        mockScopedProvider
            .Setup(p => p.GetService(typeof(SendWebhookCommand)))
            .Returns(command);

        mockScope.Setup(s => s.ServiceProvider).Returns(mockScopedProvider.Object);
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);
        _mockServiceProvider
            .Setup(p => p.GetService(typeof(IServiceScopeFactory)))
            .Returns(mockScopeFactory.Object);

        var worker = new global::Worker(_mockServiceProvider.Object, _mockMessageConsumer.Object, _mockLogger.Object);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(500);

        await worker.StartAsync(cts.Token);
        await Task.Delay(50);

        Assert.NotNull(capturedHandler);

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

        // Act
        await capturedHandler(message, CancellationToken.None);

        // Assert
        mockWebhookSender.Verify(
            s => s.SendAsync(It.IsAny<Domain.Entities.WebhookNotification>(), It.IsAny<CancellationToken>()),
            Times.Once);

        await worker.StopAsync(CancellationToken.None);
    }
}
