# اشتراک واحد و بسته‌های تعدادی مصاحبه — ADR-072 / 2026-10-19

این سند جایگزین مدل حذف‌شده ADR-071 است؛ هیچ جدول یا API مالی قدیمی بازگردانده نشده است. منبع ظاهر/قیمت اولیه `pricing_new (4).html` ارائه‌شده توسط کاربر است. کاربر اجرای migration حذف قبلی و push آن را اعلام کرده است؛ این تسک مقصد را بررسی یا تغییر نداده است.

## کاتالوگ اولیه و واحد پول

تمام قیمت‌ها اعداد صحیح ریالی و خروجی `currency=IRR` هستند؛ UI برای تومان فقط بر۱۰ تقسیم می‌کند. محاسبه قیمت و تخفیف فقط در سرور انجام می‌شود، بدون نرخ ارز و دلار و بدون هزینه دقیقه‌ای.

| محصول | تعداد | مبلغ ریال | تخفیف نسبت به ۴٬۰۰۰٬۰۰۰ریال برای هر مصاحبه |
|---|---:|---:|---:|
| اشتراک هایریار | هر ماه | 90,000,000 | ندارد |
| دانا `dana` | 10 | 40,000,000 | 0% |
| بینا `bina` | 20 | 75,000,000 | 6.25% |
| ژرفا `zharfa` | 50 | 190,000,000 | 5% |
| دلخواه `custom` | 50..300، گام10 | تعداد × 4,000,000 | 0% |

تخفیف بینا عمداً از ژرفا بیشتر است؛ این دقیقاً از HTML محاسبه شده و یکنواخت‌سازی حدسی ندارد. قیمت پایه و ماهانه و قیمت تیم، مشخصات/وضعیت بسته و تخفیف با RowVersion در سوپرادمین قابل ویرایش‌اند. سفارش‌های قبلی دوباره قیمت‌گذاری نمی‌شوند. مبلغ نهایی در صورت اعشارِ ریال به بالا گرد می‌شود.

مدت اشتراک۱/۳/۶/۱۲ماه است؛ تخفیف اولیه دوره‌ها به‌ترتیب۰/۵/۱۰/۱۵درصد و از تنظیمات سوپرادمین خوانده می‌شود. ماه تقویمی UTC با AddMonths محاسبه می‌شود. پنج کاربر شامل Owner رایگان‌اند؛ از نفر۶ تا۳۰ هر نفر ماهانه2,000,000ریال اضافه می‌شود. تعداد بالاتر از۳۰ هزینه بیشتر ندارد؛ با انتخاب۳۰ یا بیشتر ظرفیت نامحدود می‌شود. مبلغ اولیه اشتراک = ماه‌ها × (قیمت ماهانه + min(تعداد انتخابی،۳۰) منهای۵، ضرب‌در قیمت کاربر اضافه). مبلغ نهایی = سقف ریالیِ مبلغ اولیه × (۱ − درصد تخفیف مدت / ۱۰۰)؛ تخفیف شامل هزینه اعضای اضافه هم هست. قیمت ثابت/حدود از کاتالوگ جاری سرور می‌آیند، نه hardcode فرانت.

## چرخه خرید و اشتراک

ثبت‌نام موفق Owner و سازمان جدید، در همان تراکنش یک هدیهٔ یک‌باره شامل اشتراک۴۸ساعته از زمان ثبت‌نام و۲مصاحبه می‌دهد؛ سفارش/پرداخت/تأیید ادمین ندارد. ورود، refresh، ثبت‌نام ناموفق و پذیرش دعوت عضو تیم هدیه نمی‌سازند. ظرفیت تیم و نگهداری رسانه از includedUsers و retentionDays فعلی کاتالوگ snapshot می‌شوند. هدیه برای سازمان‌های قبلی backfill نمی‌شود. سهمیه هدیه هم مانند خرید انقضا ندارد؛ پس از۴۸ساعت استفاده نیازمند اشتراک فعال است. خرید زودهنگام اشتراک به پایان دوره رایگان افزوده می‌شود. ثبت سفارش فقط Owner است؛ وضعیت Pending هیچ دسترسی یا سهمیه نمی‌دهد. فعلاً پرداخت درگاه وجود ندارد: سوپرادمین پس از بررسی پرداخت با مرجع پرداخت غیرخالی و یکتا سفارش را تأیید می‌کند. تأیید با توکن سازمان یا client-side پرداخت موفق قابل جعل نیست. سفارش۲۴ساعت اعتبار دارد؛ لغوشده/منقضی قابل تأیید نیست. مرجع پرداخت اطلاعات کارت/secret/لینک پرداخت نیست و نباید داده حساس در آن فرستاد.

تأیید تکراری همان سفارش با همان مرجع، نتیجه قبلی را برمی‌گرداند؛ مرجع متفاوت برای سفارش تأییدشده409 و مرجع تکراری برای سفارش دیگر409 است. RequestId سمت Owner برای replay ثبت سفارش است؛ همان UUID فقط با همان Selection قابل استفاده است. تغییر انتخاب با UUID قبلی409 دارد. کل مبلغ/تخفیف/تعداد/ظرفیت/retention در سفارش snapshot است. هیچ مقدار مبلغی از فرانت پذیرفته نمی‌شود. تغییر کاتالوگ پس از quote و پیش از ایجاد سفارش409/pricing_changed دارد؛ سفارش موجود تا انقضایش همان قیمت ثبت‌شده را دارد.

تأیید اشتراک اولیه از زمان تأیید شروع می‌شود. تمدید زودهنگام پس از آخرین پایان موجود قرار می‌گیرد و روزهای قبلی را نمی‌سوزاند؛ ظرفیت انتخابی تمدید در دوره جدید اعمال می‌شود و ارتقای فوری میان‌دوره‌ای جداگانه در این تسک نیست. ظرفیت کمتر از تعداد فعلی عضو فعال و دعوت باز رد می‌شود. تأیید بسته، اشتراک را تمدید نمی‌کند و اشتراک، مصاحبه رایگان نمی‌دهد. خرید/تأیید بسته نیازمند اشتراک فعال است. بسته‌ها انقضا ندارند؛ پس از پایان اشتراک، سهمیه باقی است و با تمدید دوباره قابل استفاده می‌شود.

## چرخه مصاحبه

برای ایجاد موقعیت/قالب، پیشنهاد سؤالAI، دعوت جدید و افزودن/پذیرش عضو، اشتراک فعال لازم است. حساب، مالی، وضعیت اشتراک، لغو و خواندن اطلاعات موجود پشت دیوار پرداخت عمومی پنهان نشده‌اند. نقش‌های Owner/Recruiter و مرز سازمان قبلی حفظ‌اند.

دعوت جدید یک سهمیه را زیر قفل سازمان رزرو می‌کند؛ شروع معتبر آن را یک‌بار قطعی مصرف می‌کند. دعوت‌های شروع‌نشده پس از لغو/حذف یا انقضا از سهمیه قابل‌رزرو آزاد می‌شوند. انقضا در محاسبه مانده به‌صورت منطقی اعمال می‌شود و GET برای این کار write ندارد. لغو مصاحبه شروع‌شده سهمیه مصرف‌شده را برنمی‌گرداند؛ مدت واقعی مصاحبه تغییری در مصرف یک‌واحدی ندارد. دعوت هم‌زمان بیشتر از موجودی نمی‌تواند رزرو بگیرد. ارسال مجدد، رزرو دعوت قبلیِ شروع‌نشده را اتمیک آزاد و برای دعوت تازه رزرو می‌کند؛ دعوت شروع‌شده یک مصرف قبلی دارد و دعوت جایگزین سهمیه جدا می‌خواهد. خطای ساخت جایگزین، تراکنش را برمی‌گرداند.

دعوت جدید و ارسال مجدد حداکثر۱۸۰۰ثانیه است، حتی اگر قالب تاریخی۶۰دقیقه داشته باشد. فرم جدید فقط۵تا۳۰دقیقه می‌پذیرد. جلسه‌های قدیمی قبل از این مدل، بدون رزرو جدید به‌صورت پسینی شارژ نمی‌شوند و سوابقشان حفظ است. اشتراک برای شروع دعوتِ رزروشده لازم است؛ پردازش جلسه‌ای که قبلاً شروع شده با پایان اشتراک قطع نمی‌شود.

دسترسی صوت/ویدیو جلسات جدید تا۶۰روز (snapshot اشتراک) پس از پایان واقعی یا سقف زمانی جلسه است؛ بعد از آن URLهای رسانه در گزارش حذف و endpoint رسانه404 می‌دهد. گزارش متنی، نمره و تصمیم انسانی باقی و قابل خواندن‌اند. حذف فیزیکی داده/فایل یا بازپردازش AI تاریخچه انجام نمی‌شود. رسانه قدیمی فاقد قرارداد جدید محدودیت پسینی نمی‌گیرد.

## APIها

همه پاسخ‌ها no-store هستند. شناسه سازمان در API پنل از JWT سرور است. کاتالوگ و quote عمومی؛ Current و داشبورد Owner/Recruiter؛ سفارش‌ها فقط Owner؛ admin فقط JWT مستقل SuperAdmin و سیاست SystemAdmin موجود است.

| روش و مسیر | ورودی/خروجی |
|---|---|
| GET `/api/subscriptions/catalog` | PlanDto با currency، monthlyPriceRial، baseInterviewPriceRial، extraUserMonthlyPriceRial، includedUsers، paidUserCeiling، retentionDays، maximumInterviewMinutes، rowVersion و bundles |
| POST `/api/subscriptions/catalog/quote` | PurchaseSelection → PurchaseQuote |
| GET `/api/subscriptions/current` | SubscriptionStateDto، بدون قیمت و اطلاعات مالی |
| GET `/api/dashboard` | فیلد جدید `subscription` از همان StateDto |
| POST `/api/subscriptions/orders` | CreateSubscriptionOrderRequest → سفارش Pending |
| GET `/api/subscriptions/orders?page=1&pageSize=20` | Page سفارش‌های سازمان |
| GET `/api/subscriptions/orders/{id}` | جزئیات سفارش همان سازمان؛ خارج سازمان404 |
| POST `/api/subscriptions/orders/{id}/cancel` | بدون body؛ موفق204 |
| GET/PUT `/api/admin/subscriptions/plan` | PlanDto / PlanUpdateRequest |
| PUT `/api/admin/subscriptions/bundles/{code}` | BundleUpdateRequest → PlanDto |
| GET `/api/admin/subscriptions/orders?organizationId=...&page=1&pageSize=20` | Page سراسری یا فیلتر سازمان؛ organizationId فقط ادمین |
| POST `/api/admin/subscriptions/orders/{id}/confirm` | `{ "paymentReference": "مرجع پرداخت بررسی‌شده" }` → سفارش Confirmed |

نمونه quote اشتراک: `{ "product":"subscription", "months":3, "teamUsers":5 }`.
نمونه quote بسته: `{ "product":"interviews", "bundleCode":"bina" }`؛ یا دلخواه `{ "product":"interviews", "bundleCode":"custom", "quantity":80 }`.

Quote شامل product/productName/currency/months/teamUsers/unlimitedTeam/retentionDays/bundleCode/quantity/unitPriceRial/teamMonthlyPriceRial/discountPercent/originalAmountRial/discountAmountRial/amountRial/planVersion/bundleVersion است. ثبت سفارش: `{ "requestId":"UUID تازه برای این انتخاب", "selection":{...همان انتخاب...}, "planVersion":"از quote", "bundleVersion":"از quote برای بسته؛ برای اشتراک حذف شود" }`.

State: hasActiveSubscription، startedAtUtc، expiresAtUtc، remainingDays (ceil و صفر پس از انقضا)، remainingInterviews (مجموع هدیه و خرید منهای مصرف؛ شامل رزروها)، availableInterviews (مانده منهای رزرو فعال)، reservedInterviews، consumedInterviews، teamCapacity (null برای نامحدود/بدون اشتراک)، unlimitedTeam، occupiedTeamUsers (عضو فعال + دعوت باز، شامل Owner) و isTrial (دسترسی فعلی از هدیه ثبت‌نام). برای اجازه دعوت از availableInterviews استفاده شود؛ وجود سهمیه به معنی اشتراک فعال نیست. نبود/انقضای اشتراک Current با200 و hasActiveSubscription=false می‌دهد.

BundleDto: id/code/name/minimumQuantity/maximumQuantity/quantityStep/discountPercent/startingOriginalAmountRial/startingAmountRial/isActive/sortOrder/rowVersion. تعداد ثابت وقتی min=max است؛ دلخواه را از حدود/گام DB بسازید. PlanUpdateRequest: name/monthlyPriceRial/baseInterviewPriceRial/extraUserMonthlyPriceRial/includedUsers/paidUserCeiling/retentionDays/isActive/rowVersion. BundleUpdateRequest: name/minimumQuantity/maximumQuantity/quantityStep/discountPercent/sortOrder/isActive/rowVersion. حداکثر زمان مصاحبه۳۰دقیقه است و ادمین آن را بالا نمی‌برد.

نام سازمان در همه پاسخ‌های سفارش (ایجاد، جزئیات، فهرست و تأیید، از جمله retry) با organizationName برمی‌گردد؛ نام جاری Organizations.Name است و با تغییر نام سازمان به‌روز می‌شود، نه snapshot زمان خرید. فهرست با JOIN و بدون کوئری جدا برای هر ردیف خوانده می‌شود؛ schema/migration تغییر ندارد.

OrderDto: id/organizationId/organizationName/product/productName/status/currency/amountRial/originalAmountRial/discountPercent/months/teamUsers/unlimitedTeam/bundleCode/quantity/createdAtUtc/expiresAtUtc/confirmedAtUtc. status یکی از Pending/Confirmed/Cancelled/Expired؛ Expired وضعیت محاسباتی است و GET برای آن write ندارد. Page قرارداد موجود items/total/pageNumber/pageSize را حفظ می‌کند.

خطاهای مهم:403 active_subscription_required؛409 interview_quota_exhausted، team_capacity_exceeded، pricing_changed، idempotency_conflict، payment_reference_used، order_already_confirmed، order_not_payable و concurrency_conflict. invalid_bundle_quantity و team_capacity_too_small کد400 دارند. خطای نسخه همزمانی و رقابت به کلاینت مجوز success جعلی نمی‌دهد؛ وضعیت را دوباره بخوانید.

## migration

`20261019082300_AddCountBasedSubscriptions` شش جدول SubscriptionPlans، InterviewBundles، SubscriptionOrders، OrganizationMemberships، InterviewQuotaGrants و InterviewQuotaReservations و seed اشتراک/چهاربسته را اضافه می‌کند. مبلغ‌ها bigint ریالی، درصد decimal(5,2)، زمان UTC و Guid هستند. FKهای مرکب scoped و triggerهای immutable/fulfillment جلوی تغییر قرارداد خرید و اعطای سهمیه بدون تأیید/با مقدار متفاوت را می‌گیرند. Down با تاریخچه سفارش/اشتراک/سهمیه موجود متوقف می‌شود. این migration اولیه هدیه ندارد؛ هدیه سازمان‌های موجود طبق درخواست بعدی با migration مستقل زیر اضافه می‌شود.

از ریشه ریپو، با ConnectionStrings__Default محیط تنظیم‌شده:

```powershell
dotnet tool restore
dotnet ef database update --project src/HireYar.Infrastructure --startup-project src/HireYar.Api
```

این migration روی مقصد اجرا نشده؛ push/deploy در این تسک انجام نمی‌شود. فرانت در ریپوی جداست و [پرامپت پیاده‌سازی فرانت](COUNT_BASED_SUBSCRIPTIONS_FRONTEND_PROMPT.md) قرارداد همین بک‌اند را مصرف می‌کند.

## نتیجه اعتبارسنجی

Build بدون خطا/هشدار؛ کل436تست با0Fail/0Skip روی PostgreSQL18 و FFmpeg محلی واقعی موفق‌اند. UTC ماه تقویمی در session غیرUTC، قیمت/تأیید/رقابت سهمیه و expiry/retention واقعاً آزموده شدند. EF بدون drift و diff بدون whitespace error است. TRX نهایی: `tests/HireYar.Tests/TestResults/count-subscriptions-final.trx`. migration مقصد و فرانت اجرا نشده‌اند.

## هدیه ثبت‌نام — 2026-10-19 / ADR-073

Migration افزایشی `20261019085750_AddSignupTrial` جدول `SignupTrials` را با کلید یکتای سازمان، FK سازمان، قید۴۸ساعت/۲مصاحبه و trigger منع ویرایش/حذف ایجاد می‌کند. تراکنش ثبت‌نام، حساب/سازمان/هدیه/OTP/session را یکجا commit می‌کند؛ خریدها و fulfillment قبلی مستقل و دست‌نخورده‌اند. Down با هدیه موجود متوقف می‌شود. migration روی مقصد اجرا نشده است.

`isTrial` در current و dashboard فقط وقتی دسترسی فعلی از هدیه است true می‌شود؛ پایان/remainingDays کل پوشش پیوسته شامل تمدید خریداری‌شده را نشان می‌دهند. باقی شمارنده‌ها مجموع هدیه و خرید منهای مصرف/رزرو هستند. اشتراک پولی همچنان به‌خودی‌خود مصاحبه ندارد. با پایان trial، isTrial=false و hasActiveSubscription=false می‌شود مگر دوره پولی شروع شده باشد. گزارش متنی و رسانه با قواعد قبلی حفظ می‌شوند.

اعتبارسنجی ADR-073: build کل solution صفر warning/error؛۳۴تست مرتبط متمایز آخرین نتیجه Passed و صفر Skipped دارند، روی PostgreSQL18 مستقل loopback55544 و FFmpeg واقعی محلی با OTP/AI/storage مصنوعی. اجرای signup-trial-final دارای۳۲Passed و۲Fail تست زمان بود؛ پس از اصلاح fixture زمان/JWT (بدون تغییر کد محصول)، هر۲تست در signup-trial-expiry-final با۰Fail پاس شدند. سه regression جدید هدیه یک‌باره۴۸ساعت/۲مصاحبه، مصرف/رزرو/لغو، انقضا و تمدید را پوشش می‌دهند؛ ثبت‌نام rollback و رقابت، پذیرش تیم، dashboard و migration regression نیز اجرا شدند. کل suite تکرار نشده است. EF بدون drift و git diff --check موفق؛ SQL در artifacts/signup-trial-migrations.sql، TRXها در tests/HireYar.Tests/TestResults هستند. migration مقصد، Angular، commit/push/deploy انجام نشده‌اند.

## هدیه سازمان‌های موجود و تخفیف مدت — 2026-10-19 / ADR-074

`20261019111848_GrantExistingOrganizationsMonthAndInterviews` برای هر سازمان موجود هنگام اجرای migration، یک رکورد immutable در ExistingOrganizationGrants می‌سازد:۱۰۰مصاحبه افزوده به مانده قبلی و یک ماه تقویمی UTC از max(اکنون، پایان آخرین دوره پولی، پایان trial). ماه هدیه اعتبار قبلی را کوتاه نمی‌کند. ظرفیت تیم حداقل ظرفیت قبلی/اعضای فعال و دعوت باز را حفظ می‌کند؛ ظرفیت نامحدود قبلی نیز حفظ می‌شود. تنظیمات نگهداری رسانه از آخرین دوره یا trial یا کاتالوگ snapshot می‌شوند. PK سازمان و تاریخچه EF مانع تکرارند؛ اجرای مجدد script idempotent هدیه تازه نمی‌دهد. Down با رکورد هدیه موجود متوقف می‌شود. ثبت‌نام‌های پس از migration فقط هدیه۴۸ساعت/۲مصاحبه قبلی دارند. current/dashboard به‌صورت خودکار مانده و تاریخ جدید را می‌دهند؛ isTrial برای ماه هدیه false است. سهمیه هدیه انقضا ندارد و رزرو/مصرف/رسانه همان قواعد موجود را دارند. سفارش/پرداخت صوری ایجاد نمی‌شود. این هدیه endpoint عمومی یا قابل تکرار ندارد.

`20261019112006_AddSubscriptionPeriodDiscounts` سه ستون decimal(5,2) به SubscriptionPlans اضافه و با۵/۱۰/۱۵ مقداردهی می‌کند؛ قیمت‌های سفارشی موجود حفظ و RowVersion کاتالوگ تازه می‌شود. سفارش‌های قبلی تغییر نمی‌کنند. تنظیمات از همان `PUT /api/admin/subscriptions/plan` با threeMonthDiscountPercent، sixMonthDiscountPercent و twelveMonthDiscountPercent و rowVersion مدیریت می‌شوند؛ درصد۰تا۹۹٫۹۹ با حداکثر دو رقم اعشار، فقط JWT سوپرادمین. حذف/null فیلدهای جدید در درخواست قبلی مقدار فعلی را حفظ می‌کند. ویرایش درصد RowVersion را عوض می‌کند؛ quote قبلی هنگام سفارش409/pricing_changed دارد.

کاتالوگ عمومی/ادمین PlanDto اکنون periods: [{months:1,discountPercent:0},{months:3,discountPercent:5},{months:6,discountPercent:10},{months:12,discountPercent:15}] دارد؛ مقادیر نمونه‌اند و فرانت باید پاسخ فعلی API را بخواند. quote سفارش اشتراک discountPercent/originalAmountRial/discountAmountRial/amountRial را برای مجموع اشتراک و تیم محاسبه می‌کند. قیمت پایه۹۰میلیون‌ریال با۵عضو: سه‌ماهه۲۵۶٬۵۰۰٬۰۰۰، شش‌ماهه۴۸۶٬۰۰۰٬۰۰۰ و دوازده‌ماهه۹۱۸٬۰۰۰٬۰۰۰ریال. تخفیف بسته‌های مصاحبه مستقل و بدون تغییر است.

اعتبارسنجی نهایی ADR-074: build کل solution صفر Warning/Error؛ کل۴۴۳تست با۰Failed/۰Skipped روی PostgreSQL18 مستقل loopback55548 و FFmpeg واقعی محلی موفق شدند. سه regression تازه اعطای یک‌باره هنگام upgrade از schema دارای سازمان خالی/پولی/trial، حفظ قیمت سفارشی/سابقه/نسخه، اجرای دوباره SQL و عدم هدیه۱۰۰تایی به ثبت‌نام بعدی، guard rollback، مصرف سهمیه هدیه/تمدید، درصدهای اولیه/تغییر ادمین/عدم reset با درخواست قدیمی/quote409/ثبات سفارش قبلی را پوشش دادند. providerهای OTP/AI/storage مصنوعی بودند. EF بدون pending model changes و git diff --check موفق؛ SQL ترکیبی در artifacts/commerce-gifts-periods-migrations.sql و TRX در tests/HireYar.Tests/TestResults/commerce-gifts-periods-full.trx. لاگ‌های build/full زیر artifacts/commerce-gifts-periods-* هستند. اجرای مقصد، commit/push/deploy و Angular انجام نشده‌اند.
