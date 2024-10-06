namespace Craftsman.Builders;

using Helpers;
using Services;

public class WebApiLaunchSettingsBuilder(ICraftsmanUtilities utilities)
{
    public void CreateLaunchSettings(string srcDirectory, string projectBaseName)
    {
        var classPath = ClassPathHelper.WebApiLaunchSettingsClassPath(srcDirectory, $"launchSettings.json", projectBaseName);
        var fileText = GetLaunchSettingsText();
        utilities.CreateFile(classPath, fileText);
    }

    public static string GetLaunchSettingsText()
    {
        return @$"{{
  ""profiles"": {{
  }}
}}";
    }
}
