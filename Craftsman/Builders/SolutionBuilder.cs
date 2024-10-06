namespace Craftsman.Builders;

using System.IO;
using System.IO.Abstractions;
using Domain;
using Dtos;
using ExtensionBuilders;
using Helpers;
using MediatR;
using Projects;
using Services;

public class SolutionBuilder(ICraftsmanUtilities utilities, IFileSystem fileSystem, IMediator mediator)
{
    public void BuildSolution(string solutionDirectory, string projectName)
    {
        fileSystem.Directory.CreateDirectory(solutionDirectory);
        utilities.ExecuteProcess("dotnet", @$"new sln -n {projectName}", solutionDirectory);
        BuildSharedKernelProject(solutionDirectory);
    }

    public void AddProjects(string solutionDirectory, string srcDirectory, string testDirectory, DbProvider dbProvider, string projectBaseName, bool addJwtAuth, int otelAgentPort, bool useCustomErrorHandler)
    {
        // add webapi first so it is default project
        BuildWebApiProject(solutionDirectory, srcDirectory, projectBaseName, addJwtAuth, dbProvider, otelAgentPort, useCustomErrorHandler);
        BuildIntegrationTestProject(solutionDirectory, testDirectory, projectBaseName, dbProvider);
        BuildFunctionalTestProject(solutionDirectory, testDirectory, projectBaseName, dbProvider);
        BuildSharedTestProject(solutionDirectory, testDirectory, projectBaseName);
        BuildUnitTestProject(solutionDirectory, testDirectory, projectBaseName);
    }

    private void BuildWebApiProject(string solutionDirectory, string srcDirectory, string projectBaseName, bool useJwtAuth, DbProvider dbProvider, int otelAgentPort, bool useCustomErrorHandler)
    {
        var solutionFolder = srcDirectory.GetSolutionFolder(solutionDirectory);
        var webApiProjectClassPath = ClassPathHelper.WebApiProjectClassPath(srcDirectory, projectBaseName);

        new WebApiCsProjBuilder(utilities).CreateWebApiCsProj(srcDirectory, projectBaseName, dbProvider, useCustomErrorHandler);
        utilities.ExecuteProcess("dotnet", $@"sln add ""{webApiProjectClassPath.FullClassPath}"" --solution-folder {solutionFolder}", solutionDirectory);

        // base folders
        fileSystem.Directory.CreateDirectory(ClassPathHelper.ControllerClassPath(srcDirectory, "", projectBaseName, "v1").ClassDirectory);
        fileSystem.Directory.CreateDirectory(ClassPathHelper.WebApiServiceExtensionsClassPath(srcDirectory, "", projectBaseName).ClassDirectory);
        fileSystem.Directory.CreateDirectory(ClassPathHelper.WebApiMiddlewareClassPath(srcDirectory, "", projectBaseName).ClassDirectory);

        // additional from what was other projects
        fileSystem.Directory.CreateDirectory(ClassPathHelper.ExceptionsClassPath(srcDirectory, "", projectBaseName).ClassDirectory);
        fileSystem.Directory.CreateDirectory(ClassPathHelper.WebApiResourcesClassPath(srcDirectory, "", projectBaseName).ClassDirectory);
        fileSystem.Directory.CreateDirectory(ClassPathHelper.WebApiResourcesClassPath(srcDirectory, "", projectBaseName).ClassDirectory);
        fileSystem.Directory.CreateDirectory(ClassPathHelper.DbContextClassPath(srcDirectory, "", projectBaseName).ClassDirectory);

        new ApiVersioningExtensionsBuilder(utilities).CreateApiVersioningServiceExtension(srcDirectory, projectBaseName);
        new CorsExtensionsBuilder(utilities).CreateCorsServiceExtension(srcDirectory, projectBaseName);
        new OpenTelemetryExtensionsBuilder(utilities).CreateOTelServiceExtension(srcDirectory, projectBaseName, dbProvider, otelAgentPort);
        new WebApiLaunchSettingsBuilder(utilities).CreateLaunchSettings(srcDirectory, projectBaseName);
        new ProgramBuilder(utilities).CreateWebApiProgram(srcDirectory, useJwtAuth, projectBaseName, useCustomErrorHandler);
        new ServiceConfigurationBuilder(utilities).CreateWebAppServiceConfiguration(srcDirectory, projectBaseName, useCustomErrorHandler);
        new ConstsResourceBuilder(utilities).CreateLocalConfig(srcDirectory, projectBaseName);
        new QueryKitConfigBuilder(utilities).CreateConfig(srcDirectory, projectBaseName);
        new InfrastructureServiceRegistrationBuilder(utilities).CreateInfrastructureServiceExtension(srcDirectory, projectBaseName);

        if (useCustomErrorHandler)
        {
            new ErrorHandlerFilterAttributeBuilder(utilities).CreateErrorHandlerFilterAttribute(srcDirectory, projectBaseName);
        }
        else
        {
            new ErrorHandlerWithHellang(utilities).CreateErrorHandler(srcDirectory, projectBaseName);
        }

        new BasePaginationParametersBuilder(utilities).CreateBasePaginationParameters(srcDirectory, projectBaseName);
        new PagedListBuilder(utilities).CreatePagedList(srcDirectory, projectBaseName);
        mediator.Send(new CoreExceptionBuilder.CoreExceptionBuilderCommand());

        utilities.AddProjectReference(webApiProjectClassPath, @"..\..\..\SharedKernel\SharedKernel.csproj");
    }

    private void BuildIntegrationTestProject(string solutionDirectory, string testDirectory, string projectBaseName, DbProvider dbProvider)
    {
        var solutionFolder = testDirectory.GetSolutionFolder(solutionDirectory);
        var testProjectClassPath = ClassPathHelper.IntegrationTestProjectRootClassPath(testDirectory, "", projectBaseName);

        new IntegrationTestsCsProjBuilder(utilities).CreateTestsCsProj(testDirectory, projectBaseName, dbProvider);
        utilities.ExecuteProcess("dotnet", $@"sln add ""{testProjectClassPath.FullClassPath}"" --solution-folder {solutionFolder}", solutionDirectory);
    }

    private void BuildFunctionalTestProject(string solutionDirectory, string testDirectory, string projectBaseName, DbProvider dbProvider)
    {
        var solutionFolder = testDirectory.GetSolutionFolder(solutionDirectory);
        var testProjectClassPath = ClassPathHelper.FunctionalTestProjectRootClassPath(testDirectory, "", projectBaseName);

        new FunctionalTestsCsProjBuilder(utilities).CreateTestsCsProj(testDirectory, projectBaseName, dbProvider);
        utilities.ExecuteProcess("dotnet", $@"sln add ""{testProjectClassPath.FullClassPath}"" --solution-folder {solutionFolder}", solutionDirectory);
    }

    private void BuildSharedTestProject(string solutionDirectory, string testDirectory, string projectBaseName)
    {
        var solutionFolder = testDirectory.GetSolutionFolder(solutionDirectory);
        var testProjectClassPath = ClassPathHelper.SharedTestProjectRootClassPath(testDirectory, "", projectBaseName);

        new SharedTestsCsProjBuilder(utilities).CreateTestsCsProj(testDirectory, projectBaseName);
        utilities.ExecuteProcess("dotnet", $@"sln add ""{testProjectClassPath.FullClassPath}"" --solution-folder {solutionFolder}", solutionDirectory);
    }

    private void BuildUnitTestProject(string solutionDirectory, string testDirectory, string projectBaseName)
    {
        var solutionFolder = testDirectory.GetSolutionFolder(solutionDirectory);
        var testProjectClassPath = ClassPathHelper.UnitTestProjectRootClassPath(testDirectory, "", projectBaseName);

        new UnitTestsCsProjBuilder(utilities).CreateTestsCsProj(testDirectory, projectBaseName);
        utilities.ExecuteProcess("dotnet", $@"sln add ""{testProjectClassPath.FullClassPath}"" --solution-folder {solutionFolder}", solutionDirectory);
    }

    public void BuildSharedKernelProject(string solutionDirectory)
    {
        var projectExists = File.Exists(Path.Combine(solutionDirectory, "SharedKernel", "SharedKernel.csproj"));
        if (projectExists) return;

        var projectClassPath = ClassPathHelper.SharedKernelProjectRootClassPath(solutionDirectory, "");
        new SharedKernelCsProjBuilder(utilities).CreateSharedKernelCsProj(solutionDirectory);
        utilities.ExecuteProcess("dotnet", $@"sln add ""{projectClassPath.FullClassPath}""", solutionDirectory);
    }

    public void BuildAuthServerProject(string solutionDirectory, string authServerProjectName)
    {
        var projectExists = File.Exists(Path.Combine(solutionDirectory, authServerProjectName, $"{authServerProjectName}.csproj"));
        if (projectExists) return;

        var projectClassPath = ClassPathHelper.AuthServerProjectClassPath(solutionDirectory, authServerProjectName);
        new AuthServerProjBuilder(utilities).CreateProject(solutionDirectory, authServerProjectName);
        utilities.ExecuteProcess("dotnet", $@"sln add ""{projectClassPath.FullClassPath}""", solutionDirectory);
    }

    public void BuildBffProject(string solutionDirectory, string projectName, int? proxyPort)
    {
        var projectExists = File.Exists(Path.Combine(solutionDirectory, projectName, $"{projectName}.csproj"));
        if (projectExists) return;

        var projectClassPath = ClassPathHelper.BffProjectClassPath(solutionDirectory, projectName);
        new BffProjBuilder(utilities).CreateProject(solutionDirectory, projectName, proxyPort);
        utilities.ExecuteProcess("dotnet", $@"sln add ""{projectClassPath.FullClassPath}""", solutionDirectory);
    }
}

public static class Extensions
{
    public static string GetSolutionFolder(this string projectDir, string solutionDir)
    {
        var folder = projectDir.Replace(solutionDir, "");

        return folder.Length > 0 ? folder.Substring(1) : folder;
    }
}