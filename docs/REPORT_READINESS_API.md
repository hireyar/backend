> تکمیل جامع 2026-10-15: تمام بخش‌های بک‌اند اکنون پیاده شده‌اند و هیچ بخش جاری NotImplemented نیست. [COMPREHENSIVE_REPORT_API](COMPREHENSIVE_REPORT_API.md) مرجع نهایی قرارداد، فرمول‌ها، policy و تصمیم نسخه‌دار است؛ محدودیت‌های مرحله‌ایِ پایین تاریخی‌اند.

# وضعیت واقعی آمادگی گزارش و قرارداد retry — 2026-10-15

علت گزارش فرانت در کد تأیید شد: بخش‌های score، positionFit، summary، strengthsAndImprovements، specializedProfile/generalProfile مرتبط و humanReview به‌صورت ثابت NotStarted برمی‌گشتند. پردازش/worker مربوط به این بخش‌ها هنوز پیاده نشده است؛ انتظار یا درخواست مجدد آن‌ها را آماده نمی‌کند. بررسی حاضر به کد و آزمون محلی محدود بود و گیرکردن یک job مشخص در سرور مقصد را اثبات نمی‌کند.

این اصلاح، همان بخش‌های جامع را پیاده‌سازی نمی‌کند؛ حالت پیاده‌نشده را از کار واقعاً در انتظار جدا می‌کند. قابلیت مشترک تصمیم انسانی در APIهای review/decisions قبلی حفظ است؛ بخش جدید humanReview در سند گزارش هنوز متصل نشده. ارزیابی مستقل سؤال‌ها همچنان از [ANSWER_ASSESSMENT_API](ANSWER_ASSESSMENT_API.md) پیروی می‌کند.

## تغییر قرارداد برای فرانت

- sections[].state برای بخش‌های ساخته‌نشده اکنون NotImplemented است؛ پیام صریح «در نسخه فعلی پیاده‌سازی نشده و در صف پردازش نیست» و shouldPoll=false دارد. فرانت کارت این بخش را با همین وضعیت نمایش دهد؛ spinner یا شمارش معکوس نگذارد.
- پروفایل نامرتبط همچنان NotApplicable است. بخش‌های واقعی مثل شناسنامه و آمار Available هستند. NotStarted ثابت از پاسخ جاری حذف شده است.
- overallScore، summary، strengths و improvements نیز state=NotImplemented و مقادیر null/لیست خالی دارند؛ جای آن‌ها صفر یا نتیجه ساختگی نمایش داده نشود.
- questions[].assessment.isPending فقط برای Queued/Processing/RecoveryPending درست است؛ این یعنی کار ناتمام وجود دارد، نه اثبات سلامت یا فعال بودن worker.
- processing در ریشه، مرجع تصمیم polling خودکار است. sections.questions.shouldPoll نیز همان تصمیم را دارد؛ سایر بخش‌های این نسخه shouldPoll=false دارند. schemaVersion همچنان session-report/1 است؛ فیلدها افزایشی و NotImplemented یک مقدار وضعیت جدید است که فرانت باید پشتیبانی کند.

نمونه در صورت وجود یک ارزیابی واقعی در انتظار:

```json
{
  "state": "BaseReady",
  "processing": {
    "state": "Pending",
    "shouldPoll": true,
    "pollAfterSeconds": 3,
    "pendingAssessments": 1,
    "completedAssessments": 0,
    "failedAssessments": 0,
    "unavailableAssessments": 0,
    "hasUnimplementedSections": true,
    "reasonCode": null,
    "message": "ارزیابی بعضی پاسخ‌ها هنوز پایان نیافته است."
  }
}
```

| processing.state | رفتار |
|---|---|
| Pending | ارزیابی Queued/Processing/RecoveryPending هست و WorkerEnabled روشن است؛ فقط همان پاسخ‌ها در انتظارند. GET بعد از pollAfterSeconds، حداقل۳ثانیه و متناسب با PollSeconds، تکرار شود. |
| Paused | ارزیابی ناتمام هست ولی WorkerEnabled در تنظیمات خاموش است؛ shouldPoll=false، pollAfterSeconds=null و reasonCode=assessment_worker_disabled. پیام «ارزیابی خودکار فعلاً متوقف است» نمایش داده شود. انتظار فرانت تنظیمات سرور را تغییر نمی‌دهد. |
| Idle | کار ارزیابی در انتظار نیست؛ shouldPoll=false و pollAfterSeconds=null. Failed، Unavailable و InsufficientEvidence پردازش فعال نیستند. |

Idle به معنی تکمیل گزارش جامع یا موفقیت همه ارزیابی‌ها نیست. hasUnimplementedSections و وضعیت تک‌تک بخش‌ها/پاسخ‌ها مستقل‌اند. completedAssessments تعداد Succeeded و InsufficientEvidence است؛ failed/unavailable جدا شمرده می‌شوند. NotReady مربوط به جلسه تکمیل‌نشده است و job ارزیابی فعال محسوب نمی‌شود. UI مصاحبه در حال انجام می‌تواند وضعیت خود مصاحبه را جدا پیگیری کند.

برای سازگاری موقت با پاسخ قبل از این اصلاح، اگر processing موجود نبود فقط Queued/Processing/RecoveryPending سؤال‌ها مبنای انتظار باشد؛ NotStarted بخش‌های جامع به معنی پردازش فعال نیست. پس از دریافت shouldPoll=false، timer polling لغو شود؛ null بودن pollAfterSeconds را به تایمر پیش‌فرض تبدیل نکنید. نمایش کل صفحه به کامل شدن تحلیل‌ها وابسته نباشد.

این metadata براساس وضعیت ماندگار پاسخ‌ها و تنظیمات فعلی محاسبه می‌شود؛ heartbeat یا تضمین زنده بودن worker نیست. اگر روی مقصد، processing.state=Pending پایدار بود، migrationهای مرحله قبل، فعال بودن host/worker و لاگ‌های امن assessment بررسی شوند. در این تغییر به دیتابیس مقصد یا داده مصاحبه واقعی وصل نشده‌ایم.

## Swagger و پاسخ واقعی retry

مسیر قبلی حفظ شده است:

`POST /api/interviews/{id}/report/answers/{answerId}/assessment/retry`

پاسخ موفق واقعی همچنان 202 Accepted و Location مسیر GET گزارش است. Action اکنون ActionResult<RetryAnswerAssessmentResponse> دارد و ProducesResponseType به‌صراحت 202 را مستند می‌کند؛ Swagger نباید 200 موفق پیش‌فرض یا بدنه بدون مدل بسازد.

DTO پاسخ:

```text
RetryAnswerAssessmentResponse
  interviewId: Guid
  answerId: Guid
  assessment: ReportAnswerAssessment
```

بدنه همان سه فیلد قبلی را دارد و assessment شامل state/value/confidence/evidence/attempts/canRetry/failureCode و بقیه metadata قرارداد مرحله دوم، به‌اضافه isPending است. تکرار درخواست همچنان idempotent و مخصوص Owner، با همان سقف سه تلاش و scope سازمان است. این تغییر محدودیت‌ها یا مسیر را تغییر نمی‌دهد و POST تولید همزمان مدل انجام نمی‌دهد.

## محدوده انتشار

این اصلاح فقط DTO، خواندن/چاپ وضعیت و metadata OpenAPI را تغییر می‌دهد؛ schema و migration تازه ندارد. migrationهای مرحله‌های قبلی همچنان لازم‌اند. Angular در ریپوی جدا باید NotImplemented، processing.shouldPoll و Paused را مصرف کند؛ تغییر بک‌اند به‌تنهایی کد spinner فرانت را بازنویسی نمی‌کند. امتیاز کل، تناسب، پروفایل‌ها، جمع‌بندی/قوت/بهبود و اتصال بررسی انسانی در گزارش جدید همچنان کار پیاده‌سازی مراحل بعدند. push/deploy یا تغییر دیتابیس مقصد در این درخواست انجام نشده است.
