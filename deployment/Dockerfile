FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Notification.Webhook.Worker/Notification.Webhook.Worker.csproj", "src/Notification.Webhook.Worker/"]
COPY ["src/Notification.Webhook.Application/Notification.Webhook.Application.csproj", "src/Notification.Webhook.Application/"]
COPY ["src/Notification.Webhook.Domain/Notification.Webhook.Domain.csproj", "src/Notification.Webhook.Domain/"]
COPY ["src/Notification.Webhook.Infrastructure/Notification.Webhook.Infrastructure.csproj", "src/Notification.Webhook.Infrastructure/"]
RUN dotnet restore "src/Notification.Webhook.Worker/Notification.Webhook.Worker.csproj"
COPY . .
WORKDIR "/src/src/Notification.Webhook.Worker"
RUN dotnet build "Notification.Webhook.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Notification.Webhook.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Notification.Webhook.Worker.dll"]
