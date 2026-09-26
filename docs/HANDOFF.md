# ادامه کار

بررسی 2026-09-26: stack trace سرور `DefaultAntiforgery.CheckSSLConfig` را با `IsHttps=False` و وجود هدر forwarded تأیید کرد. `docker-compose.yml` مقدار `ReverseProxy__KnownProxies__0` نداشت و middleware بدون آن فعال نمی‌شد. Compose اکنون `HIREYAR_TRUSTED_PROXY_IP` را الزام می‌کند و به تنظیم API می‌دهد؛ IP را از مبدأ واقعی اتصال proxy به کانتینر تعیین کنید، نه دامنه یا IP کاربر. مقدار هدر نیز باید `https` باشد. Compose با IP آزمایشی معتبر و بدون آن نامعتبر بود؛ build صفر هشدار/خطا و تست ۲ موفق، ۲۴ Skip بدون PostgreSQL. `ApiExceptionHandler` stack trace را بدون متن exception یا داده درخواست ثبت می‌کند. push/deploy انجام نشده؛ پس از تنظیم سرور و انتشار، `/api/auth/csrf` و کوکی Secure را بررسی کنید.

آخرین تغییر (2026-09-25): راهنمای کوتاه فارسی شروع در `OpenApiText` افزوده شد و عنوان همهٔ عملیات‌های Controller در OpenAPI فارسی شد. ترتیب پیشنهادی حساب جدید: CSRF، ثبت‌نام، تأیید ایمیل، ورود؛ حساب موجود: CSRF، ورود، Bearer و مشاهده حساب. بعد از ورود CSRF جدید با Bearer لازم است؛ refresh با کوکی و بدون Bearer انجام می‌شود. Build موفق و تست مستقل Production Swagger/OpenAPI شامل وجود راهنما و عنوان همهٔ عملیات‌ها موفق شد. تست PostgreSQL در این نوبت اجرا نشد. تغییرات این نوبت commit/push/deploy نشده‌اند.

رفع جدید: کاربر در Development خطای `Failed to load configuration ... UserSecrets/.../secrets.json` گزارش کرد. `UserSecretsId` از `src/HireYar.Api/HireYar.Api.csproj` حذف شد؛ تنظیمات از appsettings.Development.local.json خوانده می‌شوند. Build موفق، اجرای واقعی API موفق، `/health` و `/swagger/index.html` هر دو 200؛ تست جاری ۱ موفق و ۲۴ Skip بدون PostgreSQL بود. فایل User Secrets کاربر تغییر داده نشد.

آخرین بررسی: کاربر `.env` را خالی کرد؛ Compose به متغیرهای اجباری وابسته بود. اسکریپت `scripts/Start-DevelopmentContainers.ps1` اکنون JWT key و رمز اتصال را از appsettings.Development.local.json می‌خواند؛ برای postgres/mailpit اجرا می‌شود و با `-IncludeApi` API را هم می‌سازد. `.env` از Git untrack شد اما در تاریخچهٔ قبلی مقادیر وجود داشت؛ پس چرخش secretها در محیط‌های مربوط لازم است. appsettings محلی از Docker build context هم کنار گذاشته شد. Docker daemon در محیط فعلی Access denied داد و اجرای کانتینرها تأیید نشد. درخواست 401 کاربر از `/swagger` روی سرور بود؛ Swagger middleware قبل از fallback authorization قرار گرفت و تست Production نشان داد `/swagger/index.html` و `/openapi/v1.json` پاسخ 200 و `/api/auth/me` بی‌توکن پاسخ 401 می‌دهد. Build موفق؛ تغییرات commit/push/deploy نشده‌اند. برای سرور باید تنظیمات Production جداگانه فراهم و نسخهٔ جدید منتشر شود.

تنظیمات محلی `src/HireYar.Api/appsettings.Development.local.json` در Git نادیده گرفته می‌شود. Program و EF design-time آن را می‌خوانند و متغیر محیطی اولویت بالاتر دارد. اسکریپت migration همین فایل را می‌خواند.

ابتدا AGENTS، PROJECT، ARCHITECTURE (ADR-012)، STATUS و BACKLOG را بخوان. provider اکنون Npgsql EF Core 10.0.3 است. migration InitialPostgres جای InitialCreate را گرفته؛ از script قبلی artifacts/migrations.sql استفاده نکن، script جدید artifacts/postgres-migrations.sql است.

نقاط حساس: Persistence/Configurations و AppDbContext (RowVersion تصادفی ۱۶بایتی)، Services/Auth/Accounts و Services/Members/TeamMembers (FOR UPDATE)، Api/Errors/ApiExceptionHandler (SQLSTATE)، tests/ApiFixture (PostgreSQL واقعی و HIREYAR_TEST_POSTGRES).

پیش از اصلاح اخیر، تست‌های PostgreSQL نتیجهٔ ۲۳ موفق و ۱ شکست OpenAPI در Production داشتند. برای اصلاح اخیر build بدون هشدار و تست مستقل Production Swagger موفق شد؛ مجموعهٔ تست‌های PostgreSQL هنوز دوباره اجرا نشده است. migration و script idempotent قبلاً روی PostgreSQL 18.3 مستقل تأیید شدند.

کاربر برای توسعه روزمره Docker PostgreSQL روی localhost:5433 را انتخاب کرد. قدم بعد HY-006: پس از دسترسی Docker daemon، `pwsh -File scripts/Start-DevelopmentContainers.ps1` و سپس `pwsh -File scripts/Update-DevelopmentDatabase.ps1` را اجرا کن. اسکریپت‌ها secret چاپ نمی‌کنند. اجرای واقعی compose/SMTP هنوز تأیید نشده است.

برنچ dev است؛ تغییرات اخیر commit/push/deploy نشده‌اند. `dotnet tool restore` در این محیط به NuGet.Config دسترسی نداشت؛ بررسی EF اجرا نشد. Docker daemon هم Access denied داد. HY-006 و HY-007 باز هستند؛ HY-010 با تصمیم دسترسی عمومی مستندات و تست مستقل بسته شد.
