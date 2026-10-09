using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;

namespace AccountingSystem.Tests.Integration;

public static class IntegrationTestHelpers
{
	public const string AdminPhone = "01000000001";
	public const string AdminPassword = "Admin@Test123!";
	public const string EmployeePhone = "01000000002";
	public const string EmployeePassword = "Employee@Test123!";
	public const string CustomerPhone = "01000000003";
	public const string CustomerPassword = "Customer@Test123!";
	public const string SupplierPhone = "01000000004";
	public const string SupplierPassword = "Supplier@Test123!";
	public const string AccessTokenCookieName = "AccountingSystem.AccessToken";

	/// <summary>
	/// زرع أدمن + الصلاحيات المطلوبة له
	/// </summary>
	public static async Task<int> SeedAdminWithPermissionsAsync(
		CustomWebApplicationFactory factory,
		params string[] permissionCodes)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var hasher = new PasswordHasher<User>();

		var user = new User
		{
			Phone = AdminPhone,
			UserType = UserType.Admin,
			Status = UserStatus.Approved,
			IsActive = true,
			IsPasswordSet = true,
			SecurityStamp = Guid.NewGuid().ToString(),
			CreatedAt = DateTime.UtcNow
		};

		user.PasswordHash = hasher.HashPassword(user, AdminPassword);
		db.Users.Add(user);
		await db.SaveChangesAsync();

		foreach (var code in permissionCodes)
		{
			var perm = await db.Permissions
				.FirstOrDefaultAsync(p => p.Code == code);

			if (perm == null)
			{
				perm = new Permission
				{
					Code = code,
					Name = code,
					Group = "Test",
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				};
				db.Permissions.Add(perm);
				await db.SaveChangesAsync();
			}

			db.UserPermissions.Add(new UserPermission
			{
				UserId = user.Id,
				PermissionId = perm.Id,
				IsGranted = true,
				CreatedAt = DateTime.UtcNow
			});
		}

		await db.SaveChangesAsync();
		return user.Id;
	}

	/// <summary>
	/// زرع موظف + الصلاحيات المطلوبة له
	/// </summary>
	public static async Task<int> SeedEmployeeWithPermissionsAsync(
		CustomWebApplicationFactory factory,
		params string[] permissionCodes)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var hasher = new PasswordHasher<User>();

		var user = new User
		{
			Phone = EmployeePhone,
			UserType = UserType.Employee,
			Status = UserStatus.Approved,
			IsActive = true,
			IsPasswordSet = true,
			SecurityStamp = Guid.NewGuid().ToString(),
			CreatedAt = DateTime.UtcNow
		};

		user.PasswordHash = hasher.HashPassword(user, EmployeePassword);
		db.Users.Add(user);
		await db.SaveChangesAsync();

		foreach (var code in permissionCodes)
		{
			var perm = await db.Permissions
				.FirstOrDefaultAsync(p => p.Code == code);

			if (perm == null)
			{
				perm = new Permission
				{
					Code = code,
					Name = code,
					Group = "Test",
					IsActive = true,
					CreatedAt = DateTime.UtcNow
				};
				db.Permissions.Add(perm);
				await db.SaveChangesAsync();
			}

			db.UserPermissions.Add(new UserPermission
			{
				UserId = user.Id,
				PermissionId = perm.Id,
				IsGranted = true,
				CreatedAt = DateTime.UtcNow
			});
		}

		await db.SaveChangesAsync();
		return user.Id;
	}

	/// <summary>
	/// زرع عميل + حساب مستخدم للبوابة — يرجّع customerId
	/// </summary>
	public static async Task<int> SeedCustomerWithUserAsync(
		CustomWebApplicationFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var customer = new Customer
		{
			Name = $"عميل بوابة {Guid.NewGuid():N}".Substring(0, 20),
			Phone = CustomerPhone,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Customers.Add(customer);
		await db.SaveChangesAsync();

		var hasher = new PasswordHasher<User>();
		var user = new User
		{
			Phone = CustomerPhone,
			UserType = UserType.Customer,
			CustomerId = customer.Id,
			Status = UserStatus.Approved,
			IsActive = true,
			IsPasswordSet = true,
			SecurityStamp = Guid.NewGuid().ToString(),
			CreatedAt = DateTime.UtcNow
		};
		user.PasswordHash = hasher.HashPassword(user, CustomerPassword);
		db.Users.Add(user);
		await db.SaveChangesAsync();

		return customer.Id;
	}

	/// <summary>
	/// زرع مورد + حساب مستخدم للبوابة — يرجّع supplierId
	/// </summary>
	public static async Task<int> SeedSupplierWithUserAsync(
		CustomWebApplicationFactory factory)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var supplier = new Supplier
		{
			Name = $"مورد بوابة {Guid.NewGuid():N}".Substring(0, 20),
			Phone = SupplierPhone,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		db.Suppliers.Add(supplier);
		await db.SaveChangesAsync();

		var hasher = new PasswordHasher<User>();
		var user = new User
		{
			Phone = SupplierPhone,
			UserType = UserType.Supplier,
			SupplierId = supplier.Id,
			Status = UserStatus.Approved,
			IsActive = true,
			IsPasswordSet = true,
			SecurityStamp = Guid.NewGuid().ToString(),
			CreatedAt = DateTime.UtcNow
		};
		user.PasswordHash = hasher.HashPassword(user, SupplierPassword);
		db.Users.Add(user);
		await db.SaveChangesAsync();

		return supplier.Id;
	}

	public static HttpClient CreateClient(CustomWebApplicationFactory factory)
	{
		return factory.CreateClient(new WebApplicationFactoryClientOptions
		{
			AllowAutoRedirect = false
		});
	}

	public static async Task<string> GetAntiForgeryTokenAsync(HttpClient client)
		=> await GetAntiForgeryTokenFromPageAsync(client, "/Account/Login");

	public static async Task<string> GetAntiForgeryTokenFromPageAsync(
		HttpClient client,
		string url)
	{
		var response = await client.GetAsync(url);
		response.EnsureSuccessStatusCode();

		var html = await response.Content.ReadAsStringAsync();

		var match = Regex.Match(
			html,
			@"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""",
			RegexOptions.IgnoreCase);

		if (!match.Success)
		{
			match = Regex.Match(
				html,
				@"value=""([^""]+)""[^>]*name=""__RequestVerificationToken""",
				RegexOptions.IgnoreCase);
		}

		if (!match.Success)
			throw new InvalidOperationException(
				$"__RequestVerificationToken not found at {url}.");

		return match.Groups[1].Value;
	}

	public static async Task<HttpResponseMessage> PostLoginAsync(
		HttpClient client,
		string username,
		string password)
	{
		var token = await GetAntiForgeryTokenAsync(client);

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["username"] = username,
			["password"] = password,
			["__RequestVerificationToken"] = token
		});

		return await client.PostAsync("/Account/Login", form);
	}

	/// <summary>
	/// تسجيل دخول كأدمن + إرجاع HttpClient جاهز
	/// </summary>
	public static async Task<HttpClient> CreateAuthenticatedClientAsync(
		CustomWebApplicationFactory factory)
	{
		return await CreateAuthenticatedClientInternalAsync(
			factory,
			AdminPhone,
			AdminPassword);
	}

	/// <summary>
	/// تسجيل دخول كموظف + إرجاع HttpClient جاهز
	/// </summary>
	public static async Task<HttpClient> CreateAuthenticatedEmployeeClientAsync(
		CustomWebApplicationFactory factory)
	{
		return await CreateAuthenticatedClientInternalAsync(
			factory,
			EmployeePhone,
			EmployeePassword);
	}

	/// <summary>
	/// تسجيل دخول كعميل + إرجاع HttpClient جاهز
	/// </summary>
	public static async Task<HttpClient> CreateAuthenticatedCustomerClientAsync(
		CustomWebApplicationFactory factory)
	{
		return await CreateAuthenticatedClientInternalAsync(
			factory,
			CustomerPhone,
			CustomerPassword);
	}

	/// <summary>
	/// تسجيل دخول كمورد + إرجاع HttpClient جاهز
	/// </summary>
	public static async Task<HttpClient> CreateAuthenticatedSupplierClientAsync(
		CustomWebApplicationFactory factory)
	{
		return await CreateAuthenticatedClientInternalAsync(
			factory,
			SupplierPhone,
			SupplierPassword);
	}

	private static async Task<HttpClient> CreateAuthenticatedClientInternalAsync(
		CustomWebApplicationFactory factory,
		string phone,
		string password)
	{
		var client = CreateClient(factory);

		var loginResponse = await PostLoginAsync(client, phone, password);

		if (loginResponse.StatusCode != System.Net.HttpStatusCode.Redirect &&
			loginResponse.StatusCode != System.Net.HttpStatusCode.Found &&
			loginResponse.StatusCode != System.Net.HttpStatusCode.SeeOther)
		{
			throw new InvalidOperationException(
				$"Login failed. Status={loginResponse.StatusCode}, " +
				$"Location={loginResponse.Headers.Location}");
		}

		var setCookies = loginResponse.Headers
			.Where(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
			.SelectMany(h => h.Value)
			.ToList();

		if (setCookies.Count == 0)
		{
			throw new InvalidOperationException(
				"Login succeeded but no Set-Cookie header was returned.");
		}

		var cookieHeader = string.Join("; ", setCookies
			.Select(c => c.Split(';')[0])
			.Where(c => !string.IsNullOrWhiteSpace(c)));

		client.DefaultRequestHeaders.Add("Cookie", cookieHeader);

		return client;
	}
}