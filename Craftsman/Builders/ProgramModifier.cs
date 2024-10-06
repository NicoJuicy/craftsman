namespace Craftsman.Builders;

using System;
using System.IO;
using System.IO.Abstractions;
using Services;

public class ProgramModifier(IFileSystem fileSystem)
{
    public void RegisterMassTransitService(string srcDirectory, string projectBaseName)
    {
        var classPath = ClassPathHelper.WebApiServiceExtensionsClassPath(srcDirectory, $"{FileNames.WebAppServiceConfiguration()}.cs", projectBaseName);

        if (!fileSystem.Directory.Exists(classPath.ClassDirectory))
            throw new DirectoryNotFoundException($"The `{classPath.ClassDirectory}` directory could not be found.");

        if (!fileSystem.File.Exists(classPath.FullClassPath))
            throw new FileNotFoundException($"The `{classPath.FullClassPath}` file could not be found.");

        var tempPath = $"{classPath.FullClassPath}temp";
        using (var input = fileSystem.File.OpenText(classPath.FullClassPath))
        {
            using var output = fileSystem.File.CreateText(tempPath);
            string line;
            while (null != (line = input.ReadLine()))
            {
                var newText = $"{line}";
                if (line.Contains($"builder.Services.AddInfrastructure"))
                    newText += @$"{Environment.NewLine}        builder.Services.AddMassTransitServices(builder.Environment, builder.Configuration);";

                //if (line.Contains($@"{infraClassPath.ClassNamespace};"))
                //    newText += @$"{ Environment.NewLine}    using { serviceRegistrationsClassPath.ClassNamespace}; ";

                output.WriteLine(newText);
            }
        }

        // delete the old file and set the name of the new one to the original name
        fileSystem.File.Delete(classPath.FullClassPath);
        fileSystem.File.Move(tempPath, classPath.FullClassPath);
    }
}
