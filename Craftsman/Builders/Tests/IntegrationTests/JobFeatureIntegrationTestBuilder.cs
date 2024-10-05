namespace Craftsman.Builders.Tests.IntegrationTests;

using System;
using Craftsman.Services;
using Domain;
using Domain.Enums;
using Helpers;
using Humanizer;
using MediatR;

public static class JobFeatureIntegrationTestBuilder
{
    public record Command(Feature Feature, string EntityPlural, string DbContextName) : IRequest;

    public class Handler(
        ICraftsmanUtilities utilities,
        IScaffoldingDirectoryStore scaffoldingDirectoryStore)
        : IRequestHandler<Command>
    {
        public Task Handle(Command request, CancellationToken cancellationToken)
        {
            var classPath = ClassPathHelper.FeatureTestClassPath(scaffoldingDirectoryStore.TestDirectory, 
                $"{request.Feature.Name}Tests.cs", 
                request.EntityPlural, 
                scaffoldingDirectoryStore.ProjectBaseName);
            var fileText = GetFileText(classPath, request.Feature, request.EntityPlural, request.DbContextName);
            utilities.CreateFile(classPath, fileText);
            return Task.FromResult(true);
        }

        private string GetFileText(ClassPath classPath, Feature feature, string entityPlural, string dbContextName)
        {
            var featuresClassPath = ClassPathHelper.FeaturesClassPath(scaffoldingDirectoryStore.SrcDirectory,
                $"",
                entityPlural,
                scaffoldingDirectoryStore.ProjectBaseName);
            var servicesClassPath = ClassPathHelper.WebApiServicesClassPath(scaffoldingDirectoryStore.SrcDirectory,
                $"",
                scaffoldingDirectoryStore.ProjectBaseName);
            var dbContextClassPath = ClassPathHelper.DbContextClassPath(scaffoldingDirectoryStore.SrcDirectory,
                $"",
                scaffoldingDirectoryStore.ProjectBaseName);
            
            return @$"namespace {classPath.ClassNamespace};

using {featuresClassPath.ClassNamespace};
using {servicesClassPath.ClassNamespace};
using {dbContextClassPath.ClassNamespace};
using Bogus;
using Domain;
using System.Threading.Tasks;

public class {classPath.ClassNameWithoutExt} : TestBase
{{
    [Fact]
    public async Task can_perform_{feature.Name.Humanize().Underscore()}()
    {{
        // Arrange
        var testingServiceScope = new {FileNames.TestingServiceScope()}();
        var user = Guid.NewGuid().ToString();
        var dbContextName = testingServiceScope.GetService<{dbContextName}>();

        // Act
        var job = new {feature.Name}(dbContextName);
        var command = new {feature.Name}.Command() {{ User = user }};
        await job.Handle(command, CancellationToken.None);

        // Assert
        // TODO job assertion
    }}
}}";
        }
    }
}