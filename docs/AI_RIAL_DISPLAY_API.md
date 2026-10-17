# خروجی ریالی مالی و اعتبار AI

قرارداد جاری 2026-10-17؛ جایگزین AI_TOMAN_DISPLAY_API.md و تمام دستورهای قبلی مبنی بر تبدیل دلار در فرانت است. این تغییر قرارداد پاسخ APIهاست و باید با فرانت هماهنگ منتشر شود. Angular در این مخزن نیست.

## تصمیم تأییدشده

کاربر تأیید کرده است: **معادل ریالی با نرخ روز تغییر کند**. موجودی و ledger حسابداری در دیتابیس USD هستند. بک‌اند در مرز HTTP همه مبالغ خروجی مالی را به IRR تبدیل می‌کند. ثابت‌بودن تعداد دلارهای اعتبار، ثابت‌بودن معادل ریالی را تضمین نمی‌کند. مبلغ قطعی پرداخت با تغییر نرخ ارز تغییر نمی‌کند؛ تبدیل آن از تومان ثبت‌شده به ریال همیشه ضریب ثابت۱۰ است. این تصمیم نیاز به migration، تغییر ledger یا بازنویسی تاریخچه ندارد.

## محاسبه و نرخ

`GET /api/pricing/ai-display-rate` عمومی و no-store:

```json
{
  "source": "Wallex",
  "symbol": "USDTTMN",
  "currency": "IRR",
  "rialPerUsdt": 650005,
  "fetchedAtUtc": "2026-10-17T10:00:00Z",
  "expiresAtUtc": "2026-10-17T10:00:05Z",
  "refreshAfterSeconds": 5
}
```

عدد نمونه است. `GET https://api.wallex.ir/v1/markets`، فیلد `result.symbols.USDTTMN.stats.lastPrice` تومان است؛ بک‌اند آن را در۱۰ ضرب می‌کند. quoteAsset=TMN و baseAsset=USDT کنترل می‌شوند. هر دلار اعتبار برای این تبدیل معادل یک تتر فرض می‌شود.

- USD → IRR: ضرب در rialPerUsdt با decimal، بدون گردکردن زودهنگام.
- IRT → IRR: ضرب در۱۰، مستقل از نرخ ارز.
- IRR → IRR: بدون تبدیل دوباره.
- واحد تمام پاسخ‌های مالی `currency=IRR`؛ در محصول شارژ `sellPriceCurrency=IRR` نیز هست.
- cache مشترک و refresh پنج ثانیه، timeout پنج ثانیه، backoff خطا پنج ثانیه؛ نرخ منقضی استفاده نمی‌شود.
- نبود نرخ معتبر: HTTP503 و `code=ai_display_rate_unavailable`؛ پاسخ مالی مخلوط USD/IRT به‌جای ریال تحویل داده نمی‌شود.
- نرخ پیش از عملیات نیازمند تبدیل، خارج transaction و قفل DB دریافت می‌شود؛ قطع والکس قبل از mutation مانع اجرای آن است. یک پاسخ و تمام اجزای آن از یک snapshot نرخ استفاده می‌کنند.
- مسیرهای صرفاً نقدی مانند payments، verify، خرید اعتبار، خرید ساده اشتراک و team-seats با تبدیل ثابت IRT×۱۰ بدون وابستگی به والکس کار می‌کنند.

## مسیرهای بررسی و اصلاح‌شده

| مسیر | خروجی ریالی |
| --- | --- |
| GET /api/pricing/packages | aiPricePerMinuteRial، periods[].price، teamUserMonthlyPriceRial |
| GET /api/pricing/ai-credit-packages | creditAmountRial، sellPriceAmount |
| GET /api/billing/panel | wallet.balance/reservedCredit/availableCredit، aiRatePerMinuteRial، current/lastExpired/scheduled و قیمت صندلی |
| GET /api/billing/ai-credit | balance، reservedCredit، availableCredit با currency=IRR |
| GET /api/billing/ai-credit/transactions | amount، balanceBefore، balanceAfter با currency=IRR |
| GET /api/billing/ai-usage | ratePerMinuteRial، billedAmountRial |
| GET /api/billing/subscription | current/scheduled.aiRatePerMinuteRial و teamUserMonthlyPriceRial |
| POST /api/billing/subscription/quote و /checkout | originalAmount، discountAmount، subtotalAmount، proratedCreditAmount، taxAmount، totalAmount، aiRatePerMinuteRial، teamSeatAmountRial و payment.amount |
| GET /api/billing/purchases | priceAmount و aiRatePerMinuteRial |
| GET /api/billing/payments و /payments/{id}، POST /payments/{id}/verify | amount با currency=IRR |
| POST /api/billing/subscription/purchases و /ai-credit/purchases | checkout.payment.amount با currency=IRR |
| GET /api/billing/financial-history | transactions.items[].amount، cashPaidRial و تمام totals مالی با پسوند Rial |
| GET /api/billing/payments/{id}/invoice | subtotalAmount، proratedCreditAmount، taxAmount، totalAmount، creditAmountRial و subscription/teamSeats تو در تو |
| GET /api/billing/team-seats | teamUserMonthlyPriceRial |
| POST /api/billing/team-seats/quote و /checkout | monthlyPriceRial، amountRial و checkout.payment.amount |
| /api/admin/pricing/packages و /ai-credit-packages، شامل پاسخ ایجاد/ویرایش/period/feature | همان قرارداد ریالی کاتالوگ عمومی |
| /api/admin/pricing/organizations/{org}/subscription و /ai-credit و POST /ai-credit/adjustments | همان قرارداد ریالی اشتراک و کیف پول |

تمام DTOهای پاسخ جدا و strongly typed هستند؛ OpenAPI پاسخ‌ها را با نام Rial... معرفی می‌کند. DTOهای Application، واحد ذخیره، امضای محاسبات داخلی و آداپتور درگاه تغییر نکرده‌اند. callback درگاه خروجی مبلغی ندارد. APIهای مصاحبه/گزارش/داشبورد/سازمان بررسی شدند و DTO مالی USD/IRT دیگری خارج از این مسیرها ندارند.

فیلدهای قبلی `...Usd` و `...Irt` در پاسخ‌های مالی به `...Rial` تغییر نام داده‌اند. استثنا: در **دو DTO کاتالوگ پکیج**، `aiPricePerMinuteUsd` یا `creditAmountUsd` هم برای نمایش کوچک دلار زیر قیمت پکیج و ویرایش تنظیمات منبع حفظ شده‌اند؛ مبلغ اصلی ریالی جداگانه موجود است. دیگر پاسخ‌ها فیلد مالی USD ندارند.

نمونه کیف پول (با۱۰دلار اعتبار،۲دلار رزرو و۸دلار قابل استفاده و نرخ نمونه بالا):

```json
{
  "currency": "IRR",
  "balance": 6500050,
  "reservedCredit": 1300010,
  "availableCredit": 5200040,
  "displayRate": {
    "source": "Wallex",
    "symbol": "USDTTMN",
    "currency": "IRR",
    "rialPerUsdt": 650005,
    "fetchedAtUtc": "2026-10-17T10:00:00Z",
    "expiresAtUtc": "2026-10-17T10:00:05Z",
    "refreshAfterSeconds": 5
  }
}
```

DTOهای تبدیل‌شده `displayRate` دارند؛ در خروجی صرفاً نقدی می‌تواند null باشد. صفحه‌بندی، شناسه‌ها، rowVersion، fingerprint، درصدها، ظرفیت، زمان و estimatedAvailableMinutes تبدیل نمی‌شوند. معادل اعتبار/مصرف تاریخی با نرخ امروز تغییر می‌کند و باید «معادل با نرخ فعلی» خوانده شود؛ پول پرداخت‌شده تاریخی ثابت است.

## درخواست‌های نوشتن ادمین و درگاه

این تغییر مخصوص **پاسخ** است. قرارداد ورودی مدیریتی قیمت‌ها همچنان صریحاً USD/IRT منبع است: PackageRequest.aiPricePerMinuteUsd، CreditPackageRequest.creditAmountUsd و sellPriceAmount با sellPriceCurrency=IRT، PeriodRequest.priceAmount با currency=IRT، PackageRequest.teamUserMonthlyPriceIrt. AdjustmentRequest.amount در حسابداری USD است. فرم ادمین از فیلد USD پکیج یا تبدیل مبلغ ریالی فرم به واحد ورودی استفاده کند؛ برای مقدار اصلاح اعتبار ریالی، amountUsd=amountRial/displayRate.rialPerUsdt با precision شش رقم و تأیید مقدار نهایی. فیلد ریالی پاسخ را مستقیم به فیلد USD/IRT درخواست کپی نکنید. درگاه و callback همچنان مطابق قرارداد قبلی IRT هستند؛ تغییر واحد HTTP موجب تغییر مبلغ قابل پرداخت نشده است.

## پرامپت آماده فرانت

همه مصرف‌کننده‌های API مالی HireYar را با پاسخ‌های جدید Rial... هماهنگ کن. بک‌اند اکنون تبدیل کامل را انجام می‌دهد؛ کیف پول، مصرف، تراکنش، تاریخچه، فاکتور، اشتراک، پکیج و صندلی پاسخ ریالی IRR دارند. هیچ مبلغ پاسخ را دوباره در نرخ تتر یا۱۰ ضرب نکن. فیلدهای ...Usd/...Irt پاسخ را به ...Rial مهاجرت بده؛ در کیف پول/تراکنش/پرداخت نام amount/balance تغییر نکرده، واحد currency از IRR خوانده می‌شود. واحد UI ریال است؛ اگر بعداً تومان خواسته شد فقط در formatter نمایش بر۱۰ تقسیم کن و برچسب تومان بگذار.

اطلاعات مالی را در ورود، هر۵ثانیه و هنگام فعال‌شدن دوباره تب بازخوانی کن تا معادل ریالی جاری شود؛ گرفتن صرف ai-display-rate برای به‌روزرسانی مبلغ‌های قبلی کافی نیست. از displayRate پاسخ برای زمان آخرین نرخ و expiresAtUtc استفاده کن. درخواست‌ها overlap نکنند، در hidden polling متوقف و در destroy پاک شود. روی خطای503 یا انقضای نرخ، مقدار صفر/دلار یا قیمت آنلاین ساختگی نشان نده؛ حالت «نرخ موقتاً در دسترس نیست» و retry، یا مقدار قبلی با برچسب صریح «آخرین نرخ دریافتی» نمایش بده. برای هماهنگی چند کارت، ترجیحاً از پاسخ panel و اجزای همان snapshot استفاده کن.

قیمت اصلی پکیج را ریالی بزرگ نشان بده؛ مقدار USD مرجع همان پکیج فقط کوچک و کم‌رنگ زیر آن باشد. همه جای دیگر مبلغ ریال است. هزینه هر دقیقه AI و معادل اعتبار محصول شارژ را با مبلغ واقعی پرداخت اشتباه نگیر. مبلغ ثابت پرداخت تاریخی را با نرخ جدید بازحساب نکن؛ معادل اعتبار/مصرف تاریخی را «معادل با نرخ فعلی» بنام. با حساب اعشاری دقیق کار کن و فقط موقع نمایش گرد کن؛ علامت منفی مصرف محفوظ باشد. عدد فارسی/RTL و واحد ریال خوانا باشند. تغییر نرخ نباید زمان قابل استفاده یا ظرفیت/درصدها را عوض کند. فرم‌های ادمین همچنان قرارداد ورودی USD/IRT دارند؛ بخش درخواست‌ها در همین سند را رعایت کن.

تست نمونه: والکس65000.5تومان → نرخ650005ریال؛ اعتبار0.1دلار →65000.5ریال. مبلغ فروش500000تومان در پاسخ5000000ریال است و با تغییر نرخ تتر ثابت می‌ماند. مقدار۱۰دلار اعتبار با تغییر نرخ650005به700000 از6500050به7000000ریال تغییر می‌کند، بدون تغییر۱۰دلار ledger.

## محدودیت اجرا

دریافت نرخ polling پنج‌ثانیه‌ای است و WebSocket نیست. تماس واقعی والکس قبلاً در این محیط به خطای DNS برخورد؛ دسترسی مقصد هنوز تأیید نشده است. فرانت و انتشار مقصد انجام نشده‌اند؛ migration مالی لازم نیست.

اعتبارسنجی نهایی تبدیل ریالی: build همه پروژه‌ها با صفر هشدار/خطا موفق؛ در اجرای PostgreSQL واقعی۷۱سناریو پاس و یک انتظار قدیمی واحد در تست migration اصلاح شد؛ بازاجرای۲۱سناریو شامل همه۲۰تست نرخ/خروجی/API جدید و همان migration با۲۱Pass/۰Fail/۰Skip موفق شد. مجموع۷۲سناریوی یکتای مرتبط تأیید شد. شواهد: artifacts/rial-billing-tests.log، artifacts/rial-billing-final.log و TestResults/rial-billing-final.trx در پروژه تست. تغییر نرخ650005→700000، پاسخIRR کیف پول/ادمین/پرداخت، ثباتledgerUSD و snapshotIRT، عدم mutation هنگام503 و cash-read بدون والکس در PostgreSQL بررسی شد. تست‌ها نرخ مصنوعی دارند؛ اتصال واقعی والکس/فرانت/انتشار تأیید نشده‌اند.
