using AccountingSystem.Models;
using AccountingSystem.Services.Jwt;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AccountingSystem.Tests
{
	public class JwtTokenServiceTests
	{
		private readonly IJwtTokenService _jwtTokenService;

		public JwtTokenServiceTests()
		{
			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["JwtSettings:Issuer"] = "TestIssuer",
					["JwtSettings:Audience"] = "TestAudience",
					["JwtSettings:SecretKey"] = "ThisIsASecretKeyThatIsAtLeast32Chars!",
					["JwtSettings:AccessTokenMinutes"] = "30",
					["JwtSettings:RefreshTokenDays"] = "7"
				})
				.Build();

			_jwtTokenService = new JwtTokenService(configuration);
		}

		private static User CreateTestUser() => new()
		{
			Id = 1,
			Phone = "01000000000",
			UserType = UserType.Admin,
			SecurityStamp = "test-stamp"
		};

		[Fact]
		public void CreateToken_ReturnsValidToken()
		{
			var user = CreateTestUser();

			var token = _jwtTokenService.CreateToken(user);

			Assert.False(string.IsNullOrWhiteSpace(token));
			Assert.Contains('.', token);
		}

		[Fact]
		public void CreateRefreshToken_ReturnsValidToken()
		{
			var user = CreateTestUser();

			var token = _jwtTokenService.CreateRefreshToken(user);

			Assert.False(string.IsNullOrWhiteSpace(token));
			Assert.Contains('.', token);
		}

		[Fact]
		public void ValidateRefreshToken_WithValidToken_ReturnsTrue()
		{
			var user = CreateTestUser();
			var token = _jwtTokenService.CreateRefreshToken(user);

			var isValid = _jwtTokenService.ValidateRefreshToken(user, token);

			Assert.True(isValid);
		}

		[Fact]
		public void ValidateRefreshToken_WithInvalidToken_ReturnsFalse()
		{
			var user = CreateTestUser();

			var isValid = _jwtTokenService.ValidateRefreshToken(user, "invalid-token");

			Assert.False(isValid);
		}

		[Fact]
		public void ValidateRefreshToken_WithWrongUser_ReturnsFalse()
		{
			var user = CreateTestUser();
			var token = _jwtTokenService.CreateRefreshToken(user);

			var differentUser = new User
			{
				Id = 2,
				Phone = "01000000001",
				UserType = UserType.Admin,
				SecurityStamp = "test-stamp"
			};

			var isValid = _jwtTokenService.ValidateRefreshToken(differentUser, token);

			Assert.False(isValid);
		}

		[Fact]
		public void GetAccessTokenLifetime_ReturnsCorrectValue()
		{
			var lifetime = _jwtTokenService.GetAccessTokenLifetime();

			Assert.Equal(TimeSpan.FromMinutes(30), lifetime);
		}

		[Fact]
		public void GetRefreshTokenLifetime_ReturnsCorrectValue()
		{
			var lifetime = _jwtTokenService.GetRefreshTokenLifetime();

			Assert.Equal(TimeSpan.FromDays(7), lifetime);
		}
	}
}
