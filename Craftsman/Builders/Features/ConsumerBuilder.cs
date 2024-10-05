namespace Craftsman.Builders.Features;

using System;
using Domain;
using Helpers;
using Services;

public class ConsumerBuilder(ICraftsmanUtilities utilities)
{
    public void CreateConsumerFeature(string solutionDirectory, string srcDirectory, Consumer consumer, string projectBaseName)
    {
        var classPath = ClassPathHelper.ConsumerFeaturesClassPath(srcDirectory, $"{consumer.ConsumerName}.cs", consumer.DomainDirectory, projectBaseName);
        var fileText = GetDirectOrTopicConsumerRegistration(classPath.ClassNamespace, consumer, solutionDirectory, srcDirectory, projectBaseName);
        utilities.CreateFile(classPath, fileText);
    }

    public string GetDirectOrTopicConsumerRegistration(string classNamespace, Consumer consumer, string solutionDirectory, string srcDirectory, string projectBaseName)
    {
        var context = utilities.GetDbContext(srcDirectory, projectBaseName);
        var contextClassPath = ClassPathHelper.DbContextClassPath(srcDirectory, "", projectBaseName);
        var dbProp = consumer.UsesDb ? @$"{context} dbContext" : "";
        var contextUsing = consumer.UsesDb ? $@"
using {contextClassPath.ClassNamespace};" : "";

        var messagesClassPath = ClassPathHelper.MessagesClassPath(solutionDirectory, "");
        return @$"namespace {classNamespace};

using MassTransit;
using {messagesClassPath.ClassNamespace};
using System.Threading.Tasks;{contextUsing}

public sealed class {consumer.ConsumerName}({dbProp}) : IConsumer<{FileNames.MessageInterfaceName(consumer.MessageName)}>
{{
    public Task Consume(ConsumeContext<{FileNames.MessageInterfaceName(consumer.MessageName)}> context)
    {{
        // do work here

        return Task.CompletedTask;
    }}
}}";
    }
}
