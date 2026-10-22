# قرارداد STT زنده مصاحبه

این تغییر افزایشی است؛ DB/migration و توکن‌های پروایدر تغییر نکرده‌اند. مسیر batch و کلاینت قبلی محفوظ‌اند. فرانت در مخزن جداست. مرجع پیاده‌سازی رابط: REALTIME_STT_FRONTEND_PROMPT.md.

## قابلیت انتخاب‌شده توسط سرور

GET /api/candidate/interviews/{interviewId}/speech/capabilities با Authorization: Bearer همان credential مصاحبه. نتیجه همچنین در CandidateInterviewDto با فیلد speechCapabilities می‌آید:

```json
{"mode":"realtime","streamUrl":"/api/candidate/interviews/00000000-0000-0000-0000-000000000001/speech/stream","protocol":"hireyar-stt-v1"}
```

یا {"mode":"batch","streamUrl":null,"protocol":"hireyar-stt-v1"}. قابلیت از پیاده‌سازی ISpeechToTextService گرفته می‌شود؛ فعلاً فقط Soniox، IRealtimeSpeechToTextService دارد. Liara/NetArz/Disabled و providerهای بدون این interface مسیر موجود batch دارند؛ انتخاب در فرانت hardcode نشود. capabilities در آغاز هر نوبت خوانده شود؛ اتصال باز، instance/provenance همان پروایدر را تا پایان نگه می‌دارد. receipt معتبر یک نوبت حتی اگر بعداً provider عوض شود مصرف می‌شود.

## اتصال امن مرورگر

URL بالا را نسبت به origin خود **API** حل و https→wss (در توسعه http→ws) کن؛ origin فرانت را به‌اشتباه به‌جای origin API نگذار. کد مصاحبه در querystring نباشد:

```js
const socket = new WebSocket(url, ['hireyar-stt-v1', `hireyar-interview.${candidateCredential}`]);
```

سرور فقط hireyar-stt-v1 را echo می‌کند. همان authentication مصاحبه، قفل تلاش ناموفق، انقضا/لغو/جایگزینی و candidate rate limit برقرار است. Origin باید با Security:FrontendUrl مطابق باشد. کنترل دسترسی بعد از هر۵ثانیه دریافت داده و پیش از صدور receipt دوباره از DB انجام می‌شود؛ اتصال ساکت پس از۱۵ثانیه قطع می‌شود. یک اتصال فعال به‌ازای مصاحبه در هر process؛ این قفل سراسری چند replica نیست. رسید و idempotency ثبت نهایی از نوشتن دو پاسخ مستقل برای یک turn جلوگیری می‌کنند؛ روی چند replica ظرفیت اتصال و محدودیت هزینه در ingress هم باید رعایت شود.

قبل از upgrade: 401/410/429 طبق احراز هویت موجود،409 realtime_not_supported یا realtime_already_connected،400 realtime_websocket_required،403 realtime_origin_invalid. خطاهای پس از upgrade به‌صورت event هستند.

## پیام‌ها

حداکثر۱۰ثانیه پس از open، پیام text شروع:

```json
{"type":"start","purpose":"answer","clientTurnId":"stable-uuid-for-this-turn","questionId":"00000000-0000-0000-0000-000000000002"}
```

purpose یکی از audioCheck/introduction/answer است. questionId فقط answer لازم است؛ در دو حالت دیگر حذف یا null. clientTurnId برای هر سه لازم، غیرخالی/trim‌شده و حداکثر۱۲۸کاراکتر است. audioCheck فقط Created؛ introduction فقط مرحله فعال معرفی بدون معرفی قبلی؛ answer فقط Questions/WaitingForAnswer/سؤال فعلی/زمان باقی‌مانده. جابه‌جایی سؤال/مرحله/لغو در میانه باعث ندادن receipt می‌شود. پیام شروع حداکثر۴KB.

پس از ready ضبط کن:

```json
{"type":"ready","maxSeconds":600,"maxBytes":20971520,"finalizationTimeoutSeconds":60,"format":"webm","fallback":"batch"}
```

این مقادیر نمونه‌اند؛ مقادیر سرور مرجع‌اند. برای audioCheck سقف۳۰ثانیه و min(2MiB,MaxUploadBytes)؛ برای پاسخ/معرفی min(1200,MaxAnswerSeconds) و MaxUploadBytes. ready یعنی اتصال اپ آماده دریافت است؛ ممکن است اتصال upstream در حال برقرارشدن باشد. صف bounded و backpressure دارد؛ success ارسال از مرورگر به معنی پردازش موفق Soniox نیست.

بایت‌های WebM/Opus یک MediaRecorder پیوسته، به‌صورت binary messages پشت‌سرهم فرستاده شوند. هر message حداکثر۶۵۵۳۶بایت؛ chunk بزرگ را به sliceهای کوچک‌تر تقسیم کن. header WebM باید ابتدای کل جریان باشد. بین chunkها recorder را stop/start نکن و containerها را به هم نچسبان. سرور به‌صورت افزایشی به Soniox می‌فرستد؛ کل فایل را قبل تماس STT جمع نمی‌کند.

```json
{"type":"transcript","finalText":"بخش قطعی تا این لحظه","interimText":"بخش موقت فعلی"}
```

این‌ها snapshot هستند: finalText را جایگزین نمایش قطعی کن و interimText را جایگزین نمایش موقت؛ با += کل پیام‌ها را به هم نچسبان. partial در گزارش/ارزیابی ذخیره نمی‌شود. سرور فقط tokenهای final را به متن قطعی اضافه می‌کند و نشانه‌های پایان را حذف می‌کند. سقف هر متن۳۲۰۰۰کاراکتر.

پس از stop recorder و ارسال آخرین dataavailable و اتمام صف ارسال:

```json
{"type":"finish"}
```

سپس سرور، پایان ورودی Soniox را می‌فرستد و تا TimeoutSeconds پروایدر منتظر نتیجه می‌ماند:

```json
{"type":"completed","finalText":"کل متن قطعی","receipt":"protected-opaque-value","audioBytes":75014}
```

فقط completed موفق است؛ transcript به‌تنهایی رسید نیست. بعد completed close عادی انجام می‌شود؛ close بعد completed باعث fallback/ارسال دوباره نشود. خطای اتصال:

```json
{"type":"error","code":"stt_timeout","fallback":"batch","message":"اتصال زنده کامل نشد؛ فایل ضبط‌شده را حفظ کنید."}
```

code می‌تواند کد کنترل‌شده provider یا realtime_interrupted/realtime_frame_invalid/audio_invalid/realtime_duration_exceeded/... باشد. JSON خراب، قطع اتصال و انقضا receipt نمی‌سازند. متن خام Soniox/کلید/headers/audio در log ذخیره نمی‌شود.

## اتصال متن قطعی به فایل ثبت‌شده

همان فایل کامل حاصل از concatenation **همان بایت‌های ارسال‌شده** را با receipt و clientTurnId در multipart مسیرهای قبلی بفرست:

- POST .../audio-check: audio، durationSeconds اختیاری، clientTurnId، realtimeReceipt.
- POST .../answers: questionId، audio، clientTurnId، realtimeReceipt، durationSeconds/lastSpeechEndUtc/video طبق قرارداد موجود.
- POST .../introduction: audio، clientTurnId، realtimeReceipt و فیلدهای موجود اختیاری.

receipt با DataProtection سمت سرور محافظت شده و متن قطعی/پروایدر/مدل، سازمان/مصاحبه/purpose/سؤال/clientTurnId/نسخه نوبت و SHA256/طول صوت را برای۱۵دقیقه به هم می‌بندد. متن دلخواه کلاینت پذیرفته نمی‌شود. هش قبل ذخیره صوت بررسی و نوبت زیر قفل سازمان دوباره بررسی می‌شود. خطاهای400: realtime_receipt_invalid، realtime_audio_mismatch و realtime_no_speech برای پاسخ/معرفی خالی. audioCheck بی‌گفتار نتیجه موجود speechRecognized=false دارد.

رسید معتبر: صوت/ویدئو طبق قرارداد فعلی ذخیره، transcript/provenance همان متن سرور اتمیک ذخیره و STT مجدد اجرا نمی‌شود؛ پاسخ وارد LLM تصمیم‌گیری می‌شود. معرفی بدون LLM مثل قبل مرحله را جلو می‌برد. retry/idempotency با همان clientTurnId رکورد/ارزیابی دوباره نمی‌سازد؛ recovery از transcript ذخیره‌شده استفاده می‌کند. audioCheck همچنان گذراست و مصاحبه را شروع نمی‌کند.

قطع live: فایل را نگه دار و بدون realtimeReceipt از همان endpoint batch استفاده کن؛ بارگیری ناموفق/receipt نامعتبر نباید خودکار و بی‌پایان درخواست تازه بسازد. بعد timeout ثبت نهایی اول current/messages و clientTurnId موجود بررسی شوند، سپس retry همان نوبت. receipt را از نوبت/فایل دیگری استفاده نکن. recording/session-video جدا از جریان STT است و با این تغییر حذف نمی‌شود.

## استقرار

فرانت Angular باید جدا تغییر کند؛ UI زنده در بک‌اند ساخته نشده است. proxy مسیر /api/candidate/interviews/.../speech/stream باید WebSocket Upgrade، timeout متناسب با ضبط+finalization، WSS و عدم ثبت credential-bearing subprotocol headers داشته باشد. کلیدهای DataProtection بین replicaها باید مانند سایر قراردادهای فعلی مشترک/پایدار باشند. تغییر DB/migration/کلید جدید ندارد؛ کلید Soniox هرگز به مرورگر نمی‌رود.
