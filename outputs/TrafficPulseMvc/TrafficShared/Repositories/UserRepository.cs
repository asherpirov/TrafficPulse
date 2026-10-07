using Microsoft.EntityFrameworkCore;
using System.Data;
using MySqlConnector;
using TrafficShared.Data;
using TrafficShared.Models;

namespace TrafficShared.Repositories;

public class UserRepository : IUserRepository
{
    private readonly TrafficDbContext _context;
    public UserRepository(TrafficDbContext context) { _context = context; }
    public Task<AppUser?> GetByEmailAsync(string email) => _context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email);
    public Task<AppUser?> GetUserAsync(Guid id) => _context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id);
    public Task<List<AppUser>> GetUsersAsync() => _context.Users.AsNoTracking().OrderBy(u => u.Name).Take(1000).ToListAsync();
    public async Task AddUserAsync(AppUser user)
    {
        _context.Users.Add(user);
        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        { throw new BusinessException("לא ניתן להירשם עם כתובת זו.", 409); }
    }
    public async Task RecordLoginAsync(Guid id, bool success)
    {
        // טרנזקציה מגינה על הספירה. במקרה של deadlock חוזרים עם DbContext נקי.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var user = await _context.Users.SingleAsync(u => u.Id == id);
                if (success) { user.FailedAttempts = 0; user.LockedUntilUtc = null; }
                else
                {
                    if (user.LockedUntilUtc <= DateTime.UtcNow) user.FailedAttempts = 0;
                    user.FailedAttempts++;
                    if (user.FailedAttempts >= 5) user.LockedUntilUtc = DateTime.UtcNow.AddMinutes(10);
                }
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return;
            }
            catch (Exception ex) when (attempt < 2 && (ex is MySqlException { Number: 1213 or 1205 } || ex.InnerException is MySqlException { Number: 1213 or 1205 }))
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                await Task.Delay(100 * (attempt + 1));
            }
        }
    }
    public async Task SetUserActiveAsync(Guid id, bool active)
    {
        string stamp = Guid.NewGuid().ToString();
        int changed = await _context.Users.Where(u => u.Id == id && u.Role != "Admin")
            .ExecuteUpdateAsync(update => update.SetProperty(u => u.IsActive, active).SetProperty(u => u.SecurityStamp, stamp));
        if (changed == 0) throw new BusinessException("המשתמש אינו קיים או שהוא מנהל מוגן.");
    }
}
