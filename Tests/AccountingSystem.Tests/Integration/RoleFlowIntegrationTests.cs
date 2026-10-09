using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class RoleFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    private static readonly string[] RolePermissions =
    {
        "role.view",
        "permission.assign"
    };

    public RoleFlowIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;

        IntegrationTestHelpers.SeedAdminWithPermissionsAsync(
                _factory, RolePermissions)
                .GetAwaiter().GetResult();

        // ✅ اربط الأدمن بدور "Admin" — مطلوب لـ CanManageRoleAsync
        EnsureAdminHasAdminRoleAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureAdminHasAdminRoleAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // 1. شوف أو أنشئ "Admin" role
        var adminRole = await db.Roles.FirstOrDefaultAsync(x => x.Code == "Admin");
        if (adminRole == null)
        {
            adminRole = new Role
            {
                Name = "Admin",
                Code = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Roles.Add(adminRole);
            await db.SaveChangesAsync();
        }

        // 2. اربطه بكل الأدمن اللي عنده AdminPhone
        var admins = await db.Users
            .Where(x => x.Phone == IntegrationTestHelpers.AdminPhone)
            .ToListAsync();

        foreach (var admin in admins)
        {
            admin.RoleId = adminRole.Id;
        }
        await db.SaveChangesAsync();
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

    private async Task<int> SeedPermissionAsync(string code = "test.perm")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var existing = await db.Permissions.FirstOrDefaultAsync(x => x.Code == code);
        if (existing != null) return existing.Id;

        var perm = new Permission
        {
            Code = code,
            Name = code,
            Group = "Test",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Permissions.Add(perm);
        await db.SaveChangesAsync();
        return perm.Id;
    }

    // =========================================
    // Index
    // =========================================

    [Fact]
    public async Task Index_Returns200()
    {
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/Role/Index");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // Permissions GET
    // =========================================

    [Fact]
    public async Task Permissions_ExistingRole_Returns200()
    {
        var roleId = await SeedRoleAsync("RolePermTest");
        await SeedPermissionAsync("test.view");

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync($"/Role/Permissions/{roleId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Permissions_NonExistingRole_RedirectsToAccessDenied()
    {
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/Role/Permissions/999999");

        // CanManageRoleAsync ترجع false (الدور مش موجود) → AccessDenied
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.Redirect,
            HttpStatusCode.Found,
            HttpStatusCode.SeeOther);
    }

    // =========================================
    // SavePermissions POST
    // =========================================

    [Fact]
    public async Task SavePermissions_ValidRole_RedirectsAndPersists()
    {
        var roleId = await SeedRoleAsync("RoleSaveTest");
        var permId = await SeedPermissionAsync("test.save");

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, $"/Role/Permissions/{roleId}");

        // ✅ اسم الحقل الصحيح من الـ View: selectedPermissionIds
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["roleId"] = roleId.ToString(),
            ["selectedPermissionIds[0]"] = permId.ToString(),
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Role/SavePermissions", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rolePerm = await db.RolePermissions
            .FirstOrDefaultAsync(x => x.RoleId == roleId && x.PermissionId == permId);

        rolePerm.Should().NotBeNull();
    }

    [Fact]
    public async Task SavePermissions_NoPermissions_ClearsRolePermissions()
    {
        var roleId = await SeedRoleAsync("RoleClearTest");
        var permId = await SeedPermissionAsync("test.clear");

        // امنح الصلاحية أول مرة
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permId,
                IsGranted = true,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, $"/Role/Permissions/{roleId}");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["roleId"] = roleId.ToString(),
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Role/SavePermissions", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var rolePerms = await db.RolePermissions
                .Where(x => x.RoleId == roleId)
                .ToListAsync();

            rolePerms.Should().BeEmpty();
        }
    }

    // =========================================
    // Authorization
    // =========================================

    [Fact]
    public async Task Index_WithoutPermission_RedirectsToAccessDenied()
    {
        // ملاحظة: بنستخدم مستخدم بدون صلاحيات
        var client = IntegrationTestHelpers.CreateClient(_factory);

        var response = await client.GetAsync("/Role/Index");

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        response.Headers.Location?.ToString().Should()
            .Match(l => l.Contains("Login") || l.Contains("AccessDenied"));
    }
}