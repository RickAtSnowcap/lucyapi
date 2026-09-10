using LucyAPI.Api.Auth;
using LucyAPI.Api.Extensions;
using LucyAPI.Api.Models;
using LucyAPI.Services.DTOs;
using LucyAPI.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace LucyAPI.Api.Endpoints.Admin;

public static class AdminAuthEndpoints
{
    private static readonly PasswordHasher<string> Hasher = new();

    public static void MapAdminAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/login", async (
            LoginRequest request,
            IUserService userService,
            JwtTokenService jwtService,
            CancellationToken ct) =>
        {
            var user = await userService.GetByUsernameAsync(request.Username, ct);
            if (user?.PasswordHash is null)
                return Results.Json(new ErrorResponse { Error = "Invalid credentials" },
                    AppJsonSerializerContext.Default.ErrorResponse, statusCode: 401);

            var result = Hasher.VerifyHashedPassword(user.Username, user.PasswordHash, request.Password);
            if (result == PasswordVerificationResult.Failed)
                return Results.Json(new ErrorResponse { Error = "Invalid credentials" },
                    AppJsonSerializerContext.Default.ErrorResponse, statusCode: 401);

            // Rehash if algorithm upgrade is needed
            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                var newHash = Hasher.HashPassword(user.Username, request.Password);
                await userService.UpdatePasswordHashAsync(user.UserId, newHash, ct);
            }

            var token = jwtService.CreateAccessToken(user.UserId, user.Username, user.Name);
            return Results.Ok(new LoginResponse
            {
                Token = token,
                UserId = user.UserId,
                Username = user.Username,
                Name = user.Name
            });
        });

        app.MapPost("/auth/refresh", async (
            HttpContext ctx,
            IUserService userService,
            JwtTokenService jwtService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var user = await userService.GetByIdAsync(caller.UserId, ct);
            if (user is null) return Results.NotFound();

            var token = jwtService.CreateAccessToken(user.UserId, user.Username, user.Name);
            return Results.Ok(new LoginResponse
            {
                Token = token,
                UserId = user.UserId,
                Username = user.Username,
                Name = user.Name
            });
        });

        app.MapGet("/auth/me", async (
            HttpContext ctx,
            IUserService userService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var user = await userService.GetByIdAsync(caller.UserId, ct);
            if (user is null) return Results.NotFound();

            return Results.Ok(new UserInfoResponse
            {
                UserId = user.UserId,
                Name = user.Name,
                Username = user.Username,
                Email = user.Email
            });
        });

        app.MapPut("/auth/password", async (
            ChangePasswordRequest request,
            HttpContext ctx,
            IUserService userService,
            CancellationToken ct) =>
        {
            var caller = ctx.GetUserContext();
            var user = await userService.GetByIdAsync(caller.UserId, ct);
            if (user?.PasswordHash is null)
                return Results.BadRequest(new ErrorResponse { Error = "Current password is incorrect" });

            var result = Hasher.VerifyHashedPassword(user.Username, user.PasswordHash, request.CurrentPassword);
            if (result == PasswordVerificationResult.Failed)
                return Results.BadRequest(new ErrorResponse { Error = "Current password is incorrect" });

            if (request.NewPassword.Length < 8)
                return Results.BadRequest(new ErrorResponse { Error = "New password must be at least 8 characters" });

            var newHash = Hasher.HashPassword(user.Username, request.NewPassword);
            await userService.UpdatePasswordHashAsync(user.UserId, newHash, ct);
            return Results.Ok(new OkResponse { Ok = true });
        });
    }
}
