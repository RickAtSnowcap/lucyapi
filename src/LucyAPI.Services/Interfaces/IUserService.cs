using LucyAPI.Data.Models;

namespace LucyAPI.Services.Interfaces;

public interface IUserService
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<User?> GetByIdAsync(int userId, CancellationToken ct = default);
    Task<List<User>> ListOtherUsersAsync(int excludeUserId, CancellationToken ct = default);
    Task<bool> UpdatePasswordHashAsync(int userId, string passwordHash, CancellationToken ct = default);
}
