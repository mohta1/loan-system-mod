using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoanSystem.Contracts;
using LoanSystem.Modules.IdentityAccess.Domain;
using LoanSystem.Modules.IdentityAccess.Infrastructure;
using LoanSystem.Modules.LoanProducts.Application;
using LoanSystem.Modules.LoanProducts.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoanSystem.IntegrationTests;

[Collection(IdentitySqlTestGroup.Name)]
public sealed class LoanProductsIntegrationTests(IdentitySqlFixture fixture)
{
    [Fact]
    public async Task Create_draft_persists_complete_aggregate_in_a_fresh_scope()
    {
        using var client = fixture.Factory.CreateClient(new() { HandleCookies = true });
        await Login(client);
        var productId = (await CreateProduct(client)).GetProperty("loanProductId").GetGuid();
        var input = new
        {
            maximumAmount = 30000m,
            currency = "OMR",
            deductionPercentage = 25.5m,
            financingTypes = new[] { "Purchase Existing House", "Build New House" },
            eligibilityConfiguration = new
            {
                requiredNationality = "Configured nationality",
                maximumApplicationCount = 2,
                rankGradeAmountRules = new[] { new { rankGrade = "Grade A", maximumAmount = 10000m }, new { rankGrade = "Grade B", maximumAmount = 20000m } },
                term = new { maximumTermMonths = 120, dueDateRule = "Configured term rule" }
            },
            effectiveFrom = new DateOnly(2035, 1, 1),
            effectiveTo = (DateOnly?)null
        };

        var response = await client.PostAsJsonAsync($"/api/v1/loan-products/{productId}/versions", input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await Read(response);
        Assert.Equal(1, created.GetProperty("versionNumber").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(created.GetProperty("eTag").GetString()));

        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LoanProductsDbContext>();
        var persisted = await database.Versions.AsNoTracking().Include(value => value.FinancingTypes)
            .SingleAsync(value => value.Id == created.GetProperty("versionId").GetGuid());
        Assert.Equal(productId, persisted.LoanProductId);
        Assert.Equal(LoanSystem.Modules.LoanProducts.Domain.LoanProductVersionStatus.Draft, persisted.Status);
        Assert.NotEmpty(persisted.RowVersion);
        Assert.Equal(["Build New House", "Purchase Existing House"], persisted.FinancingTypes.Select(value => value.Value).Order().ToArray());
        Assert.Equal("Configured nationality", persisted.EligibilityConfiguration.RequiredNationality);
        Assert.Equal(2, persisted.EligibilityConfiguration.MaximumApplicationCount);
        Assert.Equal(["Grade A", "Grade B"], persisted.EligibilityConfiguration.RankGradeAmountRules.Select(value => value.RankGrade).ToArray());
        Assert.Equal(120, persisted.EligibilityConfiguration.Term.MaximumTermMonths);
        Assert.Equal("Configured term rule", persisted.EligibilityConfiguration.Term.DueDateRule);
    }

    [Fact]
    public async Task Failed_draft_validation_rolls_back_owned_transaction_before_next_operation()
    {
        using var client = fixture.Factory.CreateClient(new() { HandleCookies = true });
        await Login(client);
        var productId = (await CreateProduct(client)).GetProperty("loanProductId").GetGuid();
        using var scope = fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<LoanProductService>();
        var database = scope.ServiceProvider.GetRequiredService<LoanProductsDbContext>();
        var eligibility = new EligibilityInput("Configured nationality", 2, [new("Grade A", 5000)], new(120, "Configured term rule"));
        var invalid = new VersionInput(0, "OMR", 25, ["Configured type"], eligibility, new(2036, 1, 1), null);
        await Assert.ThrowsAsync<LoanSystem.Modules.LoanProducts.Domain.LoanProductValidationException>(() => service.CreateDraftAsync(productId, invalid, default));
        Assert.Null(database.Database.CurrentTransaction);

        var valid = invalid with { MaximumAmount = 10000 };
        var created = await service.CreateDraftAsync(productId, valid, default);
        Assert.NotNull(created);
        Assert.Equal(1, created.VersionNumber);
        Assert.Null(database.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Real_sql_lifecycle_immutability_overlap_availability_and_contract()
    {
        using var client = fixture.Factory.CreateClient(new() { HandleCookies = true });
        await Login(client);
        var product = await CreateProduct(client);
        var productId = product.GetProperty("loanProductId").GetGuid();
        var first = await CreateDraft(client, productId, Input(new(2030, 1, 1), new(2030, 12, 31), 10000));
        var firstId = first.GetProperty("versionId").GetGuid();
        var firstEtag = first.GetProperty("eTag").GetString()!;

        var updated = await Send(client, HttpMethod.Put, $"/api/v1/loan-products/{productId}/versions/{firstId}", firstEtag, Input(new(2030, 1, 1), new(2030, 12, 31), 12000));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var currentEtag = (await Read(updated)).GetProperty("eTag").GetString()!;
        var stale = await Send(client, HttpMethod.Put, $"/api/v1/loan-products/{productId}/versions/{firstId}", firstEtag, Input(new(2030, 1, 1), new(2030, 12, 31), 13000));
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("loanProducts.concurrencyConflict", (await Read(stale)).GetProperty("errorCode").GetString());

        var publishedResponse = await Send(client, HttpMethod.Post, $"/api/v1/loan-products/{productId}/versions/{firstId}/publish", currentEtag, new { });
        Assert.Equal(HttpStatusCode.OK, publishedResponse.StatusCode);
        var published = await Read(publishedResponse);
        var publishedAt = published.GetProperty("publishedAtUtc").GetDateTimeOffset();
        var immutable = await Send(client, HttpMethod.Put, $"/api/v1/loan-products/{productId}/versions/{firstId}", currentEtag, Input(new(2030, 1, 1), new(2030, 12, 31), 1));
        Assert.Equal(HttpStatusCode.Conflict, immutable.StatusCode);
        Assert.Equal("loanProducts.versionImmutable", (await Read(immutable)).GetProperty("errorCode").GetString());
        var repeated = await Send(client, HttpMethod.Post, $"/api/v1/loan-products/{productId}/versions/{firstId}/publish", published.GetProperty("eTag").GetString()!, new { });
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);

        var second = await CreateDraft(client, productId, Input(new(2030, 12, 31), null, 15000));
        Assert.Equal(2, second.GetProperty("versionNumber").GetInt32());
        var secondId = second.GetProperty("versionId").GetGuid();
        var overlap = await Send(client, HttpMethod.Post, $"/api/v1/loan-products/{productId}/versions/{secondId}/publish", second.GetProperty("eTag").GetString()!, new { });
        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
        Assert.Equal("loanProducts.effectivePeriodConflict", (await Read(overlap)).GetProperty("errorCode").GetString());
        var secondUpdated = await Send(client, HttpMethod.Put, $"/api/v1/loan-products/{productId}/versions/{secondId}", second.GetProperty("eTag").GetString()!, Input(new(2031, 1, 1), null, 15000));
        Assert.Equal(HttpStatusCode.OK, secondUpdated.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Post, $"/api/v1/loan-products/{productId}/versions/{secondId}/publish", (await Read(secondUpdated)).GetProperty("eTag").GetString()!, new { })).StatusCode);

        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LoanProductsDbContext>();
        Assert.Empty(await database.AvailableAsync(new(2029, 12, 31), default));
        Assert.Contains(await database.AvailableAsync(new(2030, 1, 1), default), value => value.VersionId == firstId);
        Assert.Contains(await database.AvailableAsync(new(2030, 6, 1), default), value => value.VersionId == firstId);
        Assert.Contains(await database.AvailableAsync(new(2030, 12, 31), default), value => value.VersionId == firstId);
        Assert.DoesNotContain(await database.AvailableAsync(new(2031, 1, 1), default), value => value.VersionId == firstId);
        var persistedFirst = await database.Versions.AsNoTracking().Include(value => value.FinancingTypes).SingleAsync(value => value.Id == firstId);
        Assert.Equal(12000, persistedFirst.MaximumAmount);
        Assert.Equal(publishedAt, persistedFirst.PublishedAtUtc);
        Assert.Equal(2, persistedFirst.FinancingTypes.Count);
        Assert.Equal("Configured nationality", persistedFirst.EligibilityConfiguration.RequiredNationality);

        var module = scope.ServiceProvider.GetRequiredService<ILoanProductsModule>();
        Assert.Equal(LoanProductVersionLookupStatus.Available, (await module.GetVersionAsync(firstId, new(2030, 6, 1))).Status);
        Assert.Equal(LoanProductVersionLookupStatus.OutsideEffectivePeriod, (await module.GetVersionAsync(firstId, new(2031, 1, 1))).Status);
        Assert.Equal(LoanProductVersionLookupStatus.NotFound, (await module.GetVersionAsync(Guid.NewGuid(), new(2030, 1, 1))).Status);

        var detail = await client.GetAsync($"/api/v1/loan-products/{productId}");
        var productEtag = detail.Headers.ETag!.Tag.Trim('"');
        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Post, $"/api/v1/loan-products/{productId}/deactivate", productEtag, new { })).StatusCode);
        Assert.Empty(await database.AvailableAsync(new(2030, 6, 1), default));
        Assert.Equal(LoanProductVersionLookupStatus.ProductInactive, (await module.GetVersionAsync(firstId, new(2030, 6, 1))).Status);
    }

    [Fact]
    public async Task Concurrent_draft_creation_allocates_unique_monotonic_numbers()
    {
        using var client = fixture.Factory.CreateClient(new() { HandleCookies = true });
        await Login(client);
        var productId = (await CreateProduct(client)).GetProperty("loanProductId").GetGuid();
        var requests = Enumerable.Range(0, 2).Select(index => client.PostAsJsonAsync($"/api/v1/loan-products/{productId}/versions", Input(new(2040 + index, 1, 1), new(2040 + index, 12, 31), 10000 + index))).ToArray();
        var responses = await Task.WhenAll(requests);
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        var numbers = await Task.WhenAll(responses.Select(async response => (await Read(response)).GetProperty("versionNumber").GetInt32()));
        Assert.Equal([1, 2], numbers.Order().ToArray());
    }

    [Fact]
    public async Task Publish_requires_current_etag_and_concurrent_publication_is_controlled()
    {
        using var firstClient = fixture.Factory.CreateClient(new() { HandleCookies = true });
        using var secondClient = fixture.Factory.CreateClient(new() { HandleCookies = true });
        await Login(firstClient);
        await Login(secondClient);
        var productId = (await CreateProduct(firstClient)).GetProperty("loanProductId").GetGuid();
        var draft = await CreateDraft(firstClient, productId, Input(new(2045, 1, 1), new(2045, 12, 31), 10000));
        var versionId = draft.GetProperty("versionId").GetGuid();
        var staleEtag = draft.GetProperty("eTag").GetString()!;
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await firstClient.PostAsync($"/api/v1/loan-products/{productId}/versions/{versionId}/publish", null)).StatusCode);
        var edited = await Send(firstClient, HttpMethod.Put, $"/api/v1/loan-products/{productId}/versions/{versionId}", staleEtag, Input(new(2045, 1, 1), new(2045, 12, 31), 11000));
        var currentEtag = (await Read(edited)).GetProperty("eTag").GetString()!;
        var stalePublish = await Send(firstClient, HttpMethod.Post, $"/api/v1/loan-products/{productId}/versions/{versionId}/publish", staleEtag, new { });
        Assert.Equal(HttpStatusCode.PreconditionFailed, stalePublish.StatusCode);
        Assert.Equal("loanProducts.concurrencyConflict", (await Read(stalePublish)).GetProperty("errorCode").GetString());

        var attempts = await Task.WhenAll(
            Send(firstClient, HttpMethod.Post, $"/api/v1/loan-products/{productId}/versions/{versionId}/publish", currentEtag, new { }),
            Send(secondClient, HttpMethod.Post, $"/api/v1/loan-products/{productId}/versions/{versionId}/publish", currentEtag, new { }));
        Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(attempts, response => response.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict);
        Assert.DoesNotContain(attempts, response => response.StatusCode == HttpStatusCode.InternalServerError);
        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LoanProductsDbContext>();
        var publishedAt = await database.Versions.Where(value => value.Id == versionId).Select(value => value.PublishedAtUtc).SingleAsync();
        Assert.NotNull(publishedAt);
        var repeated = await Send(firstClient, HttpMethod.Post, $"/api/v1/loan-products/{productId}/versions/{versionId}/publish", (await Read(attempts.Single(response => response.StatusCode == HttpStatusCode.OK))).GetProperty("eTag").GetString()!, new { });
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        Assert.Equal(publishedAt, await database.Versions.Where(value => value.Id == versionId).Select(value => value.PublishedAtUtc).SingleAsync());
    }

    [Fact]
    public async Task Anonymous_requests_are_rejected_and_migration_shape_is_present()
    {
        using var anonymous = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/loan-products")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/loan-products/available")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/loan-products", new { name = "Denied" })).StatusCode);
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT COUNT(*) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name='loan_products'", connection);
        Assert.Equal(3, Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
        command.CommandText = """
            SELECT COUNT(*)
            FROM sys.foreign_keys fk
            JOIN sys.tables child ON child.object_id = fk.parent_object_id
            JOIN sys.schemas child_schema ON child_schema.schema_id = child.schema_id
            JOIN sys.tables principal ON principal.object_id = fk.referenced_object_id
            JOIN sys.schemas principal_schema ON principal_schema.schema_id = principal.schema_id
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            JOIN sys.columns child_column ON child_column.object_id = child.object_id AND child_column.column_id = fkc.parent_column_id
            JOIN sys.columns principal_column ON principal_column.object_id = principal.object_id AND principal_column.column_id = fkc.referenced_column_id
            WHERE child_schema.name = 'loan_products' AND principal_schema.name = 'loan_products'
              AND ((fk.name = 'FK_versions_products' AND child.name = 'loan_product_versions' AND child_column.name = 'loan_product_id' AND principal.name = 'loan_products' AND principal_column.name = 'loan_product_id' AND fk.delete_referential_action = 0)
                OR (fk.name = 'FK_financing_types_versions' AND child.name = 'loan_product_financing_types' AND child_column.name = 'version_id' AND principal.name = 'loan_product_versions' AND principal_column.name = 'version_id' AND fk.delete_referential_action = 1))
            """;
        Assert.Equal(2, Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
        command.CommandText = """
            SELECT
              (SELECT COUNT(*) FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name='loan_products' AND t.name='loan_product_versions' AND i.name='IX_versions_product_number' AND i.is_unique=1
                AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.is_included_column=0)=2
                AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name='loan_product_id')
                AND EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name='version_number')),
              (SELECT COUNT(*) FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name='loan_products' AND c.name='row_version' AND c.system_type_id=189 AND t.name IN ('loan_products','loan_product_versions')),
              (SELECT COUNT(*) FROM sys.columns c JOIN sys.types ty ON ty.user_type_id=c.user_type_id JOIN sys.tables t ON t.object_id=c.object_id JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name='loan_products' AND t.name='loan_product_versions' AND ((c.name='maximum_amount' AND ty.name='decimal' AND c.precision=19 AND c.scale=4) OR (c.name='deduction_percentage' AND ty.name='decimal' AND c.precision=9 AND c.scale=4)))
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal(2, reader.GetInt32(1));
        Assert.Equal(2, reader.GetInt32(2));
    }

    [Fact]
    public async Task Effective_permissions_are_independent_for_read_manage_publish_and_status()
    {
        using var administrator = fixture.Factory.CreateClient(new() { HandleCookies = true });
        await Login(administrator);
        var product = await CreateProduct(administrator);
        var productId = product.GetProperty("loanProductId").GetGuid();
        var productEtag = product.GetProperty("eTag").GetString()!;
        var draft = await CreateDraft(administrator, productId, Input(new(2050, 1, 1), new(2050, 12, 31), 10000));
        var versionId = draft.GetProperty("versionId").GetGuid();

        using var read = await PermissionClient(administrator, "loanProducts.read");
        Assert.Equal(HttpStatusCode.OK, (await read.GetAsync("/api/v1/loan-products")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await read.GetAsync("/api/v1/loan-products/available")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await read.GetAsync($"/api/v1/loan-products/{productId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await read.PostAsJsonAsync("/api/v1/loan-products", new { name = "Denied" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await read.PostAsync($"/api/v1/loan-products/{productId}/versions/{versionId}/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(read, HttpMethod.Post, $"/api/v1/loan-products/{productId}/deactivate", productEtag, new { })).StatusCode);

        using var manage = await PermissionClient(administrator, "loanProducts.manage");
        Assert.Equal(HttpStatusCode.Created, (await manage.PostAsJsonAsync("/api/v1/loan-products", new { name = $"Managed {Guid.NewGuid():N}" })).StatusCode);
        var managedDraft = await manage.PostAsJsonAsync($"/api/v1/loan-products/{productId}/versions", Input(new(2051, 1, 1), new(2051, 12, 31), 11000));
        Assert.Equal(HttpStatusCode.Created, managedDraft.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manage.PostAsync($"/api/v1/loan-products/{productId}/versions/{versionId}/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(manage, HttpMethod.Post, $"/api/v1/loan-products/{productId}/deactivate", productEtag, new { })).StatusCode);

        using var publish = await PermissionClient(administrator, "loanProducts.publish");
        Assert.Equal(HttpStatusCode.OK, (await Send(publish, HttpMethod.Post, $"/api/v1/loan-products/{productId}/versions/{versionId}/publish", draft.GetProperty("eTag").GetString()!, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await publish.PostAsJsonAsync("/api/v1/loan-products", new { name = "Denied" })).StatusCode);

        using var status = await PermissionClient(administrator, "loanProducts.manageStatus");
        Assert.Equal(HttpStatusCode.OK, (await Send(status, HttpMethod.Post, $"/api/v1/loan-products/{productId}/deactivate", productEtag, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await status.PostAsync($"/api/v1/loan-products/{productId}/versions/{versionId}/publish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await status.PostAsJsonAsync($"/api/v1/loan-products/{productId}/versions", Input(new(2052, 1, 1), null, 12000))).StatusCode);
    }

    private static object Input(DateOnly from, DateOnly? to, decimal amount) => new
    {
        maximumAmount = amount,
        currency = "OMR",
        deductionPercentage = 25.5m,
        financingTypes = new[] { "Purchase Existing House", "Build New House" },
        eligibilityConfiguration = new
        {
            requiredNationality = "Configured nationality",
            maximumApplicationCount = 2,
            rankGradeAmountRules = new[] { new { rankGrade = "Configured grade", maximumAmount = amount / 2 } },
            term = new { maximumTermMonths = 120, dueDateRule = "Configured term rule" }
        },
        effectiveFrom = from,
        effectiveTo = to
    };

    private static async Task<JsonElement> CreateProduct(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/loan-products", new { name = $"Integration {Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Read(response);
    }

    private static async Task<JsonElement> CreateDraft(HttpClient client, Guid productId, object input)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/loan-products/{productId}/versions", input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Read(response);
    }

    private static async Task Login(HttpClient client) => Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/login", new { username = IdentityAccessIntegrationTests.Admin, password = IdentityAccessIntegrationTests.Password })).StatusCode);

    private async Task<HttpClient> PermissionClient(HttpClient administrator, string permissionKey)
    {
        var suffix = Guid.NewGuid().ToString("N");
        Guid roleId;
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>();
            var permission = await database.Permissions.SingleAsync(value => value.Key == permissionKey);
            var role = new Role { Id = Guid.NewGuid(), Name = $"Test {permissionKey} {suffix}" };
            database.Roles.Add(role);
            database.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            await database.SaveChangesAsync();
            roleId = role.Id;
        }

        const string password = "Disposable_permission_password_123!";
        var username = $"loan-product-{suffix}";
        var createdResponse = await administrator.PostAsJsonAsync("/api/v1/users", new { username, displayName = username, password });
        var created = await Read(createdResponse);
        var assigned = await Send(administrator, HttpMethod.Put, $"/api/v1/users/{created.GetProperty("userId").GetGuid()}/roles", created.GetProperty("eTag").GetString()!, new { roleIds = new[] { roleId } });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var client = fixture.Factory.CreateClient(new() { HandleCookies = true });
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password })).StatusCode);
        return client;
    }
    private static async Task<JsonElement> Read(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string etag, object body) { using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) }; request.Headers.TryAddWithoutValidation("If-Match", $"\"{etag}\""); return await client.SendAsync(request); }
}
