# ADR-0019: OpenTelemetry و Prometheus

- وضعیت: Accepted
- تاریخ: `2026-11-16`
- مرتبط: [spec 10](../specs/10-observability.md)

## زمینه

هر شرط «چه زمانی بازنگری شود» در این ADRها یک عدد است. بدون metric متناظر، رفتن به سطح بعدی یک حدس است.

## تصمیم

OpenTelemetry SDK برای traceها و metricها با instrumentation برای ASP.NET Core، Npgsql و RabbitMQ؛ export با OTLP؛ ارائهٔ Prometheus در `/metrics`؛ لاگ‌های ساخت‌یافتهٔ JSON که `trace_id`، `reservation_id` و `customer_id` را حمل می‌کنند. هر آستانهٔ ADR یک metric نام‌دار و یک alert دارد.

## گزینه‌های بررسی‌شده

| گزینه | نتیجه |
| --- | --- |
| OpenTelemetry + Prometheus | **انتخاب‌شده.** استاندارد باز، مستقل از vendor و در .NET درجه‌یک. |
| فقط لاگ‌ها | رد شد. آستانه‌ها را نمی‌شود از روی لاگ‌ها تشخیص داد. |
| agent یک APM تجاری | رد شد. lock-in و مسائل لایسنس؛ OTLP می‌تواند بعداً داده را به یکی از آن‌ها بدهد. |

## پیامدها

- یک profile اختیاری در Compose یک collector، Prometheus و Grafana را با یک dashboard آماده اجرا می‌کند.

## چه زمانی بازنگری شود

انتظار تغییر نمی‌رود.
