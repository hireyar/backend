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

مدت اشتراک۱/۳/۶/۱۲ماه و بدون تخفیف مدت است. ماه تقویمی UTC با AddMonths محاسبه می‌شود. پنج کاربر شامل Owner رایگان‌اند؛ از نفر۶ تا۳۰ هر نفر ماهانه2,000,000ریال اضافه می‌شود. تعداد بالاتر از۳۰ هزینه بیشتر ندارد؛ با انتخاب۳۰ یا بیشتر ظرفیت نامحدود می‌شود. مبلغ اشتراک = ماه‌ها × (قیمت ماهانه + min(تعداد انتخابی،۳۰) منهای۵، ضرب‌در قیمت کاربر اضافه). قیمت ثابت/حدود از کاتالوگ جاری سرور می‌آیند، نه hardcode فرانت.

## چرخه خرید و اشتراک

ثبت‌نام هدیه/اشتراک/سهمیه خودکار نمی‌دهد. ثبت سفارش فقط Owner است؛ وضعیت Pending هیچ دسترسی یا سهمیه نمی‌دهد. فعلاً پرداخت درگاه وجود ندارد: سوپرادمین پس از بررسی پرداخت با مرجع پرداخت غیرخالی و یکتا سفارش را تأیید می‌کند. تأیید با توکن سازمان یا client-side پرداخت موفق قابل جعل نیست. سفارش۲۴ساعت اعتبار دارد؛ لغوشده/منقضی قابل تأیید نیست. مرجع پرداخت اطلاعات کارت/secret/لینک پرداخت نیست و نباید داده حساس در آن فرستاد.

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

State: hasActiveSubscription، startedAtUtc، expiresAtUtc، remainingDays (ceil و صفر پس از انقضا)، remainingInterviews (کل خرید منهای مصرف؛ شامل رزروها)، availableInterviews (مانده منهای رزرو فعال)، reservedInterviews، consumedInterviews، teamCapacity (null برای نامحدود/بدون اشتراک)، unlimitedTeam و occupiedTeamUsers (عضو فعال + دعوت باز، شامل Owner). برای اجازه دعوت از availableInterviews استفاده شود؛ وجود سهمیه به معنی اشتراک فعال نیست. نبود/انقضای اشتراک Current با200 و hasActiveSubscription=false می‌دهد.

BundleDto: id/code/name/minimumQuantity/maximumQuantity/quantityStep/discountPercent/startingOriginalAmountRial/startingAmountRial/isActive/sortOrder/rowVersion. تعداد ثابت وقتی min=max است؛ دلخواه را از حدود/گام DB بسازید. PlanUpdateRequest: name/monthlyPriceRial/baseInterviewPriceRial/extraUserMonthlyPriceRial/includedUsers/paidUserCeiling/retentionDays/isActive/rowVersion. BundleUpdateRequest: name/minimumQuantity/maximumQuantity/quantityStep/discountPercent/sortOrder/isActive/rowVersion. حداکثر زمان مصاحبه۳۰دقیقه است و ادمین آن را بالا نمی‌برد.

OrderDto: id/organizationId/product/productName/status/currency/amountRial/originalAmountRial/discountPercent/months/teamUsers/unlimitedTeam/bundleCode/quantity/createdAtUtc/expiresAtUtc/confirmedAtUtc. status یکی از Pending/Confirmed/Cancelled/Expired؛ Expired وضعیت محاسباتی است و GET برای آن write ندارد. Page قرارداد موجود items/total/pageNumber/pageSize را حفظ می‌کند.

خطاهای مهم:403 active_subscription_required؛409 interview_quota_exhausted، team_capacity_exceeded، pricing_changed، idempotency_conflict، payment_reference_used، order_already_confirmed، order_not_payable و concurrency_conflict. invalid_bundle_quantity و team_capacity_too_small کد400 دارند. خطای نسخه همزمانی و رقابت به کلاینت مجوز success جعلی نمی‌دهد؛ وضعیت را دوباره بخوانید.

## migration

`20261019082300_AddCountBasedSubscriptions` شش جدول SubscriptionPlans، InterviewBundles، SubscriptionOrders، OrganizationMemberships، InterviewQuotaGrants و InterviewQuotaReservations و seed اشتراک/چهاربسته را اضافه می‌کند. مبلغ‌ها bigint ریالی، درصد decimal(5,2)، زمان UTC و Guid هستند. FKهای مرکب scoped و triggerهای immutable/fulfillment جلوی تغییر قرارداد خرید و اعطای سهمیه بدون تأیید/با مقدار متفاوت را می‌گیرند. Down با تاریخچه سفارش/اشتراک/سهمیه موجود متوقف می‌شود. هیچ هدیه، خرید یا انتقال سهمیه‌ای برای سازمان‌های قبلی حدس زده نمی‌شود.

از ریشه ریپو، با ConnectionStrings__Default محیط تنظیم‌شده:

```powershell
dotnet tool restore
dotnet ef database update --project src/HireYar.Infrastructure --startup-project src/HireYar.Api
```

این migration روی مقصد اجرا نشده؛ push/deploy در این تسک انجام نمی‌شود. فرانت در ریپوی جداست و [پرامپت پیاده‌سازی فرانت](COUNT_BASED_SUBSCRIPTIONS_FRONTEND_PROMPT.md) قرارداد همین بک‌اند را مصرف می‌کند.

## نتیجه اعتبارسنجی

Build بدون خطا/هشدار؛ کل436تست با0Fail/0Skip روی PostgreSQL18 و FFmpeg محلی واقعی موفق‌اند. UTC ماه تقویمی در session غیرUTC، قیمت/تأیید/رقابت سهمیه و expiry/retention واقعاً آزموده شدند. EF بدون drift و diff بدون whitespace error است. TRX نهایی: `tests/HireYar.Tests/TestResults/count-subscriptions-final.trx`. migration مقصد و فرانت اجرا نشده‌اند.
