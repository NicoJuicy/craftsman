namespace Craftsman.Commands;

using System.IO.Abstractions;
using Builders;
using Domain;
using Helpers;
using MediatR;
using Services;
using Spectre.Console;
using Spectre.Console.Cli;

public class NewExampleCommand(
    IAnsiConsole console,
    IFileSystem fileSystem,
    IConsoleWriter consoleWriter,
    ICraftsmanUtilities utilities,
    IScaffoldingDirectoryStore scaffoldingDirectoryStore,
    IDbMigrator dbMigrator,
    IGitService gitService,
    IFileParsingHelper fileParsingHelper,
    IMediator mediator)
    : Command<NewExampleCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[ProjectName]")]
        public string ProjectName { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var rootDir = fileSystem.Directory.GetCurrentDirectory();
        var myEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        if (myEnv == "Dev")
            rootDir = console.Ask<string>("Enter the root directory of your project:");

        var (exampleType, projectName) = RunPrompt(settings.ProjectName);
        var templateString = GetExampleDomain(projectName, exampleType);

        var domainProject = FileParsingHelper.ReadYamlString<DomainProject>(templateString);

        scaffoldingDirectoryStore.SetSolutionDirectory(rootDir, domainProject.DomainName);
        var domainCommand = new NewDomainCommand(console, fileSystem, consoleWriter, utilities, scaffoldingDirectoryStore, dbMigrator, gitService, fileParsingHelper, mediator);
        domainCommand.CreateNewDomainProject(domainProject);

        new ExampleTemplateBuilder(utilities).CreateYamlFile(scaffoldingDirectoryStore.SolutionDirectory,
            templateString);
        console.MarkupLine($"{Environment.NewLine}[bold yellow1]Your example project is project is ready![/]");

        consoleWriter.StarGithubRequest();
        return 0;
    }

    private (ExampleType type, string name) RunPrompt(string projectName)
    {
        console.WriteLine();
        console.Write(new Rule("[yellow]Create an Example Project[/]").RuleStyle("grey").Centered());

        var typeString = AskExampleType();
        var exampleType = ExampleType.FromName(typeString, ignoreCase: true);
        if (string.IsNullOrEmpty(projectName))
            projectName = AskExampleProjectName();

        return (exampleType, projectName);
    }

    private string AskExampleType()
    {
        var exampleTypes = ExampleType.List.Select(e => e.Name);

        return console.Prompt(
            new SelectionPrompt<string>()
                .Title("What [green]type of example[/] do you want to create?")
                .PageSize(50)
                .AddChoices(exampleTypes)
        );
    }

    private string AskExampleProjectName()
    {
        return console.Ask<string>("What would you like to name this project (e.g. [green]MyExampleProject[/])?");
    }

    private static string GetExampleDomain(string name, ExampleType exampleType)
    {
        if (exampleType == ExampleType.Basic)
            return BasicTemplate(name);
        if (exampleType == ExampleType.WithAuth)
            return AuthTemplate(name);
        if (exampleType == ExampleType.WithBus)
            return BusTemplate(name);
        if (exampleType == ExampleType.WithAuthServer)
            return AuthServerTemplate(name);
        if (exampleType == ExampleType.WithForeignKey)
            return ForeignKeyTemplate(name);
        if (exampleType == ExampleType.Complex)
            return ComplexTemplate(name);

        throw new Exception("Example type was not recognized.");
    }

    private static string ForeignKeyTemplate(string name)
    {
        return $@"DomainName: {name}
BoundedContexts:
- ProjectName: RecipeManagement
  Port: 5375
  DbContext:
   ContextName: RecipesDbContext
   DatabaseName: RecipeManagement
   Provider: Postgres
  Entities:
  - Name: Recipe
    Features:
    - Type: GetList
    - Type: GetRecord
    - Type: AddRecord
    - Type: UpdateRecord
    - Type: DeleteRecord
    Properties:
    - Name: Title
      Type: string
    - Name: Directions
      Type: string
    - Name: Author
      Relationship: manyto1
      ForeignEntityName: Author
      ForeignEntityPlural: Authors
    - Name: Ingredients
      Relationship: 1tomany
      ForeignEntityName: Ingredient
      ForeignEntityPlural: Ingredients
  - Name: Author
    Features:
    - Type: GetList
    - Type: GetAll
    - Type: GetRecord
    - Type: AddRecord
    - Type: UpdateRecord
    - Type: DeleteRecord
    Properties:
    - Name: Name
      Type: string
  - Name: Ingredient
    Features:
    - Type: GetList
    - Type: GetRecord
    - Type: AddRecord
    - Type: UpdateRecord
    - Type: DeleteRecord
    - Type: AddListByFk
      BatchPropertyName: RecipeId
      BatchPropertyType: Guid
      ParentEntity: Recipe
      ParentEntityPlural: Recipes
    Properties:
    - Name: Name
      Type: string
    - Name: Visibility
      SmartNames:
      - Public
      - Friends Only
      - Private
    - Name: Quantity
      Type: string
    - Name: Measure
      Type: string";
    }

    private static string ComplexTemplate(string name)
    {
        return $@"DomainName: {name}
BoundedContexts:
- ProjectName: RecipeManagement
  Port: 5375
  DbContext:
   ContextName: RecipesDbContext
   DatabaseName: RecipeManagement
   Provider: Postgres
  Entities:
  - Name: Recipe
    Features:
    - Type: GetList
      IsProtected: true
      PermissionName: CanReadRecipes
    - Type: GetRecord
      IsProtected: true
      PermissionName: CanReadRecipes
    - Type: AddRecord
      IsProtected: true
    - Type: UpdateRecord
      IsProtected: true
    - Type: DeleteRecord
      IsProtected: true
    - Type: Job
      Name: PerformFakeBookMigration
    Properties:
    - Name: Title
    - Name: Visibility
      SmartNames:
      - Public
      - Friends Only
      - Private
      ValueObjectName: RecipeVisibility
      ValueObjectPlural: RecipeVisibilities
    - Name: Directions
    - Name: Rating
      Type: int?
      AsValueObject: Simple
      ValueObjectName: UserRating
      ValueObjectPlural: UserRatings
    - Name: DateOfOrigin
      Type: DateOnly?
    - Name: HaveMadeItMyself
      Type: bool
    - Name: Tags
      Type: string[]
    - Name: Author
      Relationship: manyto1
      ForeignEntityName: Author
      ForeignEntityPlural: Authors
    - Name: Ingredients
      Relationship: 1tomany
      ForeignEntityName: Ingredient
      ForeignEntityPlural: Ingredients
  - Name: Author
    Features:
    - Type: GetList
    - Type: GetAll
    - Type: GetRecord
    - Type: AddRecord
    - Type: UpdateRecord
    - Type: DeleteRecord
    Properties:
    - Name: Name
      Type: string
      IsLogMasked: true
    - Name: Ownership
      AsValueObject: Percent
    - Name: PrimaryEmail
  - Name: Ingredient
    Features:
    - Type: GetList
    - Type: GetRecord
    - Type: AddRecord
    - Type: UpdateRecord
    - Type: DeleteRecord
    - Type: AddListByFk
      BatchPropertyName: RecipeId
      BatchPropertyType: Guid
      ParentEntity: Recipe
      ParentEntityPlural: Recipes
    Properties:
    - Name: Name
      Type: string
    - Name: Quantity
      Type: string
    - Name: ExpiresOn
      Type: DateTime?
    - Name: BestTimeOfDay
      Type: DateTimeOffset?
    - Name: Measure
      Type: string
    - Name: AverageCost
      AsValueObject: MonetaryAmount
  Environment:
      AuthSettings:
        Authority: http://localhost:3881/realms/DevRealm
        Audience: the_kitchen_company
        AuthorizationUrl: http://localhost:3881/realms/DevRealm/protocol/openid-connect/auth
        TokenUrl: http://localhost:3881/realms/DevRealm/protocol/openid-connect/token
        ClientId: recipe_management.swagger
        ClientSecret: 974d6f71-d41b-4601-9a7a-a33081f80687
      BrokerSettings:
        Host: localhost
        VirtualHost: /
        Username: guest
        Password: guest
  Bus:
    AddBus: true
  Producers:
  - EndpointRegistrationMethodName: ImportRecipeEndpoint
    ProducerName: ImportRecipeProducer
    ExchangeName: import-recipe
    MessageName: ImportRecipe
    DomainDirectory: Recipes
    ExchangeType: fanout
    UsesDb: true
  Consumers:
  - EndpointRegistrationMethodName: AddToBookEndpoint
    ConsumerName: AddToBook
    ExchangeName: book-additions
    QueueName: add-recipe-to-book
    MessageName: ImportRecipe
    DomainDirectory: Recipes
    ExchangeType: fanout
Messages:
- Name: ImportRecipe
  Properties:
  - Name: RecipeId
    Type: guid
AuthServer:
  Name: KeycloakPulumi
  RealmName: DevRealm
  Port: 3881
  Clients:
    - Id: recipe_management.postman.machine
      Name: RecipeManagement Postman Machine
      Secret: 974d6f71-d41b-4601-9a7a-a33081f84682
      GrantType: ClientCredentials
      BaseUrl: 'https://oauth.pstmn.io/'
      Scopes:
        - the_kitchen_company #this should match the audience scope in your boundary auth settings and swagger specs
    - Id: recipe_management.postman.code
      Name: RecipeManagement Postman Code
      Secret: 974d6f71-d41b-4601-9a7a-a33081f84680 #optional
      GrantType: Code
      BaseUrl: 'https://oauth.pstmn.io/'
      Scopes:
        - the_kitchen_company #this should match the audience scope in your boundary auth settings and swagger specs
    - Id: recipe_management.swagger
      Name: RecipeManagement Swagger
      Secret: 974d6f71-d41b-4601-9a7a-a33081f80687
      GrantType: Code
      BaseUrl: 'https://localhost:5375/'
      Scopes:
        - the_kitchen_company #this should match the audience scope in your boundary auth settings and swagger specs
    - Id: recipe_management.bff
      Name: RecipeManagement BFF
      Secret: 974d6f71-d41b-4601-9a7a-a33081f80688
      BaseUrl: 'https://localhost:4378/'
      GrantType: Code
      RedirectUris:
        - 'https://localhost:4378/*'
      AllowedCorsOrigins:
        - 'https://localhost:5375' # api 1 - recipe_management
        - 'https://localhost:4378'
      Scopes:
        - the_kitchen_company #this should match the audience scope in your boundary auth settings and swagger specs";
    }

    private static string BasicTemplate(string name)
    {
        return $@"DomainName: {name}
BoundedContexts:
- ProjectName: RecipeManagement
  Port: 5375
  DbContext:
   ContextName: RecipesDbContext
   DatabaseName: RecipeManagement
   Provider: postgres
   NamingConvention: class
  Entities:
  - Name: Recipe
    Features:
    - Type: GetList
    - Type: GetRecord
    - Type: AddRecord
    - Type: UpdateRecord
    - Type: DeleteRecord
    Properties:
    - Name: Title
      Type: string
    - Name: Directions
      Type: string
    - Name: RecipeSourceLink
      Type: string
    - Name: Description
      Type: string
    - Name: ImageLink
      Type: string
    - Name: Rating
      Type: int?
      AsValueObject: Simple
    - Name: Visibility
      SmartNames:
      - Public
      - Friends Only
      - Private
    - Name: DateOfOrigin
      Type: DateOnly?";
    }

    private static string AuthTemplate(string name)
    {
        return $@"DomainName: {name}
BoundedContexts:
- ProjectName: RecipeManagement
  Port: 5375
  DbContext:
    ContextName: RecipesDbContext
    DatabaseName: RecipeManagement
    Provider: postgres
  Entities:
  - Name: Recipe
    Features:
    - Type: GetList
      IsProtected: true
      PermissionName: CanReadRecipes
    - Type: GetRecord
      IsProtected: true
      PermissionName: CanReadRecipes
    - Type: AddRecord
      IsProtected: true
    - Type: UpdateRecord
      IsProtected: true
    - Type: DeleteRecord
      IsProtected: true
    Properties:
    - Name: Title
      Type: string
    - Name: Directions
      Type: string
    - Name: RecipeSourceLink
      Type: string
    - Name: Description
      Type: string
    - Name: ImageLink
      Type: string
    - Name: Rating
      Type: int?
      AsValueObject: Simple
    - Name: Visibility
      SmartNames:
      - Public
      - Friends Only
      - Private
    - Name: DateOfOrigin
      Type: DateOnly?
  Environment:
    AuthSettings:
      Authority: http://localhost:3881/realms/DevRealm
      Audience: the_kitchen_company
      AuthorizationUrl: http://localhost:3881/realms/DevRealm/protocol/openid-connect/auth
      TokenUrl: http://localhost:3881/realms/DevRealm/protocol/openid-connect/token
      ClientId: recipe_management.swagger
      ClientSecret: 974d6f71-d41b-4601-9a7a-a33081f80687";
    }

    private static string BusTemplate(string name)
    {
        var template = $@"DomainName: {name}
BoundedContexts:
- ProjectName: RecipeManagement
  Port: 5375
  DbContext:
   ContextName: RecipesDbContext
   DatabaseName: RecipeManagement
   Provider: Postgres
  Entities:
  - Name: Recipe
    Features:
    - Type: GetList
    - Type: GetRecord
    - Type: AddRecord
    - Type: UpdateRecord
    - Type: DeleteRecord
    Properties:
    - Name: Title
      Type: string
    - Name: Directions
      Type: string
    - Name: RecipeSourceLink
      Type: string
    - Name: Description
      Type: string
    - Name: ImageLink
      Type: string
    - Name: Rating
      Type: int?
      AsValueObject: Simple
    - Name: Visibility
      SmartNames:
      - Public
      - Friends Only
      - Private
    - Name: DateOfOrigin
      Type: DateOnly?
  Environment:
      BrokerSettings:
        Host: localhost
        VirtualHost: /
        Username: guest
        Password: guest
  Bus:
    AddBus: true
  Producers:
  - EndpointRegistrationMethodName: AddRecipeProducerEndpoint
    ProducerName: AddRecipeProducer
    ExchangeName: recipe-added
    MessageName: RecipeAdded
    DomainDirectory: Recipes
    ExchangeType: fanout
    UsesDb: true
  Consumers:
  - EndpointRegistrationMethodName: AddToBookEndpoint
    ConsumerName: AddToBook
    ExchangeName: book-additions
    QueueName: add-recipe-to-book
    MessageName: RecipeAdded
    DomainDirectory: Recipes
    ExchangeType: fanout
Messages:
- Name: RecipeAdded
  Properties:
  - Name: RecipeId
    Type: guid";

        return template;
    }

    private static string AuthServerTemplate(string name)
    {
        return $@"DomainName: {name}
BoundedContexts:
- ProjectName: RecipeManagement
  Port: 5375
  DbContext:
    ContextName: RecipesDbContext
    DatabaseName: RecipeManagement
    Provider: Postgres
  Entities:
  - Name: Recipe
    Features:
    - Type: GetList
      IsProtected: true
      PermissionName: CanReadRecipes
    - Type: GetRecord
      IsProtected: true
      PermissionName: CanReadRecipes
    - Type: AddRecord
      IsProtected: true
    - Type: UpdateRecord
      IsProtected: true
    - Type: DeleteRecord
      IsProtected: true
    Properties:
    - Name: Title
      Type: string
    - Name: Directions
      Type: string
    - Name: RecipeSourceLink
      Type: string
    - Name: Description
      Type: string
    - Name: ImageLink
      Type: string
    - Name: Rating
      Type: int?
      AsValueObject: Simple
    - Name: Visibility
      SmartNames:
      - Public
      - Friends Only
      - Private
    - Name: DateOfOrigin
      Type: DateOnly?
  Environment:
    AuthSettings:
      Authority: http://localhost:3881/realms/DevRealm
      Audience: the_kitchen_company
      AuthorizationUrl: http://localhost:3881/realms/DevRealm/protocol/openid-connect/auth
      TokenUrl: http://localhost:3881/realms/DevRealm/protocol/openid-connect/token
      ClientId: recipe_management.swagger
      ClientSecret: 974d6f71-d41b-4601-9a7a-a33081f80687
AuthServer:
  Name: KeycloakPulumi
  RealmName: DevRealm
  Port: 3881
  Clients:
    - Id: recipe_management.postman.machine
      Name: RecipeManagement Postman Machine
      Secret: 974d6f71-d41b-4601-9a7a-a33081f84682
      GrantType: ClientCredentials
      BaseUrl: 'https://oauth.pstmn.io/'
      Scopes:
        - the_kitchen_company #this should match the audience scope in your boundary auth settings and swagger specs
    - Id: recipe_management.postman.code
      Name: RecipeManagement Postman Code
      Secret: 974d6f71-d41b-4601-9a7a-a33081f84680 #optional
      GrantType: Code
      BaseUrl: 'https://oauth.pstmn.io/'
      Scopes:
        - the_kitchen_company #this should match the audience scope in your boundary auth settings and swagger specs
    - Id: recipe_management.swagger
      Name: RecipeManagement Swagger
      Secret: 974d6f71-d41b-4601-9a7a-a33081f80687
      GrantType: Code
      BaseUrl: 'https://localhost:5375/'
      Scopes:
        - the_kitchen_company #this should match the audience scope in your boundary auth settings and swagger specs
    - Id: recipe_management.bff
      Name: RecipeManagement BFF
      Secret: 974d6f71-d41b-4601-9a7a-a33081f80688
      BaseUrl: 'https://localhost:4378/'
      GrantType: Code
      RedirectUris:
        - 'https://localhost:4378/*'
      AllowedCorsOrigins:
        - 'https://localhost:5375' # api 1 - recipe_management
        - 'https://localhost:4378'
      Scopes:
        - the_kitchen_company #this should match the audience scope in your boundary auth settings and swagger specs
";
    }
}