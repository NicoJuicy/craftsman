namespace Craftsman.Services;

using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using Builders;
using Builders.Auth;
using Builders.Dtos;
using Builders.Endpoints;
using Builders.EntityModels;
using Builders.Features;
using Builders.Tests.Fakes;
using Builders.Tests.FunctionalTests;
using Builders.Tests.IntegrationTests;
using Builders.Tests.IntegrationTests.UserRoles;
using Builders.Tests.UnitTests;
using Builders.Tests.Utilities;
using Domain;
using Domain.Enums;
using Helpers;
using MediatR;

public class EntityScaffoldingService(ICraftsmanUtilities utilities, IFileSystem fileSystem, IMediator mediator, IConsoleWriter consoleWriter)
{
    public void ScaffoldEntities(string solutionDirectory,
        string srcDirectory,
        string testDirectory,
        string projectBaseName,
        List<Entity> entities,
        string dbContextName,
        bool addSwaggerComments,
        bool useSoftDelete,
        DbProvider dbProvider)
    {
        foreach (var entity in entities)
        {
            // not worrying about DTOs, profiles, validators, fakers - they are all added by default
            new EntityBuilder(utilities).CreateEntity(solutionDirectory, srcDirectory, entity, projectBaseName);
            new DtoBuilder(utilities, fileSystem).CreateDtos(srcDirectory, entity, projectBaseName);
            new EntityModelBuilder(utilities, fileSystem).CreateEntityModels(srcDirectory, entity, projectBaseName);
            new EntityMappingBuilder(utilities).CreateMapping(srcDirectory, entity.Name, entity.Plural, projectBaseName);
            new ApiRouteModifier(fileSystem, consoleWriter).AddRoutes(testDirectory, entity, projectBaseName); // api routes always added to testing by default. too much of a pain to scaffold dynamically

            mediator.Send(new DatabaseEntityConfigBuilder.Command(entity.Name, entity.Plural, entity.Properties));

            var isProtected = entity.Features.Any(f => f.IsProtected); // <-- one more example of why it would be nice to have specific endpoints for each feature 😤
            if (entity.Features.Count > 0)
                new ControllerBuilder(utilities).CreateController(solutionDirectory, srcDirectory, entity.Plural, projectBaseName, isProtected);

            // TODO refactor to factory?
            foreach (var feature in entity.Features)
                AddFeatureToProject(solutionDirectory, srcDirectory, testDirectory, projectBaseName, dbContextName, addSwaggerComments, feature, entity, useSoftDelete);

            // Shared Tests
            new FakesBuilder(utilities).CreateFakes(srcDirectory, testDirectory, projectBaseName, entity);
            new FakeEntityBuilderBuilder(utilities).CreateFakeBuilder(srcDirectory, testDirectory, projectBaseName, entity);
            new CreateEntityUnitTestBuilder(utilities)
                .CreateTests(solutionDirectory, testDirectory, srcDirectory, entity.Name, entity.Plural, entity.Properties, projectBaseName);
            new UpdateEntityUnitTestBuilder(utilities)
                .CreateTests(solutionDirectory, testDirectory, srcDirectory, entity.Name, entity.Plural, entity.Properties, projectBaseName);

            // domain events
            mediator.Send(new CreatedDomainEventBuilder.CreatedDomainEventBuilderCommand(entity.Name, entity.Plural));
            mediator.Send(new UpdatedDomainEventBuilder.UpdatedDomainEventBuilderCommand(entity.Name, entity.Plural));
        }

        AddRelationships(srcDirectory, projectBaseName, entities);
        AddStringArrayItems(srcDirectory, projectBaseName, entities, dbProvider);
        AddValueObjects(srcDirectory, projectBaseName, entities);

        new DbContextModifier(fileSystem).AddDbSetAndConfig(srcDirectory, entities, dbContextName, projectBaseName);
    }

    private void AddRelationships(string srcDirectory, string projectBaseName, List<Entity> entities)
    {
        // reloop once all bases are added for relationships to mod on top -- could push into the earlier loop if perf becomes an issue
        foreach (var entity in entities)
        {
            var entityModifier = new EntityModifier(fileSystem, consoleWriter);
            var allPropsNotNone = entity.Properties.Where(x => !x.GetDbRelationship.IsNone).ToList();
            foreach (var entityProperty in allPropsNotNone)
            {
                entityProperty.GetDbRelationship.UpdateEntityProperties(entityModifier, 
                    srcDirectory,
                    entity.Name,
                    entity.Plural,
                    entityProperty.ForeignEntityName,
                    entityProperty.ForeignEntityPlural,
                    entityProperty.Name,
                    projectBaseName);
                
                entityProperty.GetDbRelationship.UpdateEntityManagementMethods(entityModifier, 
                    srcDirectory,
                    entity.Name,
                    entity.Plural,
                    entityProperty,
                    projectBaseName);
                
                entityModifier.AddParentRelationshipEntity(srcDirectory,
                    entityProperty, 
                    entity.Name, 
                    entity.Plural, 
                    projectBaseName);
                
                new DatabaseEntityConfigModifier(fileSystem, consoleWriter).AddRelationships(srcDirectory, 
                    entity.Name,
                    entity.Plural, 
                    entityProperty, 
                    projectBaseName);
            }
        }
    }

    public void AddValueObjects(string srcDirectory, string projectBaseName, List<Entity> entities)
    {
        foreach (var entity in entities)
        {
            var valueObjectProps = entity.Properties.Where(x => x.IsValueObject).ToList();
            foreach (var valueObjectProp in valueObjectProps)
            {
                new DatabaseEntityConfigModifier(fileSystem, consoleWriter).AddValueObjectConfig(srcDirectory, 
                    entity.Name,
                    valueObjectProp, 
                    projectBaseName);
            }
        }
        
        var baseValueObjects = new List<EntityProperty>
        {
            new()
            {
                ValueObjectName = "Email",
                AsValueObject = "Email",
            },
            new()
            {
                Name = "Percent",
                ValueObjectName = "Percent",
                ValueObjectPlural = "Percentages",
                AsValueObject = "Percent",
            },
            new()
            {
                Name = "MonetaryAmount",
                ValueObjectName = "MonetaryAmount",
                AsValueObject = "MonetaryAmount",
            }
        };

        var distinctValueObjects = entities
            .SelectMany(x => x.Properties.Where(p => p.IsValueObject))
            .ToList();
        distinctValueObjects.AddRange(baseValueObjects);
        distinctValueObjects = distinctValueObjects.DistinctBy(x => x.ValueObjectName).ToList();
        foreach (var valueObjectProp in distinctValueObjects)
        {
            var classPath = ClassPathHelper.WebApiValueObjectsClassPath(srcDirectory, 
                $"{valueObjectProp.ValueObjectName}.cs",
                valueObjectProp.ValueObjectPlural,
                projectBaseName);
            
            if (fileSystem.File.Exists(classPath.FullClassPath))
                continue;
            
            var fileText = valueObjectProp.ValueObjectType.GetFileText(classPath.ClassNamespace, 
                valueObjectProp.ValueObjectName,
                valueObjectProp.Type,
                valueObjectProp.SmartNames,
                srcDirectory,
                projectBaseName);
            utilities.CreateFile(classPath, fileText);
        }
        
        var entitiesThatHaveValueObjectProperties = entities
            .Where(x => x.Properties.Any(p => p.IsValueObject))
            .ToList();
        foreach (var entityThatHasValueObjectProperties in entitiesThatHaveValueObjectProperties)
        {
            var voProperties = entityThatHasValueObjectProperties.Properties.Where(x => x.IsValueObject).ToList();
            foreach (var entityProperty in voProperties)
            {
                if (entityProperty.Type.ToLowerInvariant() != "string")
                {
                    new EntityMappingModifier(fileSystem, consoleWriter)
                        .UpdateMappingAttributesForValueObject(srcDirectory, 
                            entityThatHasValueObjectProperties.Name,
                            entityThatHasValueObjectProperties.Plural, 
                            entityProperty, 
                            projectBaseName);
                }
            }
        }
    }

    public void AddStringArrayItems(string srcDirectory, string projectBaseName, List<Entity> entities, DbProvider dbProvider)
    {
        foreach (var entity in entities)
        {
            var entityModifier = new EntityModifier(fileSystem, consoleWriter);
            var stringArrayProps = entity.Properties.Where(x => x.IsStringArray).ToList();
            foreach (var stringArrayProp in stringArrayProps)
            {
                entityModifier.AddStringArrayManagement(srcDirectory,
                    stringArrayProp,
                    entity.Name,
                    entity.Plural,
                    projectBaseName);
                
                new DatabaseEntityConfigModifier(fileSystem, consoleWriter).AddStringArrayProperty(srcDirectory, 
                    entity.Name,
                    stringArrayProp, 
                    dbProvider,
                    projectBaseName);
            }
            
        }
    }
    

    public void ScaffoldRolePermissions(string solutionDirectory,
        string srcDirectory,
        string testDirectory,
        string projectBaseName,
        string dbContextName,
        bool addSwaggerComments,
        bool useSoftDelete)
    {
        var entity = new Entity()
        {
            Name = "RolePermission",
            Features = new List<Feature>()
                {
                    new() { Type = FeatureType.GetList.Name, IsProtected = true, PermissionName = "CanReadRolePermissions" },
                    new() { Type = FeatureType.GetRecord.Name, IsProtected = true, PermissionName = "CanReadRolePermissions" },
                    new() { Type = FeatureType.AddRecord.Name, IsProtected = true },
                    new() { Type = FeatureType.UpdateRecord.Name, IsProtected = true },
                    new() { Type = FeatureType.DeleteRecord.Name, IsProtected = true },
                },
            Properties = new List<EntityProperty>()
                {
                    new() { Name = "Role", Type = "string" },
                    new() { Name = "Permission", Type = "string" }
                }
        };

        new EntityBuilder(utilities).CreateRolePermissionsEntity(srcDirectory, entity, projectBaseName);
        new DtoBuilder(utilities, fileSystem).CreateDtos(srcDirectory, entity, projectBaseName);
        new EntityModelBuilder(utilities, fileSystem).CreateEntityModels(srcDirectory, entity, projectBaseName);
        new EntityMappingBuilder(utilities).CreateMapping(srcDirectory, entity.Name, entity.Plural, projectBaseName);
        new ApiRouteModifier(fileSystem, consoleWriter).AddRoutes(testDirectory, entity, projectBaseName);
        mediator.Send(new DatabaseEntityConfigRolePermissionBuilder.Command());

        if (entity.Features.Count > 0)
            new ControllerBuilder(utilities).CreateController(solutionDirectory, srcDirectory, entity.Plural, projectBaseName, true);

        // TODO refactor to factory?
        foreach (var feature in entity.Features)
        {
            AddFeatureToProject(solutionDirectory, srcDirectory, testDirectory, projectBaseName, dbContextName, addSwaggerComments, feature, entity, useSoftDelete);
        }

        // Shared Tests
        new FakesBuilder(utilities).CreateRolePermissionFakes(srcDirectory, solutionDirectory, testDirectory, projectBaseName, entity);
        new RolePermissionsUnitTestBuilder(utilities).CreateRolePermissionTests(solutionDirectory, testDirectory, srcDirectory, projectBaseName);
        new RolePermissionsUnitTestBuilder(utilities).UpdateRolePermissionTests(solutionDirectory, testDirectory, srcDirectory, projectBaseName);
        new FakeEntityBuilderBuilder(utilities).CreateFakeBuilder(srcDirectory, testDirectory, projectBaseName, entity);
        
        // need to do db modifier
        new DbContextModifier(fileSystem).AddDbSetAndConfig(srcDirectory, new List<Entity>() { entity }, dbContextName, projectBaseName);

        // domain events
        mediator.Send(new CreatedDomainEventBuilder.CreatedDomainEventBuilderCommand(entity.Name, entity.Plural));
        mediator.Send(new UpdatedDomainEventBuilder.UpdatedDomainEventBuilderCommand(entity.Name, entity.Plural));
    }


    public void ScaffoldUser(string solutionDirectory,
        string srcDirectory,
        string testDirectory,
        string projectBaseName,
        string dbContextName,
        bool addSwaggerComments,
        bool useSoftDelete)
    {
        var userEntity = new Entity()
        {
            Name = "User",
            Features =
            [
                new() { Type = FeatureType.GetList.Name, IsProtected = true },
                new() { Type = FeatureType.GetRecord.Name, IsProtected = true },
                new() { Type = FeatureType.AddRecord.Name, IsProtected = true },
                new() { Type = FeatureType.UpdateRecord.Name, IsProtected = true },
                new() { Type = FeatureType.DeleteRecord.Name, IsProtected = true }
            ],
            Properties =
            [
                new() { Name = "Identifier", Type = "string" },
                new() { Name = "FirstName", Type = "string" },
                new() { Name = "LastName", Type = "string" },
                new() { Name = "Email", Type = "string" },
                new() { Name = "Username", Type = "string" },
                new() { Name = "UserRoles", Type = "ICollection<UserRole>", ForeignEntityPlural = "UserRoles" }
            ]
        };

        var entityBuilder = new EntityBuilder(utilities);
        entityBuilder.CreateUserEntity(srcDirectory, userEntity, projectBaseName);
        entityBuilder.CreateUserRoleEntity(srcDirectory, projectBaseName);
        
        // TODO custom dto for roles
        new DtoBuilder(utilities, fileSystem).CreateDtos(srcDirectory, userEntity, projectBaseName);
        
        new EntityModelBuilder(utilities, fileSystem).CreateEntityModels(srcDirectory, userEntity, projectBaseName);
        new EntityMappingBuilder(utilities).CreateMapping(srcDirectory, "User", "Users", projectBaseName);
        new ApiRouteModifier(fileSystem, consoleWriter).AddRoutesForUser(testDirectory, projectBaseName);
        mediator.Send(new DatabaseEntityConfigUserBuilder.Command());
        mediator.Send(new DatabaseEntityConfigUserRoleBuilder.Command());
        
        new ControllerBuilder(utilities).CreateController(solutionDirectory, srcDirectory, userEntity.Plural, projectBaseName, true);
        new ControllerModifier(fileSystem).AddCustomUserEndpoint(srcDirectory, projectBaseName);
        
        foreach (var feature in userEntity.Features)
        {
            AddFeatureToProject(solutionDirectory, srcDirectory, testDirectory, projectBaseName, dbContextName, addSwaggerComments, feature, userEntity, useSoftDelete);
        }
        new CommandAddUserRoleBuilder(utilities).CreateCommand(srcDirectory, userEntity, projectBaseName, dbContextName);
        new CommandRemoveUserRoleBuilder(utilities).CreateCommand(srcDirectory, userEntity, projectBaseName, dbContextName);
        // new AddUserFeatureBuilder(_utilities).AddFeature(srcDirectory, projectBaseName);
        new AddUserFeatureOverrideModifier(fileSystem).UpdateAddUserFeature(srcDirectory, projectBaseName, dbContextName);

        // extra testing
        new FakesBuilder(utilities).CreateUserFakes(srcDirectory, solutionDirectory, testDirectory, projectBaseName, userEntity);
        new CreateUserRoleUnitTestBuilder(utilities).CreateTests(solutionDirectory, testDirectory, srcDirectory, projectBaseName);
        new AddRemoveUserRoleTestsBuilder(utilities).CreateTests(testDirectory, srcDirectory, projectBaseName);
        new UserUnitTestBuilder(utilities).CreateTests(solutionDirectory, testDirectory, srcDirectory, projectBaseName);
        new UserUnitTestBuilder(utilities).UpdateTests(solutionDirectory, testDirectory, srcDirectory, projectBaseName);
        new FakeEntityBuilderBuilder(utilities).CreateFakeBuilder(srcDirectory, testDirectory, projectBaseName, userEntity);
        
        // need to do db modifier
        new DbContextModifier(fileSystem).AddDbSetAndConfig(srcDirectory, [userEntity], dbContextName, projectBaseName);
        new DbContextModifier(fileSystem).AddDbSetAndConfig(srcDirectory, [
            new Entity() { Name = "UserRole", Plural = "UserRoles" }
        ], dbContextName, projectBaseName);

        // domain events
        mediator.Send(new CreatedDomainEventBuilder.CreatedDomainEventBuilderCommand(userEntity.Name, userEntity.Plural));
        mediator.Send(new UpdatedDomainEventBuilder.UpdatedDomainEventBuilderCommand(userEntity.Name, userEntity.Plural));
        mediator.Send(new UpdatedUserRoleDomainEventBuilder.Command());
    }

    public void AddFeatureToProject(string solutionDirectory, string srcDirectory, string testDirectory, string projectBaseName,
        string dbContextName, bool addSwaggerComments, Feature feature, Entity entity, bool useSoftDelete)
    {
        var controllerClassPath = ClassPathHelper.ControllerClassPath(srcDirectory, $"{FileNames.GetControllerName(entity.Plural)}.cs", projectBaseName);
        if (!File.Exists(controllerClassPath.FullClassPath))
            new ControllerBuilder(utilities).CreateController(solutionDirectory, srcDirectory, entity.Plural, projectBaseName, feature.IsProtected);

        if (feature.IsProtected)
            new PermissionsModifier(fileSystem).AddPermission(srcDirectory, feature.PermissionName, projectBaseName);

        if (feature.Type == FeatureType.AddRecord.Name)
        {
            new CommandAddRecordBuilder(utilities).CreateCommand(srcDirectory, entity, projectBaseName, feature.IsProtected, feature.PermissionName, dbContextName);
            switch (entity.Name)
            {
                case "RolePermission":
                    new Craftsman.Builders.Tests.IntegrationTests.RolePermissions.AddCommandTestBuilder(utilities).CreateTests(testDirectory, srcDirectory, entity, projectBaseName);
                    break;
                case "User":
                    new Craftsman.Builders.Tests.IntegrationTests.Users.AddCommandTestBuilder(utilities).CreateTests(testDirectory, srcDirectory, entity, projectBaseName);
                    break;
                default:
                    new AddCommandTestBuilder(utilities).CreateTests(testDirectory, srcDirectory, entity, projectBaseName, feature.PermissionName, feature.IsProtected);
                    break;
            }

            new ControllerModifier(fileSystem).AddEndpoint(srcDirectory, FeatureType.AddRecord, entity, addSwaggerComments,
                feature, projectBaseName);
        }

        if (feature.Type == FeatureType.GetRecord.Name)
        {
            new QueryGetRecordBuilder(utilities).CreateQuery(srcDirectory, entity, projectBaseName, feature.IsProtected, feature.PermissionName, dbContextName);
            switch (entity.Name)
            {
                case "RolePermission":
                    new Craftsman.Builders.Tests.IntegrationTests.RolePermissions.GetRecordQueryTestBuilder(utilities).CreateTests(solutionDirectory, testDirectory, srcDirectory, entity, projectBaseName);
                    break;
                case "User":
                    new Craftsman.Builders.Tests.IntegrationTests.Users.GetRecordQueryTestBuilder(utilities).CreateTests(solutionDirectory, testDirectory, srcDirectory, entity, projectBaseName);
                    break;
                default:
                    new GetRecordQueryTestBuilder(utilities).CreateTests(solutionDirectory, testDirectory, srcDirectory, entity, projectBaseName, feature.PermissionName, feature.IsProtected);
                    break;
            }

            new ControllerModifier(fileSystem).AddEndpoint(srcDirectory, FeatureType.GetRecord, entity, addSwaggerComments,
                feature, projectBaseName);
        }

        if (feature.Type == FeatureType.GetList.Name)
        {
            new QueryGetListBuilder(utilities).CreateQuery(srcDirectory, entity, projectBaseName, feature.IsProtected, feature.PermissionName, dbContextName);
            new GetListQueryTestBuilder(utilities).CreateTests(testDirectory, srcDirectory, entity, projectBaseName, feature.PermissionName, feature.IsProtected);
            new ControllerModifier(fileSystem).AddEndpoint(srcDirectory, FeatureType.GetList, entity, addSwaggerComments,
                feature, projectBaseName);
        }

        if (feature.Type == FeatureType.GetAll.Name)
        {
            new QueryGetAllBuilder(utilities).CreateQuery(srcDirectory, entity, projectBaseName, feature.IsProtected, feature.PermissionName, dbContextName);
            new GetAllQueryTestBuilder(utilities).CreateTests(testDirectory, srcDirectory, entity, projectBaseName, feature.PermissionName, feature.IsProtected);
            new ControllerModifier(fileSystem).AddEndpoint(srcDirectory, FeatureType.GetAll, entity, addSwaggerComments,
                feature, projectBaseName);
        }

        if (feature.Type == FeatureType.DeleteRecord.Name)
        {
            new CommandDeleteRecordBuilder(utilities).CreateCommand(srcDirectory, entity, projectBaseName, feature.IsProtected, feature.PermissionName, dbContextName);
            new DeleteCommandTestBuilder(utilities).CreateTests(solutionDirectory, testDirectory, srcDirectory, entity, projectBaseName, useSoftDelete, feature.PermissionName, feature.IsProtected);
            new ControllerModifier(fileSystem).AddEndpoint(srcDirectory, FeatureType.DeleteRecord, entity, addSwaggerComments,
                feature, projectBaseName);
        }

        if (feature.Type == FeatureType.UpdateRecord.Name)
        {
            new CommandUpdateRecordBuilder(utilities).CreateCommand(srcDirectory, entity, projectBaseName, feature.IsProtected, feature.PermissionName, dbContextName);
            
            switch (entity.Name)
            {
                case "RolePermission":
                    new Craftsman.Builders.Tests.IntegrationTests.RolePermissions.PutCommandTestBuilder(utilities).CreateTests(solutionDirectory, testDirectory, srcDirectory, entity, projectBaseName);
                    break;
                case "User":
                    new Craftsman.Builders.Tests.IntegrationTests.Users.PutCommandTestBuilder(utilities).CreateTests(solutionDirectory, testDirectory, srcDirectory, entity, projectBaseName);
                    break;
                default:
                    new PutCommandTestBuilder(utilities).CreateTests(solutionDirectory, testDirectory, srcDirectory, entity, projectBaseName, feature.IsProtected, feature.PermissionName);
                    break;
            }
            
            new ControllerModifier(fileSystem).AddEndpoint(srcDirectory, FeatureType.UpdateRecord, entity, addSwaggerComments,
                feature, projectBaseName);
        }

        // if (feature.Type == FeatureType.PatchRecord.Name)
        // {
        //     new CommandPatchRecordBuilder(_utilities).CreateCommand(srcDirectory, entity, projectBaseName, feature.IsProtected, feature.PermissionName);
        //     new PatchCommandTestBuilder(_utilities).CreateTests(solutionDirectory, testDirectory, srcDirectory, entity, projectBaseName);
        //     new PatchEntityTestBuilder(_utilities).CreateTests(solutionDirectory, srcDirectory, testDirectory, entity, feature.IsProtected, projectBaseName);
        //     new ControllerModifier(_fileSystem).AddEndpoint(srcDirectory, FeatureType.PatchRecord, entity, addSwaggerComments,
        //         feature, projectBaseName);
        // }

        if (feature.Type == FeatureType.AddListByFk.Name)
        {
            new CommandAddListBuilder(utilities).CreateCommand(srcDirectory, entity, projectBaseName, feature, feature.IsProtected, feature.PermissionName, dbContextName);
            new AddListCommandTestBuilder(utilities).CreateTests(solutionDirectory, testDirectory, srcDirectory, entity, feature, projectBaseName, feature.PermissionName, feature.IsProtected);
            new ControllerModifier(fileSystem).AddEndpoint(srcDirectory, FeatureType.AddListByFk, entity, addSwaggerComments,
                feature, projectBaseName);
        }

        if (feature.Type == FeatureType.AdHoc.Name)
        {
            new EmptyFeatureBuilder(utilities).CreateCommand(srcDirectory, dbContextName, projectBaseName, feature);
            // TODO ad hoc feature endpoint
            // TODO empty failing test to promote test writing?
        }

        if (feature.Type == FeatureType.Job.Name)
        {
            mediator.Send(new JobFeatureBuilder.Command(feature, entity.Plural, dbContextName));
            mediator.Send(new JobFeatureIntegrationTestBuilder.Command(feature, entity.Plural, dbContextName));
        }
    }
}
