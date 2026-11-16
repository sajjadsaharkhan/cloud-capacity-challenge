# ADR-0020: تحویل با Docker Compose؛ آزمون‌ها روی PostgreSQL واقعی

- وضعیت: Accepted
- تاریخ: `2026-11-16`
- مرتبط: [spec 11](../specs/11-testing.md)، [spec 12](../specs/12-configuration-and-deployment.md)

## زمینه

«راه‌اندازی پروژه باید برای ارزیاب ساده و تکرارپذیر باشد.» درستی سیستم به رفتار locking پایگاه داده وابسته است.

## تصمیم

- Docker Compose با `make up`، `make test`، `make demo`، `make load`.
- آزمون‌های integration از Testcontainers با همان نسخهٔ major پایگاه دادهٔ PostgreSQL در production استفاده می‌کنند، نه mock و نه SQLite.
- k6 سقف hot row و نرخ تخلیهٔ sweeper را اندازه می‌گیرد؛ نتایج همراه با سخت‌افزار استفاده‌شده commit می‌شوند.

## گزینه‌های بررسی‌شده

| گزینه | نتیجه |
| --- | --- |
| Compose + Testcontainers + k6 | **انتخاب‌شده** |
| manifestهای Kubernetes به‌عنوان تحویلی | برای ارزیابی رد شد؛ برای بازبین سنگین است. به‌جای آن topology محیط production مستند شده است. |
| repositoryهای mock برای آزمون‌های هم‌زمانی | رد شد. فقط پایگاه دادهٔ واقعی locking واقعی را نشان می‌دهد. |
| SQLite یا یک provider درون‌حافظه‌ای برای آزمون‌ها | رد شد. معنای متفاوت locking اطمینان کاذب می‌دهد. |

## پیامدها

- برای اجرای آزمون‌های integration، Docker لازم است.
- مدل بار فرضی با اعداد اندازه‌گیری‌شده سنجیده می‌شود.

## چه زمانی بازنگری شود

انتظار تغییر نمی‌رود.
