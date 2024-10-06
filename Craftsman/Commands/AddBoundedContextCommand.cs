namespace Craftsman.Commands;

using System.IO.Abstractions;
using Domain;
using Helpers;
using MediatR;
using Services;
using Spectre.Console;
using Spectre.Console.Cli;

public class AddBoundedContextCommand(
    IFileSystem fileSystem,
    IConsoleWriter consoleWriter,
    ICraftsmanUtilities utilities,
    IScaffoldingDirectoryStore scaffoldingDirectoryStore,
    IAnsiConsole console,
    IFileParsingHelper fileParsingHelper,
    IMediator mediator)
    : Command<AddBoundedContextCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<Filepath>")] public string Filepath { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var potentialSolutionDir = utilities.GetRootDir();

        utilities.IsSolutionDirectoryGuard(potentialSolutionDir);
        scaffoldingDirectoryStore.SetSolutionDirectory(potentialSolutionDir);

        fileParsingHelper.RunInitialTemplateParsingGuards(settings.Filepath);
        var boundedContexts = fileParsingHelper.GetTemplateFromFile<BoundedContextsTemplate>(settings.Filepath);
        consoleWriter.WriteHelpText($"Your template file was parsed successfully.");

        foreach (var template in boundedContexts.BoundedContexts)
            new ApiScaffoldingService(console, consoleWriter, utilities, scaffoldingDirectoryStore, fileSystem, mediator, fileParsingHelper)
                .ScaffoldApi(potentialSolutionDir, template);

        consoleWriter.WriteHelpHeader(
            $"{Environment.NewLine}Your feature has been successfully added. Keep up the good work! {Emoji.Known.Sparkles}");
        return 0;
    }
}