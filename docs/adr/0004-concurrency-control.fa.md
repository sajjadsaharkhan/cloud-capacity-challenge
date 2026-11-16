# ADR-0004: کنترل هم‌زمانی با به‌روزرسانی‌های اتمیک شرطی در یک lock order ثابت

- وضعیت: Accepted
- تاریخ: `2026-11-16`
- مرتبط: [spec 04 §4](../specs/04-capacity-allocation.md#4-hold-transaction)، [load model](../architecture/load-model.md)، حالت‌های لبه [C-1 … C-11](../edge-cases/edge-cases.md#1-concurrency-on-capacity)

## زمینه

invariant اصلی `held + used ≤ sellable` برای هر ردیف inventory است، زیر صدها درخواست هم‌زمان روی همان ردیف. یک درخواست چند ردیف را لمس می‌کند (CPU، RAM، دیسک، IP و ردیف‌های سهمیهٔ مشتری) و باید یا همه را بگیرد یا هیچ‌کدام را.

## تصمیم

- هر خط یک `UPDATE … SET held = held + q WHERE … AND held + used + q <= sellable` است. صفر ردیف تأثیرپذیرفته یعنی کمبود.
- همهٔ خط‌ها در یک تراکنش اجرا می‌شوند و به‌صورت یک batch ارسال می‌شوند (یک round trip)، با READ COMMITTED.
- lock order سراسری: ردیف‌های رزرو ← ردیف‌های سهمیه ← ردیف‌های inventory، هر کدام مرتب‌شده بر اساس کلید. همهٔ نویسنده‌ها از آن پیروی می‌کنند، پس چرخهٔ deadlock نمی‌تواند شکل بگیرد.
- یک `CHECK` constraint روی جدول همان سقف را به‌عنوان آخرین خط دفاعی اعمال می‌کند.

## گزینه‌های بررسی‌شده

| سطح | گزینه | نتیجه |
| --- | --- | --- |
| 1 | خواندن ظرفیت آزاد و سپس نوشتن آن در کد اپلیکیشن؛ یا یک شمارندهٔ Redis بدون durability | رد شد. race میان دو درخواست؛ نوشتن‌های تأییدشده در failover از دست می‌روند. |
| 2 | به‌روزرسانی اتمیک شرطی در یک تراکنش ACID محلی با lock order ثابت | **انتخاب شد** |
| 3 | escrow (تقسیم ظرفیت یک pool به برش‌های مستقل)، single-writer batching، یا distributed SQL | فعلاً رد شد. rebalance کردن برش‌ها و ظرفیتی که در آن‌ها گیر می‌افتد؛ با بار تخمینی لازم نیست. |
| — | optimistic locking با یک ستون version | رد شد. روی یک hot row بیشتر تلاش‌ها شکست می‌خورند و retry storm به دنبال دارد. |
| — | `SELECT … FOR UPDATE` و سپس `UPDATE` | رد شد. round trip اضافه زمان نگه‌داشتن lock را دو برابر می‌کند. |
| — | ایزولاسیون SERIALIZABLE | رد شد. serialization failure و retry روی hot row. |
| — | distributed lock در Redis | رد شد. fencing ندارد؛ درستی به ساعت‌ها وابسته است. |

## پیامدها

- سقف هر hot row حدود 200 تا 300 hold در ثانیه است (lock حدود 3 تا 5 ms نگه داشته می‌شود)؛ حد امن حدود 150 در ثانیه. اوج تخمینی 6 در ثانیه است.
- deadlock ساختاری وجود ندارد.
- تقسیم poolها بر اساس zone یا cluster سقف را فقط با تغییر داده بالا می‌برد.

## زمان بازنگری

- بیش از 150 hold در ثانیه به‌طور پایدار روی یک ردیف، یا p99 انتظار برای lock بیش از 20 ms: poolها بر اساس cluster تقسیم شوند.
- بیش از 300 hold در ثانیه روی poolی که قابل تقسیم نیست: escrow.
