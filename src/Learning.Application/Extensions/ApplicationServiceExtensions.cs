using Learning.Application.Interfaces;
using Learning.Application.Services;
using Learning.Domain.Algorithms;
using Microsoft.Extensions.DependencyInjection;

namespace Learning.Application.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddLearningApplication(this IServiceCollection services)
    {
        services.AddSingleton<IReviewScheduler, Sm2ReviewScheduler>();
        services.AddScoped<ILearningService, LearningService>();
        return services;
    }
}
