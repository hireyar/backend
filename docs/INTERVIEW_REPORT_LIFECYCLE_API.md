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

## محافظ‌های پردازش — تکمیل 2026-10-14

سه مرحله Generating/Validating/Saving با lease غایب/منقضی یا گذشت۱۵۰ثانیه از start، Failed/report_interrupted می‌شوند. GET status این بازیابی را زیر قفل سازمان و با reload persist می‌کند؛ worker یک poll بازیابی مستقل از مدل‌های درحال‌اجرا دارد. بازیابی transcript را بارگذاری یا مدل را دوباره صدا نمی‌زند. DB و قفل باید در دسترس باشند؛ هر poll دارای batch۲۰ و چهار recovery با deadline۵ثانیه است. صف Queued به ظرفیت وابسته است و deadline تلاش شامل زمان صف نیست.

deadline۱۳۰ثانیه کل تلاش پس از claim، شامل network و مرحله/commit است؛ انتظار provider غیرهمکار با WaitAsync قطع و timeout به Failed/report_timeout تبدیل می‌شود. shutdown به report_interrupted و مرگ فرایند به lease expiry می‌رود. retry دستی پابرجا و cached تاریخی در recovery/شکست محفوظ است؛ تلاش دیررس نتیجه تلاش جدید را عوض نمی‌کند. schema/config/migration تازه لازم نیست.

promptVersion جدید report-v3.1 با schema گزارش v3، codeهای واقعی شاخص‌ها و تعداد ارزیابی پاسخ‌های نهایی را در هر request صریح می‌کند. فرانت نباید نسخه پرامپت را با نسخه schema یکی بداند؛ cached با promptVersion قبلی محفوظ است. محافظ نقل‌قول/answerId/نمره پابرجاست. failureCode و DTO status تغییر نکرده‌اند و message اعتبارسنجی شفاف‌تر است. لاگ شکست reason ثابتی مانند json_schema_invalid، metric_catalog_invalid، metric_value_invalid، metric_citation_invalid، answer_coverage_invalid، answer_evaluation_invalid یا finding_citation_invalid دارد؛ متن provider یا پاسخ داوطلب ثبت نمی‌شود. reason جزئی در DB/DTO ذخیره نمی‌شود و برای شکست قدیمی قابل بازیابی نیست.

پنل باید وضعیت را از GET status تازه بگیرد، در Failed با canRetry=true دکمه retry را فعال و polling پردازش را متوقف کند. نمایش Processing از snapshot قبلی، وقتی status جاری Failed است، باید با polling/refresh اصلاح شود. Angular و انتشار مقصد در این تغییر انجام نشده‌اند.

## اصلاح ارجاع شواهد — 2026-10-14

promptVersion جاری report-v3.2 است و schema عمومی همان report-v3 باقی می‌ماند. sourceRef/answerRef صرفاً قرارداد داخلی مدل‌اند؛ adapter آن‌ها را از قطعات واقعی پاسخ نهایی به answerId/quote قبلی تبدیل می‌کند. هیچ فیلد عمومی جدید یا migration لازم نیست؛ فرانت sourceRef نمی‌گیرد و cached قبلی حفظ می‌شود.

این اصلاح بازنویسی UUID/متن شاهد توسط مدل را حذف می‌کند. شاهد interaction، مرجع ناشناخته، متن/شناسه ساختگی و ارجاع به پاسخ دیگر در questionEvaluation قابل قبول نیستند؛ کنترل شواهد و نمره پابرجاست. پاسخ مدل همچنان می‌تواند از نظر JSON یا ارزیابی نامعتبر باشد؛ اصلاح ارجاع تضمین کیفیت معنایی مدل نیست. علت امن شکست مرجع می‌تواند source_reference_schema_invalid، source_reference_invalid، answer_reference_invalid یا answer_source_mismatch در لاگ باشد؛ خطای adapter با failureCode عمومی report_invalid_response عرضه می‌شود. خطاهای validator مرحله Validating همچنان report_validation_failed دارند.

پس از انتشار مستقل API، Owner می‌تواند گزارش Failed موجود را با POST قبلی retry کند؛ بازیابی خودکار گزارش‌های Failed یا بازتولید گزارش موفق اضافه نشده است. تنظیمات مدل/provider مقصد خودکار تغییر نمی‌کنند.

## اصلاح محدود خروجی — 2026-10-14، report-v3.3

داخل یک generation حداکثر دو تماس مدل انجام می‌شود: خروجی اول کامل بررسی و فقط در صورت نامعتبر بودن، یک درخواست اصلاح با همان context/model/provider ارسال می‌شود. بودجه EvaluationTimeoutSeconds بین تماس‌ها مشترک است و deadline۱۳۰ثانیه قبلی نیز برقرار است. HTTP/network failure برای repair تکرار نمی‌شوند؛ خروجی دوم نامعتبر به Failed می‌رود. attempts همچنان تعداد claimهاست و با تماس repair افزایش نمی‌یابد. retry گزارش Failed از پنل دستی باقی است؛ گزارش موفق بازتولید نمی‌شود.

فاصله لبه و case reference قابل یکسان‌سازی‌اند؛ answer alias فقط با یک شاهد، و quote/answerId اضافی فقط با تطابق دقیق با منبع واقعی پذیرفته می‌شوند. شاهد مبهم/ساختگی یا مرجع ناشناخته رد می‌شود. لاگ reasonهای source_reference_missing، source_reference_unknown و source_reference_conflicting_fields را جدا و repairAttempt=1 را برای تماس اصلاح ثبت می‌کند؛ هیچ متن مدل یا داوطلب در لاگ نیست.

adapter اکنون validation کامل دارد؛ رد نهایی آن معمولاً failureStage=Generating/failureCode=report_invalid_response است. پیام این کد عدم پذیرش خروجی پس از بررسی را بیان می‌کند. JSON/schema/API/DTO قبلی و migration ثابت‌اند؛ تغییر اجباری فرانت ندارد. تضمین موفقیت همه پاسخ‌های مدل یا کیفیت معنایی از این اصلاح نتیجه نمی‌شود.
