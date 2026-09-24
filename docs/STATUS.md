# وضعیت جاری

آخرین به‌روزرسانی: 2026-09-24. مرحله ۱، بک‌اند سازمان و پنل HR. پایگاه داده جاری **PostgreSQL 18** است؛ تصمیم SQL Server با درخواست صریح کاربر جایگزین شد و داده قبلی نیاز به انتقال ندارد. تنظیمات محلی JWT و اتصال دیتابیس اکنون در `src/HireYar.Api/appsettings.Development.local.json` قرار می‌گیرند؛ فایل در Git نادیده گرفته می‌شود و متغیرهای محیطی اولویت بالاتر دارند.

## وضعیت قابلیت‌ها

| قابلیت | وضعیت | شواهد |
|---|---|---|
| ساختار، Controller، Services/DTOs/Interfaces | Verified | build Release با صفر warning/error |
| ثبت‌نام اتمیک، ایمیل یکتا، تأیید، ورود و lockout | Verified | تست روی PostgreSQL واقعی 18.3 |
| refresh rotation/replay و هم‌زمانی، logout/all | Verified | تک‌جانشینی و ابطال خانواده نشست روی PostgreSQL |
| reset/change password، پروفایل | Verified | تست موجود روی PostgreSQL موفق |
| سازمان، نقش، دعوت، انقضا/لغو/resend و فعال‌سازی | Verified | تست نقش جاری و پذیرش هم‌زمان موفق |
| tenant isolation، FK مرکب و Dashboard | Verified | رد دسترسی و رابطه بین دو سازمان روی PostgreSQL |
| موقعیت و قالب و سؤال | Verified | ایجاد، ویرایش، ترتیب، حفظ Id و تعارض نسخه موفق |
| migration PostgreSQL | Verified | InitialPostgres روی DB خالی اعمال شد؛ script idempotent دو بار اجرا شد |
| cookie، CSRF و CORS | Verified | تست‌ها موفق؛ تست ترکیبی production در بخش OpenAPI شکست می‌خورد |
| OpenAPI در Production | InProgress | نقص قبلی HY-010: شرط Development در Program.cs کامنت شده؛ مستندات عمومی پاسخ 200 می‌دهند |
| Docker و SMTP واقعی | Implemented | compose معتبر؛ Docker daemon در دسترس نیست و build/up/SMTP واقعی اجرا نشده |
| اتصال توسعه و مستندات | Implemented | .env و appsettings.Development.local.json خارج Git برای localhost:5433؛ راهنما و scripts/Update-DevelopmentDatabase.ps1 |

Verified به معنی اجرای بررسی مربوط است؛ Implemented تنها وجود کد را نشان می‌دهد. ایمیل تست جایگزین حافظه‌ای است؛ ارسال واقعی SMTP تأیید نشده است.

## نتیجه واقعی آخرین بررسی‌ها

- `dotnet restore HireYar.slnx`: موفق. provider برابر Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3؛ EF/ASP.NET نسخه 10.0.12 و SDK 10.0.400.
- `dotnet build HireYar.slnx -c Release --no-restore`: موفق، صفر warning/error.
- `dotnet test HireYar.slnx -c Release --logger 'trx;LogFileName=postgres.trx' --results-directory TestResults` با HIREYAR_TEST_POSTGRES: **۲۳ موفق، ۱ شکست، صفر Skip**.
- شکست همان `Production_sets_secure_cookies_and_hides_openapi` است که پیش از مهاجرت هم وجود داشت؛ Program.cs در این تغییر دست‌نخورده است. نتیجه قدیمی ۲۴/۲۴ مرجع checkout فعلی نیست.
- تست‌ها با باینری PostgreSQL 18.3 نصب‌شده و یک cluster جدا در artifacts/pg-validation روی loopback:55432 اجرا شدند؛ سرویس PostgreSQL موجود و SQL Server قبلی تغییر نکردند. instance آزمایشی پس از کار متوقف شد؛ دیتابیس‌های تصادفی تست حذف شدند.
- migration جاری: `src/HireYar.Infrastructure/Persistence/Migrations/20260924081519_InitialPostgres.cs` و snapshot متناظر. InitialCreate مخصوص SQL Server جایگزین شده است؛ migration داده بین دو provider انجام نمی‌شود.
- `dotnet ef migrations has-pending-model-changes ... --configuration Release`: بدون اختلاف.
- `dotnet ef migrations script --idempotent ... --output artifacts/postgres-migrations.sql`: موفق؛ اسکریپت دو بار روی DB تصادفی مستقل با psql و ON_ERROR_STOP اجرا شد؛ هر دو موفق. DB بررسی حذف شد.
- `docker compose --env-file .env.example -f compose.dev.yaml config --quiet`: موفق؛ فقط اعتبارسنجی ساختار است.

## اجرا و محدودیت‌ها

انتخاب کاربر برای استفاده روزمره PostgreSQL داخل Docker در localhost:5433 است. .env و appsettings محلی تنظیم شده‌اند؛ هیچ secret در Git یا گزارش ثبت نشده است. Docker Desktop باید اجرا شود و سپس postgres/mailpit بالا بیایند و migration توسعه اجرا شود. این کانتینرها و دیتابیس توسعه هنوز از این مسیر اجرا/ایجاد نشده‌اند. در بررسی جاری Docker daemon با خطای Access denied قابل اتصال نبود. Build پس از تغییر تنظیمات موفق شد؛ اجرای ۲۴ تست بدون HIREYAR_TEST_POSTGRES همگی Skip شدند. `dotnet tool restore` به دلیل Access denied به NuGet.Config اجرا نشد؛ اعتبارسنجی EF این نوبت انجام نشد.

rowVersion اکنون Base64 از ۱۶ بایت است؛ توکن قبلی SQL Server قابل استفاده نیست. جزئیات در ARCHITECTURE و README است. setup production/TLS و frontend همچنان خارج دامنه‌اند.

برنچ فعال dev و با origin/dev همگام است؛ HEAD برابر e33528f و مهاجرت PostgreSQL در آن commit شده است. تغییرات تنظیمات این نوبت commit/push نشده‌اند؛ دو تغییر محلی پیشین در MembersController و ApiFixture حفظ شده‌اند. production تغییر نکرد.
