using AutoMapper;

namespace Assignment.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Map Person to PersonVM
        CreateMap<Person, PersonVM>();


        // Map Staff to StaffVM
        CreateMap<Staff, StaffVM>();

        // Map Staff and Person together into StaffPersonVM
        CreateMap<Staff, StaffPersonVM>()
            .ForMember(dest => dest.Staff, opt => opt.MapFrom(src => src)) // Maps the Staff part
            .ForMember(dest => dest.Person, opt => opt.MapFrom(src => src.Person)); // Maps the Person part


        // Map Member to MemberVM
        CreateMap<Member, MemberVM>();

        // Map Member and Person together into MemberPersonVM
        CreateMap<Member, MemberPersonVM>()
            .ForMember(dest => dest.Member, opt => opt.MapFrom(src => src)) // Maps the Member part
            .ForMember(dest => dest.Person, opt => opt.MapFrom(src => src.Person)); // Maps the Person part
    }
}