# ADR-0012: PostgreSQL به‌عنوان تنها منبع حقیقت

- وضعیت: Accepted
- تاریخ: `2026-11-16`
- مرتبط: [ADR-0004](0004-concurrency-control.md)، [ADR-0018](0018-postgresql-high-availability.md)، [پایگاه داده](../database/database.md)

## زمینه

پایگاه داده باید این موارد را فراهم کند:

1. تراکنش‌های ACID چندسطری.
2. یک `UPDATE` شرطی که نتیجه‌اش را در همان دستور برگرداند.
3. خواندن به سبک صف که از سطرهای قفل‌شده رد شود (sweeper، relay).
4. Partial index فقط روی رزروهای HELD و WAITING.
5. Synchronous replication با RPO صفر.
6. بهره‌برداری ساده روی Kubernetes بدون هزینهٔ لایسنس.

## تصمیم

PostgreSQL 17 یا جدیدتر تنها منبع حقیقت برای ظرفیت، سهمیه‌ها، رزروها، ledger، idempotency keyها و رویدادهاست.

## گزینه‌های بررسی‌شده

| معیار | PostgreSQL | MySQL (InnoDB) | SQL Server | SQLite |
| --- | --- | --- | --- | --- |
| نویسنده‌های هم‌زمان | Row lock با MVCC | Row lock با MVCC | Row lock | یک نویسنده برای هر پایگاه داده |
| Update شرطی که سطرها را برگرداند | `RETURNING` | ندارد؛ دستور دوم لازم است | `OUTPUT` | `RETURNING` |
| رد شدن از سطرهای قفل‌شده | `SKIP LOCKED` | `SKIP LOCKED` (8.0+) | hint `READPAST` | ندارد |
| Partial index برای سطرهای HELD | دارد | ندارد | Filtered index | دارد |
| DDL تراکنشی برای migrationها | دارد | ندارد | دارد | دارد |
| Isolation پیش‌فرض | READ COMMITTED با MVCC | REPEATABLE READ با gap lock | READ COMMITTED مبتنی بر قفل، مگر اینکه RCSI فعال باشد | Serializable |
| مدل اتصال | یک process برای هر اتصال | یک thread برای هر اتصال | thread pool داخلی | درون process، بدون شبکه |
| Sync replication و failover | Quorum sync + operator | Semi-sync یا Group Replication | Always On؛ نسخهٔ Standard محدود به دو replica و یک پایگاه داده | ندارد |
| Kubernetes | operatorهای open-source بالغ | operatorهای open-source | از container پشتیبانی می‌کند؛ HA ابزار جداگانه لازم دارد | فقط یک pod |
| لایسنس | Open source | Open source | تجاری، به ازای هر core | Public domain |

**PostgreSQL — انتخاب شد.** هر شش نیاز را مستقیماً برآورده می‌کند، declarative partitioning را هم برای ledger اضافه می‌کند، و Npgsql یک driver بالغ برای .NET است. یک primary روی سخت‌افزار معمولی هزاران تراکنش نوشتن در ثانیه را پاسخ می‌دهد، یعنی چند برابر هدف 500/s.

**هم‌زمانی و threadها.** هم‌زمانی نویسنده‌ها را row lockها و MVCC تعیین می‌کنند، نه مدل threading سرور. تفاوت واقعی در هزینهٔ اتصال است: PostgreSQL برای هر اتصال یک process را fork می‌کند که از threadهای MySQL یا SQL Server گران‌تر است. نیاز حدود 300 اتصال کلاینت است (10 instance × 30)؛ PgBouncer در transaction mode آن‌ها را به چند ده اتصال سرور کاهش می‌دهد، پس این ضعف خنثی می‌شود.

**Scale روی Kubernetes.** CloudNativePG مدیریت failover، synchronous standbyها و backupها را بر عهده دارد؛ در ژانویهٔ 2025 به CNCF Sandbox پیوست و در نوامبر 2026 به Incubation ارتقا یافت ([EDB](https://enterprisedb.com/blog/postgres-belongs-kubernetes-cloudnativepg-reaches-cncf-incubation)). مسیر scale: اول عمودی، بعد یک read replica برای خواندن‌ها، و بعد یک cluster برای هر منطقه. Sharding هیچ‌وقت لازم نمی‌شود، چون هر تراکنش تک‌منطقه‌ای است.

**MySQL — رد شد، اما قابل دفاع است.** نزدیک‌ترین جایگزین. هزینه‌هایش برای این طراحی: `RETURNING` ندارد، پس هر hold به یک procedure یا یک دستور دوم برای هر سطر نیاز دارد؛ partial index ندارد، پس index انقضا کل تاریخچه را پوشش می‌دهد؛ gap lockها زیر REPEATABLE READ پیش‌فرض، انتظار برای قفل و deadlock را در range scanهای sweeper افزایش می‌دهند، مگر اینکه isolation پایین آورده شود؛ DDL تراکنشی نیست. اتصال‌های ارزان‌تر مزیت آن است. اگر تیم عملیات MySQL-محور باشد، گزینهٔ قابل قبولی است و تغییر فقط در لایهٔ repository می‌ماند.

**SQL Server — رد شد.** از نظر فنی قابل مقایسه است و طبیعی‌ترین همراه .NET. به چهار دلیل رد شد. لایسنس به ازای هر core، و availability groupهای نسخهٔ Standard به دو replica و یک پایگاه داده محدودند ([Microsoft Learn](https://learn.microsoft.com/sql/database-engine/availability-groups/windows/basic-availability-groups-always-on-availability-groups))، پس «یک synchronous standby از دو» به نسخهٔ Enterprise نیاز دارد. READ COMMITTED پیش‌فرض مبتنی بر قفل است، پس تا وقتی RCSI فعال نشود، خواندن‌های ظرفیت پشت نویسنده‌ها روی hot row منتظر می‌مانند. Lock escalation می‌تواند یک batch بزرگ sweeper را از row lock به table lock تبدیل کند که جلوی همهٔ holdها را می‌گیرد. و هیچ Kubernetes operator متن‌بازی با بلوغ مشابه وجود ندارد. تهیهٔ رسمی لایسنس هم برای یک شرکت در ایران مانع جداگانه‌ای است.

**SQLite — رد شد.** یک نویسنده برای کل پایگاه داده و بدون row lock. مهم‌تر اینکه یک فایل درون یک process است: چند instance از API نمی‌توانند آن را به اشتراک بگذارند، synchronous replication ندارد، و با مردن آن pod سیستم متوقف می‌شود. نمی‌تواند نیازمندی دسترس‌پذیری را برآورده کند. برای آزمون‌ها هم مناسب نیست، چون رفتار قفل‌گذاری‌اش با production فرق دارد.

**این‌ها هم رد شدند:** Redis به‌عنوان منبع حقیقت (asynchronous replication و fsync دوره‌ای باعث می‌شود holdهای تأییدشده هنگام failover از دست بروند)؛ CockroachDB، YugabyteDB، TiDB (مسئلهٔ نوشتن چندمنطقه‌ای را حل می‌کنند که regional cellها عمداً از آن پرهیز می‌کنند؛ consensus برای هر commit و retry بیشتر روی hot rowها)؛ MongoDB و Cassandra (invariantهای چندسطری و چندجدولی هستهٔ مسئله‌اند؛ lightweight transactionهای Cassandra کند هستند و counterهایش اینجا امن نیستند)؛ etcd (برای metadata کوچک ساخته شده، با محدودیت حجم چند گیگابایتی).

## پیامدها

- PgBouncer و یک operator به ردپای عملیاتی اضافه می‌شوند.
- سطرهای hot موجودی دائماً update می‌شوند: `fillfactor = 50` و autovacuum تهاجمی روی آن جدول جلوی bloat را می‌گیرند.

## چه زمانی بازنگری شود

وقتی استاندارد سازمان MySQL باشد، یا سهمیه‌ها باید در بین مناطق به‌صورت strongly consistent باشند (در آن صورت distributed SQL موضوعیت پیدا می‌کند).
