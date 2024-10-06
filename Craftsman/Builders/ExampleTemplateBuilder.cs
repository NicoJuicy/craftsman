namespace Craftsman.Builders;

using System.Text.Json;
using Domain;
using Helpers;
using Services;

public class ExampleTemplateBuilder(ICraftsmanUtilities utilities)
{
    public void CreateFile(string solutionDirectory, DomainProject domainProject)
    {
        var classPath = ClassPathHelper.ExampleYamlRootClassPath(solutionDirectory, "exampleTemplate.json");
        var fileText = JsonSerializer.Serialize(domainProject);
        utilities.CreateFile(classPath, fileText);
    }

    public void CreateYamlFile(string solutionDirectory, string domainProject)
    {
        var classPath = ClassPathHelper.ExampleYamlRootClassPath(solutionDirectory, "exampleTemplate.yaml");
        utilities.CreateFile(classPath, domainProject);
    }
}
