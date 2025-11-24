using AutoMapper;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Mapping;

/// <summary>
/// AutoMapper profile for Tag entity mappings
/// </summary>
public class TagMappingProfile : Profile
{
    public TagMappingProfile()
    {
        // Tag -> TagDto
        CreateMap<Tag, TagDto>();

        // Tag -> TagWithCountDto (PageCount set by service)
        CreateMap<Tag, TagWithCountDto>()
            .ForMember(dest => dest.PageCount, opt => opt.Ignore()); // Set by service
    }
}
