# تکمیل بک‌اند گزارش مرجع HTML — 2026-10-10

تأیید محلی نهایی: build بدون warning/error و ۲۵۰/۲۵۰ تست suite کامل روی PostgreSQL 18 اختصاصی، صفر شکست/Skip؛ `tests/HireYar.Tests/TestResults/report-v3-verified.trx`. model drift=false، constraint سازمانی و محافظ downgrade واقعی آزموده شدند؛ SQL idempotent کامل دوبار روی scratch و یک migration history entry تأیید شد. provider و رسانه آزمون مصنوعی بودند؛ AI/ضبط واقعی و frontend مقصد تأیید نشده‌اند.

این قرارداد افزوده report-v3 است؛ Angular در ریپوی جدا است. HTML ارائه‌شده مرجع محصول است، پرامپت و یادداشت‌های داخل آن دستور اجرایی مستقل نیستند. یک تماس مدل گزارش برای تمام ارزیابی‌ها حفظ شد؛ تماس به‌ازای هر سؤال و جدول موازی نمرات اضافه نشد. JSONB موجود خروجی کامل را ذخیره می‌کند. مدل‌ها، کلیدها، پرداخت و نمرات زنده تغییر نکردند.

## گزارش و محاسبه

GET/POST `/api/interviews/{id}/evaluation-report` همان مسیر قبلی‌اند. POST موفق تکراری گزارش cached v1/v2/v3 را حفظ می‌کند. برای تولید نسخه جدید یا اعمال وزن‌ها، Owner صریحاً POST `/api/interviews/{id}/evaluation-report/regenerate` با بدنه زیر می‌فرستد:

```json
{ "topicWeights": { "SQL": 2, "Linux": 1 }, "reviewThreshold": 70 }
```

بدنه `{}` معتبر است. کلید وزن باید نام دقیق موضوع پرسیده‌شده باشد؛ وزن در بازه بزرگ‌تر از صفر تا ۱۰۰، آستانه اختیاری ۰ تا ۱۰۰ است. موضوعِ بدون وزن صریح وزن ۱ دارد. وزن‌ها انتخاب انسانی‌اند؛ از JD حدس زده نمی‌شوند و مقدار Priority بانک، وزن نمره نیست. settings جدید فقط در بازتولید صریح اعمال می‌شوند. نتیجه و settings در همان JSONB persist می‌شوند.

`details.overallScore = sum(answerScore*topicWeight)/sum(topicWeight)*20` با گردکردن دو رقم؛ فقط پاسخ‌های نهایی ارزیابی‌شده در مخرج‌اند. وزن هر پاسخ وزن موضوع آن است. topics همچنان میانگین نمره همان موضوع هستند؛ در مسیر موضوعی یک سؤال، همان نمره سؤال. `topicWeights`، `scoreFormula` و `reviewThreshold` در details موجودند. نمره زیر آستانه فقط requiresHumanReview را true می‌کند؛ به تصمیم عبور/رد خودکار منجر نمی‌شود. excellent/good/weak و aboveThresholdPercent همچنان آستانه سؤال ۳ از ۵ دارند و با آستانه کل گزارش متفاوت‌اند.

بازتولید زیر lease موجود است؛ POST regenerate همزمان 409 می‌دهد. GET و POST cached حین بازتولید گزارش قبلی را می‌دهند؛ شکست، cancellation یا lease کهنه آن را حذف نمی‌کند. فقط نتیجه معتبر generator جاری جایگزین می‌شود؛ زمان/model/version جدید ثبت می‌شوند. report قبلی به‌صورت نسخه تاریخی جدا آرشیو نمی‌شود، اما سابقه تصمیم انسانی مستقل و محفوظ است.

`content.metrics` و `details.metrics` شامل code، score صحیح nullable ۰–۵، confidence nullable ۰–۱، explanation و evidence از `{answerId,quote}` هستند. برای نمودار، score را ×۲۰ کنید. تمام کدهای مربوط به نوع مصاحبه دقیقاً یک بار لازم‌اند. نمره غیر null به نقل‌قول دقیق پاسخ نهایی همان جلسه نیاز دارد؛ تعامل صرف، ID/quote ساختگی، کد اضافی/تکراری، confidence نامعتبر و نمره بدون شاهد رد می‌شوند. بدون شواهد score/confidence=null، evidence=[] و explanation درباره محدودیت است؛ صفر جایگزین نامشخص نیست.

| نوع | code | معیار |
| --- | --- | --- |
| تخصصی | knowledge_depth | فهم سازوکار، مبانی و مثال عملی در سطح سؤال واقعاً پرسیده‌شده |
| هر دو | communication | ساختار و روشنی متن پاسخ؛ بدون استنتاج از لهجه، صدا یا ظاهر |
| تخصصی | job_alignment | شواهد مهارتی در مقابل الزامات صریح JD؛ JD ناموجود = نامشخص |
| عمومی | motivation | دلایل و اهداف صریح مرتبط با شغل |
| عمومی | teamwork | نمونه‌های همکاری واقعی در پاسخ |
| عمومی | working_practices | روش‌های کاری مرتبط با انتظارات نقش؛ جایگزین سنجش حدسی شخصیت/شباهت فرهنگی |
| عمومی | responsibility | پاسخ‌گویی و پیگیری با نمونه رفتاری |
| عمومی | conflict_management | مراحل مشخص حل اختلاف |
| عمومی | documentation | سازمان‌دهی و ثبت مستندات مرتبط با کار |

عمومی دقیقاً هفت شاخص و تخصصی سه بعد دارد؛ کیفیت پاسخ همان نمرات سؤال/کل است. `details.fitScore` فقط نمره job_alignment×۲۰ است و در عمومی/نبود شاهد null می‌ماند؛ این احتمال موفقیت استخدام یا fit آماری کالیبره‌شده نیست. اطمینان پایین/شاخص فاقد شاهد requiresHumanReview=true می‌دهد. schema و citation به‌تنهایی درستی معنایی یا کیفیت فارسی را تضمین نمی‌کنند؛ آزمون مدل واقعی لازم است. `averageAnswerSeconds` میانگین durationSeconds موجود پاسخ‌های نهایی پردازش‌شده، یا null است؛ زمان اعلامی کلاینت و مستقل از billing است.

## تصمیم انسانی و رویدادها

Owner سازمان: GET `/api/interviews/{id}/review`؛ POST `/api/interviews/{id}/decisions`:

```json
{ "requestId": "UUID غیرخالی", "decision": "advance", "note": "یادداشت اختیاری تا ۲۰۰۰ کاراکتر" }
```

decision فقط advance/review/reject، فقط جلسه Completed. ActorUserId از هویت بررسی‌شده Owner و زمان از سرور است. هر تصمیم جدید به تاریخچه افزوده می‌شود؛ تصمیم قبلی overwrite نمی‌شود و نمره/جلسه/مالی یا دعوت تغییر نمی‌کنند. advance صرفاً ثبت تصمیم مرحله بعد است، دعوت یا پیام واقعی ارسال نمی‌کند. همان requestId و payload replay، payload متفاوت 409؛ سقف مستقل ۱۰۰ تصمیم انسانی و ۱۰۰۰ رویداد داوطلب در هر جلسه، زیر قفل کوتاه سازمان است.

داوطلب با Bearer دعوت: POST `/api/candidate/interviews/{interviewId}/events`:

```json
{ "requestId": "UUID غیرخالی", "kind": "consent_accepted", "occurredAtUtc": "UTC", "consentVersion": "consent-v1" }
```

kind مجاز: consent_accepted، consent_revoked، microphone_disconnected، camera_disconnected، page_hidden، recording_started، recording_stopped. consentVersion فقط برای accepted الزامی و حداکثر ۱۰۰ کاراکتر است؛ سایر kindها آن را نمی‌پذیرند. زمان UTC اعلامی باید از زمان ایجاد دعوت تا اکنون+۳۰ثانیه باشد؛ RecordedAtUtc مستقل زمان دریافت سرور است. replay دقیق با همان requestId ثبت دوباره ندارد. هیچ نمره منفی خودکار یا ادعای پایش مستقل دستگاه ایجاد نمی‌شود.

review.entries سابقه را با زمان سرور و کلاینت جدا می‌دهد؛ consentStatus=unknown/accepted/revoked از آخرین رویداد رضایت بر حسب زمان کلاینت است. این‌ها گزارش کلاینت‌اند. برای جلسات تاریخی backfill حدسی نداریم؛ نبود رویداد به معنی نبود قطعی یا رضایت نیست. دسترسی سازمان دیگر/حذف = 404؛ candidate به review/decisions مالک دسترسی ندارد.

## ضبط جلسه و زمان‌بندی

POST multipart `/api/candidate/interviews/{interviewId}/recording`: video الزامی WebM/MP4 حداکثر ۵۱۲ MiB؛ uploadId UUID ثابت غیرخالی؛ durationSeconds از ۱ تا ۱۴۴۰۰؛ timeline رشته JSON حداکثر ۳۲۰۰۰ کاراکتر:

```json
[{ "answerId": "UUID پاسخ موجود همین جلسه", "startSeconds": 12.5, "endSeconds": 42 }]
```

حداکثر ۲۰۰ answerId یکتا؛ ۰≤start<end≤duration. Offset نسبت به آغاز همین فایل و توسط recorder فرانت تولید می‌شود؛ askedAtUtc/lastSpeechEndUtc به‌صورت حدسی به offset تبدیل نمی‌شوند. فایل مستقل در storage خصوصی موجود ذخیره می‌شود، یک فایل immutable برای هر جلسه؛ replay همان uploadId+timeline+duration upload دوم ندارد؛ metadata متفاوت 409. این replay هویت درخواست است، مقایسه بایت‌های فایل دوباره‌ارسالی نیست. فایل‌های بازنده رقابت/شکست commit پاک می‌شوند. قفل DB کوتاه، upload بیرون transaction و recheck حذف/ابطال/انقضا/retention پس از آن است.

پس از start مجاز است؛ برای پایان خودکار، فقط events و recording با POST تا ده دقیقه بعد CompletedAtUtc با همان دعوت معتبر پذیرفته می‌شوند. شروع/پاسخ جدید یا current در این فرصت مجاز نیستند. ابطال/جایگزینی/انقضای دعوت همچنان مقدم است. فرانت آخر ضبط را upload و سپس complete کند یا در فرصت ده‌دقیقه‌ای پایان خودکار upload کند؛ پس از آن راه upload با دعوت وجود ندارد. uploadId را برای retry حفظ کند.

GET review برای Owner `recording={videoUrl,durationSeconds,timeline,source:"candidate_reported"}` می‌دهد. GET `/api/interviews/{id}/recording/video` با Bearer Owner و range processing و retention همان قرارداد موجود است. recordingStatus بدون فایل unknown و با فایل uploaded_unverified است؛ signature و طول واقعی upload بررسی می‌شوند اما decode، پیوستگی، همگامی صوت/تصویر یا کامل‌بودن ضبط اثبات نشده‌اند. محدودیت reverse proxy مقصد باید با سقف API سازگار باشد؛ مقصد این نوبت تغییر نمی‌کند.

## چاپ، PDF و اتصال فرانت

GET Owner `/api/interviews/{id}/evaluation-report/print` خروجی HTML فارسی RTL با print CSS، summary/topics/metrics/سؤال‌ها/شواهد/محورهای مرحله بعد/محدودیت‌ها و وضعیت واقعی review دارد. فرانت آن را با Bearer fetch کند و به‌صورت Blob در پنجره چاپ نمایش دهد؛ Print / Save as PDF مرورگر خروجی PDF می‌سازد. متن untrusted HTML-encode، اسکریپت و منابع بیرونی با CSP بسته، پاسخ no-store است. سرویس server-side PDF و آپلود PDF در storage اضافه نشده؛ گزینه print CSS طرح انتخاب شده است.

فرانت جدا باید: نمودار HR/تخصصی را از metrics با null=شواهد ناکافی بسازد؛ وزن‌ها/آستانه را برای regenerate صریح بفرستد؛ دکمه‌های تصمیم را به decisions وصل کند؛ قبل ضبط consent و هنگام رخداد events را ثبت کند؛ recorder جلسه و timeline مبتنی بر media clock بسازد؛ recording/video را با Bearer fetch و currentTime=offset تنظیم کند. URL خصوصی را در video src بدون روش تأمین Bearer قرار ندهد. مقایسه آماری معتبر داوطلبان، پیشنهاد استخدام خودکار، ادعای ضبط کامل یا داده نمونه ثابت در UI نمایش داده نشود.

اشتراک بیرون سازمان با endpoint ناشناسِ توکن‌دار توسط بازبینی خودکار ایمنی رد شد؛ هیچ کد/جدول/link sharing اعمال نشده است. دلیل: محتوای حساس داوطلب و نبود مجوز صریح خروج داده/مخاطب/روش اشتراک. ادامه این بخش به مجوز صریح کاربر وابسته است؛ توکن/لینک حساس نباید در Git یا لاگ ثبت شود.

طرح قابل تأیید بخش متوقف‌شده: Owner برای گزارش ذخیره‌شده snapshot اشتراک می‌سازد؛ پیش‌فرض انقضا ۲۴ ساعت، قابل ابطال توسط همان Owner، بدون دسترسی صوت/ویدیو و بدون ارسال پیام خودکار. فقط افرادی که Owner لینک را به آن‌ها می‌دهد با توکن تصادفی قوی آن snapshot را می‌خوانند؛ hash توکن persist، توکن در fragment لینک فرانت و سپس Authorization header API، نه query/لاگ/Git. حذف جلسه یا ابطال/انقضا، خواندن را می‌بندد. این صرفاً طرح درخواست مجوز است، نه قابلیت اعمال‌شده یا فعال.

## migration

`20261010054552_AddInterviewReportReview` جدول‌های InterviewReviewEntries و InterviewSessionRecordings را با FK مرکب سازمان/جلسه اضافه می‌کند؛ constraint گزارش را برای نگهداری نتیجه قبلی همراه lease بازتولید باز می‌کند. داده تاریخی تغییر نمی‌کند؛ مدل گزارش v3 در JSONB موجود است. Down در حضور سابقه/ضبط یا بازتولید فعال با گزارش قبلی رد می‌شود. migration مقصد/production جدا از این تغییر و هنوز اجرا نشده است.
