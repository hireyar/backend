# معماری و تصمیم‌ها

## ADR-017 — اجرای مصاحبه با persistence و provider (2026-10-01)

Domain اولیه با constructor خصوصی و backing field به EF متصل شد؛ QuestionSnapshot/SkillSnapshot/SkillState/SkillEvidence/CoveredTopic entity رابطه‌ای مستقل‌اند، نه JSON و نه owned navigation. این انتخاب drift مشاهده‌شدهٔ snapshot حذف ownedها را رفع کرد و FK بین answer/question/skill را صریح کرد. tenant-owned CandidateApplication/Skill/PositionSkill/BankQuestion/Session با کلیدهای مرکب به موقعیت/قالب/مهارت همان سازمان وصل‌اند؛ همهٔ delete behaviorها Restrict است.

بانک موقعیت مستقل از editable questionهای قالب است؛ template feature و delete/insert آن تغییر نمی‌کنند. متن/موضوع/مهارت/difficulty و plan در زمان **صدور دعوت** snapshot می‌شوند، سپس start همان plan را فعال می‌کند. این تثبیت زودتر از start مانع تغییر بی‌صدای دعوت پس از ویرایش HR می‌شود. بانک می‌تواند تغییر کند اما session فقط snapshot خودش را ارزیابی می‌کند.

Candidate token تصادفی ۲۵۶بیتی با SHA-256 ذخیره، expiry و revoke است. scheme اختصاصی Interview در Api فقط یک claim شناسه جلسه صادر می‌کند و CurrentAccountValidation HR را استفاده نمی‌کند. HR policy همچنان JWT معتبر و نقش server-side دارد؛ توکن candidate دسترسی HR ندارد. توکن از Authorization header می‌آید و در URL/log قرار نمی‌گیرد. invite token فقط یک بار در پاسخ HR عرضه می‌شود؛ ارسال پیام دعوت اضافه نشده.

صدای پاسخ و سؤال در storage خصوصی خارج wwwroot ذخیره می‌شود؛ key و signature و حجم enforce می‌شوند و دانلود از endpoint scoped است. cache سازمانی صوت با hash متن normalize/model/voice/speed میان جلسه‌ها reuse می‌شود. TTS ناموفق integrity جلسه را خراب نمی‌کند و متن برمی‌گردد. concurrent cache miss می‌تواند درخواست تکراری ایجاد کند ولی unique cache key یک winner نگه می‌دارد. Local storage برای استقرار تک instance نیازمند volume خصوصی پایدار است.

پاسخ مستقل unique session/question است. processing state و transcript و diagnostics persist می‌شوند. Session lease با expiry و optimistic RowVersion ۱۶بایتی در هر stage نوشته می‌شود؛ stale lease با retry همان پاسخ قابل بازیابی است. worker قبلی پیش از commit بعد از network، lease معتبر را دوباره بررسی می‌کند. transaction طولانی، worker پس‌زمینه یا distributed lock اضافه نشده است. قطع درخواست می‌تواند تا انقضای lease processing بماند؛ current امکان retry را مشخص می‌کند.

AI abstraction در Application، NetArz typed HttpClientFactory و options/timeouts در Infrastructure است؛ key خالی با NetArz در startup رد می‌شود و Disabled فراخوانی خارجی ندارد. LLM advisory است؛ shortlist دقیق همان attempt اعتبارسنجی می‌شود، policy پس از evidence دوباره hard limits/coverage را اعمال می‌کند. invalid JSON/401/429/5xx/timeout → deterministic fallback بدون evidence نامعتبر. خطای STT → retryable همان صوت. score evidence و justification کوتاه قابل مشاهده‌اند؛ hidden reasoning و hiring/rejection خودکار وجود ندارند. هیچ transaction هنگام AI باز نیست و retry خودکار POST خارجی انجام نمی‌شود.

metricها فقط provider/source کم‌تنوع دارند؛ شناسه‌ها فقط در structured log مجاز، بدون transcript/audio/token/key. مدت واقعی صوت بدون decoder تأیید نمی‌شود؛ سقف حجم و مدت کلی server-side هستند و duration client صرفاً محدودیت کمکی است. این محدودیت و سیاست عملیاتی retention/orphan cleanup در INTERVIEWS ثبت شده‌اند.

## ADR-016 — شروع هستهٔ مصاحبه (2026-10-01)

در پاسخ به درخواست مستقل Candidate AI Interview، Domain/Interviews و سرویس policy/validator در Application اضافه شدند؛ تصمیم پیشین درباره موکول‌بودن AI به تسک مستقل اکنون برای این درخواست فعال شده است. معماری Service → IApplicationDbContext و پوشه‌بندی مسئولیتی حفظ می‌شود. مدل جلسه snapshot سؤال/مهارت را مستقل نگه می‌دارد تا جایگزینی سؤال‌های قالب، تاریخچه جواب و evidence را تغییر ندهد. core score/confidence/coverage برابر ۰..۱ و delta برابر -۱..۱ است؛ delta ذخیره‌شده اثر واقعی پس از clamp است. topic با SkillId شناسایی می‌شود.

LLM فقط decision support است؛ shortlist و completion از policy می‌آیند. validation همه assessment را قبل از mutation بررسی می‌کند؛ پاسخ نامعتبر evidence نمی‌سازد و fallback قطعی دارد. evidence فعلاً به مهارت و موضوع همان سؤال محدود می‌شود. prompt نسخه v1 صرفاً شواهد شغلی می‌خواهد و تصمیم استخدام یا hidden reasoning نمی‌خواهد.

این تصمیم فعلاً فقط در Domain/Application پیاده شده است. persistence، schema، candidate auth و token scope، audio storage، NetArz و HTTP endpoints هنوز آماده نیستند. constructorهای مدل‌ها باید هنگام EF integration با materialization سازگار شوند. تمام tenant ownership از Application و identity/token معتبر سرور resolve خواهد شد؛ Guidهای constructor ورودی HTTP قابل اعتماد محسوب نمی‌شوند.

## تغییر احراز هویت موبایلی (در حال انتقال، 2026-09-27)

تنظیمات ارسال در بخش `Kavenegar` فایل `appsettings.json` تعریف شده‌اند: `ApiKey` و `Template` خالی هستند تا کلید واقعی در Git قرار نگیرد. `UseFixedOtp` در تنظیمات مشترک false است؛ در `appsettings.Development.json` مقدار true و `FixedOtpCode` برابر `123456` است. وقتی فلگ فعال باشد، همان کد شش‌رقمی برای challenge ذخیره می‌شود و هیچ درخواست SMS انجام نمی‌شود. اعتبار پنج دقیقه، محدودیت ۶۰ ثانیه، پنج تلاش و مصرف یک‌باره در هر دو حالت برقرارند. هنگام فعال‌سازی، کد باید دقیقاً شش رقم ASCII باشد و در startup بررسی می‌شود. مقدارهای محیطی همچنان می‌توانند appsettings را override کنند.

مسیر جدید `POST /api/auth/otp/request` و `POST /api/auth/otp/verify` شماره موبایل ایران با قالب `09` و ۱۱ رقم را می‌پذیرد. کد شش‌رقمی با CSPRNG تولید و فقط HMAC-SHA256 آن با کلید امضای سرور در PostgreSQL ذخیره می‌شود. اعتبار پنج دقیقه، فاصله درخواست ۶۰ ثانیه برای هر موبایل و حداکثر پنج تلاش اشتباه است. مصرف challenge زیر قفل `FOR UPDATE` انجام می‌شود. کاوه‌نگار VerifyLookup از Infrastructure با HTTPS و `receptor`/`token`/`template` فراخوانی می‌شود؛ کلید API و قالب از `Kavenegar__ApiKey` و `Kavenegar__Template` خارج Git می‌آیند. چون کلید در URL سرویس‌دهنده است، این HttpClient از logging خودکار URI استفاده نمی‌کند. شکست ارسال پاسخ 503 می‌دهد و متن کد یا شماره در لاگ ثبت نمی‌شود.

پس از OTP معتبر، حساب موبایلی موجود وارد می‌شود؛ در نبود آن، Owner و سازمان اولیه در یک تراکنش ساخته و نشست JWT/refresh صادر می‌شود. Owner نام سازمان را با `PUT /api/organization` تکمیل می‌کند. ایمیل برای حساب جدید موبایلی null است و پاسخ پروفایل/اعضا فیلد موبایل دارد. migration `AddMobileOtp` ستون‌های ایمیل را nullable، موبایل را unique و جدول challenge را اضافه می‌کند. مسیرهای ایمیل/رمز و دعوت Recruiter فعلاً برای جلوگیری از قطع حساب‌های موجود حفظ شده‌اند؛ حذف آن‌ها به تصمیم مهاجرت حساب‌های فعلی وابسته است. این تصمیم موقت جایگزین ADR-010 درباره نبود OTP شده و مستلزم بازنویسی نهایی آن پس از تعیین تکلیف مهاجرت است.

## تصمیم موقت ثبت‌نام بدون SMTP

گزینهٔ `Security:AutoConfirmEmailOnRegistration` هنگام ثبت‌نام Owner جدید، `EmailConfirmed` را در همان تراکنش ساخت حساب برابر true می‌کند و ایمیل تأیید نمی‌فرستد. پیش‌فرض کد false است؛ فعلاً در `appsettings.json` مقدار true دارد. این گزینه تنها برای حساب‌های تازه است و حساب‌های قدیمیِ تأییدنشده را تغییر نمی‌دهد. پس از آماده‌شدن SMTP باید false شود تا جریان تأیید ایمیل دوباره فعال شود. تا آن زمان مالکیت نشانی ایمیل بررسی نمی‌شود.

## ساختار واقعی

`HireYar.slnx` شامل Domain (Organizations/JobPositions/InterviewTemplates)، Application (سرویس‌ها، DTO، validation و قراردادها)، Infrastructure (Identity، SQL/DbContext/mapping/migrations، JWT، SMTP، دعوت) و Api (Controller، HTTP، مجوزها، CSRF، composition) و `tests/HireYar.Tests` است.

وابستگی: Application → Domain؛ Infrastructure → Application/Domain؛ Api → Application و Infrastructure. مسیر درخواست Controller → Application Service → DbContext abstraction یا مرز Identity/SMTP است. سرویس Auth مرز Identity را فراخوانی می‌کند؛ UserManager و تراکنش‌های وابسته به آن در Infrastructure می‌مانند. HttpContext فقط در Api است.

## تصمیم‌های پذیرفته‌شده

| شناسه | تصمیم، دلیل و پیامد |
|---|---|
| ADR-001 | .NET 10، EF Core 10، PostgreSQL و Controller؛ یک برنامه و یک دیتابیس برای سادگی عملیاتی |
| ADR-002 | Clean Architecture سبک؛ EF DbSet در IApplicationDbContext مجاز است تا repository عمومی نسازیم؛ Application به EF وابسته می‌شود ولی به SQL provider نه |
| ADR-003 | Identity فقط Infrastructure؛ یک کاربر/سازمان و نقش enum محدود Owner/Recruiter؛ unique index ایمیل نرمال و Owner سازمان |
| ADR-004 | JWT با HS256، issuer/audience/expiry/jti؛ وضعیت و نقش در هر درخواست از DB دوباره خوانده می‌شود؛ هزینه یک query در مقابل اثر فوری مجوز |
| ADR-005 | refresh تصادفی ۲۵۶بیتی، ذخیره SHA-256، یک family در هر login؛ تمام mutationهای نشست زیر قفل SELECT FOR UPDATE کاربر و تراکنش؛ replay خانواده همان نشست را باطل می‌کند |
| ADR-006 | cookie host-only محدود به auth و CSRF استاندارد ASP.NET؛ SameSite Strict برای deployment زیر یک سایت؛ SPA توکن CSRF را با هویت فعلی تازه می‌کند |
| ADR-007 | queryها صریحاً OrganizationId سرور را شرط می‌کنند؛ query filter عمومی نداریم. کلید مرکب JobPosition/Organization در FK قالب از اتصال بین سازمان‌ها در DB جلوگیری می‌کند |
| ADR-008 | RowVersion تصادفی ۱۶بایتی application-managed و 409؛ ویرایش parent و جایگزینی سؤال‌ها تراکنشی، شناسه سؤال‌های باقی‌مانده حفظ می‌شود؛ delete/insert داخلی سؤال امکان جابه‌جایی ترتیب زیر unique index می‌دهد |
| ADR-009 | دعوت Recruiter با hash و انقضای سه‌روزه؛ قفل سازمان عملیات دعوت را سریالی می‌کند؛ unique index رقابت ایمیل و دعوت باز را نیز پوشش می‌دهد |
| ADR-010 | بدون MediatR/CQRS/Generic Repository/worker/OTP؛ توسعه آینده فقط با تسک مستقل |

همه Guid و تاریخ‌ها DateTimeOffset UTC هستند. cascade delete تمام روابط Restrict است. enumهای وضعیت و نقش در SQL به متن محدود و check constraint ذخیره می‌شوند. DTOها اجازه overposting ندارند و Entity مستقیم برنمی‌گردد. pagination پیش‌فرض ۲۰، سقف ۱۰۰ و ترتیب CreatedAtUtc سپس Id است.

حدود متن: نام شخص/سازمان/عنوان ۲۰۰، ایمیل ۲۵۶، شرح موقعیت ۱۰۰۰۰، seniority صد، language بیست، سؤال ۲۰۰۰، competency دویست، راهنمای ارزیابی ۴۰۰۰. رمز ۱۲ تا ۱۲۸ و الزامات استاندارد Identity. قالب ۱ تا ۳۰ سؤال، ترتیب پیوسته از ۱، زمان ۵ تا ۶۰ و follow-up صفر تا سه.

Identity token provider استاندارد با اعتبار دو ساعت و Data Protection دائمی استفاده می‌شود. تأیید دوباره ایمیل با flag و قفل رد می‌شود؛ reset با security stamp یک‌بارمصرف است. ساخت سازمان و Owner تراکنشی؛ ارسال ایمیل پس از commit است. شکست SMTP داده ناقص نمی‌سازد و resend ممکن است. forgot/resend همیشه پاسخ عمومی یکسان دارند.

ProblemDetails/ValidationProblemDetails با code و traceId؛ ۴۰۰ validation، ۴۰۱ authentication، ۴۰۳ role، ۴۰۴ رکورد خارج scope، ۴۰۹ conflict، ۴۲۹ rate limit، ۵۰۰ خطای عمومی. لاگ درخواست حساس یا body/token فعال نیست؛ رخداد ارسال ناموفق بدون recipient/link ثبت می‌شود.

ADR-013: Swagger UI و OpenAPI JSON در همه محیط‌ها بدون توکن در دسترس‌اند تا قرارداد API روی سرور قابل مشاهده باشد؛ Swagger middleware پیش از fallback authorization اجرا می‌شود. endpointهای داده و حساب همچنان سیاست احراز هویت و نقش خود را دارند. تنظیمات اتصال و کلید امضا برای هر محیط جداگانه و خارج Git نگهداری می‌شوند.

ADR-014: طبق انتخاب صریح کاربر، `KnownProxies` و تنظیم IP proxy حذف شد. یک middleware کوچک در Production فقط مقدار دقیق و یکتای `X-Forwarded-Proto: https` را برای scheme درخواست اعمال می‌کند؛ سایر مقادیر نادیده گرفته می‌شوند. `CookieSecurePolicy.Always` حفظ شد. این سادگی به معنی امکان جعل هدر توسط هر اتصال مستقیم به پورت داخلی است؛ دسترسی به آن پورت باید در شبکه یا firewall به proxy محدود شود. برای عیب‌یابی 500، نوع exception، inner type، stack trace و نشانه‌های بولی HTTPS/forwarded proto با traceId و بدون متن exception یا داده درخواست لاگ می‌شوند.

ADR-015: شناسهٔ `Activity.Current.Id` در پاسخ ProblemDetails و لاگ خطای 500 یکسان است؛ در نبود Activity از `HttpContext.TraceIdentifier` استفاده می‌شود. جزئیات exception در پاسخ عمومی منتشر نمی‌شوند. برای خطای PostgreSQL فقط SQLSTATE به لاگ ساخت‌یافته اضافه می‌شود. connection string و signing key در تنظیمات تحت Git نگهداری نمی‌شوند؛ Compose آن‌ها را از محیط استقرار می‌گیرد.

موضوع باز محیطی: صحت Docker و SMTP واقعی محلی تا اجرای آن‌ها قابل تأیید نیست؛ [STATUS](STATUS.md). روش حفاظت کلیدها و TLS میزبان production باید هنگام تسک deployment تعیین شود. این موارد تصمیم پیاده‌شده تلقی نمی‌شوند.

توسعه آینده AI و فایل از قرارداد مصرف‌شده Application و implementation Infrastructure انجام می‌شود؛ اکنون abstraction یا جدول آن‌ها نداریم. جلسه آینده به snapshot سؤال و معیار نیاز دارد؛ engine نسخه‌بندی اکنون ساخته نمی‌شود.


## پوشه‌بندی پس از بازآرایی

طبق انتخاب صریح کاربر، این تغییر فقط سازمان‌دهی فایل‌هاست؛ مسیر Service → IApplicationDbContext حفظ شده و Repository جدید ساخته نشده است.

```text
src/
  HireYar.Api/
    Controllers/       # هر شش کنترلر
    DTOs/              # پاسخ‌های مخصوص HTTP
    Security/
    Errors/
  HireYar.Application/
    Services/          # Auth, Members, Organizations, JobPositions, InterviewTemplates, Dashboard
    DTOs/              # درخواست/پاسخ و validation بر اساس قابلیت
    Interfaces/        # IAccounts, ITeamMembers, ICurrentUser, IEmailSender, IApplicationDbContext
    Common/            # AppException, Page, PageQuery
  HireYar.Infrastructure/
    Services/          # Auth, Members, Email
    Auth/              # مدل Identity و تنظیمات امنیتی
    Members/           # مدل دعوت
    Persistence/       # DbContext، mapping و migrations
  HireYar.Domain/       # مدل‌های مستقل بر اساس قابلیت
```

ADR-011: پوشه‌ها بر اساس مسئولیت و سپس قابلیت مرتب شدند. namespaceهای موجود برای حفظ هویت typeها ثابت ماندند؛ نام پوشه الزاماً namespace نیست. API routeها، DI و مدل دیتابیس تغییر نکردند. سرویس/DTO/interface اکنون فایل مستقل دارند و AuthService دیگر داخل Contracts.cs نیست.


ADR-012: طبق درخواست کاربر، SQL Server با PostgreSQL و Npgsql EF Core 10.0.3 جایگزین شد؛ داده قبلی نیاز به انتقال ندارد. migration InitialPostgres مبنای دیتابیس خالی است. قفل user/organization در تراکنش Read Committed با SELECT FOR UPDATE تا commit حفظ می‌شود؛ خواندن توکن پس از گرفتن قفل انجام می‌شود. check constraint و partial indexها از شناسه‌های quoted PostgreSQL استفاده می‌کنند؛ FK مرکب و unique email حفظ شده‌اند.

RowVersion از نوع bytea و concurrency token مدیریت‌شده توسط AppDbContext است؛ هر insert/update موقعیت/قالب با Guid تصادفی جدید ۱۶بایتی ذخیره می‌شود. شرط UPDATE شامل نسخه اصلی است؛ stale version پاسخ 409 می‌دهد. استفاده از این روش نوع JSON قبلی Base64 را حفظ می‌کند؛ قرارداد طول از ۸ به ۱۶ بایت تغییر کرده است. تاریخ‌ها timestamptz و UTC هستند. خطای 23505 به conflict و 40P01/40001 به concurrency conflict نگاشت می‌شود.
