using Markdig;
using Markdig.Syntax;
using Microsoft.Extensions.Logging;
using Squirrel.Wiki.Contracts.Plugins;
using Squirrel.Wiki.Plugins;
using System.Text;
using System.Text.RegularExpressions;

namespace Squirrel.Wiki.Plugins.Markdown.TableOfContents;

/// <summary>
/// Plugin that generates a table of contents from markdown headings
/// </summary>
public class TableOfContentsPlugin : PluginBase, IMarkdownExtensionPlugin
{
    private static readonly Regex TocPlaceholderRegex = new(@"\{\{toc\}\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private ILogger? _logger;

    public override PluginMetadata Metadata { get; } = new PluginMetadata
    {
        Id = "squirrel.wiki.plugins.markdown.tableofcontents",
        Name = "Table of Contents",
        Description = "Generates a table of contents from markdown headings using {{toc}} placeholder",
        Author = "Squirrel Wiki",
        Version = "1.0.0",
        Type = PluginType.MarkdownExtension,
        RequiresConfiguration = false,
        Configuration = new[]
        {
            new PluginConfigurationItem
            {
                Key = "MaxDepth",
                DisplayName = "Maximum Heading Depth",
                Description = "Maximum heading level to include in TOC (1-6, default: 3)",
                Type = PluginConfigType.Number,
                DefaultValue = "3",
                IsRequired = false,
                IsSecret = false,
                ValidationPattern = "^[1-6]$",
                ValidationErrorMessage = "Maximum depth must be a number between 1 and 6"
            }
        }
    };

    public override IEnumerable<PluginConfigurationItem> GetConfigurationSchema()
    {
        return Metadata.Configuration ?? Enumerable.Empty<PluginConfigurationItem>();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(serviceProvider, cancellationToken);
        
        // Get logger from service provider
        _logger = serviceProvider.GetService(typeof(ILogger<TableOfContentsPlugin>)) as ILogger<TableOfContentsPlugin>;
        _logger?.LogInformation("TableOfContentsPlugin initialized");
    }

    public void ConfigurePipeline(MarkdownPipelineBuilder pipelineBuilder)
    {
        _logger?.LogInformation("ConfigurePipeline called for TableOfContentsPlugin");
        // No pipeline modifications needed - we handle TOC in post-processing
    }

    public Task<string> PreProcessMarkdownAsync(string markdown, CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("PreProcessMarkdownAsync called. Markdown length: {Length}, Contains {{{{toc}}}}: {HasToc}", 
            markdown?.Length ?? 0, 
            markdown?.Contains("{{toc}}", StringComparison.OrdinalIgnoreCase) ?? false);
        
        // No pre-processing needed
        return Task.FromResult(markdown);
    }

    public Task<string> PostProcessHtmlAsync(string html, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("PostProcessHtmlAsync called. HTML length: {Length}", html?.Length ?? 0);
        
        // Check if there's a {{toc}} placeholder
        var hasTocPlaceholder = TocPlaceholderRegex.IsMatch(html);
        _logger?.LogInformation("HTML contains {{{{toc}}}} placeholder: {HasToc}", hasTocPlaceholder);
        
        if (!hasTocPlaceholder)
        {
            _logger?.LogDebug("No {{{{toc}}}} placeholder found, returning HTML unchanged");
            return Task.FromResult(html);
        }

        _logger?.LogInformation("Generating table of contents...");
        
        // Extract headings from HTML
        var toc = GenerateTableOfContents(html);
        
        _logger?.LogInformation("Generated TOC length: {Length}", toc?.Length ?? 0);
        _logger?.LogDebug("Generated TOC content: {Toc}", toc);

        // Replace {{toc}} with the generated table of contents
        html = TocPlaceholderRegex.Replace(html, toc);
        
        _logger?.LogInformation("Replaced {{{{toc}}}} placeholder with generated TOC");

        return Task.FromResult(html);
    }

    private string GenerateTableOfContents(string html)
    {
        // Get configuration
        var maxDepth = GetConfigValue("MaxDepth", 3);
        
        _logger?.LogDebug("TOC Configuration - MaxDepth: {MaxDepth}", maxDepth);

        // Extract headings using regex
        var headingPattern = new Regex(@"<h([1-6])[^>]*id=""([^""]+)""[^>]*>(.*?)</h\1>", RegexOptions.IgnoreCase);
        var matches = headingPattern.Matches(html);
        
        _logger?.LogInformation("Found {Count} heading matches in HTML", matches.Count);

        if (matches.Count == 0)
        {
            _logger?.LogWarning("No headings found in HTML for TOC generation");
            return "<div class=\"toc\"><p><em>No headings found</em></p></div>";
        }

        var sb = new StringBuilder();
        sb.AppendLine("<nav class=\"toc\" role=\"navigation\">");
        sb.AppendLine("  <h2 class=\"toc-title\">Table of Contents</h2>");
        sb.AppendLine("  <ul class=\"toc-list\">");

        int currentLevel = 0;
        var levelStack = new Stack<int>();

        foreach (Match match in matches)
        {
            var level = int.Parse(match.Groups[1].Value);
            var id = match.Groups[2].Value;
            var text = StripHtmlTags(match.Groups[3].Value);

            // Skip if beyond max depth
            if (level > maxDepth)
                continue;

            // Handle nesting
            if (level > currentLevel)
            {
                // Open new nested lists
                while (currentLevel < level)
                {
                    sb.AppendLine($"{new string(' ', currentLevel * 2)}    <ul class=\"toc-list-nested\">");
                    levelStack.Push(currentLevel);
                    currentLevel++;
                }
            }
            else if (level < currentLevel)
            {
                // Close nested lists
                while (currentLevel > level && levelStack.Count > 0)
                {
                    currentLevel = levelStack.Pop();
                    sb.AppendLine($"{new string(' ', currentLevel * 2)}    </ul>");
                    sb.AppendLine($"{new string(' ', currentLevel * 2)}  </li>");
                }
            }
            else if (currentLevel > 0)
            {
                // Close previous item at same level
                sb.AppendLine($"{new string(' ', (currentLevel - 1) * 2)}  </li>");
            }

            // Add the TOC item
            var indent = new string(' ', (currentLevel - 1) * 2);
            sb.AppendLine($"{indent}  <li class=\"toc-item toc-level-{level}\">");
            sb.AppendLine($"{indent}    <a href=\"#{id}\" class=\"toc-link\">{text}</a>");
        }

        // Close any remaining open lists
        while (levelStack.Count > 0)
        {
            currentLevel = levelStack.Pop();
            sb.AppendLine($"{new string(' ', currentLevel * 2)}    </ul>");
            sb.AppendLine($"{new string(' ', currentLevel * 2)}  </li>");
        }

        if (currentLevel > 0)
        {
            sb.AppendLine("  </li>");
        }

        sb.AppendLine("  </ul>");
        sb.AppendLine("</nav>");

        return sb.ToString();
    }

    private string StripHtmlTags(string html)
    {
        // Remove HTML tags but preserve the text content
        var text = Regex.Replace(html, "<.*?>", string.Empty);
        // Decode HTML entities
        text = System.Net.WebUtility.HtmlDecode(text);
        return text.Trim();
    }

    private int GetConfigValue(string key, int defaultValue)
    {
        if (PluginConfiguration != null && 
            PluginConfiguration.TryGetValue(key, out var value) && 
            int.TryParse(value, out var intValue))
        {
            return intValue;
        }
        return defaultValue;
    }

    public override Task<bool> ValidateConfigurationAsync(
        Dictionary<string, string> config,
        CancellationToken cancellationToken = default)
    {
        // Validate MaxDepth
        if (config.TryGetValue("MaxDepth", out var maxDepthStr))
        {
            if (!int.TryParse(maxDepthStr, out var maxDepth) || maxDepth < 1 || maxDepth > 6)
            {
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }
}
