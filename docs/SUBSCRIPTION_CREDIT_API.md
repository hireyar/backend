# قرارداد صفحه اشتراک و اعتبار — 2026-10-05

مرجع طراحی `subscription_credit.html` ارسالی کاربر است. بک‌اند روی `codex/subscription-credit-panel` از `panel-wizard` توسعه یافته؛ Angular در این ریپو نیست. همه APIهای سازمان با Bearer معتبر Owner/Recruiter و scope سازمان جاری‌اند؛ خرید و verify فقط Owner. شناسه سازمان، قیمت، نرخ ارز، tax یا موفقیت پرداخت از کلاینت پذیرفته نمی‌شود. تاریخ‌ها UTC؛ فرانت نمایش شمسی را انجام دهد. عنوان‌های Swagger فارسی‌اند.

## ۱. وضعیت اشتراک و کارت اعتبار

`GET /api/billing/panel`:

| فیلد | کاربرد |
|---|---|
| updatedAtUtc | زمان پاسخ سرور برای «آخرین بروزرسانی» |
| subscriptionState | active / scheduled / expired / none |
| current | اشتراک جاری، نام پکیج snapshot، تاریخ شروع/انقضا، rate، retention و features |
| lastExpired | آخرین اشتراک منقضی‌شده وقتی اشتراک جاری نداریم |
| scheduled | تمدیدهای صف‌شده؛ زمان خریدشده حذف نمی‌شود |
| daysUntilExpiry | روزهای باقی‌مانده به بالا گرد می‌شود؛ expired برابر صفر، بدون پکیج null |
| wallet | currency=USD، balance، reservedCredit، availableCredit |
| aiRatePerMinuteUsd | نرخ قرارداد اشتراک جاری؛ بدون اشتراک جاری null |
| estimatedAvailableMinutes | floor(availableCredit ÷ rate × 60) ÷ 60؛ دقت ثانیه، تخمین زمان قابل استفاده |
| canPurchase, gatewayEnabled | کنترل دکمه خرید؛ recruiter خرید نمی‌کند؛ درگاه runtime فعلاً Disabled |

کارت اعتبار در تب اول و سوم از همین داده استفاده کند. در مثال ۵۰ دلار با نرخ ۰٫۰۳، مدت تخمینی ۱۶۶۶٫۶۶۶۶ دقیقه است؛ UI می‌تواند تقریباً ۲۷ ساعت و ۴۷ دقیقه نمایش دهد. مقدار مبنای تخمین **اعتبار آزاد پس از رزرو** است؛ موجودی رزروشده دوباره قابل مصرف نیست. بدون اشتراک، موجودی باقی می‌ماند ولی نرخ/زمان null است؛ UI درخواست خرید پکیج بدهد. متن HTML «اعتبار غیرقابل استفاده» توضیح قابل اتکایی برای موجودی فعال نیست.

دکمه بروزرسانی همان GET را تکرار کند. نرخ/قابلیت از قرارداد خرید ثابت می‌آید، نه کاتالوگ ویرایش‌شده امروز. read مرکب زیر قفل سازمان، با cleanup lazy رزروهای منقضی انجام می‌شود.

## ۲. انتخاب پکیج و خلاصه خرید

کارت‌ها و dropdown دوره/امکانات: `GET /api/pricing/packages`. داده پویا است؛ دو پکیج در فرانت hardcode نشوند. `retentionDays` و `aiPricePerMinuteUsd` کنار features نمایش داده شوند. دوره active شامل Price نهایی **بعد از تخفیف و قبل از مالیات پنل** است؛ تخفیف را دوباره کم نکنید.

دوره‌های seed دست‌نخورده مطابق HTML به تخفیف ۰/۵/۱۰/۲۰ درصد برای ۱/۳/۶/۱۲ ماه تطبیق یافته‌اند. قیمت قبل از tax:

| دوره | پایه (IRT) | پیشرفته (IRT) |
|---|---:|---:|
| ۱ ماه | 3,000,000 | 4,500,000 |
| ۳ ماه | 8,550,000 | 12,825,000 |
| ۶ ماه | 16,200,000 | 24,300,000 |
| ۱۲ ماه | 28,800,000 | 43,200,000 |

Migration فقط دوره‌های seed با قیمت/تخفیف قبلی دست‌نخورده را تغییر می‌دهد؛ تنظیم مدیر و pending/paid snapshot حفظ می‌شوند. برای پکیج/دوره سفارشی همان قیمت و درصد مدیر authoritative است. originalAmount از قیمت نهایی و درصد تخفیف برآورد و به تومان گرد می‌شود؛ اگر درصد ۱۰۰ با قیمت مثبت ثبت شده باشد، originalAmount/discountAmount null خواهند بود.

پیش‌نمایش: `POST /api/billing/subscription/quote`:

```json
{ "packagePeriodId": "00000000-0000-0000-0000-000000000001", "operation": "purchase" }
```

operation: `purchase` برای خرید، `renewal` تمدید همان پکیج، `upgrade` ارتقای فوری. پاسخ CheckoutSummaryDto: نام و دوره، currency=IRT، originalAmount، discountAmount/Percent، subtotalAmount، proratedCreditAmount، taxPercent/Amount، totalAmount، تاریخ شروع/پایان تخمینی، replacesSubscriptionId، rate، retention، features و quoteFingerprint. quote رکورد پرداخت نمی‌سازد.

`subtotalAmount` قیمت تخفیف‌خورده کامل دوره است. `taxable = subtotalAmount - proratedCreditAmount`؛ `taxAmount = Round(taxable × taxPercent / 100, 0, AwayFromZero)`؛ `totalAmount = taxable + taxAmount`. tax طراحی پیش‌فرض ۱۰ درصد و از `BillingPanel:SubscriptionTaxPercent` یا `BillingPanel__SubscriptionTaxPercent` قابل تغییر ۰..۱۰۰ با دو رقم اعشار است. این تنظیم فقط checkout جدید پنل را کنترل می‌کند؛ به پرداخت قدیمی مالیات اضافه نمی‌شود. برای پایه یک‌ماهه subtotal=3,000,000، tax=300,000، total=3,300,000.

ادامه پرداخت: `POST /api/billing/subscription/checkout` (فقط Owner)، همان شناسه و operation همراه `quoteFingerprint` پاسخ پیش‌نمایش. پاسخ `{ "checkout": PurchaseResult, "summary": CheckoutSummaryDto }` است. ابتدا summary را با UI تطبیق دهید؛ redirectUrl فقط از gateway می‌آید. fingerprint قیمت authoritative نیست؛ سرور دوباره محاسبه می‌کند و تغییر کاتالوگ، tax، صف یا اعتبار روزشمار نسبت به پیش‌نمایش، `409 quote_changed` می‌دهد. در این حالت quote را دوباره نمایش دهید. fingerprint خالی `400 quote_required` است. اعتبارسنجی قیمت ارسالی کلاینت همچنان رد می‌شود.

نام/قیمت/تخفیف/tax/credit/features در checkout immutable snapshot می‌شوند. فرانت مبلغ را برای callback ارسال نمی‌کند؛ verification مستقیم provider مرجع است. مسیر verify همان `POST /api/billing/payments/{id}/verify` است. پرداخت موفق فاکتور و سوابق مالی می‌سازد؛ pending/failed در سوابق پرداخت موجود قابل مشاهده‌اند، در جمع پرداخت موفق نیستند.

runtime gateway هنوز Disabled است؛ quote و خواندن صفحه فعال‌اند ولی checkout جدید پنل 503 می‌دهد و payment تازه نمی‌سازد. UI canPurchase=false را رعایت کند. adapter درگاه و پیام/پرداخت واقعی این نوبت اضافه نشده‌اند.

## تمدید و ارتقای روزشمار

تمدید، دوره را به انتهای آخرین اشتراک فعال/صف‌شده اضافه می‌کند. ارتقا برای اشتراک جاری و پکیج دارای امکانات بیشتر مجاز است؛ قابلیت‌های فعلی حذف نمی‌شوند و محدودیت Integer پایین‌تر نمی‌رود. افزایش تعداد قابلیت، محدودیت عددی یا retention ملاک است، نه نام Basic/Advanced یا صرفاً گران‌ترشدن قیمت. ارتقای همان پکیج یا downgrade برابر 400 است.

فرض کاری محصول در این تغییر: **دوره کامل جدید بعد از پرداخت شروع می‌شود و ارزش روزهای کامل باقی‌مانده دوره قبلی از آن کم می‌شود**. `remainingDays=floor((expiry-now)/86400)` و `totalDays=ceil((expiry-start)/86400)`؛ اعتبار برابر floor(قیمت خالص snapshot دوره قبلی × remainingDays / totalDays) تومان است. روز شروع‌شده دوباره اعتبار کامل نمی‌گیرد. پرداخت InternalTestGrant غیرنقدی اعتبار خرید قبلی تولید نمی‌کند. اعتبار از قیمت خالص، پیش از tax، محاسبه می‌شود؛ refund نقدی یا USD wallet entry ایجاد نمی‌کند.

اگر ارزش باقی‌مانده کل قیمت دوره مقصد را پوشش دهد، `400 upgrade_has_no_payable_amount` برمی‌گردد؛ دوره مقصد بزرگ‌تر انتخاب شود. اعتبار و مالیات checkout ثابت‌اند؛ callback دیرهنگام پرداخت معتبر همان quote را fulfill می‌کند. زمان شروع واقعی از verification موفق می‌آید و تاریخ‌های preview تخمینی‌اند.

وجود اشتراک آینده یا checkout اشتراک Pending، upgrade را با `409 subscription_queue_or_checkout_exists` متوقف می‌کند تا زمان خریدشده آینده حذف نشود. بعد از ساخت upgrade pending، checkout اشتراک دیگری حتی از مسیر قدیمی با `409 upgrade_checkout_pending` مسدود است. expiry نمایش quote به‌تنهایی مجوز حذف پرداخت نامعلوم یا مصرف دوباره اعتبار روزشمار نیست؛ با APIهای payments و verify و recovery درگاه، وضعیت آن باید تعیین شود. خطای شبکه درگاه به معنی پرداخت Failed قطعی نیست.

پس از verification، یک SubscriptionReplacement append-only، اشتراک قبلی را از نظر دسترسی جاری پایان می‌دهد و اشتراک جدید همان لحظه شروع می‌شود. رکورد و تاریخ انقضای قرارداد قبلی تغییر نمی‌کنند. unique/FK مرکب، قفل سازمان و trigger PostgreSQL از ارتقای دوباره/بین سازمانی/هم‌پوشانی مؤثر جلوگیری می‌کنند. callback هم‌زمان دوباره خرید نمی‌سازد. پرداخت ناموفق اشتراک قبلی را دست‌نخورده می‌گذارد. مصاحبه قبلاً شروع‌شده همچنان rate/retention/capabilities قرارداد خودش را استفاده می‌کند.

## ۳. افزایش اعتبار

محصول‌های واقعی: `GET /api/pricing/ai-credit-packages`، خرید Owner با `POST /api/billing/ai-credit/purchases` و `{ "creditPackageId": "..." }`. verify همان مسیر قبلی. محصول مبلغ IRT و CreditAmountUsd صریح دارد؛ مقدار دلخواه یا نرخ ارز client پذیرفته نمی‌شود. credit مستقلاً افزایش می‌یابد و دوره اشتراک را تمدید نمی‌کند. فروش اعتبار همان قرارداد قبلی با SellPriceAmount نهایی است؛ tax تازه پنل فقط اشتراک است.

دکمه «تاریخچه مصرف» تب چهارم را با transactionType=usage باز کند. APIهای قدیمی wallet/transactions/ai-usage برای مصرف‌کننده‌های موجود حفظ‌اند.

## ۴. سوابق مالی و فاکتور

`GET /api/billing/financial-history` با query:

- `page` و `pageSize` حداکثر ۱۰۰؛ `fromUtc` و `toUtc` شامل کران‌های انتخاب‌شده، بازه برعکس 400.
- `creditType=subscription|aiCredit`؛ برای «همه» حذف شود.
- `transactionType=payment|usage|adjustment`؛ payment خرید نقدی، usage مصرف مصاحبه، adjustment اصلاح/هدیه/refund است. برای همه حذف شود.

پاسخ: updatedAtUtc، transactions از نوع Page، و totals بر **تمام ردیف‌های مطابق فیلتر، مستقل از صفحه‌بندی**:

| جمع | واحد |
|---|---|
| subscriptionPaidIrt | پرداخت نقدی اشتراک با tax |
| creditPurchasedUsd | اعتبار خریداری‌شده |
| creditConsumedUsd | اعتبار مصرف‌شده، مقدار مثبت برای کارت جمع |
| billableSeconds | مجموع مدت مالی همین ردیف‌های مصرف |
| creditPaidIrt | پرداخت نقدی محصولات اعتبار |

هر ردیف: id، occurredAtUtc، description، creditType، transactionType، amount، currency، cashPaidIrt، billableSeconds، interviewId، candidateName، trackingNumber و invoiceUrl. نام داوطلب و مدت فقط برای مصرف موجود می‌آیند. ستون مانده حساب عرضه نمی‌شود. اشتراک amount منفی IRT است؛ خرید اعتبار amount مثبت USD و cashPaidIrt هزینه واقعی IRT همان خرید است؛ مصرف منفی USD. ارزها با هم جمع نمی‌شوند. ledger mirror خرید اعتبار ردیف دوباره نمی‌سازد. هدیه/اصلاح به عنوان خرید نقدی شمرده نمی‌شود؛ InternalTestGrant فاکتور نقدی/درآمد ایجاد نمی‌کند.

تاریخ فیلتر برای پرداخت `PaidAtUtc` و برای مصرف زمان ledger تسویه است؛ زمان quote مبنای پرداخت نیست. مدت مصاحبه در این صفحه **BillableSeconds سرور** است، نه مدت decodeشده صحبت یا کل زمان بازبودن صفحه. قرارداد window محدود نسخه قبلی [BILLING](BILLING.md) تغییر نکرده؛ UI عنوان «مدت محاسبه‌شده مصرف» را دقیق نمایش دهد.

فاکتور JSON: `GET /api/billing/payments/{id}/invoice`، فقط پرداخت Paid نقدی سازمان جاری؛ pending یا سازمان دیگر 404. invoiceNumber ثابت `HY-{PaymentId}`، trackingNumber provider، زمان پرداخت، نام/نوع، subtotal، credit روزشمار، tax، total و قرارداد اشتراک یا مقدار USD است. مبلغ فاکتور با ویرایش بعدی کاتالوگ تغییر نمی‌کند. این endpoint داده فاکتور است؛ PDF/فاکتور رسمی بیرونی اضافه نشده است.

## migration و تحویل

Migration `20261005133142_AddSubscriptionCreditPanel` پیش از استفاده لازم است؛ به مقصد اعمال نشده. خرید قدیمی operation=purchase، credit=0 و tax snapshot=null می‌گیرد؛ مبلغ قبلی بازنویسی نمی‌شود. downgrade دیتابیس دارای تاریخچه ارتقا راهکار عملیاتی ارائه‌شده نیست. seed به‌صورت یک‌باره در migration اصلاح می‌شود، startup قیمت‌ها را overwrite نمی‌کند.

نتایج build، PostgreSQL و migration در [STATUS](STATUS.md) و [HANDOFF](HANDOFF.md) ثبت‌اند. هیچ frontend، درگاه واقعی، پیام واقعی، push/deploy یا تغییر production انجام نشده است.
