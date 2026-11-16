# ADR-0013: Npgsql و Dapper با SQL دست‌نویس؛ migrationها با DbUp

- وضعیت: Accepted
- تاریخ: `2026-11-16`
- مرتبط: [ADR-0004](0004-concurrency-control.md)، [ADR-0023](0023-clean-architecture.md)

## زمینه

درستی سیستم به چند دستور SQL دقیق وابسته است: updateهای شرطی، lock order، یک round trip برای هر hold، و `SKIP LOCKED`. این دستورها باید قابل دیدن و قابل بازبینی باشند.

## تصمیم

- Npgsql (ADO.NET) از طریق `NpgsqlDataSource`؛ Dapper فقط برای map کردن نتایج.
- SQL در فایل‌های embedded با پسوند `.sql` در لایهٔ infrastructure قرار دارد و مثل کد بازبینی می‌شود.
- مسیر hold دستورهایش را به‌صورت یک `NpgsqlBatch` می‌فرستد.
- Migrationها با DbUp: اسکریپت‌های SQL ساده و نسخه‌دار، که با یک دستور جداگانهٔ `migrate` پیش از شروع نقش‌ها اجرا می‌شوند.

## گزینه‌های بررسی‌شده

| گزینه | نتیجه |
| --- | --- |
| Npgsql + Dapper | **انتخاب شد** |
| EF Core برای نوشتن‌ها | رد شد. change tracker و SQL تولیدشده مرزهای قفل و round tripها را پنهان می‌کنند؛ الگوی پیش‌فرض هم‌زمانی آن tokenهای optimistic است که [ADR-0004](0004-concurrency-control.md) آن را برای hot rowها رد می‌کند. |
| EF Core فقط برای خواندن‌ها | رد شد. شدنی است، اما دو ابزار دسترسی به داده برای حدود 20 query ارزشش را ندارد. |
| EF Core migrations | رد شد. به مدل EF وابسته است که استفاده نمی‌شود. |

## پیامدها

- SQL بیشتری باید دستی نوشته شود؛ هر دستور صریح است.
- Property test بررسی می‌کند که SQL و دامنه با هم سازگارند ([spec 11 §3](../specs/11-testing.md#3-property-test-sql-agrees-with-the-domain)).

## چه زمانی بازنگری شود

انتظار تغییر نمی‌رود.
