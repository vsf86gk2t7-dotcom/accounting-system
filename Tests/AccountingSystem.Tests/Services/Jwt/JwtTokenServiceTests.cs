using AccountingSystem.Models;
using AccountingSystem.Services.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AccountingSystem.Tests.Services.Jwt;

public class JwtTokenServiceTests
{
	private const string TestSecretKey =
		"TestSecretKeyForUnitTestsOnly1234567890ABCDEF"; // 44+ chars

	private readonly JwtTokenService _sut;

	public JwtTokenServiceTests()
	{
		_sut = new JwtTokenService(BuildConfiguration());
	}

	// =========================================
	// Helper: بناء IConfiguration للاختبارات
	// =========================================

	private static IConfiguration BuildConfiguration(
		string? issuer = null,
		string? audience = null,
		string? secretKey = null,
		string? accessTokenMinutes = null,
		string? refreshTokenDays = null)
	{
		var settings = new Dictionary<string, string?>
		{
			["JwtSettings:Issuer"] = issuer ?? "TestIssuer",
			["JwtSettings:Audience"] = audience ?? "TestAudience",
			["JwtSettings:SecretKey"] = secretKey ?? TestSecretKey,
			["JwtSettings:AccessTokenMinutes"] = accessTokenMinutes ?? "30",
			["JwtSettings:RefreshTokenDays"] = refreshTokenDays ?? "7"
		};

		return new ConfigurationBuilder()
			.AddInMemoryCollection(settings)
			.Build();
	}

	private static User CreateTestUser(int id = 1)
	{
		return new User
		{
			Id = id,
			Phone = "01000000000",
			UserType = UserType.Admin,
			SecurityStamp = "stamp-" + id,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
	}

	// =========================================
	// 1. CreateToken — الحالة الناجحة
	// =========================================

	[Fact]
	public void CreateToken_WithValidUser_ReturnsNonEmptyJwt()
	{
		var user = CreateTestUser();

		var token = _sut.CreateToken(user);

		token.Should().NotBeNullOrWhiteSpace();
	}

	[Fact]
	public void CreateToken_ReturnsValidJwtStructure()
	{
		var user = CreateTestUser();

		var token = _sut.CreateToken(user);

		var handler = new JwtSecurityTokenHandler();
		handler.CanReadToken(token).Should().BeTrue();

		var jwt = handler.ReadJwtToken(token);
		jwt.Issuer.Should().Be("TestIssuer");
		jwt.Audiences.Should().Contain("TestAudience");
	}

	[Fact]
	public void CreateToken_ContainsRequiredClaims()
	{
		var user = CreateTestUser(id: 42);

		var token = _sut.CreateToken(user);

		var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

		jwt.Claims.Should().Contain(c =>
			c.Type == ClaimTypes.NameIdentifier && c.Value == "42");
		jwt.Claims.Should().Contain(c =>
			c.Type == JwtRegisteredClaimNames.Sub && c.Value == "42");
		jwt.Claims.Should().Contain(c =>
			c.Type == ClaimTypes.MobilePhone && c.Value == "01000000000");
		jwt.Claims.Should().Contain(c =>
			c.Type == ClaimTypes.Role);
		jwt.Claims.Should().Contain(c =>
			c.Type == JwtRegisteredClaimNames.Jti);
	}

	[Fact]
	public void CreateToken_ContainsSecurityStamp_WhenPresent()
	{
		var user = CreateTestUser(id: 7);

		var token = _sut.CreateToken(user);

		var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

		jwt.Claims.Should().Contain(c =>
			c.Type == "SecurityStamp" && c.Value == "stamp-7");
	}

	[Fact]
	public void CreateToken_ContainsCustomerId_WhenPresent()
	{
		var user = CreateTestUser();
		user.CustomerId = 500;

		var token = _sut.CreateToken(user);

		var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

		jwt.Claims.Should().Contain(c =>
			c.Type == "CustomerId" && c.Value == "500");
	}

	[Fact]
	public void CreateToken_ContainsEmployeeId_WhenPresent()
	{
		var user = CreateTestUser();
		user.EmployeeId = 99;

		var token = _sut.CreateToken(user);

		var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

		jwt.Claims.Should().Contain(c =>
			c.Type == "EmployeeId" && c.Value == "99");
	}

	[Fact]
	public void CreateToken_DoesNotContainCustomerId_WhenMissing()
	{
		var user = CreateTestUser();
		user.CustomerId = null;

		var token = _sut.CreateToken(user);

		var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

		jwt.Claims.Should().NotContain(c => c.Type == "CustomerId");
	}

	// =========================================
	// 2. CreateToken — حالات الفشل
	// =========================================

	[Fact]
	public void CreateToken_ThrowsWhenSecretKeyMissing()
	{
		var sut = new JwtTokenService(
			BuildConfiguration(secretKey: ""));

		var user = CreateTestUser();

		// SecretKey = "" → IsNullOrWhiteSpace → Exception
		Action act = () => sut.CreateToken(user);

		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void CreateToken_ThrowsWhenSecretKeyTooShort()
	{
		var sut = new JwtTokenService(
			BuildConfiguration(secretKey: "short"));

		var user = CreateTestUser();

		Action act = () => sut.CreateToken(user);

		act.Should().Throw<InvalidOperationException>()
			.WithMessage("*32*");
	}

	// =========================================
	// 3. CreateRefreshToken
	// =========================================

	[Fact]
	public void CreateRefreshToken_ReturnsNonEmptyToken()
	{
		var user = CreateTestUser();

		var token = _sut.CreateRefreshToken(user);

		token.Should().NotBeNullOrWhiteSpace();
	}

	[Fact]
	public void CreateRefreshToken_HasRefreshTokenTypeClaim()
	{
		var user = CreateTestUser();

		var token = _sut.CreateRefreshToken(user);

		var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

		jwt.Claims.Should().Contain(c =>
			c.Type == "token_type" && c.Value == "refresh");
	}

	[Fact]
	public void CreateRefreshToken_GeneratesDifferentTokensEachCall()
	{
		var user = CreateTestUser();

		var token1 = _sut.CreateRefreshToken(user);
		var token2 = _sut.CreateRefreshToken(user);

		// Jti مختلف في كل مرة → التوكنات مختلفة
		token1.Should().NotBe(token2);
	}

	// =========================================
	// 4. ValidateRefreshToken
	// =========================================

	[Fact]
	public void ValidateRefreshToken_ReturnsTrueForValidToken()
	{
		var user = CreateTestUser();

		var refreshToken = _sut.CreateRefreshToken(user);

		var result = _sut.ValidateRefreshToken(user, refreshToken);

		result.Should().BeTrue();
	}

	[Fact]
	public void ValidateRefreshToken_ReturnsFalseForDifferentUser()
	{
		var user1 = CreateTestUser(id: 1);
		var user2 = CreateTestUser(id: 2);

		var refreshToken = _sut.CreateRefreshToken(user1);

		var result = _sut.ValidateRefreshToken(user2, refreshToken);

		result.Should().BeFalse();
	}

	[Fact]
	public void ValidateRefreshToken_ReturnsFalseForAccessToken()
	{
		var user = CreateTestUser();

		var accessToken = _sut.CreateToken(user);

		var result = _sut.ValidateRefreshToken(user, accessToken);

		// Access token مفيهوش token_type="refresh"
		result.Should().BeFalse();
	}

	[Fact]
	public void ValidateRefreshToken_ReturnsFalseForGarbageToken()
	{
		var user = CreateTestUser();

		var result = _sut.ValidateRefreshToken(
			user, "this-is-not-a-jwt");

		result.Should().BeFalse();
	}

	[Fact]
	public void ValidateRefreshToken_ReturnsFalseWhenSecurityStampChanged()
	{
		var user = CreateTestUser();

		var refreshToken = _sut.CreateRefreshToken(user);

		// تغيير SecurityStamp (زي logout أو تغيير كلمة مرور)
		user.SecurityStamp = "different-stamp";

		var result = _sut.ValidateRefreshToken(user, refreshToken);

		result.Should().BeFalse();
	}

	// =========================================
	// 5. GetAccessTokenLifetime / GetRefreshTokenLifetime
	// =========================================

	[Fact]
	public void GetAccessTokenLifetime_ReturnsConfiguredValue()
	{
		var sut = new JwtTokenService(
			BuildConfiguration(accessTokenMinutes: "45"));

		var lifetime = sut.GetAccessTokenLifetime();

		lifetime.Should().Be(TimeSpan.FromMinutes(45));
	}

	[Fact]
	public void GetAccessTokenLifetime_Returns30MinutesByDefault()
	{
		var sut = new JwtTokenService(
			BuildConfiguration(accessTokenMinutes: "0"));

		var lifetime = sut.GetAccessTokenLifetime();

		lifetime.Should().Be(TimeSpan.FromMinutes(30));
	}

	[Fact]
	public void GetRefreshTokenLifetime_ReturnsConfiguredValue()
	{
		var sut = new JwtTokenService(
			BuildConfiguration(refreshTokenDays: "14"));

		var lifetime = sut.GetRefreshTokenLifetime();

		lifetime.Should().Be(TimeSpan.FromDays(14));
	}

	[Fact]
	public void GetRefreshTokenLifetime_Returns7DaysByDefault()
	{
		var sut = new JwtTokenService(
			BuildConfiguration(refreshTokenDays: "0"));

		var lifetime = sut.GetRefreshTokenLifetime();

		lifetime.Should().Be(TimeSpan.FromDays(7));
	}
}