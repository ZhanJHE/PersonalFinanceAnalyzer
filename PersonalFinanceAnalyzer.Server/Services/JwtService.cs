using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace PersonalFinanceAnalyzer.Server.Services;

public class JwtService
{
    private readonly string _secretKey;
    private readonly string _issuer;

    public JwtService(IConfiguration config)
    {
        _secretKey = config["Jwt:SecretKey"] ?? "DefaultSuperSecretKeyAtLeast32CharsLong!";
        _issuer = config["Jwt:Issuer"] ?? "PersonalFinanceAnalyzer";
    }

    public string GenerateToken(int userId, string username, bool isMember)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim("IsMember", isMember.ToString().ToLower())
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
