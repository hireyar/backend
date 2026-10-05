# تحویل مصاحبه صوتی هوشمند به فرانت‌اند

به‌روزرسانی پنل 2026-10-05: برای ساخت موقعیت/قالب/دعوت و فهرست مصاحبه‌ها، [PANEL_WIZARD_API](PANEL_WIZARD_API.md) مرجع جدید است. محدودیت‌های تاریخی پایین درباره نبود فهرست و نیاز به ساخت دستی مهارت/بانک برای فرم HR با مسیر جدید ویزارد رفع شده‌اند. قرارداد candidate، صوت، retry و گزارش این سند همچنان معتبر است. ایمیل/SMS و ویدیو همچنان پیاده نشده‌اند و پاسخ ویزارد آن‌ها را صریح نشان می‌دهد.

تغییر قرارداد backend Pricing، 2026-10-01: قبل از invite/start، اشتراک و AI Credit سازمان را از `/api/billing/subscription` و `/api/billing/ai-credit` بررسی کنید. `active_subscription_required`/`subscription_feature_required` برابر 403 و `insufficient_ai_credit` برابر 409 هستند؛ در UI داوطلب جزئیات کیف پول عرضه نشود. CreateInterviewRequest اکنون `requiredCapabilityCode` اختیاری دارد (پیش‌فرض technical_interview، برای HR مقدار hr_interview). API و نمونه‌های purchase و gatewayEnabled=false در [BILLING](BILLING.md). فرانت قیمت authoritative یا billable duration ارسال نکند؛ rate/billing/feature snapshot سروری است. متن قدیمی این راهنما دربارهٔ شروع بدون Billing با این بند superseded است.

مرجع قرارداد فعلی بک‌اند HireYar، 2026-10-01. این فایل را همراه درخواست انتهای آن به Codex در **ریپوی فعلی فرانت‌اند** بدهید. پنل Angular در ریپوی بک‌اند نیست. رابط را با ساختار، کامپوننت‌ها و کلاینت HTTP موجود بسازید؛ پروژه یا معماری جدید لازم نیست.

## محدوده این مرحله

1. صفحه عمومی داوطلب: ورود با شناسه مصاحبه و توکن، شروع، شنیدن/خواندن سؤال، ضبط و ارسال پاسخ، انتظار، retry و پایان.
2. صفحه HR برای گزارش یک مصاحبه و شنیدن پاسخ‌ها.
3. فرم ساده HR برای بانک سؤال و ساخت درخواست داوطلب/دعوت، با استفاده از موقعیت و قالب موجود.

تصمیم استخدام/رد خودکار، چت، پخش زنده، WebSocket، داشبورد تحلیلی و سیستم ارسال دعوت نسازید. انتخاب سؤال، ارزیابی و پایان خودکار مسئولیت بک‌اند است. امتیاز و transcript به داوطلب نمایش داده نمی‌شوند.

پیش‌نیاز اجرای واقعی: migration `20261001061608_AddCandidateInterviewFlow` در محیط مقصد اعمال و AI و storage خصوصی بک‌اند تنظیم شده باشند. در audit بک‌اند 2026-10-01، smoke واقعی TTS/STT/LLM با داده مصنوعی موفق شد؛ اتصال و تنظیمات محیط فرانت/استقرار همچنان باید بررسی شوند. کلید AI هرگز وارد فرانت نمی‌شود.

## قواعد مشترک API

- مسیرهای زیر نسبت به origin بک‌اند هستند. JSON به صورت camelCase و **بدون envelope** برمی‌گردد.
- شناسه‌ها رشته Guid، زمان‌ها UTC و nullableها واقعاً `null` هستند. تاریخ را در UI به زمان محلی تبدیل کنید.
- HR از JWT و ورود موجود، با نقش Owner یا Recruiter استفاده می‌کند.
- داوطلب از `Authorization: Bearer <interview-token>` استفاده می‌کند. این توکن opaque است، JWT نیست و فقط برای همان جلسه معتبر است.
- صفحه داوطلب HR login guard ندارد. interceptor مربوط به HR نباید توکن داوطلب را جایگزین کند یا برای 401 آن refresh/logout حساب HR اجرا کند.
- توکن در URL، لاگ، analytics یا localStorage قرار نگیرد. ورود دستی توکن کافی است؛ آن را در حافظه نگه دارید. اگر حفظ ورود پس از refresh لازم است، مطابق سیاست موجود فرانت فقط sessionStorage همان tab و همان interviewId را به کار ببرید و در خروج/401 پاک کنید. refresh با ورود دوباره توکن هم باید قابل استفاده باشد.

## API داوطلب

پیشوند همه مسیرها: `/api/candidate/interviews/{interviewId}`؛ همه با توکن داوطلب.

| روش و ادامه مسیر | ورودی | خروجی موفق |
|---|---|---|
| GET `/current` | ندارد | 200، CandidateInterview |
| POST `/start` | بدون body | 200، CandidateInterview |
| POST `/answers` | multipart؛ توضیح پایین | 200، CandidateInterview |
| POST `/answers/{answerId}/retry` | بدون body و بدون فایل | 200، CandidateInterview |
| POST `/complete` | بدون body | 200، CandidateInterview |
| GET `/questions/{questionId}/audio` | ندارد | فایل audio/mpeg |

در ورود یا refresh ابتدا `current` بگیرید. فقط در وضعیت Created و با اقدام کاربر `start` بزنید؛ `current` سؤال جدید انتخاب نمی‌کند. شروع مجدد جلسه شروع‌شده معمولاً همان state را می‌دهد، ولی برای جلسه پایان‌یافته/لغوشده مجاز نیست.

```ts
type InterviewStatus = 'Created' | 'WaitingForAnswer' | 'ProcessingAnswer'
  | 'Completed' | 'Cancelled';
type AnswerStatus = 'Uploaded' | 'Transcribing' | 'Transcribed'
  | 'Evaluating' | 'Processed' | 'Failed';

interface CandidateInterview {
  interviewId: string;
  status: InterviewStatus;
  canRecord: boolean;
  isProcessing: boolean;
  question: {
    id: string; text: string;
    audioUrl: string | null; audioAvailable: boolean;
  } | null;
  progress: {
    answeredQuestions: number; maxQuestions: number; remainingSeconds: number;
  };
  answer: {
    id: string; questionId: string; status: AnswerStatus;
    canRetry: boolean; errorCode: string | null;
  } | null;
  completionReason: string | null;
}
```

### رفتار صفحه

| state/شرط | رفتار |
|---|---|
| Created | توضیح کوتاه و دکمه شروع |
| WaitingForAnswer و canRecord | سؤال، پخش صوت در صورت وجود، ضبط و ارسال |
| درخواست ارسال در حال اجرا | busy محلی؛ جلوگیری از ارسال/ضبط/پایان تکراری |
| ProcessingAnswer و isProcessing | نمایش انتظار؛ ضبط و complete غیرفعال |
| answer.canRetry و !isProcessing | دکمه «پردازش دوباره پاسخ» با answer.id؛ فایل مجدد ارسال نشود |
| Completed یا Cancelled | توقف ضبط و polling؛ نمایش پایان، بدون نتیجه استخدام |

`canRecord` و `canRetry` مرجع هستند. canRetry ممکن است بعد از انقضای lease حتی با answer.status برابر Evaluating/Transcribing فعال شود. شکست STT می‌تواند **HTTP 200** با ProcessingAnswer، answer.status=Failed و errorCode=answer_retry_available بدهد؛ آن را رفتن به سؤال بعدی حساب نکنید. شکست ارزیابی AI ممکن است با fallback بک‌اند به سؤال بعدی برسد؛ جزئیات provider به داوطلب نشان داده نشود.

در حالت انتظار فقط `current` را مثلاً هر ۵ ثانیه، با یک درخواست هم‌زمان و هنگام نمایش صفحه بخوانید. polling پردازش را دوباره راه نمی‌اندازد. با پاسخ نهایی، پایان، 401 یا خروج از صفحه متوقف شود. POST را خودکار تکرار نکنید.

`answer` ممکن است پاسخ سؤال قبلی باشد، در حالی که `question` سؤال جدید است؛ اتصال را با questionId بررسی کنید. شمارنده زمان صرفاً نمایش است؛ با صفر شدن تایمر سؤال را عوض نکنید یا وضعیت Completed نسازید. `complete` فقط در WaitingForAnswer مجاز است؛ پایان زودهنگام کاربر را با تأیید ساده انجام دهید.

### ضبط و ارسال صوت

از MediaRecorder و تشخیص `isTypeSupported` استفاده کنید. نوع واقعی Blob، پسوند فایل و Content-Type باید با هم سازگار باشند. پیش‌فرض بک‌اند: حداکثر **۲۰ MiB (20971520 بایت)** و **۶۰۰ ثانیه** برای هر پاسخ؛ این مقدارها قابل تنظیم در بک‌اند هستند و API تنظیمات عمومی ندارند. با تیم بک‌اند برای override هماهنگ کنید.

فرمت‌های قابل قبول: webm (`audio/webm` یا `video/webm`)، m4a (`audio/mp4` یا `audio/x-m4a`)، ogg (`audio/ogg` یا `application/ogg`)، mp3 (`audio/mpeg` یا `audio/mp3`) و wav (`audio/wav` یا `audio/x-wav`). فقط فرمت واقعاً پشتیبانی‌شده مرورگر را ضبط کنید؛ تغییر نام فایل تبدیل فرمت نیست. کتابخانه تبدیل صوت اضافه نکنید.

```ts
const form = new FormData();
form.append('questionId', currentQuestionId);
form.append('audio', blob, fileNameWithMatchingExtension);
form.append('durationSeconds', String(Math.max(1, Math.ceil(recordedSeconds))));
// POST .../answers با Authorization داوطلب
// Content-Type را دستی نگذارید؛ مرورگر multipart boundary را می‌سازد.
```

`durationSeconds` اختیاری است؛ در صورت ارسال باید مثبت و در سقف مجاز باشد. فیلد clientStartedAt/clientEndedAt وجود ندارد. پردازش در همان درخواست انجام می‌شود و ممکن است طولانی باشد؛ timeout کوتاه کلاینت نگذارید. در طول پخش سؤال ضبط را شروع نکنید. مجوز میکروفون، نبود MediaRecorder و نیاز به HTTPS/localhost را با پیام قابل فهم مدیریت کنید. در خروج، trackهای میکروفون و recorder را آزاد کنید.

اگر ارتباط ارسال قطع شد، ابتدا `current` بخوانید: اگر پاسخ ذخیره و در حال پردازش است منتظر بمانید؛ اگر canRetry فعال است retry همان answer؛ اگر سؤال جلو رفته از state جدید استفاده کنید. فقط اگر همان سؤال هنوز آماده پاسخ است و پاسخ آن ثبت نشده، ارسال دوباره Blob با اقدام کاربر انجام شود. ارسال POST را کورکورانه retry نکنید. Blob را فقط تا تعیین وضعیت در حافظه نگه دارید؛ صوت را در storage مرورگر ذخیره نکنید.

### پخش فایل خصوصی

audioUrl مسیر API است. آن را با origin درست resolve کنید؛ `/api` را دو بار نچسبانید. `<audio src="API URL">` هدر Authorization را نمی‌فرستد. فایل را با کلاینت احراز هویت‌شده به Blob بگیرید، `URL.createObjectURL` بسازید و آن را در audio controls بگذارید. در تغییر سؤال/خروج `URL.revokeObjectURL` بزنید. autoplay اجباری نباشد.

نبود صوت، audioAvailable=false، 404 یا خطای دانلود مانع پاسخ دادن نیست؛ متن سؤال همیشه قابل استفاده باشد. HR نیز برای شنیدن صوت باید Blob را با JWT خودش دریافت کند.

## API مدیریت HR

همه مسیرها با JWT موجود HR. موقعیت شغلی و قالب را از صفحات/کلاینت موجود انتخاب کنید (`/api/job-positions` و `/api/interview-templates?jobPositionId=...` با قرارداد صفحه‌بندی موجود).

| روش و مسیر | body | خروجی موفق |
|---|---|---|
| POST `/api/skills` | `{name}` | 200، `{id}` |
| PUT `/api/job-positions/{positionId}/skills/{skillId}` | `{isRequired,priority,targetDifficulty}` | 204 |
| GET `/api/job-positions/{positionId}/question-bank` | ندارد | 200، BankQuestion[]، حداکثر ۱۰۰؛ بدون pagination |
| POST `/api/job-positions/{positionId}/question-bank` | BankQuestionInput | 200، BankQuestion |
| PUT `/api/job-positions/{positionId}/question-bank/{questionId}` | `{question: BankQuestionInput,rowVersion}` | 200، BankQuestion |
| POST `/api/job-positions/{positionId}/applications` | `{firstName,lastName,email,phone}` | 200، `{id}` |
| POST `/api/job-positions/{positionId}/applications/{applicationId}/interviews` | `{templateId,maxQuestionCount,expiresInHours}` | 200، Invitation |
| GET `/api/interviews/{interviewId}` | ندارد | 200، HrInterview |
| POST `/api/interviews/{interviewId}/revoke` | بدون body | 204 |
| GET `/api/interviews/{interviewId}/answers/{answerId}/audio` | ندارد | فایل صوتی |
| GET `/api/interviews/{interviewId}/questions/{questionId}/audio` | ندارد | فایل صوتی |

محدودیت ورودی: name/نام/نام خانوادگی ۱..۲۰۰ کاراکتر، email معتبر حداکثر۲۵۶، phone اختیاری حداکثر۳۰؛ priority=0..100، targetDifficulty=1..5، متن سؤال۱..۲۰۰۰، topic=1..200، difficulty=1..5. maxQuestionCount=1..30 (پیش‌فرض۱۲)، expiresInHours=1..168 (پیش‌فرض۷۲).

```ts
interface BankQuestionInput {
  skillId: string; text: string; topic: string;
  difficulty: number; isActive: boolean;
}
interface BankQuestion extends BankQuestionInput {
  id: string; rowVersion: string; // Base64؛ عین مقدار آخرین پاسخ ارسال شود
}
interface Invitation {
  interviewId: string; token: string;
  expiresAtUtc: string; candidateRoute: string;
}
```

برای غیرفعال کردن سؤال isActive=false بفرستید؛ DELETE نداریم. در تعارض rowVersion بانک را دوباره بخوانید و پیش‌نویس کاربر را برای بازبینی نگه دارید.

ترتیب فرم ساده: انتخاب موقعیت/قالب موجود → ساخت یا استفاده از skillId شناخته‌شده → اتصال مهارت به موقعیت → بانک سؤال → ساخت application → ساخت interview → نمایش شناسه و توکن برای کپی. برای مهارت required سؤال فعال لازم است؛ طرح ناقص 400 با interview_plan_incomplete می‌دهد. قالب قدیمی همچنان سؤال‌های معتبر خودش را لازم دارد؛ قالب خالی نسازید. بانک موقعیت منبع سؤال‌های مصاحبه AI است؛ snapshot هنگام دعوت ثابت می‌شود و ویرایش بعدی بانک روی دعوت قبلی اثر ندارد.

توکن فقط در پاسخ ساخت دعوت برمی‌گردد؛ بازیابی دوباره یا ارسال خودکار ایمیل/SMS نداریم. candidateRoute مثل `/api/candidate/interviews/{id}` **مسیر API** است، نه لینک صفحه فرانت. لینک صفحه خودتان را بدون توکن بسازید و شناسه/توکن را جدا برای تحویل نشان دهید. آن را در لاگ ذخیره نکنید.

### گزارش HR

```ts
interface HrInterview {
  id: string; candidateApplicationId: string; jobPositionId: string;
  status: InterviewStatus; completionReason: string | null;
  startedAtUtc: string | null; completedAtUtc: string | null;
  questions: {
    id: string; skillId: string; text: string; topic: string; difficulty: number;
  }[];
  answers: {
    id: string; questionId: string; status: AnswerStatus;
    transcript: string | null; selectionSource: string | null;
    failureStage: string | null; failureCode: string | null; audioUrl: string;
  }[];
  skillStates: {
    skillId: string; currentScore: number; evidenceCount: number; confidence: number;
  }[];
  evidence: {
    answerId: string; questionId: string; skillId: string;
    scoreBefore: number; scoreAfter: number; delta: number;
    confidence: number; evidence: string; createdAtUtc: string;
  }[];
  coveredTopics: {
    skillId: string; topic: string; coverage: number; evidenceCount: number;
  }[];
}
```

سؤال و پاسخ را با questionId متصل کنید. متن، transcript و دکمه شنیدن پاسخ کافی است؛ جدول ساده مهارت/evidence اضافه کنید. score و confidence در بازه۰..۱، delta در بازه منفی۱..۱ هستند؛ نمایش درصد اختیاری است. evidenceCount=0 را «شواهد کافی نیست» نشان دهید، نه رد داوطلب. selectionSource ممکن است AI یا Fallback باشد. نام مهارت و مشخصات داوطلب در این گزارش نیست؛ فقط از اطلاعات شناخته‌شده UI استفاده کنید و در نبود آن برچسب شناسه نشان دهید.

## خطاها و APIهای موجودنبودن

خطاهای معمول ProblemDetails هستند: `{status,title,code,traceId,errors?}`؛ errors در validation ممکن است باشد. برای پاسخ غیر JSON هم پیام عمومی مناسب داشته باشید.

| HTTP | رفتار |
|---|---|
| 400 | نمایش خطای ورودی؛ wrong_question نیاز به دریافت current دارد |
| 401 داوطلب | توکن نامعتبر/منقضی/لغوشده؛ توقف polling و درخواست ورود دوباره |
| 403 HR | پیام دسترسی ناکافی |
| 404 | جلسه/رکورد در دسترس نیست؛ برای صوت فقط متن را حفظ کنید |
| 409 | تعارض/پردازش جاری/وضعیت نامعتبر؛ current یا بانک را دوباره بخوانید، POST خودکار تکرار نشود |
| 410 | انقضای جلسه؛ عملیات متوقف شود |
| 413 | فایل بیش از حد مجاز |
| 429 | درخواست بیش از حد؛ مکث در polling و امکان تلاش بعدی |
| 500 | پیام عمومی و traceId قابل ارائه به پشتیبانی |

انقضای توکن معمولاً 401 است؛ انقضای زمان جلسه می‌تواند 409/410 با interview_expired باشد. وابسته به یک code خاص برای همه خطاهای احراز هویت نباشید.

**فعلاً نداریم:** فهرست مهارت‌ها، خواندن مهارت‌های متصل به موقعیت، فهرست/جزئیات applicationها، فهرست مصاحبه‌ها/دعوت‌ها، بازیابی توکن، ارسال دعوت، transcript/امتیاز داوطلب یا تولید گروهی TTS. endpoint حدسی نسازید. صفحه گزارش با interviewId معلوم باز شود. فرم ایجاد از IDهای پاسخ همان جریان استفاده کند؛ localStorage جایگزین دیتابیس فهرست‌ها نیست. اگر مدیریت کامل و قابل بازیابی پس از reload لازم شد، نیاز دقیق API را به بک‌اند گزارش کنید؛ این مرحله را به ATS کامل گسترش ندهید.

## معیار پذیرش

- ورود داوطلب بدون حساب HR و بدون تداخل interceptorها کار کند.
- refresh از current بازیابی شود؛ دوباره سؤال/پاسخ ایجاد نشود.
- حالت‌های Created، ضبط، ارسال، انتظار، retry، پایان و توکن نامعتبر قابل استفاده باشند.
- HTTP 200 با answer Failed دکمه retry همان answer را نشان دهد.
- ارسال دوباره، complete یا ضبط هم‌زمان با پردازش رخ ندهد.
- سؤال بدون صوت قابل پاسخ باشد؛ صوت خصوصی با Authorization پخش شود.
- فرمت، اندازه، زمان و مجوز میکروفون مدیریت و منابع در خروج آزاد شوند.
- گزارش HR سؤال/صوت/transcript/evidence را نشان دهد، بدون نتیجه استخدام خودکار.
- تست‌های مرتبط و build فرانت واقعاً اجرا شوند؛ mock از اتصال واقعی بک‌اند جدا گزارش شود.

## پرامپت آماده برای Codex فرانت‌اند

```text
در ریپوی موجود فرانت‌اند HireYar بخش مصاحبه صوتی هوشمند را طبق فایل
FRONTEND_INTERVIEWS_HANDOFF.md پیاده‌سازی کن. اول دستورهای پروژه، routing،
auth/interceptor، کلاینت API و کامپوننت‌های موجود را بخوان و تغییرهای کاربر را حفظ کن.
پروژه جدید، معماری جدید، state manager یا وابستگی اضافی ایجاد نکن مگر واقعاً لازم باشد.

صفحه عمومی داوطلب با token مستقل، ضبط/ارسال، صوت خصوصی، current/retry و پایان؛
گزارش HR؛ و فرم ساده بانک سؤال و ایجاد دعوت را با امکانات موجود بساز.
قرارداد همین فایل مرجع است: مسیر یا envelope حدسی نساز، توکن را در URL نگذار،
HTTP 200 را بدون بررسی state موفقیت پردازش حساب نکن و POST را خودکار retry نکن.
APIهای فهرست موجودنیستند؛ محدوده را گسترش نده و نیاز احتمالی را دقیق گزارش کن.
امتیاز/transcript را به داوطلب و تصمیم استخدام خودکار را به هیچ‌کس نشان نده.

کمترین تغییر قابل نگهداری را انجام بده. build و تست مرتبط را اجرا کن و در پایان
صفحه‌ها، تنظیم base URL، روش تست و محدودیت‌های باقی‌مانده را کوتاه گزارش کن.
push/deploy انجام نده.
```
