# Real-Time Data — SSE, WebSockets, Polling

---

## 1. เทคนิค Real-Time

```
| เทคนิค              | ทิศทาง          | ใช้เมื่อ                          |
|--------------------|----------------|----------------------------------|
| Short Polling      | Client → Server | ง่ายที่สุด, ไม่ real-time จริง     |
| Long Polling       | Client → Server | ดีกว่า short, ไม่ต้อง WebSocket   |
| Server-Sent Events | Server → Client | Server ส่งข้อมูลทางเดียว           |
| WebSockets         | ↔ สองทาง        | Chat, game, collaborative editing |
```

---

## 2. Short Polling

```javascript
// Client ถาม server ซ้ำๆ ทุก X วินาที
// ง่ายที่สุด แต่เปลือง bandwidth

setInterval(async () => {
  const response = await fetch('/api/notifications')
  const data = await response.json()
  updateUI(data)
}, 5000)  // ทุก 5 วินาที

// ❌ ส่วนใหญ่ไม่มีข้อมูลใหม่ = เปลือง request
```

---

## 3. Long Polling

```javascript
// Client ถาม server → server รอจนมีข้อมูลใหม่ → ตอบ → ถามใหม่
// ดีกว่า short polling มาก

// Client
async function longPoll() {
  while (true) {
    try {
      const response = await fetch('/api/notifications/poll', {
        signal: AbortSignal.timeout(30000)  // timeout 30 วินาที
      })
      const data = await response.json()
      updateUI(data)
    } catch (error) {
      await new Promise(r => setTimeout(r, 1000))  // รอ 1 วินาทีก่อน retry
    }
  }
}

// Server (Express)
app.get('/api/notifications/poll', async (req, res) => {
  const userId = req.user.id

  // รอจนมี notification ใหม่ (หรือ timeout)
  const notification = await waitForNotification(userId, 25000)

  if (notification) {
    res.json(notification)
  } else {
    res.status(204).end()  // ไม่มีข้อมูลใหม่
  }
})
```

---

## 4. Server-Sent Events (SSE)

```
Server ส่งข้อมูลไปยัง client ทางเดียว ผ่าน HTTP
เหมาะกับ: notifications, live feed, stock prices
```

```javascript
// Server (Express)
app.get('/api/events', (req, res) => {
  res.setHeader('Content-Type', 'text/event-stream')
  res.setHeader('Cache-Control', 'no-cache')
  res.setHeader('Connection', 'keep-alive')

  // ส่ง event ทุก 3 วินาที
  const interval = setInterval(() => {
    const data = { time: new Date().toISOString(), price: Math.random() * 100 }
    res.write(`data: ${JSON.stringify(data)}\n\n`)
  }, 3000)

  // ส่ง event เมื่อมีข้อมูลใหม่
  const onNewOrder = (order) => {
    res.write(`event: new-order\n`)
    res.write(`data: ${JSON.stringify(order)}\n\n`)
  }
  eventEmitter.on('order-created', onNewOrder)

  // Cleanup เมื่อ client ตัดการเชื่อมต่อ
  req.on('close', () => {
    clearInterval(interval)
    eventEmitter.off('order-created', onNewOrder)
  })
})

// Client (Browser)
const eventSource = new EventSource('/api/events')

eventSource.onmessage = (event) => {
  const data = JSON.parse(event.data)
  console.log('Received:', data)
}

eventSource.addEventListener('new-order', (event) => {
  const order = JSON.parse(event.data)
  showNotification(`New order: ${order.id}`)
})

eventSource.onerror = () => {
  console.log('Connection lost, reconnecting...')
  // EventSource reconnect อัตโนมัติ!
}
```

---

## 5. WebSockets

```
การเชื่อมต่อ 2 ทาง (full-duplex) — ทั้ง client และ server ส่งข้อมูลได้ตลอดเวลา
เหมาะกับ: chat, game, collaborative editing, live dashboard
```

```javascript
// Server (ws library)
const WebSocket = require('ws')
const wss = new WebSocket.Server({ port: 8080 })

const clients = new Map()

wss.on('connection', (ws, req) => {
  const userId = getUserFromToken(req)
  clients.set(userId, ws)

  console.log(`User ${userId} connected`)

  ws.on('message', (message) => {
    const data = JSON.parse(message)

    switch (data.type) {
      case 'CHAT_MESSAGE':
        // ส่งข้อความไปหา user ปลายทาง
        const recipient = clients.get(data.toUserId)
        if (recipient && recipient.readyState === WebSocket.OPEN) {
          recipient.send(JSON.stringify({
            type: 'CHAT_MESSAGE',
            from: userId,
            text: data.text,
            timestamp: Date.now()
          }))
        }
        break

      case 'TYPING':
        // แจ้งว่ากำลังพิมพ์
        const target = clients.get(data.toUserId)
        if (target) {
          target.send(JSON.stringify({ type: 'TYPING', from: userId }))
        }
        break
    }
  })

  ws.on('close', () => {
    clients.delete(userId)
    console.log(`User ${userId} disconnected`)
  })
})

// Client (Browser)
const ws = new WebSocket('ws://localhost:8080')

ws.onopen = () => {
  console.log('Connected!')

  // ส่งข้อความ
  ws.send(JSON.stringify({
    type: 'CHAT_MESSAGE',
    toUserId: 456,
    text: 'สวัสดีครับ!'
  }))
}

ws.onmessage = (event) => {
  const data = JSON.parse(event.data)
  if (data.type === 'CHAT_MESSAGE') {
    displayMessage(data.from, data.text)
  }
}

ws.onclose = () => {
  console.log('Disconnected, reconnecting...')
  setTimeout(() => connectWebSocket(), 3000)
}
```

### Socket.IO (ง่ายกว่า raw WebSocket)

```javascript
// Server
const { Server } = require('socket.io')
const io = new Server(3000, { cors: { origin: '*' } })

io.on('connection', (socket) => {
  console.log('User connected:', socket.id)

  // Join room
  socket.on('join-room', (roomId) => {
    socket.join(roomId)
  })

  // Chat message
  socket.on('chat-message', (data) => {
    io.to(data.roomId).emit('chat-message', {
      from: socket.id,
      text: data.text,
      timestamp: Date.now()
    })
  })

  socket.on('disconnect', () => {
    console.log('User disconnected:', socket.id)
  })
})

// Client
import { io } from 'socket.io-client'
const socket = io('http://localhost:3000')

socket.emit('join-room', 'room-123')
socket.emit('chat-message', { roomId: 'room-123', text: 'Hello!' })
socket.on('chat-message', (data) => displayMessage(data))
```

---

## 6. สรุป — เลือกอะไร

```
| Use Case                | เทคนิค              |
|------------------------|---------------------|
| Notifications          | SSE                 |
| Live feed / stock      | SSE                 |
| Chat application       | WebSocket           |
| Online game            | WebSocket           |
| Collaborative editing  | WebSocket           |
| Simple status check    | Long Polling        |
| Dashboard refresh      | SSE หรือ Short Polling |

กฎง่ายๆ:
- Server → Client เท่านั้น? → SSE
- สองทาง (Client ↔ Server)? → WebSocket
- ง่ายๆ ไม่ต้อง real-time มาก? → Long Polling
```
