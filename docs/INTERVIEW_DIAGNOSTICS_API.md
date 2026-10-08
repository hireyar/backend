# لاگ زنده آزمایشی مصاحبه

درخواست 2026-10-08: نمایش مراحل واقعی مصاحبه، دستورات دیتابیس، API فراخوانی‌شده، provider/model و هدف STT/LLM/TTS، همراه پرامپت واقعی LLM. این ریپو فقط بک‌اند است؛ اتصال Angular در ریپوی جدا باقی است.

## فلگ و نمایش

در `appsettings.json` برای آزمایش هر دو مقدار روشن‌اند:

```json
"InterviewDiagnostics": {
  "Enabled": true,
  "CapturePrompts": true
}
```

`Enabled=false` همه جمع‌آوری‌های این قابلیت را خاموش می‌کند، endpointها 404 می‌دهند، و DTO مقدار `diagnosticsEnabled=false` و `diagnosticsUrl=null` دارد. خاموش‌کردن `CapturePrompts` فقط جمع‌آوری بدنه LLM را متوقف می‌کند. تنظیمات در startup خوانده می‌شوند؛ پس از تغییر فایل، برنامه را restart کنید. override محیطی `InterviewDiagnostics__Enabled` و `InterviewDiagnostics__CapturePrompts` پشتیبانی می‌شود. سایر تنظیمات AI/کلیدها عیناً محفوظ‌اند.

پاسخ‌های `current/start/answers/retry/stage/advance/introduction/complete` در `CandidateInterviewDto` فیلدهای زیر را اضافه می‌کنند. `GET /api/interviews/{id}` برای Owner هم همین فلگ‌ها و `diagnosticPromptsEnabled` دارد:

```json
{
  "diagnosticsEnabled": true,
  "diagnosticsUrl": "/api/candidate/interviews/00000000-0000-0000-0000-000000000001/diagnostics"
}
```

فرانت تنها وقتی `diagnosticsEnabled=true` است بخش موقت لاگ را نمایش دهد و به `diagnosticsUrl` همان پاسخ وصل شود. در حالت false بخش را حذف، اتصال را abort و رویدادهای نگه‌داشته‌شده در حافظه UI را پاک کند. endpoint صرفاً دریافت رویداد است؛ مدل، processing، billing یا retry را اجرا نمی‌کند.

## دو سطح دسترسی

- `GET /api/candidate/interviews/{interviewId}/diagnostics` با `Authorization: Bearer <invitation-code>`: فقط رویدادهای همان جلسه، با حذف `promptJson` و `promptTruncated`. query دلخواه امکان فعال‌کردن prompt ندارد. جلسه Completed/Cancelled/منقضی/ابطال‌شده قابل اتصال نیست.
- `GET /api/interviews/{id}/diagnostics` با JWT معتبر Owner: فقط همان سازمان، شامل بدنه واقعی prompt در صورت فعال‌بودن CapturePrompts؛ جلسه حذف‌شده/سازمان دیگر 404. جلسه Completed برای بررسی گزارش قابل مشاهده است.

پرامپت شامل رزومه، شرح شغل، متن پاسخ، تاریخچه منتخب، بانک مجاز و دستورهای ارزیابی است. نمایش آن در ابزار آزمایش Owner یا پیش‌نمایش احرازشده همان سازمان انجام شود؛ JWT مالک در صفحه عمومی داوطلب جاسازی نشود. توکن دعوت اجازه این اطلاعات را نمی‌دهد. هیچ prompt در ILogger، DB، فایل، Git یا telemetry ذخیره نمی‌شود. این یک استثنای محدود و صریح برای نمایش تشخیصی مورد درخواست کاربر در حافظه موقت است.

## SSE و اتصال

پاسخ `text/event-stream` و `Cache-Control: no-store`، با `X-Accel-Buffering: no` و flush هر رویداد است. native EventSource امکان هدر Bearer ندارد؛ از `fetch` streaming یا کتابخانه SSE با هدر استفاده کنید. توکن را داخل URL نگذارید. تنظیم buffering/timeouts reverse proxy در مقصد باید با SSE سازگار باشد؛ در مقصد این تغییر آزمایش نشده است.

```text
id: <process-id>:<sequence>
event: diagnostic
data: {"id":"<process-id>:<sequence>","atUtc":"...","requestId":"...","operationId":"...","kind":"provider","operation":"llm","status":"started","message":"...","stage":"Evaluating","method":"POST","endpoint":"chat/completions","provider":"Liara","model":"<configured-model>","durationMs":null,"httpStatusCode":null,"failureCode":null,"tables":null,"columns":null,"promptJson":null,"promptTruncated":false}

```

- `diagnostic`: یک رویداد واقعی. `id` برای dedup/reconnect؛ `requestId` برای یک API یا تلاش worker؛ `operationId` برای تطبیق شروع و پایان یک تماس/دستور.
- `reset`: تاریخچه قبلی به علت TTL/محدودیت buffer یا restart کامل نیست. UI پیام «بخشی از لاگ در دسترس نیست» نشان دهد و رویدادهای بعدی را dedup کند.
- `closed`: دسترسی حین اتصال از دست رفت؛ reason ثابت `access_unavailable`. اتصال را ببندید و بدون دعوت/ورود تازه retry نکنید.
- `reconnect`: اتصال پس از ۳۰ ثانیه بسته می‌شود؛ با cursor آخر دوباره وصل شوید. این بستن به معنی توقف مصاحبه نیست.
- commentهای `connected` و `heartbeat` داده قابل نمایش نیستند؛ heartbeat هر ۱۰ ثانیه است.

فرانت پس از قطع شبکه با backoff محدود reconnect کند و آخرین id دریافت‌شده را در `Last-Event-ID` یا query `after` بفرستد. cursor شناسه رویداد غیرحساس است، نه توکن دعوت. پاسخ‌ها می‌توانند بین chunkهای شبکه شکسته شوند؛ TextDecoder streaming و parser واقعی SSE لازم است؛ `JSON.parse` روی هر chunk خام نزنید. `data`های یک event را طبق SSE کنار هم بگذارید، سپس JSON parse کنید. چند درخواست همزمان ممکن است interleave شوند؛ آخرین پیام یک درخواست را مرحله همه درخواست‌ها فرض نکنید.

برای شروع قبل از start می‌توان از current فلگ را خواند، اتصال را باز کرد و سپس API start یا audio-check را زد. اتصال به‌خودی‌خود لاگ، STT/LLM/TTS یا queryهای تشخیصی بازگشتی نمی‌سازد. هر اتصال و هر ثانیه دسترسی session دوباره با query مختصر بررسی می‌شود؛ Owner علاوه بر session، فعال‌بودن حساب/سازمان/نقش و expiry JWT را recheck می‌کند. تأخیر تشخیص ابطال حداکثر تقریباً یک ثانیه به‌علاوه زمان query است. این queryهای recheck در feed ثبت نمی‌شوند.

## معنی رویدادها

| kind | جزئیات |
|---|---|
| `api` | شروع/پایان عملیات Controller، method و **route template** بدون query/header/body؛ وضعیت HTTP و مدت. خطا با code ثابت، بدون متن exception |
| `database` | شروع/پایان/شکست/cancel دستور EF به PostgreSQL، tables و columns واقعی از schema معتبر model، read/write و مدت. SQL، parameter و مقدار رکوردها نمایش داده نمی‌شوند |
| `stage` | مرحله واقعی سرویس: دریافت جلسه، Transcribing، PreparingContext، Evaluating، ValidatingDecision، SavingResult، GeneratingAudio، معرفی، تست میکروفون یا BackgroundRecovery |
| `provider` | مسیر نسبی upstream (`audio/transcriptions`، `chat/completions`، `audio/speech`)، provider/model واقعی و هدف تماس، شروع فوری، پایان HTTP/timeout/cancel/network/failure و مدت |
| `prompt` | بدنه JSON همان درخواست LLM پیش از ارسال، شامل model/response_format/messages. همان `operationId` تماس provider؛ فقط Owner. `promptJson` یک رشته JSON است؛ parse آن `messages[].role/content` را به دست می‌دهد |
| `audio` | cache hit؛ صوت آماده بدون تماس TTS تازه خوانده شد |
| `processing` / `recovery` / `decision` | خطای پردازش با کد ثابت، استفاده دوباره از transcript، ثبت retry یا تصمیم fallback |

`httpSucceeded` صرفاً موفقیت HTTP provider است؛ معتبر بودن JSON/متن پاسخ مدل از آن نتیجه نمی‌شود. خطای parsing/validation بعدی ممکن است رویداد processing/API failure یا fallback ایجاد کند. `database.succeeded` موفقیت اجرای دستور است، نه تضمین commit نهایی transaction یا پایان materialization همه رکوردها. columns فهرست identifierهای استفاده‌شده در دستور است و ممکن است شامل WHERE/ORDER/FK باشد؛ snapshotهای جلسه و owned collectionها نام جدول‌های مستقل خود را نشان می‌دهند.

session/snapshot/answers از DB خوانده می‌شوند؛ رزومه/JD/بانک snapshot در حافظه برای context مصرف می‌شوند و history selector محلی query/LLM تازه نیست. حضور آن‌ها در prompt به معنی query مستقل دوباره نیست. اگر transcript موجود باشد STT skipped است؛ اگر قابلیت AI قرارداد خاموش باشد LLM skipped است؛ صوت cache hit نیز TTS جدید ندارد. گزارش نهایی فقط با API گزارش تولید می‌شود. هیچ مرحله ساختگی برای عملیات اجرا‌نشده اضافه نشده است.

## عمر و محدودیت‌ها

این قابلیت single-process و موقت است؛ SSE polling داخلی buffer حداکثر ۲۵۰ میلی‌ثانیه به‌علاوه شبکه تأخیر دارد. حافظه حداکثر ۱۲۸ جلسه و ۲۵۶ رویداد برای هر جلسه، بودجه تقریبی ۶۴ MiB برای رویدادها، TTL ده دقیقه برای هر رویداد و cleanup دقیقه‌ای دارد. حداکثر چهار stream در هر سازمان/جلسه و ۶۴ stream در کل process؛ عبور از ظرفیت subscription = 429 diagnostics_busy. رویدادهای قدیمی حذف می‌شوند و restart تاریخچه را پاک می‌کند. در چند replica باید اتصال SSE و درخواست‌های همان مصاحبه به همان process برسند؛ queue یا storage توزیع‌شده اضافه نشده است.

بدنه prompt بیش از ۵۲۴۲۸۸ کاراکتر صریحاً truncate می‌شود و `promptTruncated=true` دارد؛ UI آن را «ناقص» نشان دهد و رشته بریده‌شده را JSON معتبر فرض نکند. متن ارسالی به مدل truncate یا تغییر نمی‌کند. endpoint STT/TTS فقط metadata می‌دهد؛ فایل صوت/ویدیو، خروجی خام provider، credential، URL کامل upstream و داده پاسخ DB جمع‌آوری نمی‌شوند. بخش حساس prompt فقط شامل payload درخواست واقعی LLM است؛ اگر محتوای کاربر خود دارای داده حساس باشد، Owner آن را در context خواهد دید.

بدون migration یا تغییر قرارداد مالی؛ AI واقعی، Angular، proxy مقصد و کیفیت/latency provider با این قرارداد تأیید نمی‌شوند. برای حذف آزمایش ابتدا Enabled را false و برنامه را restart کنید؛ رابط Angular طبق فلگ پنهان شود.
