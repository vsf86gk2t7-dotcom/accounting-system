using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingSystem.Tests.Integration;

public static class AttendanceTestHelpers
{
	/// <summary>
	/// زرع موظف نشط — يرجّع الـ ID
	/// </summary>
	public static async Task<int> SeedEmployeeAsync(
		CustomWebApplicationFactory factory,
		string name = "موظف اختبار")
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var employee = new Employee
		{
			Name = $"{name} {Guid.NewGuid():N}".Substring(0, 25),
			Phone = $"0100{Random.Shared.Next(1000000, 9999999)}",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};

		db.Employees.Add(employee);
		await db.SaveChangesAsync();

		return employee.Id;
	}

	/// <summary>
	/// زرع سجل حضور مباشرة (بدون HTTP)
	/// </summary>
	public static async Task<int> SeedAttendanceAsync(
		CustomWebApplicationFactory factory,
		int employeeId,
		DateTime? date = null,
		DateTime? checkIn = null,
		DateTime? checkOut = null,
		AttendanceStatus status = AttendanceStatus.Present)
	{
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

		var record = new Attendance
		{
			EmployeeId = employeeId,
			Date = (date ?? DateTime.Today).Date,
			CheckIn = checkIn,
			CheckOut = checkOut,
			Status = status,
			CreatedAt = DateTime.UtcNow
		};

		db.Attendances.Add(record);
		await db.SaveChangesAsync();

		return record.Id;
	}

	/// <summary>
	/// تسجيل حضور عبر HTTP
	/// </summary>
	public static async Task<HttpResponseMessage> CheckInViaHttpAsync(
		CustomWebApplicationFactory factory,
		HttpClient client,
		int employeeId)
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Attendance/Index");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["employeeId"] = employeeId.ToString(),
			["__RequestVerificationToken"] = token
		});

		return await client.PostAsync($"/Attendance/CheckIn/{employeeId}", form);
	}

	/// <summary>
	/// تسجيل انصراف عبر HTTP
	/// </summary>
	public static async Task<HttpResponseMessage> CheckOutViaHttpAsync(
		CustomWebApplicationFactory factory,
		HttpClient client,
		int employeeId)
	{
		var token = await IntegrationTestHelpers
			.GetAntiForgeryTokenFromPageAsync(client, "/Attendance/Index");

		var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["employeeId"] = employeeId.ToString(),
			["__RequestVerificationToken"] = token
		});

		return await client.PostAsync($"/Attendance/CheckOut/{employeeId}", form);
	}
}