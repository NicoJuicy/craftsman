namespace Craftsman.Builders.Auth;

using Helpers;
using Services;

public class UserPolicyHandlerBuilder(ICraftsmanUtilities utilities)
{
    public void CreatePolicyBuilder(string srcDirectory, string projectBaseName, string dbContextName)
    {
        var classPath = ClassPathHelper.WebApiServicesClassPath(srcDirectory, "UserPolicyHandler.cs", projectBaseName);
        var fileText = GetPolicyBuilderText(classPath.ClassNamespace, srcDirectory, projectBaseName, dbContextName);
        utilities.CreateFile(classPath, fileText);
    }

    private static string GetPolicyBuilderText(string classNamespace, string srcDirectory, string projectBaseName,
        string dbContextName)
    {
        var domainPolicyClassPath = ClassPathHelper.PolicyDomainClassPath(srcDirectory, "", projectBaseName);
        var exceptionsClassPath = ClassPathHelper.ExceptionsClassPath(srcDirectory, "", projectBaseName);
        var dbContextClassPath = ClassPathHelper.DbContextClassPath(srcDirectory, "", projectBaseName);

        return @$"namespace {classNamespace};

using Domain.Roles;
using Domain.Users.Dtos;
using Domain.Users.Features;
using {domainPolicyClassPath.ClassNamespace};
using {exceptionsClassPath.ClassNamespace};
using {dbContextClassPath.ClassNamespace};
using HeimGuard;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed class UserPolicyHandler(ICurrentUserService currentUserService, 
    {dbContextName} dbContext, IMediator mediator) : IUserPolicyHandler
{{    
    public async Task<IEnumerable<string>> GetUserPermissions()
    {{
        var roles = await GetRoles();

        // super admins can do everything
        if(roles.Contains(Role.SuperAdmin().Value))
            return Permissions.List();

        var permissions = await dbContext.RolePermissions
            .Where(x => roles.Contains(x.Role))
            .Select(x => x.Permission)
            .Distinct()
            .ToArrayAsync();

        return await Task.FromResult(permissions);
    }}
    
    public async Task<bool> HasPermission(string permission)
    {{
        var roles = await GetRoles();
    
        // super admins can do everything
        if (roles.Contains(Role.SuperAdmin().Value))
            return true;
        
        return await dbContext.RolePermissions
            .Where(rp => roles.Contains(rp.Role))
            .Select(rp => rp.Permission)
            .AnyAsync(x => x == permission);
    }}

    private async Task<string[]> GetRoles()
    {{
        var claimsPrincipal = currentUserService.User;
        if (claimsPrincipal == null) throw new ArgumentNullException(nameof(claimsPrincipal));
        
        var nameIdentifier = currentUserService.UserId;
        var usersExist = dbContext.Users.Any();
        
        if (!usersExist)
            await SeedRootUser(nameIdentifier);

        var roles = !string.IsNullOrEmpty(nameIdentifier) 
            ? dbContext.UserRoles
                .Include(x => x.User)
                .Where(x => x.User.Identifier == nameIdentifier)
                .Select(x => x.Role.Value)
                .ToArray()
            : Array.Empty<string>();

        if (roles.Length == 0)
            throw new NoRolesAssignedException();

        return roles;
    }}

    private async Task SeedRootUser(string userId)
    {{
        var rootUser = new UserForCreationDto()
        {{
            Username = currentUserService.Username,
            Email = currentUserService.Email,
            FirstName = currentUserService.FirstName,
            LastName = currentUserService.LastName,
            Identifier = userId
        }};

        var userCommand = new AddUser.Command(rootUser, true);
        var createdUser = await mediator.Send(userCommand);

        var roleCommand = new AddUserRole.Command(createdUser.Id, Role.SuperAdmin().Value, true);
        await mediator.Send(roleCommand);
        
    }}
}}";
    }
}
