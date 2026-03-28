
using Banking.Domain.Entities;

namespace Banking.Domain.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync<T>(Guid id, CancellationToken ct = default);
    Task<List<T>> GetAllAsync<T>(CancellationToken ct = default);
    Task AddAsync<T>(T entity, CancellationToken ct = default);
    void Update<T>(T entity);
    void Remove<T>(T entity);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default);
}

public interface IAccountRespository : IRepository<Account>
{
    Task<Account?> GetByAccountNumberAsync(string accountNumber, CancellationToken ct = default);
    Task<List<Account>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Account?> GetByIdForUodateAsync(Guid id, CancellationToken ct = default);
    Task<bool> AccountNumberExistsAsync(string accountNumber, CancellationToken ct = default);
}

public interface ITransactionRepository : IRepository<Transaction>
{
    Task<List<Transaction>> GetByAccountIdAsync(Guid accountId, int page, int pageSize, CancellationToken ct = default);
    Task<int> GetCountByAccountIdAsync(Guid accountId, CancellationToken ct = default);
    Task<decimal> GetTodayWithdrawalTotalAsync(Guid accountId, CancellationToken ct = default);
}

public interface IUnitOfWork
{
    IUserRepository User { get; }
    IAccountRespository Accounts { get; }
    ITransactionRepository Transactions { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}