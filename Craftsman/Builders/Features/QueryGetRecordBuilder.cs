﻿namespace Craftsman.Builders.Features;

using Domain;
using Domain.Enums;
using Helpers;
using Services;

public class QueryGetRecordBuilder(ICraftsmanUtilities utilities)
{
    public void CreateQuery(string srcDirectory, Entity entity, string projectBaseName, bool isProtected, string permissionName, string dbContextName)
    {
        var classPath = ClassPathHelper.FeaturesClassPath(srcDirectory, $"{FileNames.GetEntityFeatureClassName(entity.Name)}.cs", entity.Plural, projectBaseName);
        var fileText = GetQueryFileText(classPath.ClassNamespace, entity, srcDirectory, projectBaseName, isProtected, permissionName, dbContextName);
        utilities.CreateFile(classPath, fileText);
    }

    public static string GetQueryFileText(string classNamespace, Entity entity, string srcDirectory, 
        string projectBaseName, bool isProtected, string permissionName, string dbContextName)
    {
        var className = FileNames.GetEntityFeatureClassName(entity.Name);
        var queryRecordName = FileNames.QueryRecordName();
        var readDto = FileNames.GetDtoName(entity.Name, Dto.Read);

        var primaryKeyPropType = Entity.PrimaryKeyProperty.Type;
        var lowercasePrimaryKey = $"{entity.Name}Id";

        var dtoClassPath = ClassPathHelper.DtoClassPath(srcDirectory, "", entity.Plural, projectBaseName);
        var entityServicesClassPath = ClassPathHelper.EntityServicesClassPath(srcDirectory, "", entity.Plural, projectBaseName);
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

using {dtoClassPath.ClassNamespace};
using {dbContextClassPath.ClassNamespace};
using {exceptionsClassPath.ClassNamespace};{permissionsUsing}
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;

public static class {className}
{{
    public sealed record {queryRecordName}({primaryKeyPropType} {lowercasePrimaryKey}) : IRequest<{readDto}>;

    public sealed class Handler({dbContextName} dbContext{heimGuardCtor})
        : IRequestHandler<{queryRecordName}, {readDto}>
    {{
        public async Task<{readDto}> Handle({queryRecordName} request, CancellationToken cancellationToken)
        {{{permissionCheck}
            var result = await dbContext.{entity.Plural}
                .AsNoTracking()
                .GetById(request.{lowercasePrimaryKey}, cancellationToken);
            return result.To{readDto}();
        }}
    }}
}}";
    }
}
