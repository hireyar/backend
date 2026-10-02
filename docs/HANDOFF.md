# ادامه کار

برای دسترسی موقت همکاران، فایل کامل [Grant-ColleagueTestAccess.sql](../scripts/Grant-ColleagueTestAccess.sql) پس از migration Pricing اجرا شود: اعتبار قابل تغییر ۱۰۰ USD، Advanced یک‌ماهه، هر سازمان یک بار برای campaign. `InternalTestGrant` اعطای غیرنقدی با مبلغ اسمی است؛ پرداخت درگاه/درآمد نقدی محسوب نشود. اشتراک Advanced موجود استفاده می‌شود؛ اشتراک دیگر متداخل rollback می‌کند. روی مقصد اجرا نشده؛ تست PostgreSQL واقعی اولیه، تکرار و پس از مصرف موفق بود، DB مصنوعی حذف و cluster متوقف شد؛ build موفق.

## تحویل Pricing — 2026-10-01

برنچ فعال `codex/pricing` است؛ ابتدا از وضعیت پاک به `origin/dev` (`d9d4d72`, ادغام مصاحبه در dev) fast-forward شد. تغییرات این نوبت محلی و commitنشده‌اند؛ production و branch dev محلی تغییر نکردند؛ push/deploy انجام نشد. دستور قبلیِ اجرا روی dev بدون مصاحبه با درخواست تازهٔ کاربر superseded شد. قرارداد قابل استفاده و پنج نمونه JSON در [BILLING](BILLING.md)؛ تصمیم‌ها در ADR-018.

کاتالوگ پویا، قرارداد immutable در checkout، اشتراک صف‌شده، wallet/ledger/maximum reservation، settlement/cancellation/lazy expiry، media retention access و feature enforcement به مصاحبهٔ موجود متصل‌اند. IPaymentGateway فقط Disabled runtime دارد؛ fake از fixture tests ثبت می‌شود. درگاه واقعی یا آمادگی production payment ادعا نمی‌شود. Owner خرید سازمان خود را انجام می‌دهد؛ read برای Owner/Recruiter؛ pricing mutation فقط SystemAdmin از flag دوباره‌خوانده‌شدهٔ DB.

Verification نهایی: `dotnet test HireYar.slnx --no-restore` با HIREYAR_TEST_POSTGRES روی PostgreSQL 18 مستقل localhost:55447: **۱۵۱ موفق، صفر شکست/Skip**؛ TRX `TestResults/billing-complete.trx`. تست‌ها فقط synthetic data و fake provider دارند؛ هیچ درخواست واقعی AI/payment انجام نشد. tool restore و solution restore موفق‌اند. `dotnet build HireYar.slnx --no-restore` صفر warning/error و EF `has-pending-model-changes --no-build` بدون اختلاف بودند. migration `20261001080208_AddPricingBilling` واقعی است، script `artifacts/billing-migrations.sql` دوبار روی DB تصادفی بررسی شد و همان DB حذف شد. count نهایی fixture/script databaseها صفر و cluster آزمایشی این نوبت متوقف شد؛ `git diff --check` پاک است.

قبل از استفادهٔ پرداخت واقعی: adapter درگاه را با قرارداد idempotent PaymentId و verification مستقیم مبلغ/ارز/transaction بنویسید؛ checkout recovery و reconciliation و خطای پرداخت دیررس را با sandbox همان provider آزمایش کنید. مدیر را فقط با عملیات مورد اعتماد در DB provision کنید، credit product با قیمت/credit صریح بسازید، سازمان‌ها را از جریان خرید یا adjustment مستند تأمین کنید. در runtime خودکار هیچ اشتراک یا اعتبار رایگان ساخته نمی‌شود. جلسات started قدیمی بدون billing snapshot باید تمام/لغو و در صورت نیاز دعوت تازه شوند؛ retroactive billing وجود ندارد.

قانون مالی فعلی server response window محدود است و length واقعی صوت را اندازه نمی‌گیرد. نگهداری snapshot فقط download را محدود می‌کند؛ حذف فیزیکی cache/orphan/expired media و backup/volume با عملیات آماده شود. پایان اشتراک هنگام جلسه، نرخ را عوض نمی‌کند؛ expired reservation با wallet GET/start/adjustment settle می‌شود. مسیرهای mutation stateهای Cancelled/Suspended/Refund provider و proration آینده‌اند.

اختلاف source و audit قدیمی دربارهٔ ApiKey بررسی و ثبت شد؛ مقدار default خالی شد، secret محلی حفظ و تست نیاز کلید موفق شد. چون مقدار در تاریخچه بوده، rotation توسط operator لازم است؛ حذف default تاریخچه را پاک نمی‌کند. Generic Repository/پروژه/worker/distributed infrastructure و frontend اضافه نشده‌اند. یادداشت‌های پایین مربوط به اجراهای قبلی‌اند.

## تحویل audit پیش از staging — 2026-10-01

گزارش چهار بخش در [INTERVIEW_AUDIT](INTERVIEW_AUDIT.md) شامل نقص/فایل/اثر/fix/test، trace مراحل، migration و recovery است. تغییر کد فقط رفع IOException provider، graceful failure خواندن cache و default خالی ApiKey است؛ تست‌های race/recovery/scope/validation/logging تکمیل شدند. برنچ `codex/candidate-ai-interview` حفظ شد؛ تغییرهای مستندی تحویل فرانت نوبت قبل حفظ شدند. build صفر warning/error؛ ۱۱۸ موفق، صفر fail/Skip با PostgreSQL 18 مستقل localhost:55441؛ EF drift ندارد. اجرای نهایی دارای دسترسی loopback موفق است؛ شکست اتصال اجرای اولیه sandbox نتیجه کد محسوب نشد.

کلید تنظیم‌شده بدون چاپ مقدار به `src/HireYar.Api/appsettings.Development.local.json` منتقل و AI:Provider=NetArz همان‌جا فعال شد؛ فایل ignored است. آدرس/modelهای فعلی حفظ شدند. smoke واقعی با client موجود: TTS صوت مصنوعی ساخت، STT متن داد و LLM decision معتبر داد. هیچ داده واقعی candidate یا request body لاگ نشد. این smoke جای آزمون بار/کیفیت استقرار نیست. default provider مشترک Disabled می‌ماند؛ staging secret و فعال‌سازی خودش را لازم دارد. کلید قبلاً در Git بوده و rotation آن پیش از staging هنوز لازم است؛ حذف default تاریخچه را پاک نکرد.

schema و قرارداد API تغییر نکرد؛ migration مقصد طبق گفته کاربر قبلاً انجام شده و DB مقصد در audit دست‌نخورده است. fixture DBهای تصادفی حذف شدند و cluster مستقل تست پس از بررسی متوقف شد. commit/push/deploy انجام نشد. راهنمای [فرانت‌اند](FRONTEND_INTERVIEWS_HANDOFF.md) همچنان قرارداد معتبر است.

## تحویل به تیم فرانت‌اند — 2026-10-01

فایل مستقل [FRONTEND_INTERVIEWS_HANDOFF](FRONTEND_INTERVIEWS_HANDOFF.md) را به تیم فرانت بدهید؛ قرارداد ورودی/خروجی، stateهای UI، ضبط و دانلود خصوصی، retry، خطاها، APIهای موجودنبودن و پرامپت آماده برای Codex داخل همان فایل است. محدود به صفحات داوطلب، گزارش HR و فرم ایجاد ساده؛ معماری/وابستگی جدید لازم نیست. کد بک‌اند و migration تغییر نکردند. شروع این نوبت Git پاک روی `codex/candidate-ai-interview` و همگام با origin بود؛ یادداشت‌های commitنشده پایین مربوط به نوبت‌های قبلی هستند.

اعتبارسنجی این نوبت: قرارداد با Controllerها/DTOهای موجود تطبیق داده شد؛ build صفر warning/error و diff --check بدون خطای whitespace. تست‌های InterviewRulesTests/InterviewProviderTests/InterviewAudioTests: ۵۸ موفق، یک شکست، صفر Skip. تست Enabled_provider_requires_key_and_valid_timeouts انتظار خطا برای کلید تنظیم‌نشده دارد، ولی مقدار پیش‌فرض ApiKey در NetArzOptions فعلی خالی نیست؛ مقدار آن چاپ یا به این مستند منتقل نشد. این وضعیت از کد موجود است و در تغییر مستندی اصلاح نشده. تست اتصال واقعی NetArz یا PostgreSQL جدید جزو این نوبت نیست.

## تحویل vertical slice — 2026-10-01

برنچ همچنان `codex/candidate-ai-interview`؛ تمام تغییرات اولیه و این نوبت uncommitted حفظ شده‌اند. migration `20261001061608_AddCandidateInterviewFlow` و APIهای HR/candidate، token hash، بانک PositionSkill، صوت خصوصی و NetArz typed client اضافه شدند. [INTERVIEWS](INTERVIEWS.md) قراردادها، تنظیمات و مثال درخواست/JSON را دارد؛ یادداشت تحویل شروع پایین دیگر وضعیت فعلی نیست.

بررسی: restore/build موفق، build صفر warning/error؛ ۹۴/۹۴ تست موفق روی PostgreSQL 18 مستقل loopback:55441، صفر Skip. Fixture DB تصادفی می‌سازد و حذف می‌کند. provider جعلی فقط تست و HttpClient mocked است؛ هیچ هزینه/درخواست واقعی NetArz انجام نشده. EF model بدون drift است. cluster آزمایشی در artifacts/candidate-interview-pg است و پس از بررسی متوقف شد؛ دیتابیس موجود پروژه/production تغییر نکرده. آدرس یا کلید واقعی پروژه چاپ/به فایل جدید منتقل نشد.

نکات پیاده‌سازی: AutoInclude مجموعه‌های relational جلسه + split query در Infrastructure؛ Application همچنان فقط EF abstraction دارد. Answer/Evidence Guid از Domain می‌آیند و ValueGeneratedNever لازم است تا EF child تازه را UPDATE فرض نکند. به علت snapshot generation نامناسب owned/restrict، stateها entity مستقل‌اند. migration دستی قبلی AddMobileOtp target model ندارد؛ اگر migration تازهٔ اعمال‌نشده را با EF remove کردید، snapshot قبلی را از نسخهٔ درست ریپو حفظ کنید، وگرنه ابزار snapshot خالی می‌سازد. تاریخچه migration موجود تغییر نکرده است.

lease روی Session و نسخه ۱۶بایتی در هر stage کنترل می‌شود؛ worker قدیمی بعد از تماس خارجی session را reload و lease را بررسی می‌کند. transcript persistشده STT را در retry حذف می‌کند. SaveChanges کوتاه است و external call در transaction نیست. snapshot plan هنگام دعوت ثابت می‌شود؛ بانک موقعیت منبع سؤال است و قالب قدیمی فقط تنظیم مدت را می‌دهد. کد current سؤال بعدی انتخاب نمی‌کند و فقط cache موجود را می‌خواند.

برای استفاده واقعی، تنظیمات خارج Git طبق example با AI:Provider=NetArz و کلید واقعی لازم است؛ Disabled هیچ HTTP خارجی نمی‌زند. اجرا با کلید واقعی، volume پایدار، retention و cleanup فایل‌های orphan هنوز عملیاتی بررسی نشده‌اند. هیچ push/deploy/commit انجام نشده.

## تحویل شروع مصاحبه — 2026-10-01

برنچ فعلی `codex/candidate-ai-interview` از dev، با تغییرات commitنشده است. کاربر ساخت برنچ و شروع پرامپت کامل Candidate AI Interview را خواست. Domain در `src/HireYar.Domain/Interviews` و policy/validator/prompt در `Application/Services/Interviews`، DTO در `Application/DTOs/Interviews` و تست‌ها در `tests/HireYar.Tests/InterviewRulesTests.cs` اضافه شدند. API/Infrastructure موجود تغییر نکردند؛ این یک هستهٔ اولیه است و اجرای مصاحبه هنوز ممکن نیست.

restore و build موفق؛ build صفر warning/error. تست کل ۲۶ pass، صفر fail، ۲۷ skip بدون HIREYAR_TEST_POSTGRES؛ ۲۴ تست جدید همه pass. migration یا بررسی SQL انجام نشده؛ از موفقیت تست Domain نتیجهٔ concurrency دیتابیس نگیرید.

تحلیل ریپو: Candidate/Application/Skill/storage وجود ندارند؛ InterviewQuestion زیر InterviewTemplate است و Competency متن دارد؛ Template FK مرکب OrganizationId/JobPositionId دارد. TemplateService سؤال‌ها را delete/insert می‌کند و شناسه باقی‌مانده را نگه می‌دارد، پس snapshot جلسه باید تاریخچه را مستقل از این تغییر نگه دارد. HR JWT با CurrentAccountValidation از DB بررسی می‌شود؛ برای داوطلب باید token scoped جدا ساخته شود. سرویس‌ها مستقیم از IApplicationDbContext استفاده می‌کنند؛ Repository/MediatR اضافه نشود. Provider فعلی Npgsql و خطاها AppException/ProblemDetails هستند.

گام بعد: قبل از اتصال به EF، constructor/private collection مدل‌ها را برای materialization بررسی و backing-field mapping یا constructor خصوصی مناسب اضافه کن؛ enumها با check constraint، FK مرکب سازمان در مدل CandidateApplication/Session و unique session/question برای پاسخ. Domain اکنون فقط invariantهای حافظه‌ای را enforce می‌کند؛ optimistic concurrency باید در persistence اضافه شود. mapped stateها و evidence هنوز جدول نیستند. policy برندهٔ backend است؛ علت sufficient evidence/no questions باید فقط پس از بررسی policy برای Complete استفاده شود. تمام AI خارج transaction، هر stage ذخیره شود و processing رهاشده پس از قطع فرایند lease/recovery داشته باشد. validator باید در زمان integration حد shortlist تنظیم‌شده را هم رعایت کند؛ اکنون policy پیش‌فرض ۱۰ را بازسازی می‌کند. سپس باقی موارد فهرست‌شده در BACKLOG را اجرا کن.

کلید/داده واقعی در کد اضافه نشده؛ NetArz واقعی فراخوانی نشده و push/deploy انجام نشده است.

به‌روزرسانی همین نوبت: بخش `Kavenegar` به `appsettings.json` اضافه شد. `ApiKey` و `Template` فعلاً خالی‌اند؛ مقدار واقعی را فقط در تنظیمات خارج Git یا override محیطی وارد کنید. `UseFixedOtp=false` در تنظیمات مشترک است. در Development مقدار `UseFixedOtp=true` و `FixedOtpCode=123456` تنظیم شده؛ درخواست OTP پیامک ارسال نمی‌کند و تأیید با این کد، با همان انقضا و محدودیت تلاش، انجام می‌شود. تست PostgreSQL 18: ۲۹ موفق، صفر شکست/Skip؛ build صفر warning/error. PostgreSQL موقت پس از تست متوقف شد. push/deploy انجام نشده است.

در 2026-09-27 مسیر موبایل/OTP اضافه شد: `GET /api/auth/csrf`، سپس `POST /api/auth/otp/request` با `{ "mobile": "09..." }`، سپس `POST /api/auth/otp/verify` با `{ "mobile": "09...", "code": "......" }`. پاسخ verify همان access token و refresh cookie جاری است. حساب جدید Owner با سازمان اولیه ساخته می‌شود؛ نام شرکت از `PUT /api/organization` ویرایش می‌شود. برای ارسال واقعی `Kavenegar__ApiKey` و `Kavenegar__Template` را فقط در محیط تنظیم کنید؛ ارسال واقعی هنوز آزمایش نشده است. migration `20260927090000_AddMobileOtp` باید پیش از استفاده در محیط مقصد اعمال شود. اجرای محلی روی PostgreSQL 18: ۲۸/۲۸ تست موفق؛ build صفر warning/error؛ `has-pending-model-changes` بدون اختلاف. PostgreSQL موقت تست متوقف شد. مسیرهای ایمیلی و دعوت Recruiter عمداً تا پاسخ درباره مهاجرت کاربران فعلی باقی مانده‌اند. پس از تصمیم، مستندات قدیمی ایمیل/SMTP و تست‌ها نیز باید با قرارداد نهایی هماهنگ شوند. push/deploy انجام نشده است.

ثبت‌نام بدون SMTP: گزینهٔ `Security:AutoConfirmEmailOnRegistration` در تنظیمات مشترک فعلاً true است. حساب Owner جدید با `EmailConfirmed=true` ایجاد می‌شود، ایمیل تأیید ارسال نمی‌شود و پاسخ ثبت‌نام `confirmationEmailSent=false` دارد. حساب‌های قدیمیِ تأییدنشده تغییر نمی‌کنند. build موفق؛ تست PostgreSQL این شاخه به علت نبود `HIREYAR_TEST_POSTGRES` اجرا نشد. قبل از آماده‌کردن SMTP این گزینه را به false برگردانید؛ migration دیتابیس هدف نیز برای جدول `AspNetUsers` لازم است.

بررسی جاری ثبت‌نام Production: پاسخ `unexpected_error` با 500 به‌تنهایی علت را نشان نمی‌دهد و هنوز stack trace همان درخواست از `docker logs hireyar-api` دریافت نشده است. پس از انتشار، `traceId` پاسخ باید در همان خط خطای لاگ پیدا شود؛ نوع exception، inner type و SQLSTATE را بررسی کنید و داده‌های محرمانه را منتقل نکنید. موفقیت CSRF صحت اتصال PostgreSQL را ثابت نمی‌کند. `appsettings.json` دیگر connection string یا signing key ندارد؛ Compose به `HIREYAR_DB_CONNECTION` و `HIREYAR_SIGNING_KEY` خارج Git نیاز دارد. build بدون هشدار/خطا، تست ۲ موفق و ۲۴ Skip به علت نبود PostgreSQL تست، و اعتبارسنجی Compose موفق شدند. اتصال واقعی و migration سرور هنوز تأیید نشده‌اند؛ push/deploy انجام نشده است. مقادیر قبلاً ثبت‌شده در Git باید در محیط مربوط چرخانده شوند.

بررسی 2026-09-26: stack trace سرور `DefaultAntiforgery.CheckSSLConfig` را با `IsHttps=False` و وجود هدر forwarded تأیید کرد. طبق درخواست صریح کاربر، `KnownProxies` و متغیر IP از Program و Compose حذف شدند. middleware کوچک Production فقط مقدار دقیق `X-Forwarded-Proto: https` را از هر مبدأ اعمال می‌کند؛ پورت HTTP داخلی باید در firewall/شبکه فقط برای proxy قابل دسترس باشد. `CookieSecurePolicy.Always` حفظ شد. Build صفر هشدار/خطا و تست ۲ موفق، ۲۴ Skip به علت نبود PostgreSQL بود. `ApiExceptionHandler` stack trace را بدون متن exception یا داده درخواست ثبت می‌کند. push/deploy انجام نشده؛ پس از انتشار، `/api/auth/csrf` و کوکی Secure را بررسی کنید.

آخرین تغییر (2026-09-25): راهنمای کوتاه فارسی شروع در `OpenApiText` افزوده شد و عنوان همهٔ عملیات‌های Controller در OpenAPI فارسی شد. ترتیب پیشنهادی حساب جدید: CSRF، ثبت‌نام، تأیید ایمیل، ورود؛ حساب موجود: CSRF، ورود، Bearer و مشاهده حساب. بعد از ورود CSRF جدید با Bearer لازم است؛ refresh با کوکی و بدون Bearer انجام می‌شود. Build موفق و تست مستقل Production Swagger/OpenAPI شامل وجود راهنما و عنوان همهٔ عملیات‌ها موفق شد. تست PostgreSQL در این نوبت اجرا نشد. تغییرات این نوبت commit/push/deploy نشده‌اند.

رفع جدید: کاربر در Development خطای `Failed to load configuration ... UserSecrets/.../secrets.json` گزارش کرد. `UserSecretsId` از `src/HireYar.Api/HireYar.Api.csproj` حذف شد؛ تنظیمات از appsettings.Development.local.json خوانده می‌شوند. Build موفق، اجرای واقعی API موفق، `/health` و `/swagger/index.html` هر دو 200؛ تست جاری ۱ موفق و ۲۴ Skip بدون PostgreSQL بود. فایل User Secrets کاربر تغییر داده نشد.

آخرین بررسی: کاربر `.env` را خالی کرد؛ Compose به متغیرهای اجباری وابسته بود. اسکریپت `scripts/Start-DevelopmentContainers.ps1` اکنون JWT key و رمز اتصال را از appsettings.Development.local.json می‌خواند؛ برای postgres/mailpit اجرا می‌شود و با `-IncludeApi` API را هم می‌سازد. `.env` از Git untrack شد اما در تاریخچهٔ قبلی مقادیر وجود داشت؛ پس چرخش secretها در محیط‌های مربوط لازم است. appsettings محلی از Docker build context هم کنار گذاشته شد. Docker daemon در محیط فعلی Access denied داد و اجرای کانتینرها تأیید نشد. درخواست 401 کاربر از `/swagger` روی سرور بود؛ Swagger middleware قبل از fallback authorization قرار گرفت و تست Production نشان داد `/swagger/index.html` و `/openapi/v1.json` پاسخ 200 و `/api/auth/me` بی‌توکن پاسخ 401 می‌دهد. Build موفق؛ تغییرات commit/push/deploy نشده‌اند. برای سرور باید تنظیمات Production جداگانه فراهم و نسخهٔ جدید منتشر شود.

تنظیمات محلی `src/HireYar.Api/appsettings.Development.local.json` در Git نادیده گرفته می‌شود. Program و EF design-time آن را می‌خوانند و متغیر محیطی اولویت بالاتر دارد. اسکریپت migration همین فایل را می‌خواند.

ابتدا AGENTS، PROJECT، ARCHITECTURE (ADR-012)، STATUS و BACKLOG را بخوان. provider اکنون Npgsql EF Core 10.0.3 است. migration InitialPostgres جای InitialCreate را گرفته؛ از script قبلی artifacts/migrations.sql استفاده نکن، script جدید artifacts/postgres-migrations.sql است.

نقاط حساس: Persistence/Configurations و AppDbContext (RowVersion تصادفی ۱۶بایتی)، Services/Auth/Accounts و Services/Members/TeamMembers (FOR UPDATE)، Api/Errors/ApiExceptionHandler (SQLSTATE)، tests/ApiFixture (PostgreSQL واقعی و HIREYAR_TEST_POSTGRES).

پیش از اصلاح اخیر، تست‌های PostgreSQL نتیجهٔ ۲۳ موفق و ۱ شکست OpenAPI در Production داشتند. برای اصلاح اخیر build بدون هشدار و تست مستقل Production Swagger موفق شد؛ مجموعهٔ تست‌های PostgreSQL هنوز دوباره اجرا نشده است. migration و script idempotent قبلاً روی PostgreSQL 18.3 مستقل تأیید شدند.

کاربر برای توسعه روزمره Docker PostgreSQL روی localhost:5433 را انتخاب کرد. قدم بعد HY-006: پس از دسترسی Docker daemon، `pwsh -File scripts/Start-DevelopmentContainers.ps1` و سپس `pwsh -File scripts/Update-DevelopmentDatabase.ps1` را اجرا کن. اسکریپت‌ها secret چاپ نمی‌کنند. اجرای واقعی compose/SMTP هنوز تأیید نشده است.

برنچ dev است؛ تغییرات اخیر commit/push/deploy نشده‌اند. `dotnet tool restore` در این محیط به NuGet.Config دسترسی نداشت؛ بررسی EF اجرا نشد. Docker daemon هم Access denied داد. HY-006 و HY-007 باز هستند؛ HY-010 با تصمیم دسترسی عمومی مستندات و تست مستقل بسته شد.

یادداشت اجرای نهایی مصاحبه: یک run با خطای transport اتصال PostgreSQL شکست خورد؛ اجرای مجدد با SSL Mode=Disable و GSS Encryption Mode=Disable صرفاً روی cluster محلی آزمایشی، ۹۴/۹۴ موفق بود. build نهایی صفر warning/error؛ DB تصادفی تست باقی نماند و cluster آزمایشی متوقف شد. اسکریپت idempotent در artifacts/candidate-interview-migrations.sql تولید شد ولی این اسکریپت جداگانه اجرا نشد؛ اعمال migration از fixture واقعی PostgreSQL بررسی شد.

## تنظیمات AI در appsettings — 2026-10-02

روی برنچ `codex/ai-appsettings` از `dev`، بخش `AI` در `src/HireYar.Api/appsettings.json` اضافه شد: Provider=NetArz، BaseUrl، مدل‌های STT/LLM/TTS، voice، speed و سه timeout. binding موجود `AI:NetArz` و انتخاب `AI:Provider` همین تنظیمات را مصرف می‌کنند؛ override محیطی/local همچنان اولویت دارد. ApiKey در محل فعلی و تمام فایل‌های کلید دست‌نخورده ماند؛ کلید تازه در appsettings اضافه یا کپی نشد. یادداشت‌های تاریخی Disabled برای پیش‌فرض مشترک دیگر وضعیت جاری این برنچ نیستند.

Build موفق با صفر warning/error؛ تست‌های InterviewProviderTests و InterviewAudioTests: ۴۳ موفق، صفر شکست/Skip. تست کلید خالی اکنون مقدار خالی را صریحاً فقط در fixture خود می‌دهد تا وابسته به default فعلی نباشد. تماس واقعی AI، تست PostgreSQL، migration، commit، push و deploy انجام نشدند.
