namespace Notification.Webhook.Infrastructure.Configuration;

public class MessagingOptions
{
    public const string SectionName = "Messaging";

    public string Provider { get; set; } = "RabbitMQ";
}

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string QueueName { get; set; } = "notifications.webhook";
}

public class AzureServiceBusOptions
{
    public const string SectionName = "AzureServiceBus";

    public string ConnectionString { get; set; } = string.Empty;
    public string QueueName { get; set; } = "notifications-webhook";
}

public class WebhookOptions
{
    public const string SectionName = "Webhook";

    public int DefaultTimeoutSeconds { get; set; } = 30;
    public int DefaultMaxRetries { get; set; } = 3;
    public int RetryDelayMilliseconds { get; set; } = 1000;
}
