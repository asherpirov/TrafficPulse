using TrafficShared.Models;
namespace TrafficShared.Repositories;

public interface IUserRepository
{
    Task<AppUser?> GetByEmailAsync(string email);
    Task<AppUser?> GetUserAsync(Guid id);
    Task<List<AppUser>> GetUsersAsync();
    Task AddUserAsync(AppUser user);
    Task RecordLoginAsync(Guid id, bool success);
    Task SetUserActiveAsync(Guid id, bool active);
}
