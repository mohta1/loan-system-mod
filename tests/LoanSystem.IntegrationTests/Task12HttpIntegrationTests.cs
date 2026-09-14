using System.Net;
using System.Net.Http.Json;
using LoanSystem.Modules.Disbursements.Infrastructure;
using LoanSystem.Modules.LoanAccounts.Domain;
using LoanSystem.Modules.LoanAccounts.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoanSystem.IntegrationTests;

[Collection(IdentitySqlTestGroup.Name)]
public sealed class Task12HttpIntegrationTests(IdentitySqlFixture fixture)
{
    [Fact]
    public async Task Http_idempotency_and_actual_outbox_flow_create_one_requested_disbursement()
    {
        using var anonymous = fixture.Factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/disbursements")).StatusCode);
        var client = fixture.Factory.CreateClient(new() { HandleCookies = true }); Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/login", new { username = IdentityAccessIntegrationTests.Admin, password = IdentityAccessIntegrationTests.Password })).StatusCode);
        Guid loanId; using (var scope = fixture.Factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<LoanAccountsDbContext>(); var loan = LoanAccount.Open(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100000, "OMR", "Build"); loanId = loan.LoanId; db.LoanAccounts.Add(loan); await db.SaveChangesAsync(); }
        var body = new { loanId, amount = 25000, beneficiary = new { type = "Contractor", displayName = "Builder", accountHolderName = "Builder", bankName = "Bank", bankAccountIdentifier = "PRIVATE" }, supportingDocumentIds = Array.Empty<Guid>() };
        async Task<HttpResponseMessage> Create(object value) { using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/disbursements") { Content = JsonContent.Create(value) }; request.Headers.Add("Idempotency-Key", "same-key"); return await client.SendAsync(request); }
        var responses = await Task.WhenAll(Create(body), Create(body)); Assert.All(responses, x => Assert.Contains(x.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK })); var first = await responses[0].Content.ReadFromJsonAsync<Response>(); var second = await responses[1].Content.ReadFromJsonAsync<Response>(); Assert.Equal(first!.DisbursementId, second!.DisbursementId);
        var conflict = await Create(new { loanId, amount = 1, beneficiary = body.beneficiary, supportingDocumentIds = Array.Empty<Guid>() }); Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        for (var i = 0; i < 100; i++) { var current = await client.GetFromJsonAsync<Response>($"/api/v1/disbursements/{first.DisbursementId}"); if (current?.Status == "Requested") break; await Task.Delay(100); }
        using var verifyScope = fixture.Factory.Services.CreateScope(); var disbursements = verifyScope.ServiceProvider.GetRequiredService<DisbursementsDbContext>(); var loans = verifyScope.ServiceProvider.GetRequiredService<LoanAccountsDbContext>(); Assert.Single(await disbursements.Disbursements.Where(x => x.DisbursementId == first.DisbursementId).ToListAsync()); Assert.Single(await disbursements.OutboxMessages.Where(x => x.EventType == nameof(LoanSystem.Contracts.DisbursementCapacityRequestedV1)).ToListAsync()); Assert.Equal(25000, (await loans.LoanAccounts.AsNoTracking().SingleAsync(x => x.LoanId == loanId)).ReservedDisbursementAmount); Assert.Single(await loans.Reservations.Where(x => x.DisbursementId == first.DisbursementId).ToListAsync()); Assert.Equal("Requested", (await client.GetFromJsonAsync<Response>($"/api/v1/disbursements/{first.DisbursementId}"))!.Status);
    }
    private sealed record Response(Guid DisbursementId, string Status);
}
