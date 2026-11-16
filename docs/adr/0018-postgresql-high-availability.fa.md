# ADR-0018: CloudNativePG با synchronous replication (فقط در سطح طراحی)

- وضعیت: Accepted
- تاریخ: `2026-11-16`
- مرتبط: [ADR-0008](0008-failure-mode-and-regional-cells.md)، [ADR-0012](0012-database-postgresql.md)، [spec 12 §5](../specs/12-configuration-and-deployment.md#5-production-topology-design-only)

## زمینه

برای رزروهای تأییدشده RPO برابر 0 و RTO کمتر از 60 ثانیه لازم است. پلتفرم Kubernetes است. پیاده‌سازی تحویلی لازم نیست این را بازتولید کند.

## تصمیم

برای هر regional cell، یک cluster از CloudNativePG با یک primary و دو standby، با quorum synchronous replication که تأیید یک standby را الزامی می‌کند، failover خودکار، و PgBouncer در transaction mode در جلوی آن. کلاینت‌ها فقط از طریق سرویس read-write که operator نگه می‌دارد می‌نویسند. استک Docker Compose تحویلی فقط یک PostgreSQL اجرا می‌کند.

## گزینه‌های بررسی‌شده

| گزینه | نتیجه |
| --- | --- |
| CloudNativePG، sync quorum یک از دو | **انتخاب‌شده.** operator بومی Kubernetes و پروژهٔ CNCF است و failover، fencing primary قبلی و backupها را مدیریت می‌کند. |
| Patroni روی VMها یا StatefulSetها | برای Kubernetes رد شد؛ اگر PostgreSQL بیرون از Kubernetes اجرا شود انتخاب درست همین است. |
| Asynchronous replication | رد شد. آخرین holdها هنگام failover از دست می‌روند؛ کلاینتی که 201 گرفته رزروی ندارد. |
| سرویس پایگاه دادهٔ managed | در محیط هدف در دسترس نیست. |

## پیامدها

- از دست رفتن هر دو standby باعث توقف نوشتن می‌شود، نه ادامه بدون replication: این عمدی است و با رویکرد fail-closed سازگار است.
- operator روی primary قبلی fencing اعمال می‌کند و از split brain جلوگیری می‌کند ([F-5](../edge-cases/edge-cases.md#5-failure-and-recovery)).

## چه زمانی بازنگری شود

وقتی در محیط هدف PostgreSQL بیرون از Kubernetes اجرا شود.
