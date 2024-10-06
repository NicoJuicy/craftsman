namespace Craftsman.Builders;

using Helpers;
using MediatR;
using Services;

public static class IBoundaryServiceInterfaceBuilder
{
    public class BoundaryServiceInterfaceBuilderCommand : IRequest<bool>
    {
        public BoundaryServiceInterfaceBuilderCommand()
        {
        }
    }

    public class Handler(
        ICraftsmanUtilities utilities,
        IScaffoldingDirectoryStore scaffoldingDirectoryStore)
        : IRequestHandler<BoundaryServiceInterfaceBuilderCommand, bool>
    {
        public Task<bool> Handle(BoundaryServiceInterfaceBuilderCommand request, CancellationToken cancellationToken)
        {
            var boundaryServiceName = FileNames.BoundaryServiceInterface(scaffoldingDirectoryStore.ProjectBaseName);
            var classPath = ClassPathHelper.WebApiServicesClassPath(scaffoldingDirectoryStore.SrcDirectory, $"{boundaryServiceName}.cs", scaffoldingDirectoryStore.ProjectBaseName);
            var fileText = GetFileText(classPath.ClassNamespace);
            utilities.CreateFile(classPath, fileText);
            return Task.FromResult(true);
        }

        private string GetFileText(string classNamespace)
        {
            var boundaryServiceName = FileNames.BoundaryServiceInterface(scaffoldingDirectoryStore.ProjectBaseName);
            
            return @$"namespace {classNamespace};

public interface {boundaryServiceName}
{{
}}";
        }
    }
}
