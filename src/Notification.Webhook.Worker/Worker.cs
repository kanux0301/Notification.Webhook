using Notification.Webhook.Application.Commands;
using Notification.Webhook.Application.Common.Messaging;

public class Worker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMessageConsumer _messageConsumer;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IServiceProvider serviceProvider,
        IMessageConsumer messageConsumer,
        ILogger<Worker> logger)
    {
        _serviceProvider = serviceProvider;
        _messageConsumer = messageConsumer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook Notification Worker starting...");

        await _messageConsumer.StartAsync(async (message, ct) =>
        {
            using var scope = _serviceProvider.CreateScope();
            var command = scope.ServiceProvider.GetRequiredService<SendWebhookCommand>();
            await command.ExecuteAsync(message, ct);
        }, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Webhook Notification Worker stopping...");
        await _messageConsumer.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}
