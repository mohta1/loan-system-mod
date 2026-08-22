using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LoanSystem.Modules.Documents.Application;
using LoanSystem.Modules.Documents.Infrastructure;
using LoanSystem.Modules.Documents.Presentation;
using Microsoft.EntityFrameworkCore;
using LoanSystem.Contracts;

namespace LoanSystem.Modules.Documents;

/// <summary>Defines the composition entry points for the Documents module.</summary>
public static class ModuleRegistration
{
    public static IServiceCollection AddDocumentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var connection = configuration.GetConnectionString("LoanSystem") ?? throw new InvalidOperationException("ConnectionStrings:LoanSystem is required.");
        services.Configure<FileStorageOptions>(configuration.GetSection("FileStorage"));
        services.AddDbContext<DocumentsDbContext>(options => options.UseSqlServer(connection));
        services.AddScoped<IDocumentRepository>(x => x.GetRequiredService<DocumentsDbContext>());
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IDocumentAccessAuthorizer, UploaderDocumentAccessAuthorizer>();
        services.AddScoped<DocumentService>();
        services.AddScoped<IImportSourceDocumentStore, ImportSourceDocumentStore>();
        return services;
    }

    public static IEndpointRouteBuilder MapDocumentsModuleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        DocumentEndpoints.Map(endpoints); return endpoints;
    }

    public static async Task InitializeDocumentsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    { using var scope = services.CreateScope(); await scope.ServiceProvider.GetRequiredService<DocumentsDbContext>().Database.MigrateAsync(cancellationToken); }
}

internal sealed class ImportSourceDocumentStore(DocumentService documents) : IImportSourceDocumentStore
{
    public async Task<Guid> StoreAsync(string fileName, string contentType, long length, Stream content, Guid uploadedBy, CancellationToken cancellationToken) =>
        (await documents.UploadAsync(fileName, contentType, length, content, uploadedBy, cancellationToken)).DocumentId;
}
