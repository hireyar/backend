# سوابق مصرف مصاحبه — ADR-075 / 2026-10-20

قرارداد مکمل [اشتراک تعدادی](COUNT_BASED_SUBSCRIPTIONS_API.md). اعتبار این مدل «تعداد مصاحبه» است؛ هزینه دقیقه‌ای/ریالی محاسبه نمی‌شود. ایجاد دعوت فقط یک واحد رزرو و شروع یک واحد مصرف می‌کند. لغو/انقضای شروع‌نشده مصرف صفر دارد؛ پس از شروع، لغو اعتبار مصرف‌شده را برنمی‌گرداند. دعوت تاریخیِ بدون رزرو جدید، خودکار بدهکار نمی‌شود.

## دسترسی و فهرست

فقط JWT پنل با نقش Owner؛ Recruiter برابر403 و کارجو/anonymous برابر401. سازمان از هویت سرور، no-store، بدون نیاز به اشتراک فعال برای خواندن تاریخچه. GETها هیچ write یا تماس AI ندارند.

`GET /api/subscriptions/usage?page=1&pageSize=20&status=Expired&fromUtc=2026-10-01T00:00:00Z&toUtc=2026-11-01T00:00:00Z`

- `page` حداقل1؛ `pageSize` از1تا100، پیش‌فرض20.
- `status` اختیاری: Created، InProgress، Completed، Cancelled، Expired، Replaced، Revoked، Deleted. مقدار ناشناخته400.
- بازه اختیاری بر **زمان ایجاد دعوت** است: `fromUtc` شامل و `toUtc` غیرشامل، فقط UTC با Z/offset صفر. بازه معکوس400/invalid_usage_date_range.
- مرتب‌سازی زمان ایجاد نزولی، سپس شناسه نزولی؛ total پس از فیلتر و پیش از صفحه‌بندی است.
- هیچ فیلتر organizationId یا نقش از کلاینت پذیرفته نمی‌شود.

پاسخ `Page<InterviewUsageDto>`:

```json
{
  "items": [{
    "interviewId": "11111111-1111-1111-1111-111111111111",
    "candidateName": "کارجوی آزمایشی",
    "positionTitle": "عنوان موقعیت",
    "templateTitle": "قالب عمومی",
    "interviewType": "general",
    "status": "Completed",
    "createdAtUtc": "2026-10-20T08:00:00Z",
    "expiresAtUtc": "2026-10-23T08:00:00Z",
    "configuredDurationSeconds": 1800,
    "startedAtUtc": "2026-10-20T09:00:00Z",
    "endedAtUtc": "2026-10-20T09:12:00Z",
    "actualDurationSeconds": 720,
    "creditsConsumed": 1,
    "isReserved": false,
    "replacementInterviewId": null
  }],
  "total": 1,
  "pageNumber": 1,
  "pageSize": 20
}
```

configuredDurationSeconds همان مدت تنظیم‌شده/سقف مصاحبه است؛ این مدل زمان قرار ملاقات مجزایی ندارد. startedAtUtc شروع واقعی، endedAtUtc پایان/لغو ثبت‌شده و actualDurationSeconds فاصله آن دو با دقت ثانیه است؛ مدت صوت یا مبنای حسابداری نیست. پیش از شروع/پایان واقعی، فیلدهای ناموجود null هستند؛ پایان را از روی مهلت دعوت جعل نکنید. cancelled بدون شروع می‌تواند endedAtUtc داشته باشد اما actualDurationSeconds=null است.

isReserved فقط رزرو زنده دعوت Created را نشان می‌دهد. creditsConsumed از رزرو Consumed قطعی و برابر0یا1 است. قیمت فعلی بسته یا زمان مصاحبه آن را تغییر نمی‌دهد. نام کارجو از رکورد همان سازمان و عنوان‌ها از snapshot جلسه خوانده می‌شوند.

مصاحبه soft-delete شده نیز در سوابق باقی می‌ماند. اولویت وضعیت نمایشی: Deleted، Replaced، Cancelled، Completed، Revoked، Expired، InProgress، Created. وضعیت صفحه لزوماً آخرین نوع رویداد نیست؛ جزئیات تمام رویدادهای ثبت‌شده را نگه می‌دارد.

## جزئیات و رویدادها

`GET /api/subscriptions/usage/{interviewId}`

پاسخ `{ "interview": InterviewUsageDto, "events": InterviewUsageEventDto[] }`. نبود رکورد یا رکورد سازمان دیگر404. event دارای id، eventType، occurredAtUtc، creditsConsumed و isHistorical است. ترتیب زمان صعودی، سپس نوع رویداد و شناسه؛ رویدادهای هم‌زمان ترتیب علّی جداگانه‌ای تضمین نمی‌کنند.

| eventType | مفهوم | creditsConsumed |
|---|---|---:|
| Created | ایجاد دعوت | 0 |
| Reserved | رزرو یک سهمیه | 0 |
| Started | شروع واقعی مصاحبه | 0 |
| CreditConsumed | کسر قطعی یک سهمیه در شروع | 1 |
| Completed | پایان مصاحبه | 0 |
| Cancelled | لغو مصاحبه | 0 |
| Expired | رسیدن به مهلت دسترسی | 0 |
| Replaced | جایگزینی با دعوت جدید | 0 |
| AccessRevoked | ابطال مستقل دسترسی | 0 |
| Deleted | حذف از فهرست عملیاتی | 0 |
| Released | آزادشدن رزرو شروع‌نشده | 0 |

مقادیر رویداد تغییر مصرف در همان اتفاق‌اند؛ برای ستون مصرف کل از interview.creditsConsumed استفاده کنید. Started و CreditConsumed دو واحد مصرف نیستند. Released بازپرداخت مصرف قبلی نیست. تغییر وضعیت مرحله/پاسخ‌های مصاحبه رویداد مالی مستقل ایجاد نمی‌کند.

## ماندگاری و migration

`20261020093013_AddInterviewUsageHistory` جدول InterviewUsageEvents، FK ترکیبی سازمان/جلسه، indexهای صفحه‌بندی/یکتایی و triggerهای append-only را می‌سازد. ثبت تغییرهای lifecycle و رزرو/مصرف در همان تراکنش PostgreSQL انجام می‌شود؛ retry رویداد تکراری نمی‌سازد. حذف/ویرایش تاریخچه و رابطه بین سازمان‌ها در DB رد می‌شود.

worker داخل همین برنامه هر30ثانیه حداکثر200 دعوت منقضی‌شده را با قفل ردیف و SKIP LOCKED ثبت می‌کند؛ زمان رویداد خود expiresAtUtc است. در backlog بزرگ چند نوبت لازم است. وضعیت فهرست و آزادبودن منطقی سهمیه بلافاصله از زمان فعلی مشخص‌اند؛ timeline ممکن است تا اجرای worker تأخیر داشته باشد. لغو/جایگزینی بعد از موعد هم Expired را در همان تراکنش حفظ می‌کند. worker مصرف/وضعیت مصاحبه/رزرو قبلی را بازنویسی نمی‌کند.

برای داده موجود فقط حقایق قابل‌بازیابی (زمان‌های ذخیره‌شده و وضعیت رزرو) با isHistorical=true بازسازی می‌شوند؛ رخداد ازبین‌رفته یا مصرف حدسی ساخته نمی‌شود. رویدادهای بعد از migration این علامت را ندارند. Down در حضور هر سابقه رد می‌شود تا تاریخچه حذف نشود.

قبل از اجرای نسخه API در مقصد، با ConnectionStrings__Default محیط مقصد:

```powershell
dotnet tool restore
dotnet ef database update --project src/HireYar.Infrastructure --startup-project src/HireYar.Api
```

این تسک migration مقصد، push یا deploy انجام نمی‌دهد. فرانت در ریپوی جداست؛ [پرامپت فرانت](INTERVIEW_USAGE_HISTORY_FRONTEND_PROMPT.md).

## اعتبارسنجی محلی

Build نهایی صفر warning/error، مدل EF بدون drift و آخرین نتیجه هر۱۴۱آزمون مرتبط Passed با صفر Skip است. اجرای گسترده139Pass/2Fail داشت؛ بازاجرای۵تست شامل هر دو مورد اصلاح‌شده و هر سه تست جدید با0Fail موفق شد. اصلاح‌ها انتخاب FFmpeg مخصوص پروژه برای تست صوت و به‌روزرسانی انتظار محافظ rollback در تست قدیمی بودند. PostgreSQL18 واقعی، providerهای مصنوعی؛ backfill و اجرای دوباره SQL idempotent، مصرف/زمان/لغو/انقضا، حفظ حذف/جایگزینی، scope و immutability واقعاً آزموده شدند. کل suite اجرا نشده؛ جزئیات و TRXها در [HANDOFF](HANDOFF.md).
