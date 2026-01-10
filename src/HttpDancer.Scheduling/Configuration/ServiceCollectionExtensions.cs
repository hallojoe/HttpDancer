using HttpDancer.Scheduling.RatedScheduling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HttpDancer.Scheduling.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSchedulingFeatures(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var optionsBuilder = services.AddOptions<RatedScheduleRunnerSettings>();
        if (configuration is not null)
        {
            optionsBuilder.Bind(configuration.GetSection(RatedScheduleRunnerSettings.Key));
        }

        services.AddSingleton<IRatedScheduleFactory>(serviceProvider =>
        {
            var ratedScheduleRunnerOptions = serviceProvider.GetService<IOptions<RatedScheduleRunnerSettings>>();
            return new RatedScheduleFactory(ratedScheduleRunnerOptions?.Value);
        });

        services.AddSingleton<IRatedScheduleRunner>(serviceProvider =>
        {
            var ratedScheduleRunnerOptions = serviceProvider.GetService<IOptions<RatedScheduleRunnerSettings>>();
            return new RatedScheduleRunner(ratedScheduleRunnerOptions?.Value);
        });
        
        return services;
    }
}
