# HireYar Backend

پایه حساب‌های سازمانی و API پنل HR برای مصاحبه اولیه استخدام. تصمیم نهایی استخدام انسانی است؛ موتور مصاحبه و AI هنوز پیاده نشده‌اند.

نقطه شروع توسعه: [AGENTS](AGENTS.md). مراجع: [محصول](docs/PROJECT.md)، [معماری](docs/ARCHITECTURE.md)، [وضعیت واقعی](docs/STATUS.md)، [Backlog](docs/BACKLOG.md)، [ادامه کار](docs/HANDOFF.md).

## پیش‌نیاز و راه‌اندازی

.NET SDK 10.0.400، PostgreSQL 18، و Mailpit محلی. Compose از PostgreSQL 18.3 استفاده می‌کند. برای روش container، Docker با Linux containers لازم است.

```powershell
Copy-Item .env.example .env
# مقادیر placeholder را محلی با secret تصادفی جایگزین کن؛ .env وارد Git نشود.
docker compose -f compose.dev.yaml up -d postgres mailpit
dotnet tool restore
dotnet restore HireYar.slnx
```

اتصال SQL توسعه برای اجرای API روی میزبان: `Host=localhost;Port=5433;Database=hireyar_development;Username=hireyar;Password=<local-password>`. این فقط الگو است. برای PostgreSQL نصب‌شده روی میزبان، Host/Port/Username/Password همان instance را تنظیم کنید. دیتابیس توسعه باید مستقل از داده واقعی باشد.

```powershell
dotnet user-secrets set 'ConnectionStrings:Default' '<development-connection-string>' --project src/HireYar.Api
dotnet user-secrets set 'Security:SigningKey' '<random-secret-at-least-32-bytes>' --project src/HireYar.Api
# EF factory عمداً فقط متغیر محیطی می‌خواند؛ مقدار را از تنظیم امن محلی بگیر.
$env:ConnectionStrings__Default = '<development-connection-string>'
dotnet ef database update --project src/HireYar.Infrastructure --startup-project src/HireYar.Api
dotnet run --project src/HireYar.Api -- --urls http://localhost:5080
```

روش تمام-container پس از migration: `docker compose -f compose.dev.yaml up -d --build api`. API هیچ migration خودکاری در startup اجرا نمی‌کند. مقادیر `.env` صرفاً توسط compose خوانده می‌شوند؛ `dotnet run` خودکار آن فایل را نمی‌خواند.

- OpenAPI: `http://localhost:5080/openapi/v1.json`؛ UI: `http://localhost:5080/swagger`، فقط Development. در checkout فعلی JSON مستندات به علت تغییر قبلی در Production هم باز است؛ HY-010 باید پیش از انتشار تعیین تکلیف شود.
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

در production پنل و API باید HTTPS و زیر یک سایت باشند. گواهی TLS و reverse proxy مورد اعتماد باید در محیط استقرار تنظیم شوند؛ هدرهای forwarded از هر فرستنده پذیرفته نمی‌شوند. این تسک زیرساخت deployment نمی‌سازد. تنظیمات محیط Development را وارد production نکن.

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

ConnectionStrings:Default در User Secrets یا ConnectionStrings__Default در محیط باید قالب Npgsql داشته باشد. برای TLS معتبر production از `SSL Mode=VerifyFull` و گواهی مورد اعتماد استفاده کنید؛ تنظیم توسعه را منتقل نکنید. کاربر runtime با کاربر migration/ایجاد دیتابیس جدا باشد.

rowVersion همچنان رشته Base64 در JSON است، اما اکنون ۱۶ بایت تصادفی است و توسط AppDbContext هنگام ذخیره تغییر می‌کند. مصرف‌کننده باید مقدار پاسخ جدید را بدون تفسیر برگرداند؛ توکن ۸بایتی SQL Server معتبر نیست. تغییرات مستقیم SQL در جداول موقعیت/قالب باید RowVersion را نیز تغییر دهند؛ مسیر پشتیبانی‌شده نوشتن، API است.

برای توسعه محلی انتخاب‌شده، `.env` و User Secrets روی PostgreSQL داخل Docker در localhost:5433 تنظیم می‌شوند. پس از روشن‌کردن Docker Desktop:

```powershell
docker compose -f compose.dev.yaml up -d postgres mailpit
pwsh -File scripts/Update-DevelopmentDatabase.ps1
dotnet run --project src/HireYar.Api --launch-profile https
```

اسکریپت migration فقط اتصال محلی Docker همین پروژه را از User Secrets می‌خواند و secret را چاپ نمی‌کند. برای یک checkout جدید ابتدا `.env` و User Secrets را طبق راهنمای بالا تنظیم کنید؛ فایل‌های محلی حاوی secrets همراه Git منتقل نمی‌شوند.
