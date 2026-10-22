# پرامپت کامل پیاده‌سازی فرانت STT زنده هایریار

روی ریپوی Angular هایریار، با همان معماری و کامپوننت‌های فعلی، تبدیل گفتار به متن زنده را برای مصاحبه پیاده کن. این درخواست مستقل و کامل است. بک‌اند این قابلیت آماده شده و قرارداد کاملش REALTIME_STT_API.md است. محصول فقط فارسی/RTL است؛ هیچ انتخاب زبان یا زیرساخت چندزبانه اضافه نکن. کلیدهای Soniox و سایر پروایدرها وارد فرانت نشوند.

## هدف قابل مشاهده

وقتی کارجو صحبت می‌کند، زیر بخش ضبط صدای او متن همان صحبت در حال شکل‌گرفتن نمایش داده شود. بخش موقت ممکن است اصلاح شود؛ بخش قطعی ثابت‌تر است. پس از پایان صحبت منتظر ضبط و ارسال کامل فایل برای دیدن اولین متن نمانیم. این رفتار فقط وقتی سرور قابلیت realtime اعلام می‌کند فعال باشد. با انتخاب پروایدر فاقد این قابلیت، همان مسیر فعلی ضبط کامل→ارسال فایل→متن حفظ شود و هیچ WebSocketای باز نشود. نام Soniox/مدل را در فرانت hardcode نکن.

## قرارداد قابلیت

پاسخ GET /api/candidate/interviews/{interviewId}/current و پاسخ‌های دیگر CandidateInterviewDto اکنون speechCapabilities دارد. همچنین GET /api/candidate/interviews/{interviewId}/speech/capabilities با همان Authorization: Bearer credential مصاحبه موجود است:

```ts
interface SpeechCapabilities {
  mode: 'realtime' | 'batch';
  streamUrl: string | null;
  protocol: 'hireyar-stt-v1';
}
```

نمونه realtime: {mode:'realtime',streamUrl:'/api/candidate/interviews/ID/speech/stream',protocol:'hireyar-stt-v1'}؛ batch دارای streamUrl:null است. نبود فیلد در بک‌اند قدیمی معادل batch باشد. در شروع هر نوبت ضبط، وضعیت فعلی را بگیر و آن نوبت را با همان حالت تمام کن؛ تنظیمات را وسط یک ضبط عوض نکن. در نوبت بعد قابلیت دوباره از سرور گرفته شود. اگر MediaRecorder یا audio/webm;codecs=opus در مرورگر پشتیبانی نشد، از مسیر موجود batch استفاده کن؛ گزینه ضبط را کلاً حذف نکن.

## سرویس و lifecycle

یک سرویس/کنترل‌کننده با مالکیت روشن برای هر نوبت بساز: idle → connecting → recording → finalizing → submitting → done؛ حالت‌های fallback/error/cancelled جدا باشند. هر نوبت clientTurnId پایدار و یکتا (UUID) داشته باشد و همان در WS و multipart نهایی/تلاش مجدد ارسال شود. clientTurnId را پس از timeout عوض نکن تا پاسخ تکراری ثبت نشود. نوبت سؤال و معرفی و تست میکروفون turnهای مستقل‌اند.

اتصال، stream، recorder، timer و subscriptionها هنگام خروج از صفحه، لغو، پایان موفق یا خطا پاک شوند؛ ضبط/ارسال قبلی به نوبت جدید نشت نکند. رندر transcript با Angular interpolation/textContent و بدون innerHTML باشد. متن/صوت/receipt/کد مصاحبه در console/log/analytics یا localStorage ذخیره نشود. lifecycle را طوری بنویس که دو بار کلیک ضبط، دو اتصال/recorder نسازد.

## WebSocket به بک‌اند خودمان

streamUrl را نسبت به base URL واقعی API حل کن (نه origin فرانت). پروتکل https را wss و http توسعه را ws کن. اتصال فقط به endpoint اعلام‌شده همان API باشد. کد/token مصاحبه در querystring قرار نگیرد:

```ts
const ws = new WebSocket(websocketUrl, [
  'hireyar-stt-v1',
  `hireyar-interview.${existingCandidateCredential}`
]);
```

existingCandidateCredential همان مقداری است که فرانت فعلی برای Authorization مصاحبه دارد؛ توکن HR یا کلید Soniox نیست. سرور فقط hireyar-stt-v1 را در پاسخ انتخاب می‌کند. Origin عادی مرورگر حفظ شود؛ جعل Origin یا قرار دادن کلید خصوصی provider در مرورگر انجام نده. proxy/CSP محیط باید WSS به API را اجازه بدهد؛ برای این مورد با مسئول استقرار هماهنگ کن، تنظیمات امنیتی کل سایت را بی‌دلیل باز نکن.

بلافاصله بعد open و پیش از ضبط، start بفرست:

```json
{"type":"start","purpose":"answer","clientTurnId":"UUID","questionId":"CURRENT_QUESTION_ID"}
```

purpose برای تست قبل شروع audioCheck، برای معرفی introduction و برای پاسخ سؤال answer است. فقط answer به questionId فعلی (state.question.id) نیاز دارد؛ در دو حالت دیگر questionId را حذف/null کن. سرور حدود۱۰ثانیه برای start منتظر می‌ماند.

منتظر ready باش:

```json
{"type":"ready","maxSeconds":600,"maxBytes":20971520,"finalizationTimeoutSeconds":60,"format":"webm","fallback":"batch"}
```

این اعداد مثال‌اند؛ مقادیر دریافتی سرور سقف واقعی‌اند. audioCheck معمولاً حداکثر۳۰ثانیه و۲MiB، پاسخ/معرفی طبق تنظیمات‌اند. ready یعنی مسیر اپ آماده دریافت است؛ موفقیت نهایی provider فقط completed است. نمایش «متصل/آماده» با «متن تأییدشده» متفاوت باشد.

## ضبط هم‌زمان، جریان و فایل نهایی

از یک MediaRecorder پیوسته با audio/webm;codecs=opus و bitrate مناسب گفتار (مثلاً۶۴kbps در صورت پشتیبانی مرورگر) استفاده کن. recorder.start با timeslice حدود۲۵۰میلی‌ثانیه باشد. همه dataavailableهای غیرخالی هم برای فایل نهایی نگه داشته شوند و هم به ترتیب به WebSocket ارسال شوند. برای هر chunk یک recorder جدید نساز و recorder را بین chunkها stop/start نکن؛ همه بایت‌ها یک container پیوسته‌اند.

اگر لازم است Blob را arrayBuffer کنی، تبدیل/ارسال chunkها را با زنجیره Promise/صف مرتب کن تا async شدن باعث جابه‌جایی ترتیب نشود. هر WebSocket binary message حداکثر۶۵۵۳۶بایت است؛ chunk بزرگ‌تر را به sliceهای حداکثر۱۶KB تقسیم کن، با حفظ دقیق ترتیب بایت‌ها. Base64 یا JSON صوت نفرست. داده ابتدای جریان باید header اصلی WebM را داشته باشد. فایل نهایی از concat همان chunkها و بدون تغییر/تبدیل codec ساخته شود؛ hash سرور باید دقیقاً با آن برابر باشد.

صف و ws.bufferedAmount را محدود کن (مثلاً۲۵۶KB)؛ صف نامحدود یا حذف بی‌صدای chunk نساز. اگر شبکه عقب افتاد، live را ناموفق علامت بزن، اتصال را ببند، اما recorder و chunkهای لازم برای فایل نهایی را حفظ کن تا مسیر batch در پایان پاسخ کار کند. maxBytes و maxSeconds سرور را در UI رعایت کن. سرور اتصال بدون داده را حدود۱۵ثانیه می‌بندد؛ سکوت عادی صوتی با MediaRecorder همچنان chunk تولید می‌کند. WebSocket خالی را قبل از ضبط مدت طولانی باز نگه ندار.

ذخیره صوت/ویدئوی خصوصی و ضبط جلسه طبق رفتار فعلی باقی بماند. برای STT یک ضبط صوتی مستقل از recorder ویدئو لازم است؛ بایت‌های receipt از همان recorder صوتی باشند، نه track یا فایلی که بعداً از ویدئو استخراج می‌شود. VAD و منطق پایان پاسخ فعلی را حفظ کن؛ سکوت کوتاه یا یک interim خالی به معنی پایان پاسخ نیست. قطع صدای مصاحبه‌گر و جلوگیری از شنیدن صدای پخش‌شده خودش طبق منطق فعلی حفظ شوند.

## نمایش زنده زیر ضبط

پیام transcript:

```json
{"type":"transcript","finalText":"متن قطعی تا این لحظه","interimText":"عبارت موقت فعلی"}
```

این دو فیلد snapshot هستند. مقدار قطعی فعلی را با finalText جایگزین و مقدار موقت فعلی را با interimText جایگزین کن؛ کل متن هر پیام را با += به قبلی نچسبان چون تکراری می‌شود. بخش موقت با رنگ ملایم/نشانه درحال‌تکمیل نمایش داده شود و با قطعی‌شدن جای خود را بدهد. متن فارسی RTL و خوانا، با همان تم اپ؛ متن فنی provider/receipt به کارجو نمایش داده نشود. placeholder «در حال دریافت گفتار…»، حالت شبکه ضعیف و خطای روشن فارسی داشته باشد. تغییرات زنده برای screen reader با aria-live=polite و به‌صورت کنترل‌شده اعلام شوند، نه به ازای هر token و پرش focus.

نمایش متن زنده در هر سه بخش تست میکروفون، معرفی و پاسخ سؤال باشد. هنگام حرف زدن مصاحبه‌گر، متنِ پاسخ کارجو را از روی STT قبلی یا متن TTS نمایش نده. اگر هیچ گفتاری تشخیص داده نشد، در تست میکروفون پیام بررسی میکروفون و تلاش مجدد؛ برای پاسخ/معرفی خالی، کاربر فرصت اصلاح داشته باشد.

## پایان و ثبت نهایی

پس از پایان صحبت: recorder.stop کن، منتظر آخرین dataavailable و stop بمان، همه chunkهای صف‌شده را به ترتیب بفرست و فقط سپس پیام text {"type":"finish"} ارسال کن. این ترتیب حیاتی است. تا پایان provider، حالت «در حال نهایی‌سازی متن…» و فایل ضبط‌شده حفظ شود. deadline کلاینت از finalizationTimeoutSeconds سرور با کمی مهلت شبکه گرفته شود؛ دوباره recorder را باز نکن.

موفقیت فقط این پیام است:

```json
{"type":"completed","finalText":"متن نهایی کامل","receipt":"OPAQUE_VALUE","audioBytes":75014}
```

متن موقت را پاک، متن نهایی را جایگزین و receipt را فقط در حافظه همان نوبت نگه دار. audioBytes باید با اندازه فایل نهایی برابر باشد؛ اگر برابر نبود receipt را برای فایل تغییرکرده نفرست و حالت fallback را واضح کن. بسته‌شدن طبیعی socket بعد completed خطا نیست و نباید باعث ارسال دوباره/استفاده از batch شود.

همان Blob کامل صوت را در مسیرهای فعلی multipart ثبت کن:

- تست میکروفون: POST /api/candidate/interviews/{id}/audio-check با audio، durationSeconds، clientTurnId و realtimeReceipt.
- پاسخ سؤال: POST .../answers با questionId، audio، clientTurnId و realtimeReceipt؛ durationSeconds/lastSpeechEndUtc/video مثل قرارداد فعلی.
- معرفی: POST .../introduction با audio، clientTurnId و realtimeReceipt و فیلدهای فعلی اختیاری.

نام فیلد دقیق realtimeReceipt است؛ finalText کلاینت را به‌عنوان transcript قابل اعتماد به سرور نفرست. receipt سرور برای۱۵دقیقه به همان سازمان/مصاحبه/نوبت/سؤال و SHA256/اندازه صوت متصل است. سرور با رسید معتبر STT را دوباره اجرا نمی‌کند؛ متن قطعی خودش را ذخیره و ادامه تصمیم‌گیری را انجام می‌دهد. معرفی همچنان ارزیابی مهارت/LLM ندارد؛ تست میکروفون جلسه را شروع یا پاسخ ذخیره نمی‌کند.

برای تست میکروفون، موفقیت «صوت دریافت شد/گفتار تشخیص داده شد» را از پاسخ audio-check نهایی بگیر؛ نمایش interim به‌تنهایی مجوز عبور از تست نیست. تأیید تطابق متن توسط کارجو طبق UI موجود بماند. در سؤال و معرفی، متن موقت به ارزیابی/گزارش/تاریخچه نهایی اضافه نشود و بعد تکمیل، متن معتبر پاسخ HTTP/current مرجع باشد.

## batch و بازیابی خطا

وقتی mode=batch یا قابلیت قدیمی/مرورگر ناسازگار است، هیچ اتصال زنده نساز؛ همان ضبط کامل و POST قبلی بدون realtimeReceipt اجرا شود. محل زیر ضبط فقط وضعیت مناسب «پس از پایان ضبط پردازش می‌شود» داشته باشد و وعده متن زنده نده. حالت Soniox را در همه پروایدرها تقلید نکن.

اگر error event، قطع socket پیش از completed، timeout یا عقب‌افتادن شبکه رخ داد: فایل و clientTurnId را حفظ کن، receipt ناقص/قدیمی را کنار بگذار، متن موقت را به‌عنوان متن قطعی قبول نکن و یک‌بار به مسیر موجود batch بدون realtimeReceipt برگرد. لازم نیست کارجو دوباره همان پاسخ را بگوید. پیام فارسی واضح بده که نمایش زنده قطع شده ولی پاسخ ضبط شده است. حلقه reconnect و بارگذاری دوباره خودکار بی‌پایان نساز. اگر کد انقضا/لغو/تغییرآدرس مصاحبه است، ثبت نهایی را متوقف و همان حالت موجود دسترسی را نشان بده؛ fallback مجوز دورزدن سرور نیست.

نمونه error: {type:'error',code:'stt_timeout',fallback:'batch',message:'...'}؛ کدها ممکن است realtime_interrupted، realtime_already_connected، realtime_frame_invalid، audio_invalid یا stt_http_... باشند. مرورگر همیشه status بدنه handshake ناموفق را به JS نمی‌دهد؛ در failure حداکثر یک بار capabilities/current را از HTTP بخوان تا حالت جدید/انقضا مشخص شود. receipt نامعتبر/صوت متفاوت400 با realtime_receipt_invalid یا realtime_audio_mismatch است؛ متن دلخواه جایگزین نکن. قبل از fallback برای خطای HTTP ثبت نهایی یا timeout، اول current/messages و وضعیت clientTurnId را بررسی کن تا پاسخ موفق دوباره ثبت نشود.

تلاش مجدد همان turn و همان فایل را نگه دارد. اگر ثبت موفق شده اما پاسخ شبکه گم شده، همان clientTurnId مسیر idempotency را فعال می‌کند. با شروع turn جدید receipt و متن نوبت قبل پاک شوند. تغییر provider میان finalization و POST با receipt معتبر به‌تنهایی دلیل حذف receipt نیست؛ سرور provenance ثبت‌شده را می‌پذیرد.

## تست‌های پذیرش لازم

۱. mode=realtime: متن موقت قبل از پایان ضبط ظاهر شود، اصلاح interim بدون تکرار متن، تبدیل به قطعی و finalText نهایی صحیح؛ ضبط طولانی‌تر از timeout معمول batch نباید صرفاً به خاطر۶۰ثانیه قطع شود.
۲. mode=batch/نبود فیلد/مرورگر بدون WebM: هیچ WS ساخته نشود؛ ضبط و ارسال فعلی سالم باشد.
۳. صوت دقیق همان بایت‌های stream باشد؛ chunkبندی متفاوت، ترتیب async و آخرین dataavailable تست شوند؛ ثبت موفق نباید تماس STT دوم بسازد.
۴. قطع اتصال در شروع/وسط/نهایی‌سازی، backpressure، شبکه کند، خروج صفحه و دو کلیک؛ فایل ویدئو/صوت از دست نرود و turn قبلی به بعدی نشت نکند.
۵. completed سپس close، timeout ارسال HTTP پس از ثبت موفق و retry با همان clientTurnId؛ هیچ پاسخ/پردازش تکراری ساخته نشود.
۶. تست میکروفون/معرفی/سؤال هر سه؛ معرفی بدون LLM؛ speechRecognized=false برای سکوت؛ تست میکروفون هیچ شروع/پاسخی ثبت نکند.
۷. مصاحبه دیگر/کد اشتباه/لغو یا انقضا هنگام اتصال/سؤال عوض‌شده/receipt دستکاری‌شده یا فایل متفاوت؛ وضعیت خطا دقیق باشد و هیچ کلید/receipt/صوت در log نباشد.
۸. تعویض پروایدر در نوبت بعد و حفظ snapshot نوبت جاری؛ نمایش صحیح روی موبایل/RTL/صفحه‌کلید و screen reader.

تست واحد سرویس/کامپوننت برای ترتیب chunk و state machine، build فرانت و یک تست مرورگر با میکروفون مصنوعی اجرا کن. در گزارش مشخص کن چه چیزی واقعاً تست شد؛ اتصال صرف WS یا mock success را تست کامل provider واقعی ننام. انتشار را فقط طبق درخواست جداگانه انجام بده. این پرامپت نیاز به تغییر فرم نقش‌ها/دعوت کاربر یا زبان محصول ندارد.
