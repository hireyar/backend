## سوپرادمین محدود به ورود و مدیریت پکیج — 2026-10-12

درخواست بک‌اند روی `codex/super-admin` از `dev`: Verified محلی؛ ورود password+PNG CAPTCHA، کلید appsettings و entry=404 نامعتبر، JWT مستقل و DB permission/stamp/lockout، CLI تعاملی provisioning، همان API پویا و RowVersion قیمت‌گذاری. [SUPER_ADMIN_API](SUPER_ADMIN_API.md) و ADR-039 مرجع‌اند. schema/Repository/عضویت چندسازمانی اضافه نشده؛ سوابق مالی snapshot محفوظ‌اند.

باقی برای تحویل محصول: ساخت route guard و صفحه `/admin/{randomkey}`، تصویر کپچا/ورود، جدول پکیج/دوره/قیمت و مدیریت409 در ریپوی Angular؛ انتخاب و provision حساب واقعی، key خصوصی/Enabled مقصد و بررسی proxy/access-log مسیر خصوصی، آزمون UI واقعی. این اقدامات روی مقصد انجام نشده‌اند؛ سایر مدیریت‌های پلتفرم مجوز توسعه خودکار ندارند. فایل مشترک خاموش/کلید خالی، migration لازم نیست، commit/push/deploy نشده.

Verification نهایی: restore/build صفر warning/error؛ ۳۲۵/۳۲۵ موفق، صفر Skip، PostgreSQL18 اختصاصی محلی و provider/CAPTCHA integration مصنوعی؛ `super-admin-final.trx`. تصویر PNG واقعی به‌صورت synthetic نمایش و بررسی شد؛ OCR/فرانت واقعی ادعا نمی‌شود.

## ویدیوی خصوصی آروان — 2026-10-12

درخواست جاری: اتصال ویدیوهای تازه به S3 آروان، نگهداری credential در appsettings محلی ignored، multipart بک‌اند با حافظه محدود، پخش Owner scoped با Range و کنترل خصوصی‌بودن bucket اجرا شد؛ [INTERVIEW_VIDEO_STORAGE](INTERVIEW_VIDEO_STORAGE.md) مرجع است. migration/انتقال فایل قدیمی لازم نیست و volume صوت/رزومه/فایل تاریخی باید حفظ شود.

باقیِ پیشنهاد حجم بالا: API جلسه آپلود مستقیم، امضای UploadPart/resume/finalize با quota و مالکیت سرور، CORS محدود، cleanup orphan و مصرف در Angular جدا. این پیشنهاد مجوز اجرای خودکار مرحله دیگر نیست. عملیاتی: credential مقصد، lifecycle فقط AbortIncompleteMultipartUpload، بررسی فضای temp/proxy/timeout، بار/codec/ضبط طولانی و player authenticated Range در فرانت؛ هیچ‌کدام روی production اعمال نشده‌اند.

اعتبارسنجی نهایی: restore از NuGet عمومی و build موفق با صفر warning/error؛ suite کامل ۳۱۲/۳۱۲ موفق، صفر Failed/Skipped، PostgreSQL 18 اختصاصی loopback:55486 و S3/provider مصنوعی. TRX: tests/HireYar.Tests/TestResults/s3-video-final.trx. ۱۶ case واحد تازه و یک integration S3 شامل multipart محدود، Empty/oversize، private ACL/public policy/NotPrincipal، 404 XML امن/DTD ممنوع، abort لغو و پاسخ گم‌شده Complete، cleanup، فایل تاریخی/traversal، HTTP206/no-store و JWT/Owner/scope سازمان‌اند. اجرای اولیه suite دو شکست فقط در تنظیمات S3 fixtureهای مستقل Production داشت؛ override Local در آن‌ها اضافه و suite کامل موفق بازاجرا شد.

Smoke واقعی آروان با داده مصنوعی 8 MiB+16 بایت، دو UploadPart/Complete، خواندن seek/Range احرازشده و GET بدون امضا موفق: anonymous=403؛ فایل مصنوعی حذف شد. GetBucketAcl=200 و نبود policy با HTTP404/XML NoSuchBucketPolicy تأیید شدند. لاگ ignored artifacts/s3-video-smoke.log فاقد credential/key فایل یا محتوای کاربر است. آزمون real codec/فرانت/فایل چندگیگابایتی/بار مقصد انجام نشده؛ محدودیت 512 MiB و IFormFile/ترافیک بک‌اند محفوظ‌اند. هیچ migration یا تغییر schema/data مقصد، bucket policy/ACL مقصد، push/deploy/production انجام نشده است.
## جلوگیری از عنوان تکراری — 2026-10-12

درخواست حاضر در Create/Update موقعیت و Create/Update/SaveWizard قالب اجرا شد؛ scope سازمان، case/Trim، self-exclusion، آرشیو/حذف و رقابت با قفل موجود بررسی می‌شوند. فرانت جدا باید detail یا code پاسخ 409 (job_title_duplicate / template_title_duplicate) را کنار ورودی عنوان نمایش دهد؛ تغییر Angular اینجا انجام نشده است. migration لازم نیست، داده‌های قبلی و کپی داخلی قالب دعوت حذف/بازنویسی نشدند؛ ADR-037 مرجع حدود تضمین است.

اعتبارسنجی نهایی: tool restore و restore موفق؛ build صفر warning/error؛ suite کامل ۲۹۵/۲۹۵ موفق، صفر شکست/Skip، روی PostgreSQL 18 اختصاصی loopback:55485 و provider مصنوعی. چهار تست جدید ایجاد/ویرایش، اشتراک قاعده بین مسیرهای قدیمی/ویزارد، scope سازمان، self-edit، آرشیو/حذف، عدم تغییر سؤال/RowVersion در شکست و رقابت create/update را بررسی کردند. TRX: tests/HireYar.Tests/TestResults/title-uniqueness-final.trx. تست‌های دعوت cross-position و snapshot قبلی هم موفق‌اند. داده مصنوعی قالب دوم trial و موقعیت/قالب‌های مستقل billing عنوان مستقل گرفتند؛ assertions مالی/قابلیت‌ها حذف نشدند. نتیجه اولیه suite موفق نبود؛ نتیجه نهایی پس از تطبیق داده تست و حفظ عنوان فعلی در ویرایش است.

## پاک‌سازی کد — 2026-10-12

پاک‌سازی درخواست حاضر: حذف واسطه حساب، wiring توکن ایمیلی بی‌مصرف، متد تمدید دعوت بدون caller، projection/property/usingهای بی‌اثر؛ یکسان‌سازی اجرای recovery و حذف مرتب‌سازی مکرر shortlist تاریخی. بهسازی بدون Repository/لایه عمومی جدید و بدون حذف قرارداد API یا migration انجام شد؛ ADR-036 و STATUS مرجع نتیجه‌اند.

اعتبارسنجی واقعی: dotnet tool restore و restore موفق؛ build صفر warning/error؛ suite کامل ۲۹۱/۲۹۱ موفق، صفر شکست/Skip، روی PostgreSQL 18 اختصاصی loopback:55485 و providerهای مصنوعی. گزارش tests/HireYar.Tests/TestResults/cleanup-verified.trx و لاگ ignored artifacts/cleanup-tests.log. Git diff --check موفق است؛ کیفیت AI واقعی با این تست‌ها اثبات نمی‌شود.

حذف جدول دعوت تیم یا شاخه‌های لازم برای جلسات/گزارش‌های تاریخی در این تغییر انجام نمی‌شود؛ این بخش‌ها با «کد بلااستفاده قابل حذف بدون تغییر داده» یکسان نیستند. کارهای قبلی backlog همچنان همان وضعیت قبلی دارند؛ قابلیت تازه‌ای از backlog اجرا نشد.

## تأیید دسترسی همه پکیج‌ها در دمو — 2026-10-11

کاربر صریحاً انتخاب کرد همه امکانات باز باشند و مصرف از شارژ هدیه کم شود. بررسی کد نشان داد grant ثبت‌نام اجتماع features فعال پکیج‌های عمومی را ذخیره و RequireAsync/ویزارد/قالب موضوعی/start از همان snapshot استفاده می‌کنند؛ خرید پکیج لازم نیست و تغییر runtime لازم نشد. چهار integration مستقل برای عمومی/تخصصی با قالب عادی/موضوعی پیشرفته، حساب واقعی OTP trial با ۰٫۵ دلار و دو روز، پیشنهاد سؤال، دعوت، start/answer/complete، گزارش، کسر هزینه و رد جلسه شصت‌دقیقه‌ای با insufficient_ai_credit اضافه شدند. هیچ شارژ مصنوعی اضافی یا cash purchase در این سناریوها وجود ندارد.

REGISTRATION_TRIAL_API قرارداد فرانت را صریح می‌کند: current.features در subscription/panel، فعال‌کردن پیشرفته بدون شرط packageCode=advanced، پایان زمانی و پیام شارژ مستقل از خرید پکیج. Angular در ریپوی جداست؛ تغییر UI اینجا انجام نشده. build صفر warning/error؛ چهار تست مرتبط ۴/۴ موفق، صفر Failed/Skipped، روی PostgreSQL 18 اختصاصی loopback:55484 و AI/گزارش مصنوعی. TRX: tests/HireYar.Tests/TestResults/trial-package-access-verified.trx. suite کامل قبلی ۲۸۷ موفق متعلق به درخواست تنظیمات است و این نوبت تکرار نشد. اجرای اولیه فقط assertion مصرف شکست خورد چون clock پس از ثبت پاسخ جلو رفته بود؛ clock پیش از answer جلو رفت تا زمان پاسخ واقعی قابل محاسبه باشد و چهار مسیر کامل دوباره پاس شدند. fixture DB صفر باقی ماند و cluster اختصاصی متوقف شد؛ کیفیت/دسترسی provider واقعی با این تست‌ها اثبات نمی‌شود. push/deploy/production یا migration تازه انجام نشده است.

## تنظیم مدت و مبلغ trial — 2026-10-11

روی dev، بخش RegistrationTrial با DurationDays=3 و CreditAmountUsd=1.0 به appsettings اضافه شد؛ 0.5 و مبلغ مثبت با حداکثر شش اعشار پشتیبانی می‌شوند. مقدار نامعتبر هنگام startup رد می‌شود. تغییر سیاست با restart فقط ثبت‌نام‌های جدید را تحت تأثیر می‌گذارد؛ snapshot/سوابق قبلی و ورود مجدد دست‌نخورده‌اند. قرارداد REGISTRATION_TRIAL_API و ADR-035 به‌روز شدند.

migration 20261011120218_ConfigureRegistrationTrial الزام ثابت دیتابیس را به مدت صحیح روز و مبلغ مثبت تغییر می‌دهد؛ ledger با مبلغ grant تطبیق دارد و downgrade ناسازگار با تاریخچه رد می‌شود. migration مقصد هنوز اعمال نشده؛ push/deploy/production انجام نشده‌اند. build نهایی صفر warning/error؛ suite کامل نهایی ۲۸۷/۲۸۷ موفق، صفر Failed/Skipped، با PostgreSQL 18 اختصاصی loopback:55484 و provider مصنوعی؛ TRX: tests/HireYar.Tests/TestResults/trial-settings-complete.trx. دوازده case جدید binding/validation، ثبت نیم‌دلار و دو روز، عدم تکرار، انقضا، downgrade و تطابق ledger را بررسی می‌کنند. drift=false و rollback/apply migration در تست موجود موفق‌اند. اسکریپت delta idempotent artifacts/trial-settings-migrations.sql تولید شد؛ اجرای جداگانه اسکریپت انجام نشد و migration در fixture واقعی اعمال شد. تمام مقادیر قبلی appsettings بدون نمایش secret با HEAD برابر تأیید شدند؛ fixture DB باقی نماند و cluster اختصاصی متوقف شد. شکست اجرای اولیه فقط assertion تست محافظ بود: پیش از trial trigger، wallet mismatch اجرا می‌شد؛ مانده کیف پول synthetic با ledger اشتباه همسان شد تا trial trigger مستقل آزمایش شود و suite کامل بازاجرا شد.

## دوره آزمایشی ثبت‌نام — درخواست 2026-10-10

بک‌اند trial سه‌روزه همه قابلیت‌های فعال عمومی و یک دلار اعتبار هدیه با تشخیص DB/API پیاده شد؛ ورود تکراری و حساب‌های قدیمی هدیه تازه نمی‌گیرند. migration `20261010132215_AddRegistrationTrial` و قرارداد [REGISTRATION_TRIAL_API](REGISTRATION_TRIAL_API.md) آماده‌اند. build بدون warning/error؛ suite کامل **۲۷۵ موفق، صفر شکست/Skip** روی PostgreSQL واقعی، شامل هفت تست جدید؛ guardهای واقعی، drift=false و SQL idempotent دوبار موفق‌اند. TRX: `registration-trial-complete.trx`؛ Verified محلی.

باقی مقصد: اعمال migration پیش از نسخه جدید با مجوز انتشار مربوط؛ Angular جدا برای نمایش isTrial/مهلت/مانده و انتخاب operation=purchase پس از trial قرارداد موجود را مصرف کند. نرخ seed=۰٫۰۵ و یک دلار حداکثر بیست دقیقه رزرو مصاحبه را پوشش می‌دهد؛ وعده اعتبار نامحدود وجود ندارد. این باقی‌مانده مجوز اجرای خودکار انتشار یا تغییر فرانت نیست.

## مسیر متوسط و دو سؤال در هر موضوع — اصلاح صریح 2026-10-10: Verified محلی

متوسط اول؛ پاسخ درست → سخت، پاسخ ناقص/ضعیف/unknown → آسان؛ پس از سؤال دوم موضوع بسته می‌شود. سقف ۲×موضوع و بانک سه‌سطحی، policy snapshot مستقل و سازگاری جلسه/resend قبلی پیاده شدند. ADR-033 و [ADVANCED_QUESTIONS_API](ADVANCED_QUESTIONS_API.md). build صفر warning/error و **۲۶۸ موفق، صفر شکست/Skip** با PostgreSQL واقعی محلی؛ migration/up/down/guard و SQL idempotent دوبار تأیید شدند.

باقی مقصد: migration `20261010085424_AddMediumFirstTopicQuestions` پیش از کد، نمایش سقف ۱۰..۲۰ سؤال اصلی در Angular جدا و سنجش کیفیت پاسخ‌سنجی فارسی مدل واقعی. مجوز انتشار یا تماس واقعی provider صادر نشده؛ بخش سه‌سطحی زیر سابقه نسخه قبلی است.

## پیشروی سه‌سطحی موضوع — درخواست مستقل 2026-10-10: Verified محلی (نسخه قبلی)

دعوت موضوعی تازه با آسان ← متوسط ← سخت پس از پاسخ قابل‌قبول، grade persisted مستقل، سرور کنترل‌کننده سطح و مدل انتخاب‌کننده موضوع تازه پیاده شد. نسخه قبلی جلسه/resend محفوظ؛ سقف counters برابر ۳×موضوع، عمومی به ترتیب قالب و تخصصی shortlist. [ADVANCED_QUESTIONS_API](ADVANCED_QUESTIONS_API.md)، ADR-032 و migration `20261010075455_AddProgressiveTopicQuestions`.

build صفر warning/error و suite کامل **۲۶۴ موفق، صفر شکست/Skip** با PostgreSQL 18 محلی، script idempotent دوبار، drift=false و محافظ rollback واقعی تأیید شدند. باقی برای مقصد: اجرای migration قبل از کد، تطبیق نمایش counters/زمان در Angular جدا، ارزیابی واقعی کیفیت تشخیص پاسخ فارسی و آستانه ۰٫۷/۰٫۶. این باقی‌مانده مجوز انتشار یا تماس واقعی provider نیست؛ ارزیابی خودکار تصمیم استخدام ایجاد نمی‌کند.

## HY-301 — تکمیل بک‌اند طرح گزارش HTML (2026-10-10)

Verification محلی تکمیل: build صفر warning/error؛ suite کامل PostgreSQL **۲۵۰ موفق، صفر Failed/Skipped**؛ `report-v3-verified.trx`. migration واقعی، tenant FK و محافظ downgrade بررسی شدند؛ script idempotent دو بار روی scratch تصادفی موفق بود، یک history entry. frontend/AI واقعی/مقصد و اشتراکِ نیازمند مجوز، تکمیل‌شده محسوب نمی‌شوند.

report-v3، هفت شاخص عمومی/سه بعد تخصصی مستند، وزن/آستانه صریح و regenerate حفظ‌کننده cached، سابقه تصمیم انسانی، consent/device events گزارش‌شده و upload ویدیوی جلسه/timeline خصوصی پیاده‌اند. چاپ HTML/print CSS مسیر PDF مرورگر را فراهم می‌کند. [INTERVIEW_REPORT_V3_API](INTERVIEW_REPORT_V3_API.md) قرارداد همه مسیرها و مقیاس‌هاست؛ ADR-031 جای محدودیت‌های قبلی بک‌اند را برای نسخه جدید روشن می‌کند. وزن پیش‌فرض هر موضوع ۱ و بدون شواهد نمره null است؛ وزن JD حدس زده نمی‌شود.

باقی برای استفاده کامل محصول: اتصال در Angular جدا (metrics/null نمودار، وزن‌ها/regenerate، decision، consent/events، recorder و offset واقعی، fetch رسانه با Bearer، print Blob)، اعمال migration مقصد با مجوز انتشار مستقل، آزمون فارسی واقعی مدل/میکروفون/دوربین/پیوستگی ضبط. آستانه/وزن‌ها از انتخاب HR می‌آیند؛ schema به معنی کیفیت اثبات‌شده مدل نیست. مقایسه آماری معتبر/استخدام خودکار وعده داده نمی‌شود. اشتراک خارج سازمان blocked by automatic approval review است؛ برای endpoint ناشناس توکن‌دار باید مجوز صریح کاربر درباره خروج داده و روش اشتراک گرفته شود. این کار اعمال نشده و بدون مجوز ادامه نمی‌یابد.

## HY-301 — کسری‌های طرح مرجع evaluation-report (2026-10-10): بررسی انجام شد

پس از pull کاربر، endpoint GET/POST /api/interviews/{id}/evaluation-report و report-v2 روی dev=9947b6a بررسی شدند؛ نتیجه نبود endpoint در checkout قدیمی دیگر وضعیت جاری نیست. گزارش سؤال‌محور/موضوعی موجود است ولی پنج مورد فرانت همچنان کم‌اند: سه بعد مستقل و وزن‌ها، هفت شاخص ثابت HR، ثبت رضایت/قطعی و معیار کامل‌بودن ضبط، ویدیوی پیوسته با offset، و API تصمیم انسانی. تطبیق کد و داده موجود در STATUS ثبت شد؛ ADR-030/INTERVIEW_EVALUATION_API نیز محدودیت‌ها را تأیید می‌کنند.

ادامه پیاده‌سازی مطابق طرح نیازمند نام/تعریف/مقیاس هفت شاخص و وزن‌های ارزیابی است؛ HTML مرجع در Git نیست. ضبط پیوسته و telemetry جلسه به قرارداد دریافت داده از فرانت نیاز دارند؛ unknown تاریخی نباید با وضعیت/امتیاز فرضی backfill شود. برای گزارش cached باید سازگاری v1/v2 و سیاست بازتولید تعیین شود. runtime/schema این نوبت تغییر نکردند. build موفق؛ 9 تست report/history موفق و 3 API PostgreSQL Skip؛ نتیجه DB ادعا نشده. push/deploy/production تغییر نکردند.

## گزارش مطابق HTML — 2026-10-09

بک‌اند report-v2 و قرارداد [INTERVIEW_EVALUATION_API](INTERVIEW_EVALUATION_API.md) آماده؛ build و ۱۴ تست مرتبط PostgreSQL واقعی موفق. فرانت جدا باید details nullable برای v1، نمودار topics واقعی، نمره null برای سؤال بی‌پاسخ، confidence/بررسی انسانی، transcript/feedback و رسانه با Bearer مالک را مصرف کند. میانگین ساده/آستانه پاسخ ۳ را روشن نمایش دهد؛ fit score/رتبه‌بندی معتبر/شاخص‌های HR بدون شواهد/رضایت و هشدار دستگاه را از نمونه ثابت کپی نکند. وزن شغلی/rubric اختصاصی در snapshot موجود نیست. PDF، اشتراک، بازتولید v1 و بررسی کیفیت واقعی مدل نیازمند درخواست مستقل‌اند؛ این فهرست مجوز اجرای خودکار آن‌ها نیست.

## پیگیری 503 گزارش ارزیابی — 2026-10-08

مدل مشترک گزارش Liara/openai/gpt-6-luna با timeout=90 است؛ مدل در GET models موجود و smoke گزارش با context مصنوعی HTTP 200/حدود ۹٫۳ ثانیه/JSON قابل parse داشت. override توسعه محلی NetArz است؛ GET models آن 404 می‌دهد، بدون اثبات علت خطای گزارش. build و ۴۷ تست مستقل موفق. کد/تنظیمات/کلیدها عوض نشدند.

برای تعیین علت خطای واقعی کاربر، لاگ همان trace با operation=report و provider/model/status/httpStatusCode/durationMs لازم است. HTTP failure/timeout را از JSON/schema/citation rejection پس از HTTP 200 تفکیک کنید؛ متن رزومه/پاسخ/کلید/پرامپت واقعی در Git یا لاگ قرار نگیرد. پیام عمومی evaluation_report_unavailable همه این علت‌ها را یکسان می‌کند؛ در صورت نیاز به اصلاح تشخیص، کد علت ثابت امن را در logging/SSE گزارش نگه دارید. نبود مدل یا رفع خطای مقصد ادعا نشده است؛ این یادداشت مجوز تغییر مدل/مقصد یا انتشار نیست.

## لاگ زنده آزمایشی — 2026-10-08

بک‌اند و قرارداد [INTERVIEW_DIAGNOSTICS_API](INTERVIEW_DIAGNOSTICS_API.md) تکمیل شدند: flag/DTO نمایش، SSE scoped، metadata واقعی EF/provider/مراحل و capture همان prompt فقط Owner در حافظه محدود. build موفق؛ ۲۲۷ تست suite PostgreSQL واقعی موفق و پس از اصلاح cursor نهایی ۹ تست مرتبط مجدد موفق، صفر Skip/شکست.

باقی تحویل همین درخواست: Angular ریپوی جدا فقط طبق diagnosticsEnabled UI موقت را نمایش دهد؛ fetch streaming با Bearer و parser SSE، dedup/cursor/reconnect/closed/reset و پاک‌کردن UI هنگام خاموش‌شدن flag را مصرف کند. نمایش prompt فقط در ابزار احرازشده Owner، نه با کد دعوت؛ credential مالک در صفحه عمومی قرار نگیرد. در مقصد restart پس از تغییر flag، buffering/timeouts proxy و در صورت چند replica routing همان process بررسی شوند. وقتی آزمایش تمام شد Enabled=false؛ حذف نهایی کد به درخواست مستقل. AI واقعی و rollout انجام نشده‌اند؛ این فهرست مجوز اجرای خودکار Angular یا انتشار نیست.

## تست میکروفون داوطلب — 2026-10-08

API گذرای audio-check قبل از start پیاده و با build بدون هشدار/خطا و ۲۲۲ تست موفق PostgreSQL واقعی تأیید شد؛ قرارداد [INTERVIEW_AUDIO_CHECK_API](INTERVIEW_AUDIO_CHECK_API.md). تحویل فرانت باقی است: نمونه کوتاه، ارسال multipart با کد دعوت، نمایش transcript، تطبیق جمله توسط کاربر و retry خطا. کیفیت واقعی STT فارسی/سکوت/نویز/قطع ضبط باید با provider واقعی بررسی شود. سنجش مستقل شفافیت و کامل‌بودن جمله پیاده نشده؛ clarityVerified=false است. این فهرست مجوز توسعه خودکار موارد دیگر یا انتشار نیست.

## آخرین تکمیل: دعوت جدید و خطای STT — 2026-10-08

درخواست کد شش‌رقمی، نمایش لینک/کد در API پنل، reinvite با حفظ جلسه قبلی و پیام انقضا تکمیل و با ۲۲۰ تست موفق تأیید شد. وضعیت processing/retry و FailureStage/FailureCode برای فرانت روشن شد؛ STT JSON صریح و تشخیص امن پاسخ نامعتبر اضافه شد؛ recovery پاسخ با رزرو بسته متوقف است.

باقی تحویل: Angular ریپوی جدا باید فرم کد رشته‌ای، access-status/410 و processingMessage/isProcessing/answer.canRetry را مصرف کند. بررسی reason جدید روی Liara واقعی و آزمون صوت واقعی لازم است تا علت پاسخ نامعتبر گزارش‌شده تأیید شود. migration مقصد و انتشار نیاز به درخواست مستقل دارند. [INTERVIEW_INVITATIONS_API](INTERVIEW_INVITATIONS_API.md) مرجع قرارداد است؛ این فهرست مجوز توسعه مراحل دیگر نیست.

## آخرین تکمیل: مدل گزارش مستقل و انتخاب تاریخچه — 2026-10-08

درخواست مستقل کاربر روی dev تکمیل شد: EvaluationModel/timeout مستقل، گزارش کامل Completed با generation به درخواست پنل و cache پایدار، citation/quote معتبر و lease/tenant/حذف/reclaim؛ تاریخچه کامل DB موجود حفظ و ورودی زنده با selector محلی و محافظ context/شواهد تجمعی محدود می‌شود. build صفر warning/error و **۲۰۹ تست PostgreSQL واقعی موفق، صفر Skip**؛ migration جدید و delta idempotent روی baseline قبلی دوبار موفق. [INTERVIEW_EVALUATION_API](INTERVIEW_EVALUATION_API.md) قرارداد است و جزئیات تست در STATUS/HANDOFF هستند.

باقی این تحویل: مصرف POST/GET گزارش در Angular ریپوی جدا، migration مقصد با مجوز مستقل، تأیید پشتیبانی واقعی `openai/gpt-6-luna` و آزمون مقایسه‌ای کیفیت فارسی، ارجاع ضمنی/مترادف، duration/token واقعی و صرفه‌جویی کل prompt. گزارش خودکار در completion، بازتولید گزارش cached، embedding یا خلاصه‌ساز AI اضافه نشده‌اند. این فهرست مجوز توسعه خودکار یا انتشار نیست؛ درخواست اصلی ذخیره تاریخچه به داده‌های persistشده موجود متصل است و جدول موازی گفتگو لازم نشد.

## تکمیل قبلی: معرفی صوتی و صوت مرحله‌ها — 2026-10-07

کسری STT/upload/retry/recovery معرفی و صوت ثابت مرحله‌ها در بک‌اند رفع و با 198 تست PostgreSQL واقعی تأیید شد. [VOICE_INTRODUCTION_API](VOICE_INTRODUCTION_API.md) مرجع جدید است. باقی این تحویل: اتصال Angular به قرارداد صوتی، migration مقصد/فعال‌بودن worker و آزمون صوت/دوربین واقعی. بهینه‌سازی صوت جریانی/آماده‌سازی سؤال‌ها و تلفظ هنوز باز هستند؛ یادداشت‌های متنی پایین وضعیت تاریخی پیش از این تکمیل‌اند. این فهرست مجوز اجرای مراحل دیگر یا rollout نیست.

# معرفی‌ها، context AI و recovery — 2026-10-07: Verified محلی

درخواست مستقل ساختار بک‌اند اجرا شد: دو معرفی با فعال‌سازی/snapshot و مرحله persistشده، مستر پرامپت v2، استخراج محلی PDF متنی/DOCX یا resumeText صریح، ارسال context کامل ویزارد، ترتیب عمومی، تعامل محدود همان سؤال و worker بازیابی پاسخ پذیرفته‌شده. **۱۹۲ تست PostgreSQL واقعی موفق و صفر Skip**، ۹ تست تازه؛ migration روی legacy مصنوعی با script idempotent دوبار بررسی شد و EF drift ندارد. [INTERVIEW_CONTEXT_API](INTERVIEW_CONTEXT_API.md) مرجع قرارداد است.

باقی‌مانده اتصال/عملیات: مصرف stage/advance و interactionMessage و خطای resume_text_required در Angular ریپوی جدا، اعمال migration مقصد با مجوز مستقل و آزمون provider واقعی/کیفیت فارسی. OCR/DOC parsing، STT اختصاصی معرفی و backfill رزومه جلسات تاریخی اجرا نشده‌اند؛ معرفی کارجو ورودی متنی و مسیر legacy بدون فایل resume=null دارد. پس از سه شکست STT، retry دستی حفظ می‌شود؛ متن/evidence ساختگی برای جلو رفتن ساخته نمی‌شود. این موارد مجوز توسعه خودکار یا push/deploy نیستند.

## انتخاب پیاده‌سازی مستقل providerهای AI — 2026-10-07

روی dev، DI براساس AI:Provider یکی از NetArzClient، LiaraClient یا DisabledInterviewProvider را برای STT/LLM/TTS/پیشنهاد سؤال انتخاب می‌کند؛ مقدار ناشناخته در startup رد می‌شود. NetArzOptions و LiaraOptions مستقل‌اند، فقط تنظیمات provider انتخاب‌شده اعتبارسنجی می‌شود و lease از timeoutهای همان provider محاسبه می‌شود. Disabled هیچ HttpClient یا کلید provider نمی‌خواهد. جزئیات مشترک قرارداد HTTP در OpenAiCompatibleInterviewClient داخل Infrastructure باقی‌اند؛ نام provider در نتیجه/metrics/log از کلاس انتخاب‌شده می‌آید و property قدیمی NetArzOptions.Provider authoritative نیست.

تمام appsettingsها و مقادیر کلید موجود دست‌نخورده‌اند؛ انتخاب پیش‌فرض هنوز NetArz است. برای انتخاب Liara مقدار AI:Provider=Liara یا override محیطی AI__Provider=Liara لازم است. آدرس و مدل STT تنظیم‌شده طبق بررسی قبلی همچنان نیاز به تطبیق کنسول/تأیید واقعی دارند؛ تغییر حدسی انجام نشد. افزودن provider دیگری نیازمند options، client و شاخه ثبت DI مستقل است؛ انتخاب نام دلخواه پشتیبانی خودکار ایجاد نمی‌کند.

build صفر warning/error؛ 88/88 تست مرتبط provider/audio/selection/rules موفق، صفر شکست/Skip؛ tests/HireYar.Tests/TestResults/provider-selection.trx. هشت case تازه انتخاب چهار interface، عدم دخالت تنظیمات provider دیگر، کلید مستقل، lease، نام ناشناخته، Disabled و درخواست‌های مصنوعی Liara با آدرس/کلید/مدل/نتیجه مخصوص را بررسی می‌کنند. تماس واقعی AI، PostgreSQL/migration، commit/push/deploy انجام نشد؛ schema و قرارداد Application/API تغییر نکرده‌اند. تغییرات مستندی نوبت قبلی حفظ شدند.

## بررسی قابلیت استفاده از Liara — 2026-10-07

بررسی روی dev پاک انجام شد؛ کد runtime و تمام appsettingsها/توکن‌ها دست‌نخورده‌اند. AI:Provider فعلی NetArz است؛ AddInterviews فقط NetArz/Disabled را می‌پذیرد و AI:Liara را bind نمی‌کند. تغییر Provider به Liara در وضع فعلی startup را رد می‌کند. NetArzClient نام provider را در نتیجه/metrics/log ثابت دارد و اعتبارسنجی lease فقط timeoutهای NetArz را می‌خواند؛ پشتیبانی انتخاب Liara باید این موارد را نیز اصلاح کند.

مستند رسمی https://docs.liara.ir/ai/openai/ سازگاری chat/completions، audio/transcriptions و audio/speech، مدل openai/gpt-5.6-luna و openai/tts-1-hd و voice=nova را مستند کرده است. مدل تنظیم‌شده openai/gpt-transcribe در فهرست آن صفحه نیست؛ openai/whisper-large-v3 مستند است، اما نبود در فهرست اثبات عدم پشتیبانی نیست. نمونه BaseUrl در https://docs.liara.ir/ai/quick-start/ ترتیب /api/v1/{workspace} دارد؛ ترتیب تنظیم فعلی /api/{workspace}/v1 است و باید با آدرس واقعی کنسول تطبیق شود، نه تغییر حدسی.

درخواست GET models با کلید موجود فقط برای بررسی خواندنی انجام شد، اما HttpRequestException شبکه بدون پاسخ HTTP رخ داد؛ صحت کلید، پلن، آدرس و اجرای واقعی AI تأیید نشده‌اند. هیچ درخواست تولید متن/صوت یا ارسال داده کاربر انجام نشد و مقدار کلید/شناسه workspace در یادداشت ذخیره نشد. build صفر warning/error؛ 47/47 تست InterviewProviderTests و InterviewAudioTests موفق، صفر Skip؛ tests/HireYar.Tests/TestResults/liara-review.trx. PostgreSQL/migration، commit/push/deploy انجام نشد. نتیجه: Liara از نظر قرارداد API قابل اتصال است، اما هنوز provider قابل انتخاب پروژه نیست؛ اتصال واقعی و مدل STT باز هستند.

# Backlog

## HY-019 — پاسخ خودکار نوبتی داوطلب (2026-10-06): Verified محلی

قرارداد فرانت در [CANDIDATE_TURN_API](CANDIDATE_TURN_API.md) پیاده شد: audio قبلی، video اختیاری، clientTurnId اتمیک و replay، lastSpeechEndUtc و duration metadata، transcript/messages، capability/limits و Swagger binary. ۱۷۵ تست واقعی PostgreSQL و build موفق؛ migration/script و حفظ legacy تأیید شدند. طبق تصحیح صریح کاربر WebRTC/Realtime از این درخواست خارج شد و زیرساخت آن اضافه نشده است.

باقی تحویل: اعمال migration `20261006170044_AddCandidateAnswerCapture` در مقصد، اتصال Angular با MediaRecorder/تشخیص ۲ ثانیه سکوت و محدودیت mediaCapabilities، بررسی واقعی مجوز/codec مرورگر و timeout/حجم proxy، ضبط ویدیو و retention/cleanup عملیاتی. این موارد مجوز push/deploy یا شروع خودکار Realtime نیستند؛ مالی قبلی، مدت server و AI متن/صوت حفظ‌اند.

## رفع 500 دعوت ویزارد — 2026-10-06: اصلاح محلی؛ مقصد باز

لاگ traceId گزارش‌شده UnauthorizedAccessException هنگام ساخت پوشه رزومه را تأیید کرد. Dockerfile و هر دو Compose اکنون storage خصوصی متعلق به APP_UID و volume پایدار دارند؛ ۲۱ تست مرتبط PostgreSQL واقعی، build و config هر دو Compose موفق‌اند. اجرای Docker image به علت نبود daemon تأیید نشد. باقی: rebuild/recreate در مقصد با مجوز مستقل، حفظ/انتقال فایل قبلی احتمالی پیش از mount volume تازه، بررسی نوشتن با حساب سرویس و ارسال دعوت از پنل. migration تازه لازم نیست؛ push/deploy/production تغییر نکرده‌اند.

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

## پیگیری بررسی مرورگر مصاحبه — 2026-10-07

باز: پاسخ صوتی مقصد ثبت شده ولی transcript/ارزیابی آماده نشده؛ UI با صفر پاسخ پردازش‌شده و زمان صفر، بازیابی retry را عرضه نمی‌کند. علت اولیه با شواهد UI قطعی نیست. پیش از تغییر runtime، وضعیت answer/canRetry در current و failureCode/لاگ STT یا lease مقصد بررسی شود؛ سپس اولویت نمایش ProcessingAnswer/retry نسبت به اتمام زمان در فرانت تطبیق داده شود. این پیگیری مجوز deploy یا ارسال مجدد داده واقعی به AI نیست.

شواهد جدید 2026-10-07: لاگ مقصد stt_http_404 و tts_http_404 با provider=NetArz را تأیید کرد؛ علت اولیه اکنون شکست HTTP سرویس صوت است. پیگیری باز: تطبیق provider/BaseUrl/مدل مؤثر سرور با قرارداد همان سرویس‌دهنده و اصلاح نمایش retry در فرانت. دلیل دقیق 404 هنوز بدون response پالایش‌شده provider مشخص نیست؛ انتخاب Liara از این لاگ قابل ادعا نیست.

پیگیری Liara 2026-10-07: provider طبق لاگ Liara است ولی STT/TTS هر دو 403 می‌گیرند. کلید AI و scope همان پروژه، مدل‌های مجاز پلن و BaseUrl دقیق کنسول با /api/v1/{project} بررسی شوند. STT مستند openai/whisper-large-v3 است؛ مدل جاری openai/gpt-transcribe مستند نشده. علت قطعی 403 نیازمند error code/message پالایش‌شده مقصد است؛ تغییر حدسی کلید/پلن یا retry واقعی انجام نشد.

تکمیل محلی دو اصلاح مجاز Liara — 2026-10-07: BaseUrl و STT در appsettings.json اصلاح شدند؛ ApiKey و سایر تنظیمات حفظ شدند. build و 55 تست مرتبط موفق. تأیید رفع 403 در مقصد، scope کلید/پلن و ایراد retry فرانت همچنان باز هستند؛ override مقصد باید با تنظیمات اصلاح‌شده تطبیق داده شود.

بازبینی appsettings — 2026-10-07: JSON فایل‌های موجود معتبر، BaseUrl و STT اصلاح‌شده Liara صحیح، ApiKey نسبت به HEAD یکسان و قیود timeout/speed/lease (پیش‌فرض 300 ثانیه) معتبرند. نکته باقی‌مانده: AI:Provider فایل مشترک هنوز NetArz است؛ local override توسعه و AI__Provider در محیط این بررسی وجود ندارند. انتخاب Liara سرور از لاگ قبلی تأیید شده ولی تنظیمات مؤثر مقصد این نوبت خوانده نشد. درخواست صرفاً بررسی بود؛ تنظیمات تغییر نکردند و build/test تکرار نشد (55 تست و build نوبت اصلاح موفق بودند).

## تنظیمات Liara مطابق curl موفق کاربر — 2026-10-07

شواهد مستقیم کاربر، STT موفق با BaseUrl دارای الگوی /api/{project}/v1 و model=openai/gpt-transcribe را نشان داد. بنابراین توصیه قبلی برای جابه‌جایی مسیر و تغییر مدل بر اساس فهرست مستندات، برای این پروژه نادرست بود و این یادداشت جایگزین نتیجه آن توصیه است. در appsettings.json آدرس دقیق ارائه‌شده کاربر بدون تغییر شناسه پروژه و مدل openai/gpt-transcribe اعمال و AI:Provider=Liara شد. ApiKey و همه مقادیر دیگر عیناً حفظ و به‌صورت بدون نمایش مقدار بررسی شدند. transcript واقعی کاربر یا کلید در مستندات ثبت نشد.

build صفر warning/error و 55/55 تست provider/selection/audio موفق، صفر شکست/Skip. STT واقعی طبق curl کاربر موفق است؛ تماس تازه AI توسط agent انجام نشد و موفقیت TTS یا مسیر کامل مصاحبه هنوز تأیید نشده. Overrideهای سرور ممکن است تنظیمات فایل را جایگزین کنند. مجوز کلید/پلن و علت 403 قبلی از این curl به‌تنهایی قابل تعیین نیست. push/deploy یا تغییر DB انجام نشد.

## بررسی معرفی صوتی، تلفظ و تأخیر — 2026-10-07

بررسی کد روی dev: appsettings مشترک AI:Provider=Liara، STT=openai/gpt-transcribe، LLM=openai/gpt-5.6-luna و TTS=openai/tts-1-hd با nova و speed=1 دارد؛ override مقصد بررسی نشد. یادداشت‌های قدیمی NetArz پیش‌فرض وضعیت فعلی فایل نیستند. معرفی کارجو فقط متن stage/advance می‌پذیرد و canRecord در CandidateIntroduction=false است؛ مسیر آپلود/STT معرفی و صوت stageMessage وجود ندارد. معرفی شرکت مرحله persistشده و متن snapshot دارد، اما audio URL مرحله ارائه نمی‌شود؛ Angular در این ریپو نیست.

مسیر پاسخ STT سپس LLM و Map(generateAudio=true) است؛ cache miss تولید کامل TTS سؤال بعدی را پیش از برگشت پاسخ منتظر می‌ماند. ResponseHeadersRead داخل provider به معنی streaming به کلاینت نیست؛ پاسخ کامل در MemoryStream جمع می‌شود. cache scoped صوت و durationMs عملیات stt/llm/tts از قبل وجود دارند. مستر پرامپت فقط به LLM می‌رسد؛ درخواست TTS فقط model/input/voice/speed/response_format دارد. طبق مستند رسمی OpenAI، instructions در tts-1-hd پشتیبانی نمی‌شود؛ پشتیبانی مدل جایگزین در Liara آزمایش نشده. اصلاح تلفظ وابسته به جمله، آماده‌سازی صوت ثابت پیش از نوبت و جداسازی آماده‌شدن صوت از پاسخ API گزینه‌های بررسی‌اند، هنوز پیاده نشده‌اند. context رزومه/بانک/گفتگو برای کاهش تأخیر حذف نشود.

تنظیمات، کلیدها و کد runtime تغییر نکردند؛ build نهایی صفر warning/error و 89/89 تست provider/selection/audio/rules موفق، صفر Skip. اجرای نخست build به‌علت تداخل با build تست قفل فایل داشت؛ اجرای ترتیبی مجدد موفق شد. PostgreSQL و AI واقعی و زمان‌های مقصد در این بررسی آزمایش نشدند؛ علت غالب تأخیر بدون durationMs مقصد معلوم نیست. push/deploy/تغییر production انجام نشد.

## تکمیل معرفی صوتی و صوت مرحله‌ها — 2026-10-07: Verified محلی

طبق قرارداد فرانت روی dev، POST multipart introduction بدون questionId با audio/clientTurnId الزامی و durationSeconds/lastSpeechEndUtc/video اختیاری اضافه شد. InterviewIntroduction مستقل، unique جلسه و FK مرکب سازمان/جلسه، RowVersion و lease، فایل خصوصی، replay بدون STT دوباره، retry بدون upload و recovery worker دارد. transcript معتبر تنها شرط انتقال به Questions است؛ متن خالی یا شکست STT fallback ساختگی ندارد. معرفی progress/evidence/نمره/زمان مالی سؤال ایجاد نمی‌کند؛ transcript در context و تاریخچه داوطلب و HR موجود است و صوت/ویدیوی HR scoped و زیر retention قابل دریافت‌اند.

stageAudioUrl برای Welcome/درخواست معرفی/CompanyIntroduction/Goodbye فقط در مرحله جاری با Bearer دعوت ارائه می‌شود؛ GET در cache miss صوت را lazy تولید می‌کند، TTS ناموفق 503 و مرحله/متن محفوظ است. شرکت پیش از پایان سؤال‌ها 404 دارد؛ ادامه بعد از playback با تأیید فرانت است. backgroundRetryPending پاسخ و معرفی در lease فعال و backoff true می‌ماند؛ پس از موفقیت/آخرین شکست false. مسیر معرفی متنی قدیمی محفوظ است ولی معرفی صوتی پذیرفته‌شده را bypass نمی‌کند. [VOICE_INTRODUCTION_API](VOICE_INTRODUCTION_API.md) قرارداد نهایی OpenAPI/فرانت است. یادداشت‌های قبلی «STT معرفی وجود ندارد / canRecord معرفی=false» وضعیت قبل از این تغییرند.

build نهایی صفر warning/error؛ 198/198 تست PostgreSQL 18 اختصاصی محلی موفق، صفر شکست/Skip، شامل شش تست جدید معرفی/replay/ویدیو/تاریخچه/رسانه HR و scope/constraint، lease فعال و جلوگیری از bypass، recovery worker واقعی/backoff، خروجی STT خالی و سقف تلاش/retry، قطع درخواست و رقابت claim، و multipart/OpenAPI. tests/HireYar.Tests/TestResults/voice-introduction-final.trx. migration 20261007151257_AddVoiceIntroduction تولید و در fixture واقعی اعمال شد؛ HasPendingModelChanges=false در تست. delta idempotent artifacts/voice-introduction-migrations.sql دو بار روی baseline اختصاصی قبل از migration موفق و فقط یک history entry داشت؛ baseline این بررسی بدون داده legacy بود، بازنویسی داده تاریخی در migration وجود ندارد. downgrade در حضور معرفی صریحاً رد می‌شود.

باقی: مصرف قرارداد جدید در Angular، اعمال migration و بررسی فعال‌بودن worker در مقصد با مجوز مستقل، آزمون فارسی/STT/TTS/سکوت/VAD و دوربین واقعی. بهینه‌سازی streaming/آماده‌سازی سؤال‌ها و واژه‌نامه تلفظ در این تغییر پیاده نشده‌اند؛ مدل/کلید/appsettings دست‌نخورده‌اند. AI واقعی، push/deploy/commit و تغییر production انجام نشده‌اند. داده تست مصنوعی و SQL فقط محلی بود.

## ارسال اختیاری دعوت — 2026-10-08

روی dev، ارسال SMTP ایمیل و SMS پترن کاوه‌نگار در دعوت ویزارد/قدیمی/resend اضافه شد. InterviewNotifications:Email/Sms فلگ/config مستقل؛ SendEmail/SendSms پیش‌فرض false. پترن دعوت مستقل، token=کد شش‌رقمی و token2=لینک کامل؛ ApiKey خالی SMS از کلید OTP استفاده می‌کند. SMTP UTF-8 شامل نام/شغل/کد/لینک/انقضای UTC است. هر دو فلگ مشترک فعلاً false؛ کلیدهای قبلی محفوظ‌اند. ارسال بعد از commit، خارج قفل مالی و با نتیجه مستقل هر کانال انجام می‌شود؛ خطا لینک را rollback نمی‌کند. بدون retry/صف/ذخیره وضعیت تحویل یا migration؛ قطع فرایند پس از commit ممکن است ارسال را ناتمام بگذارد.

[INTERVIEW_NOTIFICATION_API](INTERVIEW_NOTIFICATION_API.md) قرارداد فرانت و appsettings است. Angular در ریپو نیست؛ دو تیک باید به sendEmail/sendSms وصل شوند. ارسال واقعی یا پذیرش لینک کامل در پترن مقصد تست نشده؛ پترن و SMTP را کاربر تنظیم می‌کند. NotConfigured/NotRequested/Sent/Failed فقط نتیجه همان درخواست‌اند، Sent تأیید دریافت provider است نه تحویل نهایی. ساخت دعوت برای retry ارسال تکرار نشود؛ resend لینک قبلی را باطل می‌کند.

Verification: build صفر warning/error؛ suite نهایی 240/240 موفق، صفر Failed/Skipped، PostgreSQL 18.3 اختصاصی loopback:55473 و provider جعلی، SMTP فقط loopback. tests/HireYar.Tests/TestResults/invitation-notifications-verified.trx. دوازده case مستقل اعلان و یک integration دعوت/commit/resend اضافه شدند؛ شکست اولیه فقط assertion متن ایمیل Base64 بود، رمزگشایی تست اصلاح و suite کامل بازاجرا شد. تغییرات موجود docs و InterviewReportService دست‌نخورده حفظ شدند. commit/push/deploy/production/ارسال واقعی انجام نشد.

## بررسی تایم‌اوت ایمیل دعوت — 2026-10-12

بررسی خواندنی روی dev با Git اولیه پاک: تنظیمات مشترک Email.Enabled=true، SMTP=mail.hireyar.com:587، EnableSsl=true (STARTTLS)، TimeoutSeconds=10 و Sms.Enabled=false است؛ تنظیمات اعلان در فایل‌های local یا متغیرهای محیطی این اجرا override ندارند. تنظیمات مؤثر سرور مقصد بررسی نشده‌اند. مسیر دعوت/ویزارد/resend پس از commit ایمیل را منتظر می‌ماند؛ خطای SMTP/timeout فقط Failed می‌شود و جزئیات تشخیصی ثبت نمی‌شود.

DNS خارج sandbox برای mail.hireyar.com دو A عمومی 185.143.233.132 و 185.143.234.132 و MX دامنه به همین mail را برگرداند. probe فقط اتصال TCP، بدون احراز هویت یا ارسال ایمیل، روی پورت‌های 587 و 465 هر دو پس از حدود 6 ثانیه timeout شدند. این شواهد شکست اتصال از محیط فعلی را نشان می‌دهد؛ علت نهایی میان DNS/proxy، سرویس SMTP و فایروال/مسیر شبکه، و رفتار خود سرور مقصد هنوز تفکیک نشده است. مقصد DNS شبیه IPهای anycast آروان است؛ فعال‌بودن proxy باید در پنل DNS بررسی شود، قطعی فرض نشده. Host باید endpoint واقعی SMTP باشد؛ افزایش timeout یا تغییر رمز بدون بررسی اتصال اثبات رفع مشکل نیست. 465 در SmtpClient فعلی implicit TLS پشتیبانی نمی‌شود؛ تغییر صرف Port به 465 راه‌حل نیست.

Build واقعی موفق با صفر warning/error؛ InvitationNotificationTests: 12 موفق، صفر شکست/Skip، SMTP فقط loopback. خطاهای DNS/build اجرای sandbox با اجرای خارج sandbox برطرف شدند و شاهد مشکل DNS واقعی نیستند. کد، تنظیمات و secretها تغییر نکردند؛ ارسال واقعی، تماس احراز هویت SMTP، PostgreSQL، push/deploy و production انجام نشدند. ادامه: بررسی proxy رکورد mail و endpoint/پورت اعلامی سرویس ایمیل و probe اتصال از میزبان API؛ پس از اتصال، بررسی STARTTLS و سپس احراز هویت با حفظ داده حساس.

بررسی تکمیلی SMTP — 2026-10-12: با مجوز صریح کاربر، یک تلاش ارسال ایمیل آزمایشی با تنظیمات مشترک appsettings انجام شد؛ گیرنده و credentials ثبت نشده‌اند. ارسال موفق نشد: SocketException و TimeoutReached=true، زمان واقعی حدود 42 ثانیه با TimeoutSeconds=10. لغو SendMailAsync در مرحله اتصال جاری سقف قطعی 10 ثانیه برای زمان انتظار ایجاد نکرد؛ علت طولانی‌شدن درخواست دعوت نیز می‌تواند همین مرحله باشد. SMTP پذیرش پیام را تأیید نکرد. هیچ دعوتی ساخته نشد، تنظیمات/کد تغییر نکردند و تلاش مجدد خودکار انجام نشد.

## بررسی ترتیب پرسش‌ها — 2026-10-12

کد جاری InterviewPolicy، InterviewSession.GetProgressiveQuestions، CandidateInterviewService و snapshot ویزارد با ADVANCED_QUESTIONS_API تطبیق داده شد: دعوت موضوعی تازه از متوسط اولین موضوع در ترتیب قالب شروع می‌شود؛ correctness>=0.7 و confidence>=0.6 فقط سخت همان موضوع را مجاز می‌کنند، سایر حالت‌ها آسان همان موضوع. بعد از سؤال دوم موضوع بسته است. عمومی ترتیب موضوع قالب را حفظ می‌کند؛ تخصصی با adaptive_questions می‌تواند متوسط موضوع بعد را از shortlist موضوع‌های نپرسیده انتخاب کند و در نبود پیشنهاد معتبر اولین گزینه مرتب‌شده انتخاب می‌شود. سؤال اول همیشه انتخاب سرور است. تعامل clarify/followUp/redirect سؤال اصلی را عوض نمی‌کند. قالب ساده عمومی بر اساس Order است؛ تخصصی بر اساس شواهد کمتر، الزامی/اولویت مهارت، فاصله سطح هدف و سپس Order مرتب می‌شود و AI فقط از گزینه‌های مجاز انتخاب می‌کند. دعوت/resend تاریخی سیاست snapshot قبلی را حفظ می‌کنند.

Build جاری پیش‌تر در همین بررسی‌ها با صفر warning/error موفق بود؛ اجرای مرتبط تازه InterviewRulesTests و ProgressiveTopicRulesTests: 48 موفق، صفر شکست/Skip. بررسی فقط کد و تست محلی بود؛ جلسه واقعی مقصد/Angular بررسی نشده، کد و تنظیمات تغییر نکردند و push/deploy انجام نشد.

## بررسی گزارش انتخاب نادرست سؤال — 2026-10-12

مرور مسیر کامل validator → FinishAnswer → بازانتخاب policy و snapshot ویزارد انجام شد. چهار case تشخیصی به ProgressiveTopicRulesTests اضافه شدند. بازتولید قطعی محلی: دو تصمیم با correctness=0.9 و confidence=0.9؛ اگر پیشنهاد easy همان موضوع مجاز باشد، نمره حفظ و سرور hard را تحمیل می‌کند؛ اگر پیشنهاد medium موضوع دیگر خارج shortlist باشد، validator کل تصمیم را fallback می‌کند، نمره null می‌شود و سرور easy همان موضوع را می‌پرسد. بنابراین خرابی صرف انتخاب nextQuestionId مدل می‌تواند همراه ارزیابی عددی صحیح مسیر را به آسان ببرد. این نتیجه رفتار فعلی رد کل تصمیم نامعتبر است؛ علت جلسه گزارش‌شده بدون شناسه/شواهد مقصد هنوز معلوم نیست و سیاست fallback پذیرفته‌شده بازطراحی نشد.

تست ترتیب نیز تأیید کرد بعد از دو سؤال موضوع اول، HR فقط متوسط موضوع دوم را مجاز می‌کند ولی تخصصی چهار متوسط باقی‌مانده را مجاز می‌کند؛ ترتیب خطی همه موضوع‌ها برای تخصصی تضمین نیست. کنترل سرور برای متوسط→یکی از آسان/سخت همان موضوع و منع سؤال سوم برقرار است. کیفیت تشخیص correctness توسط مدل، متن خروجی STT، صحت محتوای برچسب easy/medium/hard و snapshot تاریخی توسط تست synthetic اثبات نمی‌شوند. تست‌های قبلی نمره مدل جعلی/مستقیم دارند؛ موفقیت آن‌ها اثبات مسیر واقعی AI نیست. ارسال topics=null و بانک تخت، سیاست ساده می‌سازد؛ resend سیاست تاریخی را نگه می‌دارد.

Build تازه صفر warning/error؛ 93/93 تست ProgressiveTopicRulesTests، InterviewRulesTests و InterviewProviderTests موفق، صفر شکست/Skip، شامل چهار case جدید. git diff --check موفق. کد runtime/تنظیمات/DB تغییر نکرد؛ تست PostgreSQL تازه یا AI واقعی/Angular/مقصد اجرا نشد. از کاربر نوع انحراف، شناسه جلسه و زمان تست درخواست شد؛ ادامه برای تعیین علت واقعی با flagهای snapshot، difficulty/topic سؤال‌های واقعی، grade ذخیره‌شده و SelectionSource/FailureCode پالایش‌شده همان جلسه است. push/deploy/production انجام نشد.

## اصلاح انتخاب سؤال با حفظ ارزیابی معتبر — 2026-10-12: Verified محلی

با درخواست صریح کاربر، در InterviewDecisionValidator خطای صرف nextQuestionId برای advance معتبر در جلسه progressive به حذف پیشنهاد سؤال محدود شد؛ نمره/اطمینان و شواهد معتبر حفظ می‌شوند. CandidateInterviewService پس از FinishAnswer همان policy موجود را enforce می‌کند: متوسط صحیح به سخت همان موضوع، ضعیف/اطمینان پایین به آسان، سپس متوسط موضوع تازه. diagnostics موجود policy_reselection انتخاب سرور را مشخص می‌کند. JSON، ارزیابی یا شواهد نامعتبر همچنان fallback کامل هستند؛ تعامل با nextQuestionId غیرnull رد می‌شود و رفتار غیرprogressive محفوظ است. ADR-039 و ADVANCED_QUESTIONS_API قرارداد جاری‌اند. بازتولید قدیمیِ حذف نمره در بخش بررسی قبل، وضعیت پیش از این اصلاح است.

تست تشخیصی به regression اصلاح تبدیل شد: پیشنهاد آسان همان موضوع، موضوع دیگر، UUID ناشناخته، سؤال جاری و null با نمره معتبر؛ آستانه دقیق و پاسخ ضعیف/اطمینان پایین؛ حفظ شواهد و جلوگیری از پذیرش نمره نامعتبر/ناقص، مهارت نامرتبط و تعامل نامعتبر. تست API واقعی PostgreSQL نیز پیشنهاد UUID ناشناخته را بعد از clarify ارسال و ذخیره correctness/confidence=0.8، انتخاب hard، policy_reselection و ادامه موضوع‌های بعد تا پایان را بررسی کرد.

Build نهایی صفر warning/error؛ 150/150 تست مرتبط rules/provider/context/advanced/API flow موفق، صفر شکست/Skip؛ PostgreSQL 18 اختصاصی فقط loopback:55479، مدل/STT/TTS جعلی و داده مصنوعی. فایل نتیجه tests/HireYar.Tests/TestResults/question-selection-fix.trx. git diff --check موفق. تغییر DB/schema/migration یا تنظیمات لازم نیست؛ کیفیت نمره AI واقعی، Angular و علت جلسه گزارش‌شده مقصد هنوز تأیید نشده‌اند. تغییرات موجود مستندات/تست قبلی حفظ شدند؛ commit/push/deploy/production انجام نشد.
