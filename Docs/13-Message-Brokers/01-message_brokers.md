# Message Brokers — Kafka & RabbitMQ

---

## 1. Message Broker คืออะไร

ตัวกลางที่ **รับส่งข้อความระหว่าง services** แบบ asynchronous ผู้ส่งไม่ต้องรอผู้รับ

```
ไม่มี Message Broker (Synchronous):
Order Service → Payment Service → Email Service → Inventory Service
                 ↑ ต้องรอแต่ละตัวเสร็จ (ช้า, ถ้าตัวใดพัง = ทั้งหมดพัง)

มี Message Broker (Asynchronous):
Order Service → [Message Broker] → Payment Service
                                 → Email Service
                                 → Inventory Service
                 ↑ ส่งข้อความแล้วจบ (เร็ว, ตัวใดพังไม่กระทบตัวอื่น)
```

### เมื่อไหร่ใช้ Message Broker

```
✅ ใช้เมื่อ:
- ไม่ต้องการรอ response ทันที (ส่ง email, สร้าง report)
- ต้องการ decouple services (ไม่ขึ้นต่อกัน)
- Traffic เข้ามาเยอะ → ต้องจัดคิวค่อยๆ ทำ
- Event-driven architecture
- ต้องส่งข้อมูลไปหลาย services พร้อมกัน

❌ ไม่ต้องใช้เมื่อ:
- ต้องการ response ทันที (login, get user data)
- ระบบง่ายๆ 1-2 services
- ข้อมูลไม่สำคัญ/ไม่ต้องรับรองว่าส่งถึง
```

---

## 2. Patterns

### Point-to-Point (Queue)

```
Producer → [Queue] → Consumer
ข้อความถูกรับโดย consumer เดียว

ใช้กับ: Task processing, job queue
ตัวอย่าง: ส่ง email → 1 worker หยิบไปส่ง
```

### Publish/Subscribe (Topic)

```
Publisher → [Topic] → Subscriber 1
                   → Subscriber 2
                   → Subscriber 3
ข้อความถูกส่งให้ทุก subscribers

ใช้กับ: Event notification, broadcasting
ตัวอย่าง: Order created → Payment, Email, Inventory ทุกตัวรับรู้
```

---

## 3. RabbitMQ

```
RabbitMQ ใช้ AMQP protocol — เน้น message routing ที่ยืดหยุ่น

ข้อดี:
✅ Routing ยืดหยุ่น (exchanges)
✅ Message acknowledgment (ยืนยันว่ารับแล้ว)
✅ Dead letter queue (จัดการข้อความที่ process ไม่ได้)
✅ ง่ายกว่า Kafka สำหรับเริ่มต้น

ข้อเสีย:
❌ Throughput ต่ำกว่า Kafka
❌ ไม่เก็บ message history (ส่งแล้วลบ)
```

```javascript
// Producer (ส่งข้อความ)
const amqp = require('amqplib')

async function sendMessage() {
  const conn = await amqp.connect('amqp://localhost')
  const channel = await conn.createChannel()

  const queue = 'email_queue'
  await channel.assertQueue(queue, { durable: true })

  const message = JSON.stringify({
    to: 'user@example.com',
    subject: 'Welcome!',
    body: 'Thanks for signing up'
  })

  channel.sendToQueue(queue, Buffer.from(message), {
    persistent: true  // ข้อความไม่หายแม้ restart
  })

  console.log('Message sent')
}

// Consumer (รับข้อความ)
async function consumeMessages() {
  const conn = await amqp.connect('amqp://localhost')
  const channel = await conn.createChannel()

  const queue = 'email_queue'
  await channel.assertQueue(queue, { durable: true })
  channel.prefetch(1)  // รับทีละ 1

  channel.consume(queue, async (msg) => {
    const data = JSON.parse(msg.content.toString())
    console.log('Sending email to:', data.to)

    try {
      await sendEmail(data)          // ส่ง email จริง
      channel.ack(msg)               // ✅ ยืนยันว่าเสร็จแล้ว
    } catch (error) {
      channel.nack(msg, false, true) // ❌ ส่งกลับเข้า queue
    }
  })
}
```

---

## 4. Apache Kafka

```
Kafka เน้น high-throughput event streaming

ข้อดี:
✅ Throughput สูงมาก (ล้าน messages/วินาที)
✅ เก็บ message history (replay ได้!)
✅ Consumer groups (scale consumers)
✅ เหมาะกับ event-driven & big data

ข้อเสีย:
❌ ซับซ้อนกว่า RabbitMQ
❌ ต้องจัดการ Zookeeper/KRaft
❌ Overkill สำหรับระบบเล็ก

Kafka Concepts:
┌────────────────────────────────────────┐
│ Topic: "order-events"                  │
│ ┌──────────┬──────────┬──────────┐     │
│ │Partition 0│Partition 1│Partition 2│   │
│ │[1][2][3]  │[1][2]    │[1][2][3][4]│  │
│ └──────────┴──────────┴──────────┘     │
│     ↑                                  │
│  Messages เรียงตาม offset              │
│  Consumer อ่านจาก offset ที่ต้องการ     │
│  (replay ได้!)                          │
└────────────────────────────────────────┘
```

```javascript
// Kafka Producer
const { Kafka } = require('kafkajs')

const kafka = new Kafka({ brokers: ['localhost:9092'] })
const producer = kafka.producer()

async function publishOrderEvent(order) {
  await producer.connect()
  await producer.send({
    topic: 'order-events',
    messages: [{
      key: order.id.toString(),
      value: JSON.stringify({
        type: 'ORDER_CREATED',
        data: order,
        timestamp: Date.now()
      })
    }]
  })
}

// Kafka Consumer
const consumer = kafka.consumer({ groupId: 'payment-service' })

async function startConsumer() {
  await consumer.connect()
  await consumer.subscribe({ topic: 'order-events', fromBeginning: false })

  await consumer.run({
    eachMessage: async ({ topic, partition, message }) => {
      const event = JSON.parse(message.value.toString())

      if (event.type === 'ORDER_CREATED') {
        await processPayment(event.data)
      }
    }
  })
}
```

---

## 5. RabbitMQ vs Kafka

```
| หัวข้อ             | RabbitMQ           | Kafka               |
|-------------------|--------------------|--------------------|
| Protocol          | AMQP               | Custom (binary)     |
| Throughput        | ~50K msg/s         | ~1M+ msg/s          |
| Message Retention | ส่งแล้วลบ           | เก็บตาม config (วัน/สัปดาห์) |
| Replay            | ❌                  | ✅ อ่านซ้ำจาก offset  |
| Routing           | ⭐ ยืดหยุ่นมาก      | Topic-based เท่านั้น  |
| Order Guarantee   | Per queue           | Per partition        |
| Learning Curve    | ง่ายกว่า            | ยากกว่า              |
| Use Case          | Task queue, RPC     | Event streaming      |

เลือกอะไร:
├── Task queue / background jobs?        → RabbitMQ
├── Event streaming / big data?          → Kafka
├── ต้อง replay messages?                → Kafka
├── Complex routing?                     → RabbitMQ
├── ระบบเล็ก-กลาง?                       → RabbitMQ
└── ระบบใหญ่, traffic สูงมาก?            → Kafka
```
