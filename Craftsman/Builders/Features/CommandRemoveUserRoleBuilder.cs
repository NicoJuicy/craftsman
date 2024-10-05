namespace Craftsman.Builders.Features;

using Domain;
using Domain.Enums;
using Helpers;
using Services;

public class CommandRemoveUserRoleBuilder(ICraftsmanUtilities utilities)
{
    public void CreateCommand(string srcDirectory, Entity entity, string projectBaseName, string dbContextName)
    {
        var classPath = ClassPathHelper.FeaturesClassPath(srcDirectory, $"{FileNames.RemoveUserRoleFeatureClassName()}.cs", entity.Plural, projectBaseName);
        var fileText = GetCommandFileText(classPath.ClassNamespace, entity, srcDirectory, projectBaseName, dbContextName);
        utilities.CreateFile(classPath, fileText);
    }

    public static string GetCommandFileText(string classNamespace, Entity entity, string srcDirectory, string projectBaseName, string dbContextName)
    {
        var exceptionsClassPath = ClassPathHelper.ExceptionsClassPath(srcDirectory, "", projectBaseName);
        var dbContextClassPath = ClassPathHelper.DbContextClassPath(srcDirectory, "", projectBaseName);

        return @$"namespace {classNamespace};

using {dbContextClassPath.ClassNamespace};
using {exceptionsClassPath.ClassNamespace};
using HeimGuard;
using MediatR;
using Roles;

public static class {FileNames.RemoveUserRoleFeatureClassName()}
{{
    public sealed record Command(Guid UserId, string Role) : IRequest;

    public sealed class Handler({dbContextName} dbContext,
        IHeimGuardClient heimGuard) : IRequestHandler<Command>
    {{
        public async Task Handle(Command request, CancellationToken cancellationToken)
        {{
            await heimGuard.MustHavePermission<ForbiddenAccessException>(Permissions.CanRemoveUserRoles);
            var user = await dbContext.GetUserAggregate().GetById(request.UserId, cancellationToken);

            var roleToRemove = user.RemoveRole(new Role(request.Role));
            dbContext.UserRoles.Remove(roleToRemove);
            dbContext.Update(user);

            await dbContext.SaveChangesAsync(cancellationToken);
        }}
    }}
}}";
    }
}
