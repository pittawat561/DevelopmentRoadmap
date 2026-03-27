# AI in Development — AI ในการพัฒนาซอฟต์แวร์

> ครอบคลุม How LLMs Work, AI vs Traditional Coding, Embeddings, Vectors, RAGs, AI Coding Tools

---

## 1. How LLMs Work — LLM ทำงานอย่างไร

### LLM คืออะไร

LLM (Large Language Model) คือ โมเดล AI ขนาดใหญ่ที่ถูกฝึกจากข้อความจำนวนมหาศาล เพื่อให้สามารถ **เข้าใจและสร้างภาษา** ได้

```
ตัวอย่าง LLMs:
├── GPT-4, GPT-4o          (OpenAI)
├── Claude 3.5, Claude 4   (Anthropic)
├── Gemini                  (Google)
├── LLaMA 3                (Meta — open source)
└── Mistral                 (Mistral AI — open source)
```

### หลักการทำงาน

```
LLM ทำงานโดยการ "ทำนายคำถัดไป" (Next Token Prediction)

Input:  "The capital of France is"
                                    ↓ LLM คำนวณความน่าจะเป็น
Output: "Paris" (95%), "a" (2%), "the" (1%), ...

ขั้นตอน:
1. Tokenization — แบ่งข้อความเป็น tokens (ชิ้นเล็กๆ)
   "Hello world" → ["Hello", " world"]
   "สวัสดี" → ["ส", "วัส", "ดี"]

2. Embedding — แปลง token เป็นตัวเลข (vector)
   "Hello" → [0.12, -0.34, 0.56, ...]  (มิติสูงมาก เช่น 4096)

3. Transformer — ประมวลผลด้วย attention mechanism
   - ดูว่าแต่ละคำสัมพันธ์กับคำอื่นอย่างไร
   - "The cat sat on the ___" → ดูว่า "cat" + "sat" + "on" ชี้ไปที่อะไร

4. Output — ทำนาย token ถัดไป ทำซ้ำจนจบประโยค
```

### Transformer Architecture (ภาพรวม)

```
┌─────────────────────────────────────────┐
│            Transformer Model            │
│                                         │
│  Input: "What is Python?"               │
│         ↓                               │
│  ┌─────────────┐                        │
│  │ Tokenizer   │  แบ่งเป็น tokens       │
│  └──────┬──────┘                        │
│         ↓                               │
│  ┌─────────────┐                        │
│  │ Embedding   │  แปลงเป็น vectors      │
│  └──────┬──────┘                        │
│         ↓                               │
│  ┌─────────────┐                        │
│  │ Attention   │  × หลายชั้น (layers)    │
│  │ Layers      │  หาความสัมพันธ์ระหว่างคำ │
│  └──────┬──────┘                        │
│         ↓                               │
│  ┌─────────────┐                        │
│  │ Output      │  ทำนายคำถัดไป          │
│  │ Layer       │                        │
│  └──────┬──────┘                        │
│         ↓                               │
│  Output: "Python is a programming..."   │
└─────────────────────────────────────────┘

Attention Mechanism:
"The cat sat on the mat because it was tired"
                                   ↑
                          "it" หมายถึง "cat" (ไม่ใช่ "mat")
                          Attention ช่วยให้ model เข้าใจสิ่งนี้
```

### Parameters & Training

```
Parameters (น้ำหนัก):
- GPT-3:     175 Billion parameters
- GPT-4:     ~1.8 Trillion (rumored)
- LLaMA 3:   8B / 70B / 405B
- Mistral:   7B / 8x7B (Mixture of Experts)

Training Process:
1. Pre-training   — เรียนจากข้อความมหาศาล (internet, books, code)
2. Fine-tuning    — ปรับแต่งให้ทำงานเฉพาะทาง
3. RLHF           — เรียนจาก feedback ของมนุษย์ให้ตอบดีขึ้น

Context Window (หน่วยความจำ):
- GPT-4:    128K tokens (~300 หน้า)
- Claude:   200K tokens (~500 หน้า)
- Gemini:   1M+ tokens
```

### Temperature & Parameters ที่สำคัญ

```
Temperature — ควบคุมความสุ่ม
├── 0.0: ตอบแบบเดิมทุกครั้ง (deterministic)
├── 0.7: สมดุลดี (default)
└── 1.0+: ตอบหลากหลาย แต่อาจผิดมากขึ้น

Top-p (Nucleus Sampling):
├── 0.1: เลือกจากคำที่น่าจะเป็นที่สุดเท่านั้น
└── 0.9: เลือกจากคำที่หลากหลายกว่า

Max Tokens — จำกัดความยาว output

// ตัวอย่างการเรียก API
{
  "model": "gpt-4",
  "messages": [{"role": "user", "content": "Explain Python"}],
  "temperature": 0.7,
  "max_tokens": 500
}
```

---

## 2. AI vs Traditional Coding

```
Traditional Coding:
- เขียนกฎชัดเจนทุกอย่าง (if/else, loops)
- ผลลัพธ์แน่นอน (deterministic)
- เหมาะกับ: CRUD, business logic, calculations

AI/ML Approach:
- ให้ model เรียนรู้จากข้อมูล
- ผลลัพธ์เป็นความน่าจะเป็น (probabilistic)
- เหมาะกับ: NLP, image recognition, recommendations

| หัวข้อ              | Traditional         | AI-Powered          |
|--------------------|---------------------|---------------------|
| Spam Detection     | กฎ: ถ้ามีคำ X = spam | เรียนจากตัวอย่าง spam  |
| Search             | Keyword matching     | Semantic search      |
| Translation        | กฎไวยากรณ์            | Neural translation   |
| Code Generation    | Templates            | LLM generates code   |
| Error Handling     | กฎเฉพาะทุกกรณี       | AI ช่วยหาวิธีแก้      |

เมื่อไหร่ใช้ AI:
✅ งานที่ pattern ซับซ้อน ยากจะเขียนกฎ
✅ งานที่ต้องเข้าใจภาษาธรรมชาติ
✅ งานที่มีข้อมูลตัวอย่างมาก

เมื่อไหร่ใช้ Traditional:
✅ งานที่ต้องการผลลัพธ์แม่นยำ 100%
✅ งานที่กฎชัดเจน (financial calculations)
✅ งานที่ข้อมูลน้อย
```

---

## 3. Embeddings — การแปลงข้อมูลเป็นตัวเลข

### Embedding คืออะไร

Embedding คือการแปลง **ข้อความ (หรือข้อมูลอื่น) ให้เป็น vector ตัวเลข** ที่คอมพิวเตอร์เข้าใจ โดยข้อมูลที่ "คล้ายกัน" จะมี vector ที่ "ใกล้กัน"

```
"cat"  → [0.12, 0.45, -0.23, 0.67, ...]
"dog"  → [0.15, 0.42, -0.20, 0.65, ...]   ← ใกล้กับ cat
"car"  → [0.89, -0.34, 0.56, 0.12, ...]   ← ไกลจาก cat

ความหมายที่คล้ายกัน → Vector ใกล้กัน
ความหมายที่ต่างกัน → Vector ไกลกัน

ตัวอย่างที่มีชื่อเสียง:
King - Man + Woman ≈ Queen
(vector arithmetic ที่สะท้อนความหมาย!)
```

### ทำไมต้องใช้ Embeddings

```
ปัญหา: คอมพิวเตอร์ไม่เข้าใจข้อความ
คำตอบ: แปลงข้อความเป็นตัวเลขที่เก็บ "ความหมาย" ไว้

ใช้งาน:
1. Semantic Search    — ค้นหาตามความหมาย ไม่ใช่แค่ keyword
2. Recommendation     — แนะนำสิ่งที่คล้ายกัน
3. Clustering         — จัดกลุ่มข้อมูลที่เกี่ยวข้อง
4. RAG                — ดึงข้อมูลที่เกี่ยวข้องมาให้ LLM
```

### การใช้งาน Embeddings

```javascript
// สร้าง embeddings ด้วย OpenAI API
const { OpenAI } = require('openai')
const openai = new OpenAI()

async function getEmbedding(text) {
  const response = await openai.embeddings.create({
    model: "text-embedding-3-small",
    input: text
  })
  return response.data[0].embedding  // [0.012, -0.034, 0.056, ...]
}

// สร้าง embedding
const vec1 = await getEmbedding("How to learn programming")
const vec2 = await getEmbedding("Best way to start coding")
const vec3 = await getEmbedding("How to cook pasta")

// vec1 กับ vec2 จะ "ใกล้กัน" (ความหมายคล้าย)
// vec1 กับ vec3 จะ "ไกลกัน" (ความหมายต่าง)
```

### วัดความคล้ายด้วย Cosine Similarity

```javascript
// Cosine Similarity: วัดว่า 2 vectors ชี้ไปทิศเดียวกันไหม
// ค่า 1.0 = เหมือนกัน, 0.0 = ไม่เกี่ยว, -1.0 = ตรงข้าม

function cosineSimilarity(vecA, vecB) {
  const dotProduct = vecA.reduce((sum, a, i) => sum + a * vecB[i], 0)
  const magnitudeA = Math.sqrt(vecA.reduce((sum, a) => sum + a * a, 0))
  const magnitudeB = Math.sqrt(vecB.reduce((sum, b) => sum + b * b, 0))
  return dotProduct / (magnitudeA * magnitudeB)
}

const sim1 = cosineSimilarity(vec1, vec2)  // ~0.92 (คล้ายมาก)
const sim2 = cosineSimilarity(vec1, vec3)  // ~0.31 (ไม่คล้าย)
```

---

## 4. Vectors & Vector Databases

### Vector Database คืออะไร

Database ที่ออกแบบมาเพื่อ **เก็บและค้นหา vectors** ได้เร็ว โดยเฉพาะการหาว่า vector ไหน "ใกล้" กับ query มากที่สุด

```
Traditional DB:
SELECT * FROM products WHERE name LIKE '%phone%'
→ ค้นหาตาม keyword เท่านั้น

Vector DB:
query = embed("I need a device to make calls and browse internet")
results = vector_db.search(query, top_k=5)
→ ค้นหาตาม "ความหมาย" → เจอ smartphone, tablet, etc.
```

### Vector Databases ที่นิยม

```
| Database     | ประเภท              | เด่นเรื่อง                |
|-------------|---------------------|--------------------------|
| Pinecone    | Cloud (managed)     | ง่าย, scalable, serverless|
| Weaviate    | Open source         | hybrid search, modules    |
| Qdrant      | Open source         | เร็ว, Rust-based          |
| ChromaDB    | Open source         | ง่ายมาก, เหมาะเริ่มต้น     |
| pgvector    | PostgreSQL extension| ใช้กับ PostgreSQL ที่มีอยู่  |
| Milvus      | Open source         | scale ใหญ่มาก             |
```

### ตัวอย่าง: Semantic Search ด้วย ChromaDB

```python
# pip install chromadb openai

import chromadb

# สร้าง client
client = chromadb.Client()
collection = client.create_collection("my_docs")

# เพิ่มเอกสาร (ChromaDB สร้าง embedding อัตโนมัติ)
collection.add(
    documents=[
        "Python is great for data science and machine learning",
        "JavaScript is used for web development",
        "Docker helps with containerization and deployment",
        "Redis is an in-memory cache database",
    ],
    ids=["doc1", "doc2", "doc3", "doc4"]
)

# ค้นหาตามความหมาย
results = collection.query(
    query_texts=["How to build websites?"],
    n_results=2
)
# → เจอ "JavaScript is used for web development" (ใกล้ที่สุด)
```

---

## 5. RAG — Retrieval-Augmented Generation

### RAG คืออะไร

RAG คือเทคนิคที่ **ดึงข้อมูลที่เกี่ยวข้อง (Retrieval) มาเสริมให้ LLM (Generation)** เพื่อให้ตอบได้แม่นยำกว่าและอ้างอิงข้อมูลจริงได้

```
ปัญหาของ LLM ธรรมดา:
❌ ข้อมูลเก่า (training cutoff date)
❌ ไม่รู้ข้อมูลภายในองค์กร
❌ อาจ "แต่งคำตอบ" (hallucination)

RAG แก้ปัญหา:
✅ ดึงข้อมูลล่าสุดจาก database
✅ ใช้เอกสารภายในองค์กรได้
✅ ลด hallucination เพราะมีข้อมูลอ้างอิง
```

### RAG ทำงานอย่างไร

```
ขั้นตอน RAG:

1. INDEXING (ทำครั้งเดียว — เตรียมข้อมูล)
   เอกสาร → แบ่งเป็นชิ้น (chunks) → สร้าง Embeddings → เก็บใน Vector DB

   ┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐
   │ Documents│ →   │ Chunking │ →   │ Embedding│ →   │ Vector   │
   │ PDF, Web │     │ 500 chars│     │ OpenAI   │     │ Database │
   │ Docs     │     │ per chunk│     │ API      │     │ Pinecone │
   └──────────┘     └──────────┘     └──────────┘     └──────────┘

2. RETRIEVAL (ทุกครั้งที่ถาม)
   คำถาม → Embedding → ค้นหา chunks ที่เกี่ยวข้อง → ส่งให้ LLM

   User: "What is our refund policy?"
         ↓
   Embed query → Search Vector DB → Top 3 relevant chunks
         ↓
   ┌─────────────────────────────────────────────┐
   │ Prompt to LLM:                              │
   │                                             │
   │ Context:                                    │
   │ [Chunk 1: "Our refund policy allows..."]    │
   │ [Chunk 2: "Customers can request..."]       │
   │ [Chunk 3: "Refunds are processed..."]       │
   │                                             │
   │ Question: "What is our refund policy?"      │
   │                                             │
   │ Answer based on the context above.          │
   └─────────────────────────────────────────────┘
         ↓
   LLM generates answer using the retrieved context
```

### ตัวอย่าง RAG ฉบับเต็ม

```javascript
// RAG Pipeline ด้วย Node.js

const { OpenAI } = require('openai')
const { ChromaClient } = require('chromadb')

const openai = new OpenAI()
const chroma = new ChromaClient()

// ===== ขั้นตอน 1: Indexing =====

// 1.1 แบ่งเอกสารเป็น chunks
function chunkText(text, chunkSize = 500, overlap = 50) {
  const chunks = []
  for (let i = 0; i < text.length; i += chunkSize - overlap) {
    chunks.push(text.slice(i, i + chunkSize))
  }
  return chunks
}

// 1.2 สร้าง embeddings และเก็บใน Vector DB
async function indexDocuments(documents) {
  const collection = await chroma.getOrCreateCollection({ name: "my_kb" })

  for (const doc of documents) {
    const chunks = chunkText(doc.content)

    for (let i = 0; i < chunks.length; i++) {
      const embedding = await getEmbedding(chunks[i])

      await collection.add({
        ids: [`${doc.id}_chunk_${i}`],
        embeddings: [embedding],
        documents: [chunks[i]],
        metadatas: [{ source: doc.title }]
      })
    }
  }
}

// ===== ขั้นตอน 2: Retrieval + Generation =====

async function askQuestion(question) {
  const collection = await chroma.getCollection({ name: "my_kb" })

  // 2.1 ค้นหา chunks ที่เกี่ยวข้อง
  const queryEmbedding = await getEmbedding(question)
  const results = await collection.query({
    queryEmbeddings: [queryEmbedding],
    nResults: 3
  })

  const context = results.documents[0].join('\n\n')

  // 2.2 ส่งให้ LLM พร้อม context
  const response = await openai.chat.completions.create({
    model: "gpt-4",
    messages: [
      {
        role: "system",
        content: `Answer questions based on the provided context.
                  If you don't know, say "I don't have that information."
                  Always cite which part of the context you used.`
      },
      {
        role: "user",
        content: `Context:\n${context}\n\nQuestion: ${question}`
      }
    ],
    temperature: 0.3  // ต่ำ = อ้างอิงข้อมูลมากกว่าแต่ง
  })

  return response.choices[0].message.content
}

// ใช้งาน
const answer = await askQuestion("What is our return policy?")
console.log(answer)
```

### Chunking Strategies

```
เทคนิคการแบ่ง chunks:

1. Fixed Size — แบ่งตามจำนวนตัวอักษร (ง่ายที่สุด)
   "Lorem ipsum dolor sit amet..." → 500 chars per chunk

2. Sentence-based — แบ่งตามประโยค
   ดีกว่า fixed size เพราะไม่ตัดกลางประโยค

3. Paragraph-based — แบ่งตามย่อหน้า
   เหมาะกับเอกสารที่มีโครงสร้าง

4. Semantic Chunking — แบ่งตามความหมาย
   ใช้ AI ช่วยหาจุดที่ควรแบ่ง (แม่นที่สุด แต่ช้า)

Tips:
- Chunk size: 200-1000 chars (ขึ้นกับ model)
- Overlap: 10-20% (ป้องกันข้อมูลหายตรงรอยต่อ)
- เก็บ metadata (ชื่อเอกสาร, หน้า, section)
```

---

## 6. AI Assisted Coding — เครื่องมือ AI ช่วยเขียนโค้ด

### เครื่องมือหลัก

```
| เครื่องมือ       | บริษัท     | ทำอะไร                              |
|-----------------|-----------|-------------------------------------|
| Claude Code     | Anthropic | AI agent เขียนโค้ดใน terminal        |
| Cursor          | Cursor    | IDE ที่มี AI built-in               |
| GitHub Copilot  | GitHub    | AI autocomplete ใน VS Code/IDE      |
| Windsurf        | Codeium   | AI-powered IDE                     |
```

### วิธีใช้ AI Coding Tools อย่างมีประสิทธิภาพ

```
✅ ใช้ AI ดี:
- ช่วยเขียน boilerplate code
- อธิบายโค้ดที่ไม่เข้าใจ
- เขียน unit tests
- Refactor โค้ด
- แปลงภาษา (Python → JavaScript)
- เขียน documentation
- Debug — หาจุดที่ผิด

❌ อย่าทำ:
- Copy โค้ดจาก AI โดยไม่เข้าใจ
- ใช้ AI เขียนโค้ด security-critical โดยไม่ review
- เชื่อ AI 100% — ต้องตรวจสอบเสมอ
- ใส่ข้อมูลลับ (API keys, passwords) ลงใน AI

Best Practices:
1. เข้าใจพื้นฐานก่อน แล้วค่อยใช้ AI เสริม
2. อ่านโค้ดที่ AI สร้างให้เข้าใจก่อน commit
3. ใช้ AI เป็น "pair programmer" ไม่ใช่ "replacement"
4. เขียน prompt ให้ชัดเจน = ได้โค้ดที่ดีกว่า
```

### Building AI-Powered Features

```
สิ่งที่ Backend Developer ควรรู้:

1. Streaming — ส่ง response ทีละ chunk (เหมือน ChatGPT พิมพ์ทีละคำ)

   // Server-Sent Events (SSE)
   app.get('/api/chat', async (req, res) => {
     res.setHeader('Content-Type', 'text/event-stream')

     const stream = await openai.chat.completions.create({
       model: "gpt-4",
       messages: [...],
       stream: true
     })

     for await (const chunk of stream) {
       const content = chunk.choices[0]?.delta?.content || ''
       res.write(`data: ${JSON.stringify({ content })}\n\n`)
     }
     res.end()
   })

2. Structured Outputs — บังคับให้ AI ตอบเป็น JSON ที่กำหนด

   const response = await openai.chat.completions.create({
     model: "gpt-4",
     messages: [...],
     response_format: {
       type: "json_schema",
       json_schema: {
         name: "product_review",
         schema: {
           type: "object",
           properties: {
             sentiment: { type: "string", enum: ["positive", "negative", "neutral"] },
             score: { type: "number", minimum: 1, maximum: 5 },
             summary: { type: "string" }
           }
         }
       }
     }
   })

3. Function Calling — ให้ AI เรียกใช้ฟังก์ชันของเราได้

   const tools = [{
     type: "function",
     function: {
       name: "get_weather",
       description: "Get current weather for a location",
       parameters: {
         type: "object",
         properties: {
           location: { type: "string", description: "City name" }
         },
         required: ["location"]
       }
     }
   }]

   // AI จะตัดสินใจเรียก get_weather เมื่อผู้ใช้ถามเรื่องอากาศ
```
