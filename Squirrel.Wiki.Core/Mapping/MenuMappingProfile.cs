using AutoMapper;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Mapping;

/// <summary>
/// AutoMapper profile for Menu entity mappings
/// </summary>
public class MenuMappingProfile : Profile
{
    public MenuMappingProfile()
    {
        // Menu -> MenuDto
        CreateMap<Menu, MenuDto>()
            .ForMember(dest => dest.MenuMarkup, opt => opt.MapFrom(src => src.Markup))
            .ForMember(dest => dest.MenuType, opt => opt.MapFrom(src => (int)src.MenuType));

        // MenuCreateDto -> Menu
        CreateMap<MenuCreateDto, Menu>()
            .ForMember(dest => dest.Markup, opt => opt.MapFrom(src => src.MenuMarkup))
            .ForMember(dest => dest.MenuType, opt => opt.MapFrom(src => (MenuType)src.MenuType))
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.ModifiedOn, opt => opt.MapFrom(_ => DateTime.UtcNow));

        // MenuUpdateDto -> Menu (for updating existing entity)
        CreateMap<MenuUpdateDto, Menu>()
            .ForMember(dest => dest.Markup, opt => opt.MapFrom(src => src.MenuMarkup))
            .ForMember(dest => dest.MenuType, opt => opt.MapFrom(src => (MenuType)src.MenuType))
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.ModifiedOn, opt => opt.MapFrom(_ => DateTime.UtcNow));
    }
}
