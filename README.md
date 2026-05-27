# KtD
Звіт з проектування системи: Варіант 4 — Груповий чат

Цей документ містить архітектурне проектування мінімальної системи обміну повідомленнями з фокусом на масштабування логіки доставки для групових чатів (віялове розсилання / fan-out).

Система підтримує:

    Надсилання повідомлень між користувачами (включаючи групи);

    Асинхронну доставку;

    Індивідуальні статуси повідомлень для кожного отримувача (відправлено / доставлено / прочитано);

    Обробку користувачів, які перебувають офлайн.

Частина 1 — Діаграма компонентів

Архітектура розділена на логічні блоки для чіткого розподілу відповідальності. Використовується черга повідомлень для асинхронного віялового розсилання (fan-out) та окремі сервіси для обробки онлайн (WebSocket) і офлайн (Push) клієнтів.
graph TB
  subgraph Clients
    CA[Client A]
    CB[Client B]
    CC[Client C]
  end

  subgraph Backend
    API[Backend API]
    Auth[Auth Service]
    MS[Message Service]
    GS[Group Service]
    DS[Delivery Service]
  end

  subgraph Storage
    DB[(Messages DB)]
    GDB[(Groups DB)]
  end

  subgraph Async
    Q[Message Queue\nfan-out jobs]
  end

  subgraph Delivery
    WS[WebSocket Gateway]
    PN[Push Notification\nService]
  end

  CA -->|POST /messages| API
  CB -->|WebSocket| WS
  CC -->|WebSocket| WS

  API --> Auth
  API --> MS
  MS --> GS
  GS --> GDB
  MS --> DB
  MS -->|enqueue fan-out| Q

  Q --> DS
  DS --> DB
  DS --> WS
  DS --> PN

  WS --> CB
  WS --> CC
  PN -->|FCM/APNs| CC


Частина 2 — Діаграма послідовності

Сценарій: Користувач А надсилає повідомлення в групу, де Користувач B перебуває онлайн, а Користувач C — офлайн.

Діаграма демонструє процес збереження повідомлення, генерації DeliveryRecord для кожного отримувача та асинхронну обробку розсилання через чергу.

sequenceDiagram
  participant A as User A (online)
  participant API as Backend API
  participant MS as Message Service
  participant GS as Group Service
  participant DB as Messages DB
  participant Q as Message Queue
  participant DS as Delivery Service
  participant WS as WebSocket Gateway
  participant PN as Push Notifications
  participant B as User B (online)
  participant C as User C (offline)

  A->>API: POST /groups/42/messages { text: "Hello!" }
  API->>MS: createGroupMessage(groupId=42, senderId=A)

  MS->>GS: getMembers(groupId=42)
  GS-->>MS: [A, B, C]

  MS->>DB: save(message, status=SENT)
  MS->>DB: createDeliveryRecord(msgId, recipient=B, status=PENDING)
  MS->>DB: createDeliveryRecord(msgId, recipient=C, status=PENDING)

  MS->>Q: enqueue FanOutJob { msgId, recipients: [B, C] }
  API-->>A: 202 Accepted { messageId }

  Note over Q, DS: Асинхронна обробка fan-out

  Q->>DS: processFanOut(msgId, recipients=[B, C])

  DS->>WS: isOnline(B)?
  WS-->>DS: true

  DS->>WS: push(B, message)
  WS->>B: { type: "new_message", data: message }
  B-->>WS: ACK
  WS-->>DS: delivered
  DS->>DB: updateDeliveryRecord(msgId, B, status=DELIVERED)

  DS->>WS: isOnline(C)?
  WS-->>DS: false

  DS->>PN: sendPush(C, { preview: "A: Hello!" })
  PN-->>DS: push_sent
  DS->>DB: updateDeliveryRecord(msgId, C, status=PUSH_SENT)

  Note over B: B відкриває повідомлення
  B->>API: POST /messages/readReceipt { msgId }
  API->>DB: updateDeliveryRecord(msgId, B, status=READ)
  API->>WS: notify(A, { msgId, readBy: B })
  WS->>A: { type: "read_receipt", msgId, by: B }

  Частина 3 — Діаграма станів

Об'єктом для діаграми станів обрано не саме повідомлення загалом, а DeliveryRecord — запис про доставку конкретному отримувачу в межах групи. Це дозволяє відстежувати статус read/delivered індивідуально.

stateDiagram-v2
  direction LR

  [*] --> PENDING : DeliveryRecord створено\n(після збереження повідомлення)

  PENDING --> IN_FLIGHT : Fan-out job\nпочав обробку

  IN_FLIGHT --> DELIVERED : WebSocket ACK\nотримано від клієнта

  IN_FLIGHT --> PUSH_SENT : Користувач офлайн —\nвідправлено push

  IN_FLIGHT --> FAILED : Таймаут або\nпомилка доставки

  PUSH_SENT --> DELIVERED : Користувач відкрив\nдодаток → WebSocket ACK

  FAILED --> PENDING : Retry (з backoff)\nдо maxAttempts

  PENDING --> EXPIRED : TTL вичерпано\n(наприклад, 30 днів офлайн)

  DELIVERED --> READ : Клієнт надіслав\nread receipt

  READ --> [*]
  EXPIRED --> [*]

  Частина 4 — ADR (Architecture Decision Record)
# ADR-001: Fan-out on Write для доставки групових повідомлень

Status
Accepted

Context
При відправці повідомлення в груповий чат потрібно доставити його всім N учасникам. Існують два основні підходи:

    Fan-out on Write — при отриманні повідомлення одразу створюються N задач/копій для доставки кожному учаснику.

    Fan-out on Read — повідомлення зберігається один раз, кожен клієнт самостійно опитує сервер і "забирає" своє.

Decision
Використовуємо Fan-out on Write через асинхронну чергу:

    Message Service зберігає одне загальне повідомлення.

    Для кожного учасника групи (крім відправника) створюється DeliveryRecord зі статусом PENDING.

    В чергу додається FanOutJob з переліком отримувачів.

    Delivery Service обробляє job: для онлайн-користувачів використовується WebSocket, для офлайн — Push Notification.

Alternatives Considered

    Fan-out on Read (відхилено для малих груп)

        [+] Простіше зберігання — лише одне загальне повідомлення.

        [-] Клієнт повинен постійно поллити або підписуватись на оновлення.

        [-] Значно важче відстежувати per-recipient статуси (хто саме прочитав/отримав).

        [-] Більше навантаження при кожному відкритті чату.

    Hybrid (розглядається для груп >1000 учасників)

        Для великих broadcast-каналів fan-out on write може створювати мільйони записів. У таких випадках доцільно переходити на fan-out on read з offset-based cursors.

Consequences

    Позитивні:

        Надійна доставка: кожен отримувач має власний запис у БД і статус.

        Легко реалізувати механізм retry для конкретного отримувача у разі збою.

        Read receipts природно прив'язані до DeliveryRecord.

        Низька затримка для онлайн-користувачів (push відбувається безпосередньо через WebSocket).

    Негативні:

        Зростання обсягу даних у базі: M повідомлень × N учасників = кількість записів статусів.

        При групах >500 осіб fan-out job стає важким для обробки (потрібен batch processing).

        Виникає потреба в логіці очищення бази (видалення EXPIRED записів за допомогою TTL).
