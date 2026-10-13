# Gemini TTS در Liara — 2026-10-13

انتخاب provider قبلی از `AI:Provider` حفظ شده است. [مستندات Liara](https://docs.liara.ir/ai/google-gemini/) مبنای PCM مدل Gemini و [FFmpeg](https://ffmpeg.org/ffmpeg.html) ابزار تبدیل است. Application، storage و API صوت همچنان MP3 دریافت/تحویل می‌دهند؛ فرانت و schema دیتابیس تغییر نمی‌خواهند.

## تنظیمات و سازگاری

فقط `AI:Liara:TtsModel=google/gemini-3.8-flash-tts` و `TtsVoice=Aoede` در فایل مشترک تغییر کردند. `AI:Provider`، بخش NetArz، STT، LLM مصاحبه، مدل گزارش، URL و کلیدها محفوظ‌اند. با انتخاب NetArz همان adapter/config آن اجرا می‌شود. Liara با مدل‌های قبلی غیرGemini همچنان MP3 و speed قبلی را به سرویس می‌فرستد؛ هیچ fallback خودکار به provider یا مدل دیگری وجود ندارد.

برای مدل Liara با پیشوند google/gemini- و نام حاوی -tts، درخواست audio/speech شامل model، voice، input و response_format=pcm است؛ speed به upstream فرستاده نمی‌شود و تنظیم TtsSpeed موجود (۰٫۲۵..۴) با atempo محلی اعمال می‌شود. مدل/صدا باید با یکدیگر سازگار انتخاب شوند. تنظیمات محیطی/local همان precedence قبلی را دارند.

```json
{
  "SpeechEncoding": {
    "FfmpegPath": "ffmpeg",
    "TimeoutSeconds": 30
  }
}
```

پیش‌فرض کلاس همین است؛ path می‌تواند executable روی PATH یا مسیر خصوصی کامل باشد. TimeoutSeconds معتبر۱..۱۲۰ است. Dockerfile runtime پکیج ffmpeg را نصب می‌کند؛ برای انتشار، image باید دوباره ساخته شود. daemon این نوبت در دسترس نبود و image واقعی ساخته/اجرا نشد. اگر بدون Docker اجرا می‌شود، FFmpeg دارای libmp3lame باید موجود و مسیرش تنظیم باشد. محیط توسعه جاری مسیر `artifacts/ffmpeg-runtime/ffmpeg.exe` را در appsettings.Development.local.json ignored دارد؛ تمام تنظیمات قبلی بدون چاپ مقدار برابر تأیید شدند. این executable را تا جایگزینی با مسیر نصب‌شده حذف نکنید؛ وارد Git نمی‌شود و Python dependency محصول نیست.

## تبدیل و محافظ‌ها

PCM signed16 little-endian، 24kHz و mono از حافظه با stdin به FFmpeg و MP3 با libmp3lame/128kbps از stdout برمی‌گردد. shell و فایل PCM موقت نداریم؛ متن و credential وارد command line نمی‌شوند. stderr drain می‌شود و نگهداری/ثبت نمی‌شود. هر process یک thread و حداکثر دو encode همزمان در singleton، deadline شامل انتظار ظرفیت، cancellation و kill درخت process و انتظار محدود cleanup دارد.

سقف ورودی و خروجی۱۰MiB؛ PCM خالی یا طول فرد و MIME متن/JSON به‌جای PCM رد می‌شود، MP3 خروجی نیز signature دارد. تجاوز حجم، نبود executable/codec، خطای تبدیل و timeout کد امن typed دارند. پیش از تماس هزینه‌دار، probe مصنوعی کوتاه codec را بررسی و نتیجه موفق را cache می‌کند. در startup نیز فقط برای provider انتخاب‌شده Liara/Gemini این بررسی اجرا می‌شود؛ نبود runtime مانع شروع نسخه ناسازگار می‌شود. probe به مدل/DB وصل نیست و صدای مصنوعی آن به کاربر یا cache تحویل داده نمی‌شود.

QuestionAudioService همان فایل MP3 واقعی را در cache خصوصی ذخیره می‌کند؛ مدل، صدا و سرعت در hash قبلی هستند، بنابراین تنظیم Gemini cache تازه می‌سازد و صوت قدیمی را بازنویسی نمی‌کند. مسیرهای stage/question مدیر و داوطلب و MIME audio/mpeg محفوظ‌اند. failure همان رفتار قبلی نبود صوت آماده را دارد و با صدای ساختگی یا تغییر provider پوشانده نمی‌شود.

## اعتبارسنجی و انتشار

build صفر warning/error و ۱۹۵/۱۹۵ تست مرتبط با PostgreSQL18.6 اختصاصی و مدل/STT/TTS مصنوعی، صفر شکست/Skip؛ `tests/HireYar.Tests/TestResults/gemini-tts-final.trx`. wire PCM بدون speed، مدل‌های Gemini، MP3 قدیمی Liara/NetArz، preflight و startup، خروجی ناقص، سقف upstream، encoder واقعی/decode، سرعت‌های۰٫۲۵/۱/۱٫۵/۴، cancellation، deadline، خروجی بزرگ و reuse/concurrency بررسی شدند. padding طبیعی MP3 در سنجش طول صوت کوتاه لحاظ شده است. encoder ویندوز از archive لینک‌شده سایت رسمی FFmpeg استخراج شد؛ CRC و طول entry بررسی شد، دانلود کل archive برای تست لازم نبود و متوقف شد.

یک تماس واقعی Liara با متن کاملاً مصنوعی درباره RabbitMQ/Kubernetes/GitLab CI/CD، بدون API host/worker/DB کاربران، موفق بود: خروجی audio/mpeg با۱۳۵۹۸۰بایت، حدود۴٫۸ثانیه برای کل probe/مدل/تبدیل. `artifacts/gemini-tts-smoke.mp3` مستقل decode شد؛ credential و داده واقعی در فایل/لاگ جدید نیست. این نمونه تضمین تأخیر یا تلفظ همه متن‌ها نیست. migration لازم نیست؛ push/deploy/restart مقصد یا تغییر production انجام نشده است.

## بررسی پیام‌های آغاز و صدای آزمایشی (2026-10-13)

Welcome و CandidateIntroduction از همان ITextToSpeechService منتخب سؤال‌ها تولید می‌شوند؛ مدل جداگانه tts-hd برای مرحله‌ها وجود ندارد. start/current فقط stageAudioUrl می‌دهند و فرانت باید GET همان مسیر با Authorization را مستقل از question.audioReady دریافت و پخش کند. خطای تولید503 stage_audio_unavailable، مرحله/متن را حفظ می‌کند؛ advance قبل از دریافت/پخش، GET مرحله قبل را404 می‌کند. فلگ‌های معرفی در snapshot دعوت مستقل از provider هستند؛ با هر دو فلگ خاموش Welcome طبق سیاست قبلی رد می‌شود.

POST audio-check فقط STT برای میکروفون است؛ صدای نمونه اسپیکر در backend endpoint جدا ندارد. اگر فرانت فایل ازپیش‌ساخته پخش کند، تغییر TtsModel سرور آن asset/cache فرانت را تبدیل نمی‌کند؛ منبع واقعی صدای تست گزارش‌شده هنوز بدون فرانت/Network قطعی نیست. تست HTTP جدید با PCM مصنوعی+encoder/PostgreSQL واقعی و دو smoke مصنوعی واقعی Gemini، خوشامد و معرفی MP3 را محلی تأیید کردند؛ این تأیید رفع یا علت سایت مقصد نیست.

API نمونه اسپیکر اضافه‌شده2026-10-13: GET /api/candidate/interviews/{interviewId}/speaker-test/audio قبل ازstart، متن ثابت «سلام وقت بخیر»، MP3 با provider منتخب و cache متن/model/voice/speed؛ با تنظیمLiara/Gemini PCM→MP3 encoder همان مسیر فعلی است. INTERVIEW_AUDIO_CHECK_API قرارداد/پرامپت است. توضیح تاریخی «endpoint اسپیکر نداریم» در بررسی قبلی با این درخواست supersede شد؛ audio-check میکروفون همچنان فقطSTT است. تغییر config/provider یا انتشار مقصد انجام نشده است.
