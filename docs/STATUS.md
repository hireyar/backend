# وضعیت جاری

آخرین به‌روزرسانی: 2026-09-24. مرحله ۱: بک‌اند حساب‌های سازمانی و API پنل HR.
هدف پایان: تمام APIهای این مرحله، جداسازی سازمان، نشست امن، migration، مستندات و تست ریسک‌های اصلی؛ صحت اجرای container و SMTP محلی هنوز نیازمند محیط قابل اجرا است.

Implemented یعنی کد موجود است؛ Verified یعنی بررسی مربوط واقعاً اجرا شده است. تست API با ایمیل جایگزین، صحت ارسال واقعی SMTP را ثابت نمی‌کند.

| قابلیت | وضعیت | مسیر و شواهد |
|---|---|---|
| Solution .NET 10 و مرز پروژه‌ها | Verified | `src/` و `HireYar.slnx`؛ restore/build موفق |
| ثبت‌نام اتمیک، ایمیل یکتا و تأیید یک‌بارمصرف | Verified | `Infrastructure/Services/Auth/Accounts.cs` زیر src؛ تست transaction، duplicate و رقابت ثبت‌نام |
| ورود، lockout و مجوز جاری | Verified | `Api/Security/CurrentAccountValidation.cs` و Accounts؛ نقش جاری، کاربر غیرفعال و JWT نامعتبر/منقضی بررسی شد |
| refresh rotation/replay/هم‌زمانی | Verified | Accounts؛ فقط یک جانشین و ابطال خانواده در replay روی SQL واقعی |
| logout/all، reset/change، پروفایل | Verified | `Api/Controllers/AuthController.cs`؛ ابطال refreshها و overposting بررسی شد |
| سازمان و دعوت/اعضا | Verified | `Application/Services/Organizations/` و `Infrastructure/Services/Members/`؛ نقش، لغو، انقضا، resend و پذیرش هم‌زمان |
| جداسازی سازمان و Dashboard | Verified | `Application/Services/Dashboard/`؛ دسترسی بین دو سازمان و FK مرکب روی SQL واقعی |
| موقعیت، قالب، سؤال و RowVersion | Verified | `Application/Services/JobPositions/` و `Application/Services/InterviewTemplates/`؛ ترتیب، حفظ Id، افزودن/حذف و 409 |
| cookie/CSRF/CORS و startup امن | Verified | `Api/Program.cs`, `Api/Security/`؛ Secure در production، CSRF ناقص، origin غیرمجاز، نبود کلید |
| OpenAPI و UI توسعه | Implemented | OpenAPI JSON در integration بررسی شد؛ UI مرورگر دستی بررسی نشده |
| SMTP فارسی و مدیریت شکست | Implemented | `Infrastructure/Services/Email/SmtpEmailSender.cs`؛ شکست/بازیابی با test double بررسی شد؛ Mailpit واقعی اجرا نشده |
| Dockerfile و compose توسعه | Implemented | فایل‌های ریشه؛ Docker Linux engine روی میزبان در اجرا نبود؛ build/up انجام نشده |
| مستندات و نمونه HTTP | Implemented | README، AGENTS، docs و `src/HireYar.Api/HireYar.Api.http` |

مسیرهای کوتاه در جدول نسبت به `src/HireYar.*` هستند؛ برای مثال `Infrastructure/Auth` یعنی `src/HireYar.Infrastructure/Auth`.

## migration و بررسی‌ها

- migration موجود: `src/HireYar.Infrastructure/Persistence/Migrations/20260924065711_InitialCreate.cs` و snapshot متناظر.
- migration در دیتابیس‌های تست تصادفی SQL Server 2019 محلی اعمال شد؛ هیچ دیتابیس موجود/production تغییر نکرد.
- `dotnet restore`: موفق؛ وابستگی‌های Microsoft مربوط به EF/ASP.NET همگی 10.0.12، SwaggerUI برابر 10.0.1.
- `dotnet build --no-restore`: موفق، صفر warning و error.
- `HIREYAR_TEST_SQL` برای localhost تنظیم و `dotnet test --logger 'trx;LogFileName=integration.trx' --results-directory TestResults` اجرا شد: **۲۴ موفق، صفر شکست، صفر Skip**.
- بررسی نهایی پس از مرتب‌سازی فایل‌ها: `dotnet build HireYar.slnx -c Release --no-restore` موفق با صفر warning/error؛ `dotnet test HireYar.slnx -c Release --no-build --logger 'trx;LogFileName=integration-release.trx' --results-directory TestResults` با ۲۴ موفق، صفر شکست و صفر Skip.
- گزارش‌های محلی: `TestResults/integration.trx` و `TestResults/integration-release.trx` (خارج Git).
- `dotnet ef migrations script --idempotent ...`: موفق؛ `artifacts/migrations.sql` برای بازبینی تولید شد (خارج Git).
- `dotnet ef migrations has-pending-model-changes ...`: بدون اختلاف مدل و migration.
- اجرای نخست خطای validation record داشت؛ انتقال attributeها به پارامتر constructor مشکل را رفع کرد و تست‌ها دوباره موفق شدند.

## محدودیت‌ها و بررسی‌های باقی‌مانده

Docker build/compose و ارسال واقعی SMTP به Mailpit اجرا نشده‌اند چون Docker Linux engine در دسترس نبود. ایمیل واقعی به هیچ کاربری ارسال نشد. TLS/reverse proxy، کلیدگذاری و deployment واقعی خارج دامنه این مرحله هستند. تست‌ها رفتار HTTP را با TestServer می‌سنجند؛ مرورگر Angular و دامنه‌های واقعی هنوز آزموده نشده‌اند. هیچ نقص شناخته‌شدهٔ شکست‌خورده در مجموعه تست جاری باقی نمانده؛ این ادعا معادل ممیزی کامل امنیت یا آماده‌بودن production نیست.

برنچ فعال `dev`؛ `production` و `main` روی commit اولیه باقی‌اند. تغییرات پیاده‌سازی commit/push نشده‌اند.

 
بررسی ساختار compose: `docker compose --env-file .env.example -f compose.dev.yaml config --quiet` موفق؛ این فقط اعتبارسنجی پیکربندی است و به معنی اجرای سرویس‌ها نیست.
`2026-09-24`: خطای startup محلی ناشی از نبود SigningKey بود؛ کلید تصادفی در User Secrets ذخیره و اتصال توسعهٔ محلی تنظیم شد. اجرای Development و GET /health با HTTP 200 بررسی شد؛ دیتابیس توسعه یا migration در این بررسی تغییر نکرد.
`2026-09-24`، رفع 401 روی Swagger: middleware مربوط به UI در Development قبل از fallback authorization قرار گرفت. تست redirect /swagger، HTML، CSS، OpenAPI و محافظت API پاس شد؛ تست محدودیت Production نیز پاس شد (۲ تست منتخب Release؛ TestResults/swagger-fix.trx). فرایند در حال اجرای قبلی باید برای دریافت اصلاح rebuild/restart شود.


## آخرین بررسی پس از مرتب‌سازی پوشه‌ها

طبق انتخاب کاربر فقط فایل‌بندی تغییر کرد: Controllers، Services، DTOs و Interfaces جدا شدند؛ مسیر Service → IApplicationDbContext حفظ شد. `dotnet build HireYar.slnx -c Release --no-restore`: موفق با صفر warning/error.

`dotnet test HireYar.slnx -c Release --no-build --logger 'trx;LogFileName=folder-structure.trx' --results-directory TestResults`: ۲۳ موفق، ۱ شکست، صفر Skip. این نتیجه جایگزین نتیجه ۲۴/۲۴ قبلی برای checkout فعلی است.

شکست `Production_sets_secure_cookies_and_hides_openapi` به شرط `//if (app.Environment.IsDevelopment())` در Program.cs مربوط است که در commit پایه 1b86ec4 وجود داشت. OpenAPI در Production پاسخ 200 می‌دهد. Program.cs در این بازآرایی تغییر نکرد؛ رفتار موجود کاربر حفظ شد. ادعای قبلی «هیچ تست شکست‌خورده‌ای باقی نمانده» دیگر برای checkout فعلی معتبر نیست؛ HY-010 ثبت شد.
