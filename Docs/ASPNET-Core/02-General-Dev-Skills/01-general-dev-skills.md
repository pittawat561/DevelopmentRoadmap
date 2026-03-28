# General Development Skills สำหรับ C# Developer

> ทักษะพื้นฐานที่ต้องรู้ — หลายเรื่องมีเอกสารแยกแล้ว ดูอ้างอิงด้านล่าง

---

## 1. Git & Version Control

> ดูเอกสารละเอียดที่: `Docs/VersionControl/version_control.md`

สิ่งที่ต้องรู้สำหรับ C# project:

```bash
# .gitignore สำหรับ .NET project
# สร้างอัตโนมัติ:
dotnet new gitignore

# สิ่งที่ .gitignore ต้องมี:
# bin/           ← build output
# obj/           ← build intermediate
# .vs/           ← Visual Studio settings
# *.user         ← user settings
# appsettings.Development.json  ← local secrets (ถ้ามี secrets)

# Git workflow ที่ใช้ในองค์กร:
# main/master → production
# develop     → development
# feature/*   → feature branches
# hotfix/*    → bug fixes

# ตัวอย่าง:
git checkout -b feature/user-management
# ทำงาน → commit → push → Pull Request → Code Review → Merge
```

---

## 2. HTTP / HTTPS Protocol

> ดูเอกสารละเอียดที่: `Docs/Api/api_design.md`

สิ่งที่ต้องรู้เพิ่มสำหรับ C#:

```
HTTP Methods ที่ใช้ใน ASP.NET Core:
[HttpGet]    → GET    → อ่านข้อมูล
[HttpPost]   → POST   → สร้างข้อมูลใหม่
[HttpPut]    → PUT    → แก้ไขข้อมูลทั้งหมด
[HttpPatch]  → PATCH  → แก้ไขบางส่วน
[HttpDelete] → DELETE → ลบข้อมูล

HTTP Headers ที่สำคัญ:
- Content-Type: application/json     ← ส่ง/รับ JSON
- Authorization: Bearer <JWT_TOKEN>  ← ส่ง JWT token
- Accept: application/json
```

---

## 3. Data Structures & Algorithms

สิ่งที่ต้องรู้สำหรับ C# Enterprise API:

```csharp
// ===== Collections ที่ใช้บ่อยที่สุด =====

// List<T> — ลำดับ, อ่าน/เขียน index ได้
var users = new List<User>();                  // O(1) add, O(n) search

// Dictionary<K,V> — key-value lookup เร็ว
var cache = new Dictionary<string, User>();    // O(1) lookup

// HashSet<T> — ไม่ซ้ำ, เช็คสมาชิกเร็ว
var uniqueEmails = new HashSet<string>();      // O(1) contains

// Queue<T> — FIFO (First In First Out)
var jobQueue = new Queue<BackgroundJob>();

// Stack<T> — LIFO (Last In First Out)
var undoStack = new Stack<Action>();

// ===== Big-O ที่ควรรู้ =====
// O(1)     → constant  → Dictionary lookup, HashSet contains
// O(log n) → logarithmic → Binary search, SortedSet
// O(n)     → linear → List.Find, Where
// O(n²)    → quadratic → nested loops ← หลีกเลี่ยง!

// ===== เลือก Collection ถูกต้อง =====
// ต้องการ         → ใช้
// ลำดับ + index   → List<T>
// key-value       → Dictionary<K,V>
// ไม่ซ้ำ          → HashSet<T>
// เรียงลำดับ      → SortedSet<T>, SortedDictionary<K,V>
// Thread-safe     → ConcurrentDictionary, ConcurrentQueue
// FIFO queue      → Queue<T> หรือ Channel<T>
```

---

## 4. Design Patterns สำคัญ

```csharp
// ===== Repository Pattern =====
// แยก data access ออกจาก business logic
public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<User> AddAsync(User user);
}

// ===== Unit of Work =====
// จัดการ transaction ข้าม repositories
public interface IUnitOfWork
{
    IUserRepository Users { get; }
    IOrderRepository Orders { get; }
    Task<int> SaveChangesAsync();
}

// ===== Strategy Pattern =====
// เปลี่ยน algorithm ได้ตอน runtime
public interface INotificationSender
{
    Task SendAsync(string to, string message);
}
public class EmailSender : INotificationSender { ... }
public class SmsSender : INotificationSender { ... }

// ===== Builder Pattern =====
// สร้าง object ซับซ้อนทีละขั้น
var query = QueryBuilder<User>
    .Where(u => u.IsActive)
    .OrderBy(u => u.Name)
    .Page(1, 10)
    .Build();
```

---

## 5. StyleCop / Code Standards

```
StyleCop Rules สำคัญ:
- ใช้ PascalCase สำหรับ public members (MethodName, PropertyName)
- ใช้ camelCase สำหรับ private fields + parameters (_fieldName, paramName)
- ใช้ I นำหน้า Interface (IUserService)
- 1 class ต่อ 1 file
- ใส่ access modifier เสมอ (public, private, internal)
- ใช้ var เมื่อ type ชัดเจน
- Async method ลงท้ายด้วย Async

ติดตั้ง:
dotnet add package StyleCop.Analyzers
สร้าง .editorconfig ในโปรเจกต์ root
```
