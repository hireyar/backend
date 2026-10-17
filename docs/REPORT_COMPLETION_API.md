تغییر2026-10-17: تولید خودکار برای تکمیل‌های جدید در [AUTOMATIC_REPORT_COMPLETION_API](AUTOMATIC_REPORT_COMPLETION_API.md) تعریف شده و تولید صرفاً دستی این سند را برای آن جلسات جایگزین می‌کند؛ تاریخچه بدون درخواست صریح بازپردازش نمی‌شود.

# تکمیل داده‌های گزارش — 2026-10-17

این قرارداد به درخواست تکمیل موارد باقی‌مانده مرجع HTML، بخش داده‌ای ADR-059 و موارد متناظر در REPORT_TEMPLATE_PARITY را تکمیل می‌کند. حذف قبلی تناسب از خروجی عمومی اکنون با درخواست جدید تغییر کرده است؛ policy/وزن/فرمول عمومی و PUT policy404 همچنان محفوظ‌اند. Angular در این ریپو نیست. هیچ داده نمونه یا عدد جایگزین در محصول ساخته نمی‌شود.

## علت null سه شاخص

مسیر production از قبل شامل catalog، درخواست مدل، اعتبارسنجی، ذخیره DimensionsJson و read model بوده است؛ پس null به‌تنهایی اثبات نبود قابلیت نیست. علت همان گزارش واقعی بدون شناسه و بررسی رکورد/پاسخ تشخیصی آن تأیید نمی‌شود. این تغییر تفکیک زیر را در `specializedProfile.metrics[].issues[]` و معادل عمومی ارائه می‌کند:

| reasonCode | معنی |
|---|---|
| dimension_not_returned | ارزیابی اصلی موجود است ولی آن شاخص در خروجی/ذخیره نیست؛ نمونه قابل بازتولید: JSON اصلی موفق بدون dimensions. |
| legacy_assessment_without_dimensions | evaluatorVersion قدیمی answer-quality/1؛ ارزیابی موفق تاریخی به‌صورت خودکار بازتولید نمی‌شود. |
| dimension_invalid_output | قالب/نمره/اطمینان/شاهد/تکرار کد یا طول متن شاخص نامعتبر است. |
| dimension_storage_invalid | JSON یا شواهد ذخیره‌شده شاخص معتبر نیست. |
| dimension_insufficient_evidence | مدل صریحاً شاهد کافی نداشته؛ null یک نتیجه معتبر است. |
| job_description_missing | شرح نقش در snapshot نیست؛ job_alignment/work_alignment نمره حدسی نمی‌گیرد. |
| primary_assessment_not_available یا کد امن assessment_* | پاسخ نهایی/ارزیابی اصلی معتبر موجود نیست؛ علت شکست اصلی حفظ می‌شود. |

هر issue شامل questionId/state/reasonCode است؛ یک سؤال بی‌پاسخ می‌تواند issue جدا داشته باشد. quality/performance از نمره اصلی می‌آید و جای سه شاخص مستقل نمی‌نشیند. Prompt اکنون وجود آرایه dimensions و یک entry برای تک‌تک کدهای ورودی، حتی insufficient_evidence، را صریحاً الزام می‌کند. validator همچنان مستقل است و دستور prompt موفقیت واقعی مدل را تضمین نمی‌کند.

خطاهای HTTP402،401،403 و404 ارزیابی اصلی به‌ترتیب payment_required،unauthorized،forbidden و endpoint_or_model_not_found امن تفکیک می‌شوند؛ متن خام provider/کلید/پاسخ کارجو در لاگ یا reason نمی‌آید. این کد HTTP لزوماً علت مالی/احراز دقیق درون provider را اثبات نمی‌کند. narrative و dimensionRecovery نیز payment_required/unauthorized/forbidden جدا دارند.

## بازیابی شاخص‌های جاافتاده بدون تغییر نمره موفق

`POST /api/interviews/{id}/report/answers/{answerId}/dimensions/retry`

- فقط Owner، همان سازمان، جلسه کامل/حذف‌نشده، قابلیت خریداری‌شده گزارش و پاسخ نهایی دارای assessment موفق.
- ارزیابی اصلی، EvidenceJson، Provider/Model اصلی، Attempts اصلی، نمره و توضیح موفق تغییر نمی‌کنند. فقط شاخص واقعاً مفقود/نامعتبر قابل ترمیم است؛ شاخص موفق قبلی overwrite نمی‌شود و explicit insufficient_evidence یا نبود JD دلیل تولید عدد نیست.
- `dimensionRecovery` زیر assessment شامل state،attempts،canRetry،failureCode و shouldPoll است. سه تلاش مستقل حداکثر؛ قفل کوتاه سازمان، شبکه بیرون قفل، timeout موجود، lease و token مانع ثبت پاسخ قدیمی می‌شوند. درخواست هم‌زمان تماس دوم نمی‌سازد. پس از قطع درخواست/crash، تلاش منقضی با درخواست صریح بعدی و همان بودجه قابل بازیابی است؛ worker جدید و retry خودکار نداریم.
- پاسخ200 از نوع RetryAnswerAssessmentResponse؛ هنگام تلاش هم‌زمان ممکن است dimensionRecovery.state=Processing و shouldPoll=true باشد. GET گزارش را با فاصله حداقل۳ثانیه poll کنید؛ پس از پایان قطع کنید. canRetry برای Recruiter=false است و POST403 می‌گیرد.
-409: dimensions_not_ready یا dimensions_retry_unavailable. پاسخ primary Failed باید از مسیر قبلی assessment/retry استفاده کند، نه این مسیر.

## تناسب و سطح نسبت به انتظار

`positionFit` اکنون در GET عمومی ارائه می‌شود: state/value/confidence/classification/coverage/skills/message. از محاسبه موجود و فقط پاسخ معتبر در سطح هدف snapshot یا بالاتر می‌آید؛ وزن‌ها،policy وformula همچنان از خروجی عمومی حذف می‌شوند، سابقه fit داخلی در snapshot عمومی مخفی می‌ماند. section positionFit نیز برگشته است (۱۲بخش). coverage ناقص و مهارت الزامی بدون شاهد همان محدودیت قبلی را دارند. این عدد احتمال موفقیت استخدام یا مجوز پذیرش خودکار نیست.

`expectedLevel` داده مستقلی برای مقایسه سطح دارد: state،belowTargetAnswers،atTargetAnswers،aboveTargetAnswers،answers وmessage. هر answer،questionId/topic/askedDifficulty/targetDifficulty/relation دارد و فقط پاسخ ارزیابی‌شده با شاهد وارد آن می‌شود. درصدی شبیه۷۸٪ مرجع ساخته نشده: روش مستقلی برای چنین درصدی توافق نشده است. HTML شمارش پایین‌تر/هم‌سطح/بالاتر را نمایش می‌دهد؛ این شمارش دشواری سؤال است، نه نمره کیفیت پاسخ یا دانش کلی فرد.

## روایت AI، پیشنهاد مرحله بعد، سؤال تکمیلی و سابقه کار

`POST /api/interviews/{id}/report/narrative`

بدنه: `{ "reportFingerprint": "اثر انگشت فعلی GET گزارش" }`.

فقط Owner سازمان جاری، جلسه کامل با متن حداقل یک پاسخ نهایی و قابلیت گزارش. این عملیات صریح و همزمان است؛ تماس مدل در GET و job پس‌زمینه تازه نداریم. از همان provider و AnswerAssessmentModel مستقل تنظیم‌شده گزارش و timeout موجود استفاده می‌شود؛ مدل زنده مصاحبه تغییر نمی‌کند. نتیجه در SessionReportNarratives مستقل ذخیره می‌شود و از طریق `report.narrative` خوانده می‌شود.

- `summary`: متن روایی مستقل، حداکثر۲۰۰۰کاراکتر، همراه evidence.
- `nextStep`: پیشنهاد تمرکز مصاحبه تکمیلی/بررسی انسانی، حداکثر۱۰۰۰کاراکتر، همراه evidence؛ تصمیم پذیرش/رد یا دعوت واقعی ایجاد نمی‌کند.
- `followUpQuestions`: حداکثر۲سؤال پیشنهادی با text/topic/reason/evidence؛ این‌ها سؤال‌های پرسیده‌شده جلسه یا تغییر بانک سؤال نیستند.
- `experience`: state=SelfReported یا InsufficientEvidence، statements وmessage. مدل فقط sourceId گفتار صریح سابقه را انتخاب می‌کند؛ متن/offset/answerId/kind از منبع دقیق سرور بازسازی می‌شود. منبع رزومه snapshot، معرفی یا پاسخ نهایی همان جلسه است. سابقه تأییدشده، محاسبه مجموع سال‌ها از تاریخ‌ها یا عدد yearsExperience حدسی ارائه نمی‌شود. در نبود گفته صریح statements خالی است.
- شواهد summary/nextStep/سؤال تکمیلی باید متعلق به متن پاسخ نهایی همان جلسه باشند؛ رزومه به‌تنهایی جای شاهد پاسخ را نمی‌گیرد. شناسه ناشناخته، شاهد جعلی، extra field، نوع/طول نامعتبر و بیشتر از۲سؤال رد می‌شوند. متن مدل با وجود شاهد ممکن است نیاز به بررسی معنایی انسان داشته باشد؛ validator صحت مستقل معنای ادعا یا سابقه استخدام را تضمین نمی‌کند.
- هر source شامل id محلی،kind،answerId اختیاری،start وtext دقیق منبع است. کل source catalog در log یا پاسخ عمومیِ بدون نیاز کپی نمی‌شود؛ فقط evidence انتخاب‌شده برمی‌گردد. sourceId دستور اجرایی یا مرجع خارجی نیست.
- `state`: NotReady/NotGenerated/Processing/Failed/Unavailable/InsufficientEvidence/Available؛ attempts،canGenerate،failureCode،inputHash،generatedAtUtc،message وshouldPoll نیز موجودند. فقط Processing باید با فاصله حداقل۳ثانیه poll شود. NotGenerated صف یا job منتظر نیست. canGenerate برای Recruiter=false؛ تولید بدون شاهد یا قبل completion409 است.
- پاسخ200 شامل ReportNarrative است؛ شکست مدل نیز state=Failed ذخیره‌شده دارد و نمره‌های قبلی را از بین نمی‌برد. درخواست همزمان هنگام Processing200 همان وضعیت برمی‌گرداند. هر ورودی/نسخه حداکثر۳تلاش با token/lease دارد؛ موفقیت cache می‌شود و بازتولید موفق همان ورودی وجود ندارد. قطع درخواست/کرش تا پایان lease در Processing می‌ماند و سپس Failed قابل درخواست مجدد با بودجه باقی‌مانده می‌شود.
-409: report_changed،narrative_not_ready،narrative_evidence_missing یاnarrative_retry_unavailable. درخواست تکراری موفق با fingerprint شروع همان تولید یا fingerprint فعلی cache همان نسخه را برمی‌گرداند؛ تغییر واقعی ارزیابی/منبع cache قدیمی را به گزارش جدید وصل نمی‌کند.

## نسخه و تصمیم انسانی

پیش از تولید، fingerprint همان نسخه محاسباتی است. بعد از روایت موفق، نتیجه معتبر روایت هم وارد fingerprint نهایی می‌شود؛ review قبلی ReviewOutdated می‌شود. snapshot تصمیم تازه، narrative همراه شواهد را هم نگه می‌دارد. retry/فراخوانی قدیمی حق overwrite نتیجه تلاش تازه ندارد؛ تغییر ورودی حین مدل با narrative_input_changed رد و در نسخه جدید NotGenerated نشان داده می‌شود. تاریخچه نسخه‌های قبلی حذف نمی‌شود. روایت هیچ امتیاز، نتیجه زنده، اعتبار مالی یا تصمیم انسانی را تغییر نمی‌دهد.

## migration و تحویل

Migration افزایشی `20261017083027_CompleteReportEvidenceAndNarrative` چهار ستون بودجه/lease بازیابی شاخص و جدول SessionReportNarratives را اضافه می‌کند. FK مرکب سازمان/جلسه، CHECK وضعیت/تلاش/lease و JSONB دارد. Down در صورت وجود روایت یا تلاش بازیابی با P0001 متوقف می‌شود؛ داده موفق قبلی حذف/بازنویسی یا خودکار backfill نمی‌شود. migration باید پیش از API تازه در محیط موردنظر با مجوز انتشار جدا اعمال شود؛ روی مقصد اعمال نشده است.

نمای HTML همان چیدمان قبلی است و اکنون داده‌های جدید را در کارت/جمع‌بندی/پیشنهاد/شناسنامه موجود مصرف می‌کند. دکمه‌های عملیات پنل هنوز به Angular جدا نیاز دارند. فونت، پخش خصوصی احرازشده و ادعای سلامت کامل ضبط در این تغییر تکمیل نشده‌اند. API فرانت باید به‌جای متن جایگزین، state،issues،canRetry/canGenerate وshouldPoll را مصرف کند.
## نتیجه اعتبارسنجی و نمونه‌ها

Build بدون warning/error و کل۴۸۷تست بدون شکست یاSkip روی PostgreSQL18 محلی و FFmpeg واقعی موفق است؛ فایل نتیجه `tests/HireYar.Tests/TestResults/report-completion-verified.trx`. نمونه‌های صرفاً آزمایشی هر دو نوع از GET واقعی و renderer جاری در `artifacts/report-completion-preview/general.*` و `specialized.*` هستند؛ JSON همان wire response است، نه DTO داخلی دارای policy. مدل واقعی فراخوانی نشده و بهبود prompt/validator، موفقیت معنایی provider مقصد یا ترمیم یک جلسه واقعی را اثبات نمی‌کند.

همه متن‌های مدل و منبع در فرانت plain text نمایش داده شوند؛ renderer بک‌اند HTML-encode می‌کند. کنترل‌های نمایشی بازیابی، مانند canRetry وابسته به نقش، وارد fingerprint محتوایی نمی‌شوند؛ هر دو نقش مجاز همان نسخه و روایت را می‌بینند. قاعده پیشنهاد/تصمیم خودکار جدید، صف مستقل، تنظیم درگاه/اعتبار یا تغییر حالت مصاحبه زنده اضافه نشده است.
