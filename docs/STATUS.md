# وضعیت جاری

## رفع 500 ذخیره رزومه دعوت — 2026-10-06

لاگ مقصد برای `POST /api/panel-wizard/invitations` با traceId `00-84eab4a04b430563040f0607ad411f60-8e12eee948539082-00` علت را تأیید کرد: UnauthorizedAccessException در Directory.CreateDirectory داخل LocalInterviewAudioStorage.SaveAsync؛ SQLSTATE خالی است. Dockerfile قبلی فقط /keys را برای APP_UID آماده می‌کرد، نه storage پیش‌فرض زیر /app. روی dev پوشه خصوصی /app/storage/interviews با مالکیت APP_UID و مجوز 700 و volume پایدار interview-storage در هر دو Compose اضافه شد؛ مسیر storage صریح است. schema/runtime API تغییر نکرد.

build صفر warning/error؛ ۲۱ تست مرتبط PanelWizardTests، InterviewAudioTests و AdvancedQuestionTests روی PostgreSQL 18 محلی اختصاصی موفق، صفر شکست/Skip؛ `tests/HireYar.Tests/TestResults/invitations-storage-fix.trx`. هر دو Compose با config --quiet معتبرند (development با مقادیر مصنوعی). Docker daemon در محیط موجود نیست؛ build/run image و مالکیت volume روی Linux هنوز تأیید نشده‌اند. دیتابیس‌های fixture حذف شدند. رفع مقصد نیازمند rebuild/recreate مجاز و بررسی مسیر/مالکیت volume است؛ push/deploy یا تغییر production انجام نشده. فایل قبلی احتمالی پیش از mount تازه باید با حفظ keyهای نسبی منتقل شود؛ [INTERVIEWS](INTERVIEWS.md).

## سؤال‌های پیشرفته مطابق HTML — 2026-10-06: Verified محلی

روی dev: پیشنهاد کل بانک/بازسازی یک موضوع از JD scoped، سه سطح در ۵ تا ۱۰ موضوع، تأیید/ویرایش ویزارد با RowVersion، شمارنده‌های بانک/مصاحبه، snapshot و یک پرسش در هر موضوع اضافه شدند. معمولی و قالب‌های تاریخی محفوظ؛ entitlement در تولید/ذخیره/دعوت/شروع بررسی می‌شود. migration `20261006110718_AddAdvancedTopicQuestions` در مقصد لازم است؛ Angular در ریپو نیست. [ADVANCED_QUESTIONS_API](ADVANCED_QUESTIONS_API.md). restore و tool restore موفق؛ build صفر warning/error؛ **۱۷۰/۱۷۰ تست موفق، صفر شکست و صفر Skip** روی PostgreSQL 18 محلی، شامل ۶ تست جدید موضوعی و ۴ تست adapter پیشنهاد AI. `tests/HireYar.Tests/TestResults/advanced-questions-final.trx`. مسیر کامل ۱۵ سؤال بانک → ۵ پاسخ/پنج موضوع → Completed، scope، تأیید/ویرایش و stale version، Basic، خروجی خراب/timeout و افت اشتراک پیشرفته به معمولی پیش از start تأیید شدند. EF بدون drift؛ اسکریپت idempotent دو بار روی baseline قبلی با قالب/سؤال/جلسه مصنوعی اجرا شد و legacy/constraint حفظ شدند. دیتابیس تصادفی حذف شد. AI/پرداخت واقعی، push/deploy و DB مقصد تغییر نکرده‌اند.


## پاک‌سازی حساب و حذف پنل — 2026-10-06: Verified محلی

روی dev، تغییرات مستندی و اسکریپت تشخیص قبلی حفظ شدند. APIهای ایمیل/رمز و تیم و SMTP/Mailpit حذف شدند؛ حساب فعال فقط Owner با موبایل تأییدشده، حداکثر یک حساب فعال در هر سازمان. DELETE موقعیت/قالب/مصاحبه با حذف منطقی، scope، حفظ تاریخچه، منع دعوت تازه و تسویه مصرف/آزادسازی رزرو اضافه شد. [PANEL_CLEANUP_API](PANEL_CLEANUP_API.md) تحویل فرانت و شرایط انتقال Owner ایمیلی است. Angular/production تغییر نکردند؛ باگ entitlement ویزارد مشاهده‌شده در مرورگر هنوز اصلاح فرانت می‌خواهد و علت API آن این نوبت ادعا نشده است.

restore/tool restore موفق؛ build صفر warning/error؛ **۱۶۰/۱۶۰ تست PostgreSQL 18 محلی موفق و صفر Skip**، شامل ۱۰ تست cleanup و انتقال fixture از ایمیل به OTP. EF بدون drift؛ script idempotent روی baseline قبلی با Owner/Recruiter/refresh/دعوت مصنوعی دو بار موفق و حفظ هویت/ابطال دسترسی تیم تأیید شد. `tests/HireYar.Tests/TestResults/panel-cleanup-final.trx`. migration `20261006071854_SimplifyAccountsAndPanelDeletion` در مقصد لازم است. پیامک/AI/پرداخت واقعی، push/deploy یا تغییر DB مقصد انجام نشد.

## بررسی 500 پنل و داشبورد مقصد — 2026-10-06: در انتظار شواهد سرور

روی `dev` پاک، گزارش `unexpected_error` برای `/api/billing/panel` و `/api/dashboard` بررسی شد. هر دو مسیر وابسته به schema جدیدند: جدول SubscriptionReplacements در مالی و ستون‌های snapshot/ResumeStorageKey در داشبورد. Program/Docker migration خودکار ندارند؛ نبود migration محتمل است ولی بدون لاگ traceId و وضعیت schema مقصد علت قطعی نیست. build صفر warning/error و ۱۷ تست مرتبط PostgreSQL واقعی بدون Skip موفق شدند؛ `tests/HireYar.Tests/TestResults/panel-errors-diagnostic.trx`. اسکریپت فقط‌خواندنی [Check-PanelSchema.sql](../scripts/Check-PanelSchema.sql) روی baseline قبل از دو migration، اشیای missing و پس از migration همه OK را نشان داد؛ DB مصنوعی حذف شد. کد runtime تغییر نکرد؛ production/push/deploy انجام نشد. برای ادامه، ExceptionType/SQLSTATE/StackTrace همان درخواست و نتیجه schema check از همان DB/search_path API لازم است.

## اشتراک و اعتبار مطابق پنل — 2026-10-05

برنچ `codex/subscription-credit-panel` از `panel-wizard` پاک ساخته شد؛ APIهای چهار تب فایل `subscription_credit.html` آماده‌اند: `/api/billing/panel` وضعیت/صف/انقضا و wallet/rate/زمان تخمینی از اعتبار آزاد؛ quote/checkout با discount، tax تنظیمی پیش‌فرض ۱۰٪، fingerprint کنترل تغییر خلاصه و ارتقای روزشمار؛ financial-history با فیلتر نوع/تاریخ و totals مستقل از page؛ invoice JSON از پرداخت Paid scoped. owner خرید می‌کند، recruiter فقط مشاهده/preview. Swagger فارسی و مسیرهای قدیمی حفظ شدند؛ محاسبه مدت محدود سرور و billing جلسات قبلی تغییر نکرد.

ارتقا طبق فرض کاری «دوره کامل جدید با کسر ارزش روزهای کامل باقی‌مانده» با SubscriptionReplacement append-only و transaction/tenant FK/unique/trigger انجام می‌شود؛ تاریخچه قبلی بازنویسی نمی‌شود. صف آینده یا پرداخت Pending پیش از ارتقا باید حل شود. quote و read با runtime Disabled فعال‌اند ولی checkout جدید 503 است؛ درگاه واقعی اضافه نشده. migration `20261005133142_AddSubscriptionCreditPanel` دوره‌های seed دست‌نخورده ۳/۶ماهه را مطابق تخفیف ۵/۱۰٪ HTML تنظیم می‌کند، قیمت ویرایش‌شده مدیر و snapshotهای قبلی محفوظ‌اند. [SUBSCRIPTION_CREDIT_API](SUBSCRIPTION_CREDIT_API.md) قرارداد جدید فرانت است. نکات «اشتراک دست‌نخورده» در تحویل ویزارد پایین مربوط به محدوده نوبت قبلی‌اند.

بررسی واقعی: tool restore/restore موفق، build صفر warning/error؛ **۱۶۱ Passed، صفر Failed، صفر Skipped** روی PostgreSQL 18 مستقل loopback:55453 (شش تست تازه مالی پنل، provider جعلی). TRX نهایی `TestResults/subscription-credit-final.trx`. تست quote/tax/discount/invoice/renewal، upgrade هم‌زمان و immutable/cutover، failure/صف، عدم تکرار خرید credit، PaidAtUtc و فیلتر/totals، reservation estimate و مدت/نام داوطلب و gateway Disabled موفق‌اند. EF بدون pending changes؛ script idempotent دو بار روی baseline PanelWizard با خرید/اشتراک مصنوعی قدیمی و قیمت ویرایش‌شده مدیر موفق بود؛ operation backfill، مبلغ/tax قبلی و حفاظت قیمت مدیر بررسی شد. DBهای تصادفی حذف و cluster اختصاصی پس از بررسی متوقف شد. Angular، PDF فاکتور، پرداخت/AI واقعی، migration مقصد، commit/push/deploy و تغییر production انجام نشده‌اند.

## پنل ویزاردی — 2026-10-05

روی `dev` از Git پاک، براساس `panel_v5.html` ارسالی APIهای ساده ساخت/ویرایش موقعیت و قالب متنی و دعوت multipart در `api/panel-wizard` اضافه شدند. انتخاب نوع عمومی/تخصصی مستقل از قالب، حداقل پنج سؤال، ترتیب و پیام‌های شروع/پایان، اطلاعات کارجو و رزومه خصوصی تا ۱۰ MiB آماده‌اند. فهرست مصاحبه‌ها با جستجو/چند وضعیت/موقعیت/بازه تاریخ، metadata و درصد مبتنی بر evidence، صدور مجدد با ابطال توکن قدیمی، لغو، دانلود scoped رزومه و آمار/آخرین مصاحبه‌های داشبورد اضافه شدند. سؤال و شرح شغل و پیام‌ها هنگام دعوت snapshot می‌شوند؛ HR جدید نیاز به Skill/PositionSkill/BankQuestion ندارد. قالب همان سازمان برای موقعیت دیگر در دعوت کپی می‌شود تا FK پذیرفته‌شده حفظ شود.

قرارداد فرانت جدید: [PANEL_WIZARD_API](PANEL_WIZARD_API.md). مسیرهای قدیمی و همه عنوان‌های فارسی Swagger حفظ شده‌اند. subscription/wallet/pricing و سیاست مصرف اعتبار تغییری ندارند. Angular، ارسال واقعی ایمیل/SMS، ویدیو و تنظیمات مدت سازمانی اجرا نشده‌اند؛ delivery برابر NotConfigured و videoAvailable=false است تا UI موفقیت ارسال یا وجود ویدیو را ادعا نکند.

اعتبارسنجی واقعی: restore موفق؛ ابزار EF پس از خطای feed موجود با config موقت منبع عمومی بازیابی شد. build صفر warning/error؛ **۱۵۵ Passed، صفر Failed، صفر Skipped** با PostgreSQL 18 مستقل loopback:55451 و provider آزمایشی؛ TRX در `TestResults/panel-wizard-final.trx`. چهار تست جدید ویزارد، flow بدون ساخت مهارت، snapshot/ترتیب/پیام، stale version، scope، file/type/phone validation، reuse قالب، resend/expiry/cancel و entitlement عمومی/تخصصی را پوشش می‌دهند. تست فارسی OpenAPI هم موفق است. EF بدون pending model changes؛ migration `20261005130515_AddPanelWizard` و script idempotent با baseline قبلی و داده مصنوعی، دو بار موفق؛ backfill general و پیام‌ها بررسی شد. دیتابیس‌های تصادفی حذف و cluster مستقل تست پس از بررسی متوقف شد. migration مقصد، commit، push و deploy انجام نشده‌اند.

اسکریپت دسترسی همکاران برای تست: [Grant-ColleagueTestAccess.sql](../scripts/Grant-ColleagueTestAccess.sql) به سازمان‌های دارای کاربر فعال، پیش‌فرض ۱۰۰ USD اعتبار PromotionalCredit و Advanced یک‌ماهه می‌دهد. اجرای مجدد campaign تکراری شارژ نمی‌کند؛ Advanced فعال موجود حفظ می‌شود، اشتراک متداخل دیگر باعث rollback کامل است. سند پرداخت `InternalTestGrant` صرفاً اعطای غیرنقدی با مبلغ اسمی کاتالوگ است و باید از گزارش درآمد نقدی کنار گذاشته شود. روی DB کاربران اجرا نشده؛ PostgreSQL 18 با سه کاربر مصنوعی/دو سازمان، اجرای تکراری و حفظ موجودی پس از مصرف موفق؛ build صفر warning/error.

## Pricing و Billing — 2026-10-01

برنچ `codex/pricing` از وضعیت پاک با fast-forward به `origin/dev` در `d9d4d72` به‌روز شد؛ مصاحبهٔ موجود نیز در همین برنچ است. پکیج/دوره/feature پویا، snapshot قرارداد خرید، اشتراک صف‌شده بدون از دست‌رفتن زمان، AI wallet/ledger، رزرو حداکثر هزینه، settlement یک‌باره، API عمومی/admin/organization، مرز SystemAdmin و IPaymentGateway پیاده شدند. currency IRT/USD و مدت مالی window محدود سرور مستقل‌اند؛ راهنمای معتبر [BILLING](BILLING.md).

وضعیت: رفتار داخلی مالی با PostgreSQL **Verified**؛ درگاه واقعی و آمادگی پرداخت production **NotImplemented**. runtime provider غیرفعال است؛ pending quote ساخته می‌شود ولی verification برابر 503 و fulfillment مالی انجام نمی‌شود. محصولات اعتبار باید با مقدار و قیمت صریح توسط مدیر ایجاد شوند. فایل‌ها بر اساس retention snapshot از download خارج می‌شوند؛ حذف فیزیکی/backup/volume آماده‌سازی عملیاتی‌اند.

نتیجهٔ واقعی این نوبت:

- `dotnet tool restore` و `dotnet restore HireYar.slnx`: موفق پس از دسترسی به NuGet.Config؛ هیچ package/framework جدید اضافه نشد.
- `dotnet build HireYar.slnx --no-restore`: موفق، صفر warning/error؛ EF `has-pending-model-changes --no-build`: بدون اختلاف مدل.
- `dotnet test HireYar.slnx --no-restore` با PostgreSQL 18 واقعی مستقل loopback:55447: **۱۵۱ Passed، صفر Failed، صفر Skipped**؛ TRX: `TestResults/billing-complete.trx`.
- ۳۳ تست جدید: ۱۰ math case و ۲۳ تست PostgreSQL، شامل callback هم‌زمان، provider transaction تکراری، رزرو race، ledger/tenant constraint، snapshot، expiry، basic capabilities، rounding، retention و provider غیرفعال. تست‌های مصاحبهٔ موجود با خرید/اعتبار آزمایشی صریح ادامه یافتند؛ auto-funding runtime اضافه نشد.
- migration `20261001080208_AddPricingBilling` تولید شد؛ precision، unique، FK مرکب، Restrict و backfill بررسی شدند. script idempotent در `artifacts/billing-migrations.sql` دو بار روی DB تصادفی مستقل اعمال شد؛ هر دو موفق، کاتالوگ دو پکیج و هشت دوره داشت؛ DB بررسی حذف شد.
- constraint triggerهای deferred، wallet بدون ledger و رزرو بیش از موجودی را در commit رد می‌کنند. ثبت دورهٔ جدید، ساخت lazy wallet و limiter partition در جریان تست اصلاح شدند. snapshot پکیج در یک statement خوانده می‌شود تا split query قرارداد ترکیبی نسازد.

دیتابیس تصادفی script و تمام دیتابیس‌های fixture حذف شدند؛ count نهایی صفر بود. cluster آزمایشی مستقل همین نوبت متوقف شد. `git diff --check` بدون ایراد whitespace است.

اختلاف با یادداشت audit قبلی بررسی شد: source دریافتی NetArzOptions هنوز ApiKey غیرخالی داشت؛ default این نوبت خالی شد و تست الزام کلید موفق است. هیچ AI یا درگاه واقعی فراخوانی نشد؛ کلید محلی دست‌نخورده است. rotation کلید قبلاً ثبت‌شده در Git همچنان عملیاتی لازم است. push/deploy و تغییر production انجام نشده؛ تغییرات این نوبت commit نشده‌اند. بخش‌های تاریخی پایین مرجع وضعیت Pricing نیستند.

## audit پیش از staging — 2026-10-01

نتیجه فعلی در [INTERVIEW_AUDIT](INTERVIEW_AUDIT.md): دو نقص مسیر خطا (قطع response stream provider و خواندن cache صوت) اصلاح شدند؛ ApiKey از default فایل تحت Git به تنظیمات محلی ignored منتقل شد و تست الزام کلید موفق است. NetArz فقط در Development local فعال شد؛ endpoint/قرارداد/schema تغییر نکرد. build صفر warning/error؛ **۱۱۸ تست موفق، صفر شکست/Skip** با PostgreSQL 18 واقعی مستقل. EF بدون pending model changes. smoke واقعی TTS → STT → LLM با داده مصنوعی و decision معتبر موفق شد؛ گزارش‌های «NetArz آزمایش نشده» و «تست کلید شکست دارد» پایین تاریخی‌اند. rotation کلید قبلاً ثبت‌شده در Git پیش از staging هنوز تأیید نشده؛ volume/retention/backup عملیاتی باقی‌اند. migration مقصد طبق گفته کاربر قبلاً اجرا شده و در این audit تغییر نکرد.

## تحویل قرارداد فرانت‌اند — 2026-10-01

راهنمای مستقل [FRONTEND_INTERVIEWS_HANDOFF](FRONTEND_INTERVIEWS_HANDOFF.md) بر اساس DTOها و Controllerهای فعلی اضافه شد: جریان داوطلب، فرم حداقلی HR، گزارش، صوت خصوصی، retry، محدودیت APIها و پرامپت آماده Codex. این مرحله فقط مستندات است؛ فرانت‌اند، API یا دیتابیس تغییر نکرد. اعتبارسنجی این مرحله در HANDOFF ثبت می‌شود؛ نتیجه ۹۴ تست بخش قبلی مربوط به پیاده‌سازی بک‌اند است.

## جریان قابل اجرای مصاحبه — ادامه 2026-10-01

این بخش جایگزین محدودیت‌های «شروع ماژول» پایین است. مسیر کامل HR invitation → candidate token → start/current → private audio → STT → exact shortlist/LLM → evidence/fallback → next question/complete پیاده و روی PostgreSQL واقعی با provider جعلی آزمایش شد. retry همان پاسخ، lease/recovery، schema tenant FK، unique answer و optimistic concurrency، download خصوصی و TTS cache برقرارند. مدل‌ها دیگر صرفاً حافظه‌ای نیستند؛ تمام stateها entity رابطه‌ای مستقل با explicit mapping و Restrict هستند. جزئیات قرارداد و تنظیمات در [INTERVIEWS](INTERVIEWS.md).

Migration جدید `20261001061608_AddCandidateInterviewFlow` تولید و روی DB تصادفی fixture PostgreSQL 18 اعمال شد؛ migration قبلی یا جدول‌های قبلی recreate نشدند. EF has-pending-model-changes بدون اختلاف. build صفر warning/error؛ کل تست‌ها **۹۴ موفق، صفر شکست، صفر Skip** با HIREYAR_TEST_POSTGRES روی cluster آزمایشی مستقل localhost:55441. HTTPهای واقعی AI در suite فراخوانی نشده‌اند؛ NetArz با mocked handler بررسی شده و پیاده‌سازی واقعی HttpClientFactory آمادهٔ تنظیم است. کلید واقعی و smoke-test حساب NetArz، volume/retention خصوصی فایل‌ها و اعمال migration در محیط مقصد هنوز لازم‌اند. push/deploy/commit انجام نشده است.

## شروع ماژول مصاحبه داوطلب — 2026-10-01

طبق درخواست مستقل کاربر، پرامپت Candidate AI Interview روی برنچ `codex/candidate-ai-interview` از `dev` شروع شد؛ Git در شروع پاک بود. فقط هستهٔ Domain/Application این نوبت پیاده شده و ماژول کامل یا production-ready نیست.

- مدل جلسه با وضعیت Created/WaitingForAnswer/ProcessingAnswer/Completed/Cancelled؛ پاسخ مستقل با Uploaded/Transcribing/Transcribed/Evaluating/Processed/Failed، retry همان پاسخ و حفظ صوت و transcript.
- snapshot مستقل سؤال و مهارت، state مهارت، evidence با delta واقعی پس از clamp و coverage موضوع به تفکیک مهارت؛ امتیاز و confidence در بازه ۰..۱.
- سیاست shortlist بین ۵ تا ۲۰، حذف سؤال پرسیده‌شده/غیرفعال/موضوع پوشش‌یافته، اولویت کمبود evidence، required/priority و difficulty؛ fallback قطعی.
- DTO سخت‌گیر خروجی AI، validation و fallback بدون تغییر assessment نامعتبر؛ prompt نسخه v1. پیشنهاد تکمیل AI شرط پایان را تغییر نمی‌دهد.
- restore موفق (دسترسی NuGet.Config با escalation)، build صفر warning/error؛ `dotnet test HireYar.slnx --no-restore`: ۲۶ موفق، صفر شکست، ۲۷ Skip بدون HIREYAR_TEST_POSTGRES. ۲۴ مورد تست جدید Domain/Application موفق‌اند؛ این شواهد اثبات رفتار PostgreSQL نیستند.

مدل‌ها هنوز به EF متصل نیستند؛ جدول یا migration جدید، API، candidate access token، storage، NetArz HttpClient، observability و تست provider/دیتابیس اضافه نشده‌اند. فراخوانی واقعی AI یا تغییر دیتابیس صورت نگرفت. push/deploy/commit انجام نشد.

به‌روزرسانی 2026-09-27: `Kavenegar` در appsettings تعریف شد؛ `ApiKey`/`Template` خالی، `UseFixedOtp=false` در تنظیمات مشترک و کد ثابت `123456` با فلگ true فقط در Development. حالت ثابت SMS را کامل رد می‌کند و از همان challenge و محدودیت‌های OTP استفاده می‌کند. تست جدید آن روی PostgreSQL 18 اجرا شد؛ نتیجهٔ کل مجموعه ۲۹/۲۹ موفق، build صفر warning/error. ارسال واقعی کاوه‌نگار همچنان بدون کلید و قالب بررسی نشده است.

تغییر 2026-09-27: ورود یکپارچه با موبایل و OTP در `otp/request` و `otp/verify` پیاده شد؛ نبود کاربر باعث ساخت Owner و سازمان اولیه می‌شود. اطلاعات شرکت با `PUT /api/organization` تکمیل می‌شود. VerifyLookup کاوه‌نگار با تنظیمات محیطی و بدون ثبت کلید/کد در لاگ اضافه شد. migration `AddMobileOtp` و تست جدید روی PostgreSQL 18 محلی اجرا شد؛ مجموعهٔ کامل ۲۸/۲۸ موفق، build بدون warning/error، و EF `has-pending-model-changes` بدون اختلاف بود. ارسال واقعی کاوه‌نگار به علت نبود کلید و قالب آزمایش نشده است. مسیرهای ایمیل/رمز و دعوت Recruiter هنوز فعال‌اند تا تصمیم مهاجرت حساب‌های فعلی گرفته شود؛ بنابراین جایگزینی کامل هنوز انجام نشده است. به دیتابیس استقرار migration اعمال نشده و push/deploy انجام نشده است.

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

یادداشت اجرای نهایی مصاحبه: یک run با خطای transport اتصال PostgreSQL شکست خورد؛ اجرای مجدد با SSL Mode=Disable و GSS Encryption Mode=Disable صرفاً روی cluster محلی آزمایشی، ۹۴/۹۴ موفق بود. build نهایی صفر warning/error؛ DB تصادفی تست باقی نماند و cluster آزمایشی متوقف شد. اسکریپت idempotent در artifacts/candidate-interview-migrations.sql تولید شد ولی این اسکریپت جداگانه اجرا نشد؛ اعمال migration از fixture واقعی PostgreSQL بررسی شد.

## تنظیمات AI در appsettings — 2026-10-02

روی برنچ `codex/ai-appsettings` از `dev`، بخش `AI` در `src/HireYar.Api/appsettings.json` اضافه شد: Provider=NetArz، BaseUrl، مدل‌های STT/LLM/TTS، voice، speed و سه timeout. binding موجود `AI:NetArz` و انتخاب `AI:Provider` همین تنظیمات را مصرف می‌کنند؛ override محیطی/local همچنان اولویت دارد. ApiKey در محل فعلی و تمام فایل‌های کلید دست‌نخورده ماند؛ کلید تازه در appsettings اضافه یا کپی نشد. یادداشت‌های تاریخی Disabled برای پیش‌فرض مشترک دیگر وضعیت جاری این برنچ نیستند.

Build موفق با صفر warning/error؛ تست‌های InterviewProviderTests و InterviewAudioTests: ۴۳ موفق، صفر شکست/Skip. تست کلید خالی اکنون مقدار خالی را صریحاً فقط در fixture خود می‌دهد تا وابسته به default فعلی نباشد. تماس واقعی AI، تست PostgreSQL، migration، commit، push و deploy انجام نشدند.
