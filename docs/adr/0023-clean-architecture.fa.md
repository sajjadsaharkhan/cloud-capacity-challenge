# ADR-0023: Clean architecture؛ invariantهای بین‌aggregate پشت یک port، اعمال‌شده در SQL

- وضعیت: Accepted
- تاریخ: `2026-11-16`
- مرتبط: [معماری](../architecture/backend-architecture.md)، [ADR-0004](0004-concurrency-control.md)، [ADR-0013](0013-data-access-npgsql-dapper.md)

## زمینه

backend باید از clean architecture پیروی کند: قواعد کسب‌وکار در domain، و frameworkها و I/O در لبه‌ها. مهم‌ترین قاعده، یعنی `held + used ≤ sellable`، را نمی‌شود در حافظهٔ application بررسی کرد: دو درخواست هر دو می‌بینند که یک واحد باقی مانده است.

## تصمیم

چهار پروژه با قاعدهٔ وابستگی Domain ← Application ← Infrastructure, Host؛ که با یک architecture test بررسی می‌شود.

قواعد بر اساس چیزی که با آن سروکار دارند جای‌گذاری می‌شوند:

| قاعده با چه سروکار دارد | محل قرارگیری | اعمال‌شده توسط |
| --- | --- | --- |
| یک رزرو (گذارهای حالت، مهلت‌ها، تمدید) | Domain (`Reservation`، `RequestStateMachine`) | در C# روی ردیفی که با `FOR UPDATE` قفل شده تصمیم‌گیری می‌شود و با version check نوشته می‌شود |
| counterهای مشترک (ظرفیت، سهمیه) | Domain فرمول را تعریف می‌کند (`SellableCapacity`)؛ port یعنی `ICapacityLedger` invariant را بیان می‌کند | `UPDATE` شرطی و CHECK constraint در adapter لایهٔ infrastructure |

domain مقدار `now` را از transaction دریافت می‌کند؛ هرگز خودش ساعت را نمی‌خواند. application مالک مرزهای transaction (یک use case، یک transaction) و lock order سراسری است.

## گزینه‌های بررسی‌شده

| گزینه | نتیجه |
| --- | --- |
| Clean architecture با portهای حامل invariant | **انتخاب‌شده** |
| بررسی کاملاً domain-driven ظرفیت در C# پیش از ذخیره | رد شد. مستعد race است؛ فقط با یک global lock درست کار می‌کند که throughput را نابود می‌کند. |
| Transaction script با SQL در endpointها | رد شد. قواعد پراکنده می‌شوند و بدون پایگاه داده قابل آزمون نیستند. |
| Stored procedureهایی که همهٔ منطق را نگه می‌دارند | رد شد. آزمون و بازبینی‌شان سخت است؛ منطق domain را به پایگاه داده گره می‌زند. |

## پیامدها

- domain بدون پایگاه داده به‌طور کامل unit test می‌شود.
- infrastructure شامل SQLی است که یک invariant را اعمال می‌کند، و بعضی خواننده‌ها این را منطق کسب‌وکار بیرون از domain می‌بینند. این محدود به `ICapacityLedger` است، و یک property test بررسی می‌کند که با فرمول domain هم‌خوانی دارد.

## چه زمانی بازنگری شود

انتظار تغییر نمی‌رود.
