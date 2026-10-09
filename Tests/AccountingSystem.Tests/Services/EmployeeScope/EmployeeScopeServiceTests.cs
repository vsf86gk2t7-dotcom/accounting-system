using AccountingSystem.Models;
using AccountingSystem.Services.EmployeeScope;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using EmployeeScopeModel = AccountingSystem.Services.EmployeeScope.EmployeeScope;

namespace AccountingSystem.Tests.EmployeeScopeTests;

public class EmployeeScopeServiceTests : BaseTest
{
	private readonly Mock<IHttpContextAccessor> _httpAccessorMock;
	private readonly EmployeeScopeService _sut;

	public EmployeeScopeServiceTests()
	{
		_httpAccessorMock = new Mock<IHttpContextAccessor>();
		_httpAccessorMock
			.Setup(x => x.HttpContext)
			.Returns((HttpContext?)null);

		_sut = new EmployeeScopeService(
			Context,
			_httpAccessorMock.Object);
	}

	private void SetHttpContext(int userId)
	{
		var context = new DefaultHttpContext();
		var claims = new List<Claim>
		{
			new(ClaimTypes.NameIdentifier, userId.ToString())
		};
		var identity = new ClaimsIdentity(claims, "TestAuth");
		context.User = new ClaimsPrincipal(identity);
		_httpAccessorMock.Setup(x => x.HttpContext).Returns(context);
	}

	private void SetUnauthenticatedContext()
	{
		var context = new DefaultHttpContext();
		context.User = new ClaimsPrincipal(new ClaimsIdentity());
		_httpAccessorMock.Setup(x => x.HttpContext).Returns(context);
	}

	private async Task<Role> SeedRoleAsync(string code)
	{
		var role = new Role { Name = code, Code = code, IsActive = true, CreatedAt = DateTime.UtcNow };
		Context.Roles.Add(role);
		await Context.SaveChangesAsync();
		return role;
	}

	private async Task<User> SeedUserAsync(int? employeeId = null, int? roleId = null)
	{
		var user = new User
		{
			Phone = $"0100000{Random.Shared.Next(1000, 9999)}",
			UserType = UserType.Employee,
			EmployeeId = employeeId,
			RoleId = roleId,
			Status = UserStatus.Approved,
			IsActive = true,
			IsPasswordSet = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Users.Add(user);
		await Context.SaveChangesAsync();
		return user;
	}

	private async Task<Employee> SeedEmployeeAsync()
	{
		var employee = new Employee
		{
			Name = "Test Employee",
			Phone = $"0100000{Random.Shared.Next(1000, 9999)}",
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.Employees.Add(employee);
		await Context.SaveChangesAsync();
		return employee;
	}

	private async Task AssignBranchToEmployeeAsync(int employeeId, int branchId)
	{
		Context.EmployeeBranches.Add(new EmployeeBranch { EmployeeId = employeeId, BranchId = branchId });
		await Context.SaveChangesAsync();
	}

	private async Task AssignStoreToEmployeeAsync(int employeeId, int storeId)
	{
		Context.EmployeeStores.Add(new EmployeeStore { EmployeeId = employeeId, StoreId = storeId });
		await Context.SaveChangesAsync();
	}

	[Fact]
	public async Task GetScopeAsync_WhenHttpContextIsNull_ReturnsUnrestricted()
	{
		var scope = await _sut.GetScopeAsync();
		scope.IsRestricted.Should().BeFalse();
		scope.IsAdmin.Should().BeFalse();
		scope.UserId.Should().Be(0);
	}

	[Fact]
	public async Task GetScopeAsync_WhenUserNotAuthenticated_ReturnsUnrestricted()
	{
		SetUnauthenticatedContext();
		var scope = await _sut.GetScopeAsync();
		scope.IsRestricted.Should().BeFalse();
		scope.IsAdmin.Should().BeFalse();
	}

	[Fact]
	public async Task GetScopeAsync_WhenUserIdClaimMissing_ReturnsUnrestricted()
	{
		var context = new DefaultHttpContext();
		var identity = new ClaimsIdentity(new[] { new Claim("other", "value") }, "TestAuth");
		context.User = new ClaimsPrincipal(identity);
		_httpAccessorMock.Setup(x => x.HttpContext).Returns(context);

		var scope = await _sut.GetScopeAsync();
		scope.IsRestricted.Should().BeFalse();
	}

	[Fact]
	public async Task GetScopeAsync_WhenUserIdClaimInvalid_ReturnsUnrestricted()
	{
		var context = new DefaultHttpContext();
		var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "not-a-number") }, "TestAuth");
		context.User = new ClaimsPrincipal(identity);
		_httpAccessorMock.Setup(x => x.HttpContext).Returns(context);

		var scope = await _sut.GetScopeAsync();
		scope.IsRestricted.Should().BeFalse();
	}

	[Fact]
	public async Task GetScopeAsync_WhenUserNotFound_ReturnsUnrestricted()
	{
		SetHttpContext(userId: 99999);
		var scope = await _sut.GetScopeAsync();
		scope.IsRestricted.Should().BeFalse();
		scope.UserId.Should().Be(99999);
	}

	[Fact]
	public async Task GetScopeAsync_WhenUserIsAdmin_ReturnsAdminScope()
	{
		var adminRole = await SeedRoleAsync("Admin");
		var user = await SeedUserAsync(roleId: adminRole.Id);
		SetHttpContext(user.Id);

		var scope = await _sut.GetScopeAsync();
		scope.IsAdmin.Should().BeTrue();
		scope.IsRestricted.Should().BeFalse();
		scope.UserId.Should().Be(user.Id);
	}

	[Fact]
	public async Task GetScopeAsync_WhenUserIsNotEmployee_ReturnsUnrestricted()
	{
		var user = await SeedUserAsync(employeeId: null);
		SetHttpContext(user.Id);

		var scope = await _sut.GetScopeAsync();
		scope.IsRestricted.Should().BeFalse();
		scope.IsAdmin.Should().BeFalse();
		scope.UserId.Should().Be(user.Id);
	}

	[Fact]
	public async Task GetScopeAsync_WhenUserIsInactive_ReturnsUnrestricted()
	{
		var user = await SeedUserAsync();
		user.IsActive = false;
		await Context.SaveChangesAsync();
		SetHttpContext(user.Id);

		var scope = await _sut.GetScopeAsync();
		scope.IsRestricted.Should().BeFalse();
	}

	[Fact]
	public async Task GetScopeAsync_WhenUserIsEmployee_ReturnsRestrictedScope()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employeeId: employee.Id);
		SetHttpContext(user.Id);

		var scope = await _sut.GetScopeAsync();
		scope.IsRestricted.Should().BeTrue();
		scope.EmployeeId.Should().Be(employee.Id);
		scope.UserId.Should().Be(user.Id);
	}

	[Fact]
	public async Task GetScopeAsync_WhenEmployeeHasBranches_IncludesBranchIds()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employeeId: employee.Id);
		await AssignBranchToEmployeeAsync(employee.Id, branchId: 10);
		await AssignBranchToEmployeeAsync(employee.Id, branchId: 20);
		await AssignBranchToEmployeeAsync(employee.Id, branchId: 30);
		SetHttpContext(user.Id);

		var scope = await _sut.GetScopeAsync();
		scope.BranchIds.Should().BeEquivalentTo(new[] { 10, 20, 30 });
	}

	[Fact]
	public async Task GetScopeAsync_WhenEmployeeHasStores_IncludesStoreIds()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employeeId: employee.Id);
		await AssignStoreToEmployeeAsync(employee.Id, storeId: 100);
		await AssignStoreToEmployeeAsync(employee.Id, storeId: 200);
		SetHttpContext(user.Id);

		var scope = await _sut.GetScopeAsync();
		scope.StoreIds.Should().BeEquivalentTo(new[] { 100, 200 });
	}

	[Fact]
	public async Task GetScopeAsync_WhenEmployeeHasNoAssignments_ReturnsEmptyLists()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employeeId: employee.Id);
		SetHttpContext(user.Id);

		var scope = await _sut.GetScopeAsync();
		scope.IsRestricted.Should().BeTrue();
		scope.BranchIds.Should().BeEmpty();
		scope.StoreIds.Should().BeEmpty();
	}

	[Fact]
	public async Task GetScopeAsync_CalledTwice_ReturnsSameInstance()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employeeId: employee.Id);
		await AssignBranchToEmployeeAsync(employee.Id, branchId: 10);
		SetHttpContext(user.Id);

		var scope1 = await _sut.GetScopeAsync();
		var scope2 = await _sut.GetScopeAsync();
		scope1.Should().BeSameAs(scope2);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public async Task CanAccessBranchAsync_WithInvalidId_ReturnsFalse(int branchId)
	{
		var result = await _sut.CanAccessBranchAsync(branchId);
		result.Should().BeFalse();
	}

	[Fact]
	public async Task CanAccessBranchAsync_WhenUnrestricted_ReturnsTrue()
	{
		var result = await _sut.CanAccessBranchAsync(branchId: 999);
		result.Should().BeTrue();
	}

	[Fact]
	public async Task CanAccessBranchAsync_WhenRestrictedAndHasBranch_ReturnsTrue()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employeeId: employee.Id);
		await AssignBranchToEmployeeAsync(employee.Id, branchId: 5);
		SetHttpContext(user.Id);

		var result = await _sut.CanAccessBranchAsync(branchId: 5);
		result.Should().BeTrue();
	}

	[Fact]
	public async Task CanAccessBranchAsync_WhenRestrictedAndNoBranch_ReturnsFalse()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employeeId: employee.Id);
		await AssignBranchToEmployeeAsync(employee.Id, branchId: 5);
		SetHttpContext(user.Id);

		var result = await _sut.CanAccessBranchAsync(branchId: 999);
		result.Should().BeFalse();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-100)]
	public async Task CanAccessStoreAsync_WithInvalidId_ReturnsFalse(int storeId)
	{
		var result = await _sut.CanAccessStoreAsync(storeId);
		result.Should().BeFalse();
	}

	[Fact]
	public async Task CanAccessStoreAsync_WhenUnrestricted_ReturnsTrue()
	{
		var result = await _sut.CanAccessStoreAsync(storeId: 999);
		result.Should().BeTrue();
	}

	[Fact]
	public async Task CanAccessStoreAsync_WhenRestrictedAndHasStore_ReturnsTrue()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employeeId: employee.Id);
		await AssignStoreToEmployeeAsync(employee.Id, storeId: 50);
		SetHttpContext(user.Id);

		var result = await _sut.CanAccessStoreAsync(storeId: 50);
		result.Should().BeTrue();
	}

	[Fact]
	public async Task CanAccessStoreAsync_WhenRestrictedAndNoStore_ReturnsFalse()
	{
		var employee = await SeedEmployeeAsync();
		var user = await SeedUserAsync(employeeId: employee.Id);
		await AssignStoreToEmployeeAsync(employee.Id, storeId: 50);
		SetHttpContext(user.Id);

		var result = await _sut.CanAccessStoreAsync(storeId: 999);
		result.Should().BeFalse();
	}

	[Fact]
	public void ApplyBranchFilter_WhenUnrestricted_ReturnsSameQuery()
	{
		var query = Context.Branches.AsQueryable();
		var scope = EmployeeScopeModel.Unrestricted(userId: 1);
		var result = _sut.ApplyBranchFilter(query, scope);
		result.Should().BeSameAs(query);
	}

	[Fact]
	public void ApplyBranchFilter_WhenAdmin_ReturnsSameQuery()
	{
		var query = Context.Branches.AsQueryable();
		var scope = EmployeeScopeModel.Admin(userId: 1);
		var result = _sut.ApplyBranchFilter(query, scope);
		result.Should().BeSameAs(query);
	}

	[Fact]
	public async Task ApplyBranchFilter_WhenRestrictedWithIds_FiltersCorrectly()
	{
		for (int i = 1; i <= 5; i++)
		{
			Context.Branches.Add(new Branch
			{
				Id = i, Name = $"Branch {i}", CompanyId = 1,
				IsActive = true, CreatedAt = DateTime.UtcNow
			});
		}
		await Context.SaveChangesAsync();

		var query = Context.Branches.AsQueryable();
		var scope = EmployeeScopeModel.Restricted(
			userId: 1, employeeId: 1,
			branchIds: new[] { 2, 4 },
			storeIds: Array.Empty<int>());

		var result = await _sut.ApplyBranchFilter(query, scope).ToListAsync();
		result.Should().HaveCount(2);
		result.Select(x => x.Id).Should().BeEquivalentTo(new[] { 2, 4 });
	}

	[Fact]
	public async Task ApplyBranchFilter_WhenRestrictedWithNoIds_ReturnsEmpty()
	{
		for (int i = 1; i <= 3; i++)
		{
			Context.Branches.Add(new Branch
			{
				Id = i, Name = $"Branch {i}", CompanyId = 1,
				IsActive = true, CreatedAt = DateTime.UtcNow
			});
		}
		await Context.SaveChangesAsync();

		var query = Context.Branches.AsQueryable();
		var scope = EmployeeScopeModel.Restricted(
			userId: 1, employeeId: 1,
			branchIds: Array.Empty<int>(),
			storeIds: Array.Empty<int>());

		var result = await _sut.ApplyBranchFilter(query, scope).ToListAsync();
		result.Should().BeEmpty();
	}

	[Fact]
	public void ApplyStoreFilter_WhenUnrestricted_ReturnsSameQuery()
	{
		var query = Context.Stores.AsQueryable();
		var scope = EmployeeScopeModel.Unrestricted(userId: 1);
		var result = _sut.ApplyStoreFilter(query, scope);
		result.Should().BeSameAs(query);
	}

	[Fact]
	public async Task ApplyStoreFilter_WhenRestrictedWithIds_FiltersCorrectly()
	{
		for (int i = 1; i <= 5; i++)
		{
			Context.Stores.Add(new Store
			{
				Id = i, Name = $"Store {i}", BranchId = 1,
				IsActive = true, CreatedAt = DateTime.UtcNow
			});
		}
		await Context.SaveChangesAsync();

		var query = Context.Stores.AsQueryable();
		var scope = EmployeeScopeModel.Restricted(
			userId: 1, employeeId: 1,
			branchIds: Array.Empty<int>(),
			storeIds: new[] { 1, 3, 5 });

		var result = await _sut.ApplyStoreFilter(query, scope).ToListAsync();
		result.Should().HaveCount(3);
		result.Select(x => x.Id).Should().BeEquivalentTo(new[] { 1, 3, 5 });
	}

	[Fact]
	public async Task ApplyStoreFilter_WhenRestrictedWithNoIds_ReturnsEmpty()
	{
		for (int i = 1; i <= 3; i++)
		{
			Context.Stores.Add(new Store
			{
				Id = i, Name = $"Store {i}", BranchId = 1,
				IsActive = true, CreatedAt = DateTime.UtcNow
			});
		}
		await Context.SaveChangesAsync();

		var query = Context.Stores.AsQueryable();
		var scope = EmployeeScopeModel.Restricted(
			userId: 1, employeeId: 1,
			branchIds: Array.Empty<int>(),
			storeIds: Array.Empty<int>());

		var result = await _sut.ApplyStoreFilter(query, scope).ToListAsync();
		result.Should().BeEmpty();
	}

	[Fact]
	public void EmployeeScope_Unrestricted_CanAccessAnything()
	{
		var scope = EmployeeScopeModel.Unrestricted(userId: 1);
		scope.CanAccessBranch(999).Should().BeTrue();
		scope.CanAccessStore(999).Should().BeTrue();
		scope.IsRestricted.Should().BeFalse();
	}

	[Fact]
	public void EmployeeScope_Admin_CanAccessAnything()
	{
		var scope = EmployeeScopeModel.Admin(userId: 1);
		scope.CanAccessBranch(999).Should().BeTrue();
		scope.CanAccessStore(999).Should().BeTrue();
		scope.IsAdmin.Should().BeTrue();
	}

	[Fact]
	public void EmployeeScope_Restricted_OnlyAllowsListedItems()
	{
		var scope = EmployeeScopeModel.Restricted(
			userId: 1, employeeId: 10,
			branchIds: new[] { 1, 2 },
			storeIds: new[] { 100 });

		scope.CanAccessBranch(1).Should().BeTrue();
		scope.CanAccessBranch(2).Should().BeTrue();
		scope.CanAccessBranch(3).Should().BeFalse();
		scope.CanAccessStore(100).Should().BeTrue();
		scope.CanAccessStore(200).Should().BeFalse();
	}
}