using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers
{
    /// <summary>
    /// Controller ตัวอย่างสำหรับ API พยากรณ์อากาศ
    /// สร้างมาพร้อมกับ template ของ ASP.NET Core — ใช้ทดสอบว่า API ทำงานได้
    ///
    /// [ApiController] — attribute ที่เปิดฟีเจอร์เฉพาะสำหรับ API Controller:
    ///   1. Model Validation อัตโนมัติ (ส่ง 400 Bad Request ถ้า input ไม่ถูกต้อง)
    ///   2. Binding Source Inference (เดา source ของ parameter เช่น [FromBody], [FromQuery])
    ///   3. Problem Details Response (error response ตามมาตรฐาน RFC 7807)
    ///
    /// [Route("[controller]")] — กำหนด URL route เป็นชื่อ controller
    ///   [controller] จะถูกแทนที่ด้วยชื่อ class (ตัด "Controller" ออก)
    ///   WeatherForecastController → /WeatherForecast
    ///
    /// ControllerBase — base class สำหรับ API Controller (ไม่มี View support)
    ///   ให้ helper method เช่น Ok(), NotFound(), BadRequest()
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        /// <summary>
        /// รายชื่อคำอธิบายสภาพอากาศ — ใช้สุ่มใส่ใน forecast
        /// static readonly — สร้างครั้งเดียว ใช้ร่วมกันทุก request (ประหยัด memory)
        /// </summary>
        private static readonly string[] Summaries =
        [
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        ];

        /// <summary>
        /// API endpoint สำหรับดึงข้อมูลพยากรณ์อากาศ 5 วัน
        /// HTTP GET /WeatherForecast
        ///
        /// [HttpGet(Name = "GetWeatherForecast")] — กำหนดให้ method นี้รับ HTTP GET request
        ///   Name = "GetWeatherForecast" — ตั้งชื่อ route สำหรับอ้างอิง (เช่น ใน Swagger)
        ///
        /// Enumerable.Range(1, 5) — สร้างตัวเลข 1 ถึง 5 (5 ตัว)
        ///   ใช้เป็นจำนวนวัน (วันที่ 1 ถึง วันที่ 5 นับจากวันนี้)
        ///
        /// .Select(index =&gt; new WeatherForecast { ... }) — แปลงแต่ละตัวเลขเป็น WeatherForecast
        ///   DateOnly.FromDateTime(DateTime.Now.AddDays(index))
        ///     — สร้างวันที่ในอนาคต (วันนี้ + index วัน) แปลงเป็น DateOnly (ไม่มีเวลา)
        ///   Random.Shared.Next(-20, 55)
        ///     — สุ่มอุณหภูมิ -20 ถึง 54 องศาเซลเซียส
        ///     Random.Shared — ใช้ static Random instance ที่ thread-safe
        ///   Summaries[Random.Shared.Next(Summaries.Length)]
        ///     — สุ่มเลือกคำอธิบายสภาพอากาศจาก array Summaries
        ///
        /// .ToArray() — แปลง IEnumerable เป็น array เพื่อ execute query ทันที
        /// </summary>
        /// <returns>รายการพยากรณ์อากาศ 5 วัน (JSON array)</returns>
        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}
