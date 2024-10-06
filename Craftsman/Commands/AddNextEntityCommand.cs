namespace Craftsman.Commands;

using System.IO.Abstractions;
using Domain;
using Helpers;
using MediatR;
using Services;
using Spectre.Console;
using Spectre.Console.Cli;

public class AddNextEntityCommand(
    IFileSystem fileSystem,
    IConsoleWriter consoleWriter,
    ICraftsmanUtilities utilities,
    IScaffoldingDirectoryStore scaffoldingDirectoryStore,
    IFileParsingHelper fileParsingHelper,
    IMediator mediator)
    : Command<AddNextEntityCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<Filepath>")]
        public string Filepath { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var potentialNextRootDir = utilities.GetRootDir();

        utilities.IsNextJsRootDir(potentialNextRootDir);
        scaffoldingDirectoryStore.SetNextJsDir(potentialNextRootDir);

        fileParsingHelper.RunInitialTemplateParsingGuards(settings.Filepath);
        var template = fileParsingHelper.GetTemplateFromFile<NextJsEntityTemplate>(settings.Filepath);
        consoleWriter.WriteHelpText($"Your template file was parsed successfully.");
        
        new NextJsEntityScaffoldingService(utilities, fileSystem, mediator).ScaffoldEntities(template, scaffoldingDirectoryStore.SpaSrcDirectory);

        consoleWriter.WriteHelpHeader($"{Environment.NewLine}Your entity scaffolding has been successfully added. Keep up the good work! {Emoji.Known.Sparkles}");
        return 0;
    }
}