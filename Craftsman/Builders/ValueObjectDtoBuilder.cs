namespace Craftsman.Builders;

using Domain;
using Domain.Enums;
using Helpers;
using MediatR;
using Services;

public static class ValueObjectDtoBuilder
{
    public class ValueObjectDtoBuilderCommand : IRequest<bool>
    {
    }

    public class Handler(
        ICraftsmanUtilities utilities,
        IScaffoldingDirectoryStore scaffoldingDirectoryStore)
        : IRequestHandler<ValueObjectDtoBuilderCommand, bool>
    {
        public Task<bool> Handle(ValueObjectDtoBuilderCommand request, CancellationToken cancellationToken)
        {
            var addressReadDtoClassPath = ClassPathHelper.WebApiValueObjectDtosClassPath(scaffoldingDirectoryStore.SrcDirectory, 
                ValueObjectEnum.Address,
                Dto.Read,
                scaffoldingDirectoryStore.ProjectBaseName);
            var readDtoText = GetAddressDtoText(addressReadDtoClassPath.ClassNamespace);
            utilities.CreateFile(addressReadDtoClassPath, readDtoText);
            
            var addressCreateDtoClassPath = ClassPathHelper.WebApiValueObjectDtosClassPath(scaffoldingDirectoryStore.SrcDirectory, 
                ValueObjectEnum.Address,
                Dto.Creation,
                scaffoldingDirectoryStore.ProjectBaseName);
            var createDtoText = GetAddressCreateDtoText(addressCreateDtoClassPath.ClassNamespace);
            utilities.CreateFile(addressCreateDtoClassPath, createDtoText);
            
            var addressUpdateDtoClassPath = ClassPathHelper.WebApiValueObjectDtosClassPath(scaffoldingDirectoryStore.SrcDirectory, 
                ValueObjectEnum.Address,
                Dto.Update,
                scaffoldingDirectoryStore.ProjectBaseName);
            var updateDtoText = GetAddressUpdateDtoText(addressUpdateDtoClassPath.ClassNamespace);
            utilities.CreateFile(addressUpdateDtoClassPath, updateDtoText);

            return Task.FromResult(true);
        }
        
        private string GetAddressDtoText(string classNamespace)
        {
            var dtoName = FileNames.GetDtoName(ValueObjectEnum.Address.Name, Dto.Read);
            return $@"namespace {classNamespace};
            
public class {dtoName}
{{
    public string Line1 {{ get; set; }}
    public string Line2 {{ get; set; }}
    public string City {{ get; set; }}
    public string State {{ get; set; }}
    public string PostalCode {{ get; set; }}
    public string Country {{ get; set; }}
}}";
        }
        
        private string GetAddressCreateDtoText(string classNamespace)
        {
            var dtoName = FileNames.GetDtoName(ValueObjectEnum.Address.Name, Dto.Creation);
            return $@"namespace {classNamespace};
            
public class {dtoName}
{{
    public string Line1 {{ get; set; }}
    public string Line2 {{ get; set; }}
    public string City {{ get; set; }}
    public string State {{ get; set; }}
    public string PostalCode {{ get; set; }}
    public string Country {{ get; set; }}
}}";
        }
        
        private string GetAddressUpdateDtoText(string classNamespace)
        {
            var dtoName = FileNames.GetDtoName(ValueObjectEnum.Address.Name, Dto.Update);
            return $@"namespace {classNamespace};
            
public class {dtoName}
{{
    public string Line1 {{ get; set; }}
    public string Line2 {{ get; set; }}
    public string City {{ get; set; }}
    public string State {{ get; set; }}
    public string PostalCode {{ get; set; }}
    public string Country {{ get; set; }}
}}";
        }

    }
}
