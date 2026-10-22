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
