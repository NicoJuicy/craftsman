namespace Craftsman.Builders.Features;

using Domain;
using Domain.Enums;
using Helpers;
using Services;

public class CommandAddUserRoleBuilder(ICraftsmanUtilities utilities)
{
    public void CreateCommand(string srcDirectory, Entity entity, string projectBaseName, string dbContextName)
    {
        var classPath = ClassPathHelper.FeaturesClassPath(srcDirectory, $"{FileNames.AddUserRoleFeatureClassName()}.cs", entity.Plural, projectBaseName);
        var fileText = GetCommandFileText(classPath.ClassNamespace, entity, srcDirectory, projectBaseName, dbContextName);
        utilities.CreateFile(classPath, fileText);
    }

    public static string GetCommandFileText(string classNamespace, Entity entity, string srcDirectory, string projectBaseName, string dbContextName)
    {
        var entityClassPath = ClassPathHelper.EntityClassPath(srcDirectory, "", entity.Plural, projectBaseName);
        var dtoClassPath = ClassPathHelper.DtoClassPath(srcDirectory, "", entity.Plural, projectBaseName);
        var servicesClassPath = ClassPathHelper.WebApiServicesClassPath(srcDirectory, "", projectBaseName);
        var exceptionsClassPath = ClassPathHelper.ExceptionsClassPath(srcDirectory, "", projectBaseName);
        var dbContextClassPath = ClassPathHelper.DbContextClassPath(srcDirectory, "", projectBaseName);

        return @$"namespace {classNamespace};

using {dbContextClassPath.ClassNamespace};
using {entityClassPath.ClassNamespace};
using {dtoClassPath.ClassNamespace};
using {servicesClassPath.ClassNamespace};
using {exceptionsClassPath.ClassNamespace};
using HeimGuard;
using Mappings;
using MediatR;
using Roles;

public static class {FileNames.AddUserRoleFeatureClassName()}
{{
    public sealed record Command(Guid UserId, string Role, bool SkipPermissions = false) : IRequest;

    public sealed class Handler({dbContextName} dbContext, IHeimGuardClient heimGuard) : IRequestHandler<Command>
    {{
        public async Task Handle(Command request, CancellationToken cancellationToken)
        {{
            if(!request.SkipPermissions)
                await heimGuard.MustHavePermission<ForbiddenAccessException>(Permissions.CanAddUserRoles);
            
            var user = await dbContext.GetUserAggregate().GetById(request.UserId, cancellationToken);

            var roleToAdd = user.AddRole(new Role(request.Role));
            await dbContext.UserRoles.AddAsync(roleToAdd, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
        }}
    }}
}}";
    }
}
