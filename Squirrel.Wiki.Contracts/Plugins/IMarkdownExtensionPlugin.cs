using Markdig;

namespace Squirrel.Wiki.Contracts.Plugins;

/// <summary>
/// Interface for plugins that extend Markdown rendering capabilities
/// </summary>
public interface IMarkdownExtensionPlugin : IPlugin
{
    /// <summary>
    /// Configure the Markdig pipeline with custom extensions
    /// </summary>
    /// <param name="pipelineBuilder">The Markdig pipeline builder to configure</param>
    void ConfigurePipeline(MarkdownPipelineBuilder pipelineBuilder);
    
    /// <summary>
    /// Post-process rendered HTML (optional)
    /// </summary>
    /// <param name="html">The rendered HTML</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The processed HTML</returns>
    Task<string> PostProcessHtmlAsync(string html, CancellationToken cancellationToken = default)
    {
        // Default implementation: no post-processing
        return Task.FromResult(html);
    }
    
    /// <summary>
    /// Pre-process markdown before rendering (optional)
    /// </summary>
    /// <param name="markdown">The markdown content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The processed markdown</returns>
    Task<string> PreProcessMarkdownAsync(string markdown, CancellationToken cancellationToken = default)
    {
        // Default implementation: no pre-processing
        return Task.FromResult(markdown);
    }
}
