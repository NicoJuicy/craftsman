namespace Craftsman.Builders.Features;

using System;
using Domain;
using Helpers;
using Services;

public class ProducerBuilder(ICraftsmanUtilities utilities)
{
    public void CreateProducerFeature(string solutionDirectory, string srcDirectory, Producer producer, string projectBaseName)
    {
        var classPath = ClassPathHelper.ProducerFeaturesClassPath(srcDirectory, $"{producer.ProducerName}.cs", producer.DomainDirectory, projectBaseName);
        var fileText = GetProducerRegistration(classPath.ClassNamespace, producer, solutionDirectory, srcDirectory, projectBaseName);
        utilities.CreateFile(classPath, fileText);
    }

    public string GetProducerRegistration(string classNamespace, Producer producer, string solutionDirectory, string srcDirectory, string projectBaseName)
    {
        var context = utilities.GetDbContext(srcDirectory, projectBaseName);
        var contextClassPath = ClassPathHelper.DbContextClassPath(srcDirectory, "", projectBaseName);
        var dbProp = producer.UsesDb ? @$"{context} dbContext, " : "";
        var contextUsing = producer.UsesDb ? $@"
using {contextClassPath.ClassNamespace};" : "";

        var messagesClassPath = ClassPathHelper.MessagesClassPath(solutionDirectory, "");
        
        var commandName = $"{producer.ProducerName}Command";

        return @$"namespace {classNamespace};

using {messagesClassPath.ClassNamespace};
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;{contextUsing}

public static class {producer.ProducerName}
{{
    public sealed record {commandName} : IRequest;

    public sealed class Handler({dbProp}IPublishEndpoint publishEndpoint) : IRequestHandler<{commandName}>
    {{

        public async Task Handle({commandName} request, CancellationToken cancellationToken)
        {{
            var message = new {FileNames.MessageClassName(producer.MessageName)}
            {{
                // map content to message here
            }};
            await publishEndpoint.Publish<{FileNames.MessageInterfaceName(producer.MessageName)}>(message, cancellationToken);
        }}
    }}
}}";
    }
}
