using Banking.Domain.Entities;
using Banking.Domain.Interfaces;
using Banking.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Banking.Infrastructure.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    /// <summary>
    /// ค้นหา User จาก email — ใช้ตอน Login
    /// Include(u => u.Accounts) — โหลด Accounts มาด้วย (Eager Loading)
    /// เพื่อให้ดูข้อมูลบัญชีได้ทันทีหลัง login
    /// </summary>
    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _dbSet
            .Include(u => u.Accounts)
            .FirstOrDefaultAsync(u => u.Email == email, ct);
    }

    /// <summary>
    /// เช็คว่า email มีอยู่แล้วไหม — ใช้ตอน Register
    /// AnyAsync เร็วกว่า FirstOrDefault เพราะแค่เช็ค EXISTS (ไม่โหลดข้อมูล)
    /// </summary>
    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(u => u.Email == email, ct);
    }

    public async Task<bool> PhoneExistsAsync(string phone, CancellationToken ct = default)
    {
        return await _dbSet.AnyAsync(u => u.Phone == phone, ct);
    }
}
