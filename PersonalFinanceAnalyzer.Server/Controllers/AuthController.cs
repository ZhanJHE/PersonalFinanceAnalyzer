using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceAnalyzer.Server.Data;
using PersonalFinanceAnalyzer.Server.Models;
using PersonalFinanceAnalyzer.Server.Services;

namespace PersonalFinanceAnalyzer.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;

    public AuthController(AppDbContext db, JwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public record RegisterRequest(string Username, string Password);
    public record LoginRequest(string Username, string Password);
    public record AuthResponse(bool Success, string? Token, string? Message);

    [EnableRateLimiting("RegisterPolicy")]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new AuthResponse(false, null, "用户名和密码不能为空。"));

        if (request.Password.Length < 6)
            return BadRequest(new AuthResponse(false, null, "密码长度至少6位。"));

        var exists = await _db.Users.AnyAsync(u => u.Username == request.Username);
        if (exists)
            return Conflict(new AuthResponse(false, null, "用户名已存在。"));

        var user = new User
        {
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsMember = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _jwt.GenerateToken(user.Id, user.Username, user.IsMember);
        return Ok(new AuthResponse(true, token, "注册成功。"));
    }

    [EnableRateLimiting("LoginPolicy")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user == null)
            return Unauthorized(new AuthResponse(false, null, "用户名或密码错误。"));

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new AuthResponse(false, null, "用户名或密码错误。"));

        var token = _jwt.GenerateToken(user.Id, user.Username, user.IsMember);
        return Ok(new AuthResponse(true, token, "登录成功。"));
    }
}
