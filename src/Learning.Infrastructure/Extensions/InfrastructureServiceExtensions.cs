using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Kafka;
using BuildingBlocks.Messaging.Outbox;
using Learning.Application.Interfaces;
using Learning.Infrastructure.Data;
using Learning.Infrastructure.Inbox;
using Learning.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Learning.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddLearningInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("LearningDb")
            ?? configuration["LEARNING_DB_CONNECTION"]
            ?? "Server=localhost,1433;Database=LearningDb;User Id=sa;Password=YourStrong@Passw0rd!2026;TrustServerCertificate=True;";

        services.AddDbContext<LearningDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(LearningDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            });
        });

        services.AddScoped<ILearningDbContext>(sp => sp.GetRequiredService<LearningDbContext>());
        services.AddScoped<IOutboxRepository, SqlLearningOutboxRepository>();
        services.AddScoped<IInboxRepository, SqlLearningInboxRepository>();

        // Kafka producer & outbox worker
        services.Configure<KafkaProducerConfig>(options =>
        {
            var section = configuration.GetSection("Kafka");
            options.BootstrapServers = section["BootstrapServers"]
                ?? configuration["KAFKA_BOOTSTRAP_SERVERS"]
                ?? "localhost:9092";
            options.ClientId = section["ClientId"] ?? "learning-service-producer";
        });

        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
        services.AddSingleton<OutboxPublisherWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<OutboxPublisherWorker>());

        // Redis
        string redisConn = configuration.GetConnectionString("Redis")
            ?? configuration["REDIS_CONNECTION"]
            ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var config = ConfigurationOptions.Parse(redisConn);
            config.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(config);
        });

        return services;
    }
}
