using Squirrel.Wiki.Core.Configuration;

namespace Squirrel.Wiki.Web.Extensions;

public static class WebHostBuilderExtensions
{
    public static IWebHostBuilder ConfigureSquirrelWikiKestrel(
        this IWebHostBuilder webHostBuilder,
        ILogger logger)
    {
        // Set a generous hard-coded limit for Kestrel (10 GB)
        // The actual file size limit is enforced by FileService using ConfigurationService
        // which reads from the database setting SQUIRREL_FILE_MAX_SIZE_MB
        const long maxRequestBodySize = 10L * 1024L * 1024L * 1024L; // 10 GB in bytes

        webHostBuilder.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Limits.MaxRequestBodySize = maxRequestBodySize;
        });

        logger.LogInformation("Kestrel max request body size configured: 10 GB (actual file size limit enforced by FileService)");

        return webHostBuilder;
    }
}
