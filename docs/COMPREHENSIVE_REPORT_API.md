# گزارش جامع کامل — قرارداد جاری بک‌اند

تمام ۱۲ بخش گزارش جدید پیاده شده‌اند. این سند بر یادداشت‌های تاریخی «مرحله بعد»، NotStarted و NotImplemented در اسناد گزارش اولویت دارد. موتور قدیمی تولید یکپارچه بازگردانده نشده است. Angular در ریپوی جداست و باید DTOهای این سند را نمایش دهد.

## خواندن و نمایش

`GET /api/interviews/{id}/report` و `/report/print` با scope سازمان، Owner/Recruiter، feature snapshot مالی، NoStore و رسانه خصوصی قبلی محفوظ‌اند. schemaVersion برای سازگاری session-report/1 و state برای جلسه کامل BaseReady باقی مانده؛ آمادگی تحلیل از coverage، processing و state تک‌تک بخش‌ها خوانده می‌شود. GET فقط از داده‌های ذخیره‌شده می‌خواند و هیچ تماس AI یا write ندارد.

تمام بخش‌ها در اولین پاسخ وجود دارند: navigation، identity، score، positionFit، statistics، summary، strengthsAndImprovements، specializedProfile، generalProfile، questions، signals، humanReview. پروفایل نوع دیگر NotApplicable است. هیچ بخش جاری NotStarted یا NotImplemented نیست؛ processing.hasUnimplementedSections=false است. Available، Partial، InsufficientEvidence و NoFindings وضعیت نتیجه واقعی‌اند و به معنی قابلیت ساخته‌نشده نیستند. NotReady برای جلسه کامل‌نشده و AwaitingReview برای عمل انسانی است. فقط processing.shouldPoll=true تایمر ارزیابی می‌خواهد؛ تصمیم انسانی هرگز spinner خودکار نمی‌سازد.

بدنه شامل فیلدهای قبلی به‌علاوه coverage، positionFit، specializedProfile، generalProfile، policy، humanReview، dataGaps و fingerprint است. ReportNotes علاوه بر items متنی، findings ساختاری شامل questionId/answerId/topic/score/text/evidence دارد. هر عدد و نمودار باید از همان فیلدهای API رسم شود؛ null به صفر تبدیل نشود.

## امتیاز، پوشش و تناسب

- فقط assessment موفق پاسخ نهایی با نمره/اطمینان معتبر و شاهد متعلق به همان پاسخ وارد نمره می‌شود. پاسخ بی‌پاسخ، Failed، InsufficientEvidence یا شاهد خراب حذف می‌شود و در مخرج نمره صفر نیست؛ کمبودها جدا در dataGaps و coverage دیده می‌شوند.
- تخصصی: برای هر SkillId میانگین نمره پاسخ‌های معتبر آن مهارت، سپس میانگین وزن‌دار این میانگین‌ها. تعداد بیشتر سؤال تطبیقی وزن مهارت را خودکار بیشتر نمی‌کند. وزن پیش‌فرض max(1, snapshot.Priority) است و وزن صفرِ تنظیم‌شده فقط مهارت اختیاری را از محاسبه حذف می‌کند.
- عمومی: میانگین ساده نمره پاسخ‌های نهاییِ معتبر. وزن policy در این نوع بر تناسب نقش اثر دارد، نه بر این میانگین عمومی. نمره بین طرح‌ها/سطوح متفاوت به‌عنوان رتبه استخدامی قابل تعمیم نیست.
- confidence میانگین با همان قاعده وزن است؛ تخمین مدل و غیرکالیبره است. اعداد نمایشی تا دو رقم اعشار با AwayFromZero گرد می‌شوند. coverage.ratio تعداد نمره معتبر/سؤال پرسیده‌شده است؛ سؤال‌های بانکِ استفاده‌نشده مخرج نیستند. isProvisional در جلسه کامل‌نشده یا پوشش ناقص true است.
- PositionFit: در هر مهارت فقط نمره سؤال‌هایی با difficulty >= snapshot.TargetDifficulty؛ سپس میانگین وزن‌دار مهارت‌های مشاهده‌شده. Skills تمام مهارت‌های snapshot از جمله الزامی‌های پرسیده‌نشده را نشان می‌دهد. coverage تناسب مجموع وزن مهارت‌های دارای شاهد در سطح هدف/تمام وزن‌های مثبت است؛ نمره مهارت نامعلوم null می‌ماند.
- classification: InterviewIncomplete، InsufficientCoverage، RequiredSkillsUnobserved، AtConfiguredThreshold یا BelowConfiguredThreshold. تا حداقل پوشش policy حاصل نشود یا مهارت الزامی بدون شاهد باشد، عبور از آستانه ادعا نمی‌شود. این مقایسه معیار مشاهده‌شده است، نه احتمال موفقیت شغلی یا تصمیم استخدام/رد.
- formula در خود DTO صریح است؛ تمام میانگین‌ها توسط سرور محاسبه می‌شوند. اگر وزن مثبتِ مشاهده‌شده یا شاهد کافی نباشد Value=null و InsufficientEvidence است؛ تقسیم بر صفر یا نمره حدسی نداریم.

## پروفایل‌ها و نمودارها

Axes پروفایل از SkillId/Topic واقعاً پرسیده‌شده با نمره/اطمینان، تعداد پرسیده/نمره‌دار، سطوح، questionIds و شواهد ساخته می‌شوند. عنوان/ترتیب و سطوح از snapshot هستند. specializedProfile چهار حوزه دارد: performance از نمره اصلی پاسخ‌ها، knowledge_depth، communication و job_alignment. generalProfile هفت شاخص دارد: communication، motivation، teamwork، work_alignment، accountability، conflict_management و documentation.

شاخص‌ها از ارزیابی همان شاخص در همان پاسخ میانگین گرفته می‌شوند؛ نمره کلی به شاخص نامرتبط کپی نمی‌شود. تفسیر HR فقط نمونه‌ها و اظهارات شغلیِ صریح است؛ انگیزه واقعی/شخصیت/صفات حفاظت‌شده یا تناسب فرهنگی جمعیتی استنباط نمی‌شود. work_alignment صرفاً همسویی با شیوه کارِ صریح نقش است. معیار تبعیض‌آمیز یا صفت شخصی حساس حتی اگر صریحاً در متن آمده باشد، مبنای نمره نیست. بدون شرح شغل، job_alignment/work_alignment ناشناخته می‌مانند. نبود شاهد با InsufficientEvidence/null مشخص می‌شود؛ خرابی یک شاخص فقط همان شاخص Unavailable می‌شود.

Distribution شامل strong/satisfactory/needsImprovement/unscored با آستانه‌های policy است. چاپ RTL تمام بخش‌ها، جدول تناسب و تنظیمات، نمودار رادار، نوار شاخص‌ها، سؤال/پاسخ/شاهد، قوت/بهبود و سابقه انسانی را دارد. رادار فقط در صورت حداقل سه محور و معلوم بودن همه مقادیر، چندضلعی نمره را رسم می‌کند؛ محور نامعلوم صفر فرض نمی‌شود. چاپ بدون JS و دارایی خارجی، با HTML escape و CSP قبلی و CSS مناسب Save as PDF است. رسانه باید با درخواست احرازهویت‌شده پنل باز شود؛ HTML چاپ توکن یا URL عمومی صادر نمی‌کند.

## جمع‌بندی و یافته‌ها

Summary یک متن محاسباتی سرور است، نه درخواست مدل دوم. پایان جلسه، تعداد نمره معتبر، پوشش، نمره موقت/نهایی مشاهده‌شده، تعداد یافته‌ها و محدودیت تناسب را توضیح می‌دهد؛ حتی در صورت شکست تمام ارزیابی‌ها، گزارش پایه و جمع‌بندی کمبود شواهد وجود دارند.

Strengths از پاسخ با score >= strongScore و Improvements از score < passingScore ساخته می‌شوند؛ explanation و شواهد همان پاسخ و لینک questionId همراه‌اند. NoFindings یعنی شاهد مطابق آستانه وجود ندارد؛ نشانه کار ناتمام نیست. پاسخ مفقود/خطای فنی به‌عنوان ضعف شخص نوشته نمی‌شود؛ dataGaps این موارد و مهارت الزامی بی‌شاهد را جدا می‌کند.

## تنظیمات مالک

`PUT /api/interviews/{id}/report/policy` فقط Owner همان سازمان با قابلیت report:

```json
{"revision":"00000000-0000-0000-0000-000000000000","passingScore":60,"strongScore":80,"minimumCoverage":0.6,"weights":[{"skillId":"شناسه واقعی snapshot","weight":100}]}
```

revision اولیه Guid.Empty است؛ مقدار جاری و تمام SkillIdها را از GET.policy کپی کنید. وزن‌ها باید دقیقاً همه مهارت‌های snapshot، بدون تکرار/شناسه خارجی باشند؛ ۰..۱۰۰۰ با حداکثر۴ اعشار و برای مهارت الزامی حتماً مثبت. حداقل یک وزن مثبت لازم است. ۰<=passingScore<=strongScore<=۱۰۰ (۴اعشار)، ۰<minimumCoverage<=۱ (۶اعشار). آستانه‌ها پیش‌فرض نمایشی‌اند، نه حکم قبولی استخدام.

۲۰۰ با ReportPolicy و revision تازه؛ درخواست همزمان با revision قبلی ۴۰۹ report_policy_changed، مقادیر نامعتبر ۴۰۰ report_policy_invalid، سازمان دیگر/حذف‌شده۴۰۴، عضو غیرمالک۴۰۳. تغییر سیاست با قفل کوتاه سازمان است؛ نتیجه‌های AI را تغییر یا بازتولید نمی‌کند. اگر JSON تنظیمات ذخیره‌شده خراب باشد، GET با policy.state=Defaulted و warningCode=report_policy_invalid از پیش‌فرض‌های آشکار استفاده می‌کند و کل گزارش نمی‌شکند.

## تصمیم انسانی، نسخه و سابقه

GET.humanReview شامل state، canSubmit، submitUrl، reportFingerprint، latest، history و message است. AwaitingReview یعنی تصمیمی ثبت نشده؛ Reviewed فقط برای تصمیم متصل به fingerprint جاری، ReviewOutdated برای گزارش تغییرکرده و UnversionedReview برای تصمیم قدیمی بدون اتصال نسخه است. Recruiter می‌خواند؛ فقط Owner می‌تواند ثبت کند.

`POST /api/interviews/{id}/report/review`:

```json
{"requestId":"UUID تازه برای عمل کارشناس","decision":"review","reportFingerprint":"مقدار دقیق fingerprint از GET","note":"یادداشت اختیاری تا ۲۰۰۰ کاراکتر"}
```

decision فقط advance/review/reject؛ جلسه باید Completed باشد. تصمیم به‌صورت خودکار از score/fit ثبت نمی‌شود. پاسخ۲۰۰ ReportHumanReview است. ثبت با کلید requestId تکرارپذیر است؛ همان بدنه رکورد دوم نمی‌سازد و بدنه متفاوت با همان کلید Conflict است. fingerprint نامعتبر۴۰۰، گزارش تغییرکرده۴۰۹ report_changed، جلسه ناتمام۴۰۹ interview_not_completed، سقف۱۰۰ تصمیم۴۰۹ review_history_limit، scope و نقش مانند policy است.

در قفل سازمان، گزارش جاری دوباره خوانده و fingerprint مقایسه می‌شود؛ همزمانی worker یا تغییر policy نمی‌تواند تصمیم را به نسخه اشتباه متصل کند. hash شامل نسخه محاسبه، سؤال/پاسخ/ارزیابی/شاخص‌ها، snapshot نقش/مهارت و سیاست است؛ تاریخ خواندن، هویت مشاهده‌کننده و تاریخچه تصمیم‌ها در hash نیستند. بنابراین Owner و Recruiter یک fingerprint می‌بینند و ثبت تصمیم خودش نسخه گزارش را عوض نمی‌کند.

همراه تصمیم، snapshot نسخه، score، coverage، positionFit و policy در DB نگه داشته و از history.snapshot خوانده می‌شود؛ پس از تغییر سیاست، اعداد و مبنای بررسی قبلی از بین نمی‌روند. APIهای مشترک /review و /decisions قبلی حفظ‌اند؛ تصمیم‌های آن مسیر بدون fingerprint به‌صورت UnversionedReview نمایش داده می‌شوند. history فقط ردیف‌های انسانی همان سازمان/جلسه است، نه رویداد کارجو.

## ارزیاب و سازگاری داده

answer-quality/2 همچنان همان پردازش مستقل، سه تلاش، deadline، lease، fencing و retry۲۰۲ نوع‌دار را دارد. input اکنون شرح شغل snapshot و تعریف شاخص‌های همان نوع را هم دارد. مدل dimensions با code/verdict/score/confidence/explanation/evidenceIds می‌دهد؛ quote/answerId/offset توسط سرور از همان قطعات پاسخ ساخته می‌شوند. explanation هر شاخص حداکثر۵۰۰ کاراکتر و کل خروجی تا۱۶۰۰۰ کاراکتر است؛ سقف envelope۶۴۰۰۰ بایت. خروجی نامعتبر یک شاخص، نمره اصلی یا شاخص‌های معتبر دیگر را حذف نمی‌کند.

نتیجه‌های موفق قبلی پاک یا بازتولید نمی‌شوند. داده قدیمی که شاخص‌ها را نداشته، برای همان شاخص InsufficientEvidence دارد؛ نمره اصلی و پروفایل موضوعی آن قابل استفاده است. این وضعیت کمبود داده است و job ناموجود/NotImplemented نیست. مدل واقعی کیفیت معنایی را تضمین نمی‌کند؛ بررسی انسانی و میزان پوشش همیشه نمایش داده می‌شوند. هیچ متن واقعی کارجو/خروجی خام یا secret وارد Git/لاگ جدید نشده است.

## migration و انتشار

Migration `20261015165557_CompleteSessionReports` افزایشی است: DimensionsJson با پیش‌فرض[]، فیلدهای nullable ReportFingerprint/ReportSnapshotJson در تاریخچه بررسی، و جدول SessionReportPolicies با FK سازمان/جلسه و check آستانه‌ها. سابقه مصاحبه، نمره موفق و داده مالی حذف نمی‌شوند. SQL idempotent در artifacts/complete-session-reports.sql تولید می‌شود. Down در صورت وجود شاخص، policy یا تصمیم نسخه‌دار با P0001 متوقف می‌شود تا سابقه پاک نشود.

پیش از API جدید migration لازم است؛ به‌روزرسانی قرارداد فرانت جداگانه است. migration/تنظیمات مقصد، push/deploy و production در این کار تغییر داده نشده‌اند. همه بخش‌های بک‌اند و چاپ در این ریپو پیاده شده‌اند؛ پیاده‌سازی Angular در ریپوی حاضر وجود ندارد.

## پنهان‌سازی موقت معیارها و وزن مهارت‌ها — 2026-10-16
با درخواست کاربر، policy و وزن مهارت‌ها و فرمول/آستانه‌ها از خروجی عمومی گزارش و snapshotهای تصمیم انسانی حذف شدند؛ چاپ نیز این اطلاعات را نمایش نمی‌دهد. PUT report/policy پس از کنترل دسترسی گزارش 404 می‌دهد. داده ذخیره‌شده و محاسبات داخلی محفوظ‌اند؛ migration لازم نیست. این تنظیمات توسط AI تولید نمی‌شوند و فراخوانی AI مستقلی نداشتند؛ ارزیابی پاسخ‌ها تغییر نکرد. Angular در مخزن جداست و باید کارت «معیارها و وزن مهارت‌ها» را حذف کند و نبود policy را بپذیرد. انتشار انجام نشده است.

## حذف موقت تناسب با نیازهای موقعیت — 2026-10-16
طبق درخواست کاربر، بخش positionFit از فهرست بخش‌ها و چاپ حذف شد؛ positionFit و fit داخل snapshot تاریخچه در خروجی عمومی HTTP ارسال نمی‌شوند. جمع‌بندی دیگر وعده نمایش جداگانه تناسب را نمی‌دهد. محاسبات و داده‌های داخلی برای بازگرداندن آینده محفوظ‌اند؛ AI درخواست جداگانه‌ای برای این بخش ندارد. Angular جدا باید کارت/جدول مهارت‌های این بخش، لینک ناوبری، چاپ و وابستگی به positionFit را حذف کند؛ امتیاز کل و پروفایل تخصصی مستقل حفظ شوند. migration و انتشار ندارد.
