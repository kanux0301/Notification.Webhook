using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Notification.Webhook.Application.Commands;
using Notification.Webhook.Application.Common.Messaging;
using Notification.Webhook.Domain.Services;
using Notification.Webhook.Infrastructure.Configuration;
using Notification.Webhook.Infrastructure.Messaging;
using Notification.Webhook.Infrastructure.Providers;

namespace Notification.Webhook.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MessagingOptions>(
            configuration.GetSection(MessagingOptions.SectionName));

        services.Configure<WebhookOptions>(
            configuration.GetSection(WebhookOptions.SectionName));

        services.AddScoped<SendWebhookCommand>();

        services.AddMessaging(configuration);
        services.AddWebhookSender(configuration);

        return services;
    }

    private static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var messagingProvider = configuration
            .GetSection(MessagingOptions.SectionName)
            .GetValue<string>("Provider") ?? "RabbitMQ";

        switch (messagingProvider.ToLowerInvariant())
        {
            case "azureservicebus":
                services.Configure<AzureServiceBusOptions>(
                    configuration.GetSection(AzureServiceBusOptions.SectionName));
                services.AddSingleton<IMessageConsumer, AzureServiceBusConsumer>();
                break;

            case "rabbitmq":
            default:
                services.Configure<RabbitMqOptions>(
                    configuration.GetSection(RabbitMqOptions.SectionName));
                services.AddSingleton<IMessageConsumer, RabbitMqConsumer>();
                break;
        }

        return services;
    }

    private static IServiceCollection AddWebhookSender(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration
            .GetSection(WebhookOptions.SectionName)
            .GetValue<string>("Provider") ?? "Http";

        switch (provider.ToLowerInvariant())
        {
            case "console":
                services.AddSingleton<IWebhookSender, ConsoleWebhookSender>();
                break;

            case "http":
            default:
                services.AddHttpClient<IWebhookSender, HttpWebhookSender>();
                break;
        }

        return services;
    }
}
