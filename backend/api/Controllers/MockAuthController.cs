using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace backend.api.Controllers;

[ApiController]
[Route("api/auth")]
public class MockAuthController : ControllerBase
{
    private readonly IConfiguration configuration;

    public MockAuthController(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    [HttpPost("mock-login")]
    public IActionResult MockLogin([FromBody] MockLoginRequest request)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, request.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("role", "authenticated"),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            configuration["Jwt:Key"] ?? "TankX-Dev-Secret-Key-For-Testing-Only-2026!"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "tankx-mock",
            audience: "tankx",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return Ok(new
        {
            access_token = new JwtSecurityTokenHandler().WriteToken(token),
            expires_in = 3600,
            token_type = "bearer",
        });
    }
}

public record MockLoginRequest(string Email, string Password);
