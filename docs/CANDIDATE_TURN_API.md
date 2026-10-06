# قرارداد گفت‌وگوی خودکار نوبتی داوطلب — 2026-10-06

این قرارداد روی dev پیاده و با ۱۷۵ تست PostgreSQL واقعی تأیید شده است؛ هنوز در مقصد منتشر نشده. قبل از کد جدید migration `20261006170044_AddCandidateAnswerCapture` لازم است. طبق تصحیح کاربر، WebRTC/Realtime در این درخواست وجود ندارد؛ فرانت با getUserMedia، MediaRecorder، تشخیص سکوت و HTTP کار می‌کند. Angular در ریپوی دیگری است. AI همچنان STT → ارزیابی → انتخاب سؤال → TTS است؛ ویدیو برای ذخیره/بازبینی HR است و به AI ارسال نمی‌شود.

## احراز هویت و مسیرها

پیشوند داوطلب: `/api/candidate/interviews/{interviewId}`. همه درخواست‌ها `Authorization: Bearer <interview-token>` دارند؛ توکن opaque همان جلسه است، نه JWT حساب HR. توکن در query، لاگ، analytics یا localStorage قرار نگیرد. HR با JWT و Owner سروری به گزارش همان سازمان دسترسی دارد. لینک فایل نسبی است و به origin API resolve شود.

| روش/مسیر | ورودی | خروجی |
|---|---|---|
| GET `/current` | ندارد | CandidateInterviewDto |
| POST `/start` | ندارد | CandidateInterviewDto؛ فقط Created را با اقدام کاربر شروع کنید |
| POST `/answers` | multipart زیر | CandidateInterviewDto |
| POST `/answers/{answerId}/retry` | بدون فایل و body | پردازش دوباره همان پاسخ persistشده |
| GET `/messages` | ندارد | آرایه پیام‌های همین جلسه، با ترتیب زمانی سرور |
| POST `/complete` | ندارد | فقط WaitingForAnswer؛ پایان زودهنگام با اقدام کاربر |
| GET `/questions/{questionId}/audio` | ندارد | audio/mpeg خصوصی |

خروجی‌های start/current/answers/retry/complete علاوه بر فیلدهای قبلی، این اطلاعات را دارند:

```ts
interface InterviewMediaCapabilities {
  videoAnswers: boolean;
  maxAudioBytes: number;  // default: 20971520 (20 MiB)
  maxVideoBytes: number;  // default: 41943040 (40 MiB)
  maxAnswerSeconds: number; // default: 600 (10 minutes)
}
// CandidateInterviewDto.mediaCapabilities: InterviewMediaCapabilities
// CandidateInterviewDto.answer همچنان nullable است.
interface CandidateAnswer {
  id: string;
  questionId: string;
  status: 'Uploaded' | 'Transcribing' | 'Transcribed' | 'Evaluating' | 'Processed' | 'Failed';
  canRetry: boolean;
  errorCode: string | null;
  transcript: string | null;
  clientTurnId: string | null;
  submittedAtUtc: string;
  lastSpeechEndUtc: string | null;
  durationSeconds: number | null;
  videoAvailable: boolean; // وجود فایل برای همین پاسخ؛ با توان ضبط اشتباه نشود
}
```

`mediaCapabilities.videoAnswers` روی این نسخه true است؛ روی نسخه قدیمی/نبود فلگ، ضبط و ارسال ویدیو انجام نشود و preview دوربین به‌تنهایی مجاز است. سقف‌ها را از پاسخ بخوانید. ویدیو همواره اختیاری است؛ رد مجوز دوربین مانع مصاحبه صوتی نشود.

## فرم پاسخ

Swagger فقط `multipart/form-data` دارد؛ `audio` و `video` از نوع string/binary هستند. Content-Type درخواست را دستی تنظیم نکنید تا مرورگر boundary را بسازد.

| فیلد | قرارداد |
|---|---|
| questionId | Guid سؤال جاری، الزامی |
| audio | فایل الزامی؛ سقف پیش‌فرض ۲۰ MiB و فرمت/پسوند/type واقعی مطابق قرارداد صوت قبلی |
| durationSeconds | اختیاری، مثبت و حداکثر mediaCapabilities.maxAnswerSeconds؛ مدت ضبط کلاینت، حقیقت مالی نیست |
| clientTurnId | رشته بدون فاصله ابتدا/انتها، ۱..۱۲۸ کاراکتر، case-sensitive؛ UUID با crypto.randomUUID پیشنهاد می‌شود. برای فرانت جدید هر نوبت الزامی است؛ برای سازگاری مصرف‌کننده قدیمی سرور null را می‌پذیرد |
| lastSpeechEndUtc | اختیاری برای سازگاری قدیمی؛ فرانت جدید ISO UTC با toISOString بفرستد. زمان آخرین صدای تشخیص‌داده‌شده، قبل از انتظار ۲ ثانیه‌ای سکوت |
| video | اختیاری، پیش‌فرض حداکثر ۴۰ MiB؛ WebM با video/webm یا MP4 با video/mp4، پسوند و امضای container باید مطابق باشند |

timestamp غیر UTC، بیش از ۳۰ ثانیه در آینده زمان دریافت سرور یا پیش از شروع جلسه با ارفاق ۳۰ ثانیه → 400 last_speech_end_invalid. ساعت مرورگر همگام باشد. زمان در PostgreSQL دقت میکروثانیه دارد. lastSpeechEndUtc صرفاً metadata است و سیاست مالی محدود مبتنی بر زمان سرور را تغییر نمی‌دهد؛ دو ثانیه سکوت را از timestamp کلاینت حذف کنید، نه با کم‌کردن حدسی از ساعت دریافت سرور.

اعتبارسنجی فایل در این نسخه اندازه/پسوند/MIME/امضای container است، نه decode، اندازه‌گیری قطعی مدت، اثبات وجود track تصویری یا اسکن فایل. audio همیشه جدا لازم است؛ یک فایل video جای audio را نمی‌گیرد. نام اصلی فایل ذخیره نمی‌شود. ویدیو در storage خصوصی موجود و volume interview-storage، با key نسبی category=videos نگهداری می‌شود؛ پسوند داخلی مشترک .audio است و نوع دانلود از metadata می‌آید.

```ts
const form = new FormData();
form.append('questionId', questionId);
form.append('audio', audioBlob, audioFileName);
form.append('durationSeconds', String(Math.max(1, Math.ceil(recordedSeconds))));
form.append('clientTurnId', turnId); // یک بار هنگام آغاز نوبت بسازید
form.append('lastSpeechEndUtc', new Date(lastSpeechEndMs).toISOString());
if (capabilities.videoAnswers && videoBlob) {
  form.append('video', videoBlob, videoFileName);
}
// POST /api/candidate/interviews/{interviewId}/answers با Bearer داوطلب
```

API سقف کل درخواست ۹۲ MiB دارد تا تنظیم صوت حداکثر ۵۰ MiB، ویدیو ۴۰ MiB و overhead جا شوند. سقف واقعی هر فایل مستقل enforce می‌شود. proxy/ingress نیز باید حجم multipart پیش‌فرض بیش از ۶۰ MiB و timeout کافی داشته باشد؛ محدودیت proxy با این کد تغییر نمی‌کند.

## ارسال تکراری و بازیابی

اولین ثبت موفق `(interviewId, clientTurnId)` برنده است؛ unique index PostgreSQL و قفل سازمان ثبت را اتمیک می‌کنند. تکرار همان شناسه برای همان questionId پاسخ جدید، STT، ارزیابی یا هزینه تکراری نمی‌سازد؛ HTTP 200 با همان answer.id و وضعیت فعلی آن برمی‌گردد. body/فایل‌های ارسال مجدد جای داده ثبت‌شده را نمی‌گیرند. پاسخ JSON کپی ثابت پاسخ اولیه نیست؛ وضعیت فعلی جلسه و همان answer برمی‌گردد. حتی پس از عبور به سؤال بعدی یا Completed، با توکن هنوز معتبر replay همان نوبت ممکن است.

استفاده همان clientTurnId برای سؤال دیگر → 409 client_turn_id_conflict. نوبت تازه حتماً شناسه تازه داشته باشد؛ retry انتقال همان شناسه قبلی را حفظ کند. کلاینت قدیمی بدون clientTurnId تضمین replay جدید را ندارد و همچنان از unique question/session و conflict قبلی استفاده می‌کند. بعد از 401/انقضا/ابطال دسترسی، replay مجوز دورزدن auth نیست.

در قطع ارتباط اول current را بخوانید. اگر پاسخ ثبت شده/processing است، فایل تازه نفرستید و وضعیت را بررسی کنید. اگر answer.canRetry=true، POST همان answer.id/retry بدون فایل انجام شود؛ replay /answers پردازش متوقف‌شده را دوباره راه نمی‌اندازد. اگر هنوز همان سؤال آماده ضبط است و ثبت پاسخ روشن نیست، همان Blob و همان clientTurnId را دوباره ارسال کنید؛ برای retry شبکه شناسه تازه تولید نکنید. اگر Blob بعد از reload از دست رفته، state/history را بازیابی کنید؛ در نبود پاسخ ثبت‌شده همان سؤال را با اقدام کاربر دوباره ضبط کنید.

`answer` ممکن است برای سؤال قبلی باشد، در حالی که `question` سؤال جدید است؛ بر اساس questionId/id تطبیق دهید. metadata شناسه نوبت را می‌توان طبق سیاست موجود در sessionStorage همان tab نگه داشت؛ فایل/متن حساس/توکن در لاگ یا storage عمومی ذخیره نشود.

## متن و تاریخچه

```ts
interface CandidateMessage {
  id: string; // question:<questionId> یا answer:<answerId>، پایدار
  questionId: string;
  answerId: string | null;
  role: 'assistant' | 'candidate';
  text: string | null;
  createdAtUtc: string; // ساعت سرور
  status: string | null;
}
```

messages فقط سؤال‌های مطرح‌شده و پاسخ‌های همین جلسه را می‌دهد؛ بانک سؤال آینده، evidence، score یا prompt/provider metadata را به داوطلب نمی‌دهد. transcript تا پایان STT null است؛ Failed/Transcribing را «در حال آماده‌شدن متن» یا وضعیت مناسب نشان دهید، نه متن ساختگی. بعد از STT متن همان پیام با همان id تکمیل می‌شود. timestamp سؤال هنگام Start/AskNext ثبت شده و با reload تغییر نمی‌کند؛ برای جلسات legacy بدون AskedAtUtc، timestamp پاسخ یا شروع سرور fallback است و زمان تاریخی دقیق پرسش ادعا نمی‌شود. تاریخچه محدود به plan حداکثر ۳۰ سؤال است و pagination لازم نیست.

در بازکردن صفحه current و messages را بخوانید و پیام‌ها را با id upsert کنید. در processing فقط current هر ۲ ثانیه، حداکثر یک درخواست جاری؛ messages را پس از تغییر answer/status/transcript یا پایان پاسخ refresh کنید، نه هر ۲ ثانیه کنار current. محدودیت API داوطلب ۶۰ درخواست/دقیقه است. با 429 سرعت را کم کنید؛ با خروج/401/410/Completed/Cancelled polling را متوقف کنید.

## ضبط و نمایش

پس از اقدام اولیه کاربر و مجوز میکروفون، سؤال را با درخواست authenticated دریافت و به Blob URL تبدیل کنید. هنگام پخش AI ضبط پاسخ/تشخیص سکوت برای ارسال فعال نباشد؛ پایان پخش → شروع نوبت و recorder. سکوت اولیه پیش از اولین speech موجب پاسخ خالی نشود. آخرین speech را به‌روزرسانی کنید؛ پس از ۲ ثانیه سکوت recorderها را stop و پس از dataavailable نهایی هر دو، فرم را یک‌بار ارسال کنید. تا سؤال تازه/پایان مشخص نشده recorder دوباره شروع نشود. autoplay blocked دکمه پخش روشن داشته باشد؛ TTS unavailable متن سؤال را نمایش دهد و ادامه صوتی با اقدام کاربر قابل انجام باشد. در خروج recorderها، trackها، audio nodes، timerها و Blob URLها آزاد شوند.

## بازبینی HR و خطاها

گزارش `GET /api/interviews/{id}` در هر answer، videoUrl nullable، clientTurnId، lastSpeechEndUtc و durationSeconds دارد. `GET /api/interviews/{id}/answers/{answerId}/video` فایل خصوصی با MIME ثبت‌شده و range processing می‌دهد؛ JWT HR همان سازمان و retention مالی موجود لازم‌اند. token داوطلب به مسیر HR دسترسی ندارد. لیست پنل videoAvailable را بر اساس وجود فایل واقعی در پاسخ‌ها می‌دهد؛ true تضمین پایان retention یا صحت decode نیست. 404 برای فایل غایب/خارج scope/پایان retention حفظ است.

400: فایل/فیلد نامعتبر؛ 413: audio_too_large یا video_too_large؛ 409: وضعیت/سؤال/شناسه نوبت ناسازگار؛ 401/410: دسترسی/انقضا؛ 429: محدودیت درخواست. شکست STT همچنان ممکن است HTTP 200 با ProcessingAnswer و answer.canRetry=true باشد. متن خطای provider یا انتخاب استخدام/رد به داوطلب نمایش داده نشود. فایل تازه در شکست ذخیره و بازنده رقابت حذف می‌شود؛ crash فرایند همچنان ممکن است orphan داشته باشد و cleanup عملیاتی مستقل است.
