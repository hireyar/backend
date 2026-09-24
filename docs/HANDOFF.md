# ادامه کار

آخرین کار: تنظیم JWT signing key و اتصال PostgreSQL در `src/HireYar.Api/appsettings.Development.local.json` طبق درخواست کاربر؛ فایل محلی در Git نادیده گرفته شده و مقادیر آن از `.env` توسعه گرفته شده‌اند. Program و EF design-time این فایل را می‌خوانند؛ متغیر محیطی بر آن مقدم است. اسکریپت migration هم همین appsettings محلی را می‌خواند. تغییرات پیشین کاربر در MembersController و ApiFixture حفظ شده‌اند.

ابتدا AGENTS، PROJECT، ARCHITECTURE (ADR-012)، STATUS و BACKLOG را بخوان. provider اکنون Npgsql EF Core 10.0.3 است. migration InitialPostgres جای InitialCreate را گرفته؛ از script قبلی artifacts/migrations.sql استفاده نکن، script جدید artifacts/postgres-migrations.sql است.

نقاط حساس: Persistence/Configurations و AppDbContext (RowVersion تصادفی ۱۶بایتی)، Services/Auth/Accounts و Services/Members/TeamMembers (FOR UPDATE)، Api/Errors/ApiExceptionHandler (SQLSTATE)، tests/ApiFixture (PostgreSQL واقعی و HIREYAR_TEST_POSTGRES).

build Release موفق؛ تست‌ها ۲۳ موفق و ۱ شکست قبلی OpenAPI در Production هستند. Program.cs تغییر نکرده؛ HY-010 همچنان باز است. migration و script idempotent دو بار روی PostgreSQL 18.3 مستقل تأیید شدند و مدل اختلافی ندارد. cluster آزمایشی artifacts/pg-validation روی 55432 متوقف شده؛ دیتابیس‌های تصادفی تست حذف شدند.

کاربر برای توسعه روزمره Docker PostgreSQL روی localhost:5433 را انتخاب کرد. .env خارج Git و User Secrets به این اتصال تنظیم شده‌اند. Docker daemon در دسترس نبود؛ قدم بعد HY-006: Docker Desktop را روشن کن، `docker compose -f compose.dev.yaml up -d postgres mailpit` سپس `pwsh -File scripts/Update-DevelopmentDatabase.ps1`. اسکریپت فقط اتصال همین دیتابیس توسعه را می‌پذیرد و secret چاپ نمی‌کند. اجرای واقعی compose/SMTP و خود helper هنوز تأیید اجرایی نشده‌اند.

برنچ dev اکنون بر پایه e33528f و با origin/dev همگام است؛ مهاجرت PostgreSQL commit شده، تغییرات تنظیمات این نوبت commit نشده‌اند. هیچ push/deploy انجام نشده است. build موفق؛ تست جاری بدون HIREYAR_TEST_POSTGRES همگی Skip شدند. `dotnet tool restore` در این محیط به NuGet.Config دسترسی نداشت؛ بررسی EF اجرا نشد. Docker daemon هم Access denied داد. HY-006، HY-007 و HY-010 هنوز باز هستند.
