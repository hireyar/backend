# وضعیت جاری

تغییر موقت 2026-09-26: برای نبود SMTP، ثبت‌نام Owner با `Security:AutoConfirmEmailOnRegistration=true` ایمیل را در DB تأییدشده ثبت می‌کند و ارسال ایمیل تأیید را رد می‌کند؛ ورود با همان حساب ممکن است. build موفق با صفر warning/error؛ تست بدون اتصال PostgreSQL: ۲ موفق، ۲۵ Skip، شامل تست جدید این مسیر. اجرای واقعی تست جدید روی PostgreSQL و اعمال migration در محیط هدف هنوز تأیید نشده است. تغییر محلی کاربر در `appsettings.Development.json` حفظ شد.

بررسی ثبت‌نام Production در 2026-09-26: پاسخ 500 کاربر فقط `unexpected_error` و `traceId` دارد و لاگ Docker همان درخواست هنوز دریافت نشده؛ علت قطعی مشخص نیست. مسیر ثبت‌نام پیش از ارسال ایمیل به PostgreSQL دسترسی دارد، ولی CSRF به دیتابیس نیاز ندارد. برای تشخیص، شناسهٔ لاگ و پاسخ یکسان شد و SQLSTATE خطای PostgreSQL بدون متن حساس در لاگ ثبت می‌شود. تنظیمات اتصال و کلید امضا از `appsettings.json` تحت Git حذف و در Compose به متغیرهای اجباری خارج Git منتقل شدند. build بدون هشدار/خطا، تست ۲ موفق و ۲۴ Skip به علت نبود PostgreSQL تست، و اعتبارسنجی Compose موفق شد. بررسی واقعی سرور، اتصال دیتابیس و migration پس از انتشار لازم است؛ push/deploy انجام نشده است.

بررسی 2026-09-26: stack trace سرور خطای `DefaultAntiforgery.CheckSSLConfig` را تأیید کرد؛ `IsHttps=False` با وجود هدر forwarded. طبق درخواست صریح کاربر، `KnownProxies` و متغیر IP از کد و Compose حذف شدند؛ API در Production فقط مقدار دقیق و یکتای `X-Forwarded-Proto: https` را بدون بررسی مبدأ برای scheme اعمال می‌کند. الزام کوکی Secure حفظ شد. دسترسی مستقیم به پورت HTTP داخلی باید در شبکه سرور محدود باشد. build بدون هشدار/خطا و تست: ۲ موفق، ۲۴ Skip به علت نبود PostgreSQL تست. رفتار سرور پس از انتشار هنوز تأیید نشده؛ push/deploy انجام نشده است.

آخرین به‌روزرسانی: 2026-09-25. مرحله ۱، بک‌اند سازمان و پنل HR. پایگاه داده جاری **PostgreSQL 18** است؛ تصمیم SQL Server با درخواست صریح کاربر جایگزین شد و داده قبلی نیاز به انتقال ندارد. تنظیمات محلی JWT و اتصال دیتابیس اکنون در `src/HireYar.Api/appsettings.Development.local.json` قرار می‌گیرند؛ فایل در Git نادیده گرفته می‌شود و متغیرهای محیطی اولویت بالاتر دارند.

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
| cookie، CSRF و CORS | Verified | تست‌های قبلی PostgreSQL موفق؛ تست ترکیبی Production با رفتار جدید OpenAPI هنوز دوباره اجرا نشده |
| OpenAPI در Production | Verified | Swagger و JSON هر دو پاسخ 200؛ راهنمای شروع و عنوان همهٔ عملیات‌ها به فارسی در JSON بررسی شد |
| Docker و SMTP واقعی | Implemented | compose معتبر؛ Docker daemon در دسترس نیست و build/up/SMTP واقعی اجرا نشده |
| اتصال توسعه و مستندات | Implemented | appsettings.Development.local.json خارج Git برای localhost:5433؛ اسکریپت‌های راه‌اندازی و migration |

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

Swagger اکنون راهنمای فارسی کوتاه برای مسیر `csrf → register → confirm-email → login` و مسیر ورود حساب موجود دارد. عنوان همهٔ عملیات‌های API فارسی است. Build بدون warning/error و تست مستقل Production OpenAPI موفق شد؛ تست‌های وابسته به PostgreSQL در این نوبت اجرا نشدند.

رفع خطای startup: `UserSecretsId` قدیمی از پروژه API حذف شد. ASP.NET Core دیگر در Development فایل User Secrets خراب/غیرقابل‌دسترس را پیش از بارگذاری appsettings محلی باز نمی‌کند. Build موفق؛ اجرای واقعی `dotnet run --launch-profile http` با `/health` و `/swagger/index.html` هر دو 200 بود. تست جاری ۱ موفق و ۲۴ Skip به علت نبود HIREYAR_TEST_POSTGRES است.

انتخاب کاربر برای استفاده روزمره PostgreSQL داخل Docker در localhost:5433 است. appsettings محلی تنظیم شده، اما Docker daemon در این محیط Access denied می‌دهد و کانتینرها و SMTP واقعی تأیید نشده‌اند. `dotnet tool restore` به دلیل Access denied به NuGet.Config اجرا نشد؛ اعتبارسنجی EF این نوبت انجام نشد.

بررسی تازه: کاربر `.env` را خالی کرد و Compose به‌درستی متغیرهای اجباری را نیافت. `scripts/Start-DevelopmentContainers.ps1` اکنون رمز دیتابیس و signing key را از appsettings محلی می‌خواند و فقط برای اجرای Compose در محیط فرایند قرار می‌دهد. `.env` از فهرست فایل‌های تحت پیگیری Git خارج شد، ولی محتوای نسخه‌های قبلی هنوز در تاریخچه است و مقادیر قبلی باید در محیط‌های مربوط چرخانده شوند. appsettings محلی از Docker context و خروجی publish کنار گذاشته شد؛ publish Release موفق و نبود فایل محلی در خروجی بررسی شد. اجرای اسکریپت تا فراخوانی Docker تأیید شد؛ daemon با Access denied مانع up واقعی شد. Build موفق و نتیجهٔ تست جاری ۱ موفق، ۲۴ Skip به دلیل نبود HIREYAR_TEST_POSTGRES بود. تست مستقل Production Swagger موفق شد. اجرای API روی میزبان و `/health` پاسخ 200 داشت؛ `/api/auth/me` بدون توکن پاسخ 401 داد.

rowVersion اکنون Base64 از ۱۶ بایت است؛ توکن قبلی SQL Server قابل استفاده نیست. جزئیات در ARCHITECTURE و README است. setup production/TLS و frontend همچنان خارج دامنه‌اند.

برنچ فعال dev و در شروع این نوبت با origin/dev همگام بود. تغییرات راهنمای فارسی Swagger هنوز commit/push نشده‌اند؛ production تغییر نکرد.
