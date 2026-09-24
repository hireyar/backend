# ادامه کار

آخرین کار: انتقال بک‌اند از SQL Server به PostgreSQL طبق درخواست کاربر؛ داده SQL Server مهم نیست و مهاجرت از دیتابیس خالی انجام می‌شود. SQL Server و PostgreSQL نصب‌شده قبلی دست‌نخورده‌اند.

ابتدا AGENTS، PROJECT، ARCHITECTURE (ADR-012)، STATUS و BACKLOG را بخوان. provider اکنون Npgsql EF Core 10.0.3 است. migration InitialPostgres جای InitialCreate را گرفته؛ از script قبلی artifacts/migrations.sql استفاده نکن، script جدید artifacts/postgres-migrations.sql است.

نقاط حساس: Persistence/Configurations و AppDbContext (RowVersion تصادفی ۱۶بایتی)، Services/Auth/Accounts و Services/Members/TeamMembers (FOR UPDATE)، Api/Errors/ApiExceptionHandler (SQLSTATE)، tests/ApiFixture (PostgreSQL واقعی و HIREYAR_TEST_POSTGRES).

build Release موفق؛ تست‌ها ۲۳ موفق و ۱ شکست قبلی OpenAPI در Production هستند. Program.cs تغییر نکرده؛ HY-010 همچنان باز است. migration و script idempotent دو بار روی PostgreSQL 18.3 مستقل تأیید شدند و مدل اختلافی ندارد. cluster آزمایشی artifacts/pg-validation روی 55432 متوقف شده؛ دیتابیس‌های تصادفی تست حذف شدند.

کاربر برای توسعه روزمره Docker PostgreSQL روی localhost:5433 را انتخاب کرد. .env خارج Git و User Secrets به این اتصال تنظیم شده‌اند. Docker daemon در دسترس نبود؛ قدم بعد HY-006: Docker Desktop را روشن کن، `docker compose -f compose.dev.yaml up -d postgres mailpit` سپس `pwsh -File scripts/Update-DevelopmentDatabase.ps1`. اسکریپت فقط اتصال همین دیتابیس توسعه را می‌پذیرد و secret چاپ نمی‌کند. اجرای واقعی compose/SMTP و خود helper هنوز تأیید اجرایی نشده‌اند.

برنچ dev بر پایه dc61cd2؛ همه تغییرات این نوبت commit نشده‌اند. هیچ push/deploy انجام نشده است. main در آغاز فعال بود و به dev برگشتیم؛ فایل‌های IDE باز دست‌نخورده باقی ماندند. namespaceها و ساختار Controllers/Services/DTOs/Interfaces حفظ شدند؛ Repository اضافه نشده است.
