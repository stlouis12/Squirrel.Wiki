using AutoMapper;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Models;

namespace Squirrel.Wiki.Core.Mapping;

/// <summary>
/// AutoMapper profile for User entity mappings
/// </summary>
public class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        // User -> UserDto
        CreateMap<User, UserDto>()
            .ForMember(dest => dest.ExternalId, opt => opt.MapFrom(src => src.ExternalId ?? string.Empty))
            .ForMember(dest => dest.Roles, opt => opt.MapFrom(src => GetRoles(src)));

        // UserCreateDto -> User
        CreateMap<UserCreateDto, User>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedOn, opt => opt.MapFrom(_ => DateTime.UtcNow))
            .ForMember(dest => dest.LastLoginOn, opt => opt.MapFrom(_ => DateTime.UtcNow))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
            .ForMember(dest => dest.IsLocked, opt => opt.MapFrom(_ => false))
            .ForMember(dest => dest.FailedLoginAttempts, opt => opt.MapFrom(_ => 0))
            .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
            .ForMember(dest => dest.Provider, opt => opt.Ignore())
            .ForMember(dest => dest.ExternalId, opt => opt.Ignore())
            .ForMember(dest => dest.FirstName, opt => opt.Ignore())
            .ForMember(dest => dest.LastName, opt => opt.Ignore())
            .ForMember(dest => dest.LockedUntil, opt => opt.Ignore())
            .ForMember(dest => dest.LastPasswordChangeOn, opt => opt.Ignore())
            .ForMember(dest => dest.PasswordResetToken, opt => opt.Ignore())
            .ForMember(dest => dest.PasswordResetExpiry, opt => opt.Ignore());

        // UserUpdateDto -> User (for updating existing entity)
        CreateMap<UserUpdateDto, User>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Username, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedOn, opt => opt.Ignore())
            .ForMember(dest => dest.LastLoginOn, opt => opt.Ignore())
            .ForMember(dest => dest.IsActive, opt => opt.Ignore())
            .ForMember(dest => dest.IsLocked, opt => opt.Ignore())
            .ForMember(dest => dest.FailedLoginAttempts, opt => opt.Ignore())
            .ForMember(dest => dest.PasswordHash, opt => opt.Ignore())
            .ForMember(dest => dest.Provider, opt => opt.Ignore())
            .ForMember(dest => dest.ExternalId, opt => opt.Ignore())
            .ForMember(dest => dest.LockedUntil, opt => opt.Ignore())
            .ForMember(dest => dest.LastPasswordChangeOn, opt => opt.Ignore())
            .ForMember(dest => dest.PasswordResetToken, opt => opt.Ignore())
            .ForMember(dest => dest.PasswordResetExpiry, opt => opt.Ignore());
    }

    private static List<string> GetRoles(User user)
    {
        var roles = new List<string>();
        if (user.IsAdmin) roles.Add("Admin");
        if (user.IsEditor) roles.Add("Editor");
        return roles;
    }
}
