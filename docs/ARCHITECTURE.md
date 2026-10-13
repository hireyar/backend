## ADR-044 — Gemini TTS با حفظ انتخاب provider و MP3 (2026-10-13)

طبق درخواست پیاده‌سازی و تأکید بعدی کاربر، AI:Provider و adapterهای مستقل قبلی حفظ شدند. فقط Liara با مدل google/gemini-*-tts مسیر PCM دارد؛ NetArz و مدل‌های MP3 قبلی Liara ثابت‌اند. تنظیم مشترک Liara TtsModel و TtsVoice به Gemini3.8/Aoede تغییر کرد؛ مدل‌های STT/مصاحبه/گزارش، URL، credential و سایر مقادیر بدون تغییر تأیید شدند. PCM16LE/24kHz/mono در Infrastructure با FFmpeg به MP3 واقعی تبدیل می‌شود؛ speed قبلی با atempo محلی، نه پارامتر تأییدنشده upstream. Application/Domain/API و cache همچنان MP3 هستند؛ migration و تغییر Angular نداریم.

encoder singleton با دو ظرفیت و process یک-thread، سقف۱۰MiB ورودی/خروجی، deadline شامل انتظار، pipes بدون shell/PCM temp، stderr بدون ذخیره/لاگ، cancellation و kill/cleanup محدود دارد. probe codec مصنوعی قبل از تماس هزینه‌دار و startup فقط برای Liara/Gemini؛ شکست typed و رفتار قبلی نبود صوت، بدون جابه‌جایی خودکار provider یا صدای ساختگی. cache identity مدل/صدا/سرعت قبلی نتیجه تازه می‌سازد و داده قدیمی محفوظ است. Docker runtime دارای ffmpeg و مسیر توسعه فقط در config ignored است؛ مستند GEMINI_TTS قرارداد اجرا و محدودیت اعتبارسنجی را ثبت می‌کند. انتشار انجام نشده است.

## ADR-043 — تولید خودکار گزارش و retry فقط پس از شکست (2026-10-13)

طبق درخواست صریح کاربر، رفتار on-demand/synchronous در ADR-026 و بازتولید گزارش موفق در ADR-031 تغییر می‌کند: completion خودکار/دستی برای قرارداد snapshot دارای interview_report، رکورد Queued را در همان تراکنش تسویه/پایان ثبت می‌کند. یک worker ساده Infrastructure مستقل از recovery پاسخ، polling دوثانیه‌ای و concurrency حداکثر۴ دارد؛ صف همان جدول گزارش PostgreSQL است، بدون Repository/زیرساخت توزیع‌شده. تماس مدل همان یک تماس تمام تاریخچه و network خارج transaction؛ مدل/timeout/provider/محافظ شواهد و فرمول v3 محفوظ‌اند.

GenerationStage، attempts واقعی، زمان درخواست/شروع/پایان، failureCode امن و failureStage persist می‌شوند. مرحله‌ها Queued/Generating/Validating/Saving/Succeeded/Failed؛ تنظیمات retry JSONB و cached قبلی محفوظ. GET status سبک و scoped بدون بارگذاری تمام transcriptها، DTO ثابت/message فارسی/canRetry دارد؛ GET گزارش بدوننتیجه دیگر404 نیست، pending202 و failed200 status؛ cached200 schema قبلی. POST و regenerate فقط Failed را به Queued با202 تبدیل می‌کنند؛ صف/فعال/موفق409/report_retry_not_allowed. دو retry یک winner دارند. جلسات تاریخی بدوننتیجه Failed/report_not_generated و درخواست دستی می‌خواهند؛ تاریخچه خودکار بازتولید نمی‌شود.

claim/ثبت مراحل/commit/retry زیر قفل کوتاه سازمان و reload رکورد گزارش؛ lease150ثانیه و محافظ cancellation تماس130ثانیه، در کنار timeout provider<=120. expiry به failure قابلretry می‌رود، worker failure را خودکار تکرار نمی‌کند. generator قدیمی حق تغییر نتیجه یا failure تلاش جدید ندارد؛ حذف جلسه commit را رد می‌کند. DTO/log خطا متن provider/مصاحبه/credential ندارد. migration additive، cached و lease تاریخی را backfill بدون بازنویسی نتیجه حفظ و downgrade دارای کار/تاریخچه را رد می‌کند. [INTERVIEW_REPORT_LIFECYCLE_API](INTERVIEW_REPORT_LIFECYCLE_API.md) قرارداد فرانت جدا/rollout است؛ مقصد منتشر نشده.

## ADR-042 — ضبط بخش‌بندی‌شده، CORS رسانه و انتشار (2026-10-12)

طبق انتخاب صریح کاربر، ادامه بعد از refresh با فایل‌های مستقل و پخش پشت‌سرهم، بدون ادغام codec/file، اضافه شد. جدول InterviewRecordingSegments با PK جلسه/شماره، uploadId یکتا، FK مرکب سازمان/جلسه و SHA-256 محتوا دارد. Candidate POST/GET recording/segments دعوت همان جلسه، HR manifest/GET بخش فقط Owner همان سازمان و retention می‌خواهند. هر فایل مستقل حداکثر 40 MiB، مجموع 512 MiB/۱۴۴۰۰ ثانیه و ۱۰۰۰ بخش؛ شماره پیوسته و totals زیر قفل سازمان کنترل می‌شوند. network/hash بیرون transaction، recheck قبل commit و cleanup loser/failed برقرار است. Replay فقط با همان hash/metadata؛ مسیر تک‌فایلی قدیمی با حالت بخش‌بندی مخلوط نمی‌شود و رکورد تاریخی تبدیل ندارد. migration جدید additive و Down دارای داده رد می‌شود.

گرِیس پایان قبلی ۱۰ دقیقه فقط برای GET/POST بخش‌ها هم گسترش یافت؛ پاسخ جدید/start/current بعد از completion باز نمی‌شوند. مدت/timeline اعلام کلاینت هستند؛ استقلال codec هر بخش، gaps و ذخیره Blob ارسال‌نشده مسئولیت فرانت است و فریم‌های ازدست‌رفته بازیابی نمی‌شوند. Review دارای manifest اختیاری و uploaded_segments_unverified، چاپ تعداد بخش‌ها را نشان می‌دهد؛ این نام کامل‌بودن رسانه را تأیید نمی‌کند. RECORDING_SEGMENTS_API قرارداد فرانت جداست.

CORS برای originهای صریح چهار هدر Content-Range/Accept-Ranges/ETag/Content-Disposition را expose می‌کند؛ wildcard اضافه نشده. ویدیوها inline/download=true و ETag دارند؛ S3 از HEAD object، بخش جدید از SHA-256 واقعی و local تاریخی از tag ضعیف هویت immutable استفاده می‌کند. فایل خصوصی همچنان از API می‌گذرد و هیچ GET امضاشده/عمومی ایجاد نشده. Compose کلیدهای S3 خصوصی را لازم دارد، Docker/Compose temp اختصاصی دارند و نمونه proxy سقف 514 MiB بدنه با فایل 512 MiB/inactivity timeout را مستند می‌کند؛ deploy/proxy/bucket مقصد خودکار تغییر نکرده‌اند.

## ADR-041 — یکتایی عنوان مستقل از فاصله و نیم‌فاصله (2026-10-12)

طبق درخواست کاربر، مقایسه عنوان موقعیت و قالب در ADR-037 از Trim/case به حذف تمام whitespaceهای Unicode و U+200C در هر جای متن گسترش یافت. TitleComparison در Application.Common یک expression مشترک string.Replace برای ورودی و query EF می‌سازد؛ حذف کاراکترها در SQL ترجمه می‌شود و فقط EXISTS روی عنوان‌های scope سازمان/حذف‌نشده اجرا می‌شود، نه بارگذاری همه عنوان‌ها در حافظه. lower برای case انگلیسی سیاست قبلی را حفظ می‌کند؛ حرف‌های فارسی/عربی یا نشانه‌های دیگر تغییر داده نمی‌شوند.

متن نمایشی عنوان و schema ثابت‌اند؛ migration/بازنویسی داده نداریم. عنوان با normalized خالی 400/invalid_title دارد. Create/Update موقعیت و Create/Update/SaveWizard قالب از قاعده واحد زیر قفل سازمان موجود استفاده می‌کنند؛ رقابت دو شکل فاصله‌دار/نیم‌فاصله‌دار فقط یک winner دارد. scope سازمان، self-edit عنوان معادل، حفظ عنوان تاریخی و کپی داخلی دعوت، رزرو عنوان آرشیو و آزادشدن پس از حذف ADR-037 محفوظ‌اند. عنوان تکراری 409 با codeهای قبلی است؛ PANEL_WIZARD_API قرارداد فرانت جاری است.

## ADR-040 — اعطای سوپرادمین به حساب موجود از پنل (2026-10-12)

طبق درخواست صریح کاربر، POST /api/admin/users/super-admins با موبایل حساب موجود اضافه شد. فقط policy=SystemAdmin با audience مستقل و اعتبارسنجی زنده حساب درخواست‌دهنده مجاز است؛ مالک سازمان عادی/داوطلب مجوز ندارند. این عملیات مدیریت سراسری است و سازمان/عضویت مقصد تغییر نمی‌کند. Application DTO/interface، Controller در Api و Identity/SQL/تراکنش در AdminAccounts Infrastructure هستند؛ سرویس واسطه یا Repository تازه اضافه نشد.

Owner فعال و موبایل‌تأییدشده مقصد زیر FOR UPDATE بررسی می‌شود؛ سوپرادمین موجود 409 بدون تغییر رمز/نشست، نبود حساب 404 و حساب نامناسب 400 دارد. رمز از ۱۶ بایت CSPRNG همراه الزامات Identity ساخته، هش و فقط در پاسخ no-store همان grant برگردانده می‌شود؛ رمز حساب غیرادمین قبلی جایگزین، stamp تازه و قفل/شمارنده بازنشانی می‌شوند. ثبت plaintext/ارسال SMS/email نداریم. درخواست هم‌زمان فقط یک winner دارد. CLI موجود برای bootstrap/reset حفظ و منطق مشترک تنظیم رمز استخراج شد. schema/migration لازم نیست؛ قرارداد اتصال فرانت جدا در SUPER_ADMIN_API مستند است.

## ADR-039 — ورود اختصاصی سوپرادمین و کلید مسیر (2026-10-12)

طبق درخواست کاربر روی برنچ مستقل `codex/super-admin` از `dev`، ورود password+CAPTCHA و route فرانت `/admin/{randomkey}` به محدوده افزوده شد. مدل موجود IsSystemAdmin/Identity/یک سازمان حفظ می‌شود؛ مجوز تازه در register/profile یا account عمومی ایجاد نمی‌شود. CLI محلی و تعاملی `--provision-super-admin` فقط حساب Owner موجود و تأییدشده را با رمز hashed و stamp تازه تجهیز می‌کند؛ تغییر حساب در تراکنش و بدون چاپ رمز/شروع HTTP است. روی مقصد اجرا نشده است.

کلید URL-safe تصادفی در SuperAdmin از appsettings خصوصی/environment، نه فایل تحت Git، خوانده می‌شود؛ endpointهای auth با X-Admin-Entry آن را ثابت‌زمان مقایسه و نامعتبر/خاموش را 404 می‌کنند. API کلید صحیح را افشا نمی‌کند. صفحه HTML/route guard در Angular ریپوی جدا باید پیش از نمایش login endpoint entry را بررسی کند؛ این ریپو صفحه فرانت ندارد. کلید مسیر دفاع اضافی است، مجوز به حساب معتبر DB وابسته می‌ماند.

JWT ادمین scheme/audience مستقل، عمر حداکثر۳۰دقیقه، بدون refresh/cookie و بدون org/role دارد؛ SystemAdmin policy فقط این scheme را می‌پذیرد. توکن OTP حسابی که IsSystemAdmin دارد نیز به admin دسترسی نمی‌دهد؛ توکن admin به HR/Candidate دسترسی ندارد. وضعیت DB، stamp، lockout و fingerprint کلید هر درخواست بررسی می‌شوند. حذف مجوز/تغییر رمز/خروج همه‌نشست‌های ادمین فوراً token قدیمی را رد می‌کند. گردش کلید/disable تنظیمات نیازمند restart است. بخش احراز ادمین ADR-018 با این ورود اختصاصی تکمیل/جایگزین می‌شود، snapshot مالی همان تصمیم قبلی است.

CAPTCHA شش‌رقمی PNG فقط BCL در Infrastructure، hash/TTL دودقیقه/مصرف حتی در شکست، حافظه bounded۱۰۲۴ یک process؛ restart challenge را حذف می‌کند. این کپچای ساده تضمین OCR یا سرویس تشخیص انسان نیست. RateLimit پیش از authentication همان IP اتصال را مصرف و پنج رمز اشتباه Identity پانزده دقیقه lockout می‌دهد؛ ثبت شکست/ورود/provision/logout زیر FOR UPDATE کاربر انجام می‌شوند. HttpContext فقط Api؛ Application قرارداد/DTO، Infrastructure Identity/SQL/token/captcha؛ Repository/Domain entity/schema/migration جدید نداریم. [SUPER_ADMIN_API](SUPER_ADMIN_API.md) قرارداد تنظیمات، frontend و API قیمت موجود است.

## ADR-038 — ویدیوی خصوصی در S3 آروان (2026-10-12)

طبق درخواست کاربر، provider ویدیو ArvanS3 در Infrastructure و SDK رسمی AWSSDK.S3 اضافه شد؛ Domain/Application و قراردادهای فعلی IInterviewAudioStorage/DTO تغییر نکردند. فقط ویدیوهای تازهٔ پاسخ/معرفی/ضبط کامل به S3 با multipart قطعات 8 MiB و ACL=Private می‌روند؛ صوت/رزومه و keyهای تاریخی محلی محفوظ‌اند و migration/انتقال خودکار داده نداریم. کلید s3/... محل storage را مشخص می‌کند؛ provider ناموفق fallback محلی ندارد.

GET رسانه همچنان از Api با Owner معتبر، سازمان و retention و no-store می‌گذرد؛ URL عمومی/GET امضاشده صادر نمی‌شود، چون دارندهٔ لینک امضاشده تا انقضا مستقل از هویت می‌تواند آن را بخواند. stream seekable با HEAD/ETag و Range محدود، FileStreamResult 206 را بدون دانلود کامل فایل حفظ می‌کند. Bucket ACL و policy پیش از save/open بررسی و public/نامعلوم رد می‌شوند؛ کنترل خصوصی‌بودن فقط ACL فایل نیست. پاسخ واقعی NoSuchBucketPolicy در SDK v4 با XML امن/DTD ممنوع پذیرفته می‌شود.

Endpoint سرویس از bucket جداست، HTTPS/ForcePathStyle/region صریح است؛ کلیدهای ارائه‌شده در appsettings.Development.local.json ignored تنظیم شدند، فایل مشترک credential خالی دارد و مقصد باید credential خودش را فراهم کند. lifecycle حذف multipart ناقص پیشنهاد شده ولی روی مقصد اعمال نشده است. Upload هنوز از HTTP multipart بک‌اند و IFormFile عبور می‌کند، سقف ضبط همان 512 MiB است؛ طرح browser-to-S3 multipart برای فایل‌های بزرگ فقط پیشنهاد مستند است. [INTERVIEW_VIDEO_STORAGE](INTERVIEW_VIDEO_STORAGE.md) قرارداد، محدودیت‌ها و اتصال فرانت جدا را توضیح می‌دهد.

## ADR-037 — جلوگیری از عنوان تکراری ورودی پنل (2026-10-12)

عنوان موقعیت و قالب هنگام ایجاد/ویرایش در هر سازمان مستقل بررسی می‌شود؛ Trim و تطبیق case-insensitive انگلیسی با lower در query PostgreSQL، رکورد جاری مستثنا و DeletedAtUtc=null ملاک است. آرشیو عنوان را آزاد نمی‌کند؛ حذف منطقی می‌کند. قالب در کل سازمان یکتا بررسی می‌شود، نه فقط در یک موقعیت. ردیف‌های قبلی تغییر/حذف خودکار نمی‌شوند؛ حفظ عنوان فعلی، حتی در داده تکراری قدیمی یا کپی داخلی، مجاز است؛ تغییر عنوان باید غیرتکراری باشد.

JobService.Create/Update به قفل FOR UPDATE موجود سازمان پیوستند؛ TemplateService.Create/Update/SaveWizard از قبل زیر همان قفل‌اند. بررسی عنوان و ذخیره در یک تراکنش کوتاه با همان scope سازمان انجام می‌شوند؛ درخواست هم‌زمان از مسیر قدیمی و ویزارد نمی‌تواند هر دو عنوان یکسان ثبت کند. لایه/Repository/جدول/ستون تازه‌ای اضافه نشد.

خطا HTTP 409 با job_title_duplicate یا template_title_duplicate و متن فارسی است. این قاعده برای ورود کاربر از سرویس‌هاست، نه unique constraint جهانی جدول: کپی داخلی قالب در دعوت cross-position مطابق ADR-019/027 عمداً عنوان snapshot مبدأ را نگه می‌دارد؛ رد آن، دعوت معتبر و قرارداد FK قبلی را می‌شکند. migration لازم نیست و داده/کپی تاریخی محفوظ‌اند. حذف کپی‌ها یا تبدیل مدل قالب مستقل از موقعیت در این درخواست انجام نمی‌شود.

## ADR-036 — حذف واسطه و کد بلااستفاده (2026-10-12)

طبق درخواست پاک‌سازی، AuthService که فقط متدهای IAccounts را forward می‌کرد حذف شد. AuthController مستقیماً قرارداد Application یعنی IAccounts را مصرف می‌کند و شناسه کاربر را فقط از ICurrentUser معتبر سرور می‌گیرد؛ Identity، OTP، JWT و تراکنش‌ها در Infrastructure باقی‌اند. این استثنای ساده برای مرز حساب، جایگزین شرح تاریخی Controller → AuthService → IAccounts است؛ سرویس‌های دارای منطق کاربردی و IApplicationDbContext حفظ شده‌اند.

ثبت default token providerهای Identity و تنظیم عمر توکن ایمیلی حذف شدند: هیچ مسیر فعال تولید/مصرف آن‌ها وجود ندارد و OTP و refresh پیاده‌سازی مستقل دارند. Data Protection دعوت/کوکی باقی است. RenewInvitation بدون مصرف و Provider قابل تنظیم ولی بی‌اثر NetArz حذف شدند؛ resend جاری همچنان جلسه تازه می‌سازد و سابقه را حفظ می‌کند. پیش‌فرض ApiKey در NetArzOptions خالی است؛ appsettings و تنظیمات محلی تغییر نکردند.

اجرای مشترک recovery پاسخ/معرفی در یک متد خصوصی worker قرار گرفت؛ queryهای مستقل، scope مجزا برای هر کار، lease/backoff و ادامه پس از شکست محفوظ‌اند. shortlist سیاست سه‌سطحی قبلی یک‌بار مرتب می‌شود و topic جاری/اول فقط یک‌بار محاسبه می‌شود. رفتار دوشاخه تازه و جلسه‌های تاریخی تغییر ندارد. جدول دعوت تیم و schema/migrationهای تاریخی برای حفظ داده و نبود تغییر مدل حذف نشدند؛ پاک‌سازی کد مجوز حذف داده نیست.

## ADR-035 — تنظیمات دوره آزمایشی ثبت‌نام (2026-10-11)

طبق درخواست کاربر، RegistrationTrial:DurationDays و RegistrationTrial:CreditAmountUsd از appsettings با IOptions و ValidateOnStart خوانده می‌شوند؛ پیش‌فرض ۳ روز/۱ دلار، مدت صحیح ۱..۳۶۵۰ روز و مبلغ decimal مثبت کمتر از 10^12 با حداکثر شش اعشار است. IOptions برای تغییر سیاست نیازمند راه‌اندازی مجدد برنامه است. تنظیمات فقط هنگام ثبت‌نام تازه به انقضای UTC و مبلغ snapshot grant/ledger/wallet تبدیل می‌شوند؛ دوره‌های قدیمی و ورود مجدد تغییر ندارند.

migration ConfigureRegistrationTrial بدون بازنویسی داده، الزام ثابت ۷۲ ساعت/۱ دلار ADR-034 را با روز صحیح مثبت و مبلغ مثبت جایگزین می‌کند؛ constraint trigger مبلغ و BalanceAfter را با CreditAmountUsd همان grant تطبیق می‌دهد. FK/immutable/اتمیک‌بودن signup محفوظ‌اند؛ downgrade با تاریخچه دارای مدت/مبلغ متفاوت از defaults قدیمی رد می‌شود. REGISTRATION_TRIAL_API قرارداد تنظیمات است؛ معماری و API درخواست ثبت‌نام تغییر نکرده‌اند.

## ADR-034 — دوره آزمایشی ثبت‌نام (2026-10-10)

طبق درخواست کاربر، فقط ساخت Owner/سازمان تازه در OTP، یک trial دقیق ۷۲ساعته و یک دلار ledger PromotionalCredit را در تراکنش ثبت‌نام ایجاد می‌کند. اشتراک IsTrial=true و PaymentId nullable برای trial است؛ خریدهای قبلی PaymentId الزامی و IsTrial=false دارند. RegistrationTrials و entitlements قرارداد immutable مستقل از پرداخت‌اند؛ grant پرداخت/خرید جعلی ایجاد نمی‌کند. قابلیت‌ها اجتماع پکیج‌های فعال عمومی هنگام ثبت‌نام، rate از مرجع دارای بیشترین retention و retention بیشینه‌اند؛ Integer بالاتر و Text با ترتیب قطعی snapshot می‌شود. [REGISTRATION_TRIAL_API](REGISTRATION_TRIAL_API.md).

همان subscription ID در InterviewBilling استفاده می‌شود؛ Require/Map/Capabilities قرارداد trial را می‌خوانند و snapshot جلسه شروع‌شده بعد انقضا باقی است. اعتبار هدیه منقضی نمی‌شود ولی دسترسی تازه پس از ۷۲ ساعت به اشتراک نیاز دارد. خرید عادی پس از trial در صف می‌رود؛ upgrade trial رد می‌شود چون ارزش نقدی ندارد. Unique یک trial/سازمان، FK مرکب، immutable و trigger تکمیل ledger/grant در PostgreSQL برقرارند؛ Down دارای تاریخچه رد می‌شود. نبود کاتالوگ ثبت‌نام را کامل rollback می‌کند. ثبت‌نام، refresh یا DTO هیچ کنترل client برای دادن هدیه ندارند؛ Repository/worker/درگاه یا provider تازه اضافه نشد.

## ADR-033 — متوسط سپس فقط آسان یا سخت (2026-10-10)

کاربر مسیر ADR-032 را برای دعوت تازه اصلاح کرد: متوسط اولین سؤال هر موضوع است؛ درستی >=۰٫۷ و اطمینان >=۰٫۶ به سخت، پاسخ ناقص/ضعیف/unknown به آسان همان موضوع می‌رود. پس از پاسخ سؤال دوم، موضوع مستقل از نمره آن بسته می‌شود؛ سقف ۲×موضوع (۱۰..۲۰). بانک سه‌سطحی همچنان کامل ذخیره می‌شود. مدل grade می‌دهد و سرور/Domain شاخه و منع سؤال سوم را کنترل می‌کنند. shortlist ارزیابی متوسط فقط دو شاخه همان موضوع است؛ پس از سؤال دوم، متوسط موضوع تازه با انتخاب AI تخصصی یا ترتیب قالب عمومی.

`MediumFirstTopicQuestions` persisted با default=false به flag progressive افزوده شد؛ snapshot دعوت و resend قبلی رفتار قبلی را نگه می‌دارند. migration جدید flag و constraint وابستگی/sقف را دارد و Down دارای سیاست جدید را رد می‌کند. نسخه prompt تازه `v4-medium-first` است؛ نسخه master قبلی در resend حفظ می‌شود. زمان، billing، تعامل محدود، coverage-independent progression و عدم پایان sufficient evidence حفظ شدند. [ADVANCED_QUESTIONS_API](ADVANCED_QUESTIONS_API.md).

## ADR-032 — پیشروی سه‌سطحی موضوع (2026-10-10؛ نسخه قبلی، اصلاح دعوت تازه در ADR-033)

درخواست کاربر برای آسان ← متوسط ← سخت در همان موضوع، رفتار یک سؤال در هر موضوع ADR-022 را فقط در دعوت‌های تازهٔ موضوعی جایگزین می‌کند. `ProgressiveTopicQuestions` مستقل از flag قدیمی و persisted با پیش‌فرض false است؛ resend سیاست قبلی را حفظ می‌کند. دامنه بانک کامل ۵ تا ۱۰ موضوع با سه سؤال فعال ۱/۳/۵ و یک skill مستقل برای هر موضوع را اعتبارسنجی می‌کند؛ سقف سؤال سه برابر موضوع‌هاست.

مدل نمره nullable درستی پاسخ همان سؤال و اطمینان ارزیابی را در بازه ۰..۱ می‌دهد، مستقل از delta مهارت، clarity/relevance و گزارش نهایی. سرور نمره >=۰٫۷ و اطمینان >=۰٫۶ را برای سطح بعد می‌پذیرد. ارزیابی پاسخ نهایی در InterviewAnswer ذخیره می‌شود؛ تعامل نمره ندارد. Domain و Application سطح بعدِ موضوع موفق را اجباری و موضوع بسته را غیرقابل بازگشت می‌کنند؛ ضعف/unknown/fallback یا پایان سخت به آسان موضوع تازه می‌رود. blacklist پوشش و شرط sufficient evidence در این مسیر اعمال نمی‌شوند؛ زمان و سقف پرسش پابرجایند.

قبل از ارزیابی، shortlist هر دو نتیجهٔ ممکن را دارد؛ پس از FinishAnswer انتخاب دوباره با ارزیابی persisted محدود می‌شود. AI تخصصی موضوع تازه را فقط از shortlist انتخاب می‌کند؛ عمومی ترتیب قالب را دارد. numeric بدون precision محدود مانع گردکردن نمره زیر آستانه به نمره عبور می‌شود. check PostgreSQL جفت بودن/بازه/وضعیت نمره و ناسازگاری دو flag را می‌بندد؛ migration Down دارای داده جدید را حذف نمی‌کند. [ADVANCED_QUESTIONS_API](ADVANCED_QUESTIONS_API.md). کیفیت معنایی تشخیص درست/غلط با تست fake اثبات نمی‌شود؛ تنظیم مدل/provider عوض نشده است.

## ADR-031 — تکمیل گزارش، سابقه انسانی و ضبط جلسه (2026-10-10)

درخواست صریح تطبیق و تکمیل HTML مرجع، report-v3 را به ADR-030 می‌افزاید. یک تماس مدل و JSONB گزارش موجود حفظ می‌شوند. catalog ثابت هفت شاخص عمومی/سه بعد تخصصی با citation معتبر یا score=null؛ تناسب فقط مهارتِ نشان‌داده‌شده در مقابل JD است و سنجش حدسی شخصیت/شباهت فرهنگی جای خود را به working_practices مرتبط با نقش می‌دهد. وزن‌های مثبت موضوعات و آستانه بررسی انتخاب صریح Owner در regenerate و persist در نتیجه هستند؛ وزن ناموجود=۱، نه استخراج حدسی از JD/Priority. JSON/schema به‌تنهایی صحت معنایی مدل را اثبات نمی‌کند.

بازتولید صریح lease موجود را مصرف و گزارش cached را تا commit معتبر حفظ می‌کند؛ constraint metadata اجازه نتیجه قبلی همراه lease می‌دهد. شکست/رقابت/stale generator نتیجه قبلی را حذف نمی‌کند. v1/v2 در POST عادی محفوظ‌اند؛ regenerate نتیجه جاری را جایگزین می‌کند، آرشیو نسخه گزارش تازه نداریم.

InterviewReviewEntry سابقه append-only تصمیم Owner از هویت سرور و telemetry گزارش‌شده توسط داوطلب است؛ درخواست replay با RequestId و unique composite، serialize زیر قفل کوتاه سازمان. زمان دریافت سرور و رخداد کلاینت جدا، UTC با precision PostgreSQL؛ caps انسانی و داوطلب مستقل‌اند. این داده در prompt ارزیابی یا نمرات منفی خودکار به کار نمی‌رود. نبود شاهد unknown است.

InterviewSessionRecording فایل immutable جلسه در storage خصوصی videos موجود و timeline JSONB اعلامی recorder است؛ FK مرکب tenant، حداکثر ۵۱۲ MiB/چهار ساعت، validation offset/answer/اندازه و signature، upload بیرون transaction و recheck دسترسی/retention زیر قفل. فایل با scope Owner/retention/range عرضه می‌شود؛ فایل بازنده پاک می‌شود. داده timestamp قبلی به offset تبدیل نمی‌شود؛ decode/کامل‌بودن رسانه ادعا نمی‌شود. استثنای احراز دعوت فقط POST recording/events تا ده دقیقه پس از Completed، بدون بازکردن current/start/answers، برای آپلود پس از پایان خودکار است. ابطال/حذف/جایگزینی/expiry همچنان مقدم‌اند.

چاپ HTML فارسی با encode/CSP/no-store و print CSS انتخاب PDF مرورگرِ طرح است؛ سرویس PDF/storage یا provider تازه نداریم. اشتراک بیرون سازمان با endpoint ناشناسِ توکن‌دار توسط بازبینی خودکار ایمنی رد شد و اعمال نشده است؛ ادامه نیازمند مجوز صریح کاربر است. Angular/انتشار مقصد همچنان خارج ریپو هستند. [INTERVIEW_REPORT_V3_API](INTERVIEW_REPORT_V3_API.md) قرارداد دقیق اتصال، مقیاس و محدودیت‌هاست.

## ADR-028 — تست گذرای میکروفون پیش از مصاحبه (2026-10-08)

POST audio-check در Controller داوطلب، با scheme دعوت موجود و فقط Created، اعتبارسنجی صوت و STT موجود را مصرف می‌کند؛ حداکثر فایل ۲ MiB (یا حد تنظیمی پایین‌تر)، مدت اعلامی کلاینت ۱ تا ۳۰ ثانیه. Application دسترسی و وضعیت را پیش از تماس و پس از reload بررسی می‌کند. فایل/متن در storage/DB/لاگ برنامه ثبت نمی‌شوند؛ state، billing، Answer/Introduction، LLM و TTS تغییر نمی‌کنند. هر ارسال تماس مستقل STT است؛ retry خودکار/cache/idempotency یا migration ندارد. DTO تفکیک دریافت فایل، تشخیص گفتار و clarityVerified=false دارد؛ STT فعلی معیار کیفیت صوت یا قطع‌نشدن جمله نیست. تطبیق متن با جمله آزمایشی و تأیید کاربر در Angular ریپوی جدا انجام می‌شود؛ API شرط اجباری start نیست. [INTERVIEW_AUDIO_CHECK_API](INTERVIEW_AUDIO_CHECK_API.md).

## ADR-027 — دعوت شش‌رقمی و جایگزینی جلسه (2026-10-08)

دعوت تازه کد شش‌رقمی CSPRNG دارد؛ بررسی به Guid مسیر وابسته است. Data Protection وابسته به جلسه برای نمایش مجدد کد استفاده می‌شود و AccessTokenHash fingerprint ciphertext است تا enumeration آفلاین hash شش‌رقمی ممکن نباشد. دعوت قدیمی hash-only بدون backfill پشتیبانی می‌شود؛ پنل کد قدیمی را بازیابی نمی‌کند. پنج شکست در پانزده دقیقه قفل پانزده‌دقیقه‌ای می‌دهد و limiter IP قبل از authentication است. کلیدهای DP موجود باید پایدار بمانند.

Resend جلسه جدید با snapshot قبلی و بدون داده پاسخ/نمره می‌سازد، تحت همان قفل/transaction سازمان جلسه قبلی را supersede/revoke و رزرو را طبق پاسخ‌های پذیرفته‌شده تسویه می‌کند. FK مرکب سازمانی و unique replacement، رابطه بین سازمان‌ها/جایگزینی دوباره را منع می‌کنند. پایان جلسه ورود دوباره را 410 می‌کند؛ فقط ack/audio مراحل پایانی نتیجه جاری تا Finished استثنا هستند. وضعیت عمومی بدون داده حساس برای صفحه منقضی موجود است. جزئیات در [INTERVIEW_INVITATIONS_API](INTERVIEW_INVITATIONS_API.md).

STT صریح JSON درخواست می‌کند؛ متن نامعتبر fallback ساختگی ندارد و shape diagnostics فقط reason ثابت و اندازه بایت را ثبت می‌کند. DTO پاسخ FailureStage/FailureCode و وضعیت نمایشی processing را عرضه می‌کند. رزرو بسته از recovery پاسخ سؤال حذف می‌شود؛ در current پردازش/retry/pending خاموش و خطای قابل نمایش است، بدون حذف صوت/تغییر امتیاز. این تغییر اثبات علت بسته‌شدن رزرو روی سرور کاربر نیست؛ لاگ موجود فقط خطای transcript پس از HTTP 200 را اثبات می‌کند.

## ADR-025 — معرفی صوتی مستقل و رسانه مرحله جاری (2026-10-07)

براساس قرارداد فرانت، معرفی صوتی موجودیت InterviewIntroduction مستقل از پاسخ سؤال دارد: unique جلسه، clientTurnId الزامی، فایل صوت/ویدیو خصوصی و transcript/status، RowVersion و lease/recovery مستقل. FK مرکب سازمان/جلسه مانع رابطه بین سازمان‌ها است؛ Application فقط EF abstraction و provider/storage موجود را مصرف می‌کند. فایل/network بیرون transaction، پذیرش/claim/ثبت نتیجه زیر قفل کوتاه سازمان با reload و recheck دسترسی/billing است. replay همان نوبت STT تازه ندارد؛ فایل بازنده رقابت پاک می‌شود. worker موجود معرفی را هم با state DB بازیابی می‌کند؛ زیرساخت توزیع‌شده یا Repository جدید نیست.

معرفی نمره/progress/answer یا زمان مالی سؤال نمی‌سازد؛ transcript معتبر در context جلسه persist و سپس Questions آغاز می‌شود. متن خالی/STT ناموفق fallback ساختگی ندارد. مسیر JSON قدیمی محفوظ است ولی پس از پذیرش صوت اجازه bypass پردازش نمی‌دهد. history داوطلب و HR معرفی را مستقل از سؤال نشان می‌دهند؛ رسانه HR scoped/retention دارد. downgrade با داده معرفی موجود رد می‌شود.

stageAudioUrl فقط مرحله ثابت جاری را با Bearer همان دعوت عرضه می‌کند؛ شرکت پیش از پایان Questions یا مرحله آینده/گذشته 404 است. صوت lazy در GET با cache scoped موجود تولید می‌شود؛ state منتظر صوت ثابت نیست، شکست TTS=503 و متن/مرحله محفوظ. پایان پخش توسط فرانت و stage/advance تأیید می‌شود؛ server رویداد playback را نمی‌بیند. پرچم backgroundRetryPending پاسخ/معرفی در lease فعال و backoff true می‌ماند و پس از موفقیت/آخرین شکست false است. [VOICE_INTRODUCTION_API](VOICE_INTRODUCTION_API.md) قرارداد جاری این تکمیل است؛ Angular و rollout مقصد خارج این تغییرند.

## ADR-024 — معرفی‌ها، context دعوت و recovery پایدار (2026-10-07)

طبق درخواست مستقل کاربر، پیام معرفی کارجو با متن پیش‌فرض و معرفی شرکت با پرچم‌های مستقل در قالب و snapshot دعوت افزوده شدند. Stage مستقل از InterviewStatus مالی/پردازش، ترتیب معرفی‌ها را persist می‌کند؛ معرفی شرکت فقط پس از پایان سؤال‌ها عرضه می‌شود. مرحله‌های ثابت با تأیید کلاینت جلو می‌روند؛ متن معرفی کارجو بدون امتیاز مهارتی به context می‌رسد. مسیر بدون معرفی و جلسات تاریخی آغاز قبلی را حفظ می‌کنند. [INTERVIEW_CONTEXT_API](INTERVIEW_CONTEXT_API.md) قرارداد فرانت است.

مستر پرامپت v2 در دعوت snapshot و در system message LLM با schema معتبر ترکیب می‌شود؛ JD، متن رزومه، زبان/نوع، بانک/shortlist، وضعیت/زمان و گفتگو داده untrusted هستند. PDF/DOCX محلی با abstraction IResumeTextExtractor استخراج و snapshot می‌شوند؛ فایل غیرقابل استخراج متن صریح لازم دارد، بدون OCR/ارسال فایل خام. PdfPig فقط Infrastructure است. دعوت فایل‌دار بدون متن معتبر رد می‌شود. مسیر legacy بدون فایل/رزومه همچنان null دارد و backfill تاریخچه انجام نمی‌شود.

تعامل محدود با همان سؤال و answer مستقل، IsInteraction و AssistantMessage ذخیره می‌شود؛ unique partial یک پاسخ نهایی/درحال‌پردازش برای هر سؤال، unique clientTurnId و RowVersion محفوظ‌اند. فقط پاسخ نهایی نمره/evidence دارد و progress نهایی را می‌شمارد؛ schema درخواست AI اجازه گسترش shortlist یا نمره در تعامل ندارد. عمومی سؤال ساده را به ترتیب و بدون sufficient-evidence completion می‌پرسد؛ موضوعی عمومی یک سطح متوسط در هر موضوع با ترتیب ثابت. سیاست تخصصی موضوعی پذیرفته‌شده حفظ شده است. زمان مالی هر نوبت از window محدود سرور و زیر سقف کل جلسه است؛ retry مصرف تازه ثبت نمی‌کند.

BackgroundService در Infrastructure فقط recovery پاسخ پذیرفته‌شده را poll می‌کند؛ queue توزیع‌شده یا Repository جدید نیست. NextRecoveryAtUtc/RecoveryAttempts و lease در DB هستند؛ claim با pair سازمان/جلسه و قفل کوتاه سازمان، recheck دسترسی/billing و commit پیش از network انجام می‌شود. STT شکست‌خورده تا سه تلاش خودکار، سپس retry دستی؛ LLM unavailable تا سقف تلاش و سپس fallback بدون شواهد ساختگی. لغو درخواست/فرایند با lease منقضی قابل recovery است. متن transcript آماده حفظ و دوباره STT نمی‌شود؛ رقابت workerها همان policy RowVersion/lease را دارد. schema قدیمی migration لازم دارد؛ downgrade در حضور تعامل ثبت‌شده به‌جای حذف تاریخچه رد می‌شود.

## تکمیل ADR-017 — انتخاب provider AI (2026-10-07)

انتخاب AI:Provider در composition Infrastructure به client مستقل NetArz/Liara/Disabled وصل می‌شود؛ هر provider options مستقل دارد. به دلیل قرارداد wire یکسان OpenAI-compatible، پردازش HTTP، محدودیت پاسخ، cancellation و logging امن در base داخلی مشترک‌اند و کلاس concrete هویت provider را تعیین می‌کند. فقط تنظیمات provider فعال و lease متناسب با timeoutهای آن اعتبارسنجی می‌شوند؛ Disabled کلید یا HttpClient ندارد. Application و API همچنان فقط interfaceهای موجود را مصرف می‌کنند. provider ناشناخته startup error است؛ fallback به provider دیگر یا retry خودکار تماس خارجی اضافه نشده. تنظیمات و کلیدهای موجود دست‌نخورده‌اند و اتصال واقعی Liara هنوز تأیید نشده است.
# معماری و تصمیم‌ها

## ADR-030 — گزارش سؤال‌محور ساده (2026-10-09)

مطابق HTML مرجع کاربر، report-v2 ارزیابی تمام پاسخ‌های نهایی و جمع‌بندی را در همان یک تماس مدل گزارش تولید می‌کند؛ prompt پیوست پیشنهاد است، نه مجوز PDF/اشتراک/زیرساخت اضافه. DTO و validator در Application، JSONB/lease/provider موجود محفوظ؛ migration یا Repository تازه نداریم. سرور نمره کل/موضوع/توزیع را از نمرات ۰–۵ معتبر محاسبه می‌کند، با میانگین ساده چون وزن JD persist نشده است. فقط سؤال پرسیده‌شده (timestamp یا پاسخ تاریخی) وارد گزارش می‌شود؛ تعامل مستقل نمره نمی‌گیرد و پاسخ‌نداده null است. نقل‌قول و پوشش یک‌به‌یک پاسخ‌ها بررسی می‌شوند. عمومی رزومه/معرفی را کنار می‌گذارد و فقط موضوعات واقعی عرضه می‌کند؛ شاخص HR ثابت، fit score، verdict خودکار یا رویداد دستگاه حدسی ندارد. v1 cached محفوظ و فیلدهای تازه nullable هستند. [INTERVIEW_EVALUATION_API](INTERVIEW_EVALUATION_API.md) قرارداد و محدودیت‌هاست.

## ADR-029 — لاگ زنده موقت و پرامپت احرازشده (2026-10-08)

طبق درخواست مستقل کاربر، SSE تشخیصی با فلگ InterviewDiagnostics:Enabled و CapturePrompts اضافه شد؛ این فقط مشاهده pipeline نوبتی است و تصمیم قبلی عدم افزودن provider مصاحبه Realtime/WebRTC محفوظ است. interface/DTO و execution context بدون HttpContext در Application؛ ring buffer singleton محدود و EF interceptor/provider capture در Infrastructure؛ middleware و SSE/auth در Api. context با سازمان/جلسه بررسی‌شده سرور و worker داخلی ساخته می‌شود، نه داده کلاینت. middleware پس از authorization است و stream/recheck خودش را trace نمی‌کند. EF فقط identifierهای schema مدل را می‌دهد، نه SQL/parameter/value؛ HTTP فقط route template/مسیر نسبی ثابت دارد. prompt از همان JsonContent ارسالی گرفته و شبکه مستقل از subscriber ادامه می‌یابد.

بدنه prompt به درخواست صریح فقط در حافظه موقت نگه‌داری و در stream Owner همان سازمان عرضه می‌شود؛ candidate هرگز payload را دریافت نمی‌کند. این استثنا مجوز ثبت داده واقعی در Git/ILogger/DB نیست. Enabled خاموش جمع‌آوری و endpoint را خاموش و فلگ DTO را false می‌کند؛ config startup و restart لازم است. حافظه/TTL/subscription محدود، expiry/ابطال/حذف/نقش در stream recheck و اتصال ۳۰ثانیه‌ای قابل reconnect است. migration/queue/storage توزیع‌شده/Repository ندارد؛ multi-replica نیاز به routing همان process دارد. [INTERVIEW_DIAGNOSTICS_API](INTERVIEW_DIAGNOSTICS_API.md) جزئیات و محدودیت‌هاست.

## ADR-026 — مدل مستقل گزارش و انتخاب تاریخچه زنده (2026-10-08)

مدل `EvaluationModel` و timeout مستقل در options هر provider، با پیش‌فرض `openai/gpt-6-luna`، فقط گزارش Completed را تولید می‌کنند؛ `LlmModel` زنده/پیشنهاد بانک محفوظ است. گزارش با POST scoped به درخواست پنل، پرامپت مستقل و کل تاریخچه پردازش‌شده تولید و JSONB/model/provider/version/time persist می‌شود؛ GET و POST موفق تکراری از DB هستند. claim/commit قفل کوتاه سازمان، lease با انقضا، recheck حذف و FK مرکب tenant دارند؛ network بیرون transaction، generator کهنه حق commit ندارد. citation باید answerId پاسخ نهایی همان جلسه و نقل‌قول دقیق transcript باشد؛ گزارش score یا قرارداد مالی را تغییر نمی‌دهد و تصمیم انسانی باقی است. migration فقط جدول گزارش می‌افزاید و downgrade گزارش موجود را دور نمی‌ریزد. [INTERVIEW_EVALUATION_API](INTERVIEW_EVALUATION_API.md) قرارداد پنل و تنظیمات است.

تاریخچه کامل از قبل در answers/snapshot/introduction persist بود؛ جدول موازی گفتگو اضافه نمی‌شود. selector محلی، بدون AI/query تازه، همه تعامل‌های سؤال جاری و گروه‌های اخیر را الزامی نگه می‌دارد؛ گروه‌های مرتبط قدیمی در بودجه نرم کاراکتری، و ارجاع صریح/اصلاح با تاریخچه کامل. JD/رزومه/معرفی، state/coverage و تمام evidence تجمعی محفوظ‌اند؛ بانک پرامپت به سؤال جاری و shortlist مجاز محدود است. علامت partial و instruction clarification از حدس درباره گذشته جلوگیری می‌کنند؛ خاموش‌کردن selector قابل تنظیم است. تضمین برابری کیفیت/latency واقعی از تست مصنوعی نتیجه نمی‌شود؛ آزمون provider واقعی و واژگان ضمنی باقی است. Realtime، خلاصه‌ساز LLM، embedding، Repository یا زیرساخت توزیع‌شده اضافه نشده‌اند.

## ADR-023 — پاسخ نوبتی با ویدیو و شناسه کلاینت (2026-10-06)

طبق قرارداد ارسالی فرانت، جریان HTTP فعلی با clientTurnId، lastSpeechEndUtc، durationSeconds ذخیره‌شده و ویدیوی اختیاری گسترش یافت؛ کاربر انتخاب اولیه WebRTC را تصحیح و Realtime را از این درخواست حذف کرد. سرویس/provider جدید بلادرنگ اضافه نشده. زمان پایان speech و مدت کلاینت metadata هستند و سیاست مالی محدود سرور تغییر نکرده است.

clientTurnId رشته opaque تا ۱۲۸ کاراکتر و در هر جلسه unique با partial index PostgreSQL است؛ null برای کلاینت قدیمی مجاز. replay همان شناسه/سؤال همان answer با وضعیت جاری می‌دهد و پردازش/هزینه دوباره ندارد؛ استفاده برای سؤال دیگر conflict است. upload/network بیرون transaction، persistence با قفل سازمان موجود، recheck دسترسی/مالی/state پس از قفل و optimistic RowVersion برای processing حفظ‌اند. نخستین commit برنده و فایل‌های بازنده حذف می‌شوند؛ retry پردازش همچنان مسیر answer.id مستقل است.

storage خصوصی موجود category=videos را با type/size metadata و constraint کامل‌بودن سه فیلد می‌پذیرد؛ WebM/MP4 با signature محدود، بدون decode یا ارسال ویدیو به AI. دانلود فقط HR scoped زیر retention موجود و range processing است؛ global filter یا Repository تازه نداریم. mediaCapabilities در پاسخ candidate و videoAvailable مبتنی بر وجود فایل در لیست پنل‌اند. transcript خود داوطلب و messages فقط سؤال‌های مطرح‌شده/پاسخ‌های همان جلسه را عرضه می‌کنند؛ timestamp سؤال هنگام مطرح‌شدن persist و برای legacy از زمان سرور fallback می‌شود، نه backfill حدسی. Swagger فرم و فایل‌های binary را صریح توصیف می‌کند. [CANDIDATE_TURN_API](CANDIDATE_TURN_API.md) قرارداد فعلی فرانت است.

## ADR-022 — بانک موضوعی پیشرفته و پیشنهاد گذرای AI (2026-10-06)

ساختار گروهی opt-in در ویزارد است؛ قابلیت‌های موجود advanced_ai_workflow/adaptive_questions/adaptive_difficulty تعیین دسترسی می‌کنند، نه package code. قالب‌های ساده و جلسات قدیمی تغییر خودکار نمی‌کنند. گروه با TopicOrder/TopicName و Difficulty=1/3/5 روی سؤال ذخیره می‌شود؛ unique سطح در گروه و check PostgreSQL، اعتبارسنجی بانک کامل در Application. TopicOrder ترتیب آرایه و نام موضوع یکتا است؛ موجودیت اضافی برای گروه بی‌متادیتا لازم نیست. UseTopicQuestions قالب و AskOneQuestionPerTopic جلسه، snapshot ساختار را مستقل از ویرایش آینده نگه می‌دارند. Domain و Policy یک سؤال در هر موضوع را enforce می‌کنند؛ هر موضوع skill snapshot مستقل و سطح هدف از میانگین شواهد معتبر موضوع‌های قبلی، شروع متوسط. پیشنهاد AI صرفاً پاسخ گذرا، از JD scoped، با JSON محدود/اعتبارسنجی و تأیید صریح در save است؛ network زیر قفل سازمان نیست. قراردادهای مالی immutable و قابلیت جلسه پس از شروع از خرید billing خودش باقی‌اند. [ADVANCED_QUESTIONS_API](ADVANCED_QUESTIONS_API.md).


## ADR-021 — حساب موبایلی تک‌کاربر و حذف منطقی پنل (2026-10-06)

طبق درخواست کاربر، API ایمیل/رمز و تیم و سرویس SMTP حذف شدند. Identity برای هویت/refresh باقی است. JWT/refresh/OTP فقط Owner فعال با موبایل تأییدشده را می‌پذیرند؛ DB unique سازمان برای IsActive و check نقش Owner مانع چند حساب فعال و Recruiter فعال‌اند. migration دسترسی Recruiter و refresh/invitation معلق را بازنشسته می‌کند ولی سابقه را پاک نمی‌کند. Entity/enum تاریخی برای سازگاری schema باقی است. انتقال Owner ایمیلی به شماره باید با بررسی مالکیت همان حساب باشد، نه تطبیق حدسی یا انتقال tenant.

DeletedAtUtc از archive مجزاست؛ فیلترها در Application صریح‌اند و global filter نداریم، تا FK و مالی تاریخچه همچنان دیده شوند. حذف موقعیت قالب‌های خودش را در همان transaction پنهان می‌کند؛ دعوت قبلی snapshot دارد. ثبت قالب/دعوت و حذف زیر قفل موجود سازمان سریال‌اند؛ upload/network خارج transaction دعوت می‌ماند. ReplaceQuestions در transaction caller شرکت می‌کند و فقط transaction خودش را commit می‌کند؛ Repository جدید نیست.

حذف جلسه زیر قفل مالی، دسترسی و lease را باطل، جلسه غیرنهایی را Cancel و مصرف پاسخ persistشده را تسویه می‌کند؛ باقی رزرو آزاد و history immutable است. پاسخ‌ها پس از گرفتن قفل بارگذاری می‌شوند تا انتظار قفل موجب تسویه داده کهنه نشود. حذف فیزیکی فایل/هویت یا restore اضافه نشد؛ این حذف از پنل است. cancel/revoke قدیمی مستقل‌اند. Swagger فارسی/tenant 404 حفظ و counters تیم از DTO داشبورد حذف شدند.

## ADR-020 — اشتراک و اعتبار پنل و ارتقای روزشمار (2026-10-05)

درخواست مستقل `subscription_credit.html` روی برنچ مشتق از panel-wizard اجرا شد. APIهای panel، quote/checkout، financial-history و invoice داده چهار تب را می‌دهند؛ Controller → Application Service → EF abstraction و SQL فقط Infrastructure باقی‌اند. کاتالوگ پویا و feature-based است؛ ارتقا با حفظ تمام قابلیت‌ها و افزایش امکانات/محدودیت/retention تشخیص داده می‌شود، نام پکیج شرط runtime نیست. قیمت دوره قبلی final بعد از discount است؛ tax تنظیمی پیش‌فرض ۱۰٪ فقط checkout جدید و بر خالص پس از credit اعمال می‌شود. fingerprint محاسبه سرور تغییر قیمت/صف/اعتبار/tax بین preview و checkout را 409 می‌کند؛ داده مالی از client authoritative نیست.

فرض کاری ارتقا: شروع دوره کامل جدید در verification و کسر ارزش روزهای کامل باقی‌مانده snapshot خالص قرارداد قبلی، floor تومان و بدون بازپرداخت نقدی. اعتبار غیرنقدی InternalTestGrant به اعتبار روزشمار تبدیل نمی‌شود. صف اشتراک/پرداخت Pending پیش از ارتقا باید تعیین تکلیف شود؛ pending upgrade مانع checkout اشتراک دیگر از هر دو مسیر جدید و قدیمی است. انقضای نمایشی به معنی تأیید عدم پرداخت نیست. callback/verify معتبر همان quote ثابت را fulfill می‌کند؛ pendingهای نامعلوم recovery درگاه می‌خواهند.

OrganizationSubscription و SubscriptionPurchase قبلی همچنان immutable‌اند. SubscriptionReplacement رکورد append-only با FK مرکب سازمان، unique source/payment و زمان قطع دسترسی مؤثر است؛ trigger، payment Paid و quote جایگزین را بررسی و وجود اشتراک جدید را در commit enforce می‌کند. trigger هم‌پوشانی از effective end (کمینه expiry و replacement) استفاده می‌کند. Current entitlement رکورد پایان‌یافته را حذف می‌کند؛ billing جلسات شروع‌شده همچنان purchase اصلی را می‌خواند. قفل سازمان، transaction کوتاه و عدم network داخل transaction حفظ‌اند.

گزارش مالی یک projection SQL از پرداخت Paid نقدی و ledger بدون duplicate CreditPurchase است؛ زمان payment=PaidAtUtc، مصرف=تسویه ledger، مجموع‌ها پیش از page و ارزها مستقل‌اند. InternalTestGrant درآمد نقدی نیست؛ adjustment/هدیه در category مستقل تراکنش می‌ماند. کارت اعتبار زمان را از AvailableCredit و rate قرارداد جاری تخمین می‌زند؛ بدون قرارداد rate/time=null. BillableSeconds محدود سرور ثابت ماند و decoded audio ادعا نمی‌شود. Invoice داده JSON از snapshot است؛ PDF یا adapter پرداخت واقعی اضافه نشده. مسیرهای قبلی compatible و Swagger فارسی باقی‌اند.

## ADR-019 — قرارداد ساده ویزارد پنل (2026-10-05)

براساس HTML ارسالی و درخواست مستقل کاربر، مسیرهای `api/panel-wizard` ورودی ساده موقعیت، قالب متنی و دعوت multipart دارند؛ مسیرهای قدیمی سازگار باقی‌اند. Service → IApplicationDbContext حفظ شده، Repository/MediatR یا persistence draft اضافه نشده. فرانت شناسه و داده مراحل را نگه می‌دارد. بانک UI همان مجموعه سؤال‌های قالب است؛ HR نیازی به تعریف مهارت یا بانک موقعیت ندارد. موتور موجود با یک مهارت snapshot محلی «ارزیابی شغلی» کار می‌کند؛ سؤال/ترتیب/شرح شغل/پیام‌های قالب در صدور دعوت ثابت‌اند. نوع دعوت مستقل از نوع پیشنهادی قالب، با entitlement موجود بررسی می‌شود؛ Billing تغییری ندارد.

رابطه مرکب قالب/موقعیت تصمیم پذیرفته‌شده باقی است: انتخاب قالب همان سازمان برای موقعیت دیگر در دعوت یک کپی می‌سازد، مبدأ تغییر نمی‌کند. کارجو، جلسه و کپی اختیاری در یک SaveChanges ثبت می‌شوند. رزومه از abstraction storage خصوصی موجود با category مستقل resumes استفاده می‌کند، bounded size و signature/type بررسی می‌شود و دانلود فقط HR scoped است. فایل تازه در شکست persistence حذف می‌شود؛ نام اصلی کاربر ذخیره نمی‌شود. این اعتبارسنجی اسکن ضدبدافزار نیست و parsing/ارسال رزومه به AI اضافه نشده است.

Resend فقط Created و غیرلغوشده است؛ token hash و expiry را با RowVersion جدید عوض می‌کند. توکن خام دوباره فقط در پاسخ caller HR عرضه می‌شود. delivery ایمیل/SMS صریحاً NotConfigured است؛ بدون adapter لینک/پیام تازه ادعای ارسال نمی‌شود. ویدیو همچنان وجود ندارد. فهرست وضعیت مؤثر expiry را زمان خواندن محاسبه و score فقط با evidence واقعی عرضه می‌کند. جستجو/فیلتر/صفحه‌بندی در SQL و mapping دستی‌اند. قرارداد جدید [PANEL_WIZARD_API](PANEL_WIZARD_API.md) مرجع فرانت جدید است؛ قرارداد صوتی قبلی معتبر می‌ماند.

## ADR-018 — Pricing، اشتراک و اعتبار مستقل AI (2026-10-01)

درخواست مستقل Pricing روی `codex/pricing` پس از fast-forward به `origin/dev` (`d9d4d72`) اجرا شد؛ مصاحبهٔ موجود همان پایه حفظ شد. پکیج/دوره/feature/محصول اعتبار entity پویا در PostgreSQL هستند، نه enum پکیج یا شرط Basic/Advanced. seed فقط migration یک‌باره است. مسیر Service → IApplicationDbContext و لایه‌ها حفظ شد؛ SQL/locking/query splitting در Infrastructure می‌ماند. `PricingCatalog` فقط سیاست خواندن تک‌statement قرارداد را تأمین می‌کند؛ Repository جدید نیست.

SubscriptionPurchase و entitlements در checkout immutable snapshot می‌شوند؛ تغییر قیمت/قابلیت آینده قرارداد پرداخت قدیمی یا pending را تغییر نمی‌دهد. fulfillment پس از verification مستقیم درگاه، اشتراک را اکنون یا انتهای صف اشتراک فعلی زمان‌بندی می‌کند. بازه‌ها با UTC و AddMonths هستند؛ وضعیت مؤثر از زمان خوانده می‌شود. snapshot نرخ/retention و reference اشتراک در start مصاحبه تثبیت می‌شود و پایان اشتراک مانع پایان مصاحبهٔ شروع‌شده نیست. capabilityهایی چون analysis/adaptive/difficulty در پردازش از همین قرارداد خوانده می‌شوند.

IRT تومان مستقیم، USD واحد accounting کیف پول است. محصولات اعتبار مبلغ فروش و مقدار USD صریح دارند و نسبت مؤثر قرارداد snapshot می‌شود؛ forex زنده یا نرخ مشترک اضافه نشد. ledger immutable با compensating entry و Balance cache، reservation کامل حداکثر هزینه و unique reference مالی دارد. تمام mutationهای مالی و read مرکب wallet زیر قفل FOR UPDATE سازمان و transaction هستند؛ DB triggerهای deferred تطابق ledger/balance/reservations و trigger عدم هم‌پوشانی اشتراک را enforce می‌کنند. FK مرکب tenant و Restrict تاریخچه را حفظ می‌کنند.

مدت مالی نسخه اول window پاسخ‌گویی سرور بین آماده‌شدن سؤال پس از TTS و رسیدن پاسخ است، با floor ثانیه و سقف ۱۲۰ ثانیهٔ هر پاسخ و سقف کل جلسه. پاسخ بی‌پاسخ و processing/retry زمان مالی ندارند؛ DurationSeconds کلاینت حقیقت مالی نیست. هزینهٔ نهایی decimal با شش رقم و ToEven گرد می‌شود. این قاعده زمان دقیق صوت را ادعا نمی‌کند؛ جزئیات محدودیت و توسعهٔ decoder/capture در [BILLING](BILLING.md) است.

IPaymentGateway فقط abstraction Application و Disabled implementation runtime دارد؛ fake فقط تست. callback صرفاً محرک verify است و success/price از client نمی‌گیرد. DB unique Provider/TransactionId، PaymentId fulfillment و reference ledger به همراه قفل سازمان، تکرار callback را بی‌اثر می‌کنند. حداقل مرز مدیریت پلتفرم IsSystemAdmin از DB است؛ JWT claim آن در هر درخواست بازسازی و ثبت‌نام/profile هرگز آن را نمی‌پذیرند. Owner/Recruiter مدیر پلتفرم نیستند.

retention دسترسی دانلود رسانه را از snapshot کنترل می‌کند؛ حذف فیزیکی و shared-cache cleanup عملیاتی‌اند. expiry رزرو به صورت lazy در wallet/start/adjustment تسویه می‌شود؛ worker/زیرساخت آینده ساخته نشد. proration، refund درگاه و payment adapter واقعی کار مستقل‌اند. مستندات audit پایه دربارهٔ خالی‌بودن ApiKey با source دریافتی اختلاف داشت؛ default در این تغییر خالی و الزام کلید دوباره تست شد، secret محلی تغییر نکرد. تصمیم‌های قدیمی دربارهٔ موکول‌بودن پرداخت اکنون برای این درخواست مستقل superseded هستند.

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

## تنظیمات AI در appsettings — 2026-10-02

روی برنچ `codex/ai-appsettings` از `dev`، بخش `AI` در `src/HireYar.Api/appsettings.json` اضافه شد: Provider=NetArz، BaseUrl، مدل‌های STT/LLM/TTS، voice، speed و سه timeout. binding موجود `AI:NetArz` و انتخاب `AI:Provider` همین تنظیمات را مصرف می‌کنند؛ override محیطی/local همچنان اولویت دارد. ApiKey در محل فعلی و تمام فایل‌های کلید دست‌نخورده ماند؛ کلید تازه در appsettings اضافه یا کپی نشد. یادداشت‌های تاریخی Disabled برای پیش‌فرض مشترک دیگر وضعیت جاری این برنچ نیستند.

Build موفق با صفر warning/error؛ تست‌های InterviewProviderTests و InterviewAudioTests: ۴۳ موفق، صفر شکست/Skip. تست کلید خالی اکنون مقدار خالی را صریحاً فقط در fixture خود می‌دهد تا وابسته به default فعلی نباشد. تماس واقعی AI، تست PostgreSQL، migration، commit، push و deploy انجام نشدند.

## ADR-030 — اعلان اختیاری دعوت (2026-10-08)

طبق درخواست کاربر، ایمیل SMTP و SMS پترن کاوه‌نگار با Enabled/config مستقل سراسری و SendEmail/SendSms مستقل درخواست اضافه شدند. Application فقط IInterviewInvitationNotifier؛ Infrastructure اتصال‌ها و options را دارد. دعوت/جایگزینی پیش از network commit می‌شود؛ شکست کانال دعوت را rollback نمی‌کند و کانال دیگر اجرا می‌شود. SMS پترن مستقل با token=کد و token2=لینک دارد؛ کلید مستقل اختیاری با fallback به کلید OTP، بدون تغییر OTP. پیش‌فرض خاموش/انتخاب=false. ارسال inline محدود به timeout، بدون retry/صف/persistence تحویل/migration است؛ قطع فرایند پس از commit ممکن است اعلان را ناتمام بگذارد. داده حساس و exception body/URI خارجی لاگ نمی‌شوند. [INTERVIEW_NOTIFICATION_API](INTERVIEW_NOTIFICATION_API.md).
