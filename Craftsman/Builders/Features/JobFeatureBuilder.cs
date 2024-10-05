namespace Craftsman.Builders.Features;

using Craftsman.Domain;
using Craftsman.Helpers;
using Craftsman.Services;
using Humanizer;
using MediatR;

public static class JobFeatureBuilder
{
    public record Command(Feature Feature, string EntityPlural, string DbContextName) : IRequest;

    public class Handler(
        ICraftsmanUtilities utilities,
        IScaffoldingDirectoryStore scaffoldingDirectoryStore)
        : IRequestHandler<Command>
    {
        public Task Handle(Command request, CancellationToken cancellationToken)
        {
            var classPath = ClassPathHelper.FeaturesClassPath(scaffoldingDirectoryStore.SrcDirectory,
                $"{request.Feature.Name}.cs",
                request.EntityPlural,
                scaffoldingDirectoryStore.ProjectBaseName);
            var fileText = GetFileText(classPath.ClassNamespace, request.Feature, request.DbContextName);
            utilities.CreateFile(classPath, fileText);
            return Task.FromResult(true);
        }

        private string GetFileText(string classNamespace, Feature feature, string dbContextName)
        {
            var hangfireUtilsClassPath = ClassPathHelper.HangfireResourcesClassPath(scaffoldingDirectoryStore.SrcDirectory,
                $"",
                scaffoldingDirectoryStore.ProjectBaseName);
            var servicesUtilsClassPath = ClassPathHelper.WebApiServicesClassPath(scaffoldingDirectoryStore.SrcDirectory,
                $"",
                scaffoldingDirectoryStore.ProjectBaseName);
            var dbContextClassPath = ClassPathHelper.DbContextClassPath(scaffoldingDirectoryStore.SrcDirectory, 
                "", 
                scaffoldingDirectoryStore.ProjectBaseName);
            
            return @$"namespace {classNamespace};

using Hangfire;
using HeimGuard;
using {hangfireUtilsClassPath.ClassNamespace};
using {servicesUtilsClassPath.ClassNamespace};
using {dbContextClassPath.ClassNamespace};

public class {feature.Name}({dbContextName} dbContext)
{{    
    public sealed class Command : IJobWithUserContext
    {{
        public string User {{ get; set; }}
    }}

    [JobDisplayName(""{feature.Name.Humanize(LetterCasing.Title)}"")]
    [AutomaticRetry(Attempts = 1)]
    // [Queue(Consts.HangfireQueues.{feature.Name.Humanize(LetterCasing.Title).Replace(" ", "")})]
    [CurrentUserFilter]
    public async Task Handle(Command command, CancellationToken cancellationToken)
    {{
        // TODO some work here
        await dbContext.SaveChangesAsync(cancellationToken);
    }}
}}";
        }
    }
}