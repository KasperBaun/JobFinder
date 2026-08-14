using Jobmatch.Platform.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Jobmatch.Api.Infrastructure;

/// <summary>
/// The wire's JSON policy, taken from Platform.Json so HTTP responses, the SSE feed and the
/// on-disk records cannot drift apart: camelCase members, enums as camelCase strings (so
/// JobSearchState/Phase serialise as "running"/"llmJudging", not 4), and nulls omitted.
/// </summary>
public static class JobmatchJsonSetup
{
    public static IServiceCollection AddJobmatchJson(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            var shared = JobmatchJsonOptions.Default;
            options.SerializerOptions.PropertyNamingPolicy = shared.PropertyNamingPolicy;
            options.SerializerOptions.DefaultIgnoreCondition = shared.DefaultIgnoreCondition;
            options.SerializerOptions.Encoder = shared.Encoder;
            foreach (var converter in shared.Converters)
                options.SerializerOptions.Converters.Add(converter);
        });

        return services;
    }
}
