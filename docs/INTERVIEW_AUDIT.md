# Candidate AI Interview — بررسی پیش از staging

تاریخ: 2026-10-01؛ branch: `codex/candidate-ai-interview`. بررسی روی کد، mapping، migration و تست واقعی انجام شد؛ نتیجه‌های تاریخی مستندات مبنای تأیید نبودند. تغییرهای مستندی نوبت قبل حفظ شدند. قابلیت اصلی، endpoint یا migration جدید اضافه نشده است؛ push/deploy انجام نشد.

## A. نقص‌های اثبات‌شده و اصلاح‌شده

### 1. قطع جریان دریافت پاسخ provider

- Severity: Medium.
- File: `src/HireYar.Infrastructure/Services/Interviews/NetArzClient.cs`.
- Problem: IOException هنگام خواندن response stream به InterviewProviderException تبدیل نمی‌شد.
- Impact: در LLM، fallback اجرا نمی‌شد و درخواست با 500 و lease فعال باقی می‌ماند؛ در STT کد خطا به‌اشتباه audio_unavailable محسوب می‌شد. telemetry نیز این مسیر را success گزارش می‌کرد.
- Fix: تبدیل IOException به کد امن `{operation}_unavailable` و outcome=network_failure؛ cancellation درخواست همچنان حفظ می‌شود.
- Test: Broken_response_stream_is_safe_and_typed برای هر سه عملیات قبل از fix شکست خورد و پس از fix موفق شد. تست شبکه/محرمانگی لاگ نیز برای هر سه عملیات موفق است.

### 2. خطای خواندن فایل cache سؤال

- Severity: Medium.
- File: `src/HireYar.Application/Services/Interviews/QuestionAudioService.cs`.
- Problem: خطاهای IOException و UnauthorizedAccessException خواندن cache بیرون مسیر graceful failure قرار داشتند؛ فقط نبود فایل مدیریت می‌شد.
- Impact: current/start/submit ممکن بود با وجود سؤال معتبر متنی 500 بدهد؛ TTS optional بود ولی خرابی storage خواندن آن جریان را متوقف می‌کرد.
- Fix: cache غیرقابل خواندن audioAvailable=false می‌دهد؛ متن باقی می‌ماند و فقط failureCode عمومی log می‌شود. فایل واقعاً مفقود همچنان در مسیر generate قابل بازسازی است.
- Test: Unreadable_cached_question_audio_does_not_stop_the_text_flow روی PostgreSQL، برای خطای I/O و دسترسی و حالت generate/read-only؛ قبل از fix شکست و پس از fix موفق.

### 3. کلید واقعی در مقدار پیش‌فرض کد

- Severity: High.
- File: `src/HireYar.Infrastructure/Services/Interviews/NetArzOptions.cs`.
- Problem: ApiKey مقدار غیرخالی در فایل تحت Git داشت؛ تنظیم‌نکردن کلید باعث رد startup نمی‌شد. تست الزام کلید شکست داشت.
- Impact: secret در source/نسخه کامپایل‌شده قرار می‌گرفت و اعتبارسنجی نبود تنظیم واقعی بی‌اثر بود.
- Fix: default خالی شد؛ مقدار تنظیم‌شده در فایل ignored `src/HireYar.Api/appsettings.Development.local.json` حفظ شد، بدون نمایش یا انتقال به مستندات. NetArz در همان تنظیمات محلی Development فعال شد؛ آدرس و مدل‌های موجود تغییر نکردند. default provider مشترک همچنان Disabled است.
- Test: Enabled_provider_requires_key_and_valid_timeouts اکنون موفق؛ مسیر local settings با git check-ignore بررسی شد؛ smoke واقعی با کلید محلی موفق بود.
- Limit: حذف initializer تاریخچه Git قبلی را پاک نمی‌کند. کلید قبلی باید در حساب provider تعویض شود؛ این audit کلید را revoke/rotate نکرد.

## مسیر واقعی و شواهد بررسی

| مرحله | نتیجه بررسی |
|---|---|
| HR create application/skill/bank/invite | JWT و نقش Owner/Recruiter، scope از ICurrentUser؛ ورودی OrganizationId نداریم؛ موقعیت، application، template و skill در سازمان/موقعیت مربوط بررسی می‌شوند. snapshot در invite ثابت است. |
| token | ۳۲ بایت RandomNumberGenerator؛ SHA-256 در DB؛ unique hash index؛ مقایسه FixedTimeEquals؛ expiry/revoke server-side. claim فقط شناسه جلسه است. |
| start/current | شناسه مسیر باید با claim معتبر جلسه برابر باشد؛ application update scoped؛ start یک commit کوتاه دارد؛ current سؤال جدید انتخاب نمی‌کند. |
| audio submit | وضعیت Waiting/current/time قبل از ذخیره بررسی می‌شود؛ signature/MIME/extension/size؛ key سرور مستقل از filename. سپس answer و lease همراه تغییر Session در یک SaveChanges ذخیره می‌شوند. |
| STT/transcript | Transcribing پیش از تماس خارجی commit می‌شود؛ پس از پاسخ، session reload و access/lease دوباره بررسی می‌شود؛ transcript جدا commit می‌شود. شکست STT پاسخ retryable همان فایل، بدون سؤال بعدی است. |
| shortlist/LLM/validation | shortlist دقیق attempt؛ LLM بیرون transaction؛ JSON سخت‌گیر، شناسه/موضوع/محدوده امتیاز و سؤال انتخابی بررسی می‌شوند. پیشنهاد AI شرط پایان را دور نمی‌زند. |
| evidence/advance/complete | پس از reload و lease check؛ assessment قبل از mutation کامل بررسی می‌شود؛ evidence، skill state، answer Processed، advance/complete و release lease در یک SaveChanges اتمیک ذخیره می‌شوند. policy پس از evidence دوباره اعمال می‌شود. |
| question audio | cache با organization + hash؛ private file؛ شکست provider/storage متن را حفظ می‌کند. Candidate فقط current یا سؤال پاسخ‌داده‌شده همان جلسه را می‌خواند؛ HR فقط سازمان خود. |
| API contract | status، question/text/audio، flags، progress، answer/retry و completionReason موجودند؛ transcript/evidence/کلید provider در DTO داوطلب نیست. قرارداد تغییر نکرد. |

Query کاندیدا با Id تنها، بعد از تطبیق Id با claim احرازشده است؛ scope آن **جلسه** است و فقدان OrganizationId در همان query نقص tenant محسوب نشد. OrganizationId تماس‌های بعدی از جلسه معتبر استخراج می‌شود. Query authentication با hash معتبر token آغاز می‌شود. HR queryها organization-scoped هستند؛ childهای session از aggregate احرازشده گرفته می‌شوند.

هیچ BeginTransaction/TransactionScope در مسیر Interview وجود ندارد. SaveChangesهای مراحل پردازش commitهای کوتاه و تمام‌شده‌اند؛ STT/LLM/TTS و ذخیره فایل بین آنها اجرا می‌شوند. transaction قالب قدیمی فقط delete/insert سؤال‌های همان قالب است و تماس AI ندارد. EF logging حساس فعال نشده؛ HTTP logger پیش‌فرض NetArz حذف شده و logger اختصاصی فقط عملیات/model/status/مدت و شناسه‌های داخلی/کد خطا را می‌نویسد. exception/body/prompt/audio/transcript/Authorization/ApiKey در logging این ماژول چاپ نمی‌شوند؛ تست logger خطای شبکه نیز این را بررسی می‌کند.

### concurrency و recovery

تست تازه دو درخواست را **پس از خواندن WaitingForAnswer و ذخیره فایل، پیش از commit** هم‌زمان کرد: یک winner، یک DbUpdateException، یک answer، یک STT، یک evidence، یک تغییر سؤال و حذف فایل loser. unique session/question و RowVersion هر دو در PostgreSQL بررسی شدند. تست lease جدید نیز worker تازه را پیش از برگشت worker قدیمی commit کرد؛ worker قدیمی 409 گرفت و evidence/سؤال را تغییر نداد.

| crash point | بازیابی بررسی‌شده |
|---|---|
| فایل و answer ذخیره شده، پیش از STT | پس از lease expiry، current canRetry=true؛ retry همان answer/audio، STT یک بار در attempt بازیابی. |
| transcript commit شده، پیش از LLM | retry از transcript استفاده می‌کند؛ STT دوباره اجرا نمی‌شود. |
| Evaluating، خروجی AI پیش از commit نهایی از دست رفته | خروجی تصمیم persist نشده و LLM می‌تواند دوباره اجرا شود؛ هیچ evidence پیش از commit موجود نیست. retry یک بار evidence و advance ثبت می‌کند؛ retry دوباره پاسخ Processed بی‌اثر است. |
| lease منقضی | worker تازه lease می‌گیرد؛ worker قبلی پس از reload اجازه commit ندارد. |
| فایل ذخیره شده ولی answer هنوز commit نشده | رکورد retry وجود ندارد؛ client باید current را بخواند و در صورت آماده بودن سؤال دوباره submit کند. فایل orphan ممکن است بماند؛ نیاز به retention/cleanup عملیاتی دارد. |

قطع ناگهانی process واقعاً با kill شبیه‌سازی نشده؛ حالت‌های persistشده متناظر در PostgreSQL ایجاد و بازیابی آزموده شدند. شکست commit نهایی DB state را اتمیک نگه می‌دارد؛ دسترسی پس از expiry/revoke عمداً ادامه پردازش را اجازه نمی‌دهد.

### fallback و امنیت صوت

تست provider برای timeout، network failure، قطع response stream، HTTP 401/429/500، envelope ناقص/JSON خراب، STT نامعتبر و TTS نامعتبر اجرا شد. تست سرویس، LLM failure/JSON نامعتبر → SelectionSource=Fallback را پوشش می‌دهد؛ validator برای SkillId، Topic، confidence/score، null assessment، سؤال ناموجود/پرسیده‌شده/خارج shortlist بدون هیچ update معتبرِ جزئی fallback می‌کند. STT failure fallback سؤال ایجاد نمی‌کند و retry همان answer را حفظ می‌کند.

تست‌های صوت zero/oversize/duration، extension/MIME/signature ناسازگار، traversal و storage round-trip را پوشش می‌دهند. root خارج wwwroot، key با الگوی محدود و بررسی symlink/junction است. نام client physical path نمی‌شود. candidate token روی HR audio/report رد شد؛ token جلسه A روی جلسه واقعی B و سؤال آینده رد شد؛ HR سازمان دیگر به answer/question audio دسترسی نداشت؛ spoof کردن OrganizationId ورودی نیز 400 بود.

محدودیت موجود: signature ابتدایی، اعتبار کامل codec/container یا مدت واقعی صوت را اثبات نمی‌کند؛ durationSeconds ادعای client است. سقف bytes و زمان جلسه server-side است. decoder/worker جدید طبق محدوده درخواست اضافه نشد.

### migration

Migration `20261001061608_AddCandidateInterviewFlow`، mapping و snapshot بررسی شدند. Up فقط ۱۲ جدول جدید و alternate key قالب اضافه می‌کند؛ drop/recreate جدول قبلی، UPDATE/DELETE داده قبلی یا cascade ندارد. nullableها برای metadata مراحل نیمه‌کاره‌اند؛ status/limits/score check دارند. FKهای tenant به موقعیت/template/application/PositionSkill مرکب و Restrict هستند؛ childها با session به snapshot skill/question/answer وصل‌اند. ترتیب ایجاد parent قبل از child و ترتیب Down برعکس وابستگی‌هاست. Down داده جدول‌های قابلیت را حذف می‌کند و برای rollback محیط دارای داده نباید بی‌بررسی اجرا شود.

RowVersion از نوع bytea، concurrency token برنامه‌ای ۱۶بایتی است؛ timestamp SQL Server نیست. hash index و session/question answer index unique هستند. schema این audit تغییر نکرد؛ migration جدید لازم نیست. fixture همه migrationهای موجود از DB تازه را اعمال کرد؛ tenant FK، unique answer و دو context concurrent با PostgreSQL واقعی موفق بودند. `has-pending-model-changes` هیچ اختلافی گزارش نکرد. طبق گفته کاربر migration مقصد قبلاً اجرا شده؛ این audit DB مقصد را بررسی یا دوباره migrate نکرد.

## B. blockerهای پیش از staging

در مسیر backend بررسی‌شده، نقص کدی اصلاح‌نشدهٔ blocker پیدا نشد. **کلیدی که پیش‌تر در Git بوده باید تعویض و فقط خارج Git در staging تنظیم شود**؛ پاک‌کردن default کافی نیست. امکان انجام rotation از این workspace وجود ندارد و انجام‌شدن آن تأیید نشده است.

staging باید AI:Provider=NetArz، secret جدید و storage خصوصی قابل نوشتن داشته باشد. local Development در این audit فعال شد؛ این تنظیم به staging منتقل نمی‌شود. تست Live فقط سه درخواست مصنوعی موفق بوده و ظرفیت/کیفیت طولانی‌مدت provider یا deploy واقعی را اثبات نمی‌کند.

## C. کار عملیاتی پیش از production

- volume خصوصی پایدار؛ دسترسی فقط حساب سرویس، backup هماهنگ فایل و DB و تست restore. چند instance باید همان storage خصوصی را ببینند.
- مدت نگهداری صوت/transcript/evidence و پاکسازی orphan با تطبیق مراجع DB؛ cleanup خودکار این مرحله ساخته نشد.
- کلید جدید خارج Git، گردش secret و محدودکردن دسترسی لاگ/گزارش HR. migration مقصد طبق روند استقرار بررسی شود.
- rate limit فعلی داوطلب ۶۰ درخواست در دقیقه برای هر جلسه و برای درخواست بدون claim بر اساس IP است؛ محدودیت لبه شبکه/اندازه body و timeout proxy باید با پردازش طولانی هم‌خوان شود. سازگاری ظرفیت در این audit load-test نشد.
- `/health` فعلی liveness ساده است؛ بررسی آماده بودن PostgreSQL/storage/اعتبار حساب provider و alert روی failureCode/latency در تنظیمات عملیاتی لازم است. transcript/token وارد telemetry نشود.

## D. بهبودهای اختیاری آینده

پیش‌تولید TTS و کاهش cache miss هم‌زمان، decode کامل صوت برای کنترل مدت واقعی، و ابزار مدیریتی recovery/retention در درخواست مستقل قابل بررسی‌اند. Angular، ATS، پیام دعوت، background worker، S3 و streaming/WebSocket در این iteration ساخته نشدند.

## نتیجه اجرا

- Restore: passed؛ dotnet-ef 10.0.12 بازیابی شد.
- Build: passed؛ Warnings=0، Errors=0.
- Tests: **118 passed، 0 failed، 0 skipped**؛ تمام مجموعه با PostgreSQL 18 واقعی loopback:55441.
- PostgreSQL tests executed: **yes**؛ fixture دیتابیس‌های اختصاصی تصادفی را ساخت و حذف کرد؛ SQLite/InMemory استفاده نشد.
- Pending model changes: **no**.
- Live NetArz: **TTS/STT/LLM passed**؛ صوت مصنوعی 168960 بایت، متن STT با 117 کاراکتر و decision معتبر با یک skill update؛ محتوای متن، صوت و کلید در گزارش ثبت نشده‌اند. تماس‌ها مستقیم با client واقعی بودند؛ کل جریان HTTP/DB از provider آزمایشی استفاده کرد.
- تست‌های regression پیش از fix: چهار شکست قابل بازتولید؛ نتیجه 118 موفق، مربوط به اجرای نهایی پس از اصلاح است. اجرای اول کامل در sandbox اتصال PostgreSQL را reset کرد؛ اجرای نهایی با دسترسی مجاز به loopback موفق شد.
- فایل‌های بررسی محلی: `artifacts/audit-all.log` و `tests/HireYar.Tests/TestResults/audit-all.trx`؛ runner smoke در artifacts و خارج Git است. این فایل‌ها برای commit لازم نیستند.

هیچ secret یا داده واقعی داوطلب در تست‌های اضافه‌شده استفاده نشد. تغییر migration مقصد، commit، push یا deploy انجام نشد.
