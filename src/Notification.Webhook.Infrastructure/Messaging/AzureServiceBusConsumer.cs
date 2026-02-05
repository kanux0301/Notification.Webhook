using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notification.Webhook.Application.Common.Messaging;
using Notification.Webhook.Infrastructure.Configuration;

namespace Notification.Webhook.Infrastructure.Messaging;

public class AzureServiceBusConsumer : IMessageConsumer, IAsyncDisposable
{
    private readonly AzureServiceBusOptions _options;
    private readonly ILogger<AzureServiceBusConsumer> _logger;
    private ServiceBusClient? _client;
    private ServiceBusProcessor? _processor;

    public AzureServiceBusConsumer(
        IOptions<AzureServiceBusOptions> options,
        ILogger<AzureServiceBusConsumer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(Func<SendWebhookMessage, CancellationToken, Task> handler, CancellationToken cancellationToken)
    {
        _client = new ServiceBusClient(_options.ConnectionString);
        _processor = _client.CreateProcessor(_options.QueueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 1
        });

        _processor.ProcessMessageAsync += async args =>
        {
            try
            {
                var message = JsonSerializer.Deserialize<SendWebhookMessage>(args.Message.Body.ToString());

                if (message != null)
                {
                    await handler(message, args.CancellationToken);
                }

                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
            }
        };

        _processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception, "Service Bus processor error");
            return Task.CompletedTask;
        };

        await _processor.StartProcessingAsync(cancellationToken);
        _logger.LogInformation("Azure Service Bus consumer started. Listening on queue: {QueueName}", _options.QueueName);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor != null)
            await _processor.StopProcessingAsync(cancellationToken);

        _logger.LogInformation("Azure Service Bus consumer stopped");
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor != null)
            await _processor.DisposeAsync();

        if (_client != null)
            await _client.DisposeAsync();
    }
}
