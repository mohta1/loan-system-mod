using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LoanSystem.Modules.IdentityAccess.Domain;
using LoanSystem.Modules.IdentityAccess.Infrastructure;
using LoanSystem.Modules.LoanOrigination.Domain;
using LoanSystem.Modules.LoanOrigination.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoanSystem.IntegrationTests;

[Collection(IdentitySqlTestGroup.Name)]
public sealed class LoanOriginationIntegrationTests(IdentitySqlFixture fixture)
{
    static readonly string[] FinancingTypes = ["Build", "Renovate"];
    [Fact]
    public async Task Create_get_search_edit_and_stale_concurrency_use_real_http_and_sql()
    {
        using var client = await Administrator();
        var setup = await SetupAvailableVersion(client);
        var createdResponse = await client.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup));
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var originalTag = createdResponse.Headers.ETag!.Tag.Trim('"');
        var created = await Read(createdResponse);
        var applicationId = created.GetProperty("loanApplicationId").GetGuid();
        Assert.Equal("Draft", created.GetProperty("status").GetString());
        Assert.Equal(setup.BorrowerName, created.GetProperty("borrowerSnapshot").GetProperty("fullName").GetString());
        Assert.Equal(setup.ProductName, created.GetProperty("productSnapshot").GetProperty("productName").GetString());

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/loan-applications/{applicationId}")).StatusCode);
        var defaults = await Read(await client.GetAsync("/api/v1/loan-applications"));
        Assert.Equal(1, defaults.GetProperty("pageNumber").GetInt32());
        Assert.Equal(25, defaults.GetProperty("pageSize").GetInt32());
        foreach (var query in new[] { $"loanApplicationId={applicationId}", $"borrowerId={setup.BorrowerId}", $"loanProductId={setup.ProductId}", "status=Draft" })
            Assert.Contains((await Read(await client.GetAsync($"/api/v1/loan-applications?{query}"))).GetProperty("items").EnumerateArray(), item => item.GetProperty("loanApplicationId").GetGuid() == applicationId);
        foreach (var query in new[] { "pageNumber=0&pageSize=25", "pageNumber=1&pageSize=101" })
        {
            var invalid = await client.GetAsync($"/api/v1/loan-applications?{query}");
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal("loanApplications.invalidPagination", (await Read(invalid)).GetProperty("errorCode").GetString());
        }

        var updatedResponse = await Send(client, HttpMethod.Put, $"/api/v1/loan-applications/{applicationId}", originalTag, new { requestedAmount = 400m, financingType = "Renovate" });
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        Assert.NotEqual(originalTag, updatedResponse.Headers.ETag!.Tag.Trim('"'));
        var updated = await Read(updatedResponse);
        Assert.Equal(400m, updated.GetProperty("requestedAmount").GetDecimal());
        Assert.Equal(created.GetProperty("borrowerSnapshot").GetRawText(), updated.GetProperty("borrowerSnapshot").GetRawText());
        var stale = await Send(client, HttpMethod.Put, $"/api/v1/loan-applications/{applicationId}", originalTag, new { requestedAmount = 300m, financingType = "Build" });
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("loanApplications.concurrencyConflict", (await Read(stale)).GetProperty("errorCode").GetString());
        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LoanOriginationDbContext>();
        Assert.Equal(400m, (await database.LoanApplications.AsNoTracking().SingleAsync(value => value.Id == applicationId)).RequestedAmount);
    }

    [Fact]
    public async Task Creation_rejects_unavailable_references_types_and_amounts_with_stable_codes()
    {
        using var client = await Administrator();
        var setup = await SetupAvailableVersion(client);
        await Rejected(client, new { borrowerId = Guid.NewGuid(), loanProductVersionId = setup.VersionId, requestedAmount = 1m, financingType = "Build" }, "loanApplications.borrowerNotFound");
        var borrowerDetail = await client.GetAsync($"/api/v1/borrowers/{setup.BorrowerId}");
        await Send(client, HttpMethod.Post, $"/api/v1/borrowers/{setup.BorrowerId}/deactivate", borrowerDetail.Headers.ETag!.Tag.Trim('"'), new { });
        await Rejected(client, ApplicationInput(setup), "loanApplications.borrowerInactive");
        await Send(client, HttpMethod.Post, $"/api/v1/borrowers/{setup.BorrowerId}/activate", (await client.GetAsync($"/api/v1/borrowers/{setup.BorrowerId}")).Headers.ETag!.Tag.Trim('"'), new { });
        await Rejected(client, new { borrowerId = setup.BorrowerId, loanProductVersionId = Guid.NewGuid(), requestedAmount = 1m, financingType = "Build" }, "loanApplications.productVersionNotFound");
        var draft = await CreateDraft(client, setup.ProductId, DateOnly.FromDateTime(DateTime.UtcNow));
        await Rejected(client, new { borrowerId = setup.BorrowerId, loanProductVersionId = draft.VersionId, requestedAmount = 1m, financingType = "Build" }, "loanApplications.productVersionUnavailable");
        var futureProduct = await Read(await client.PostAsJsonAsync("/api/v1/loan-products", new { name = $"Future {Guid.NewGuid():N}" }));
        var futureProductId = futureProduct.GetProperty("loanProductId").GetGuid();
        var future = await CreateDraft(client, futureProductId, DateOnly.FromDateTime(DateTime.UtcNow).AddYears(2));
        await Publish(client, futureProductId, future);
        await Rejected(client, new { borrowerId = setup.BorrowerId, loanProductVersionId = future.VersionId, requestedAmount = 1m, financingType = "Build" }, "loanApplications.productVersionUnavailable");
        await Rejected(client, new { borrowerId = setup.BorrowerId, loanProductVersionId = setup.VersionId, requestedAmount = 1m, financingType = "Other" }, "loanApplications.invalidFinancingType", HttpStatusCode.BadRequest);
        await Rejected(client, new { borrowerId = setup.BorrowerId, loanProductVersionId = setup.VersionId, requestedAmount = 0m, financingType = "Build" }, "loanApplications.invalidRequestedAmount", HttpStatusCode.BadRequest);
        await Rejected(client, new { borrowerId = setup.BorrowerId, loanProductVersionId = setup.VersionId, requestedAmount = 1001m, financingType = "Build" }, "loanApplications.invalidRequestedAmount", HttpStatusCode.BadRequest);
        var product = await client.GetAsync($"/api/v1/loan-products/{setup.ProductId}");
        await Send(client, HttpMethod.Post, $"/api/v1/loan-products/{setup.ProductId}/deactivate", product.Headers.ETag!.Tag.Trim('"'), new { });
        await Rejected(client, ApplicationInput(setup), "loanApplications.productVersionUnavailable");
    }

    [Fact]
    public async Task Historical_snapshots_do_not_follow_borrower_master_changes()
    {
        using var client = await Administrator();
        var setup = await SetupAvailableVersion(client);
        var created = await Read(await client.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup)));
        var applicationId = created.GetProperty("loanApplicationId").GetGuid();
        var borrower = await client.GetAsync($"/api/v1/borrowers/{setup.BorrowerId}");
        var changed = new { civilNumber = setup.CivilNumber, employeeNumber = setup.EmployeeNumber, fullName = "Changed Name", phoneNumber = "90000000", nationality = "Changed", organization = "MOD", rankGrade = "B", employmentInformation = "Changed" };
        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Put, $"/api/v1/borrowers/{setup.BorrowerId}", borrower.Headers.ETag!.Tag.Trim('"'), changed)).StatusCode);
        var loaded = await Read(await client.GetAsync($"/api/v1/loan-applications/{applicationId}"));
        var snapshot = loaded.GetProperty("borrowerSnapshot");
        Assert.Equal(setup.BorrowerName, snapshot.GetProperty("fullName").GetString());
        Assert.Equal("OM", snapshot.GetProperty("nationality").GetString());
        Assert.Equal("A", snapshot.GetProperty("rankGrade").GetString());
        Assert.Equal(setup.ProductName, loaded.GetProperty("productSnapshot").GetProperty("productName").GetString());
    }

    [Fact]
    public async Task Loan_application_permissions_are_independent()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var anonymous = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/loan-applications")).StatusCode);
        using var admin = await Administrator();
        var setup = await SetupAvailableVersion(admin);
        var createdResponse = await admin.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup));
        var application = await Read(createdResponse);
        var id = application.GetProperty("loanApplicationId").GetGuid();
        var etag = createdResponse.Headers.ETag!.Tag.Trim('"');
        using var read = await PermissionClient(admin, $"read-{suffix}", "loanApplications.read");
        Assert.Equal(HttpStatusCode.OK, (await read.GetAsync("/api/v1/loan-applications")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await read.GetAsync($"/api/v1/loan-applications/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await read.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(read, HttpMethod.Put, $"/api/v1/loan-applications/{id}", etag, new { requestedAmount = 2m, financingType = "Build" })).StatusCode);
        using var create = await PermissionClient(admin, $"create-{suffix}", "loanApplications.create");
        Assert.Equal(HttpStatusCode.Created, (await create.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await create.GetAsync("/api/v1/loan-applications")).StatusCode);
        using var update = await PermissionClient(admin, $"update-{suffix}", "loanApplications.update");
        Assert.Equal(HttpStatusCode.OK, (await Send(update, HttpMethod.Put, $"/api/v1/loan-applications/{id}", etag, new { requestedAmount = 2m, financingType = "Build" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await update.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup))).StatusCode);
    }


    [Fact]
    public async Task Eligibility_and_submission_lifecycle_persist_and_enforce_etags()
    {
        using var client = await Administrator();
        var setup = await SetupAvailableVersion(client);
        var createdResponse = await client.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup));
        var created = await Read(createdResponse);
        var id = created.GetProperty("loanApplicationId").GetGuid();
        var createdTag = createdResponse.Headers.ETag!.Tag.Trim('"');

        var missing = await client.PostAsJsonAsync($"/api/v1/loan-applications/{id}/evaluate-eligibility", new { });
        Assert.Equal((HttpStatusCode)428, missing.StatusCode);
        Assert.Equal("loanApplications.preconditionRequired", (await Read(missing)).GetProperty("errorCode").GetString());

        var evaluatedResponse = await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{id}/evaluate-eligibility", createdTag, new { });
        Assert.Equal(HttpStatusCode.OK, evaluatedResponse.StatusCode);
        var evaluatedTag = evaluatedResponse.Headers.ETag!.Tag.Trim('"');
        var evaluated = await Read(evaluatedResponse);
        Assert.True(evaluated.GetProperty("eligibilityDecision").GetProperty("isEligible").GetBoolean());
        Assert.Equal(1000m, evaluated.GetProperty("eligibilityDecision").GetProperty("permittedAmount").GetDecimal());

        var stale = await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{id}/evaluate-eligibility", createdTag, new { });
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal("loanApplications.concurrencyConflict", (await Read(stale)).GetProperty("errorCode").GetString());

        var edit = await Send(client, HttpMethod.Put, $"/api/v1/loan-applications/{id}", evaluatedTag, new { requestedAmount = 400m, financingType = "Renovate" });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await Read(edit)).GetProperty("eligibilityDecision").ValueKind);

        var reevaluated = await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{id}/evaluate-eligibility", edit.Headers.ETag!.Tag.Trim('"'), new { });
        var submitted = await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{id}/submit", reevaluated.Headers.ETag!.Tag.Trim('"'), new { });
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var submittedBody = await Read(submitted);
        Assert.Equal("Submitted", submittedBody.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, submittedBody.GetProperty("submittedAtUtc").ValueKind);

        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<LoanOriginationDbContext>();
        var stored = await database.LoanApplications.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(LoanApplicationStatus.Submitted, stored.Status);
        Assert.NotNull(stored.EligibilityDecision);
        Assert.NotNull(stored.SubmittedAtUtc);

        var currentTag = submitted.Headers.ETag!.Tag.Trim('"');
        foreach (var response in new[] {
            await Send(client, HttpMethod.Put, $"/api/v1/loan-applications/{id}", currentTag, new { requestedAmount = 300m, financingType = "Build" }),
            await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{id}/evaluate-eligibility", currentTag, new { }),
            await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{id}/submit", currentTag, new { })
        })
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("loanApplications.notDraft", (await Read(response)).GetProperty("errorCode").GetString());
        }
    }


    [Fact]
    public async Task Eligibility_uses_snapshot_and_application_count_and_rechecks_product()
    {
        using var client = await Administrator();
        var setup = await SetupAvailableVersion(client);

        var firstResponse = await client.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup));
        var first = await Read(firstResponse);
        var firstId = first.GetProperty("loanApplicationId").GetGuid();

        var borrowerResponse = await client.GetAsync($"/api/v1/borrowers/{setup.BorrowerId}");
        var changedBorrower = new { civilNumber = setup.CivilNumber, employeeNumber = setup.EmployeeNumber, fullName = "Changed Master", phoneNumber = "90000000", nationality = "XX", organization = "MOD", rankGrade = "B", employmentInformation = "Changed" };
        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Put, $"/api/v1/borrowers/{setup.BorrowerId}", borrowerResponse.Headers.ETag!.Tag.Trim('"'), changedBorrower)).StatusCode);

        var firstEval = await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{firstId}/evaluate-eligibility", firstResponse.Headers.ETag!.Tag.Trim('"'), new { });
        Assert.Equal(HttpStatusCode.OK, firstEval.StatusCode);
        var firstBody = await Read(firstEval);
        Assert.True(firstBody.GetProperty("eligibilityDecision").GetProperty("isEligible").GetBoolean());
        Assert.Equal("OM", firstBody.GetProperty("borrowerSnapshot").GetProperty("nationality").GetString());
        Assert.Equal("A", firstBody.GetProperty("borrowerSnapshot").GetProperty("rankGrade").GetString());

        var firstSubmit = await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{firstId}/submit", firstEval.Headers.ETag!.Tag.Trim('"'), new { });
        Assert.Equal(HttpStatusCode.OK, firstSubmit.StatusCode);

        var secondResponse = await client.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup));
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var second = await Read(secondResponse);
        var secondId = second.GetProperty("loanApplicationId").GetGuid();

        var secondEval = await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{secondId}/evaluate-eligibility", secondResponse.Headers.ETag!.Tag.Trim('"'), new { });
        Assert.Equal(HttpStatusCode.OK, secondEval.StatusCode);
        var secondDecision = (await Read(secondEval)).GetProperty("eligibilityDecision");
        Assert.False(secondDecision.GetProperty("isEligible").GetBoolean());
        Assert.True(secondDecision.GetProperty("observedConflictingApplicationCount").GetInt32() >= 1);
        Assert.Contains(secondDecision.GetProperty("ruleResults").EnumerateArray(), x => x.GetProperty("reasonCode").GetString() == "eligibility.applicationCountExceeded");

        var ineligibleSubmit = await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{secondId}/submit", secondEval.Headers.ETag!.Tag.Trim('"'), new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, ineligibleSubmit.StatusCode);
        Assert.Equal("loanApplications.ineligible", (await Read(ineligibleSubmit)).GetProperty("errorCode").GetString());

        var thirdResponse = await client.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup));
        var third = await Read(thirdResponse);
        var thirdId = third.GetProperty("loanApplicationId").GetGuid();
        var noEligibility = await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{thirdId}/submit", thirdResponse.Headers.ETag!.Tag.Trim('"'), new { });
        Assert.Equal(HttpStatusCode.Conflict, noEligibility.StatusCode);
        Assert.Equal("loanApplications.eligibilityRequired", (await Read(noEligibility)).GetProperty("errorCode").GetString());

        var productResponse = await client.GetAsync($"/api/v1/loan-products/{setup.ProductId}");
        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Post, $"/api/v1/loan-products/{setup.ProductId}/deactivate", productResponse.Headers.ETag!.Tag.Trim('"'), new { })).StatusCode);

        var freshSetup = await SetupAvailableVersion(client);
        var productCheckResponse = await client.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(freshSetup));
        var productCheck = await Read(productCheckResponse);
        var productCheckId = productCheck.GetProperty("loanApplicationId").GetGuid();
        var originalSnapshot = productCheck.GetProperty("productSnapshot").GetRawText();
        var productCheckEval = await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{productCheckId}/evaluate-eligibility", productCheckResponse.Headers.ETag!.Tag.Trim('"'), new { });
        var productDetail = await client.GetAsync($"/api/v1/loan-products/{freshSetup.ProductId}");
        Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Post, $"/api/v1/loan-products/{freshSetup.ProductId}/deactivate", productDetail.Headers.ETag!.Tag.Trim('"'), new { })).StatusCode);
        var unavailableSubmit = await Send(client, HttpMethod.Post, $"/api/v1/loan-applications/{productCheckId}/submit", productCheckEval.Headers.ETag!.Tag.Trim('"'), new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unavailableSubmit.StatusCode);
        Assert.Equal("loanApplications.productVersionUnavailable", (await Read(unavailableSubmit)).GetProperty("errorCode").GetString());
        var reloaded = await Read(await client.GetAsync($"/api/v1/loan-applications/{productCheckId}"));
        Assert.Equal("Draft", reloaded.GetProperty("status").GetString());
        Assert.Equal(originalSnapshot, reloaded.GetProperty("productSnapshot").GetRawText());
    }


    [Fact]
    public async Task Evaluate_and_submit_permissions_are_independent()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var admin = await Administrator();
        var setup = await SetupAvailableVersion(admin);
        var createdResponse = await admin.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup));
        var created = await Read(createdResponse);
        var id = created.GetProperty("loanApplicationId").GetGuid();
        var createdTag = createdResponse.Headers.ETag!.Tag.Trim('"');

        using var anonymous = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"/api/v1/loan-applications/{id}/evaluate-eligibility", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"/api/v1/loan-applications/{id}/submit", new { })).StatusCode);

        using var readOnly = await PermissionClient(admin, $"task07-read-{suffix}", "loanApplications.read");
        Assert.Equal(HttpStatusCode.OK, (await readOnly.GetAsync($"/api/v1/loan-applications/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(readOnly, HttpMethod.Post, $"/api/v1/loan-applications/{id}/evaluate-eligibility", createdTag, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(readOnly, HttpMethod.Post, $"/api/v1/loan-applications/{id}/submit", createdTag, new { })).StatusCode);

        using var evaluateOnly = await PermissionClient(admin, $"task07-eval-{suffix}", "loanApplications.evaluateEligibility");
        var evaluated = await Send(evaluateOnly, HttpMethod.Post, $"/api/v1/loan-applications/{id}/evaluate-eligibility", createdTag, new { });
        Assert.Equal(HttpStatusCode.OK, evaluated.StatusCode);
        var evaluatedTag = evaluated.Headers.ETag!.Tag.Trim('"');
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(evaluateOnly, HttpMethod.Post, $"/api/v1/loan-applications/{id}/submit", evaluatedTag, new { })).StatusCode);

        using var submitOnly = await PermissionClient(admin, $"task07-submit-{suffix}", "loanApplications.submit");
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(submitOnly, HttpMethod.Post, $"/api/v1/loan-applications/{id}/evaluate-eligibility", evaluatedTag, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(submitOnly, HttpMethod.Post, $"/api/v1/loan-applications/{id}/submit", evaluatedTag, new { })).StatusCode);
    }

    [Fact]
    public async Task Unit_decisions_persist_and_enforce_validation_state_concurrency_and_authorization()
    {
        using var admin = await Administrator();
        async Task<(Guid Id, string ETag)> Submitted()
        {
            var setup = await SetupAvailableVersion(admin); var created = await admin.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup)); var body = await Read(created); var id = body.GetProperty("loanApplicationId").GetGuid();
            var evaluated = await Send(admin, HttpMethod.Post, $"/api/v1/loan-applications/{id}/evaluate-eligibility", created.Headers.ETag!.Tag.Trim('"'), new { });
            var submitted = await Send(admin, HttpMethod.Post, $"/api/v1/loan-applications/{id}/submit", evaluated.Headers.ETag!.Tag.Trim('"'), new { }); return (id, submitted.Headers.ETag!.Tag.Trim('"'));
        }
        var approve = await Submitted();
        var missing = await admin.PostAsJsonAsync($"/api/v1/loan-applications/{approve.Id}/unit-decision", new { decision = "approve" }); Assert.Equal((HttpStatusCode)428, missing.StatusCode); Assert.Equal("loanApplications.preconditionRequired", (await Read(missing)).GetProperty("errorCode").GetString());
        var malformed = await Send(admin, HttpMethod.Post, $"/api/v1/loan-applications/{approve.Id}/unit-decision", "bad", new { decision = "approve" }); Assert.Equal((HttpStatusCode)428, malformed.StatusCode);
        var invalid = await Send(admin, HttpMethod.Post, $"/api/v1/loan-applications/{approve.Id}/unit-decision", approve.ETag, new { decision = "other" }); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode); Assert.Equal("loanApplications.invalidUnitDecision", (await Read(invalid)).GetProperty("errorCode").GetString());
        foreach (var reason in new string?[] { null, "", "   " }) { var response = await Send(admin, HttpMethod.Post, $"/api/v1/loan-applications/{approve.Id}/unit-decision", approve.ETag, new { decision = "reject", comment = reason }); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Equal("loanApplications.rejectionReasonRequired", (await Read(response)).GetProperty("errorCode").GetString()); }
        var approved = await Send(admin, HttpMethod.Post, $"/api/v1/loan-applications/{approve.Id}/unit-decision", approve.ETag, new { decision = "approve", comment = " reviewed " }); Assert.Equal(HttpStatusCode.OK, approved.StatusCode); var approvedBody = await Read(approved); Assert.Equal("UnitApproved", approvedBody.GetProperty("status").GetString()); Assert.Equal("Approved", approvedBody.GetProperty("unitApproval").GetProperty("decision").GetString()); Assert.Equal("reviewed", approvedBody.GetProperty("unitApproval").GetProperty("comment").GetString()); Assert.NotEqual(Guid.Empty, approvedBody.GetProperty("unitApproval").GetProperty("actorUserId").GetGuid());
        var second = await Send(admin, HttpMethod.Post, $"/api/v1/loan-applications/{approve.Id}/unit-decision", approved.Headers.ETag!.Tag.Trim('"'), new { decision = "approve" }); Assert.Equal(HttpStatusCode.Conflict, second.StatusCode); Assert.Equal("loanApplications.notSubmitted", (await Read(second)).GetProperty("errorCode").GetString());
        var stale = await Send(admin, HttpMethod.Post, $"/api/v1/loan-applications/{approve.Id}/unit-decision", approve.ETag, new { decision = "approve" }); Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode); Assert.Equal("loanApplications.concurrencyConflict", (await Read(stale)).GetProperty("errorCode").GetString());
        using (var scope = fixture.Factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<LoanOriginationDbContext>(); var stored = await db.LoanApplications.AsNoTracking().SingleAsync(x => x.Id == approve.Id); Assert.Equal(UnitDecision.Approved, stored.UnitApproval!.Decision); }
        var reject = await Submitted(); var rejected = await Send(admin, HttpMethod.Post, $"/api/v1/loan-applications/{reject.Id}/unit-decision", reject.ETag, new { decision = "reject", comment = " incomplete " }); var rejectedBody = await Read(rejected); Assert.Equal("Rejected", rejectedBody.GetProperty("status").GetString()); Assert.Equal("Rejected", rejectedBody.GetProperty("unitApproval").GetProperty("decision").GetString()); Assert.Equal("incomplete", rejectedBody.GetProperty("unitApproval").GetProperty("rejectionReason").GetString()); Assert.NotEqual(JsonValueKind.Null, rejectedBody.GetProperty("rejectedAtUtc").ValueKind);
        using (var scope = fixture.Factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<LoanOriginationDbContext>(); var stored = await db.LoanApplications.AsNoTracking().SingleAsync(x => x.Id == reject.Id); Assert.Equal(UnitDecision.Rejected, stored.UnitApproval!.Decision); Assert.Equal("incomplete", stored.UnitApproval.RejectionReason); Assert.NotNull(stored.RejectedAtUtc); }
        using var anonymous = fixture.Factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"/api/v1/loan-applications/{reject.Id}/unit-decision", new { decision = "approve" })).StatusCode);
        using var readOnly = await PermissionClient(admin, $"task08-read-{Guid.NewGuid():N}", "loanApplications.read"); Assert.Equal(HttpStatusCode.Forbidden, (await Send(readOnly, HttpMethod.Post, $"/api/v1/loan-applications/{reject.Id}/unit-decision", rejected.Headers.ETag!.Tag.Trim('"'), new { decision = "approve" })).StatusCode);
        var permissionTarget = await Submitted(); using var unitOnly = await PermissionClient(admin, $"task08-unit-{Guid.NewGuid():N}", "loanApplications.unitApprove"); var unitOnlyDecision = await Send(unitOnly, HttpMethod.Post, $"/api/v1/loan-applications/{permissionTarget.Id}/unit-decision", permissionTarget.ETag, new { decision = "approve" }); Assert.Equal(HttpStatusCode.OK, unitOnlyDecision.StatusCode);
        var draftSetup = await SetupAvailableVersion(admin); var draftResponse = await admin.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(draftSetup)); var draftBody = await Read(draftResponse); var wrongState = await Send(admin, HttpMethod.Post, $"/api/v1/loan-applications/{draftBody.GetProperty("loanApplicationId").GetGuid()}/unit-decision", draftResponse.Headers.ETag!.Tag.Trim('"'), new { decision = "approve" }); Assert.Equal(HttpStatusCode.Conflict, wrongState.StatusCode); Assert.Equal("loanApplications.notSubmitted", (await Read(wrongState)).GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Unit_approved_application_still_counts_toward_maximum_application_rule()
    {
        using var admin = await Administrator();
        var setup = await SetupAvailableVersion(admin);
        var firstResponse = await admin.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup));
        var firstBody = await Read(firstResponse);
        var firstId = firstBody.GetProperty("loanApplicationId").GetGuid();
        var firstEvaluated = await Send(admin, HttpMethod.Post, $"/api/v1/loan-applications/{firstId}/evaluate-eligibility", firstResponse.Headers.ETag!.Tag.Trim('"'), new { });
        var firstSubmitted = await Send(admin, HttpMethod.Post, $"/api/v1/loan-applications/{firstId}/submit", firstEvaluated.Headers.ETag!.Tag.Trim('"'), new { });
        var firstApproved = await Send(admin, HttpMethod.Post, $"/api/v1/loan-applications/{firstId}/unit-decision", firstSubmitted.Headers.ETag!.Tag.Trim('"'), new { decision = "approve" });
        Assert.Equal(HttpStatusCode.OK, firstApproved.StatusCode);

        var secondResponse = await admin.PostAsJsonAsync("/api/v1/loan-applications", ApplicationInput(setup));
        var secondBody = await Read(secondResponse);
        var secondId = secondBody.GetProperty("loanApplicationId").GetGuid();
        var secondEvaluated = await Send(admin, HttpMethod.Post, $"/api/v1/loan-applications/{secondId}/evaluate-eligibility", secondResponse.Headers.ETag!.Tag.Trim('"'), new { });
        Assert.Equal(HttpStatusCode.OK, secondEvaluated.StatusCode);
        var decision = (await Read(secondEvaluated)).GetProperty("eligibilityDecision");
        Assert.False(decision.GetProperty("isEligible").GetBoolean());
        Assert.Contains(decision.GetProperty("ruleResults").EnumerateArray(), x => x.GetProperty("reasonCode").GetString() == "eligibility.applicationCountExceeded");
    }

    async Task<HttpClient> Administrator() { var client = fixture.Factory.CreateClient(new() { HandleCookies = true }); Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/login", new { username = IdentityAccessIntegrationTests.Admin, password = IdentityAccessIntegrationTests.Password })).StatusCode); return client; }
    static async Task<Setup> SetupAvailableVersion(HttpClient client) { var suffix = Guid.NewGuid().ToString("N"); var civil = $"C-{suffix}"; var employee = $"E-{suffix}"; var name = $"Original {suffix}"; var borrower = await Read(await client.PostAsJsonAsync("/api/v1/borrowers", new { civilNumber = civil, employeeNumber = employee, fullName = name, phoneNumber = "90000000", nationality = "OM", organization = "MOD", rankGrade = "A", employmentInformation = "Active" })); var productName = $"Product {suffix}"; var product = await Read(await client.PostAsJsonAsync("/api/v1/loan-products", new { name = productName })); var productId = product.GetProperty("loanProductId").GetGuid(); var draft = await CreateDraft(client, productId, DateOnly.FromDateTime(DateTime.UtcNow)); await Publish(client, productId, draft); return new(borrower.GetProperty("borrowerId").GetGuid(), productId, draft.VersionId, name, productName, civil, employee); }
    static object ApplicationInput(Setup setup) => new { borrowerId = setup.BorrowerId, loanProductVersionId = setup.VersionId, requestedAmount = 500m, financingType = "Build" };
    static async Task<Draft> CreateDraft(HttpClient client, Guid productId, DateOnly from) { var response = await client.PostAsJsonAsync($"/api/v1/loan-products/{productId}/versions", new { maximumAmount = 1000m, currency = "OMR", deductionPercentage = 10m, financingTypes = FinancingTypes, eligibilityConfiguration = new { requiredNationality = "OM", maximumApplicationCount = 1, rankGradeAmountRules = new[] { new { rankGrade = "A", maximumAmount = 1000m } }, term = new { maximumTermMonths = 240, dueDateRule = "Monthly" } }, effectiveFrom = from, effectiveTo = (DateOnly?)null }); Assert.Equal(HttpStatusCode.Created, response.StatusCode); var body = await Read(response); return new(body.GetProperty("versionId").GetGuid(), response.Headers.ETag!.Tag.Trim('"')); }
    static async Task Publish(HttpClient client, Guid productId, Draft draft) => Assert.Equal(HttpStatusCode.OK, (await Send(client, HttpMethod.Post, $"/api/v1/loan-products/{productId}/versions/{draft.VersionId}/publish", draft.ETag, new { })).StatusCode);
    static async Task Rejected(HttpClient client, object input, string code, HttpStatusCode status = HttpStatusCode.UnprocessableEntity) { var response = await client.PostAsJsonAsync("/api/v1/loan-applications", input); Assert.Equal(status, response.StatusCode); Assert.Equal(code, (await Read(response)).GetProperty("errorCode").GetString()); }
    async Task<HttpClient> PermissionClient(HttpClient administrator, string username, string permissionKey) { Guid roleId; using (var scope = fixture.Factory.Services.CreateScope()) { var database = scope.ServiceProvider.GetRequiredService<IdentityAccessDbContext>(); var permission = await database.Permissions.SingleAsync(value => value.Key == permissionKey); var role = new Role { Id = Guid.NewGuid(), Name = $"Test {permissionKey} {username}" }; database.Roles.Add(role); database.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id }); await database.SaveChangesAsync(); roleId = role.Id; } const string password = "Disposable_permission_password_123!"; var created = await Read(await administrator.PostAsJsonAsync("/api/v1/users", new { username, displayName = username, password })); Assert.Equal(HttpStatusCode.OK, (await Send(administrator, HttpMethod.Put, $"/api/v1/users/{created.GetProperty("userId").GetGuid()}/roles", created.GetProperty("eTag").GetString()!, new { roleIds = new[] { roleId } })).StatusCode); var client = fixture.Factory.CreateClient(new() { HandleCookies = true }); Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password })).StatusCode); return client; }
    static async Task<JsonElement> Read(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, string etag, object body) { using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) }; request.Headers.TryAddWithoutValidation("If-Match", $"\"{etag}\""); return await client.SendAsync(request); }
    sealed record Setup(Guid BorrowerId, Guid ProductId, Guid VersionId, string BorrowerName, string ProductName, string CivilNumber, string EmployeeNumber);
    sealed record Draft(Guid VersionId, string ETag);
}
