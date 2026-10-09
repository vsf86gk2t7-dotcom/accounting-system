using AccountingSystem.Models;

namespace AccountingSystem.Services.Jwt
{
	public interface IJwtTokenService
	{
		/// <summary>ينشئ Access Token للمستخدم.</summary>
		string CreateToken(User user);

		/// <summary>ينشئ Refresh Token للمستخدم.</summary>
		string CreateRefreshToken(User user);

		/// <summary>يتحقق من صلاحية Refresh Token الخاص بالمستخدم.</summary>
		bool ValidateRefreshToken(User user, string refreshToken);

		/// <summary>يستخرج UserId من الـ Refresh Token.</summary>
		bool TryGetUserIdFromRefreshToken(string refreshToken, out int userId);

		/// <summary>مدة صلاحية الـ Access Token.</summary>
		TimeSpan GetAccessTokenLifetime();

		/// <summary>مدة صلاحية الـ Refresh Token.</summary>
		TimeSpan GetRefreshTokenLifetime();
	}
}