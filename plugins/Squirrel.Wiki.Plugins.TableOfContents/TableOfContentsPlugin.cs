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
        var maxDepth = GetConfigValue("MaxDepth", 3);
        _logger?.LogDebug("TOC Configuration - MaxDepth: {MaxDepth}", maxDepth);

        var headings = ExtractHeadings(html);
        
        if (headings.Count == 0)
        {
            _logger?.LogWarning("No headings found in HTML for TOC generation");
            return "<div class=\"toc\"><p><em>No headings found</em></p></div>";
        }

        return BuildTocHtml(headings, maxDepth);
    }

    private List<TocHeading> ExtractHeadings(string html)
    {
        var headingPattern = new Regex(@"<h([1-6])[^>]*id=""([^""]+)""[^>]*>(.*?)</h\1>", RegexOptions.IgnoreCase);
        var matches = headingPattern.Matches(html);
        
        _logger?.LogInformation("Found {Count} heading matches in HTML", matches.Count);

        var headings = new List<TocHeading>();
        foreach (Match match in matches)
        {
            headings.Add(new TocHeading
            {
                Level = int.Parse(match.Groups[1].Value),
                Id = match.Groups[2].Value,
                Text = StripHtmlTags(match.Groups[3].Value)
            });
        }

        return headings;
    }

    private static string BuildTocHtml(List<TocHeading> headings, int maxDepth)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<nav class=\"toc\" role=\"navigation\">");
        sb.AppendLine("  <h2 class=\"toc-title\">Table of Contents</h2>");
        sb.AppendLine("  <ul class=\"toc-list\">");

        var context = new TocBuildContext();

        foreach (var heading in headings)
        {
            if (heading.Level > maxDepth)
                continue;

            ProcessHeading(sb, heading, context);
        }

        CloseRemainingLists(sb, context);

        sb.AppendLine("  </ul>");
        sb.AppendLine("</nav>");

        return sb.ToString();
    }

    private static void ProcessHeading(StringBuilder sb, TocHeading heading, TocBuildContext context)
    {
        if (heading.Level > context.CurrentLevel)
        {
            OpenNestedLists(sb, heading.Level, context);
        }
        else if (heading.Level < context.CurrentLevel)
        {
            CloseNestedLists(sb, heading.Level, context);
        }
        else if (context.CurrentLevel > 0)
        {
            ClosePreviousItem(sb, context);
        }

        AddTocItem(sb, heading, context);
    }

    private static void OpenNestedLists(StringBuilder sb, int targetLevel, TocBuildContext context)
    {
        while (context.CurrentLevel < targetLevel)
        {
            sb.AppendLine($"{GetIndent(context.CurrentLevel)}    <ul class=\"toc-list-nested\">");
            context.LevelStack.Push(context.CurrentLevel);
            context.CurrentLevel++;
        }
    }

    private static void CloseNestedLists(StringBuilder sb, int targetLevel, TocBuildContext context)
    {
        while (context.CurrentLevel > targetLevel && context.LevelStack.Count > 0)
        {
            context.CurrentLevel = context.LevelStack.Pop();
            sb.AppendLine($"{GetIndent(context.CurrentLevel)}    </ul>");
            sb.AppendLine($"{GetIndent(context.CurrentLevel)}  </li>");
        }
    }

    private static void ClosePreviousItem(StringBuilder sb, TocBuildContext context)
    {
        sb.AppendLine($"{GetIndent(context.CurrentLevel - 1)}  </li>");
    }

    private static void AddTocItem(StringBuilder sb, TocHeading heading, TocBuildContext context)
    {
        var indent = GetIndent(context.CurrentLevel - 1);
        sb.AppendLine($"{indent}  <li class=\"toc-item toc-level-{heading.Level}\">");
        sb.AppendLine($"{indent}    <a href=\"#{heading.Id}\" class=\"toc-link\">{heading.Text}</a>");
    }

    private static void CloseRemainingLists(StringBuilder sb, TocBuildContext context)
    {
        while (context.LevelStack.Count > 0)
        {
            context.CurrentLevel = context.LevelStack.Pop();
            sb.AppendLine($"{GetIndent(context.CurrentLevel)}    </ul>");
            sb.AppendLine($"{GetIndent(context.CurrentLevel)}  </li>");
        }

        if (context.CurrentLevel > 0)
        {
            sb.AppendLine("  </li>");
        }
    }

    private static string GetIndent(int level)
    {
        return new string(' ', level * 2);
    }

    private class TocHeading
    {
        public int Level { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    private class TocBuildContext
    {
        public int CurrentLevel { get; set; }
        public Stack<int> LevelStack { get; } = new Stack<int>();
    }

    private static string StripHtmlTags(string html)
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
