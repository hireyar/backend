# معرفی صوتی و صوت مرحله‌های ثابت — 2026-10-07

قرارداد بک‌اند `dev` برای تکمیل فرانت. migration `20261007151257_AddVoiceIntroduction` پس از migration زمینه/Recovery لازم است. Angular، تنظیمات provider/کلیدها و محیط مقصد در این تغییر دست‌نخورده‌اند.

## دریافت صوت معرفی

```http
POST /api/candidate/interviews/{interviewId}/introduction
Authorization: Bearer <candidate token>
Content-Type: multipart/form-data
```

| فیلد | الزام | توضیح |
|---|---|---|
| `audio` | الزامی | فایل جداگانه با همان محدودیت size/type/signature پاسخ سؤال |
| `clientTurnId` | الزامی | رشته opaque غیرخالی تا ۱۲۸ کاراکتر، بدون فاصله ابتدا/انتها؛ برای replay همان مقدار |
| `durationSeconds` | اختیاری | مثبت و زیر سقف `mediaCapabilities.maxAnswerSeconds`؛ مبنای مالی نیست |
| `lastSpeechEndUtc` | اختیاری | UTC؛ حداکثر ۳۰ ثانیه جلوتر از سرور و نه قبل از شروع مرحله با تحمل ۳۰ ثانیه |
| `video` | اختیاری | WebM/MP4 طبق `mediaCapabilities.videoAnswers/maxVideoBytes` |

`questionId` ندارد. فقط در CandidateIntroduction و WaitingForAnswer با زمان باقی‌مانده و billing باز پذیرفته می‌شود. پیش‌فرض صوت ۲۰ MiB، ویدیو ۴۰ MiB و مدت ۶۰۰ ثانیه است؛ سقف مؤثر از mediaCapabilities خوانده شود.

فایل‌ها خارج transaction ذخیره می‌شوند. رکورد مستقل معرفی و lease زیر قفل کوتاه سازمان، پیش از STT commit می‌شوند. transcript غیرخالی معتبر در جلسه ذخیره می‌شود و فقط بعد از آن مرحله Questions می‌شود. متن خالی/بیش‌ازحد یا شکست STT، Failed ایجاد می‌کند؛ متن ساختگی جایگزین نمی‌شود. اعتبارسنجی حاضر متن خروجی STT را بررسی می‌کند؛ تشخیص آکوستیکی سکوت/VAD یا کیفیت واقعی provider با تست‌های مصنوعی تأیید نشده است.

معرفی در InterviewAnswers نیست، progress/evidence/نمره را افزایش نمی‌دهد و LLM ارزیابی آن فراخوانی نمی‌شود. transcript به زمینه ارزیابی سؤال‌های بعدی می‌رسد. سیاست مالی قبلی حفظ شده؛ معرفی زمان پاسخ سؤال یا مصرف مالی تازه ایجاد نمی‌کند.

## Replay و recovery

در هر جلسه یک معرفی صوتی پذیرفته می‌شود. ارسال همان clientTurnId همان introduction.id و وضعیت جاری را بدون STT دوباره برمی‌گرداند. در دو upload هم‌زمان قفل و unique جلسه یک رکورد می‌پذیرند و فایل بازنده حذف می‌شود. شناسه متفاوت پس از پذیرش `409 introduction_already_submitted` است؛ استفاده از شناسه معرفی برای پاسخ سؤال `409 client_turn_id_conflict` می‌دهد.

پس از خطای انتقال ابتدا current را بخوانید. اگر introduction موجود است، upload دوباره نکنید؛ اگر رکوردی وجود ندارد، همان ارسال را با همان clientTurnId تکرار کنید. پردازش دوباره فایل پذیرفته‌شده، بدون body یا فایل:

```http
POST /api/candidate/interviews/{interviewId}/introduction/{introductionId}/retry
Authorization: Bearer <candidate token>
```

شناسه خارج از جلسه 404 و retry معرفی تکمیل‌شده idempotent است. هنگام backgroundRetryPending=true، retry دستی `409 background_retry_pending` می‌دهد. lease فعال مانع پردازش هم‌زمان و نتیجه lease منقضی مانع overwrite نتیجه تازه است. قطع درخواست پس از commit فایل را نگه می‌دارد؛ worker پس از انقضای lease فایل ذخیره‌شده را می‌خواند.

worker موجود معرفی را با pair سازمان/جلسه و بررسی دوباره stage/status، زمان، ابطال/حذف و billing claim می‌کند. گزینه‌های مشترک InterviewAudio: BackgroundRecoveryEnabled، RecoveryPollSeconds، MaxRecoveryAttempts و LeaseSeconds هستند. پیش‌فرض poll پانزده ثانیه و سه recovery خودکار است؛ backoff شکست نخست ۳۰ ثانیه و شکست‌های بعدی ۶۰/۱۲۰/۲۴۰ ثانیه است. retry پس از شکست/قطع STT ممکن است provider را دوباره فراخوانی کند، اما رکورد/مصرف مالی تازه ایجاد نمی‌کند.

مسیر JSON قبلی stage/advance برای معرفی متنی کلاینت قدیمی محفوظ است. وقتی معرفی صوتی پذیرفته شده و مرحله هنوز معرفی است، این مسیر `409 introduction_audio_pending` می‌دهد و اجازه دورزدن STT را نمی‌دهد.

## current و پاسخ‌های state

فیلدهای افزوده: stageAudioUrl و introduction. introduction شامل:

`id`, `clientTurnId`, `status`, `transcript`, `canRetry`, `errorCode`, `recoveryAttempts`, `backgroundRetryPending`, `submittedAtUtc`, `lastSpeechEndUtc`, `durationSeconds`, `videoAvailable`.

| وضعیت معرفی | canRecord | isProcessing | رفتار فرانت |
|---|---|---|---|
| هنوز ارسال نشده | true با زمان باقی‌مانده | false | پس از پخش پیام معرفی ضبط شود |
| STT با lease فعال | false | true | current بازیابی شود |
| شکست و انتظار worker | false | true | هنگام backgroundRetryPending هیچ retry پردازشی نفرستید |
| شکست، worker خاموش یا تلاش‌ها تمام شده | false | false | اگر introduction.canRetry=true، retry بدون upload طبق سیاست محدود فرانت |
| موفق | تابع Questions | تابع سؤال | transcript باقی می‌ماند و stage=Questions می‌شود |

status مالی/جلسه در معرفی همچنان WaitingForAnswer است؛ stage/isProcessing/introduction.status برای UI معتبرند. status معرفی Uploaded/Transcribing/Processed/Failed است؛ answer را برای معرفی مصرف نکنید.

backgroundRetryPending بالادستی برای پاسخ سؤال نیز در lease فعال و انتظار backoff true می‌ماند؛ فقط پس از موفقیت یا آخرین شکست و پایان lease false می‌شود. فرانت هنگام true فقط وضعیت را بخواند. انقضا/ابطال و billing بسته نیازمند رسیدگی‌اند و worker آن‌ها را ادامه نمی‌دهد.

## صوت پیام‌های ثابت

در مرحله جاری Welcome، CandidateIntroduction، CompanyIntroduction و Goodbye:

```http
GET /api/candidate/interviews/{interviewId}/stages/{stage}/audio
Authorization: Bearer <candidate token>
```

مسیر دقیق از stageAudioUrl برداشته شود؛ پاسخ audio/mpeg با range است. توکن در URL نیست؛ فرانت با هدر Authorization فایل را دریافت و پخش کند. cache خصوصی سازمان از متن snapshot و model/voice/speed استفاده می‌کند. اولین GET در cache miss TTS ایجاد می‌کند؛ endpoint state منتظر صوت پیام ثابت نمی‌ماند. stageAudioUrl مسیر مجاز است، نه تضمین وجود فایل آماده. شکست TTS `503 stage_audio_unavailable` می‌دهد، مرحله جلو نمی‌رود و stageMessage محفوظ است. current به‌تنهایی TTS فراخوانی نمی‌کند.

فقط مرحله جاری قابل دریافت است؛ شرکت قبل از پایان Questions یا مرحله گذشته/آینده 404. Created/Cancelled/Finished یا متن خالی audio URL ندارند؛ دسترسی دعوت و retention بررسی می‌شوند.

فرانت برای پیام‌های ثابت بعد از رویداد پایان پخش stage/advance را با stage جاری بفرستد. بعد از پخش درخواست معرفی، ضبط و POST introduction انجام شود؛ برای معرفی صوتی stage/advance JSON نفرستید. سرور رویداد پایان پخش را نمی‌بیند و انتقال پیام ثابت با تأیید کلاینت است. Completed به‌تنهایی پایان UI نیست؛ CompanyIntroduction/Goodbye تا Finished مصرف شوند.

## تاریخچه و HR

messages درخواست معرفی و نوبت معرفی پذیرفته‌شده را با stage=CandidateIntroduction، questionId=null و answerId=null می‌دهد. پیام کارجو با id=introduction:{id} هنگام پردازش نیز هست و transcript تا موفقیت null است. سؤال نخست پیش از Questions در تاریخچه عرضه نمی‌شود. معرفی متنی قبلی نیز با stage/شناسه ثابت عرضه می‌شود؛ زمان آن از timestamp ثبت مرحله موجود است، بدون backfill حدسی.

GET /api/interviews/{id} برای HR، candidateIntroductionTranscript و introduction مستقل شامل id/status/transcript/submittedAtUtc/audioUrl/videoUrl دارد. رسانه‌ها با Bearer سازمان، retention و range:

`GET /api/interviews/{id}/introduction/audio`

`GET /api/interviews/{id}/introduction/video`

سازمان دیگر، جلسه حذف‌شده یا ویدیوی غایب 404 می‌گیرند. توکن داوطلب مجوز HR ندارد. فایل/متن واقعی در لاگ یا مستندات ثبت نمی‌شود.

## rollout

API، migration و فرانت جدید هماهنگ منتشر شوند؛ فعال‌بودن worker و تنظیمات مؤثر مقصد جداگانه تأیید شود. تنظیمات معرفی از قبل در دعوت snapshot می‌شوند؛ دعوت تاریخی بازنویسی نمی‌شود. downgrade در حضور رکورد معرفی صریحاً رد می‌شود تا metadata تاریخچه حذف نشود. این سند مجوز push/deploy یا migration مقصد نیست.
