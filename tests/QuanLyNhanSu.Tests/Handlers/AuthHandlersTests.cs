using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using QuanLyNhanSuMicroservice.Core.Repositories;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Application.Command.Auth;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Application.Handlers.AuthHandler;
using QuanLyNhanSuMicroservice.QuanLyNhanVien.Domain.Entities;

namespace QuanLyNhanSu.Tests.Handlers;

public class AuthHandlersTests
{
    private const string Password = "unit-test-password";
    private readonly Mock<ITaiKhoanRepository> _repo = new();

    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "UnitTestSigningKeyThatIsLongEnoughForHmacSha256!!",
            ["Jwt:Issuer"] = "UnitTestIssuer",
            ["Jwt:Audience"] = "UnitTestAudience",
        })
        .Build();

    private TaiKhoan SeedUser(string role = "Admin")
    {
        var user = new TaiKhoan("admin", BCrypt.Net.BCrypt.HashPassword(Password), role);
        _repo.Setup(r => r.GetByUsernameAsync("admin")).ReturnsAsync(user);
        return user;
    }

    [Fact]
    public async Task Login_DungMatKhau_TraJwtCoClaimVaIssuer()
    {
        var user = SeedUser();

        var res = await new LoginHandler(_repo.Object, Config()).Handle(new LoginRequestDTO("admin", Password), CancellationToken.None);

        // Xác thực token y như các service làm (AuthenticationExtensions): issuer, audience, hạn, chữ ký.
        // Dùng JsonWebTokenHandler (cùng phiên bản với Microsoft.IdentityModel.Tokens) thay vì
        // JwtSecurityTokenHandler.ReadJwtToken: project đang có System.IdentityModel.Tokens.Jwt 7.x chạy cùng
        // Microsoft.IdentityModel.Tokens 8.x nên đọc payload bằng handler cũ bị thiếu claim (token thật vẫn đúng).
        var result = await new JsonWebTokenHandler { MapInboundClaims = true }.ValidateTokenAsync(res.Token, ServiceValidationParameters());

        Assert.True(result.IsValid, result.Exception?.Message);
        var jwt = (JsonWebToken)result.SecurityToken;
        Assert.Equal("UnitTestIssuer", jwt.Issuer);
        Assert.Contains("UnitTestAudience", jwt.Audiences);
        Assert.Equal(user.Id.ToString(), result.ClaimsIdentity.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("admin", result.ClaimsIdentity.FindFirst(ClaimTypes.Name)?.Value);
        Assert.Equal("Admin", result.ClaimsIdentity.FindFirst(ClaimTypes.Role)?.Value);
        Assert.InRange(jwt.ValidTo, DateTime.UtcNow.AddHours(1.9), DateTime.UtcNow.AddHours(2.1));
    }

    [Fact]
    public async Task Login_TokenKhongHopLeVoiKhoaKhac()
    {
        SeedUser();
        var res = await new LoginHandler(_repo.Object, Config()).Handle(new LoginRequestDTO("admin", Password), CancellationToken.None);

        var parameters = ServiceValidationParameters();
        parameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("AnotherSigningKeyThatIsAlsoLongEnough!!!!!!"));
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(res.Token, parameters);

        Assert.False(result.IsValid);
    }

    private static TokenValidationParameters ServiceValidationParameters()
    {
        var config = Config();
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidAudience = config["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)),
        };
    }

    [Fact]
    public async Task Login_SaiMatKhau_BaoLoi()
    {
        SeedUser();

        var ex = await Assert.ThrowsAsync<Exception>(() =>
            new LoginHandler(_repo.Object, Config()).Handle(new LoginRequestDTO("admin", "sai"), CancellationToken.None));
        Assert.Contains("không đúng", ex.Message);
    }

    [Fact]
    public async Task Login_KhongCoTaiKhoan_BaoLoi()
    {
        await Assert.ThrowsAsync<Exception>(() =>
            new LoginHandler(_repo.Object, Config()).Handle(new LoginRequestDTO("khongco", Password), CancellationToken.None));
    }

    [Fact]
    public async Task Register_TaiKhoanMoi_LuuMatKhauDaHash()
    {
        TaiKhoan? saved = null;
        _repo.Setup(r => r.AddAsync(It.IsAny<TaiKhoan>())).Callback<TaiKhoan>(t => saved = t).Returns(Task.CompletedTask);

        var ok = await new RegisterHandler(_repo.Object).Handle(new RegisterRequestDTO("moi", Password, "Admin"), CancellationToken.None);

        Assert.True(ok);
        Assert.NotNull(saved);
        Assert.Equal("moi", saved!.Username);
        Assert.Equal("Admin", saved.Role);
        Assert.NotEqual(Password, saved.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(Password, saved.PasswordHash));
    }

    [Fact]
    public async Task Register_TrungTen_BaoLoiVaKhongLuu()
    {
        SeedUser();

        await Assert.ThrowsAsync<Exception>(() =>
            new RegisterHandler(_repo.Object).Handle(new RegisterRequestDTO("admin", Password, "User"), CancellationToken.None));
        _repo.Verify(r => r.AddAsync(It.IsAny<TaiKhoan>()), Times.Never);
    }
}
