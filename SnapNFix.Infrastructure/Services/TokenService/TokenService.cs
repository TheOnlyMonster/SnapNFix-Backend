﻿using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SnapNFix.Domain.Entities;
using SnapNFix.Domain.Enums;
using SnapNFix.Application.Common.Interfaces;
using SnapNFix.Application.Interfaces;
using SnapNFix.Domain.Interfaces;

namespace SnapNFix.Infrastructure.Services.TokenService;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<User> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDeviceManager _deviceManager;

    public TokenService(
        IConfiguration configuration,
        UserManager<User> userManager,
        IUnitOfWork unitOfWork,
        IDeviceManager deviceManager)
    {
        _configuration = configuration;
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _deviceManager = deviceManager;
    }

    public async Task<(string AccessToken, string RefreshToken)> GenerateTokensForDeviceAsync(
        User user,
        string deviceId,
        string deviceName,
        string platform,
        string deviceType,
        string fcmToken )
    {
        var userDevice = await _deviceManager.RegisterDeviceAsync(
            user.Id,
            deviceId,
            deviceName,
            platform,
            deviceType,
            fcmToken);

        var accessToken = await GenerateJwtToken(user, userDevice);
        var refreshToken = GenerateRefreshToken(userDevice);

        userDevice.RefreshToken = refreshToken;
        await _unitOfWork.Repository<UserDevice>().Update(userDevice);
        await _unitOfWork.SaveChanges();

        return (accessToken, refreshToken.Token);
    }

    public async Task<string> GenerateJwtToken(User user, UserDevice device)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new Claim("DeviceId", device.Id.ToString()),
        };

        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = GetTokenExpiration();

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    public RefreshToken GenerateRefreshToken(UserDevice userDevice)
    {
        var refreshToken = new RefreshToken
        {
            Token = GenerateRefreshToken(),
            UserDeviceId = userDevice.Id,
            Expires = GetRefreshTokenExpirationDays(),
            CreatedAt = DateTime.UtcNow
        };
        return refreshToken;
    }

    public DateTime GetTokenExpiration()
    {
        double minutesToExpire = 5;
        if (double.TryParse(_configuration["Jwt:TokenExpirationMinutes"], out double configMinutes))
        {
            minutesToExpire = configMinutes;
        }
        return DateTime.UtcNow.AddHours(minutesToExpire);
    }


    public async Task<(string JwtToken, string RefreshToken)> RefreshTokenAsync(RefreshToken refreshToken)
    {

        var userDevice = refreshToken.UserDevice;

        var user = userDevice.User;
        var newAccessToken = await GenerateJwtToken(user, userDevice);
        var newRefreshToken = GenerateRefreshToken(userDevice);

        refreshToken.Token = newRefreshToken.Token;
        refreshToken.Expires = GetRefreshTokenExpirationDays();

        userDevice.LastUsedAt = DateTime.UtcNow;
        await _unitOfWork.Repository<UserDevice>().Update(userDevice);
        await _unitOfWork.Repository<RefreshToken>().Update(refreshToken);
        await _unitOfWork.SaveChanges();

        return (newAccessToken, newRefreshToken.Token);
    }


    public async Task<bool> RevokeDeviceTokensAsync(Guid userId, string deviceId)
    {
        var userDevice = await _unitOfWork.Repository<UserDevice>()
            .FindBy(d => d.UserId == userId && d.DeviceId == deviceId)
            .Include(d => d.RefreshToken)
            .FirstOrDefaultAsync();

        if (userDevice?.RefreshToken == null)
        {
            return false;
        }

        userDevice.RefreshToken.Expires = DateTime.UtcNow;
        await _unitOfWork.Repository<RefreshToken>().Update(userDevice.RefreshToken);
        await _unitOfWork.SaveChanges();

        return true;
    }

    public DateTime GetRefreshTokenExpirationDays()
    {
        int daysToExpire = 7;
        if (int.TryParse(_configuration["Jwt:RefreshTokenExpirationDays"], out int configDays))
        {
            daysToExpire = configDays;
        }
        return DateTime.UtcNow.AddDays(daysToExpire);
    }


    public string GenerateToken(string emailOrPhoneNumber, TokenPurpose purpose)
    {
        var claims = new List<Claim>
        {
            new("contact", emailOrPhoneNumber),
            new("purpose", purpose.ToString()),
            new("issued_at", DateTime.UtcNow.ToString("O"))
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(5); 

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    
    

}
