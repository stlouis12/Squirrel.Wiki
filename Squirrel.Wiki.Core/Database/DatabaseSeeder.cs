using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Core.Database.Entities;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Squirrel.Wiki.Core.Database;

public static class DatabaseSeeder
{
    private const string SeedDataFileName = "seed-data.yaml";

    public static async Task SeedAsync(SquirrelDbContext context, ILogger logger)
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
            var seedData = LoadSeedData(logger);
            if (seedData == null)
            {
                logger.LogWarning("No seed data loaded, skipping database seeding");
                return;
            }

            // Seed Categories
            var categories = await SeedCategoriesAsync(context, seedData.Categories, logger);
            
            // Seed Tags
            var tags = await SeedTagsAsync(context, seedData.Tags, logger);
            
            // Seed Users (still hardcoded as they contain sensitive data)
            var users = await SeedUsersAsync(context, logger);
            
            // Seed Pages with Content
            await SeedPagesAsync(context, seedData.Pages, categories, tags, users, logger);
            
            // Seed Menus
            await SeedMenusAsync(context, seedData.Menus, logger);
            
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

    private static SeedData? LoadSeedData(ILogger logger)
    {
        try
        {
            // Look for seed-data.yaml in the Database directory
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
            var seedFilePath = Path.Combine(assemblyDirectory!, "Database", SeedDataFileName);

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

            logger.LogInformation("Loading seed data from: {Path}", seedFilePath);

            var yaml = File.ReadAllText(seedFilePath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var seedData = deserializer.Deserialize<SeedData>(yaml);
            
            logger.LogInformation("Loaded seed data: {Categories} categories, {Tags} tags, {Pages} pages, {Menus} menus",
                seedData.Categories.Count, seedData.Tags.Count, seedData.Pages.Count, seedData.Menus.Count);

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

    private static async Task<List<User>> SeedUsersAsync(SquirrelDbContext context, ILogger logger)
    {
        logger.LogInformation("Seeding users...");
        
        var now = DateTime.UtcNow;
        var users = new List<User>
        {
            new User
            {
                Username = "admin",
                Email = "admin@squirrel.wiki",
                DisplayName = "Admin User",
                ExternalId = "dev-admin-001",
                IsAdmin = true,
                IsEditor = true,
                CreatedOn = now,
                LastLoginOn = now
            },
            new User
            {
                Username = "editor",
                Email = "editor@squirrel.wiki",
                DisplayName = "Editor User",
                ExternalId = "dev-editor-001",
                IsAdmin = false,
                IsEditor = true,
                CreatedOn = now,
                LastLoginOn = now
            },
            new User
            {
                Username = "viewer",
                Email = "viewer@squirrel.wiki",
                DisplayName = "Viewer User",
                ExternalId = "dev-viewer-001",
                IsAdmin = false,
                IsEditor = false,
                CreatedOn = now,
                LastLoginOn = now
            }
        };

        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Seeded {Count} users", users.Count);
        return users;
    }

    private static async Task SeedPagesAsync(SquirrelDbContext context, List<SeedPage> seedPages, List<Category> categories, List<Tag> tags, List<User> users, ILogger logger)
    {
        logger.LogInformation("Seeding pages...");
        
        var now = DateTime.UtcNow;
        var adminUser = users.First(u => u.IsAdmin);

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
                CreatedBy = adminUser.Email,
                ModifiedOn = now,
                ModifiedBy = adminUser.Email,
                IsLocked = false,
                Contents = new List<PageContent>
                {
                    new PageContent
                    {
                        VersionNumber = 1,
                        Text = seedPage.Content,
                        EditedOn = now,
                        EditedBy = adminUser.Email
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

    private static async Task SeedSiteConfigurationAsync(SquirrelDbContext context, ILogger logger)
    {
        logger.LogInformation("Seeding site configuration...");
        
        var now = DateTime.UtcNow;
        var configs = new List<SiteConfiguration>
        {
            new SiteConfiguration
            {
                Key = "SiteName",
                Value = "\"Squirrel Wiki\"",
                ModifiedOn = now,
                ModifiedBy = "system"
            },
            new SiteConfiguration
            {
                Key = "SiteUrl",
                Value = "\"http://localhost:5000\"",
                ModifiedOn = now,
                ModifiedBy = "system"
            },
            new SiteConfiguration
            {
                Key = "Theme",
                Value = "\"default\"",
                ModifiedOn = now,
                ModifiedBy = "system"
            },
            new SiteConfiguration
            {
                Key = "MarkupType",
                Value = "\"Markdown\"",
                ModifiedOn = now,
                ModifiedBy = "system"
            },
            new SiteConfiguration
            {
                Key = "IsPublicSite",
                Value = "true",
                ModifiedOn = now,
                ModifiedBy = "system"
            },
            new SiteConfiguration
            {
                Key = "AllowedFileTypes",
                Value = "\".jpg,.jpeg,.png,.gif,.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.zip\"",
                ModifiedOn = now,
                ModifiedBy = "system"
            },
            new SiteConfiguration
            {
                Key = "MaxAttachmentSize",
                Value = "10485760",
                ModifiedOn = now,
                ModifiedBy = "system"
            },
            new SiteConfiguration
            {
                Key = "UseHtmlWhiteList",
                Value = "true",
                ModifiedOn = now,
                ModifiedBy = "system"
            },
            new SiteConfiguration
            {
                Key = "MenuMarkupEnabled",
                Value = "true",
                ModifiedOn = now,
                ModifiedBy = "system"
            }
        };

        await context.SiteConfigurations.AddRangeAsync(configs);
        await context.SaveChangesAsync();
        
        logger.LogInformation("Seeded {Count} site configuration entries", configs.Count);
    }
}
