
using Banking.Domain.Entities;
using Banking.Domain.Interfaces;
using Banking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Banking.Infrastructure.Repositories;

/// <summary>
/// Generic Repository — implement method พื้นฐาน CRUD ให้ทุก Entity
/// ทุก specific repository (UserRepository, AccountRepository) จะ inherit จากนี้
/// ไม่ต้องเขียน GetById, GetAll, Add, Update, Remove ซ้ำทุกตัว
/// </summary>

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    // _context: ใช้เข้าถึง database ผ่าน EF Core
    // protected: ให้ class ลูก (UserRepository ฯลฯ) เข้าถึงได้
    protected readonly AppDbContext _context;

    // _dbSet: เหมือนตารางใน database (เช่น DbSet<User> = ตาราง users)
    protected readonly DbSet<T> _dbSet;
    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();  // ดึง DbSet ของ Entity type T
    }

    /// <summary>
    /// ค้นหาจาก Id — ใช้ FindAsync ซึ่งดูใน cache ก่อน ถ้าไม่มีค่อย query database
    /// เร็วกว่า FirstOrDefaultAsync เมื่อค้นหาด้วย Primary Key
    /// </summary>
    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbSet.FindAsync([id], ct);
    }
    /// <summary>
    /// ดึงทั้งหมด — QueryFilter (IsDeleted) ถูกใช้อัตโนมัติ
    /// ToListAsync() ส่ง query ไป database แล้วแปลงเป็น List ใน memory
    /// </summary>
    public virtual async Task<List<T>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbSet.ToListAsync(ct);
    }

    /// <summary>
    /// เพิ่ม Entity ใหม่ — ยังไม่บันทึกจริงจนกว่าจะ SaveChanges
    /// EF Core จะ track entity นี้ใน state "Added"
    /// </summary>
    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
    }

    /// <summary>
    /// อัปเดต Entity — EF Core จะ track การเปลี่ยนแปลง
    /// ไม่ต้อง query ก่อน ถ้ามี entity อยู่แล้ว แค่ attach + mark Modified
    /// </summary>
    public virtual void Update(T entity)
    {
        _dbSet.Update(entity);
    }

    /// <summary>
    /// Soft Delete — ไม่ลบจริง แค่เปลี่ยน IsDeleted = true
    /// ระบบธนาคารห้ามลบข้อมูลจริง ต้องเก็บไว้เพื่อ audit
    /// </summary>
    public virtual void Remove(T entity)
    {
        entity.IsDeleted = true;
        _dbSet.Update(entity);
    }
}