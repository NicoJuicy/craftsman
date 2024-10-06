namespace Craftsman.Builders.Projects;

using Helpers;
using Services;

public class SharedKernelCsProjBuilder(ICraftsmanUtilities utilities)
{
    public void CreateSharedKernelCsProj(string solutionDirectory)
    {
        var classPath = ClassPathHelper.SharedKernelProjectClassPath(solutionDirectory);
        var fileText = GetMessagesCsProjFileText();
        utilities.CreateFile(classPath, fileText);
    }

    public static string GetMessagesCsProjFileText()
    {
        return @$"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
  </PropertyGroup>

</Project>";
    }
}
