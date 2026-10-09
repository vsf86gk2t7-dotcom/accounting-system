using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class EmployeePortalFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    private static readonly string[] PortalPermissions =
    {
        "employee.view",
        "employee.portal.manage"
    };

    public EmployeePortalFlowIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // =========================================
    // Seed helpers
    // =========================================

    private async Task<int> SeedEmployeeAsync(
        bool portalRequested = false,
        bool portalApproved = false,
        bool hasPortalAccount = false,
        bool isActive = true,
        string? phone = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var emp = new Employee
        {
            Name = "موظف " + Guid.NewGuid().ToString("N")[..6],
            Phone = phone ?? "011" + Random.Shared.Next(10000000, 99999999),
            JobTitle = "Tester",
            Salary = 3000,
            IsActive = isActive,
            EmployeeType = EmployeeType.Office,
            PortalRequested = portalRequested,
            PortalApproved = portalApproved,
            HasPortalAccount = hasPortalAccount,
            CreatedAt = DateTime.UtcNow
        };

        db.Employees.Add(emp);
        await db.SaveChangesAsync();
        return emp.Id;
    }

    private async Task<int> SeedRoleAsync(string code = "TestRole")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var existing = await db.Roles.FirstOrDefaultAsync(x => x.Code == code);
        if (existing != null) return existing.Id;

        var role = new Role
        {
            Name = "دور " + code,
            Code = code,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    // =========================================
    // RequestPortal
    // =========================================

    [Fact]
    public async Task RequestPortal_ValidEmployee_SetsPortalRequested()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);
        var empId = await SeedEmployeeAsync();

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Employee/Index");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync(
            $"/Employee/RequestPortal/{empId}", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emp = await db.Employees.FindAsync(empId);

        emp!.PortalRequested.Should().BeTrue();
        emp.PortalApproved.Should().BeFalse();
        emp.HasPortalAccount.Should().BeFalse();
    }

    [Fact]
    public async Task RequestPortal_NonExisting_ReturnsNotFound()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Employee/Index");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync(
            "/Employee/RequestPortal/999999", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RequestPortal_AlreadyRequested_RedirectsWithError()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);
        var empId = await SeedEmployeeAsync(portalRequested: true);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Employee/Index");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync(
            $"/Employee/RequestPortal/{empId}", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);
    }

    // =========================================
    // PortalRequests
    // =========================================

    [Fact]
    public async Task PortalRequests_Returns200()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
            _factory, PortalPermissions);
        await SeedEmployeeAsync(portalRequested: true);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/Employee/PortalRequests");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // ApprovePortal
    // =========================================

    [Fact]
    public async Task ApprovePortal_ValidEmployee_SetsPortalApproved()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
            _factory, PortalPermissions);
        var empId = await SeedEmployeeAsync(portalRequested: true);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Employee/PortalRequests");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync(
            $"/Employee/ApprovePortal/{empId}", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emp = await db.Employees.FindAsync(empId);

        emp!.PortalApproved.Should().BeTrue();
    }

    [Fact]
    public async Task ApprovePortal_NotRequested_RedirectsWithError()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
            _factory, PortalPermissions);
        var empId = await SeedEmployeeAsync(portalRequested: false);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Employee/PortalRequests");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync(
            $"/Employee/ApprovePortal/{empId}", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emp = await db.Employees.FindAsync(empId);

        emp!.PortalApproved.Should().BeFalse();
    }

    [Fact]
    public async Task ApprovePortal_NonExisting_ReturnsNotFound()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
            _factory, PortalPermissions);
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Employee/PortalRequests");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync(
            "/Employee/ApprovePortal/999999", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =========================================
    // CreatePortalAccount
    // =========================================

    [Fact]
    public async Task CreatePortalAccount_ApprovedEmployee_CreatesUserAndMarksHasAccount()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
            _factory, PortalPermissions);
        var roleId = await SeedRoleAsync("Cashier2");
        var empId = await SeedEmployeeAsync(
            portalRequested: true,
            portalApproved: true,
            hasPortalAccount: false);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Employee/PortalRequests");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = empId.ToString(),
            ["roleId"] = roleId.ToString(),
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync(
            $"/Employee/CreatePortalAccount/{empId}?roleId={roleId}", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emp = await db.Employees.FindAsync(empId);

        emp!.HasPortalAccount.Should().BeTrue();

        var user = await db.Users
            .FirstOrDefaultAsync(x => x.EmployeeId == empId);

        user.Should().NotBeNull();
        user!.UserType.Should().Be(UserType.Employee);
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreatePortalAccount_NotApproved_RedirectsWithError()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
            _factory, PortalPermissions);
        var roleId = await SeedRoleAsync("Cashier3");
        var empId = await SeedEmployeeAsync(
            portalRequested: true,
            portalApproved: false);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Employee/PortalRequests");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = empId.ToString(),
            ["roleId"] = roleId.ToString(),
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync(
            $"/Employee/CreatePortalAccount/{empId}?roleId={roleId}", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emp = await db.Employees.FindAsync(empId);

        emp!.HasPortalAccount.Should().BeFalse();
    }

    [Fact]
    public async Task CreatePortalAccount_AlreadyHasAccount_RedirectsWithError()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
            _factory, PortalPermissions);
        var roleId = await SeedRoleAsync("Cashier4");
        var empId = await SeedEmployeeAsync(
            portalRequested: true,
            portalApproved: true,
            hasPortalAccount: true);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Employee/PortalRequests");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = empId.ToString(),
            ["roleId"] = roleId.ToString(),
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync(
            $"/Employee/CreatePortalAccount/{empId}?roleId={roleId}", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);
    }

    [Fact]
    public async Task CreatePortalAccount_InvalidRole_RedirectsWithError()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
            _factory, PortalPermissions);
        var empId = await SeedEmployeeAsync(
            portalRequested: true,
            portalApproved: true,
            hasPortalAccount: false);

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Employee/PortalRequests");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = empId.ToString(),
            ["roleId"] = "999999",
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync(
            $"/Employee/CreatePortalAccount/{empId}?roleId=999999", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emp = await db.Employees.FindAsync(empId);

        emp!.HasPortalAccount.Should().BeFalse();
    }

    [Fact]
    public async Task CreatePortalAccount_NonExisting_ReturnsNotFound()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
            _factory, PortalPermissions);
        var roleId = await SeedRoleAsync("Cashier5");

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Employee/PortalRequests");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id"] = "999999",
            ["roleId"] = roleId.ToString(),
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync(
            $"/Employee/CreatePortalAccount/999999?roleId={roleId}", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}