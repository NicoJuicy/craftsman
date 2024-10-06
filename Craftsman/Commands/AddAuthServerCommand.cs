namespace Craftsman.Commands;

using System.IO.Abstractions;
using Builders;
using Builders.AuthServer;
using Builders.Docker;
using Domain;
using Helpers;
using MediatR;
using Services;
using Spectre.Console;
using Spectre.Console.Cli;

public class AddAuthServerCommand(
    IFileSystem fileSystem,
    IConsoleWriter consoleWriter,
    ICraftsmanUtilities utilities,
    IScaffoldingDirectoryStore scaffoldingDirectoryStore,
    IFileParsingHelper fileParsingHelper,
    IMediator mediator,
    IAnsiConsole console)
    : Command<AddAuthServerCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<Filepath>")]
        public string Filepath { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var potentialSolutionDir = utilities.GetRootDir();

        utilities.IsSolutionDirectoryGuard(potentialSolutionDir);
        scaffoldingDirectoryStore.SetSolutionDirectory(potentialSolutionDir);

        fileParsingHelper.RunInitialTemplateParsingGuards(settings.Filepath);
        var template = fileParsingHelper.GetTemplateFromFile<AuthServerTemplate>(settings.Filepath);
        consoleWriter.WriteHelpText($"Your template file was parsed successfully.");

        AddAuthServer(scaffoldingDirectoryStore.SolutionDirectory, template);

        consoleWriter.WriteHelpHeader($"{Environment.NewLine}Your auth server has been successfully added. Keep up the good work! {Emoji.Known.Sparkles}");
        return 0;
    }

    public void AddAuthServer(string solutionDirectory, AuthServerTemplate template)
    {
        console.Status()
            .AutoRefresh(true)
            .Spinner(Spinner.Known.Dots2)
            .Start($"[yellow]Adding Auth Server [/]", ctx =>
            {
                ctx.Spinner(Spinner.Known.BouncingBar);
                ctx.Status($"[bold blue]Scaffolding files for Auth Server [/]");
                var projectBaseName = template.Name;
        
                new SolutionBuilder(utilities, fileSystem, mediator).BuildAuthServerProject(solutionDirectory, projectBaseName);

                var pulumiYamlBuilder = new PulumiYamlBuilders(utilities);
                pulumiYamlBuilder.CreateBaseFile(solutionDirectory, projectBaseName);
                pulumiYamlBuilder.CreateDevConfig(solutionDirectory, projectBaseName, template.Port, template.Username, template.Password);

                new Builders.AuthServer.ProgramBuilder(utilities).CreateAuthServerProgram(solutionDirectory, projectBaseName);
        
                new UserExtensionsBuilder(utilities).Create(solutionDirectory, projectBaseName);
                new ClientExtensionsBuilder(utilities).Create(solutionDirectory, projectBaseName);
                new ClientFactoryBuilder(utilities).Create(solutionDirectory, projectBaseName);
                new ScopeFactoryBuilder(utilities).Create(solutionDirectory, projectBaseName);
                new RealmBuildBuilder(utilities).Create(solutionDirectory, projectBaseName, template.RealmName, template.Clients);

                new DockerComposeBuilders(utilities, fileSystem).AddAuthServerToDockerCompose(solutionDirectory, template);
                
                consoleWriter.WriteLogMessage($"Auth server scaffolding was successful");
            });
    }
}