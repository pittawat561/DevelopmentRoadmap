# Caching ใน ASP.NET Core — Memory, Redis, EF 2nd Level

> เทคนิค cache ใน .NET เพื่อให้ API เร็วขึ้น 10-100 เท่า

---

## 1. Memory Cache (In-Process)

```csharp
// เก็บ cache ใน RAM ของ app process เดียวกัน
// เร็วที่สุด แต่หายเมื่อ app restart, ไม่ share ข้าม instances

// ===== ติดตั้ง =====
// Program.cs
builder.Services.AddMemoryCache();

// ===== ใช้งาน =====
public class ProductService : IProductService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public ProductService(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync()
    {
        // ลองดึงจาก cache ก่อน
        if (_cache.TryGetValue("categories", out List<CategoryDto>? cached))
            return cached!;    // Cache Hit!

        // Cache Miss → ดึง DB
        var categories = await _context.Categories
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Products.Count))
            .ToListAsync();

        // เก็บ cache (หมดอายุ 1 ชม.)
        _cache.Set("categories", categories, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
            SlidingExpiration = TimeSpan.FromMinutes(30),  // ต่ออายุถ้ามีคนเข้าถึง
            Priority = CacheItemPriority.Normal
        });

        return categories;
    }

    // GetOrCreate — สั้นกว่า (แนะนำ!)
    public async Task<ProductDto> GetProductAsync(int id)
    {
        return await _cache.GetOrCreateAsync($"product:{id}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);

            var product = await _context.Products
                .Where(p => p.Id == id)
                .Select(p => new ProductDto(p.Id, p.Name, p.Price, p.Stock))
                .FirstOrDefaultAsync()
                ?? throw new NotFoundException("Product", id);

            return product;
        })!;
    }

    // Invalidate cache เมื่อข้อมูลเปลี่ยน
    public async Task UpdateProductAsync(int id, UpdateProductRequest request)
    {
        var product = await _context.Products.FindAsync(id)
            ?? throw new NotFoundException("Product", id);

        product.Name = request.Name ?? product.Name;
        product.Price = request.Price ?? product.Price;
        await _context.SaveChangesAsync();

        _cache.Remove($"product:{id}");     // ลบ cache ของ product นี้
        _cache.Remove("categories");         // ลบ cache categories ด้วย (count อาจเปลี่ยน)
    }
}
```

---

## 2. Distributed Cache (Redis)

```csharp
// เก็บ cache ใน Redis — share ข้าม instances, survive restart

// ===== ติดตั้ง =====
// dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis

// Program.cs
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "MyApp:";    // prefix ทุก key
});

// ===== ใช้งานผ่าน IDistributedCache =====
public class CachedUserService : IUserService
{
    private readonly IUserService _inner;
    private readonly IDistributedCache _cache;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public CachedUserService(IUserService inner, IDistributedCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<UserDetailDto> GetByIdAsync(int id)
    {
        var cacheKey = $"user:{id}";

        // ดึงจาก cache
        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached is not null)
            return JsonSerializer.Deserialize<UserDetailDto>(cached, _jsonOptions)!;

        // Cache Miss → ดึงจาก service จริง
        var user = await _inner.GetByIdAsync(id);

        // เก็บ cache
        await _cache.SetStringAsync(cacheKey,
            JsonSerializer.Serialize(user, _jsonOptions),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                SlidingExpiration = TimeSpan.FromMinutes(10)
            });

        return user;
    }
}

// ===== ลงทะเบียนแบบ Decorator Pattern =====
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IUserService>(sp =>
    new CachedUserService(
        sp.GetRequiredService<UserService>(),
        sp.GetRequiredService<IDistributedCache>()));
```

### Redis ตรงๆ ด้วย StackExchange.Redis

```csharp
// dotnet add package StackExchange.Redis

// Program.cs
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

// ใช้งาน
public class RedisCacheService : ICacheService
{
    private readonly IDatabase _redis;

    public RedisCacheService(IConnectionMultiplexer connection)
    {
        _redis = connection.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _redis.StringGetAsync(key);
        return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var json = JsonSerializer.Serialize(value);
        await _redis.StringSetAsync(key, json, expiry ?? TimeSpan.FromMinutes(30));
    }

    public async Task RemoveAsync(string key)
        => await _redis.KeyDeleteAsync(key);

    public async Task RemoveByPrefixAsync(string prefix)
    {
        var server = _redis.Multiplexer.GetServer(_redis.Multiplexer.GetEndPoints().First());
        var keys = server.Keys(pattern: $"{prefix}*").ToArray();
        if (keys.Length > 0)
            await _redis.KeyDeleteAsync(keys);
    }
}
```

---

## 3. Response Caching & Output Caching

```csharp
// ===== Output Caching (.NET 7+) — cache HTTP response ทั้งก้อน =====
// Program.cs
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(builder => builder.Expire(TimeSpan.FromMinutes(5)));
    options.AddPolicy("Products", builder => builder
        .Expire(TimeSpan.FromMinutes(30))
        .Tag("products"));
});

app.UseOutputCache();

// Controller
[HttpGet]
[OutputCache(PolicyName = "Products")]
public async Task<IActionResult> GetProducts()
{
    return Ok(await _productService.GetAllAsync());
}

// Invalidate
[HttpPut("{id}")]
public async Task<IActionResult> Update(int id, UpdateProductRequest request, IOutputCacheStore store)
{
    var result = await _productService.UpdateAsync(id, request);
    await store.EvictByTagAsync("products", default);  // ลบ cache ทุก product
    return Ok(result);
}

// ===== Response Caching (HTTP headers) =====
[HttpGet]
[ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "page", "pageSize" })]
public async Task<IActionResult> GetAll(int page = 1, int pageSize = 10)
{
    // → Cache-Control: public, max-age=300
    return Ok(await _service.GetAllAsync(page, pageSize));
}
```

---

## 4. EF Core 2nd Level Cache

```csharp
// EF Core ไม่มี 2nd level cache built-in
// ใช้ library: EFCoreSecondLevelCacheInterceptor

// dotnet add package EFCoreSecondLevelCacheInterceptor

// Program.cs
builder.Services.AddEFSecondLevelCache(options =>
{
    options.UseMemoryCacheProvider()           // หรือ UseRedisCacheProvider
        .CacheAllQueries(CacheExpirationMode.Sliding, TimeSpan.FromMinutes(30))
        .UseCacheKeyPrefix("EF:");
});

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString)
        .AddInterceptors(sp.GetRequiredService<SecondLevelCacheInterceptor>());
});

// ทุก query จะถูก cache อัตโนมัติ!
// query ที่เหมือนกัน → ดึงจาก cache แทน DB

// ยกเว้น query ที่ไม่ต้อง cache:
var freshUsers = await _context.Users
    .NotCacheable()              // ข้าม cache
    .Where(u => u.IsActive)
    .ToListAsync();
```

---

## 5. Cache Strategy สำหรับ Enterprise API

```
| ข้อมูล                   | Cache Type     | TTL        |
|--------------------------|----------------|------------|
| Categories, Settings     | Memory         | 1-24 ชม.   |
| Product list             | Output Cache   | 5-30 นาที  |
| User profile             | Redis          | 30 นาที    |
| Dashboard stats          | Redis          | 5 นาที     |
| Session data             | Redis          | 24 ชม.     |
| Frequently queried data  | EF 2nd Level   | 30 นาที    |
| Static API responses     | Response Cache | 1-24 ชม.   |

กฎทอง:
1. Cache ข้อมูลที่อ่านบ่อย เขียนน้อย
2. ตั้ง TTL ให้เหมาะสม (สั้นเกิน = ไม่มีประโยชน์, ยาวเกิน = stale data)
3. Invalidate เมื่อข้อมูลเปลี่ยน
4. 1 instance → Memory Cache, หลาย instances → Redis
```
