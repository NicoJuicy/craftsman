﻿namespace Craftsman.Builders;

using Domain;
using Helpers;
using MediatR;
using Services;

public static class DatabaseEntityConfigUserBuilder
{
    public class Command : IRequest<bool>
    {
    }

    public class Handler(
        ICraftsmanUtilities utilities,
        IScaffoldingDirectoryStore scaffoldingDirectoryStore)
        : IRequestHandler<Command, bool>
    {
        public Task<bool> Handle(Command request, CancellationToken cancellationToken)
        {
            var entityName = "User";
            var classPath = ClassPathHelper.DatabaseConfigClassPath(scaffoldingDirectoryStore.SrcDirectory, 
                $"{FileNames.GetDatabaseEntityConfigName(entityName)}.cs",
                scaffoldingDirectoryStore.ProjectBaseName);
            var fileText = GetFileText(classPath.ClassNamespace, entityName);
            utilities.CreateFile(classPath, fileText);
            return Task.FromResult(true);
        }
        private string GetFileText(string classNamespace, string entityName)
        {
            return @$"namespace {classNamespace};

using Domain.Users;
using Domain.Emails;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class {FileNames.GetDatabaseEntityConfigName(entityName)} : IEntityTypeConfiguration<{entityName}>
{{
    /// <summary>
    /// The database configuration for Users. 
    /// </summary>
    public void Configure(EntityTypeBuilder<User> builder)
    {{
        // Relationship Marker -- Deleting or modifying this comment could cause incomplete relationship scaffolding

        builder.Property(x => x.Email)
            .HasConversion(x => x.Value, x => new Email(x))
            .HasColumnName(""email"");
    }}
}}";
        }
    }
    
}
