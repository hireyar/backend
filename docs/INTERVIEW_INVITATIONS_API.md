# دعوت مصاحبه و وضعیت پردازش — 2026-10-08

پنل Angular در این ریپو نیست. مسیر پیشنهادی فرانت `/interview/{id}` است؛ `id` شناسه Guid جلسه و کد در فرم جداگانه وارد می‌شود. لینک از `Security:FrontendUrl` و `InterviewInvitations:CandidateLinkPath` ساخته می‌شود؛ کد در URL قرار نمی‌گیرد.

## پنل سازمان

- پاسخ invite/resend شامل `invitation.interviewId`, `accessCode`, `interviewUrl` و `expiresAtUtc` است. `token` برای سازگاری همان رشته شش‌رقمی است، با حفظ صفر آغازین.
- `GET /api/interviews/{id}/access` لینک، کد، `canResend` و `accessStatus` را برمی‌گرداند. همان فیلدها در لیست مصاحبه‌ها موجودند. دسترسی نقش Owner فعلی پنل و سازمان جاری لازم است؛ سازمان دیگر 404 می‌گیرد. پاسخ‌ها `no-store` هستند.
- `POST /api/interviews/{id}/resend` با `{ "deadlineDays": 7 }` جلسه جدید می‌سازد؛ بازه 1 تا 14 روز. برای جلسه تمام‌شده، در جریان، لغوشده یا منقضی نیز ممکن است، اگر شغل/قالب فعال و قابلیت جاری سازمان معتبر باشد. جلسه قبلی فوراً revoked و به جلسه جدید مرتبط می‌شود؛ تکرار روی جلسه جایگزین‌شده 409 `invitation_already_replaced` دارد. دعوت مجدد ایمیل ارسال نمی‌کند.
- snapshot سؤال‌ها و تنظیمات منتقل می‌شوند؛ پاسخ/امتیاز/گزارش/معرفی قبلی در تاریخچه جلسه قبلی حفظ می‌شود. جلسه جدید از ابتدا آغاز می‌شود. رزرو قبلی طبق پنجره‌های مالی موجود تسویه می‌شود؛ جلسه جدید هنگام start رزرو تازه لازم دارد.

## لینک و کد کارجو

`GET /api/candidate/interviews/{interviewId}/access-status` بدون کد، فقط وضعیت عمومی و پیام را می‌دهد. فرانت پیش از فرم ورود آن را بخواند؛ متن کارجو، کد و شناسه جایگزین عرضه نمی‌شود.

APIهای کارجو کد را به صورت `Authorization: Bearer 001234` دریافت می‌کنند. کد فقط برای Guid همان مسیر بررسی می‌شود. پنج خطای کد در پنجره پانزده‌دقیقه‌ای، ورود همان دعوت را پانزده دقیقه قفل می‌کند: 429 `interview_access_locked` و `Retry-After: 900`. محدودیت IP نیز قبل از authentication اعمال می‌شود.

جلسه Completed در current/start و بازکردن دوباره لینک 410 `interview_completed`، جلسه جایگزین‌شده 410 `interview_address_changed`، انقضا 410 `interview_expired` می‌دهد؛ پیام فارسی در `title` است. نتیجه نهایی همان درخواست همچنان مرحله معرفی شرکت/خداحافظی را دارد؛ فقط صوت و تأیید همان مراحل تا Finished قابل استفاده است. برای نمایش پایان، فرانت همان نتیجه نهایی را نگه دارد و current را دوباره فراخوانی نکند. لغو دسترسی 401 است. شناسه ناموجود/کد نادرست 401 است؛ دسترسی HR خارج سازمان همچنان 404.

کدهای جدید با Data Protection وابسته به Guid رمز می‌شوند؛ hash موجود fingerprint ciphertext است، نه hash کد کم‌دامنه. کلیدهای `/keys` باید پایدار بمانند. از دست رفتن کلید، خواندن کد را ناممکن می‌کند؛ دعوت مجدد راه بازیابی است. دعوت‌های قدیمی 64hex همچنان تا پایان/انقضا معتبرند؛ کد قابل نمایش ندارند (`accessCode:null`) و با دعوت مجدد ارتقا می‌یابند. migration جدید backfill کد انجام نمی‌دهد؛ downgrade پس از وجود کد جدید/جایگزینی/خطای ورود رد می‌شود.

## توقف بررسی پاسخ صوتی

لاگ گزارش‌شده HTTP 200 در STT و سپس `stt_invalid_response` است؛ درخواست HTTP موفق شده ولی transcript معتبر استخراج نشده است. بدون پاسخ واقعی provider، خالی بودن متن یا شکل ناسازگار آن قطعی نیست. درخواست اکنون `response_format=json` دارد؛ warning امن `reason` یکی از `invalid_json`, `missing_text`, `text_not_string`, `empty_text`, `text_too_long` است. متن پاسخ و صوت در لاگ ثبت نمی‌شود. `status=success` در لاگ HTTP به تنهایی موفقیت تبدیل صوت نیست.

پاسخ submit/current اکنون `answer.failureStage`, `answer.failureCode` و برای پردازش پاسخ سؤال `processingState`, `processingMessage`, `nextRecoveryAtUtc` دارد. وضعیت‌ها `idle`, `transcribing`, `evaluating`, `retryScheduled`, `retryAvailable`, `unavailable` هستند. در `retryScheduled` پیام تلاش مجدد را نمایش دهید؛ براساس `isProcessing` و `backgroundRetryPending` polling محدود انجام دهید، نه صرفاً `status==ProcessingAnswer`. پس از آخرین شکست خودکار، pending و spinner خاموش و دکمه retry طبق `answer.canRetry` نمایش داده شود. در `unavailable` رزرو مالی بسته شده؛ spinner/retry خاموش است، صوت و پاسخ ذخیره‌شده حفظ می‌شود و تماس با سازمان/دعوت مجدد پیشنهاد می‌شود. worker جلسه با رزرو بسته را دوباره به STT نمی‌فرستد. قرارداد معرفی صوتی مستقل در VOICE_INTRODUCTION_API باقی است.

Angular باید این فیلدها و پیام‌های 410 را مصرف کند؛ این تغییر بک‌اند به تنهایی spinner نسخه منتشرشده فرانت را عوض نمی‌کند. سرویس واقعی Liara و صوت واقعی در آزمون محلی فراخوانی نشده‌اند.
