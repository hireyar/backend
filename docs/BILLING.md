به‌روزرسانی 2026-10-11: مدت و شارژ trial از RegistrationTrial در appsettings (DurationDays/CreditAmountUsd) خوانده می‌شوند؛ ۳ روز و ۱ دلار صرفاً پیش‌فرض‌اند. مبلغ اعشاری مانند 0.5 تا شش اعشار پشتیبانی می‌شود؛ مقدار ثبت‌شده هر grant و زمان انقضا مستقل از تنظیمات بعدی محفوظ‌اند. migration ConfigureRegistrationTrial و ADR-035 الزامات ثابت قبلی دیتابیس را جایگزین می‌کنند؛ قرارداد REGISTRATION_TRIAL_API را ببینید.

# Pricing, Subscription و AI Credit

به‌روزرسانی 2026-10-10: ثبت‌نام تازه trial سه‌روزه همه قابلیت‌های فعال عمومی و یک دلار هدیه مستقل از cash purchase دارد. کیف پول اکنون برای سازمان جدید در ثبت‌نام ایجاد می‌شود؛ lazy creation پایین همچنان برای سازمان‌های قدیمی معتبر است. OrganizationSubscription.PaymentId فقط برای IsTrial=true nullable است و قرارداد trial در RegistrationTrials/RegistrationTrialEntitlement ذخیره می‌شود. [REGISTRATION_TRIAL_API](REGISTRATION_TRIAL_API.md)، ADR-034 و migration `20261010132215_AddRegistrationTrial` مرجع‌اند. درگاه واقعی/اشتراک‌های تاریخی تغییر نکرده‌اند.

به‌روزرسانی 2026-10-05: [SUBSCRIPTION_CREDIT_API](SUBSCRIPTION_CREDIT_API.md) قرارداد صفحه جدید است: overview، quote/checkout مالیات‌دار، ارتقای روزشمار با رکورد جایگزینی مستقل، financial-history و فاکتور JSON اضافه شده‌اند. محدودیت تاریخی «proration/tax نداریم» پایین برای مسیر جدید superseded است؛ مسیرهای قبلی و محاسبه مدت/اعتبار حفظ‌اند. دوره‌های seed دست‌نخورده مطابق HTML تخفیف ۵٪ سه‌ماهه و ۱۰٪ شش‌ماهه می‌گیرند؛ کاتالوگ ویرایش‌شده و snapshot خرید قبلی overwrite نمی‌شوند. نسخه production/درگاه واقعی همچنان آماده نیست.

مرجع قرارداد بک‌اند روی `codex/pricing`، 2026-10-01. این برنچ ابتدا با fast-forward از `origin/dev` در commit `d9d4d72` به‌روز شد؛ ماژول مصاحبهٔ موجود از همین پایه آمده است. Angular یا solution جدید ساخته نشد. پرداخت واقعی هنوز فعال نیست.

## معماری و کاتالوگ

Controller → Application Service → IApplicationDbContext؛ Domain مستقل است. SQL، Npgsql، قفل و تنظیم query فقط در Infrastructure هستند. Repository، MediatR، CQRS و worker جدید نداریم. همه شناسه‌ها Guid، زمان‌ها UTC و عملیات‌ها CancellationToken دارند.

`PricingPackage`، `PackagePeriod`، `FeatureDefinition` و `PackageFeature` کاتالوگ قابل ویرایش‌اند. برنامه بر اساس کد پکیج تصمیم نمی‌گیرد؛ Basic/Advanced و قیمت‌های اولیه فقط در seed یک‌بارهٔ migration تعریف شده‌اند. دوره‌ها مستقل و قابل تنظیم ۱ تا ۱۲۰ ماه‌اند؛ مبلغ دوره قیمت نهایی و authoritative است، نه حاصل محاسبهٔ فرانت‌اند یا درصد تخفیف نمایشی. نگهداری رسانه فقط `InterviewRetentionDays` است؛ تعریف feature مستقل با کد `interview_retention_days` ممنوع است.

پکیج عمومی باید active/public و دارای حداقل یک دورهٔ active باشد. فقط دوره‌ها و featureهای فعال در پاسخ عمومی عرضه می‌شوند. پکیج/دوره/محصول اعتبار حذف نمی‌شود؛ با `IsActive=false` آرشیو می‌شود. PUT پکیج، دوره و اتصال feature از RowVersion پکیج استفاده می‌کنند؛ دورهٔ تازه و feature تازه صریحاً Added می‌شوند. کد و DataType تعریف feature ثابت‌اند؛ نام و شرح قابل ویرایش‌اند. feature Boolean مقدار مستقل ندارد؛ Integer و Text مقدار معتبر می‌خواهند.

`PricingCatalog` در IApplicationDbContext یک IQueryable با سیاست خواندن تک‌statement در Infrastructure است؛ این تنظیم، split-query عمومی پروژه را فقط برای خواندن قرارداد پکیج override می‌کند. همه اجزای quote از یک snapshot PostgreSQL می‌آیند؛ ویرایش هم‌زمان مدیر نمی‌تواند قیمت یک نسخه را با قابلیت نسخهٔ دیگری ترکیب کند.

## قرارداد خرید و اشتراک

ایجاد checkout، یک Payment Pending و `SubscriptionPurchase` immutable با snapshot نام/کد، دوره، مبلغ IRT، تخفیف، نرخ USD، retention و `SubscriptionEntitlement`های فعال ایجاد می‌کند. درگاه مبلغ و ارز همین quote را تأیید می‌کند؛ ویرایش یا آرشیو کاتالوگ بعد از checkout، قرارداد pending یا paid را تغییر نمی‌دهد. `CreatedAtUtc` زمان quote است؛ زمان پرداخت واقعی در `Payment.PaidAtUtc` ذخیره می‌شود. quote به معنی خرید موفق نیست.

پس از verification معتبر، `OrganizationSubscription` با PaymentId یکتا ایجاد می‌شود. شروع از زمان پرداخت یا انتهای آخرین اشتراک فعال/زمان‌بندی‌شدهٔ همان سازمان است. پایان با `AddMonths` تقویمی UTC محاسبه می‌شود؛ زمان پرداخت‌شده از بین نمی‌رود و صف اشتراک‌ها هم‌پوشانی ندارد. trigger دیتابیس با قفل سازمان هم‌پوشانی را رد می‌کند.

سطر اشتراک تاریخچهٔ ثابت است؛ وضعیت مؤثر Active/Scheduled/Expired از بازهٔ UTC خوانده می‌شود، بدون worker برای تغییر روزانهٔ status. سرویس entitlement، Scheduled دارای بازهٔ جاری را نیز Active می‌داند. نسخه اول مسیر تغییر/لغو/تعلیق اشتراک پرداخت‌شده یا proration ندارد؛ enumهای مربوط مرز توسعهٔ آینده‌اند. برای inspection جاری/صف آینده و تاریخچهٔ خرید API وجود دارد.

`ISubscriptionEntitlementService` مرجع active subscription، ویژگی‌ها، نرخ، retention و expiry است. invite و start قابلیت موردنیاز و `ai_interview` را بررسی می‌کنند. `requiredCapabilityCode` در invite برابر `hr_interview` یا `technical_interview` است؛ پیش‌فرض technical برای حفظ ماهیت مصاحبهٔ فنی موجود. نوع قالب HR/فنی باید در فرم محصول درست انتخاب شود؛ محتوای آزاد بانک سؤال طبقه‌بندی خودکار نمی‌شود.

## کیف پول، ledger و رزرو

هر سازمان یک `OrganizationAiWallet` با ارز USD دارد. کیف پول به صورت lazy و زیر قفل سازمان ساخته می‌شود. `Balance` cache جمع ledger است؛ `AvailableCredit = Balance - Sum(ActiveReservations)`. خواندن کیف پول نیز تراکنشی است تا balance و reserved از دو لحظه متفاوت نیایند.

تمام تغییرات موجودی سطر جدید `AiCreditTransaction` با Amount signed، BalanceBefore/After، نوع، ReferenceType/Id و UTC دارند. خرید اعتبار مثبت، usage منفی و اصلاح دستی compensating entry است. Amountهای داخلی تا شش رقم اعشار پذیرفته می‌شوند؛ ارقام اضافه پیش از ذخیره رد می‌شوند. API اصلاح مدیر، دلیل و IdempotencyKey می‌خواهد. استفادهٔ دوباره از همان key با مقدار/دلیل متفاوت 409 است. Enumهای Refund/Promotion/Correction برای مرز توسعه موجودند؛ refund provider یا promo API در این نسخه نداریم.

Application guard و trigger PostgreSQL update/delete ledger و purchase/entitlement را رد می‌کنند. paid payment و settled billing نیز immutable‌اند؛ حذف تاریخچه و روابط مالی Restrict است. constraint triggerهای deferred در commit، موجودی کیف پول را با جمع ledger و سقف رزرو کنترل می‌کنند؛ دست‌کاری موجودی بدون ledger رد می‌شود.

تمام mutationهای مالی سازمان از `WithOrganizationBillingLockAsync` عبور می‌کنند: Read Committed transaction و `SELECT ... FOR UPDATE` روی Organizations. قفل سازمان پیش از query کیف پول، reservation یا fulfillment گرفته و تا commit حفظ می‌شود؛ حتی رقابت برای ساخت کیف پول صفر را پوشش می‌دهد. wallet unique OrganizationId، transaction unique Organization/ReferenceType/ReferenceId، reservation و billing unique InterviewSessionId هستند. رابطهٔ مالی بین سازمان‌ها در FKهای مرکب نیز ممنوع است.

در start، کل `MaxDurationSeconds / 60 × RateSnapshot` رزرو می‌شود، بدون کم‌کردن Balance. start جلسه، تغییر وضعیت application، rate/retention snapshot، billing و reservation در یک تراکنش‌اند؛ پس از commit TTS اجرا می‌شود. credit ناکافی `insufficient_ai_credit` می‌دهد و AI-paid processing شروع نمی‌شود؛ جزئیات مالی سازمان به candidate عرضه نمی‌شوند.

completion خودکار و دستی، settlement و ledger را اتمیک ذخیره می‌کنند. هزینهٔ واقعی از reservation مصرف و بقیه آزاد می‌شود. settlement و completion تکراری دوباره شارژ نمی‌کنند. revoke جلسهٔ غیرنهایی را cancel می‌کند و کل reservation را بدون usage charge آزاد می‌کند؛ سطر صفر ledger نیز اثر تصمیم مالی را ثبت می‌کند. اگر قبلاً settlement شده، revoke هزینهٔ گذشته را تغییر نمی‌دهد.

رزرو تا حداقلِ expiry دعوت و start+MaxDurationSeconds معتبر است. خواندن کیف پول، اصلاح مدیر یا start بعدی، رزروهای منقضی را با پاسخ‌های ثبت‌شده تسویه می‌کند. رهاشدهٔ بدون پاسخ هزینه صفر دارد؛ پاسخ پذیرفته‌شده با STT ناموفق نیز window ثابت خود را دارد و retry زمان مالی تازه نمی‌سازد. worker یا cleanup timer اضافه نشده؛ GET کیف پول مدیر نیز cleanup سازمان را فعال می‌کند. reservation منقضی که هنوز lazy cleanup ندیده، محافظه‌کارانه reserved باقی می‌ماند.

## قاعدهٔ مدت و گردکردن

مدت مالی نسخه اول **window پاسخ‌گویی مشاهده‌شده در سرور** است؛ طول decodeشدهٔ صوت یا مدت باز بودن صفحه نیست:

1. پس از آماده‌شدن متن/تلاش TTS، `QuestionReadyAtUtc` یک‌بار persist می‌شود.
2. هنگام رسیدن پاسخ معتبر، پیش از validation/storage/STT/LLM، timestamp سرور گرفته می‌شود.
3. BillableSeconds هر پاسخ = floor فاصلهٔ این دو زمان، محدود به ۱۲۰ ثانیه و زمان باقی‌ماندهٔ جلسه در هنگام آماده‌شدن سؤال. idle/disconnect فراتر از ۱۲۰ ثانیه حذف می‌شود.
4. جلسه مجموع این windowها را تا MaxDurationSeconds جمع می‌کند. سؤال بی‌پاسخ، completion، processing latency، TTS و provider retry window تازه ندارند. DurationSeconds ارسالی client تنها validation کمکی صوت است.

این قاعده عمداً محدود و قطعی است؛ server timestamp در window نخست نمی‌تواند مکث واقعی را از ضبط صوت تشخیص دهد. قرارداد محصول همین window محدود است، نه ادعای اندازه‌گیری زمان دقیق صحبت‌کردن. decoder قابل اعتماد یا capture/heartbeat protocol می‌تواند در تسک جدا این اندازه‌گیری را دقیق‌تر کند؛ استفاده از ادعای duration کلاینت به عنوان پول ممنوع است.

`Cost = decimal.Round(BillableSeconds / 60m × RateSnapshot, 6, MidpointRounding.ToEven)`؛ فقط مبلغ نهایی گرد می‌شود. USD accounting برابر numeric(18,6)، IRT برابر numeric(18,0)، discount numeric(18,2)، نسبت مؤثر USD/IRT برابر numeric(24,6) است. مثال‌ها: ۴۲ دقیقه با ۰٫۰۳ = ۱٫۲۶؛ با ۰٫۰۵ = ۲٫۱۰؛ ۲۵۴۰ ثانیه با ۰٫۰۵ = ۲٫۱۱۶۶۶۷ دلار. این‌ها تست تابع محاسبه‌اند؛ سقف window هر پاسخ در محاسبهٔ session اعمال می‌شود.

## اتصال به مصاحبه و retention

نرخ و retention در `InterviewBilling` در start ثابت می‌شوند. قابلیت‌های پردازش از همان purchase immutable می‌آیند؛ expiry اشتراک هنگام مصاحبه یا ویرایش پکیج، rate/contract را عوض نمی‌کند. پاسخ/جلسه lease و optimistic concurrency قبلی را حفظ می‌کنند؛ تراکنش هنگام STT/LLM/TTS باز نیست.

قابلیت `answer_analysis` اجازهٔ ثبت evidence تحلیلی، `adaptive_questions` اجازهٔ انتخاب LLM، `adaptive_difficulty` اجازهٔ سختی تطبیقی و `avoid_repeated_topics` اجازهٔ coverage را می‌دهد. بدون analysis/adaptive_questions، گردش HR از STT و fallback قطعی استفاده می‌کند؛ Advanced همهٔ قابلیت‌های اولیه را دارد. skill/profile/report metadata محصول از همین کاتالوگ قابل عرضه است؛ transcript و شواهد تاریخی در گزارش HR همچنان قابل بازبینی‌اند و حذف نمی‌شوند.

دانلود خصوصی صوت سؤال/پاسخ از expiry retention snapshot به بعد 404 می‌دهد. مبنای شروع retention، CompletedAtUtc است ولی به حداقلِ start+MaxDurationSeconds و expiry دعوت محدود می‌شود؛ جلسهٔ پایان‌نیافته نیز همین پایان سخت را مبنا می‌گیرد. لغو دیررس نمی‌تواند رسانهٔ منقضی را دوباره قابل دانلود کند. متون گزارش و ledger باقی می‌مانند. حذف فیزیکی فایل‌های expired و orphan، مدیریت shared TTS cache، volume و backup وظیفهٔ عملیاتی جداست؛ این نسخه به معنی حذف خودکار فایل نیست. media قدیمی فاقد قرارداد billing، retention تازهٔ عطف‌به‌ماسبق نمی‌گیرد.

جلسهٔ Created پیش از migration می‌تواند پس از خرید اشتراک/اعتبار شروع و snapshot بگیرد. جلسهٔ started قدیمی بدون billing snapshot اجازهٔ paid processing جدید ندارد؛ می‌توان آن را تمام/لغو کرد یا برای ادامه دعوت تازه ساخت. هیچ شارژ عطف‌به‌ماسبق اضافه نمی‌شود. پیش از rollout بهتر است جلسه‌های legacy فعال پایان یابند.

## ارز و پرداخت

Subscription و فروش اعتبار به **تومان مستقیم با کد IRT** هستند؛ IRR نیستند. موجودی AI همیشه USD است. هر `AiCreditPackage` مبلغ فروش IRT و مقدار اعتبار USD صریح دارد؛ نسبت `SellPriceAmount / CreditAmountUsd` در payment snapshot می‌شود. این نسبت effective contract ratio یک محصول با قیمت ثابت است، نه نرخ لحظه‌ای forex؛ مبلغ پرداخت مستقیماً از SellPriceAmount می‌آید. rate service، نرخ جهانی یا arbitrary USD purchase در این مرحله لازم نیست و ساخته نشد. محصولات اعتباری اولیه seed نشده‌اند؛ مدیر باید قیمت و مقدار مشخص ایجاد کند.

`IPaymentGateway` در Application، implementation در Infrastructure؛ payload شامل PaymentId، Amount و Currency سرور است. adapter واقعی باید PaymentId را کلید idempotency ایجاد checkout قرار دهد، authority را محرمانه نگه دارد، Amount/Currency/TransactionId معتبر را پس از verification مستقیم برگرداند و recovery timeout را پشتیبانی کند. network قبل از transaction fulfillment است. unique Provider/TransactionId و Provider/Authority، PaymentId unique subscription و ledger reference، callback تکراری را یک‌بار fulfill می‌کنند.

Callback فقط paymentId را اعلام می‌کند؛ هیچ success/amount/authority از client قبول نمی‌شود. سرور authority ذخیره‌شده را مستقیم verify می‌کند و callback به جای مالی/اطلاعات tenant، 204 می‌دهد. verification ناموفق یا mismatch به Failed می‌رود و مالی fulfill نمی‌شود؛ خطای transport adapter نباید به Paid تبدیل شود. verification موفق دیررس، حتی بعد از checkout expiry سی‌دقیقه‌ای، پول تأییدشده را fulfill می‌کند؛ ExpiresAtUtc در این نسخه مهلت checkout است و job تغییر status وجود ندارد. cancel/expire/refund/reconciliation درگاه واقعی باید با قرارداد adapter تکمیل شوند.

Runtime همیشه `DisabledPaymentGateway` دارد؛ هیچ success جعلی در Development/Production نیست. purchase یک pending record با gatewayEnabled=false و redirectUrl=null می‌دهد، verify/callback برابر 503 و بدون تغییر مالی است. FakePaymentGateway فقط در tests ثبت می‌شود. provider واقعی یا production payment readiness ادعا نمی‌شود؛ تست عادی درگاه یا AI واقعی را صدا نمی‌زند.

## API و مجوز

| مرز | endpoint |
|---|---|
| عمومی | GET `/api/pricing/packages`؛ GET `/api/pricing/ai-credit-packages` |
| Owner/Recruiter | GET `/api/billing/subscription`، `/ai-credit`، `/ai-credit/transactions`، `/ai-usage`، `/payments`، `/payments/{id}`، `/purchases` |
| فقط Owner سازمان | POST `/api/billing/subscription/purchases`، `/ai-credit/purchases`، `/payments/{id}/verify` |
| callback بدون افشای اطلاعات | POST `/api/payment-callbacks/{paymentId}`؛ تأیید مستقل provider الزامی |
| فقط SystemAdmin | GET/POST packages؛ GET/PUT packages/{id}؛ POST/PUT دوره؛ GET/POST/PUT feature definition؛ PUT اتصال feature؛ GET/POST/PUT credit product در `/api/admin/pricing` |
| فقط SystemAdmin | GET `/api/admin/pricing/organizations/{org}/subscription` و `/ai-credit`؛ POST `/ai-credit/adjustments` |

list تراکنش، usage، payment و purchase صفحه‌بندی استاندارد PageQuery دارند (۲۰ پیش‌فرض، سقف ۱۰۰). subscription overview current و حداکثر ۱۰۰ آیتم صف آینده را نشان می‌دهد. شناسهٔ سازمان از JWT بررسی‌شدهٔ سرور است؛ payment دیگران 404. توکن Candidate، حتی برای همان سازمان، مجوز finance/admin ندارد.

`ApplicationUser.IsSystemAdmin` پیش‌فرض false و از register/profile DTO قابل نوشتن نیست. از ADR-039، سیاست SystemAdmin فقط توکن ورود مستقل موبایل/رمز/CAPTCHA با audience/scheme سوپرادمین را می‌پذیرد؛ توکن OTP حساب دارای این flag هم مجوز admin API ندارد. active/مجوز/stamp/lockout از DB هر درخواست بررسی می‌شوند؛ provisioning/revocation فقط operator خارج APIهای کاربر، خروج همه‌نشست‌های ادمین از endpoint اختصاصی است. [SUPER_ADMIN_API](SUPER_ADMIN_API.md) قرارداد کلید مسیر و مدیریت پکیج است. RateLimiter جاری پیش از authentication/authorization و با IP اتصال اجرا می‌شود؛ توصیف تاریخی partition پس از auth دیگر وضعیت جاری نیست.

token/authority، gateway secret، AI key، candidate token، request body و دادهٔ callback در لاگ ثبت نمی‌شوند. Meter `HireYar.Billing` counterهای purchase subscription/credit، reservation created/failed، USD consumed و verification failed دارد؛ هیچ entity ID label ندارد. رخدادهای مالی عادی پس از commit شمرده می‌شوند؛ lazy expiry accounting در ledger ثبت می‌شود ولی مصرف آن وارد counter عادی settlement نمی‌شود.

## نمونه JSON

نمونه‌ها کوتاه‌شده‌اند؛ در عمل تمام دوره‌ها و featureهای فعال بازمی‌گردند. GUID خرید اعتبار مثال است و باید از کاتالوگ عمومی واقعی برداشته شود؛ محصول اعتبار seed ندارد. RowVersion و شناسهٔ subscription/payment نیز در مثال نمایشی‌اند.

GET `/api/pricing/packages`:

```json
{"packages":[{"id":"bd000000-0000-0000-0000-000000000001","code":"basic","name":"پکیج پایه","description":"مصاحبه عمومی و غربالگری اولیه","isActive":true,"isPublic":true,"sortOrder":1,"aiPricePerMinuteUsd":0.03,"retentionDays":30,"rowVersion":"AQEBAQEBAQEBAQEBAQEBAQ==","features":[{"id":"11111111-1111-1111-1111-111111111111","code":"hr_interview","name":"مصاحبه عمومی HR","description":"","dataType":"Boolean","isEnabled":true,"value":null}],"periods":[{"id":"82487cde-6cc1-4996-ce4b-3384a394346a","months":1,"price":3000000,"currency":"IRT","discountPercent":0,"isActive":true,"sortOrder":1}]}]}
```

GET `/api/billing/subscription`:

```json
{"current":{"id":"22222222-2222-2222-2222-222222222222","packageCode":"basic","packageName":"پکیج پایه","status":"Active","startedAtUtc":"2026-10-01T08:00:00+00:00","expiresAtUtc":"2026-11-01T08:00:00+00:00","aiRatePerMinuteUsd":0.03,"retentionDays":30,"features":[{"code":"hr_interview","name":"مصاحبه عمومی HR","dataType":"Boolean","value":null}]} ,"scheduled":[]}
```

GET `/api/billing/ai-credit`:

```json
{"currency":"USD","balance":25.000000,"reservedCredit":3.000000,"availableCredit":22.000000}
```

POST `/api/billing/subscription/purchases` (request):

```json
{"packagePeriodId":"82487cde-6cc1-4996-ce4b-3384a394346a"}
```

Response با درگاه پیش‌فرض غیرفعال:

```json
{"payment":{"id":"33333333-3333-3333-3333-333333333333","type":"Subscription","amount":3000000,"currency":"IRT","status":"Pending","provider":"Disabled","createdAtUtc":"2026-10-01T08:00:00+00:00","paidAtUtc":null,"expiresAtUtc":"2026-10-01T08:30:00+00:00"},"redirectUrl":null,"gatewayEnabled":false}
```

POST `/api/billing/ai-credit/purchases` (request):

```json
{"creditPackageId":"44444444-4444-4444-4444-444444444444"}
```

Response نمونه برای محصول مدیریتی با ۱۰ دلار اعتبار و قیمت ۱۲۳۴۵۶ تومان:

```json
{"payment":{"id":"55555555-5555-5555-5555-555555555555","type":"AiCredit","amount":123456,"currency":"IRT","status":"Pending","provider":"Disabled","createdAtUtc":"2026-10-01T08:00:00+00:00","paidAtUtc":null,"expiresAtUtc":"2026-10-01T08:30:00+00:00"},"redirectUrl":null,"gatewayEnabled":false}
```

## migration، تست و عملیات

`20261001080208_AddPricingBilling` migration PostgreSQL واقعی است؛ migrationهای قبلی دست‌نخورده‌اند. جدول‌های جدید: PricingPackages، PackagePeriods، FeatureDefinitions، PackageFeatures، AiCreditPackages، Payments، SubscriptionPurchases، SubscriptionEntitlement، OrganizationSubscriptions، OrganizationAiWallets، AiCreditTransactions، AiCreditReservations و InterviewBillings. فیلدهای جدید جلسه/پاسخ و IsSystemAdmin با backfill امن اضافه می‌شوند. default capability جلسات قدیمی technical_interview و BillableSeconds قبلی صفر است.

seed از SQL idempotent همان migration با ON CONFLICT DO NOTHING استفاده می‌کند؛ در startup هیچ کاتالوگی overwrite نمی‌شود. triggerهای مالی/عدم هم‌پوشانی/تطابق ledger در همین migration هستند. Down برای دیتابیس آزمایشی است؛ برای دادهٔ مالی واقعی rollback به نسخهٔ بدون billing مجاز تلقی نمی‌شود. تمام FKهای مالی Restrict و tenant-owned relations مرکب‌اند. pending-model-change validation و اعمال دوبارهٔ script idempotent روی DB مستقل در STATUS ثبت شده‌اند.

تست‌های math، package/period/feature/snapshot، payment pending/failed/duplicate callback/duplicate provider transaction، ledger/adjustment، رزرو هم‌زمان و settlement، expiry اشتراک و رزرو، retention، tenant FK و candidate authorization از PostgreSQL واقعی استفاده می‌کنند. fixture دیتابیس تصادفی محلی می‌سازد و همان را حذف می‌کند. provider جعلی فقط در tests است. نتیجهٔ نهایی و شمارش در STATUS/HANDOFF ثبت می‌شود.

عملیات لازم قبل از پرداخت واقعی: پیاده‌سازی و verification/recovery/reconciliation adapter درگاه، provisioning مدیر مورد اعتماد، ایجاد محصولات اعتبار با قیمت صریح، خرید/اعتبار اولیهٔ سازمان‌ها، پایان جلسات legacy، backup و سیاست حذف رسانه، و rotation کلید AI قدیمیِ ثبت‌شده در Git. کلید default در checkout دریافتی با یادداشت audit مبنی بر خالی‌بودن آن اختلاف داشت؛ این نوبت default دوباره خالی شد، بدون تغییر secret محلی یا فراخوانی provider.

خارج از این نسخه: Angular، invoice PDF، tax، coupon/promo engine، شارژ خودکار کارت، proration، forex زنده، refund provider و حسابداری خارجی. این موارد backlog مستقل می‌خواهند.
