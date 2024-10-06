namespace Craftsman.Builders;

using Helpers;
using MediatR;
using Services;

public static class UpdatedDomainEventBuilder
{
    public sealed record UpdatedDomainEventBuilderCommand(string EntityName, string EntityPlural) : IRequest;

    public class Handler(
        ICraftsmanUtilities utilities,
        IScaffoldingDirectoryStore scaffoldingDirectoryStore)
        : IRequestHandler<UpdatedDomainEventBuilderCommand>
    {
        public Task Handle(UpdatedDomainEventBuilderCommand request, CancellationToken cancellationToken)
        {
            var classPath = ClassPathHelper.DomainEventsClassPath(scaffoldingDirectoryStore.SrcDirectory,
                $"{FileNames.EntityUpdatedDomainMessage(request.EntityName)}.cs",
                request.EntityPlural,
                scaffoldingDirectoryStore.ProjectBaseName);
            var fileText = GetFileText(classPath.ClassNamespace, request.EntityName);
            utilities.CreateFile(classPath, fileText);
            return Task.CompletedTask;
        }

        private static string GetFileText(string classNamespace, string entityName)
        {
            return @$"namespace {classNamespace};

public sealed class {FileNames.EntityUpdatedDomainMessage(entityName)} : DomainEvent
{{
    public Guid Id {{ get; set; }} 
}}
            ";
        }
    }
}