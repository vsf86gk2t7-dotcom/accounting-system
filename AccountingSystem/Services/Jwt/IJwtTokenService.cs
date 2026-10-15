using AccountingSystem.Models;

namespace AccountingSystem.Services.Jwt
{
	public interface IJwtTokenService
	{
		string CreateToken(User user);

		string CreateRefreshToken(User user);

		bool ValidateRefreshToken(User user, string refreshToken);

		TimeSpan GetAccessTokenLifetime();

		TimeSpan GetRefreshTokenLifetime();
	}
}
