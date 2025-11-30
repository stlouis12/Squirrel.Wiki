using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Events.Search;

/// <summary>
/// Event raised when a page needs to be indexed
/// </summary>
public class PageIndexRequestedEvent : DomainEvent
{
    public int PageId { get; }
    public string Title { get; }
    public string Slug { get; }

    public PageIndexRequestedEvent(int pageId, string title, string slug)
    {
        PageId = pageId;
        Title = title;
        Slug = slug;
    }
}

/// <summary>
/// Event raised when multiple pages need to be indexed
/// </summary>
public class PagesIndexRequestedEvent : DomainEvent
{
    public IEnumerable<int> PageIds { get; }

    public PagesIndexRequestedEvent(IEnumerable<int> pageIds)
    {
        PageIds = pageIds;
    }
}

/// <summary>
/// Event raised when a page needs to be removed from the index
/// </summary>
public class PageRemovedFromIndexEvent : DomainEvent
{
    public int PageId { get; }

    public PageRemovedFromIndexEvent(int pageId)
    {
        PageId = pageId;
    }
}

/// <summary>
/// Event raised when the entire search index needs to be rebuilt
/// </summary>
public class IndexRebuildRequestedEvent : DomainEvent
{
    public string? RequestedBy { get; }

    public IndexRebuildRequestedEvent(string? requestedBy = null)
    {
        RequestedBy = requestedBy;
    }
}

/// <summary>
/// Event raised when the search index needs to be optimized
/// </summary>
public class IndexOptimizationRequestedEvent : DomainEvent
{
    public string? RequestedBy { get; }

    public IndexOptimizationRequestedEvent(string? requestedBy = null)
    {
        RequestedBy = requestedBy;
    }
}

/// <summary>
/// Event raised when the search index needs to be cleared
/// </summary>
public class IndexClearRequestedEvent : DomainEvent
{
    public string? RequestedBy { get; }

    public IndexClearRequestedEvent(string? requestedBy = null)
    {
        RequestedBy = requestedBy;
    }
}

/// <summary>
/// Event raised when a file needs to be indexed
/// </summary>
public class FileIndexRequestedEvent : DomainEvent
{
    public Guid FileId { get; }

    public FileIndexRequestedEvent(Guid fileId)
    {
        FileId = fileId;
    }
}

/// <summary>
/// Event raised when multiple files need to be indexed
/// </summary>
public class FilesIndexRequestedEvent : DomainEvent
{
    public IEnumerable<Guid> FileIds { get; }

    public FilesIndexRequestedEvent(IEnumerable<Guid> fileIds)
    {
        FileIds = fileIds;
    }
}

/// <summary>
/// Event raised when a file needs to be removed from the index
/// </summary>
public class FileRemovedFromIndexEvent : DomainEvent
{
    public Guid FileId { get; }

    public FileRemovedFromIndexEvent(Guid fileId)
    {
        FileId = fileId;
    }
}
