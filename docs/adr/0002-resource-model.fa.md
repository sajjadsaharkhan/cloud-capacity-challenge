# ADR-0002: منابع به‌صورت poolها، classها و inventoryهای مبتنی بر شمارنده مدل می‌شوند

- وضعیت: Accepted
- تاریخ: `2026-11-16`
- مرتبط: [PRD A-4, A-8, A-9](../prd/PRD.md#7-assumptions)، [spec 03](../specs/03-resource-state-machine.md)، [database](../database/database.md)

## زمینه

منابع با هم فرق دارند: CPU، RAM و دیسک تقسیم‌پذیرند؛ IPها و GPUها گسسته‌اند؛ overcommit و محدودیت‌های فروش برای هر محصول متفاوت است. محصولات و منابع جدید مرتب اضافه می‌شوند.

## تصمیم

- یک **resource class** (VCPU، RAM_MB، DISK_GB، IPV4، GPU) یک واحد پایه و یک scope (`ZONE`، `REGION`، `HOST`) دارد که در پیکربندی تعریف می‌شود.
- یک **pool** واحدی از ظرفیت در یک منطقه است، و به‌طور اختیاری در یک zone یا روی یک host.
- یک ردیف **inventory** برای هر (pool، class) مقادیر `total`، `reserved`، `allocation_ratio`، `min_unit`، `step_size`، `max_unit` و شمارنده‌های `held` و `used` را نگه می‌دارد.
- ظرفیت قابل فروش = `floor((total − reserved) × allocation_ratio)`.
- منابع گسسته شمرده می‌شوند، نه شناسایی: سرویس تصمیم می‌گیرد که یک IP در دسترس است، IPAM تصمیم می‌گیرد کدام IP.
- classهای تقسیم‌پذیر در سطح zone با حاشیهٔ `reserved` به میزان 10%؛ IPها در سطح منطقه؛ GPUها در سطح host.

## گزینه‌های بررسی‌شده

| سطح | گزینه | نتیجه |
| --- | --- | --- |
| 1 | ستون‌های ثابت مانند `cpu_free` و `ram_free` روی جدول محصول | رد شد. هر منبع جدید یک migration است؛ overcommit و واحدهای گسسته در آن جا نمی‌شوند. |
| 2 | class عمومی + شمارنده‌های inventory با واحدهای پایهٔ صحیح | **انتخاب شد.** همان مدلی که OpenStack Placement در مقیاس بزرگ استفاده کرده است. |
| 3 | inventory به‌ازای هر واحد با هویت، traits، affinity و NUMA | رد شد. میلیون‌ها ردیف برای حدود 100k VM و یک scheduler که خارج از محدوده است. |

## پیامدها

- یک resource class جدید پیکربندی است؛ یک pool یا محصول جدید داده است.
- واحدهای پایهٔ صحیح (millicore، MB) خطاهای گردکردن را حذف می‌کنند.
- با 3 منطقه، هر کدام 2 zone، و حدود 30 host دارای GPU، جدول inventory زیر 100 ردیف می‌ماند.
- fragmentation دیده نمی‌شود؛ حاشیهٔ رزروشده و release پس از provisioning ناموفق آن را کاهش می‌دهند.

## زمان بازنگری

همان شرطِ [ADR-0001](0001-system-boundary.md): خطاهای provisioning ناشی از fragmentation بیش از حدود 1%.
