using AutoMapper;
using Squirrel.Wiki.Core.Database.Entities;
using Squirrel.Wiki.Core.Models;
using FileEntity = Squirrel.Wiki.Core.Database.Entities.File;

namespace Squirrel.Wiki.Core.Mapping;

/// <summary>
/// AutoMapper profile for file and folder entity mappings
/// </summary>
public class FileMappingProfile : Profile
{
    public FileMappingProfile()
    {
        // File mappings
        CreateMap<FileEntity, FileDto>()
            .ForMember(dest => dest.FolderName, 
                opt => opt.MapFrom(src => src.Folder != null ? src.Folder.Name : null))
            .ForMember(dest => dest.DownloadUrl, 
                opt => opt.Ignore()) // Set by service layer
            .ForMember(dest => dest.ThumbnailUrl, 
                opt => opt.Ignore()) // Set by service layer
            .ForMember(dest => dest.CurrentVersion,
                opt => opt.MapFrom(src => src.CurrentVersion));
            
        // Folder mappings
        CreateMap<Folder, FolderDto>()
            .ForMember(dest => dest.ParentFolderName, 
                opt => opt.MapFrom(src => src.ParentFolder != null ? src.ParentFolder.Name : null))
            .ForMember(dest => dest.FileCount, 
                opt => opt.Ignore()) // Set by service layer
            .ForMember(dest => dest.SubFolderCount, 
                opt => opt.Ignore()); // Set by service layer
            
        // Folder tree mappings
        CreateMap<Folder, FolderTreeDto>()
            .ForMember(dest => dest.Children, 
                opt => opt.Ignore()) // Built recursively by service layer
            .ForMember(dest => dest.FileCount, 
                opt => opt.Ignore()); // Set by service layer
    }
}
