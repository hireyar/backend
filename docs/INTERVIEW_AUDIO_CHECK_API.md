# تست میکروفون ویزارد داوطلب

`POST /api/candidate/interviews/{interviewId}/audio-check`

احراز هویت: `Authorization: Bearer <invitation-code>` همان مصاحبه، با scheme دعوت موجود؛ JWT پنل جایگزین کد دعوت نیست. فقط وضعیت Created (پیش از start) مجاز است. محدودکننده IP داوطلب و بررسی انقضا/حذف/ابطال/جایگزینی دعوت فعال‌اند و دسترسی پس از STT دوباره بررسی می‌شود.

درخواست `multipart/form-data`:

| فیلد | قرارداد |
|---|---|
| audio | فایل الزامی؛ WebM/WAV/MP3/M4A/OGG با MIME و signature سازگار؛ حداکثر ۲ MiB یا MaxUploadBytes تنظیم‌شده اگر کمتر باشد |
| durationSeconds | اختیاری، عدد صحیح ۱ تا ۳۰؛ مدت گزارش‌شده کلاینت است و از صوت decode نمی‌شود |

سقف کل درخواست ۳ MiB است. فرانت بهتر است نمونه کوتاه ۵ تا ۱۰ ثانیه‌ای از یک جمله مشخص ضبط کند؛ همان جمله و متن خروجی را برای تأیید به کاربر نشان دهد. مجوز مرورگر، انتخاب میکروفون و تشخیص قطع شدن ضبط در فرانت است.

نمونه پاسخ 200:

```json
{
  "audioReceived": true,
  "receivedBytes": 18432,
  "speechRecognized": true,
  "transcript": "این یک جمله آزمایشی برای بررسی میکروفون است.",
  "clarityVerified": false,
  "message": "فایل صوتی دریافت و گفتار به متن تبدیل شد. متن را با جمله‌ای که گفتید تطبیق دهید؛ شفافیت صوت به‌صورت مستقل سنجیده نشده است."
}
```

audioReceived یعنی فایل آپلودشده با طول اعلام‌شده همان فایل در سرور موجود است؛ ثابت نمی‌کند جمله قبل از ضبط یا انتهای گفتار بریده نشده است. speechRecognized فقط خروجی متن غیرخالی STT است. clarityVerified فعلاً همواره false است: provider فعلی confidence، سنجش نویز، clipping یا کامل‌بودن جمله عرضه نمی‌کند؛ استخراج متن اثبات شفافیت صوت نیست. تأیید ادامه ویزارد پس از تطبیق متن توسط کاربر انجام شود. 200 با speechRecognized=false یعنی متن خالی؛ provider فعلی ممکن است خروجی خالی را نامعتبر بداند و 503 بدهد. هیچ مسیر شکست متن ساختگی تولید نمی‌کند.

خطاها ProblemDetails موجودند: 400 فایل/مدت نامعتبر یا audio_incomplete، 401 کد نامعتبر، 409 audio_check_unavailable پس از شروع، 410 دعوت منقضی/تمام‌شده/جایگزین‌شده، 413 فایل/درخواست بزرگ، 429 محدودکننده یا قفل ورود، 503 audio_check_transcription_unavailable برای شکست/خروجی نامعتبر STT. پس از 503 کاربر می‌تواند نمونه را دوباره بفرستد؛ retry خودکار و idempotency ندارد و هر ارسال معتبر ممکن است هزینه provider داشته باشد.

فایل و transcript این تست در storage/DB/لاگ برنامه ثبت نمی‌شوند؛ پردازش STT توسط provider فعال موجود انجام می‌شود. Answer/Introduction، شروع جلسه، رزرو/زمان مالی مصاحبه، LLM و TTS ایجاد نمی‌شود. این نتیجه gate اجباری سمت سرور برای start نیست. تست HTTP موقت است؛ قطع درخواست نیاز به ارسال مجدد دارد. migration لازم ندارد. Angular در این ریپو نیست؛ تماس واقعی provider و کیفیت صوت فارسی باید جداگانه بررسی شوند.

## تفاوت تست میکروفون و صدای نمونه

این endpoint فقط فایل میکروفون را با STT بررسی می‌کند؛ TTS اجرا نمی‌کند و صدای نمونه اسپیکر برنمی‌گرداند. بک‌اند فعلی endpoint جدا برای تولید نمونه قبل از start ندارد. فایل ثابت یا مسیر دیگری که فرانت برای تست اسپیکر پخش می‌کند از این قرارداد مستقل است؛ تغییر TtsModel فقط generation جاری stage/question سرور را عوض می‌کند. صدای Welcome/معرفی بعد از start طبق stageAudioUrl و VOICE_INTRODUCTION_API دریافت/پخش می‌شود.

## API تست اسپیکر با متن ثابت — 2026-10-13

طبق درخواست کاربر، علاوه بر audio-check میکروفون، مسیر مستقل پخش نمونه صوت اضافه شد:

```http
GET /api/candidate/interviews/{interviewId}/speaker-test/audio
Authorization: Bearer <invitation-code>
```

توکن/کد دعوت همان مصاحبه، نه JWT پنل؛ scheme و limiter داوطلب موجود. فقط Created، پیش از POST start؛ متن همیشه دقیقاً «سلام وقت بخیر» است. body یا پارامتر متن/مدل/صدا ندارد و حتی query text ورودی را تغییر نمی‌دهد. پاسخ200 باینری MP3 با Content-Type:audio/mpeg، Range processing و Cache-Control:no-store؛ JSON یا لینک صوت عمومی برنمی‌گرداند.

تولید از ITextToSpeechService منتخب AI.Provider و تنظیمات همان provider استفاده می‌کند؛ با config فعلی Liara/google/gemini-3.8-flash-tts/Aoede است. مدل در endpoint hardcode نشده و NetArz/Disabled طبق تنظیم انتخاب می‌شوند؛ شکست provider صدای ساختگی یا fallback مدل قدیمی نمی‌سازد. QuestionAudio cache خصوصی موجود با scope سازمان و hash متن/model/voice/speed مصرف می‌شود؛ تکرار درخواست/دعوت همان سازمان و profile از cache می‌خواند، تغییر model/voice/speed به cache دیگری می‌رود. طبق رفتار قبلی cache، miss همزمان می‌تواند چند تماس provider ایجاد کند و unique index یک رکورد برنده نگه می‌دارد.

این درخواست start/advance/STT/LLM/answer یا رزرو و تسویه مصاحبه انجام نمی‌دهد؛ state جلسه Created محفوظ است. نخستین generation ممکن است هزینه سرویس TTS داشته باشد ولی billing مصاحبه/کیف‌پول را تغییر نمی‌دهد؛ نیاز به migration/جدول/تنظیمات جدید نیست. هنگام اجرای provider تراکنش/قفل طولانی سازمان نداریم؛ بعد از generation دسترسی/وضعیت جلسه reload و دوباره بررسی می‌شود. این صدای نمونه فقط برای شنیدن و تأیید انسانی اسپیکر است، نه اندازه‌گیری سلامت خروجی صوت کلاینت توسط سرور.

خطاها:401 کد نامعتبر/بدون‌هویت یا دعوت باطل‌شده (از جمله ابطال در زمان اجرای درخواست)،410 دعوت منقضی/تمام‌شده/جایگزین‌شده در authentication،429 limiter/قفل ورود،409/speaker_test_unavailable پس از شروع،503/speaker_test_audio_unavailable در نبود صوت/cache-storage/provider. پس از503 با اقدام کاربر دوباره GET ممکن است؛ retry خودکار فرانت اضافه نشود. متن یا provider-secret در خطا نمایش داده نمی‌شود.

### پرامپت اتصال فرانت

دکمه «تست اسپیکر» ویزارد پیش از شروع مصاحبه را به GET /api/candidate/interviews/{interviewId}/speaker-test/audio با Authorization دعوت همان جلسه وصل کن. بدون متن/body/model بفرست و responseType=blob را بگیر. Blob URL بساز و MP3 را با audio element پس از اقدام صریح کاربر پخش کن؛ خطای play را بگیر و امکان زدن دوباره پخش بده. Blob URL قبلی را revoke کن، هنگام خروج/تعویض جلسه پاک‌سازی و caching مرورگر را no-store کن. پخش نمونه نباید start یا stage/advance را صدا بزند؛ متن نمایشی همیشه «سلام وقت بخیر» باشد. حین دریافت/پخش وضعیت مناسب و غیرفعال‌کردن کلیک تکراری داشته باش. خطاهای API را نمایش بده و شکست را با صوت دیگری مخفی نکن. GET JSON نیست و token در URL/query قرار نگیرد. پس از start دکمه تست پیش‌ازشروع دیگر فعال نیست. تست میکروفون POST audio-check با flow/STT قبلی جدا باقی می‌ماند.

اعتبارسنجی نمونه اسپیکر: build صفر warning/error؛59/59 مرتبط بدون شکست/Skip روی PostgreSQL18 اختصاصی محلی و FFmpeg واقعی، HTTP/PCM/credential مصنوعی Liara. دو تست جدید API و race ابطال، fixed input و cache سازمان/مدل، عدم start/billing، auth/state/error و audio/mpeg Swagger را تأیید کردند؛ speaker-test-final.trx. کیفیت مدل/صدای واقعی مقصد، فرانت و انتشار انجام نشده‌اند؛ migration تازه لازم نیست.
