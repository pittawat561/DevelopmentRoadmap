# Testing — การทดสอบซอฟต์แวร์

> ครอบคลุม Unit Testing, Integration Testing, Functional Testing

---

## 1. ทำไมต้อง Test

```
ไม่มี Tests:
- แก้ bug ตรงนี้ → พังตรงนั้น (ไม่รู้ตัว!)
- deploy ขึ้น production → ลูกค้าเจอ bug
- กลัวแก้โค้ด เพราะอาจพังอย่างอื่น
- เสียเวลา debug มากกว่าเขียนโค้ด

มี Tests:
✅ รัน tests → รู้ทันทีว่าพังตรงไหน
✅ มั่นใจ deploy ได้
✅ กล้า refactor เพราะมี tests คอยตรวจ
✅ Tests เป็นเอกสารว่าโค้ดควรทำงานยังไง
```

## 2. Testing Pyramid

```
                 ╱╲
                ╱  ╲
               ╱ E2E╲         น้อย — ช้า — แพง
              ╱──────╲
             ╱        ╲
            ╱Integration╲     กลาง
           ╱──────────────╲
          ╱                ╲
         ╱   Unit Tests     ╲  มาก — เร็ว — ถูก
        ╱────────────────────╲

Unit Tests:       70% ของ tests ทั้งหมด
Integration Tests: 20%
E2E/Functional:    10%
```

## 3. Unit Testing — ทดสอบทีละหน่วย

### Unit Test คืออะไร

ทดสอบ **ฟังก์ชันเดียว** แบบแยกจากส่วนอื่น (isolated) เร็วมาก รันได้เป็นร้อยในไม่กี่วินาที

### JavaScript (Jest)

```javascript
// calculator.js
function add(a, b) {
  if (typeof a !== 'number' || typeof b !== 'number') {
    throw new Error('Arguments must be numbers')
  }
  return a + b
}

function divide(a, b) {
  if (b === 0) throw new Error('Cannot divide by zero')
  return a / b
}

module.exports = { add, divide }

// calculator.test.js
const { add, divide } = require('./calculator')

describe('add', () => {
  test('adds two positive numbers', () => {
    expect(add(2, 3)).toBe(5)
  })

  test('adds negative numbers', () => {
    expect(add(-1, -2)).toBe(-3)
  })

  test('adds zero', () => {
    expect(add(5, 0)).toBe(5)
  })

  test('throws error for non-numbers', () => {
    expect(() => add('a', 2)).toThrow('Arguments must be numbers')
  })
})

describe('divide', () => {
  test('divides correctly', () => {
    expect(divide(10, 2)).toBe(5)
  })

  test('returns decimal', () => {
    expect(divide(7, 2)).toBe(3.5)
  })

  test('throws error when dividing by zero', () => {
    expect(() => divide(10, 0)).toThrow('Cannot divide by zero')
  })
})

// รัน: npx jest
```

### C# (xUnit)

```csharp
// Calculator.cs
public class Calculator
{
    public double Add(double a, double b) => a + b;

    public double Divide(double a, double b)
    {
        if (b == 0) throw new DivideByZeroException("Cannot divide by zero");
        return a / b;
    }
}

// CalculatorTests.cs
using Xunit;

public class CalculatorTests
{
    private readonly Calculator _calculator = new();

    [Fact]
    public void Add_TwoPositiveNumbers_ReturnsCorrectSum()
    {
        var result = _calculator.Add(2, 3);
        Assert.Equal(5, result);
    }

    [Theory]
    [InlineData(10, 2, 5)]
    [InlineData(7, 2, 3.5)]
    [InlineData(-6, 3, -2)]
    public void Divide_ValidNumbers_ReturnsCorrectResult(
        double a, double b, double expected)
    {
        var result = _calculator.Divide(a, b);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Divide_ByZero_ThrowsException()
    {
        Assert.Throws<DivideByZeroException>(
            () => _calculator.Divide(10, 0)
        );
    }
}

// รัน: dotnet test
```

### Mocking — จำลอง dependencies

```javascript
// userService.js
class UserService {
  constructor(userRepository, emailService) {
    this.userRepo = userRepository
    this.emailService = emailService
  }

  async registerUser(name, email) {
    // เช็คว่า email ซ้ำไหม
    const existing = await this.userRepo.findByEmail(email)
    if (existing) throw new Error('Email already exists')

    // สร้าง user
    const user = await this.userRepo.create({ name, email })

    // ส่ง welcome email
    await this.emailService.sendWelcome(email, name)

    return user
  }
}

// userService.test.js
describe('UserService', () => {
  let userService, mockRepo, mockEmail

  beforeEach(() => {
    // สร้าง mock objects (จำลอง dependencies)
    mockRepo = {
      findByEmail: jest.fn(),
      create: jest.fn()
    }
    mockEmail = {
      sendWelcome: jest.fn()
    }
    userService = new UserService(mockRepo, mockEmail)
  })

  test('registers new user successfully', async () => {
    // Arrange — เตรียมข้อมูล
    mockRepo.findByEmail.mockResolvedValue(null)  // ไม่มี user เก่า
    mockRepo.create.mockResolvedValue({ id: 1, name: 'John', email: 'john@test.com' })
    mockEmail.sendWelcome.mockResolvedValue(true)

    // Act — ทำ action
    const user = await userService.registerUser('John', 'john@test.com')

    // Assert — ตรวจผลลัพธ์
    expect(user.name).toBe('John')
    expect(mockRepo.create).toHaveBeenCalledWith({
      name: 'John', email: 'john@test.com'
    })
    expect(mockEmail.sendWelcome).toHaveBeenCalledWith('john@test.com', 'John')
  })

  test('rejects duplicate email', async () => {
    mockRepo.findByEmail.mockResolvedValue({ id: 1 })  // มี user เก่า

    await expect(
      userService.registerUser('Jane', 'john@test.com')
    ).rejects.toThrow('Email already exists')

    expect(mockRepo.create).not.toHaveBeenCalled()  // ไม่ถูกเรียก
    expect(mockEmail.sendWelcome).not.toHaveBeenCalled()
  })
})
```

---

## 4. Integration Testing — ทดสอบหลายส่วนทำงานร่วมกัน

ทดสอบว่า **หลายส่วนของระบบทำงานร่วมกันถูกต้อง** เช่น API + Database

```javascript
// API Integration Test (Supertest + Jest)
const request = require('supertest')
const app = require('../app')
const db = require('../db')

describe('POST /api/users', () => {
  // Setup/Teardown
  beforeAll(async () => {
    await db.migrate.latest()     // รัน migrations
  })

  afterEach(async () => {
    await db('users').truncate()  // ล้างข้อมูลหลังแต่ละ test
  })

  afterAll(async () => {
    await db.destroy()            // ปิด connection
  })

  test('creates a new user and returns 201', async () => {
    const response = await request(app)
      .post('/api/users')
      .send({ name: 'John', email: 'john@test.com' })
      .expect(201)

    expect(response.body).toMatchObject({
      name: 'John',
      email: 'john@test.com'
    })
    expect(response.body.id).toBeDefined()

    // ตรวจสอบ database จริง
    const userInDb = await db('users').where({ email: 'john@test.com' }).first()
    expect(userInDb).toBeTruthy()
    expect(userInDb.name).toBe('John')
  })

  test('returns 400 for invalid email', async () => {
    await request(app)
      .post('/api/users')
      .send({ name: 'John', email: 'invalid' })
      .expect(400)
  })

  test('returns 409 for duplicate email', async () => {
    // สร้าง user ก่อน
    await request(app)
      .post('/api/users')
      .send({ name: 'John', email: 'john@test.com' })

    // สร้างซ้ำ
    await request(app)
      .post('/api/users')
      .send({ name: 'Jane', email: 'john@test.com' })
      .expect(409)
  })
})
```

### C# Integration Test

```csharp
// WebApplicationFactory — test .NET API จริง
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using Xunit;

public class UsersApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public UsersApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateUser_ReturnsCreated()
    {
        var response = await _client.PostAsJsonAsync("/api/users", new
        {
            Name = "John",
            Email = "john@test.com"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal("John", user.Name);
    }
}
```

---

## 5. Functional Testing (E2E) — ทดสอบจากมุมผู้ใช้

```
ทดสอบ flow ทั้งหมดเหมือนผู้ใช้จริง
เช่น: เปิดหน้าเว็บ → กรอก form → กด submit → เห็นผลลัพธ์

เครื่องมือ:
- Playwright (แนะนำ — เร็ว, รองรับหลาย browser)
- Cypress (นิยมสำหรับ frontend)
- Selenium (เก่าแก่ที่สุด)
```

```javascript
// Playwright E2E Test
const { test, expect } = require('@playwright/test')

test('user can register and login', async ({ page }) => {
  // ไปหน้า register
  await page.goto('http://localhost:3000/register')

  // กรอก form
  await page.fill('input[name="name"]', 'John Doe')
  await page.fill('input[name="email"]', 'john@test.com')
  await page.fill('input[name="password"]', 'MyPassword123!')

  // กด submit
  await page.click('button[type="submit"]')

  // ตรวจสอบว่า redirect ไปหน้า login
  await expect(page).toHaveURL('/login')
  await expect(page.locator('.success-message')).toContainText('Registration successful')

  // Login
  await page.fill('input[name="email"]', 'john@test.com')
  await page.fill('input[name="password"]', 'MyPassword123!')
  await page.click('button[type="submit"]')

  // ตรวจสอบว่าเข้า dashboard ได้
  await expect(page).toHaveURL('/dashboard')
  await expect(page.locator('h1')).toContainText('Welcome, John')
})
```

---

## 6. Test Patterns & Best Practices

### AAA Pattern

```
Arrange — เตรียมข้อมูลและ dependencies
Act     — ทำ action ที่จะทดสอบ
Assert  — ตรวจสอบผลลัพธ์

test('calculates total with discount', () => {
  // Arrange
  const cart = new ShoppingCart()
  cart.addItem({ name: 'Shirt', price: 1000 })
  cart.addItem({ name: 'Pants', price: 1500 })
  cart.applyDiscount(10) // 10%

  // Act
  const total = cart.getTotal()

  // Assert
  expect(total).toBe(2250) // (1000 + 1500) * 0.9
})
```

### Test Coverage

```bash
# Jest — รัน tests พร้อมดู coverage
npx jest --coverage

# ผลลัพธ์:
# File           | % Stmts | % Branch | % Funcs | % Lines |
# calculator.js  |   100   |   100    |   100   |   100   |
# userService.js |    85   |    75    |   100   |    85   |

# .NET
dotnet test --collect:"XPlat Code Coverage"

# เป้าหมาย:
# 80%+ ก็ดี
# 100% ไม่จำเป็น — focus ที่ business logic สำคัญ
```

### สรุป

```
| ประเภท Test     | ทดสอบอะไร            | เร็ว? | เครื่องมือ           |
|----------------|---------------------|------|---------------------|
| Unit           | ฟังก์ชันเดียว         | ⚡ เร็วมาก | Jest, xUnit, NUnit  |
| Integration    | หลายส่วนร่วมกัน      | 🔶 ปานกลาง | Supertest, WebAppFactory |
| E2E/Functional | ทั้ง flow จากมุมผู้ใช้ | 🐌 ช้า    | Playwright, Cypress  |

กฎง่ายๆ:
1. เขียน Unit Test ก่อน — มาก, เร็ว, ถูก
2. เสริม Integration Test — ทดสอบ API + DB
3. เขียน E2E สำหรับ critical flows เท่านั้น
4. ทุก bug ที่เจอ → เขียน test ก่อนแก้ (regression test)
```
