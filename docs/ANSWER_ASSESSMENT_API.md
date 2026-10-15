> تکمیل جامع 2026-10-15: تمام بخش‌های بک‌اند اکنون پیاده شده‌اند و هیچ بخش جاری NotImplemented نیست. [COMPREHENSIVE_REPORT_API](COMPREHENSIVE_REPORT_API.md) مرجع نهایی قرارداد، فرمول‌ها، policy و تصمیم نسخه‌دار است؛ محدودیت‌های مرحله‌ایِ پایین تاریخی‌اند.

> اصلاح قرارداد آمادگی 2026-10-15: بخش پیاده‌نشده NotImplemented است و در صف نیست؛ processing.shouldPoll مرجع polling و Swagger retry دارای مدل پاسخ صریح202 شد. [REPORT_READINESS_API](REPORT_READINESS_API.md) بر توضیح‌های تاریخی NotStarted پایین اولویت دارد.

# ارزیابی مستقل پاسخ‌ها — مرحله دوم گزارش

وضعیت 2026-10-15: این مرحله روی گزارش پایه مستقل ساخته شده است. موتور گزارش قبلی، پرامپت/validator/repair و جدول حذف‌شده بازگردانده نشده‌اند. امتیاز کل، تناسب، پروفایل‌ها و جمع‌بندی همچنان مرحله‌های بعدی‌اند. مرجع گزارش پایه [SESSION_REPORT_API](SESSION_REPORT_API.md) است.

## رفتار اصلی

بعد از Completed شدن جلسه، worker پاسخ‌های نهایی دارای transcript غیرخالی را از PostgreSQL کشف می‌کند؛ باز کردن صفحه یا POST لازم نیست. در GET هیچ job ساخته، نتیجه تولید یا به AI درخواست فرستاده نمی‌شود. هر پاسخ یک رکورد مستقل در AnswerAssessments دارد؛ نتیجه موفق نهایی حفظ می‌شود. ارزیابی لحظه‌ای InterviewAnswer.Correctness برای گزارش جدید استفاده نمی‌شود و این مرحله روی داده‌های پاسخ، مسیر زنده یا تسویه مالی write ندارد.

worker در هر poll حداکثر سه پاسخ را با سه scope مستقل پردازش می‌کند. نتیجه/خطای یک پاسخ روی دیگری اثر ندارد. داده پایه و همه بخش‌های گزارش بلافاصله قابل خواندن می‌مانند؛ BaseReady همچنان به معنی آماده بودن پایه است، نه تکمیل تحلیل‌ها.

## قرارداد JSON و فرانت

مسیر خواندن و schemaVersion قبلی جدید، یعنی GET /api/interviews/{id}/report و session-report/1، حفظ شده‌اند. questions[].assessment از حالت placeholder به اطلاعات زیر گسترش یافته است:

```json
{
  "state": "Succeeded",
  "value": 80,
  "confidence": 0.8,
  "formula": "answer-quality/100/v1",
  "explanation": "توضیح ارزیابی بر اساس پاسخ",
  "evidence": [{"answerId":"00000000-0000-0000-0000-000000000001","start":0,"length":12,"quote":"نمونه ساختگی"}],
  "attempts": 1,
  "canRetry": false,
  "failureCode": null,
  "requestedAtUtc": "2026-10-15T08:00:00Z",
  "startedAtUtc": "2026-10-15T08:00:02Z",
  "finishedAtUtc": "2026-10-15T08:00:09Z",
  "nextAttemptAtUtc": null,
  "evaluatorVersion": "answer-quality/1",
  "provider": "provider identifier",
  "model": "configured model identifier"
}
```

مقادیر نمونه بالا صرفاً نمایش شکل قراردادند. score در بازه ۰ تا ۱۰۰، کیفیت/درستی همان پاسخ است و رتبه کارجو یا امتیاز کل نیست. confidence در بازه ۰ تا ۱ برآورد مدل است و احتمال آماری کالیبره‌شده محسوب نمی‌شود. formula در این مرحله نام مقیاس نمره است؛ محاسبه تجمیعی وجود ندارد. شناسه، offset و متن نقل‌قول را سرور از همان پاسخ می‌سازد؛ start/length بر حسب UTF-16 مانند index رشته C#/JavaScript هستند.

| state | معنای نمایشی |
|---|---|
| NotApplicable | سؤال پاسخ نهایی ندارد؛ ارزیابی آن موضوعیت ندارد |
| NotReady | جلسه هنوز Completed نشده |
| Unavailable | transcript موجود نیست، یا شواهد ذخیره‌شده قابل اتکا نیست |
| Queued | منتظر worker یا زمان تلاش بعدی؛ ممکن است هنوز رکوردی ایجاد نشده باشد |
| Processing | فقط ارزیابی همین پاسخ در حال اجراست |
| RecoveryPending | مهلت پردازش قبلی منقضی شده و worker باید آن را بازیابی کند |
| Succeeded | نمره، توضیح و شواهد معتبر از نظر ساختاری و تعلق به پاسخ ذخیره شده‌اند |
| InsufficientEvidence | مدل صریحاً شواهد کافی برای ارزیابی ندارد؛ مقدار null است |
| Failed | تلاش ناموفق؛ canRetry تعیین می‌کند بودجه دستی باقی مانده یا نه |

فرانت برای Queued/Processing/RecoveryPending با فاصله معقول گزارش را مجدد بخواند، فقط همان پاسخ را در حال پردازش نشان دهد و کل صفحه را پشت spinner نبرد. NotReady با پایان جلسه قابل تغییر است. InsufficientEvidence را صفر یا خطای فنی نمایش ندهد. پیام انسانی را بر اساس failureCode ثابت بسازد؛ provider/model اطلاعات فنی اختیاری‌اند. نمایش ویدیو و متن/شواهد به پایان همه ارزیابی‌ها وابسته نیست. نسخه چاپی جدید وضعیت، نمره، توضیح و شاهد هر پاسخ را نیز با escape متن نشان می‌دهد.

## retry دستی

`POST /api/interviews/{id}/report/answers/{answerId}/assessment/retry`

فقط Owner همان سازمان؛ همان feature snapshot گزارش لازم است. موفقیت 202 و Location مسیر GET گزارش، با body شامل interviewId، answerId و assessment فعلی است. تماس AI در این POST انجام نمی‌شود. درخواست تکراری برای Queued/Processing، همان کار موجود را نگه می‌دارد و بودجه اضافه نمی‌کند. تلاش موفق یا InsufficientEvidence بازتولید نمی‌شود. سقف مادام‌العمر هر پاسخ در این نسخه سه تلاش است؛ POST سقف را reset نمی‌کند.

- 401 احراز هویت نشده، 403 Recruiter یا قرارداد بدون قابلیت report؛ 404 جلسه/پاسخ خارج سازمان، حذف‌شده یا پاسخ متعلق به جلسه دیگر.
- 409 assessment_not_ready: جلسه کامل نشده، پاسخ تعامل است یا transcript ندارد.
- 409 assessment_retry_unavailable: نتیجه نهایی یا بودجه مصرف شده است.
- مسیرهای evaluation-report قدیمی همچنان scoped410 هستند و در تولید جدید نقشی ندارند.

## بازیابی و سقف‌ها

- سقف سه تلاش برای هر پاسخ؛ مدل و پرامپت مستقل یک پاسخ، بدون تولید یکپارچه یا repair گزارش.
- پیش‌فرض deadline سرویس ۷۵ ثانیه، timeout HTTP ارزیاب ۶۰ ثانیه و lease برابر deadline+۱۵ ثانیه است. حتی provider نادیده‌گیرنده cancellation با WaitAsync محدود می‌شود؛ خطای task دیررس observe و نتیجه‌اش ذخیره نمی‌شود.
- خطای موقت شبکه/timeout/HTTP408/429/5xx پس از تلاش اول ۵ ثانیه و پس از دوم ۳۰ ثانیه فاصله دارد؛ تلاش سوم Failed نهایی است. سایر خروجی‌های نامعتبر و خطاهای دائمی Failed می‌شوند و retry دستی فقط در بودجه باقی‌مانده مجاز است.
- توقف فرایند یا لغو host ممکن است Processing ماندگار بگذارد؛ GET پس از پایان lease وضعیت RecoveryPending یا Failed پس از تلاش سوم را محاسبه می‌کند و در Processing منقضی نمی‌ماند. worker پس از restart همان رکورد را دوباره claim می‌کند؛ حافظه فرایند منبع حقیقت نیست.
- قفل کوتاه سازمان برای claim/commit و retry استفاده می‌شود؛ تماس مدل بیرون قفل است. AttemptId و lease در commit دوباره کنترل می‌شوند؛ تلاش قبلی حق بازنویسی تلاش جدید را ندارد.
- hash ورودی شامل متن سؤال، زمینه نقش، تکه‌های همان پاسخ و نسخه ارزیاب در claim ثبت و قبل commit دوباره مقایسه می‌شود. تغییر ورودی هنگام پردازش نتیجه را رد می‌کند.
- خاموش کردن WorkerEnabled عمداً پردازش را متوقف می‌کند؛ این حالت Queued را خودکار تکمیل نمی‌کند. دسترسی به DB و اجرای worker شرط پیشرفت است؛ API در قطعی زیرساخت نمره یا موفقیت ساختگی نمی‌سازد.

کدهای عمومی: assessment_timeout، assessment_provider_unavailable، assessment_provider_busy، assessment_not_configured، assessment_provider_rejected، assessment_invalid_output، assessment_internal_error، assessment_input_changed، assessment_lease_expired، assessment_transcript_unavailable و assessment_storage_invalid. کدهای خام provider یا exception message به پاسخ و لاگ منتقل نمی‌شوند. لاگ worker فقط شناسه/نوع exception، و لاگ ارزیابی فقط شناسه پاسخ، کد ثابت و تعداد تلاش دارد؛ raw output/prompt/متن پاسخ ثبت نمی‌شود.

## شواهد و محدودیت کیفیت

سرور متن واقعی همان پاسخ را به قطعات شماره‌دار حداکثر ۳۲۰ کاراکتری تقسیم می‌کند، با حفظ تمام متن و offset. مدل فقط شماره ۱ تا ۵ قطعه موجود را انتخاب می‌کند. نمره/اطمینان خارج بازه، شماره ناشناخته/تکراری، فیلد لازم مفقود یا نوع نادرست فقط همان ارزیابی را Failed می‌کنند. فیلد اضافی quote/answerId مدل استفاده نمی‌شود. InsufficientEvidence باید نمره/اطمینان null و شاهد خالی داشته باشد. مدل فقط سؤال/موضوع/سطح، نوع مصاحبه، عنوان نقش و قطعات همان پاسخ را دریافت می‌کند؛ رزومه، متن سایر پاسخ‌ها یا تصمیم زنده به عنوان شاهد وارد نمی‌شود.

هنگام خواندن نیز تعلق شناسه و تطابق offset/متن شاهد با پاسخ دوباره کنترل می‌شود؛ خرابی شاهدِ ذخیره‌شده فقط همان assessment را Unavailable می‌کند و مقدار آن null می‌شود. صحت نقل‌قول و تعلق داده توسط سرور enforce می‌شود؛ استنتاج معنایی و کیفیت نمره نیازمند ارزیابی مدل واقعی و بررسی انسانی است. آزمون‌ها مصنوعی‌اند و تأیید کیفیت واقعی مدل ادعا نمی‌شود.

## تنظیمات، migration و انتشار

- `AnswerAssessments:WorkerEnabled=true`, `PollSeconds=2`, `AttemptTimeoutSeconds=75`؛ محدوده PollSeconds برابر۱..۶۰ و deadline برابر۱..۱۲۰ با اعتبارسنجی startup.
- در provider منتخب: `AI:Liara:AnswerAssessmentModel` پیش‌فرض openai/gpt-5.6-luna، یا `AI:NetArz:AnswerAssessmentModel` پیش‌فرض gpt-5.6-luna و `AnswerAssessmentTimeoutSeconds=60`، محدوده۱..۱۲۰. انتخاب AI:Provider/credential/BaseUrl همان provider مصاحبه است؛ مدل ارزیاب مستقل از LlmModel است. EvaluationModel قدیمی بازنگشته است.
- migration `20261015083205_AddAnswerReportAssessments` فقط جدول AnswerAssessments، indexها و FK/checkهای آن را اضافه می‌کند. FK مرکب هم سازمان/جلسه و هم تعلق پاسخ به جلسه را enforce می‌کند. پاسخ/مالی قدیمی تغییر داده نمی‌شوند. Down فقط همین نتایج جدید را حذف می‌کند.
- پیش از اجرای API جدید migration لازم است. migration قبلی RemoveLegacyEvaluationReports نیز در زنجیره لازم است و توقف نسخه پیش از بازسازی را طبق سند پایه می‌خواهد. اگر مرحله اول از قبل اعمال شده، این migration صرفاً افزایشی است.
- Angular در ریپوی جداست: خواندن assessment جدید، polling محدود هر پاسخ، retry فقط Owner و کدهای بالا باید متصل شوند. push/deploy، تغییر دیتابیس مقصد یا تماس مدل واقعی این مرحله انجام نشده‌اند.
