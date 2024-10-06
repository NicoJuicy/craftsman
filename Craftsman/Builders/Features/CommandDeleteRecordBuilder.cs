namespace Craftsman.Builders.Features;

using Domain;
using Helpers;
using Services;

public class CommandDeleteRecordBuilder(ICraftsmanUtilities utilities)
{
    public void CreateCommand(string srcDirectory, Entity entity, string projectBaseName, bool isProtected, string permissionName, string dbContextName)
    {
        var classPath = ClassPathHelper.FeaturesClassPath(srcDirectory, $"{FileNames.DeleteEntityFeatureClassName(entity.Name)}.cs", entity.Plural, projectBaseName);
        var fileText = GetCommandFileText(classPath.ClassNamespace, entity, srcDirectory, projectBaseName, isProtected, permissionName, dbContextName);
        utilities.CreateFile(classPath, fileText);
    }

    public static string GetCommandFileText(string classNamespace, Entity entity, string srcDirectory, string projectBaseName, bool isProtected, string permissionName, string dbContextName)
    {
        var className = FileNames.DeleteEntityFeatureClassName(entity.Name);
        var deleteCommandName = FileNames.CommandDeleteName();

        var primaryKeyPropType = Entity.PrimaryKeyProperty.Type;
        var lowercasePrimaryKey = $"{entity.Name}Id";
        var primaryKeyPropNameLowercase = Entity.PrimaryKeyProperty.Name.LowercaseFirstLetter();
        var repoInterface = FileNames.EntityRepositoryInterface(entity.Name);
        var repoInterfaceProp = $"{entity.Name.LowercaseFirstLetter()}Repository";

        var entityServicesClassPath = ClassPathHelper.EntityServicesClassPath(srcDirectory, "", entity.Plural, projectBaseName);
        var servicesClassPath = ClassPathHelper.WebApiServicesClassPath(srcDirectory, "", projectBaseName);
        var exceptionsClassPath = ClassPathHelper.ExceptionsClassPath(srcDirectory, "", projectBaseName);
        var dbContextClassPath = ClassPathHelper.DbContextClassPath(srcDirectory, "", projectBaseName);
        
        FeatureBuilderHelpers.GetPermissionValuesForHandlers(srcDirectory, 
            projectBaseName, 
            isProtected, 
            permissionName, 
            out string heimGuardCtor, 
            out string permissionCheck, 
            out string permissionsUsing);

        return @$"namespace {classNamespace};

using {dbContextClassPath.ClassNamespace};
using {servicesClassPath.ClassNamespace};
using {exceptionsClassPath.ClassNamespace};{permissionsUsing}
using MediatR;

public static class {className}
{{
    public sealed record {deleteCommandName}({primaryKeyPropType} {lowercasePrimaryKey}) : IRequest;

    public sealed class Handler({dbContextName} dbContext{heimGuardCtor})
        : IRequestHandler<{deleteCommandName}>
    {{
        public async Task Handle({deleteCommandName} request, CancellationToken cancellationToken)
        {{{permissionCheck}
            var recordToDelete = await dbContext.{entity.Plural}.GetById(request.{lowercasePrimaryKey}, cancellationToken: cancellationToken);
            dbContext.Remove(recordToDelete);
            await dbContext.SaveChangesAsync(cancellationToken);
        }}
    }}
}}";
    }
}
