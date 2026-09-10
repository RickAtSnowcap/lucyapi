using LucyAPI.Data.Models;
using LucyAPI.Data.Repositories;
using LucyAPI.Services.Interfaces;

namespace LucyAPI.Services.Implementations;

public sealed class UserService(UserRepository userRepo) : IUserService
{
    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => userRepo.GetByUsernameAsync(username, ct);

    public Task<User?> GetByIdAsync(int userId, CancellationToken ct = default)
        => userRepo.GetByIdAsync(userId, ct);

    public Task<List<User>> ListOtherUsersAsync(int excludeUserId, CancellationToken ct = default)
        => userRepo.ListOtherUsersAsync(excludeUserId, ct);

    public Task<bool> UpdatePasswordHashAsync(int userId, string passwordHash, CancellationToken ct = default)
        => userRepo.UpdatePasswordHashAsync(userId, passwordHash, ct);
}
