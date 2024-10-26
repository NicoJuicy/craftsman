namespace Craftsman.Builders.AuthServer;

using Craftsman.Helpers;
using Craftsman.Services;

public class ClientExtensionsBuilder
{
    private readonly ICraftsmanUtilities _utilities;

    public ClientExtensionsBuilder(ICraftsmanUtilities utilities)
    {
        _utilities = utilities;
    }

    public void Create(string solutionDirectory, string projectBaseName)
    {
        var classPath = ClassPathHelper.AuthServerExtensionsClassPath(solutionDirectory, "ClientExtensions.cs", projectBaseName);
        var fileText = GetFileText(classPath.ClassNamespace);
        _utilities.CreateFile(classPath, fileText);
    }

    private static string GetFileText(string classNamespace)
    {
        // language=csharp
        return $$"""
                 namespace {{classNamespace}};

                 using Pulumi;
                 using Pulumi.Keycloak.OpenId;

                 public static class ClientExtensions
                 {
                     public static void ExtendDefaultScopes(this Client client, params string[] scopeNames)
                     {
                         var scopeList = new InputList<string>()
                         {
                             "openid",
                             "profile",
                             "email",
                             "roles",
                             "web-origins"
                         };
                         foreach (var scopeName in scopeNames)
                         {
                             scopeList.Add(scopeName);
                         }
                         
                         var clientDefaultScopes = new ClientDefaultScopes($"client_default_scopes_{client.GetResourceName()}", new ClientDefaultScopesArgs
                         {
                             RealmId = client.RealmId,
                             ClientId = client.Id,
                             DefaultScopes = scopeList,
                         });
                     }
                     
                     public static void AddAudienceMapper(this Client client, string audience)
                     {
                         var audienceMapper = new AudienceProtocolMapper($"audience_mapper_{client.GetResourceName()}", new AudienceProtocolMapperArgs
                         {
                             RealmId = client.RealmId,
                             ClientId = client.Id,
                             IncludedCustomAudience = audience,
                             Name = $"{audience}-Mapping"
                         });
                     }
                     
                     // example tenant mapper
                     // public static void AddTenantMapper(this Client client)
                     // {
                     //     var userAttributeMapper = new UserAttributeProtocolMapper($"tenant_mapper_{client.GetResourceName()}", new()
                     //     {
                     //         RealmId = client.RealmId,
                     //         ClientId = client.Id,
                     //         Name = "tenant-mapper",
                     //         UserAttribute = "organization-id",
                     //         ClaimName = "organization_id"
                     //     });
                     // }
                 }
                 """;
    }
}
