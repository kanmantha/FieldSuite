using FieldSuite.Infrastructure.Data;
using FieldSuite.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FieldSuite.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(GetConnectionString(configuration)));

        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.AddHttpClient(OpenAICompatibleLlmClient.HttpClientName, (sp, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddScoped<ILLMClient>(sp =>
        {
            var options = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new OpenAICompatibleLlmClient(factory, options);
        });

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IPermitRiskScoringService, PermitRiskScoringService>();
        services.AddScoped<ICostSuggestionService, CostSuggestionService>();
        services.AddScoped<INudgeEngine, NudgeEngine>();
        services.AddScoped<IStructuredDraftService, StructuredDraftService>();
        services.AddScoped<IAssistantService, AssistantService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }

    private static string GetConnectionString(IConfiguration configuration)
    {
        var databaseUrl = configuration["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(databaseUrl)) return ParseDatabaseUrl(databaseUrl);

        return configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("No database connection string configured.");
    }

    private static string ParseDatabaseUrl(string url)
    {
        var uri = new Uri(url);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = SslMode.Require
        };
        if (uri.Port > 0) builder.Port = uri.Port;

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            var separator = uri.UserInfo.IndexOf(':');
            if (separator > 0)
            {
                builder.Username = Uri.UnescapeDataString(uri.UserInfo[..separator]);
                builder.Password = Uri.UnescapeDataString(uri.UserInfo[(separator + 1)..]);
            }
            else
            {
                builder.Username = Uri.UnescapeDataString(uri.UserInfo);
            }
        }

        return builder.ConnectionString;
    }
}
