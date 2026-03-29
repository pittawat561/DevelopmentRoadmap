namespace Banking.Api
{
    /// <summary>
    /// Model สำหรับข้อมูลพยากรณ์อากาศ — ใช้เป็น DTO (Data Transfer Object) ส่งกลับให้ client
    /// ถูกสร้างมาพร้อม template ของ ASP.NET Core สำหรับทดสอบ API
    /// </summary>
    public class WeatherForecast
    {
        /// <summary>
        /// วันที่ของพยากรณ์ — ใช้ DateOnly เพราะไม่ต้องการเวลา (แค่วันที่)
        /// DateOnly เป็น type ใหม่ตั้งแต่ .NET 6 — เหมาะกว่า DateTime สำหรับข้อมูลวันที่ล้วน
        /// </summary>
        public DateOnly Date { get; set; }

        /// <summary>
        /// อุณหภูมิในหน่วยองศาเซลเซียส
        /// </summary>
        public int TemperatureC { get; set; }

        /// <summary>
        /// อุณหภูมิในหน่วยฟาเรนไฮต์ — Computed Property (คำนวณจาก TemperatureC)
        /// สูตร: °F = 32 + (°C / 0.5556) ≈ 32 + (°C × 1.8)
        /// เป็น read-only property (มีแค่ get) — ไม่ได้เก็บค่า แต่คำนวณทุกครั้งที่เรียกใช้
        /// (int) — cast เป็น integer เพื่อตัดทศนิยมออก
        /// </summary>
        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

        /// <summary>
        /// คำอธิบายสภาพอากาศ เช่น "Cool", "Warm", "Scorching"
        /// เป็น nullable (string?) เพราะอาจไม่มีคำอธิบาย
        /// </summary>
        public string? Summary { get; set; }
    }
}
