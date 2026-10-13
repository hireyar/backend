# چرخه تولید گزارش — 2026-10-13

این قرارداد جایگزین رفتار تاریخی POST همزمان، GET404 برای گزارش آماده‌نشده و regenerate گزارش موفق در INTERVIEW_EVALUATION_API و INTERVIEW_REPORT_V3_API است. schema report-v3، شواهد، نمرات و چاپ محفوظ‌اند؛ Angular جداست.

## شروع و مراحل

پایان خودکار و دستی، برای قرارداد snapshot دارای interview_report، یک Queued در همان تراکنش completion/تسویه ثبت می‌کند. درخواست پایان تماس مدل ندارد. worker مستقل از recovery پاسخ، هر دو ثانیه صف PostgreSQL را بررسی و حداکثر چهار گزارش را همزمان پردازش می‌کند؛ صف شلوغ منتظر ظرفیت است و restart صف را از بین نمی‌برد.

Queued → Generating → Validating → Saving → Succeeded؛ هر شکست → Failed. یک تماس مدل مستقل برای تمام تاریخچه پردازش‌شده، با EvaluationTimeoutSeconds قبلی و حداکثر۱۲۰ثانیه، برقرار است. محافظ worker تماس را بعد۱۳۰ثانیه لغو و lease بعد۱۵۰ثانیه منقضی می‌شود. زمان‌ها تضمین سرعت یا درصد پیشرفت نیستند. توقف عادی failure را ذخیره می‌کند؛ مرگ فرایند با expiry به Failed/report_interrupted می‌رود. worker شکست را خودکار تکرار نمی‌کند.

claim، retry، ثبت مرحله و commit زیر قفل کوتاه سازمان و کنترل lease؛ شبکه خارج transaction. سازمان worker فقط از صف ماندگار است. نتیجه generator قدیمی یا جلسه حذف‌شده commit نمی‌شود؛ cached تاریخی در شکست حفظ می‌شود.

## API و اتصال فرانت

فقط Owner همان سازمان؛ جلسه غایب/حذف‌شده/سازمان دیگر404/not_found. آماده‌نبودن گزارش404 نیست.

| درخواست | رفتار |
| --- | --- |
| GET `/api/interviews/{id}/evaluation-report/status` | HTTP200 با DTO وضعیت ثابت برای polling |
| GET `/api/interviews/{id}/evaluation-report` | cached: HTTP200 با InterviewReportDto قبلی؛ بدونcached و Queued/Processing: HTTP202 با DTO وضعیت، Location به status و Retry-After=3؛ Failed/Unavailable بدونcached: HTTP200 با DTO وضعیت |
| POST `/api/interviews/{id}/evaluation-report` | فقط retry پس از Failed؛ HTTP202 با Queued و Location به status؛ مدل داخل HTTP اجرا نمی‌شود |
| POST `/api/interviews/{id}/evaluation-report/regenerate` | همان محدودیت retry، با settings اختیاری `{topicWeights,reviewThreshold}` |

هر دو POST در صف/پردازش/موفقیت HTTP409/report_retry_not_allowed دارند. تنظیمات نامعتبر400/report_settings_invalid؛ ناتمام409/interview_not_completed؛ قرارداد بدون گزارش403/feature_unavailable. cached موفق با POST بازتولید نمی‌شود.

```json
{
  "interviewId": "00000000-0000-0000-0000-000000000001",
  "status": "Processing",
  "stage": "Generating",
  "message": "گزارش در حال تولید و ارزیابی پاسخ‌ها است.",
  "canRetry": false,
  "attempts": 1,
  "requestedAtUtc": "2026-10-13T06:00:00Z",
  "startedAtUtc": "2026-10-13T06:00:02Z",
  "finishedAtUtc": null,
  "failureCode": null,
  "failureStage": null,
  "reportAvailable": false
}
```

status: Queued/Processing/Succeeded/Failed/Unavailable؛ stage دقیق‌تر، با WaitingForCompletion پیش از پایان و Unavailable برای قرارداد فاقد گزارش. canRetry مرجع نمایش دکمه و محدودیت سرور است. attempts تعداد claim واقعی، نه تعداد GET/POST. requestedAtUtc→startedAtUtc زمان صف و startedAtUtc→finishedAtUtc مدت تلاش؛ UTC و بدون درصد پیشرفت فرضی. failureStage مرحله شکست و message توضیح فارسی قابل نمایش است.

failureCode امن: report_timeout، report_invalid_response، report_validation_failed، evaluation_report_unavailable، report_generation_failed، report_interrupted؛ متن خام provider یا داده داوطلب عرضه نمی‌شود. retry زمان‌ها/خطای قبلی را پاک می‌کند؛ فقط یکی از POSTهای همزمان صف می‌سازد. POST بدونsettings آن‌ها را حفظ می‌کند؛ regenerate با `{}` پیش‌فرض را انتخاب می‌کند. قواعد مثبت بودن وزن/موضوع معتبر و آستانه۰..۱۰۰ قبلی محفوظ‌اند.

فرانت پس از completion و refresh، هر۳ثانیه status را تا Succeeded/Failed بخواند؛ در صف/پردازش message مرحله و دکمه غیرفعال، Failed با canRetry=true دکمه درخواست مجدد، POST202 سپس polling؛ فقط reportAvailable=true گزارش را از GET بخواند. schema ثابت status، ابهام پاسخ GET اصلی را برطرف می‌کند. چاپ گزارش آماده‌نشده409/report_not_ready دارد. Angular اینجا تغییر نکرده است.

## migration و تاریخچه

پیش از اجرای نسخه جدید، migration `20261013054737_AddReportGenerationLifecycle` در تسک انتشار لازم است. فیلدهای چرخه/index به جدول موجود افزوده می‌شوند؛ Repository/زیرساخت توزیع‌شده تازه نداریم. cached بدونlease→Succeeded؛ lease تاریخی→Generating؛ JSON/model/provider/version/زمان قدیمی محفوظ. رکورد خالی→Failed/report_not_generated. Completed قدیمی بدون رکورد هم همین وضعیت و canRetry=true دارد تا Owner درخواست کند؛ کل تاریخچه خودکار به صف نمی‌رود و زمان تاریخی حدسی ساخته نمی‌شود. downgrade دارای صف/تاریخچه تلاش با guard رد می‌شود.

تنظیمات AI/secret، DB مقصد و انتشار تغییر نکرده‌اند. handler جاری HTTP/body را یکسان تنظیم می‌کند؛ report_processing در کد قبلی فقط POST بود و GET بدوننتیجه404. منشأ HTTP404 همراه body409 مقصد بدون نسخه/لاگ پراکسی تأیید نشده است.
