namespace Craftsman.Builders.Auth;

using System.IO.Abstractions;
using Services;

public class PermissionsModifier(IFileSystem fileSystem)
{
    public void AddPermission(string srcDirectory, string permission, string projectBaseName)
    {
        if(string.IsNullOrWhiteSpace(permission))
            return;
        
        var classPath = ClassPathHelper.PolicyDomainClassPath(srcDirectory, $"Permissions.cs", projectBaseName);

        if (!fileSystem.Directory.Exists(classPath.ClassDirectory))
            fileSystem.Directory.CreateDirectory(classPath.ClassDirectory);

        if (!fileSystem.File.Exists(classPath.FullClassPath))
            throw new FileNotFoundException($"The `{classPath.FullClassPath}` file could not be found.");

        var fileText = fileSystem.File.ReadAllText(classPath.FullClassPath);
        if (fileText.Contains($"const string {permission} ="))
            return;

        var tempPath = $"{classPath.FullClassPath}temp";
        using (var input = fileSystem.File.OpenText(classPath.FullClassPath))
        {
            using var output = fileSystem.File.CreateText(tempPath);
            string line;
            while (null != (line = input.ReadLine()))
            {
                var newText = $"{line}";
                if (line.Contains($"Permissions marker"))
                {
                    newText += @$"{Environment.NewLine}    public const string {permission} = nameof({permission});";
                }

                output.WriteLine(newText);
            }
        }

        // delete the old file and set the name of the new one to the original name
        fileSystem.File.Delete(classPath.FullClassPath);
        fileSystem.File.Move(tempPath, classPath.FullClassPath);
    }
}

