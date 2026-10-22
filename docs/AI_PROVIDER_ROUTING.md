## علت بازتولیدشده Soniox400: تکرار language_hints — 2026-10-22

تست واقعی از دعوتِ موجود در پنل با اجازه کاربر انجام شد؛ فقط OTP ورود دعوت و تست گذرای صوت، بدون start/answers/complete. میکروفون Chromium فایل WAV گفتار مصنوعی را مصرف کرد و MediaRecorder فایل۶ثانیه‌ای WebM/Opus با header=1a45dfa3 و۷۵۰۱۴بایت ساخت. ارسال آن به audio-check واقعی اپ503 گرفت؛ همان بایت‌ها مستقیم در Soniox، هم یکجا و هم با قطعات۱۶KB، تا finished=true و token نهایی موفق بودند. بنابراین WebM این نمونه سالم است و تغییر اجباری به MP3 راه‌حل این ایراد نیست.

علت در binding واقعی .NET بازتولید شد: AiCapabilitySettings.LanguageHints پیش‌فرض [fa] دارد و ConfigurationBinder مقدار [fa] فایل را به آن اضافه می‌کند؛ آرایه [fa,fa] حاصل می‌شود. تست از DI واقعی قبل اصلاح FAIL با Actual=[fa,fa] و Expected=[fa] داشت (artifacts/soniox-hints-repro.log). ارسال واقعی همین تکرار به Soniox دقیقاً400 و پیام Language hints must be unique. داد؛ این پیام قبلاً provider_request_rejected شده بود. adapter اکنون فقط هنگام serialization درخواست Soniox، LanguageHints را با Ordinal.Distinct یکتا می‌کند؛ پیش‌فرض فارسی، تنظیمات و پروایدرهای دیگر عوض نمی‌شوند. همین خطا در diagnostics به language_hints_duplicate نگاشت می‌شود. توکن‌ها و appsettings دست‌نخورده‌اند؛ فرانت/codec/DB/migration تغییر نکرده‌اند.

عبور کامل از UI ممکن نشد: چک‌باکس تأیید بلندگو در محیط headless غیرفعال ماند؛ پس تست ارسال با ضبط واقعی Chrome و endpoint واقعی اپ انجام شد، نه ادعای موفقیت کامل ویزارد UI. screenshot مرحله توقف و نتیجه درخواست/فایل مصنوعی در artifacts/soniox-app-* موجودند؛ شناسه دعوت/کد/توکن در گزارش درج نشده‌اند. نسخه مقصد هنوز اصلاح distinct را دریافت نکرده؛ تست پس از انتشار لازم است. این یافته جایگزین فرضیه‌های قبلی فرمت/احراز هویت در این مورد است. WebM در مرجع رسمی Soniox پشتیبانی می‌شود: https://soniox.com/docs/stt/rt/real-time-transcription#audio-formats .

## تشخیص بدون انتشار مجدد API

ابزار مستقل [soniox_diagnose.py](../tools/soniox_diagnose.py) را کنار appsettings.json سرور بگذارید و با **همان محیط سرویس API** اجرا کنید:

```sh
python3 soniox_diagnose.py ./appsettings.json
```

Python3 استاندارد کافی است؛ pip/SDK/تغییر توکن لازم نیست. ابزار یک ثانیه سکوت مصنوعی می‌فرستد و فقط نتیجه یا علت حذف‌کلیدشده provider را نشان می‌دهد. فایل محیط ASPNETCORE_ENVIRONMENT/DOTNET_ENVIRONMENT و AI__... لحاظ می‌شوند؛ هیچ تغییری در تنظیمات نمی‌دهد. توکن را در خط فرمان وارد نکنید. خروجی را ارسال کنید؛ فایل تنظیمات یا env را ارسال نکنید. موفقیت این probe صحت فایل ضبط‌شده اصلی را ثابت نمی‌کند. پروتکل و redaction آن۵تست دارند؛ آزمایش واقعی محلی با توکن فعلی موفق است، مقصد هنوز اجرا نشده.

## تشخیص خطای400 Soniox — 2026-10-22

لاگ مقصد stt_http_400 تأیید می‌کند provider درخواست را رد می‌کند؛ هشدار cache آنتی‌فورجری علت شکست STT نیست. عدد400 به‌تنهایی decode صوت/پارامتر/مدل را تفکیک نمی‌کند و علت دقیق درخواست گزارش‌شده هنوز اثبات نشده است. SonioxSpeechToTextService اکنون failureReason را فقط از واژگان ثابت مستند (audio_decode_error، model_not_available، audio_channels_required، audio_sample_rate_required، audio_frame_invalid، audio_too_long، provider_request_rejected) ثبت می‌کند، همراه audioFormat کنترل‌شده و Activity traceId. پیام خام Soniox یا صوت/توکن ذخیره یا log نمی‌شود؛ Reason استثنا هم فقط همین مقدار ثابت است. قرارداد عمومی503 و کد stt_http_400 محفوظ‌اند. پارامترها/توکن/انتخاب مدل تغییر نکردند؛ migration و انتشار ندارد.

فرمت‌های auto در مرجع Soniox شامل webm/wav/ogg/mp3/aac هستند؛ M4A/MP4 مجاز در ورودی اپ، در این فهرست تصریح نشده‌اند. این صرفاً فرضیه بررسی فرمت است، نه اثبات علت خطای مقصد. تبدیل صوت یا تغییر مدلِ حدسی اعمال نشد. مرجع: https://soniox.com/docs/stt/rt/real-time-transcription#audio-formats و https://soniox.com/docs/api-reference/stt/websocket-api . برای تشخیص مقصد، لاگ جدید failureReason یا فایل صوتی بازتولید لازم است؛ هنوز از محیط مقصد در دسترس نیست.

## اصلاح تشخیص Soniox — 2026-10-22

finished=true بدون token نهایی، متن خالی معتبر است؛ audio-check با speechRecognized=false راهنمای میکروفون می‌دهد و آن را خطای503 نمی‌کند. پاسخ مصاحبه همچنان شرط متن غیرخالی خود را دارد. خطای واقعی در لاگ و Trace با failureCode دقیق (مثل stt_http_401/402/429 یا stt_invalid_response) ثبت می‌شود؛ متن خام پاسخ و توکن ثبت نمی‌شوند. پاسخ HTTP عمومیِ شکست provider همچنان503 است. توکن تنظیم‌شده با اجازه کاربر، با سکوت و گفتار مصنوعی در Soniox واقعی موفق آزمایش شد؛ فایل appsettings تغییر نکرد.

# انتخاب مستقل پروایدرهای AI

2026-10-22

ساختار جاری مستقیماً در `src/HireYar.Api/appsettings.json` آمده است.
طبق درخواست کاربر فایل نمونه جدا حذف شد؛ برای تنظیمات AI فایل local یا نمونه
جدید ساخته نمی‌شود. مقادیر کلیدها و توکن‌های موجود در فایل اصلی حفظ می‌شوند
و مدیریت آن‌ها با کاربر است.
`AI:Providers` اطلاعات اتصال را نگه می‌دارد؛ هر قابلیت `Provider`، `Model` و
`TimeoutSeconds` مستقل دارد. هیچ endpoint یا DTO فرانت تغییر نکرده است.

| قابلیت | پروایدرهای قابل انتخاب |
|---|---|
| SpeechToText | NetArz، Liara، Soniox، Disabled |
| InterviewDecision | NetArz، Liara، Cloudflare، Disabled |
| QuestionSuggestion | NetArz، Liara، Cloudflare، Disabled |
| AnswerAssessment | NetArz، Liara، Cloudflare، Disabled |
| ReportNarrative | NetArz، Liara، Cloudflare، Disabled |
| TextToSpeech | NetArz، Liara، Disabled |

پیش‌فرض فعلی عین تنظیمات قبل است: هر شش قابلیت Liara؛ STT برابر
`openai/gpt-transcribe`، تصمیم/پیشنهاد/ارزیابی/روایت برابر
`openai/gpt-5.6-luna`، و TTS برابر `google/gemini-3.8-flash-tts` با
`Aoede` و سرعت 1. timeoutها به ترتیب 60/30/30/60/60/30 ثانیه‌اند.
آدرس‌ها و credentialهای موجود حفظ شده‌اند؛ دو پروایدر جدید انتخاب نشده‌اند.

## نمونه تغییر فقط STT

```json
"SpeechToText": {
  "Provider": "Soniox",
  "Model": "stt-rt-v5",
  "TimeoutSeconds": 60,
  "Options": { "LanguageHints": ["fa"] }
}
```

کلید از `AI__Providers__Soniox__ApiKey` و آدرس از
`AI:Providers:Soniox:WebSocketUrl` خوانده می‌شود. مدل را از کاتالوگ
فعال حساب انتخاب کنید. بک‌اند فایل آپلودشده فعلی را با `audio_format=auto`
به‌صورت binary می‌فرستد؛ احراز هویت با هدر Bearer، پایان ورودی با پیام text
خالی و پایان نتیجه با `finished=true` است. فقط tokenهای نهایی به متن افزوده
می‌شوند؛ متن موقت و نشانه‌های پایان وارد گزارش نمی‌شوند. این تغییر نیازمند
WebSocket جدید در مرورگر نیست.

## Cloudflare

Cloudflare در این نسخه Workers AI برای قابلیت‌های متنی است. تنظیم
`AI__Providers__Cloudflare__AccountId` و `AI__Providers__Cloudflare__ApiToken`
لازم است. قابلیت موردنظر را روی `Cloudflare` و مدل سازگار با chat completions
و JSON mode قرار دهید؛ آدرس حساب به‌صورت خودکار ساخته می‌شود:
`https://api.cloudflare.com/client/v4/accounts/{AccountId}/ai/v1/chat/completions`.
توکن در سرور می‌ماند. پشتیبانی صوتی Cloudflare در این نسخه پیاده نشده است.

## سازگاری و اعتبارسنجی

- تنظیم قدیمی `AI:Provider` و `AI:NetArz`/`AI:Liara` وقتی هیچ بخش جدیدی
  وجود ندارد، همان مسیر قبلی را دارد.
- حضور هر بخش قابلیت یا `AI:Providers` ساختار جدید را فعال می‌کند؛
  `AI:Provider` قدیمی دیگر انتخاب‌ها را تغییر نمی‌دهد. در این حالت قابلیت
  تعریف‌نشده Disabled است؛ هنگام مهاجرت هر شش قابلیت را صریح بنویسید.
- هر قابلیت را جدا می‌توان Disabled کرد. برای تنظیمات جدید از مسیرهای جدید
  متغیر محیطی استفاده کنید؛ مثلاً `AI__InterviewDecision__Model`.
- فقط اتصال‌های انتخاب‌شده اعتبارسنجی می‌شوند. پروایدر نامعتبر، اتصال ناامن،
  کلید خالی، مدل خالی یا timeout خارج 1 تا 120 ثانیه خطای شروع برنامه دارد.
- Lease باید از مجموع timeoutهای STT و تصمیم فعال به‌اضافه 30 ثانیه بیشتر باشد.
- fallback بین پروایدرها یا retry تماس خارجی اضافه نشده است. policy، promptها،
  امتیازدهی، cache صوت و مسیر FFmpeg فعلی Gemini حفظ شده‌اند.
- migration دیتابیس لازم نیست. تغییر انتخاب‌ها با راه‌اندازی مجدد اعمال می‌شود.

مراجع رسمی:
[Soniox WebSocket](https://soniox.com/docs/api-reference/stt/websocket-api)،
[Cloudflare OpenAI compatibility](https://developers.cloudflare.com/workers-ai/configuration/open-ai-compatibility/).
