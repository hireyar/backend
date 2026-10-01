# Backlog

ادامهٔ 2026-10-01: vertical slice درخواست‌شده پیاده شد؛ HY-101 از منظر چرخه جلسه/توکن/API و HY-201 از منظر جریان صوتی/lease/fallback/cache اکنون Verified با ۹۴ تست روی PostgreSQL واقعی و provider آزمایشی‌اند. NetArz زنده هنوز به‌عنوان dependency محیط مقصد بررسی نشده است. فهرست «کار باقی‌مانده» در یادداشت شروع پایین تاریخی است؛ items مربوط به EF/API/storage/HttpClient/concurrency/retry این نوبت انجام شدند. گزارش HR شامل شواهد اولیه است؛ گزارش محصولی HY-301 یا frontend کامل ساخته نشده است.

پیگیری‌های عملیاتی همین قابلیت: smoke-test NetArz با کلید/مدل حساب مقصد، migration مقصد در تسک مجاز مستقل، volume خصوصی مشترک در صورت چند instance و سیاست retention/cleanup فایل orphan. duration واقعی صوت decode نمی‌شود و محدودیت فعلی در INTERVIEWS مستند است. ایمیل دعوت داوطلب ارسال نمی‌شود؛ response توکن و route را به caller معتبر HR می‌دهد. پیش‌تولید batch صوت و جلوگیری کامل از TTS هم‌زمان cache miss توسعهٔ بعدی‌اند؛ cache hit عادی اکنون provider را فراخوانی نمی‌کند.

به‌روزرسانی 2026-10-01: درخواست مستقل ماژول Candidate AI Interview دریافت و روی `codex/candidate-ai-interview` شروع شد. HY-101 و HY-201 اکنون InProgress هستند؛ وضعیت NotStarted در جدول قدیمی پایین مربوط به پیش از این درخواست است. هستهٔ مدل و policy و validation با ۲۴ تست جدید آماده است؛ قابلیت قابل استفاده از API هنوز وجود ندارد.

کار باقی‌ماندهٔ همین درخواست: مدل scoped CandidateApplication و Skill/PositionSkill، بانک سؤال قابل تنظیم و snapshot از قالب؛ EF mapping/FK مرکب/unique answer و concurrency و migration تولیدشده؛ سرویس‌های start/current/submit/retry/complete با commit کوتاه قبل و بعد AI؛ توکن دعوت اختصاصی جلسه و تفکیک از HR JWT؛ storage خصوصی و اعتبارسنجی صوت و دانلود مجاز؛ NetArz STT/LLM/TTS با options/timeouts/diagnostics؛ cache صوت با hash متن/model/voice/speed؛ endpointها و قرارداد browser؛ تست HttpClient، PostgreSQL و race/recovery؛ راهنمای تنظیمات و نمونه HTTP. HY-301 گزارش HR و frontend همچنان خارج کار این نوبت هستند.

| HY-014 | انتقال احراز هویت به موبایل و OTP | ۱ | InProgress | تصمیم مهاجرت حساب‌های ایمیلی/Recruiter، کلید و قالب کاوه‌نگار | حذف یا انتقال مسیرهای قدیمی، اجرای VerifyLookup واقعی، migration استقرار و تست انتهابه‌انتها |

پیاده‌سازی `otp/request`، `otp/verify`، migration و تست PostgreSQL انجام شد. `PUT /api/organization` موجود برای تکمیل نام شرکت استفاده می‌شود. تصمیم انتقال حساب‌های قبلی و دعوت اعضا هنوز باز است؛ مسیرهای قدیمی تا آن زمان باقی می‌مانند.

حالت کد ثابت برای Development افزوده و روی PostgreSQL آزمایش شد. تنظیم کلید و قالب واقعی کاوه‌نگار و بررسی ارسال واقعی همچنان در HY-014 باز است.

پیگیری موقت: پس از فراهم‌شدن SMTP، `Security:AutoConfirmEmailOnRegistration` را false کنید و ثبت‌نام/تأیید ایمیل را روی PostgreSQL واقعی دوباره بررسی کنید. تا آن زمان ایمیل حساب‌های تازه بدون اثبات مالکیت تأیید می‌شود.

وضعیت‌ها مطابق [STATUS](STATUS.md). تسک آینده نیازمند تعریف و درخواست مستقل است.

| شناسه | عنوان | مرحله | وضعیت | وابستگی | معیار پذیرش |
|---|---|---|---|---|---|
| HY-001 | ساختار Solution و persistence | ۱ | Verified | — | build و migration واقعی PostgreSQL موفق |
| HY-002 | حساب سازمان و نشست امن | ۱ | Verified | HY-001 | ثبت‌نام اتمیک، تأیید، login، rotation/replay/رقابت، reset/logout تست شوند |
| HY-003 | سازمان و تیم | ۱ | Verified | HY-002 | Owner/Recruiter، دعوت یک‌بارمصرف و scope سازمان تست شوند |
| HY-004 | موقعیت و قالب | ۱ | Verified | HY-001, HY-002 | CRUD دامنه، ترتیب و شناسه سؤال و RowVersion بررسی شوند |
| HY-005 | داشبورد و قرارداد HTTP | ۱ | Verified | HY-003, HY-004 | آمار واقعی scoped، cookie/CSRF/CORS و ProblemDetails |
| HY-006 | اجرای Docker و SMTP محلی | ۱ | Blocked | Docker Linux engine فعال؛ PostgreSQL:5433 | compose build/up، اعمال migration و دریافت سه نوع ایمیل در Mailpit؛ بدون ایمیل واقعی |
| HY-007 | بازبینی اتصال مصرف‌کننده | ۱ | NotStarted | HY-005 | UI مستندات و نمونه HTTP با cookie jar؛ یک مرورگر روی HTTPS یک سایت برای flow refresh/CSRF بررسی شود |
| HY-008 | مستندات تحویل | ۱ | Implemented | HY-001 تا HY-005 | README/AGENTS/PROJECT/ARCHITECTURE/STATUS/BACKLOG/HANDOFF با کد و تست هماهنگ |
| HY-101 | داوطلب، دعوت مصاحبه و چرخه جلسه | ۲ | NotStarted | پایان ۱ و تعریف تسک | مدل وضعیت و snapshot قالب طراحی و با معیار مستقل تأیید شود |
| HY-201 | مصاحبه صوتی نوبتی STT/LLM/TTS | ۳ | NotStarted | HY-101 و تعریف تسک | جریان صوتی و خطا/بازیابی با دامنه مستقل مشخص شود |
| HY-301 | ارزیابی و گزارش مستند HR | ۴ | NotStarted | HY-201 و تعریف تسک | گزارش شواهد قابل بررسی و تصمیم انسانی |
| HY-F01 | پنل Angular | مسیر جدا | NotStarted | API مرحله ۱ | قرارداد احراز هویت و API در تسک frontend مستقل مصرف شود |

نزدیک‌ترین کارها: HY-006 و سپس HY-007؛ نواقص واقعی مرحله اول بر توسعه قابلیت آینده اولویت دارند. فعال‌شدن Docker مانع HY-006 را رفع می‌کند. هیچ push، deploy یا تغییر production جزو این backlog به‌تنهایی مجاز نیست.
پیشرفت HY-007: خطای 401 UI مستندات رفع و redirect/HTML/CSS با تست HTTP تأیید شد؛ بررسی مرورگر HTTPS و جریان کامل نمونه‌ها همچنان باقی است.

راهنمای فارسی کوتاه شروع، عنوان فارسی همهٔ عملیات‌ها و ترتیب حساب جدید/موجود در Swagger اضافه و با تست OpenAPI بررسی شد. بررسی عملی جریان مرورگر HTTPS همچنان در HY-007 باز است.

تنظیمات محلی توسعه: JWT signing key و اتصال PostgreSQL در appsettings.Development.local.json خارج Git قرار گرفتند؛ اجرای واقعی Docker/SMTP و تست‌های PostgreSQL همچنان در HY-006 باز هستند.

| HY-009 | مرتب‌سازی Controllers/Services/DTOs/Interfaces | ۱ | Verified | HY-001 | build پس از جابه‌جایی موفق و دسترسی دیتابیس فعلی حفظ شود |

| HY-010 | تعیین تکلیف انتشار OpenAPI در Production | ۱ | Verified | درخواست دسترسی Swagger روی سرور | Swagger و OpenAPI عمومی در Production؛ تست مستقل دسترسی 200 و حفاظت API 401 موفق |

| HY-011 | انتقال از SQL Server به PostgreSQL | ۱ | Verified | تأیید عدم نیاز به داده قدیمی | provider، migration جدید، قفل و RowVersion، compose و ۲۳ تست مرتبط PostgreSQL موفق؛ شکست مستقل HY-010 پابرجاست |

| HY-012 | CSRF پشت reverse proxy | ۱ | Implemented | هدر `X-Forwarded-Proto: https` و محدودیت شبکه پورت داخلی در سرور | تست محلی Production با forwarded HTTPS پاسخ 200 و کوکی Secure؛ پس از انتشار پاسخ واقعی سرور بررسی شود |
| HY-013 | تشخیص 500 ثبت‌نام Production | ۱ | InProgress | لاگ همان درخواست و دسترسی سرور | همسان‌سازی traceId لاگ و پاسخ و حذف secretهای تنظیمات Git انجام شد؛ علت با exception و SQLSTATE سرور، اتصال PostgreSQL و migration تأیید شود |
