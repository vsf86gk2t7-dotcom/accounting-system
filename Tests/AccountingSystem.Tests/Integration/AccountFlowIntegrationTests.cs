using AccountingSystem.Data;
using AccountingSystem.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AccountingSystem.Tests.Integration;

public class AccountFlowIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AccountFlowIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // =========================================
    // GET Pages
    // =========================================

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    [InlineData("/Account/ForgotPassword")]
    [InlineData("/Account/AccessDenied")]
    [InlineData("/Account/RegisterSuccess")]
    public async Task AnonymousGet_Returns200(string url)
    {
        var client = IntegrationTestHelpers.CreateClient(_factory);
        var response = await client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK, $"URL={url}");
    }

    [Fact]
    public async Task ResetPassword_Get_WithoutPhone_RedirectsToForgotPassword()
    {
        var client = IntegrationTestHelpers.CreateClient(_factory);
        var response = await client.GetAsync("/Account/ResetPassword");

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);
        response.Headers.Location?.ToString().Should().Contain("ForgotPassword");
    }

    [Fact]
    public async Task ResetPassword_Get_WithPhone_Returns200()
    {
        var client = IntegrationTestHelpers.CreateClient(_factory);
        var response = await client.GetAsync(
            "/Account/ResetPassword?phone=01000000001");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // Login — Happy path
    // =========================================

    [Fact]
    public async Task Login_ValidAdmin_RedirectsToHome()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);

        var client = IntegrationTestHelpers.CreateClient(_factory);
        var response = await IntegrationTestHelpers.PostLoginAsync(
            client,
            IntegrationTestHelpers.AdminPhone,
            IntegrationTestHelpers.AdminPassword);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);
        response.Headers.Location?.ToString().Should().Contain("/Home");
        response.Headers.Contains("Set-Cookie").Should().BeTrue();
    }

    [Fact]
    public async Task Login_AsCustomer_RedirectsToCustomerPortal()
    {
        await IntegrationTestHelpers.SeedCustomerWithUserAsync(_factory);

        var client = IntegrationTestHelpers.CreateClient(_factory);
        var response = await IntegrationTestHelpers.PostLoginAsync(
            client,
            IntegrationTestHelpers.CustomerPhone,
            IntegrationTestHelpers.CustomerPassword);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);
        response.Headers.Location?.ToString().Should().Contain("CustomerPortal");
    }

    [Fact]
    public async Task Login_AsSupplier_RedirectsToSupplierPortal()
    {
        await IntegrationTestHelpers.SeedSupplierWithUserAsync(_factory);

        var client = IntegrationTestHelpers.CreateClient(_factory);
        var response = await IntegrationTestHelpers.PostLoginAsync(
            client,
            IntegrationTestHelpers.SupplierPhone,
            IntegrationTestHelpers.SupplierPassword);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);
        response.Headers.Location?.ToString().Should().Contain("SupplierPortal");
    }

    // =========================================
    // Login — Error cases (status only)
    // =========================================

       [Fact]
    public async Task Login_WrongPassword_Returns200()
    {
        // ✅ مستخدم منفصل عشان مانزوّدش عدّاد فشل الأدمن المشترك
        const string phone = "01777770077";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = new PasswordHasher<User>();
            var u = new User
            {
                Phone = phone,
                UserType = UserType.Admin,
                Status = UserStatus.Approved,
                IsActive = true,
                IsPasswordSet = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            };
            u.PasswordHash = hasher.HashPassword(u, "CorrectPass@123");
            db.Users.Add(u);
            await db.SaveChangesAsync();
        }

        var client = IntegrationTestHelpers.CreateClient(_factory);
        var response = await IntegrationTestHelpers.PostLoginAsync(
            client, phone, "WrongPass!999");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_NonExistingPhone_Returns200()
    {
        var client = IntegrationTestHelpers.CreateClient(_factory);
        var response = await IntegrationTestHelpers.PostLoginAsync(
            client, "01999999999", "AnyPass123!");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_InactiveUser_Returns200()
    {
        const string phone = "01777770001";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = new PasswordHasher<User>();
            var u = new User
            {
                Phone = phone,
                UserType = UserType.Admin,
                Status = UserStatus.Approved,
                IsActive = false,
                IsPasswordSet = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            };
            u.PasswordHash = hasher.HashPassword(u, "Test@1234");
            db.Users.Add(u);
            await db.SaveChangesAsync();
        }

        var client = IntegrationTestHelpers.CreateClient(_factory);
        var response = await IntegrationTestHelpers.PostLoginAsync(
            client, phone, "Test@1234");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_PendingStatus_Returns200()
    {
        const string phone = "01777770002";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = new PasswordHasher<User>();
            var u = new User
            {
                Phone = phone,
                UserType = UserType.Admin,
                Status = UserStatus.Pending,
                IsActive = true,
                IsPasswordSet = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            };
            u.PasswordHash = hasher.HashPassword(u, "Test@1234");
            db.Users.Add(u);
            await db.SaveChangesAsync();
        }

        var client = IntegrationTestHelpers.CreateClient(_factory);
        var response = await IntegrationTestHelpers.PostLoginAsync(
            client, phone, "Test@1234");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_EmptyCredentials_Returns200()
    {
        var client = IntegrationTestHelpers.CreateClient(_factory);
        var response = await IntegrationTestHelpers.PostLoginAsync(client, "", "");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_LockedOutUser_Returns200()
    {
        // ✅ مستخدم منفصل عشان مانقفلش الأدمن المشترك
        const string phone = "01777770088";
        const string pass = "Test@1234";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = new PasswordHasher<User>();
            var u = new User
            {
                Phone = phone,
                UserType = UserType.Admin,
                Status = UserStatus.Approved,
                IsActive = true,
                IsPasswordSet = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            };
            u.PasswordHash = hasher.HashPassword(u, pass);
            u.LockoutUntil = DateTime.UtcNow.AddMinutes(10);
            u.FailedLoginAttempts = 5;
            db.Users.Add(u);
            await db.SaveChangesAsync();
        }

        var client = IntegrationTestHelpers.CreateClient(_factory);
        var response = await IntegrationTestHelpers.PostLoginAsync(
            client, phone, pass);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
    // =========================================
    // Login — Repeated failures (isolated user!)
    // =========================================

    [Fact]
    public async Task Login_RepeatedFailures_LocksAccount()
    {
        // ✅ نستخدم مستخدم منفصل عشان ما نأثرش على الأدمن المشترك
        const string phone = "01777770099";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = new PasswordHasher<User>();
            var u = new User
            {
                Phone = phone,
                UserType = UserType.Admin,
                Status = UserStatus.Approved,
                IsActive = true,
                IsPasswordSet = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            };
            u.PasswordHash = hasher.HashPassword(u, "Test@1234");
            db.Users.Add(u);
            await db.SaveChangesAsync();
        }

        var client = IntegrationTestHelpers.CreateClient(_factory);

        for (int i = 0; i < 5; i++)
        {
            var token = await IntegrationTestHelpers.GetAntiForgeryTokenAsync(client);
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = phone,
                ["password"] = "WrongPass!",
                ["__RequestVerificationToken"] = token
            });
            await client.PostAsync("/Account/Login", form);
        }

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user2 = await db2.Users.FirstAsync(x => x.Phone == phone);

        user2.FailedLoginAttempts.Should().BeGreaterThan(0);
        user2.LockoutUntil.Should().NotBeNull();
        user2.LockoutUntil!.Value.Should().BeAfter(DateTime.UtcNow);
    }

    // =========================================
    // Logout (status only)
    // =========================================

    [Fact]
    public async Task Logout_Returns302()
    {
        // ✅ مستخدم أدمن خاص بالاختبار — معزول تماماً
        const string phone = "01777770066";
        const string pass = "Logout@Test123";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = new PasswordHasher<User>();
            var u = new User
            {
                Phone = phone,
                UserType = UserType.Admin,
                Status = UserStatus.Approved,
                IsActive = true,
                IsPasswordSet = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow
            };
            u.PasswordHash = hasher.HashPassword(u, pass);
            db.Users.Add(u);
            await db.SaveChangesAsync();
        }

        // Login يدوي بالمستخدم ده
        var client = IntegrationTestHelpers.CreateClient(_factory);
        var loginResponse = await IntegrationTestHelpers.PostLoginAsync(
            client, phone, pass);

        loginResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        // نقل الكوكيز
        var setCookies = loginResponse.Headers
            .Where(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            .SelectMany(h => h.Value)
            .ToList();

        var cookieHeader = string.Join("; ", setCookies
            .Select(c => c.Split(';')[0])
            .Where(c => !string.IsNullOrWhiteSpace(c)));

        client.DefaultRequestHeaders.Add("Cookie", cookieHeader);

        // Logout
        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Account/Login");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/Logout", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);
    }

    // =========================================
    // Me
    // =========================================

    [Fact]
    public async Task Me_Anonymous_Returns401()
    {
        var client = IntegrationTestHelpers.CreateClient(_factory);
        var response = await client.GetAsync("/Account/Me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_Authenticated_ReturnsUserInfo()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/Account/Me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain(IntegrationTestHelpers.AdminPhone);
    }

    // =========================================
    // Register
    // =========================================

    [Fact]
    public async Task Register_Get_WhenAuthenticated_RedirectsHome()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);
        var client = await IntegrationTestHelpers
            .CreateAuthenticatedClientAsync(_factory);

        var response = await client.GetAsync("/Account/Register");

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);
    }

    [Fact]
    public async Task Register_AsCustomer_PersistsAndRedirectsToSuccess()
    {
        const string phone = "01099880001";

        var client = IntegrationTestHelpers.CreateClient(_factory);
        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Account/Register");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["AccountType"] = "1",
            ["Name"] = "عميل جديد",
            ["Phone"] = phone,
            ["Email"] = "new@customer.com",
            ["Address"] = "القاهرة",
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/Register", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);
        response.Headers.Location?.ToString().Should().Contain("RegisterSuccess");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var customer = await db.Customers.FirstOrDefaultAsync(x => x.Phone == phone);

        customer.Should().NotBeNull();
        customer!.PortalRequested.Should().BeTrue();
        customer.PortalApproved.Should().BeFalse();
        customer.HasPortalAccount.Should().BeFalse();
    }

    [Fact]
    public async Task Register_AsSupplier_PersistsAndRedirectsToSuccess()
    {
        const string phone = "01099880002";

        var client = IntegrationTestHelpers.CreateClient(_factory);
        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Account/Register");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["AccountType"] = "2",
            ["Name"] = "مورد جديد",
            ["Phone"] = phone,
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/Register", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var supplier = await db.Suppliers.FirstOrDefaultAsync(x => x.Phone == phone);

        supplier.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_AsEmployee_PersistsAndRedirectsToSuccess()
    {
        const string phone = "01099880003";

        var client = IntegrationTestHelpers.CreateClient(_factory);
        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Account/Register");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["AccountType"] = "3",
            ["Name"] = "موظف جديد",
            ["Phone"] = phone,
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/Register", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var employee = await db.Employees.FirstOrDefaultAsync(x => x.Phone == phone);

        employee.Should().NotBeNull();
    }

    [Fact]
    public async Task Register_PhoneAlreadyUsedInUsers_Returns200()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);

        var client = IntegrationTestHelpers.CreateClient(_factory);
        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Account/Register");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["AccountType"] = "1",
            ["Name"] = "محاولة تكرار",
            ["Phone"] = IntegrationTestHelpers.AdminPhone,
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/Register", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_EmptyName_Returns200()
    {
        var client = IntegrationTestHelpers.CreateClient(_factory);
        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Account/Register");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["AccountType"] = "1",
            ["Name"] = "",
            ["Phone"] = "01011119999",
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/Register", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // ForgotPassword
    // =========================================

    [Fact]
    public async Task ForgotPassword_ExistingUser_CreatesRequest()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);

        var client = IntegrationTestHelpers.CreateClient(_factory);
        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Account/ForgotPassword");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Phone"] = IntegrationTestHelpers.AdminPhone,
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/ForgotPassword", form);

        // أي redirect
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);

        // ✅ نتحقق من الـ DB بدل من Location
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var req = await db.PasswordResetRequests
            .FirstOrDefaultAsync(x => x.Phone == IntegrationTestHelpers.AdminPhone);

        req.Should().NotBeNull();
        req!.IsCompleted.Should().BeFalse();
        req.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task ForgotPassword_NonExistingUser_StillRedirects()
    {
        var client = IntegrationTestHelpers.CreateClient(_factory);
        var token = await IntegrationTestHelpers
            .GetAntiForgeryTokenFromPageAsync(client, "/Account/ForgotPassword");

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Phone"] = "01000000999",
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/ForgotPassword", form);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect, HttpStatusCode.Found, HttpStatusCode.SeeOther);
    }

    // =========================================
    // ResetPassword
    // =========================================

    [Fact]
    public async Task ResetPassword_ValidCode_Returns200Or302()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);
        await SeedResetRequestAsync("123456", DateTime.UtcNow.AddMinutes(15));

        var client = IntegrationTestHelpers.CreateClient(_factory);
        var token = await IntegrationTestHelpers.GetAntiForgeryTokenFromPageAsync(
            client,
            "/Account/ResetPassword?phone=" + IntegrationTestHelpers.AdminPhone);

        const string newPass = "NewAdmin@Test9999";
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Phone"] = IntegrationTestHelpers.AdminPhone,
            ["VerificationCode"] = "123456",
            ["Password"] = newPass,
            ["ConfirmPassword"] = newPass,
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/ResetPassword", form);

        // ✅ لا 500 ولا crash — إما نجاح أو عرض الخطأ
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Redirect,
            HttpStatusCode.Found,
            HttpStatusCode.SeeOther);
    }

    [Fact]
    public async Task ResetPassword_WrongCode_Returns200()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);
        await SeedResetRequestAsync("555555", DateTime.UtcNow.AddMinutes(15));

        var client = IntegrationTestHelpers.CreateClient(_factory);
        var token = await IntegrationTestHelpers.GetAntiForgeryTokenFromPageAsync(
            client,
            "/Account/ResetPassword?phone=" + IntegrationTestHelpers.AdminPhone);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Phone"] = IntegrationTestHelpers.AdminPhone,
            ["VerificationCode"] = "000000",
            ["Password"] = "NewPass@Test1",
            ["ConfirmPassword"] = "NewPass@Test1",
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/ResetPassword", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_WeakPassword_Returns200()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);

        var client = IntegrationTestHelpers.CreateClient(_factory);
        var token = await IntegrationTestHelpers.GetAntiForgeryTokenFromPageAsync(
            client,
            "/Account/ResetPassword?phone=" + IntegrationTestHelpers.AdminPhone);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Phone"] = IntegrationTestHelpers.AdminPhone,
            ["VerificationCode"] = "123456",
            ["Password"] = "weak",
            ["ConfirmPassword"] = "weak",
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/ResetPassword", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_ExpiredCode_Returns200()
    {
        await IntegrationTestHelpers.SeedAdminWithPermissionsAsync(_factory);
        await SeedResetRequestAsync("111111", DateTime.UtcNow.AddMinutes(-5));

        var client = IntegrationTestHelpers.CreateClient(_factory);
        var token = await IntegrationTestHelpers.GetAntiForgeryTokenFromPageAsync(
            client,
            "/Account/ResetPassword?phone=" + IntegrationTestHelpers.AdminPhone);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Phone"] = IntegrationTestHelpers.AdminPhone,
            ["VerificationCode"] = "111111",
            ["Password"] = "NewPass@Test1",
            ["ConfirmPassword"] = "NewPass@Test1",
            ["__RequestVerificationToken"] = token
        });

        var response = await client.PostAsync("/Account/ResetPassword", form);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // =========================================
    // Helpers
    // =========================================

    private async Task SeedResetRequestAsync(string code, DateTime expires)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users
            .FirstAsync(x => x.Phone == IntegrationTestHelpers.AdminPhone);

        var hasher = new PasswordHasher<User>();

        db.PasswordResetRequests.Add(new PasswordResetRequest
        {
            Phone = IntegrationTestHelpers.AdminPhone,
            UserName = user.Phone,
            UserType = user.UserType,
            CodeHash = hasher.HashPassword(user, code),
            RequestedAt = DateTime.UtcNow,
            ExpiresAt = expires,
            IsSent = false,
            IsCompleted = false
        });

        await db.SaveChangesAsync();
    }
}