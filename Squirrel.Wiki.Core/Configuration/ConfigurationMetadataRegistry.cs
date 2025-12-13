using Squirrel.Wiki.Contracts.Configuration;

namespace Squirrel.Wiki.Core.Configuration;

/// <summary>
/// Central registry of all configuration property metadata
/// </summary>
public static class ConfigurationMetadataRegistry
{
    /// <summary>
    /// Configuration category constants
    /// </summary>
    public static class Categories
    {
        public const string General = "General";
        public const string Security = "Security";
        public const string Content = "Content";
        public const string Performance = "Performance";
        public const string Application = "Application";
        public const string Database = "Database";
        public const string Files = "Files";
    }

    public static class ConfigurationKeys
    {
        // Site Configuration
        public const string SQUIRREL_SITE_NAME = "SQUIRREL_SITE_NAME";
        public const string SQUIRREL_SITE_URL = "SQUIRREL_SITE_URL";
        public const string SQUIRREL_DEFAULT_LANGUAGE = "SQUIRREL_DEFAULT_LANGUAGE";
        public const string SQUIRREL_TIMEZONE = "SQUIRREL_TIMEZONE";

        // Admin Bootstrap Configuration
        public const string SQUIRREL_ADMIN_USERNAME = "SQUIRREL_ADMIN_USERNAME";
        public const string SQUIRREL_ADMIN_PASSWORD = "SQUIRREL_ADMIN_PASSWORD";
        public const string SQUIRREL_ADMIN_EMAIL = "SQUIRREL_ADMIN_EMAIL";
        public const string SQUIRREL_ADMIN_DISPLAYNAME = "SQUIRREL_ADMIN_DISPLAYNAME";

        // Security Configuration
        public const string SQUIRREL_ALLOW_ANONYMOUS_READING = "SQUIRREL_ALLOW_ANONYMOUS_READING";
        public const string SQUIRREL_SESSION_TIMEOUT_MINUTES = "SQUIRREL_SESSION_TIMEOUT_MINUTES";
        public const string SQUIRREL_MAX_LOGIN_ATTEMPTS = "SQUIRREL_MAX_LOGIN_ATTEMPTS";
        public const string SQUIRREL_ACCOUNT_LOCK_DURATION_MINUTES = "SQUIRREL_ACCOUNT_LOCK_DURATION_MINUTES";

        // Content Configuration
        public const string SQUIRREL_DEFAULT_PAGE_TEMPLATE = "SQUIRREL_DEFAULT_PAGE_TEMPLATE";
        public const string SQUIRREL_MAX_PAGE_TITLE_LENGTH = "SQUIRREL_MAX_PAGE_TITLE_LENGTH";
        public const string SQUIRREL_ENABLE_PAGE_VERSIONING = "SQUIRREL_ENABLE_PAGE_VERSIONING";

        // Performance Configuration
        public const string SQUIRREL_ENABLE_CACHING = "SQUIRREL_ENABLE_CACHING";
        public const string SQUIRREL_CACHE_DURATION_MINUTES = "SQUIRREL_CACHE_DURATION_MINUTES";
        public const string SQUIRREL_ENABLE_RESPONSE_CACHING = "SQUIRREL_ENABLE_RESPONSE_CACHING";
        public const string SQUIRREL_RESPONSE_CACHE_DURATION_MINUTES = "SQUIRREL_RESPONSE_CACHE_DURATION_MINUTES";
        public const string SQUIRREL_CACHE_PROVIDER = "SQUIRREL_CACHE_PROVIDER";
        public const string SQUIRREL_REDIS_CONFIGURATION = "SQUIRREL_REDIS_CONFIGURATION";
        public const string SQUIRREL_REDIS_INSTANCE_NAME = "SQUIRREL_REDIS_INSTANCE_NAME";

        // Application Paths
        public const string SQUIRREL_APP_DATA_PATH = "SQUIRREL_APP_DATA_PATH";
        public const string SQUIRREL_SEED_DATA_FILE_PATH = "SQUIRREL_SEED_DATA_FILE_PATH";

        // Database Configuration
        public const string SQUIRREL_DATABASE_PROVIDER = "SQUIRREL_DATABASE_PROVIDER";
        public const string SQUIRREL_DATABASE_CONNECTION_STRING = "SQUIRREL_DATABASE_CONNECTION_STRING";
        public const string SQUIRREL_DATABASE_AUTO_MIGRATE = "SQUIRREL_DATABASE_AUTO_MIGRATE";
        public const string SQUIRREL_DATABASE_SEED_DATA = "SQUIRREL_DATABASE_SEED_DATA";

        // File Storage Configuration
        public const string SQUIRREL_FILE_STORAGE_PATH = "SQUIRREL_FILE_STORAGE_PATH";
        public const string SQUIRREL_FILE_MAX_SIZE_MB = "SQUIRREL_FILE_MAX_SIZE_MB";
        public const string SQUIRREL_FILE_ALLOWED_EXTENSIONS = "SQUIRREL_FILE_ALLOWED_EXTENSIONS";
    }

    private static readonly Dictionary<string, ConfigurationProperty> _metadata = new()
    {
        // Site Configuration
        {
            ConfigurationKeys.SQUIRREL_SITE_NAME,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_SITE_NAME,
                DisplayName = "Site Name",
                Description = "The name of your wiki site",
                Category = Categories.General,
                ValueType = typeof(string),
                DefaultValue = "Squirrel Wiki",
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_SITE_NAME,
                IsSecret = false,
                AllowRuntimeModification = true,
                Validation = new ValidationRules()
            }
        },
        {
            ConfigurationKeys.SQUIRREL_SITE_URL,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_SITE_URL,
                DisplayName = "Site URL",
                Description = "The public URL of your wiki site (e.g., https://wiki.example.com)",
                Category = Categories.General,
                ValueType = typeof(string),
                DefaultValue = "",
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_SITE_URL,
                IsSecret = false,
                AllowRuntimeModification = true,
                Validation = new ValidationRules
                {
                    MustBeUrl = true
                }
            }
        },
        {
            ConfigurationKeys.SQUIRREL_DEFAULT_LANGUAGE,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_DEFAULT_LANGUAGE,
                DisplayName = "Default Language",
                Description = "Default language for the wiki interface",
                Category = Categories.General,
                ValueType = typeof(string),
                DefaultValue = "en",
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_DEFAULT_LANGUAGE,
                IsSecret = false,
                AllowRuntimeModification = true,
                Validation = new ValidationRules
                {
                    AllowedValues = new[] { "en", "es", "fr", "de", "it" }
                }
            }
        },
        {
            ConfigurationKeys.SQUIRREL_TIMEZONE,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_TIMEZONE,
                DisplayName = "Time Zone",
                Description = "Default time zone for displaying dates and times",
                Category = Categories.General,
                ValueType = typeof(string),
                DefaultValue = "UTC",
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_TIMEZONE,
                IsSecret = false,
                AllowRuntimeModification = true,
                Validation = new ValidationRules()
            }
        },

        // Admin Bootstrap Configuration
        {
            ConfigurationKeys.SQUIRREL_ADMIN_USERNAME,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_ADMIN_USERNAME,
                DisplayName = "Admin Username",
                Description = "Default admin username for initial bootstrap",
                Category = Categories.Security,
                ValueType = typeof(string),
                DefaultValue = "admin",
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_ADMIN_USERNAME,
                IsSecret = false,
                AllowRuntimeModification = false,
                IsVisibleInUI = false,
                Validation = new ValidationRules()
            }
        },
        {
            ConfigurationKeys.SQUIRREL_ADMIN_PASSWORD,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_ADMIN_PASSWORD,
                DisplayName = "Admin Password",
                Description = "Default admin password for initial bootstrap (should be changed after first login)",
                Category = Categories.Security,
                ValueType = typeof(string),
                DefaultValue = "Squirrel123!",
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_ADMIN_PASSWORD,
                IsSecret = true,
                AllowRuntimeModification = false,
                IsVisibleInUI = false,
                Validation = new ValidationRules()
            }
        },
        {
            ConfigurationKeys.SQUIRREL_ADMIN_EMAIL,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_ADMIN_EMAIL,
                DisplayName = "Admin Email",
                Description = "Default admin email address for initial bootstrap",
                Category = Categories.Security,
                ValueType = typeof(string),
                DefaultValue = "admin@localhost",
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_ADMIN_EMAIL,
                IsSecret = false,
                AllowRuntimeModification = false,
                IsVisibleInUI = false,
                Validation = new ValidationRules()
            }
        },
        {
            ConfigurationKeys.SQUIRREL_ADMIN_DISPLAYNAME,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_ADMIN_DISPLAYNAME,
                DisplayName = "Admin Display Name",
                Description = "Default admin display name for initial bootstrap",
                Category = Categories.Security,
                ValueType = typeof(string),
                DefaultValue = "Administrator",
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_ADMIN_DISPLAYNAME,
                IsSecret = false,
                AllowRuntimeModification = false,
                IsVisibleInUI = false,
                Validation = new ValidationRules()
            }
        },

        // Security Configuration
        {
            ConfigurationKeys.SQUIRREL_ALLOW_ANONYMOUS_READING,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_ALLOW_ANONYMOUS_READING,
                DisplayName = "Allow Anonymous Reading",
                Description = "Whether unauthenticated users can read wiki pages",
                Category = Categories.Security,
                ValueType = typeof(bool),
                DefaultValue = false,
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_ALLOW_ANONYMOUS_READING,
                IsSecret = false,
                AllowRuntimeModification = true,
                Validation = new ValidationRules()
            }
        },
        {
            ConfigurationKeys.SQUIRREL_SESSION_TIMEOUT_MINUTES,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_SESSION_TIMEOUT_MINUTES,
                DisplayName = "Session Timeout (Minutes)",
                Description = "How long a user session remains active without activity",
                Category = Categories.Security,
                ValueType = typeof(int),
                DefaultValue = 480,
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_SESSION_TIMEOUT_MINUTES,
                IsSecret = false,
                AllowRuntimeModification = true,
                Validation = new ValidationRules
                {
                    MinValue = 30,
                    MaxValue = 20160 // 14 days
                }
            }
        },
        {
            ConfigurationKeys.SQUIRREL_MAX_LOGIN_ATTEMPTS,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_MAX_LOGIN_ATTEMPTS,
                DisplayName = "Maximum Login Attempts",
                Description = "Number of failed login attempts before account is locked",
                Category = Categories.Security,
                ValueType = typeof(int),
                DefaultValue = 5,
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_MAX_LOGIN_ATTEMPTS,
                IsSecret = false,
                AllowRuntimeModification = true,
                Validation = new ValidationRules
                {
                    MinValue = 3,
                    MaxValue = 20
                }
            }
        },
        {
            ConfigurationKeys.SQUIRREL_ACCOUNT_LOCK_DURATION_MINUTES,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_ACCOUNT_LOCK_DURATION_MINUTES,
                DisplayName = "Account Lock Duration (Minutes)",
                Description = "How long an account remains locked after too many failed login attempts",
                Category = Categories.Security,
                ValueType = typeof(int),
                DefaultValue = 30,
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_ACCOUNT_LOCK_DURATION_MINUTES,
                IsSecret = false,
                AllowRuntimeModification = true,
                Validation = new ValidationRules
                {
                    MinValue = 5,
                    MaxValue = 1440 // 24 hours
                }
            }
        },

        // Content Configuration
        {
            ConfigurationKeys.SQUIRREL_DEFAULT_PAGE_TEMPLATE,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_DEFAULT_PAGE_TEMPLATE,
                DisplayName = "Default Page Template",
                Description = "Default markdown template for new pages",
                Category = Categories.Content,
                ValueType = typeof(string),
                DefaultValue = "",
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_DEFAULT_PAGE_TEMPLATE,
                IsSecret = false,
                AllowRuntimeModification = true,
                Validation = new ValidationRules()
            }
        },
        {
            ConfigurationKeys.SQUIRREL_MAX_PAGE_TITLE_LENGTH,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_MAX_PAGE_TITLE_LENGTH,
                DisplayName = "Maximum Page Title Length",
                Description = "Maximum number of characters allowed in page titles",
                Category = Categories.Content,
                ValueType = typeof(int),
                DefaultValue = 200,
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_MAX_PAGE_TITLE_LENGTH,
                IsSecret = false,
                AllowRuntimeModification = true,
                Validation = new ValidationRules
                {
                    MinValue = 25,
                    MaxValue = 500
                }
            }
        },
        {
            ConfigurationKeys.SQUIRREL_ENABLE_PAGE_VERSIONING,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_ENABLE_PAGE_VERSIONING,
                DisplayName = "Enable Page Versioning",
                Description = "Whether to keep historical versions of pages",
                Category = Categories.Content,
                ValueType = typeof(bool),
                DefaultValue = false,
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_ENABLE_PAGE_VERSIONING,
                IsSecret = false,
                AllowRuntimeModification = true,
                Validation = new ValidationRules()
            }
        },

        // Performance Configuration - Memory Cache
        {
            ConfigurationKeys.SQUIRREL_ENABLE_CACHING,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_ENABLE_CACHING,
                DisplayName = "Memory Cache",
                Description = "Whether to enable in-memory caching for improved performance",
                Category = Categories.Performance,
                ValueType = typeof(bool),
                DefaultValue = true,
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_ENABLE_CACHING,
                IsSecret = false,
                AllowRuntimeModification = true,
                Validation = new ValidationRules()
            }
        },
        {
            ConfigurationKeys.SQUIRREL_CACHE_DURATION_MINUTES,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_CACHE_DURATION_MINUTES,
                DisplayName = "Memory Cache Duration (Minutes)",
                Description = "How long cached items remain valid in memory",
                Category = Categories.Performance,
                ValueType = typeof(int),
                DefaultValue = 60,
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_CACHE_DURATION_MINUTES,
                IsSecret = false,
                AllowRuntimeModification = true,
                Validation = new ValidationRules
                {
                    MinValue = 1,
                    MaxValue = 1440 // 24 hours
                }
            }
        },
        
        // Performance Configuration - Response Cache
        {
            ConfigurationKeys.SQUIRREL_ENABLE_RESPONSE_CACHING,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_ENABLE_RESPONSE_CACHING,
                DisplayName = "Response Caching",
                Description = "Whether to enable HTTP response caching for pages",
                Category = Categories.Performance,
                ValueType = typeof(bool),
                DefaultValue = true,
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_ENABLE_RESPONSE_CACHING,
                IsSecret = false,
                AllowRuntimeModification = true,
                Validation = new ValidationRules()
            }
        },
        {
            ConfigurationKeys.SQUIRREL_RESPONSE_CACHE_DURATION_MINUTES,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_RESPONSE_CACHE_DURATION_MINUTES,
                DisplayName = "Response Cache Duration (Minutes)",
                Description = "How long HTTP responses are cached by browsers and proxies",
                Category = Categories.Performance,
                ValueType = typeof(int),
                DefaultValue = 5,
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_RESPONSE_CACHE_DURATION_MINUTES,
                IsSecret = false,
                AllowRuntimeModification = true,
                Validation = new ValidationRules
                {
                    MinValue = 1,
                    MaxValue = 60
                }
            }
        },
        
        // Application Paths (Startup-Only, useful for containerized deployments)
        {
            ConfigurationKeys.SQUIRREL_APP_DATA_PATH,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_APP_DATA_PATH,
                DisplayName = "Application Data Path",
                Description = "The directory path where application data (database, search index, etc.) is stored. Useful for containerized deployments to mount volumes.",
                Category = Categories.Application,
                ValueType = typeof(string),
                DefaultValue = "App_Data",
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_APP_DATA_PATH,
                IsSecret = false,
                AllowRuntimeModification = false,
                IsVisibleInUI = false,
                Validation = new ValidationRules()
            }
        },
        {
            ConfigurationKeys.SQUIRREL_SEED_DATA_FILE_PATH,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_SEED_DATA_FILE_PATH,
                DisplayName = "Seed Data File Path",
                Description = "The path to a custom seed data YAML file. If not specified, uses the default embedded seed-data.yaml. Useful for containerized deployments with custom initial data.",
                Category = Categories.Application,
                ValueType = typeof(string),
                DefaultValue = null,
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_SEED_DATA_FILE_PATH,
                IsSecret = false,
                AllowRuntimeModification = false,
                IsVisibleInUI = false,
                Validation = new ValidationRules()
            }
        },
        
        // Database Configuration (Startup-Only)
        {
            ConfigurationKeys.SQUIRREL_DATABASE_PROVIDER,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_DATABASE_PROVIDER,
                DisplayName = "Database Provider",
                Description = "The database provider to use (PostgreSQL, MySQL, MariaDB, SQLServer, SQLite)",
                Category = Categories.Database,
                ValueType = typeof(string),
                DefaultValue = "SQLite",
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_DATABASE_PROVIDER,
                IsSecret = false,
                AllowRuntimeModification = false,
                IsVisibleInUI = false,
                Validation = new ValidationRules
                {
                    AllowedValues = new[] { "PostgreSQL", "MySQL", "MariaDB", "SQLServer", "SQLite" }
                }
            }
        },
        {
            ConfigurationKeys.SQUIRREL_DATABASE_CONNECTION_STRING,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_DATABASE_CONNECTION_STRING,
                DisplayName = "Database Connection String",
                Description = "The database connection string (startup-only configuration)",
                Category = Categories.Database,
                ValueType = typeof(string),
                DefaultValue = "Data Source=App_Data/squirrel.db",
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_DATABASE_CONNECTION_STRING,
                IsSecret = true,
                AllowRuntimeModification = false,
                IsVisibleInUI = false,
                Validation = new ValidationRules()
            }
        },
        {
            ConfigurationKeys.SQUIRREL_DATABASE_AUTO_MIGRATE,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_DATABASE_AUTO_MIGRATE,
                DisplayName = "Auto Migrate Database",
                Description = "Whether to automatically apply database migrations on startup",
                Category = Categories.Database,
                ValueType = typeof(bool),
                DefaultValue = true,
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_DATABASE_AUTO_MIGRATE,
                IsSecret = false,
                AllowRuntimeModification = false,
                IsVisibleInUI = false,
                Validation = new ValidationRules()
            }
        },
        {
            ConfigurationKeys.SQUIRREL_DATABASE_SEED_DATA,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_DATABASE_SEED_DATA,
                DisplayName = "Seed Database Data",
                Description = "Whether to seed initial data into the database on startup",
                Category = Categories.Database,
                ValueType = typeof(bool),
                DefaultValue = true,
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_DATABASE_SEED_DATA,
                IsSecret = false,
                AllowRuntimeModification = false,
                IsVisibleInUI = false,
                Validation = new ValidationRules()
            }
        },
        
        // Performance Configuration - Cache Provider
        {
            ConfigurationKeys.SQUIRREL_CACHE_PROVIDER,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_CACHE_PROVIDER,
                DisplayName = "Memory Cache Provider",
                Description = "The caching provider to use (Memory or Redis)",
                Category = Categories.Performance,
                ValueType = typeof(string),
                DefaultValue = "Memory",
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_CACHE_PROVIDER,
                IsSecret = false,
                AllowRuntimeModification = false,
                IsVisibleInUI = true,
                Validation = new ValidationRules
                {
                    AllowedValues = new[] { "Memory", "Redis" }
                }
            }
        },
        {
            ConfigurationKeys.SQUIRREL_REDIS_CONFIGURATION,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_REDIS_CONFIGURATION,
                DisplayName = "Redis Configuration",
                Description = "Redis connection string (e.g., localhost:6379)",
                Category = Categories.Performance,
                ValueType = typeof(string),
                DefaultValue = "localhost:6379",
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_REDIS_CONFIGURATION,
                IsSecret = false,
                AllowRuntimeModification = false,
                IsVisibleInUI = true,
                Validation = new ValidationRules()
            }
        },
        {
            ConfigurationKeys.SQUIRREL_REDIS_INSTANCE_NAME,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_REDIS_INSTANCE_NAME,
                DisplayName = "Redis Instance Name",
                Description = "Redis instance name prefix for cache keys",
                Category = Categories.Performance,
                ValueType = typeof(string),
                DefaultValue = "Squirrel_",
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_REDIS_INSTANCE_NAME,
                IsSecret = false,
                AllowRuntimeModification = false,
                IsVisibleInUI = true,
                Validation = new ValidationRules()
            }
        },
        
        // File Storage Configuration
        {
            ConfigurationKeys.SQUIRREL_FILE_STORAGE_PATH,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_FILE_STORAGE_PATH,
                DisplayName = "File Storage Path",
                Description = "Directory path where uploaded files are stored",
                Category = Categories.Files,
                ValueType = typeof(string),
                DefaultValue = "App_Data/Files",
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_FILE_STORAGE_PATH,
                IsSecret = false,
                AllowRuntimeModification = false,
                IsVisibleInUI = true,
                Validation = new ValidationRules()
            }
        },
        {
            ConfigurationKeys.SQUIRREL_FILE_MAX_SIZE_MB,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_FILE_MAX_SIZE_MB,
                DisplayName = "Maximum File Size (MB)",
                Description = "Maximum allowed file size for uploads in megabytes",
                Category = Categories.Files,
                ValueType = typeof(int),
                DefaultValue = 100,
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_FILE_MAX_SIZE_MB,
                IsSecret = false,
                AllowRuntimeModification = true,
                Validation = new ValidationRules
                {
                    MinValue = 1,
                    MaxValue = 2048 // 2GB
                }
            }
        },
        {
            ConfigurationKeys.SQUIRREL_FILE_ALLOWED_EXTENSIONS,
            new ConfigurationProperty
            {
                Key = ConfigurationKeys.SQUIRREL_FILE_ALLOWED_EXTENSIONS,
                DisplayName = "Allowed File Extensions",
                Description = "Comma-separated list of allowed file extensions (e.g., .pdf,.doc,.jpg)",
                Category = Categories.Files,
                ValueType = typeof(string),
                DefaultValue = ".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.md,.jpg,.jpeg,.png,.gif,.bmp,.svg,.zip,.rar,.7z",
                EnvironmentVariable = ConfigurationKeys.SQUIRREL_FILE_ALLOWED_EXTENSIONS,
                IsSecret = false,
                AllowRuntimeModification = true,
                Validation = new ValidationRules()
            }
        }
    };

    /// <summary>
    /// Gets metadata for a specific configuration key
    /// </summary>
    public static ConfigurationProperty GetMetadata(string key)
    {
        if (_metadata.TryGetValue(key, out var metadata))
        {
            return metadata;
        }

        throw new KeyNotFoundException($"Configuration key '{key}' not found in metadata registry");
    }

    /// <summary>
    /// Gets all configuration metadata
    /// </summary>
    public static IEnumerable<ConfigurationProperty> GetAllMetadata()
    {
        return _metadata.Values;
    }

    /// <summary>
    /// Checks if a configuration key exists
    /// </summary>
    public static bool HasMetadata(string key)
    {
        return _metadata.ContainsKey(key);
    }

    /// <summary>
    /// Gets all configuration keys
    /// </summary>
    public static IEnumerable<string> GetAllKeys()
    {
        return _metadata.Keys;
    }

    /// <summary>
    /// Gets metadata for all keys in a specific category
    /// </summary>
    public static IEnumerable<ConfigurationProperty> GetMetadataByCategory(string category)
    {
        return _metadata.Values.Where(m => m.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all configuration metadata that should be visible in the UI
    /// </summary>
    public static IEnumerable<ConfigurationProperty> GetUIVisibleMetadata()
    {
        return _metadata.Values.Where(m => m.IsVisibleInUI);
    }

    /// <summary>
    /// Gets metadata for all UI-visible keys in a specific category
    /// </summary>
    public static IEnumerable<ConfigurationProperty> GetUIVisibleMetadataByCategory(string category)
    {
        return _metadata.Values.Where(m => 
            m.IsVisibleInUI && 
            m.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    }
}
