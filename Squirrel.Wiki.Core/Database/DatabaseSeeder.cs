using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Configuration;
using Squirrel.Wiki.Core.Database.Entities;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Squirrel.Wiki.Core.Database;

public static class DatabaseSeeder
{
    private const string SeedDataFileName = "seed-data.yaml";

    /// <summary>
    /// Seeds the database with initial data from the default embedded seed-data.yaml file
    /// </summary>
    public static async Task SeedAsync(SquirrelDbContext context, ILogger logger)
    {
        await SeedAsync(context, logger, null);
    }

    /// <summary>
    /// Seeds the database with initial data from a custom seed data file or the default embedded file
    /// </summary>
    /// <param name="context">The database context</param>
    /// <param name="logger">The logger</param>
    /// <param name="customSeedDataPath">Optional path to a custom seed data YAML file. If null, uses the default embedded file.</param>
    public static async Task SeedAsync(SquirrelDbContext context, ILogger logger, string? customSeedDataPath)
    {
        try
        {
            // Check if database is already seeded
            if (await context.Pages.AnyAsync())
            {
                logger.LogInformation("Database already seeded, skipping seed data");
                return;
            }

            logger.LogInformation("Seeding database with initial data...");

            // Load seed data from YAML file
            var seedData = LoadSeedData(logger, customSeedDataPath);
            if (seedData == null)
            {
                logger.LogWarning("No seed data loaded, skipping database seeding");
                return;
            }

            // Seed Categories
            var categories = await SeedCategoriesAsync(context, seedData.Categories, logger);
            
            // Seed Tags
            var tags = await SeedTagsAsync(context, seedData.Tags, logger);
            
            // Seed Pages with Content (using system user)
            await SeedPagesAsync(context, seedData.Pages, categories, tags, logger);
            
            // Seed Menus
            await SeedMenusAsync(context, seedData.Menus, logger);
            
            // Seed Settings from YAML
            await SeedSettingsAsync(context, seedData.Settings, logger);
            
            // Seed Site Configuration (still hardcoded for system settings)
            await SeedSiteConfigurationAsync(context, logger);

            await context.SaveChangesAsync();
            
            logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database");
            throw;
        }
    }

    private static SeedData? LoadSeedData(ILogger logger, string? customSeedDataPath)
    {
        try
        {
            string seedFilePath;
            
            // If a custom seed data path is provided, use it
            if (!string.IsNullOrEmpty(customSeedDataPath))
            {
                seedFilePath = customSeedDataPath;
                
                if (!File.Exists(seedFilePath))
                {
                    logger.LogWarning("Custom seed data file not found: {Path}", seedFilePath);
                    return null;
                }
                
                logger.LogInformation("Loading seed data from custom file: {Path}", seedFilePath);
            }
            else
            {
                // Look for seed-data.yaml in the Database directory
                var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
                seedFilePath = Path.Combine(assemblyDirectory!, "Database", SeedDataFileName);

                // If not found in assembly directory, try current directory
                if (!File.Exists(seedFilePath))
                {
                    seedFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Database", SeedDataFileName);
                }

                // If still not found, try one level up (for development scenarios)
                if (!File.Exists(seedFilePath))
                {
                    var parentDir = Directory.GetParent(Directory.GetCurrentDirectory())?.FullName;
                    if (parentDir != null)
                    {
                        seedFilePath = Path.Combine(parentDir, "Database", SeedDataFileName);
                    }
                }

                if (!File.Exists(seedFilePath))
                {
                    logger.LogWarning("Seed data file not found at expected locations. Tried: {Path}", seedFilePath);
                    return null;
                }

                logger.LogInformation("Loading seed data from default file: {Path}", seedFilePath);
            }

            var yaml = File.ReadAllText(seedFilePath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var seedData = deserializer.Deserialize<SeedData>(yaml);
            
            logger.LogInformation("Loaded seed data: {Categories} categories, {Tags} tags, {Pages} pages, {Menus} menus, {Settings} settings",
                seedData.Categories.Count, seedData.Tags.Count, seedData.Pages.Count, seedData.Menus.Count, seedData.Settings.Count);

            return seedData;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load seed data from YAML file");
            return null;
        }
    }

    private static async Task<List<Category>> SeedCategoriesAsync(SquirrelDbContext context, List<SeedCategory> seedCategories, ILogger logger)
    {
        logger.LogInformation("Seeding categories...");
        
        var now = DateTime.UtcNow;
        var categories = seedCategories.Select(sc => new Category
        {
            Name = sc.Name,
            Slug = sc.Slug,
            Description = sc.Description,
            DisplayOrder = sc.DisplayOrder,
            CreatedOn = now,
            CreatedBy = "system",
            ModifiedOn = now,
            ModifiedBy = "system"
        }).ToList();

        await context.Categories.AddRangeAsync(categories);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Seeded {Count} categories", categories.Count);
        return categories;
    }

    private static async Task<List<Tag>> SeedTagsAsync(SquirrelDbContext context, List<SeedTag> seedTags, ILogger logger)
    {
        logger.LogInformation("Seeding tags...");
        
        var tags = seedTags.Select(st => new Tag
        {
            Name = st.Name,
            NormalizedName = st.NormalizedName
        }).ToList();

        await context.Tags.AddRangeAsync(tags);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Seeded {Count} tags", tags.Count);
        return tags;
    }

    private static async Task SeedPagesAsync(SquirrelDbContext context, List<SeedPage> seedPages, List<Category> categories, List<Tag> tags, ILogger logger)
    {
        logger.LogInformation("Seeding pages...");
        
        var now = DateTime.UtcNow;
        const string systemUser = "system";

        var pages = new List<Page>();

        foreach (var seedPage in seedPages)
        {
            // Find category by slug if specified
            Category? category = null;
            if (!string.IsNullOrEmpty(seedPage.Category))
            {
                category = categories.FirstOrDefault(c => c.Slug == seedPage.Category);
                if (category == null)
                {
                    logger.LogWarning("Category '{Category}' not found for page '{Title}', skipping category assignment", 
                        seedPage.Category, seedPage.Title);
                }
            }

            // Find tags by name
            var pageTags = new List<PageTag>();
            foreach (var tagName in seedPage.Tags)
            {
                var tag = tags.FirstOrDefault(t => t.Name == tagName);
                if (tag != null)
                {
                    pageTags.Add(new PageTag { TagId = tag.Id });
                }
                else
                {
                    logger.LogWarning("Tag '{Tag}' not found for page '{Title}', skipping tag", tagName, seedPage.Title);
                }
            }

            var page = new Page
            {
                Title = seedPage.Title,
                Slug = seedPage.Slug,
                CategoryId = category?.Id,
                CreatedOn = now,
                CreatedBy = systemUser,
                ModifiedOn = now,
                ModifiedBy = systemUser,
                IsLocked = false,
                Contents = new List<PageContent>
                {
                    new PageContent
                    {
                        VersionNumber = 1,
                        Text = seedPage.Content,
                        EditedOn = now,
                        EditedBy = systemUser
                    }
                },
                PageTags = pageTags
            };

            pages.Add(page);
        }

        await context.Pages.AddRangeAsync(pages);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Seeded {Count} pages with content", pages.Count);
    }

    private static async Task SeedMenusAsync(SquirrelDbContext context, List<SeedMenu> seedMenus, ILogger logger)
    {
        logger.LogInformation("Seeding menus...");
        
        var now = DateTime.UtcNow;
        var menus = new List<Menu>();

        foreach (var seedMenu in seedMenus)
        {
            // Parse menu type from string
            if (!Enum.TryParse<MenuType>(seedMenu.Type, true, out var menuType))
            {
                logger.LogWarning("Invalid menu type '{Type}' for menu '{Name}', skipping", seedMenu.Type, seedMenu.Name);
                continue;
            }

            var menu = new Menu
            {
                Name = seedMenu.Name,
                MenuType = menuType,
                Description = seedMenu.Description,
                Markup = seedMenu.Markup,
                FooterLeftZone = seedMenu.FooterLeftZone,
                FooterRightZone = seedMenu.FooterRightZone,
                DisplayOrder = seedMenu.DisplayOrder,
                IsEnabled = seedMenu.IsEnabled,
                ModifiedOn = now,
                ModifiedBy = "system"
            };

            menus.Add(menu);
        }

        await context.Menus.AddRangeAsync(menus);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Seeded {Count} menus", menus.Count);
    }

    private static async Task SeedSettingsAsync(SquirrelDbContext context, List<SeedSetting> seedSettings, ILogger logger)
    {
        logger.LogInformation("Seeding settings from YAML...");
        
        var now = DateTime.UtcNow;
        var settings = new List<SiteConfiguration>();

        foreach (var seedSetting in seedSettings)
        {
            var setting = new SiteConfiguration
            {
                Key = seedSetting.Key,
                Value = seedSetting.Value, // Store as plain string, not JSON-serialized
                ModifiedOn = now,
                ModifiedBy = "system",
                IsFromEnvironment = false
            };

            settings.Add(setting);
        }

        await context.SiteConfigurations.AddRangeAsync(settings);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Seeded {Count} settings from YAML", settings.Count);
    }

    private static async Task SeedSiteConfigurationAsync(SquirrelDbContext context, ILogger logger)
    {
        logger.LogInformation("Seeding site configuration from ConfigurationMetadataRegistry...");
        
        var now = DateTime.UtcNow;
        var configs = new List<SiteConfiguration>();

        // Get all configuration metadata from the registry
        var allMetadata = ConfigurationMetadataRegistry.GetAllMetadata();

        // Get existing keys to avoid duplicates
        var existingKeys = await context.SiteConfigurations
            .Select(s => s.Key)
            .ToListAsync();

        foreach (var metadata in allMetadata)
        {
            // Skip if already exists (from YAML or previous seeding)
            if (existingKeys.Contains(metadata.Key))
            {
                logger.LogDebug("Skipping {Key} - already exists in database", metadata.Key);
                continue;
            }

            // Only seed settings that are visible in the UI
            // Settings like SQUIRREL_ADMIN_PASSWORD should only come from environment variables
            // and should not be stored in the database
            if (!metadata.IsVisibleInUI)
            {
                logger.LogDebug("Skipping {Key} - not visible in UI (should only come from environment variables)", metadata.Key);
                continue;
            }

            // Serialize the default value based on type
            // Note: We store values as plain strings, not JSON-serialized
            string serializedValue;
            if (metadata.DefaultValue == null)
            {
                serializedValue = string.Empty;
            }
            else if (metadata.ValueType == typeof(string))
            {
                // Store string values directly without JSON serialization
                serializedValue = metadata.DefaultValue.ToString()!;
            }
            else if (metadata.ValueType == typeof(bool))
            {
                serializedValue = metadata.DefaultValue.ToString()!.ToLower();
            }
            else if (metadata.ValueType == typeof(int) || metadata.ValueType == typeof(long) || metadata.ValueType == typeof(double))
            {
                serializedValue = metadata.DefaultValue.ToString()!;
            }
            else
            {
                // For other types, convert to string
                serializedValue = metadata.DefaultValue.ToString()!;
            }

            var config = new SiteConfiguration
            {
                Key = metadata.Key,
                Value = serializedValue,
                ModifiedOn = now,
                ModifiedBy = "system",
                IsFromEnvironment = false
            };

            configs.Add(config);
        }

        if (configs.Any())
        {
            await context.SiteConfigurations.AddRangeAsync(configs);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} configuration entries from registry", configs.Count);
        }
        else
        {
            logger.LogInformation("No new configuration entries to seed - all already exist");
        }
    }
}
