using AccountingSystem.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AccountingSystem.Services.Jwt
{
	public class JwtTokenService : IJwtTokenService
	{
		private readonly IConfiguration _configuration;

		public JwtTokenService(
			IConfiguration configuration)
		{
			_configuration = configuration;
		}

		// =========================================
		// Access Token
		// =========================================

		public string CreateToken(User user)
		{
			var jwtSection =
				_configuration.GetSection("JwtSettings");

			var issuer =
				jwtSection["Issuer"]
				?? throw new InvalidOperationException(
					"JwtSettings:Issuer غير موجود.");

			var audience =
				jwtSection["Audience"]
				?? throw new InvalidOperationException(
					"JwtSettings:Audience غير موجود.");

			var secretKey =
				jwtSection["SecretKey"]
				?? throw new InvalidOperationException(
					"JwtSettings:SecretKey غير موجود.");

			if (secretKey.Length < 32)
			{
				throw new InvalidOperationException(
					"JwtSettings:SecretKey يجب أن يكون 32 حرفًا على الأقل.");
			}

			var accessTokenMinutes =
				GetAccessTokenMinutes();

			var now = DateTime.UtcNow;

			var claims = new List<Claim>
			{
				new Claim(
					JwtRegisteredClaimNames.Sub,
					user.Id.ToString()),

				new Claim(
					ClaimTypes.NameIdentifier,
					user.Id.ToString()),

				new Claim(
					JwtRegisteredClaimNames.Jti,
					Guid.NewGuid().ToString()),

				new Claim(
					ClaimTypes.MobilePhone,
					user.Phone),

				new Claim(
					ClaimTypes.Role,
					user.UserType.ToString())
			};

			if (!string.IsNullOrWhiteSpace(user.SecurityStamp))
			{
				claims.Add(
					new Claim(
						"SecurityStamp",
						user.SecurityStamp));
			}

			if (user.CustomerId.HasValue)
			{
				claims.Add(
					new Claim(
						"CustomerId",
						user.CustomerId.Value.ToString()));
			}

			if (user.SupplierId.HasValue)
			{
				claims.Add(
					new Claim(
						"SupplierId",
						user.SupplierId.Value.ToString()));
			}

			if (user.EmployeeId.HasValue)
			{
				claims.Add(
					new Claim(
						"EmployeeId",
						user.EmployeeId.Value.ToString()));
			}

			var key =
				new SymmetricSecurityKey(
					Encoding.UTF8.GetBytes(secretKey));

			var credentials =
				new SigningCredentials(
					key,
					SecurityAlgorithms.HmacSha256);

			var token =
				new JwtSecurityToken(
					issuer: issuer,
					audience: audience,
					claims: claims,
					notBefore: now,
					expires: now.AddMinutes(
						accessTokenMinutes),
					signingCredentials: credentials);

			return new JwtSecurityTokenHandler()
				.WriteToken(token);
		}

		// =========================================
		// Refresh Token
		// =========================================

		public string CreateRefreshToken(User user)
		{
			var jwtSection =
				_configuration.GetSection("JwtSettings");

			var issuer =
				jwtSection["Issuer"]
				?? throw new InvalidOperationException(
					"JwtSettings:Issuer غير موجود.");

			var audience =
				jwtSection["Audience"]
				?? throw new InvalidOperationException(
					"JwtSettings:Audience غير موجود.");

			var secretKey =
				jwtSection["SecretKey"]
				?? throw new InvalidOperationException(
					"JwtSettings:SecretKey غير موجود.");

			if (secretKey.Length < 32)
			{
				throw new InvalidOperationException(
					"JwtSettings:SecretKey يجب أن يكون 32 حرفًا على الأقل.");
			}

			var refreshTokenDays = GetRefreshTokenDays();

			var now = DateTime.UtcNow;

			var claims = new List<Claim>
			{
				new Claim(
					JwtRegisteredClaimNames.Sub,
					user.Id.ToString()),

				new Claim(
					ClaimTypes.NameIdentifier,
					user.Id.ToString()),

				new Claim(
					JwtRegisteredClaimNames.Jti,
					Guid.NewGuid().ToString()),

				new Claim("token_type", "refresh")
			};

			if (!string.IsNullOrWhiteSpace(user.SecurityStamp))
			{
				claims.Add(
					new Claim(
						"SecurityStamp",
						user.SecurityStamp));
			}

			var key =
				new SymmetricSecurityKey(
					Encoding.UTF8.GetBytes(secretKey));

			var credentials =
				new SigningCredentials(
					key,
					SecurityAlgorithms.HmacSha256);

			var token =
				new JwtSecurityToken(
					issuer: issuer,
					audience: audience,
					claims: claims,
					notBefore: now,
					expires: now.AddDays(refreshTokenDays),
					signingCredentials: credentials);

			return new JwtSecurityTokenHandler()
				.WriteToken(token);
		}

		// =========================================
		// Validate Refresh Token
		// =========================================

		public bool ValidateRefreshToken(User user, string refreshToken)
		{
			try
			{
				var jwtSection =
					_configuration.GetSection("JwtSettings");

				var issuer = jwtSection["Issuer"];
				var audience = jwtSection["Audience"];
				var secretKey = jwtSection["SecretKey"];

				var key =
					new SymmetricSecurityKey(
						Encoding.UTF8.GetBytes(secretKey!));

				var tokenHandler = new JwtSecurityTokenHandler();

				tokenHandler.ValidateToken(
					refreshToken,
					new TokenValidationParameters
					{
						ValidateIssuerSigningKey = true,
						IssuerSigningKey = key,
						ValidateIssuer = true,
						ValidIssuer = issuer,
						ValidateAudience = true,
						ValidAudience = audience,
						ValidateLifetime = true,
						ClockSkew = TimeSpan.FromSeconds(30)
					},
					out var validatedToken);

				if (validatedToken is not JwtSecurityToken jwtToken)
					return false;

				var tokenType = jwtToken.Claims
					.FirstOrDefault(x => x.Type == "token_type")?.Value;

				if (tokenType != "refresh")
					return false;

				var userId = jwtToken.Claims
					.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;

				if (userId != user.Id.ToString())
					return false;

				var securityStamp = jwtToken.Claims
					.FirstOrDefault(x => x.Type == "SecurityStamp")?.Value;

				if (securityStamp != user.SecurityStamp)
					return false;

				return true;
			}
			catch
			{
				return false;
			}
		}

		// =========================================
		// ✅ استخراج UserId من Refresh Token
		// =========================================

		public bool TryGetUserIdFromRefreshToken(
			string refreshToken,
			out int userId)
		{
			userId = 0;

			if (string.IsNullOrWhiteSpace(refreshToken))
			{
				return false;
			}

			try
			{
				var tokenHandler = new JwtSecurityTokenHandler();

				if (!tokenHandler.CanReadToken(refreshToken))
				{
					return false;
				}

				var jwtToken = tokenHandler.ReadJwtToken(refreshToken);

				// التأكد إنه Refresh Token وليس Access Token
				var tokenType = jwtToken.Claims
					.FirstOrDefault(x => x.Type == "token_type")?.Value;

				if (tokenType != "refresh")
				{
					return false;
				}

				// استخراج الـ UserId
				var idClaim = jwtToken.Claims
					.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value
					?? jwtToken.Claims
						.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Sub)?.Value;

				if (string.IsNullOrWhiteSpace(idClaim))
				{
					return false;
				}

				return int.TryParse(idClaim, out userId);
			}
			catch
			{
				return false;
			}
		}

		// =========================================
		// Lifetimes
		// =========================================

		public TimeSpan GetAccessTokenLifetime()
		{
			return TimeSpan.FromMinutes(
				GetAccessTokenMinutes());
		}

		public TimeSpan GetRefreshTokenLifetime()
		{
			return TimeSpan.FromDays(
				GetRefreshTokenDays());
		}

		// =========================================
		// Helpers
		// =========================================

		private int GetAccessTokenMinutes()
		{
			var configured =
				_configuration["JwtSettings:ExpiryMinutes"];

			if (int.TryParse(
					configured,
					out var minutes) &&
				minutes > 0)
			{
				return minutes;
			}

			return 60; // fallback = نفس قيمة appsettings
		}

		private int GetRefreshTokenDays()
		{
			var configured =
				_configuration["JwtSettings:RefreshTokenExpiryDays"];

			if (int.TryParse(
					configured,
					out var days) &&
				days > 0)
			{
				return days;
			}

			return 7; // fallback = نفس قيمة appsettings
		}
	}
}