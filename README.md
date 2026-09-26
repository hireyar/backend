# HireYar Backend

تا زمان فراهم‌شدن SMTP، `Security:AutoConfirmEmailOnRegistration=true` در تنظیمات مشترک فعال است: حساب‌های Owner تازه بدون ایمیل تأیید، قابل ورود هستند. این گزینه مالکیت ایمیل را بررسی نمی‌کند و پس از راه‌اندازی SMTP باید false شود. این تنظیم جای اجرای migration PostgreSQL را نمی‌گیرد.

پایه حساب‌های سازمانی و API پنل HR برای مصاحبه اولیه استخدام. تصمیم نهایی استخدام انسانی است؛ موتور مصاحبه و AI هنوز پیاده نشده‌اند.

نقطه شروع توسعه: [AGENTS](AGENTS.md). مراجع: [محصول](docs/PROJECT.md)، [معماری](docs/ARCHITECTURE.md)، [وضعیت واقعی](docs/STATUS.md)، [Backlog](docs/BACKLOG.md)، [ادامه کار](docs/HANDOFF.md).

## پیش‌نیاز و راه‌اندازی

.NET SDK 10.0.400، PostgreSQL 18، و Mailpit محلی. Compose از PostgreSQL 18.3 استفاده می‌کند. برای روش container، Docker با Linux containers لازم است.

```powershell
# src/HireYar.Api/appsettings.Development.local.json را با مقادیر توسعه بساز.
pwsh -File scripts/Start-DevelopmentContainers.ps1
dotnet tool restore
dotnet restore HireYar.slnx
```

اتصال SQL توسعه برای اجرای API روی میزبان: `Host=localhost;Port=5433;Database=hireyar_development;Username=hireyar;Password=<local-password>`. این فقط الگو است. برای PostgreSQL نصب‌شده روی میزبان، Host/Port/Username/Password همان instance را تنظیم کنید. دیتابیس توسعه باید مستقل از داده واقعی باشد.

```powershell
# src/HireYar.Api/appsettings.Development.local.json را با این ساختار بساز:
# {"ConnectionStrings":{"Default":"Host=localhost;Port=5433;Database=hireyar_development;Username=hireyar;Password=<local-password>"},"Security":{"SigningKey":"<random-secret-at-least-32-bytes>"}}
# این فایل در Git نادیده گرفته می‌شود و منبع تنظیمات توسعه برای اجرای میزبان و Compose است.
dotnet ef database update --project src/HireYar.Infrastructure --startup-project src/HireYar.Api
dotnet run --project src/HireYar.Api -- --urls http://localhost:5080
```

روش تمام-container پس از migration: `pwsh -File scripts/Start-DevelopmentContainers.ps1 -IncludeApi`. اسکریپت مقدارهای لازم Compose را از appsettings محلی می‌خواند و فقط در محیط همان فرایند قرار می‌دهد؛ `.env` لازم نیست. API هیچ migration خودکاری در startup اجرا نمی‌کند.

- OpenAPI: `http://localhost:5080/openapi/v1.json`؛ UI: `http://localhost:5080/swagger`. راهنمای کوتاه فارسیِ شروع و عنوان فارسی هر API بالای صفحه و کنار عملیات‌ها دیده می‌شود. هر دو در Development و Production بدون توکن در دسترس‌اند؛ endpointهای حساب و داده همچنان مجوز خود را می‌خواهند.
- Mailpit: `http://localhost:8025`؛ SMTP محلی: پورت 1025.
- health: `/health` فقط زنده‌بودن فرایند را نشان می‌دهد و آزمون آمادگی دیتابیس نیست.
- نمونه جریان‌ها: [HireYar.Api.http](src/HireYar.Api/HireYar.Api.http). cookie jar ابزار HTTP باید فعال باشد. OpenAPI مرجع کامل endpointهاست.

## تنظیمات

| کلید محیطی | کاربرد |
|---|---|
| `ConnectionStrings__Default` | اتصال SQL؛ در production کاربر دیتابیس کم‌مجوز و TLS معتبر |
| `Security__SigningKey` | secret تصادفی حداقل ۳۲ بایت؛ بدون fallback |
| `Security__Issuer`, `Security__Audience` | صادرکننده و مخاطب JWT |
| `Security__AccessMinutes`, `Security__RefreshDays` | پیش‌فرض ۱۵ دقیقه و ۷ روز |
| `Security__FrontendUrl` | origin پنل برای لینک‌های ایمیل |
| `Security__AllowedOrigins__0` | origin دقیق بدون slash پایانی؛ برای چند origin اندیس‌های بعدی |
| `Security__DataProtectionPath` | مسیر دائمی قابل نوشتن برای کلیدهای Identity/CSRF |
| `Smtp__Host`, `Smtp__Port`, `Smtp__From` | سرویس ارسال ایمیل |
| `Smtp__EnableSsl`, `Smtp__Username`, `Smtp__Password` | TLS و اطلاعات اتصال SMTP؛ خارج ریپو |
| `AllowedHosts` | hostnameهای واقعی API؛ پیش‌فرض localhost |
| `AuthRateLimit__PermitLimit` | پیش‌فرض ۲۰ درخواست در دقیقه برای هر IP روی مسیرهای auth/ارسال دعوت |

در production پنل و API باید HTTPS و زیر یک سایت باشند. گواهی TLS و reverse proxy باید در محیط استقرار تنظیم شوند؛ مسیر HTTP داخلی API فقط برای proxy در دسترس باشد. تنظیمات محیط Development را وارد production نکن.

اگر proxy درخواست HTTPS را با HTTP به کانتینر می‌رساند، باید هدر دقیق `X-Forwarded-Proto: https` را ارسال کند. API در Production تنها همین مقدار را برای scheme درخواست اعمال می‌کند تا کوکی CSRF با `Secure` صادر شود؛ هدرهای چندمقداری یا سایر مقادیر پذیرفته نمی‌شوند. مسیر HTTP داخلی API باید با firewall یا شبکه خصوصی فقط برای proxy قابل دسترس باشد؛ هر فرستنده‌ای که مستقیم به آن برسد می‌تواند مقدار این هدر را جعل کند. پس از انتشار، پاسخ HTTPS `GET /api/auth/csrf` باید 200 و کوکی `hireyar.csrf` دارای `Secure` باشد.

برای اجرای سرور با `ASPNETCORE_ENVIRONMENT=Production`، فایل نادیده‌گرفته‌شدهٔ `appsettings.Production.local.json` را کنار فایل‌های منتشرشدهٔ API قرار دهید، یا تنظیمات معادل را با متغیر محیطی بدهید. حداقل `ConnectionStrings:Default`، `Security:SigningKey`، `Security:Issuer`، `Security:Audience`، `Security:FrontendUrl`، `Security:AllowedOrigins`، `Security:DataProtectionPath` و تنظیمات SMTP لازم‌اند. فایل Development محلی همراه Git منتقل نمی‌شود. پس از انتشار نسخهٔ جدید، `/swagger` باید بدون Bearer باز شود؛ پاسخ 401 از `/api/auth/me` بدون Bearer رفتار درست است. کلید JWT سرور را ثابت و اختصاصی همان محیط نگه دارید؛ تغییر آن توکن‌های صادرشدهٔ قبلی را نامعتبر می‌کند.

برای اجرای همین `docker-compose.yml`، دو مقدار `HIREYAR_DB_CONNECTION` و `HIREYAR_SIGNING_KEY` را در محیط سرور یا فایل `.env` خارج Git تنظیم کنید. Compose بدون این دو مقدار اجرا نمی‌شود. پیش از بررسی ثبت‌نام، دسترسی API به PostgreSQL و اعمال migration را با تنظیمات همان محیط بررسی کنید؛ `GET /api/auth/csrf` به دیتابیس متصل نمی‌شود و موفقیت آن صحت اتصال را ثابت نمی‌کند.

کلیدهای Data Protection را روی volume دائمی، با دسترسی فقط حساب اجرای API، backup محدود و دیسک رمزگذاری‌شده نگه دار. ذخیره filesystem به‌تنهایی رمزگذاری در حالت سکون نیست؛ در production حفاظت volume یا certificate/KMS متناسب با میزبان الزامی است. حذف کلیدها لینک‌های Identity و CSRF را نامعتبر می‌کند. volume `protection-keys` در compose برای restart حفظ می‌شود. signing key JWT مستقل است.

## اتصال Angular و جریان نمونه

۱. با `withCredentials: true` از `GET /api/auth/csrf` توکن بگیر؛ cookie مربوط به antiforgery خودکار ذخیره می‌شود. مقدار JSON را در حافظه نگه دار و در `X-CSRF-TOKEN` برای تمام درخواست‌های غیر GET مسیر auth بفرست.
۲. register را با نام، سازمان، ایمیل و رمز بفرست. لینک Mailpit را در صفحه frontend بخوان و `userId` و `token` را با POST به confirm-email بده. هیچ JWT پیش از تأیید صادر نمی‌شود.
۳. login، Access Token را در JSON و refresh را فقط در cookie HttpOnly می‌دهد. access در حافظه Angular نگه‌داری شود؛ در `Authorization: Bearer ...` ارسال شود.
۴. پس از login یا تغییر هویت bearer، CSRF جدید با همان bearer دریافت کن. توکن CSRF وابسته به هویت درخواست است. هنگام انقضای access، برای دریافت CSRF و refresh هر دو Authorization را حذف کن؛ سپس access جدید و CSRF متناسب با هویت جدید بگیر. refresh هم‌زمان در چند tab را هماهنگ کن؛ بازپخش، خانواده همان نشست را باطل می‌کند.
۵. موقعیت بساز، شناسه آن را برای ساخت قالب همراه سؤال‌ها استفاده کن. در ویرایش `rowVersion` پاسخ قبلی را بفرست؛ 409 نیازمند reload و تصمیم کاربر است. آرشیو موقعیت با `status=Archived` و آرشیو قالب با `isArchived=true` در PUT انجام می‌شود.

refresh cookie، host-only، `Path=/api/auth`، `SameSite=Strict` و در production `Secure` است. CORS فقط originهای صریح و credentials را می‌پذیرد. CORS جای CSRF نیست. برای API با origin متفاوت، interceptor خودکار Angular لزوماً هدر XSRF را نمی‌فرستد؛ interceptor محدود به origin API خودتان برای `X-CSRF-TOKEN` بنویسید.

logout فقط نشست refresh جاری را باطل می‌کند؛ logout-all همه نشست‌ها را. JWT صادرشده تا انقضای کوتاهش معتبر می‌ماند. غیرفعال‌شدن و تغییر نقش در هر درخواست از دیتابیس بررسی می‌شوند و بلافاصله اثر دارند. تغییر/بازیابی رمز همه refreshها را باطل می‌کند.

خطاها ProblemDetails با `code` و `traceId` هستند؛ validation دارای `errors` است. پاسخ generic برای forgot/resend تضمین ارسال نیست؛ شکست SMTP با رویداد بدون داده حساس ثبت می‌شود. register و دعوت فیلد وضعیت ارسال دارند و resend در دسترس است.

برای خطای 500، لاگ سرور با همان `traceId` نوع خطا و خطای داخلی، SQLSTATE در صورت وجود، stack trace، وضعیت HTTPS و فقط وجود هدر forwarded proto را ثبت می‌کند. با جست‌وجوی `traceId` پاسخ در `docker logs hireyar-api` خطای همان درخواست را پیدا کنید. متن exception، مقدار هدر، cookie، token و body ثبت نمی‌شوند؛ پاسخ عمومی نیز جزئیات داخلی را نشان نمی‌دهد.

## تست و migration استقرار

```powershell
$env:HIREYAR_TEST_POSTGRES = 'Host=localhost;Port=5433;Username=hireyar;Password=<local-password>;Database=postgres'
dotnet test HireYar.slnx --logger 'trx;LogFileName=integration.trx' --results-directory TestResults
New-Item -ItemType Directory artifacts -Force
dotnet ef migrations script --idempotent --project src/HireYar.Infrastructure --startup-project src/HireYar.Api --output artifacts/migrations.sql
```

تست‌ها PostgreSQL واقعی و دیتابیس تصادفی با پیشوند `hireyar_tests_` می‌سازند؛ پس از اجرا فقط همان دیتابیس حذف می‌شود. اتصال فقط localhost، 127.0.0.1 یا ::1 مجاز است؛ بدون متغیر محیطی تست‌های SQL به‌صورت صریح Skip می‌شوند. SMTP در تست API جایگزین حافظه‌ای کنترل‌شده دارد و ایمیل واقعی ارسال نمی‌شود. نتیجه تست و محدودیت Docker/SMTP در [STATUS](docs/STATUS.md) ثبت است.

SQL script باید قبل از انتشار بازبینی و با مجوز جداگانه اعمال شود. روی production خودکار اجرا نکن. برنچ `dev` محل توسعه است؛ `production` اکنون فقط نقطه پایه است و هنوز نسخه آماده انتشار ندارد.

مراحل آینده: داوطلب و دعوت مصاحبه، جلسه صوتی، STT/LLM/TTS و گزارش مستند. حساب HR برای داوطلب ساخته نمی‌شود. هنگام ساخت جلسه باید از سؤال‌ها و معیارهای قالب snapshot گرفت تا ویرایش قالب روی جلسه قبلی اثر نگذارد. در این نسخه جلسه، snapshot engine، AI، فایل، OTP، social login، MFA، پرداخت، platform admin یا زیرساخت توزیع‌شده ساخته نشده است.



## انتقال از SQL Server

با تأیید کاربر، PostgreSQL از صفر ساخته می‌شود و انتقال داده SQL Server لازم نیست. migration قبلی با InitialPostgres جایگزین شد؛ آن را روی دیتابیس PostgreSQL خالی اجرا کنید. دیتابیس SQL Server قبلی حذف یا تغییر داده نشده است.

ConnectionStrings:Default در appsettings محلی یا ConnectionStrings__Default در محیط باید قالب Npgsql داشته باشد. برای TLS معتبر production از `SSL Mode=VerifyFull` و گواهی مورد اعتماد استفاده کنید؛ تنظیم توسعه را منتقل نکنید. کاربر runtime با کاربر migration/ایجاد دیتابیس جدا باشد.

rowVersion همچنان رشته Base64 در JSON است، اما اکنون ۱۶ بایت تصادفی است و توسط AppDbContext هنگام ذخیره تغییر می‌کند. مصرف‌کننده باید مقدار پاسخ جدید را بدون تفسیر برگرداند؛ توکن ۸بایتی SQL Server معتبر نیست. تغییرات مستقیم SQL در جداول موقعیت/قالب باید RowVersion را نیز تغییر دهند؛ مسیر پشتیبانی‌شده نوشتن، API است.

برای توسعه محلی انتخاب‌شده، `src/HireYar.Api/appsettings.Development.local.json` روی PostgreSQL داخل Docker در localhost:5433 تنظیم می‌شود. پس از روشن‌کردن Docker Desktop:

```powershell
pwsh -File scripts/Start-DevelopmentContainers.ps1
pwsh -File scripts/Update-DevelopmentDatabase.ps1
dotnet run --project src/HireYar.Api --launch-profile https
```

اسکریپت migration فقط اتصال محلی Docker همین پروژه را از appsettings محلی می‌خواند و secret را چاپ نمی‌کند. برای یک checkout جدید ابتدا appsettings محلی را طبق راهنمای بالا تنظیم کنید؛ فایل محلی حاوی secrets همراه Git منتقل نمی‌شود.
