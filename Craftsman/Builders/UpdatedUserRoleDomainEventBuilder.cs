namespace Craftsman.Builders;

using Helpers;
using MediatR;
using Services;

public static class UpdatedUserRoleDomainEventBuilder
{
    public class Command : IRequest
    {
    }

    public class Handler(
        ICraftsmanUtilities utilities,
        IScaffoldingDirectoryStore scaffoldingDirectoryStore)
        : IRequestHandler<Command>
    {
        public Task Handle(Command request, CancellationToken cancellationToken)
        {
            var classPath = ClassPathHelper.DomainEventsClassPath(scaffoldingDirectoryStore.SrcDirectory,
                $"{FileNames.UserRolesUpdateDomainMessage()}.cs",
                "Users",
                scaffoldingDirectoryStore.ProjectBaseName);
            var fileText = GetFileText(classPath.ClassNamespace);
            utilities.CreateFile(classPath, fileText);
            return Task.CompletedTask;
        }

        private static string GetFileText(string classNamespace)
        {
            return @$"namespace {classNamespace};

public class {FileNames.UserRolesUpdateDomainMessage()} : DomainEvent
{{
    public Guid UserId;
}}
            ";
        }
    }
}