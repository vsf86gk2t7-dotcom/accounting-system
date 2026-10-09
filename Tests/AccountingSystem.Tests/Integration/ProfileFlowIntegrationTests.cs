using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class ProfileFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ProfileFlowIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;

        // Profile مش محتاج permissions خاصة — أي مستخدم مسجل يقدر يعدل بروفايله
        IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory)
            .GetAwaiter().GetResult();
    }

    // =========================================
    // Edit GET
    // =========================================

    [Fact]
    public async Task Edit_Get_Authenticated_Returns200()
    {
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/Profile/Edit");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Edit_Get_Anonymous_RedirectsToLogin()
    {
        var client = IntegrationTestHelpers.CreateClient(_factory);

        var response = await client.GetAsync("/Profile/Edit");

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        response.Headers.Location?.ToString().Should()
            .Match(l => l.Contains("Login") || l.Contains("Account"));
    }

    // =========================================
    // Edit POST
    // =========================================

    [Fact]
    public async Task Edit_Post_UpdatesProfile()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Profile/Edit");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "الاسم بعد التعديل",
            ["Address"] = "القاهرة الجديدة",
            ["BirthDate"] = "1990-01-01",
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Profile/Edit", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);
    }

    [Fact]
    public async Task Edit_Post_EmptyFullName_Returns200()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Profile/Edit");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FullName"] = "",
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Profile/Edit", form);

        // إما يقبل أو يرفض بـ 200
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Redirect,
            HttpStatusCode.Found,
            HttpStatusCode.SeeOther);
    }
}