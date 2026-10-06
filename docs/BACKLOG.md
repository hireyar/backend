# Backlog

## HY-018 — بانک سؤال موضوعی پیشرفته (2026-10-06): Verified محلی

پیاده‌سازی backend پیشنهاد AI از JD، تولید کل/یک موضوع، ذخیره بانک سه‌سطحی کامل ۵ تا ۱۰ موضوع، ویرایش/حذف پیش‌نویس از PUT کامل، snapshot جلسه/کپی قالب و جلوگیری تکرار موضوع انجام شده؛ معمولی محفوظ است. [ADVANCED_QUESTIONS_API](ADVANCED_QUESTIONS_API.md). باقی تحویل خارج ریپو: Angular لیست/آکاردئون، loading/retry، تأیید و counters؛ اجرای migration و تست AI واقعی در محیط مقصد توسط مالک محیط. این باقی‌مانده مجوز push/deploy یا تماس واقعی provider نیست. build بدون warning/error، ۱۷۰ تست موفق بدون Skip، EF بدون drift و migration idempotent دوباره روی داده legacy مصنوعی تأیید شدند.


## HY-017 — حساب تک‌کاربر و حذف پنل (2026-10-06): Verified محلی

ورود فقط OTP، حذف API ایمیل/رمز/تیم، DB constraint Owner فعال واحد، بازنشسته‌کردن دسترسی تیم تاریخی و DELETE موقعیت/قالب/مصاحبه با حفظ history/مالی/lease انجام شد. ۱۶۰ تست PostgreSQL واقعی و build موفق؛ [PANEL_CLEANUP_API](PANEL_CLEANUP_API.md) قرارداد فرانت. حذف سؤال از قالب قبلاً با PUT وجود داشت و حفظ شد. مدل چند Recruiter در یادداشت‌های قدیمی دیگر قرارداد محصول نیست.

باقی مقصد: اتصال Owner ایمیلی فاقد شماره به موبایل معتبر همان حساب، اجرای migration تازه، حذف صفحات ایمیل/تیم و دکمه DELETE در Angular، اصلاح entitlement ویزارد گزارش‌شده در مرورگر. این موارد در مقصد/ریپوی فرانت اجرا نشدند؛ backlog مجوز اجرای خودکار قابلیت دیگر نیست.

## خطای 500 پنل و داشبورد مقصد — 2026-10-06: تشخیص مقصد باز است

build و ۱۷ تست مرتبط PostgreSQL محلی موفق‌اند؛ وابستگی هر دو مسیر به migrationهای AddPanelWizard و AddSubscriptionCreditPanel تأیید شد، ولی اجرای آن‌ها در مقصد معلوم نیست. [Check-PanelSchema.sql](../scripts/Check-PanelSchema.sql) تشخیص فقط‌خواندنی schema است و روی DB قدیمی/جدید مصنوعی بررسی شد. دریافت لاگ traceId گزارش‌شده و نتیجه این اسکریپت از DB واقعی API، سپس اصلاح بر اساس علت تأییدشده باقی است؛ این یادداشت مجوز تغییر production نیست.

## HY-016 — API اشتراک و اعتبار HTML (2026-10-05): Verified

روی `codex/subscription-credit-panel` از panel-wizard، overview/زمان از اعتبار آزاد، quote/checkout با tax و discount و کنترل stale summary، ارتقای روزشمار با cutover append-only، فیلتر/جمع یکپارچه سوابق و invoice JSON پیاده شدند. Swagger فارسی، scope/Owner و snapshot مالی حفظ‌اند؛ ۱۶۱ تست PostgreSQL واقعی موفق، شش تست جدید و migration/script دوباره با داده قدیمی مصنوعی بررسی شدند. [SUBSCRIPTION_CREDIT_API](SUBSCRIPTION_CREDIT_API.md) قرارداد فرانت است. فرض کاری ارتقا دوره کامل جدید با کسر روزهای کامل مانده است؛ صف آینده یا pending باید پیش از upgrade حل شود. صرف expiry نمایش quote مجوز نادیده‌گرفتن پول احتمالی نیست.

باقی‌مانده عملیاتی: adapter و recovery/reconciliation درگاه واقعی (HY-403)، اعمال migration و تنظیم tax مقصد، PDF/فاکتور بیرونی و frontend در ریپوی خودش. این‌ها در این نوبت اجرا نشده‌اند؛ gateway runtime همچنان Disabled و checkout جدید 503 است. این backlog مجوز push/deploy یا شروع خودکار قابلیت بعدی نیست. HY-015 و متن «اشتراک دست‌نخورده» زیر مربوط به نوبت پیشین‌اند.

## HY-015 — تطبیق API با پنل ویزاردی (2026-10-05): Verified

درخواست مستقل HTML پنل انجام شد: API ساده موقعیت و قالب، دعوت multipart با رزومه و انتخاب مستقل نوع، snapshot، فهرست/فیلتر/metadata، resend/cancel، دانلود scoped و داشبورد. راهنمای [PANEL_WIZARD_API](PANEL_WIZARD_API.md)، migration جدید و ۱۵۵ تست موفق واقعی PostgreSQL (چهار تست جدید ویزارد) آماده‌اند؛ Swagger فارسی حفظ است. اشتراک و اعتبار طبق خواست کاربر دست‌نخورده‌اند. این تکمیل frontend یا تمام امکانات HTML نیست: integration ارسال ایمیل/SMS با لینک فرانت، ویدیو، تنظیمات مدت سازمانی و migration محیط مقصد جداگانه باقی‌اند؛ backlog مجوز اجرای خودکارشان نیست. پاسخ API نبود delivery/video را صریح می‌گوید. فرم جدید نیاز به ساخت دستی مهارت/بانک موقعیت ندارد؛ محدودیت قدیمی راهنمای HR برای این مسیر superseded است.

پیشرفت HY-404: اسکریپت دستی اعطای Advanced و اعتبار تست همکاران در `scripts/Grant-ColleagueTestAccess.sql` با ledger و idempotency روی PostgreSQL واقعی بررسی شد. اجرای مقصد و provisioning عملیاتی همچنان انجام نشده‌اند؛ این اعطای غیرنقدی با provider داخلی باید از درآمد نقدی مستثنا شود.

## Pricing — نتیجه 2026-10-01

| شناسه | عنوان | وضعیت | شواهد/باقی‌مانده |
|---|---|---|---|
| HY-401 | کاتالوگ پویا، snapshot، اشتراک و API billing/admin | Verified | PostgreSQL واقعی، tenant scope، کاتالوگ و snapshot، queued purchase و حداقل مرز SystemAdmin |
| HY-402 | کیف پول، ledger، رزرو و billing مصاحبه | Verified | race/idempotency/constraint، نرخ و capability snapshot، زمان سرور، cancellation/expiry، retention؛ جزء مجموعهٔ ۱۵۱ تست موفق |
| HY-403 | adapter پرداخت واقعی | NotImplemented | IPaymentGateway + pending record آماده؛ Disabled runtime. adapter، verification/recovery/reconciliation واقعی و تست sandbox provider لازم‌اند |
| HY-404 | آماده‌سازی عملیاتی Billing | NotStarted | provisioning مدیر، محصولات اعتبار، اشتراک/اعتبار اولیه، migration مقصد، پایان جلسات legacy، backup/cleanup رسانه و rotation کلید قدیمی |

قواعد و محدودیت‌های پذیرفته‌شده در [BILLING](BILLING.md): اندازه‌گیری window سرور با سقف ۱۲۰ ثانیه هر پاسخ، lazy expiry reservation، وضعیت اشتراک time-derived، قیمت IRT و accounting USD. precision decoder/capture و automation cleanup نیازمند تسک جدا هستند. proration، refund provider، forex، invoice/tax و Angular جزو این درخواست اجرا نشدند. این backlog مجوز push/deploy یا شروع خودکار کار آینده نیست.

audit پیش از staging 2026-10-01: [INTERVIEW_AUDIT](INTERVIEW_AUDIT.md) مرجع نتیجه فعلی است. IOException provider و خطای خواندن cache اصلاح شدند؛ secret default حذف و local settings حفظ شد؛ شکست تست الزام کلید رفع شد. تست کامل ۱۱۸/۱۱۸ با PostgreSQL واقعی و smoke زنده TTS/STT/LLM موفق‌اند. نیاز به smoke اولیه NetArz که در یادداشت قبلی باز بود، اکنون انجام شد؛ آزمون ظرفیت/کیفیت طولانی‌مدت همچنان انجام نشده است. schema/API بدون تغییر و migration تازه لازم نیست. کار باقی‌مانده واقعی: rotation کلید قبلی پیش از staging، secret استقرار خارج Git، volume/retention/orphan cleanup/backup و تنظیم timeout proxy. قابلیت آینده‌ای خودکار فعال یا اضافه نشد.

تحویل فرانت‌اند 2026-10-01: قرارداد و پرامپت اجرای حداقلی در [FRONTEND_INTERVIEWS_HANDOFF](FRONTEND_INTERVIEWS_HANDOFF.md) آماده است. پیاده‌سازی فرانت همچنان در ریپوی خودش انجام می‌شود؛ این مستند تکمیل HY-301 یا مجوز توسعه endpointهای فهرست جدید نیست. کمبود APIهای فهرست در راهنما صریح ثبت شده است.

بررسی همین تحویل: تست الزام کلید NetArz شکست دارد چون default فعلی ApiKey خالی نیست. بازگرداندن default خالی و تأمین کلید خارج Git نیاز به اصلاح مستقل دارد؛ مقدار موجود در مستند/خروجی ثبت نشد. نتیجه فعلی تست مرتبط ۵۸ موفق/۱ شکست است و نتیجه تاریخی ۹۴ موفق را نباید نتیجه checkout فعلی دانست.

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

## تنظیمات AI در appsettings — 2026-10-02

روی برنچ `codex/ai-appsettings` از `dev`، بخش `AI` در `src/HireYar.Api/appsettings.json` اضافه شد: Provider=NetArz، BaseUrl، مدل‌های STT/LLM/TTS، voice، speed و سه timeout. binding موجود `AI:NetArz` و انتخاب `AI:Provider` همین تنظیمات را مصرف می‌کنند؛ override محیطی/local همچنان اولویت دارد. ApiKey در محل فعلی و تمام فایل‌های کلید دست‌نخورده ماند؛ کلید تازه در appsettings اضافه یا کپی نشد. یادداشت‌های تاریخی Disabled برای پیش‌فرض مشترک دیگر وضعیت جاری این برنچ نیستند.

Build موفق با صفر warning/error؛ تست‌های InterviewProviderTests و InterviewAudioTests: ۴۳ موفق، صفر شکست/Skip. تست کلید خالی اکنون مقدار خالی را صریحاً فقط در fixture خود می‌دهد تا وابسته به default فعلی نباشد. تماس واقعی AI، تست PostgreSQL، migration، commit، push و deploy انجام نشدند.
