# سوابق تصمیم‌های معماری (ADR)

هر فایل یک تصمیم را ثبت می‌کند. هر کدام زمینه، تصمیم، گزینه‌های بررسی‌شده (معمولاً در سه سطح بلوغ: حداقلی، حد وسطِ انتخاب‌شده، و enterprise کامل)، دلیل رد بقیه، پیامدها، و شرط قابل‌اندازه‌گیری برای بازنگری را ثبت می‌کند.

مقادیر وضعیت: `Proposed`، `Accepted`، `Superseded by ADR-NNNN`.

## تصمیم‌های طراحی

| ADR | تصمیم | وضعیت |
| --- | --- | --- |
| [0001](0001-system-boundary.md) | سرویس ظرفیت را رزرو می‌کند؛ ساخت سرویس را orchestrate نمی‌کند | Accepted |
| [0002](0002-resource-model.md) | منابع به‌صورت poolها، classها و inventoryهای مبتنی بر شمارنده مدل می‌شوند | Accepted |
| [0003](0003-ledger-and-counters.md) | ledger فقط‌افزودنی (append-only) منبع حقیقت است؛ شمارنده‌ها در همان تراکنش به‌روز می‌شوند | Accepted |
| [0004](0004-concurrency-control.md) | کنترل هم‌زمانی با به‌روزرسانی‌های اتمیک شرطی در یک lock order ثابت | Accepted |
| [0005](0005-synchronous-hold-with-lease.md) | تصمیم hold به‌صورت هم‌زمان (synchronous) همراه با یک lease محدود به زمان | Accepted |
| [0006](0006-idempotency.md) | کلیدهای idempotency کلاینت در همان تراکنش ذخیره می‌شوند | Accepted |
| [0007](0007-quota-in-the-ledger.md) | سهمیه‌ها شمارنده‌هایی در همان تراکنش‌اند؛ holdها هم حساب می‌شوند | Accepted |
| [0008](0008-failure-mode-and-regional-cells.md) | سازگاری بر دسترس‌پذیری مقدم است؛ fail closed؛ یک cell برای هر منطقه | Accepted |
| [0009](0009-transactional-outbox.md) | رویدادها از طریق یک transactional outbox که با تاریخچهٔ رویدادها ادغام شده | Accepted |
| [0010](0010-modular-monolith.md) | modular monolith با سه نقش اجرایی | Accepted |
| [0021](0021-shortage-responses-and-wait-queue.md) | سه نوع پاسخ به کمبود؛ انتظار اختیاری (opt-in) و محدود | Accepted |
| [0022](0022-two-state-machines-effective-state.md) | دو DFA؛ وضعیت مؤثر هنگام خواندن محاسبه می‌شود | Accepted |
| [0023](0023-clean-architecture.md) | clean architecture؛ invariantهای بین aggregateها پشت یک port و اعمال‌شده در SQL | Accepted |
| [0024](0024-lean-data-model.md) | مدل دادهٔ کم‌حجم برای پیاده‌سازی تحویلی | Accepted |

## تصمیم‌های فناوری

| ADR | تصمیم | وضعیت |
| --- | --- | --- |
| [0011](0011-language-dotnet.md) | C# روی .NET 10 با ASP.NET Core | Accepted |
| [0012](0012-database-postgresql.md) | PostgreSQL به‌عنوان تنها منبع حقیقت | Accepted |
| [0013](0013-data-access-npgsql-dapper.md) | Npgsql و Dapper با SQL دست‌نویس؛ migrationها با DbUp | Accepted |
| [0014](0014-rest-api.md) | REST با OpenAPI و Problem Details | Accepted |
| [0015](0015-no-redis-in-v1.md) | بدون Redis در نسخهٔ اول | Accepted |
| [0016](0016-message-broker-rabbitmq.md) | RabbitMQ به‌عنوان event broker | Accepted |
| [0017](0017-no-workflow-engine.md) | بدون workflow engine | Accepted |
| [0018](0018-postgresql-high-availability.md) | CloudNativePG با synchronous replication (فقط در طراحی) | Accepted |
| [0019](0019-observability.md) | OpenTelemetry و Prometheus | Accepted |
| [0020](0020-delivery-and-testing.md) | تحویل با Docker Compose؛ آزمون‌ها روی PostgreSQL واقعی | Accepted |

## قالب

```markdown
# ADR-NNNN: Title

- Status: Accepted
- Date: YYYY-MM-DD
- Related: …

## Context
## Decision
## Options considered
| Level | Option | Verdict |
## Consequences
## Revisit when
```
