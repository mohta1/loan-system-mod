using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
namespace LoanSystem.ApiTests;
public sealed class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public ApiTests(WebApplicationFactory<Program> factory) => _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoints_are_healthy(string path) => Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().GetAsync(path)).StatusCode);
    [Fact] public async Task System_info_and_correlation_are_returned() { using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/info"); request.Headers.Add("X-Correlation-ID", "test-correlation"); var response = await _factory.CreateClient().SendAsync(request); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("test-correlation", response.Headers.GetValues("X-Correlation-ID").Single()); var info = await response.Content.ReadFromJsonAsync<SystemInfoResponse>(); Assert.Equal("Operational", info?.Status); }
    [Fact] public async Task Unexpected_errors_use_problem_details() { var response = await _factory.CreateClient().GetAsync("/api/v1/system/error"); Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType); }
}
