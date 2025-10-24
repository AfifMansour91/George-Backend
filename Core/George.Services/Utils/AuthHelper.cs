using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using George.Common;
using George.DB;

namespace George.Services
{
	public class AuthHelper
	{
		//*********************  Data members/Constants  *********************//
		public const int INVALID_ID = -1;
		private readonly IConfiguration _configuration;
		private readonly ILogger _logger;
		private readonly string _tokenIssuer;
		private readonly string _tokenAudience;
		private readonly double _accessTokenExpirationInMinutes = 15;
		private readonly double _refreshTokenExpirationInMinutes = 150;


		//**************************    Construction    **************************//
		public AuthHelper(IConfiguration configuration, ILogger<AuthHelper> logger)
		{
			_configuration = configuration;
			_logger = logger;

			_tokenIssuer = _configuration["Auth:Jwt:Issuer"];
			_tokenAudience = _configuration["Auth:Jwt:Audience"];
			_accessTokenExpirationInMinutes = _configuration["Auth:Jwt:AccessTokenExpirationInMin"].ToDouble(1314000); // 1 year
			_refreshTokenExpirationInMinutes = _configuration["Auth:Jwt:RefreshTokenExpirationInMin"].ToDouble(2 * 1314000); // 2 years
		}


		//*************************    Public Methods    *************************//
		public string GetUserIdFromToken(string token)
		{
			var jwtToken = new JwtSecurityToken(token);

			string gameId = "";
			foreach (var c in jwtToken.Claims)
			{
				if (c.Type == CustomClaimType.UserId)
				{
					gameId = c.Value;
					break;
				}
			}

			return gameId;
		}

		public int GetUserIdFromExpiredToken(string token)
		{
			int userId = INVALID_ID;

			var principal = GetPrincipalFromAccessToken(token, false);
			if (principal != null)
			{
				foreach (var c in principal.Claims)
				{
					if (c.Type == CustomClaimType.UserId)
					{
						if (c.Value.IsNumeric())
							userId = c.Value.ToInt();
						break;
					}
				}
			}

			return userId;
		}


		public string GetClaimFromToken(string token, string claimType)
		{
			var jwtToken = new JwtSecurityToken(token);

			var value = "";
			foreach (var c in jwtToken.Claims)
				if (c.Type == claimType)
				{
					value = c.Value;
					break;
				}
			return value;
		}

		public AuthRes CreateAuthenticationToken(int userId, bool isMaster/*, UserRole role = UserRole.None*/)
		{
			// Add claims.
			var claims = new List<Claim>();
			claims.Add(new Claim(CustomClaimType.Authorized, "1"));
			//claims.Add(new Claim(CustomClaimType.Id, accountId.ToString()));
			claims.Add(new Claim(CustomClaimType.UserId, userId.ToString()));
			claims.Add(new Claim(CustomClaimType.IsMaster, isMaster.ToString()));
			//claims.Add(new Claim(CustomClaimType.Role, role.ToString()));
			//if (role != UserRole.None)
			//	claims.Add(new Claim(ClaimTypes.Role, role.ToString()));

			// Create the token and response.
			var response = CreateAuthenticationToken(claims, userId/*, role*/);

			//// Set the role.
			//response.RoleId = (UserRole)user.RoleId;

			return response;
		}

		public string GenerateRefreshToken()
		{
			return Guid.NewGuid().ToStringFormatted();
		}

		//public ResetPasswordTokenResponse CreateResetPasswordToken(string username)
		//{
		//	ResetPasswordTokenResponse response = new ResetPasswordTokenResponse();

		//	// Create the token string.
		//	string token = Guid.NewGuid().ToString() + "-" + username;

		//	// Encrypt it.
		//	response.Token = DataSecureHelper.Encrypt(token);

		//	// Set expiration.
		//	response.Expiration = DateTime.Now.AddMinutes(Convert.ToDouble(_configuration["Identity:ResetPasswordExpireMinutes"]));

		//	return response;
		//}

		public void DeleteClaims()
		{
			JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
		}


		//*************************    Private Methods    *************************//

		private SymmetricSecurityKey GenerateKey()
		{
			return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Auth:Jwt:Key"]));
		}

		private AuthRes CreateAuthenticationToken(List<Claim> claims, int userId /*UserRole role,*/)
		{
			DateTime tokenExpiration = DateTime.UtcNow.AddMinutes(_accessTokenExpirationInMinutes);
			DateTime refreshTokenExpiration = DateTime.UtcNow.AddMinutes(_refreshTokenExpirationInMinutes);

			// Create the token's credentials.
			var key = GenerateKey();
			var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

			// Create the token.
			var token = new JwtSecurityToken(
				_tokenIssuer,
				_tokenAudience,
				claims,
				expires: tokenExpiration,
				signingCredentials: credentials
			);

			// Create the refresh token.
			var refreshToken = GenerateRefreshToken();

			// Set the response.
			var response = new AuthRes {
				UserId = userId,
				//RoleId = role,
				AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
				AccessTokenExpiration = tokenExpiration,
				RefreshToken = refreshToken,
				RefreshTokenExpiration = refreshTokenExpiration
			};

			return response;
		}

		private ClaimsPrincipal GetPrincipalFromAccessToken(string token, bool validateLifetime = true)
		{
			var tokenValidationParameters = new TokenValidationParameters {
				ValidAudience = _tokenAudience,
				ValidateAudience = true,
				ValidIssuer = _tokenIssuer,
				ValidateIssuer = true,
				ValidateIssuerSigningKey = true,
				IssuerSigningKey = GenerateKey(),
				ValidateLifetime = validateLifetime // Ignore the token's expiration date?.
			};

			var tokenHandler = new JwtSecurityTokenHandler();
			SecurityToken securityToken;
			var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out securityToken);
			var jwtSecurityToken = securityToken as JwtSecurityToken;
			if (jwtSecurityToken == null || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
				return null;//throw new SecurityTokenException("Invalid token");

			return principal;
		}
	}
}