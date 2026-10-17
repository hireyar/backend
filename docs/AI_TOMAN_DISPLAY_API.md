# نمایش تومانی اعتبار AI با والکس

قرارداد 2026-10-17. حسابداری USD و مبالغ قطعی پرداخت IRT باقی می‌مانند؛ این قابلیت صرفاً تبدیل نمایش است و نرخ فروش پکیج شارژ/checkout را تغییر نمی‌دهد. فرانت Angular در این مخزن نیست.

## API

`GET /api/pricing/ai-display-rate` عمومی، no-store:

```json
{
  "source": "Wallex",
  "symbol": "USDTTMN",
  "currency": "IRT",
  "tomanPerUsdt": 65000.5,
  "fetchedAtUtc": "2026-10-17T10:00:00Z",
  "expiresAtUtc": "2026-10-17T10:00:05Z",
  "refreshAfterSeconds": 5
}
```

عدد نمونه است. نرخ از `https://api.wallex.ir/v1/markets`، مسیر `result.symbols.USDTTMN.stats.lastPrice` گرفته می‌شود؛ quoteAsset=TMN و baseAsset=USDT کنترل می‌شوند. تقسیم بر ۱۰ لازم نیست. برای نمایش، هر دلار اعتبار معادل یک تتر فرض می‌شود. نرخ تا۵ثانیه cache مشترک دارد؛ refresh همزمان سری می‌شود و failure پنج ثانیه backoff دارد. نرخ نامعتبر/قطع سرویس/timeout پاسخ503 با code=`ai_display_rate_unavailable`؛ نرخ منقضی برگردانده نمی‌شود. fetch بیرون transaction/قفل دیتابیس انجام می‌شود. هیچ کلید API لازم نیست.

## پرامپت آماده فرانت

نمایش مالی AI در تمام پنل HireYar را به تومان تبدیل کن. یک سرویس مرکزی reactive برای GET /api/pricing/ai-display-rate بساز؛ همه کامپوننت‌ها از یک snapshot نرخ استفاده کنند. در ورود صفحه و هر refreshAfterSeconds (فعلاً۵ثانیه) نرخ را بگیر؛ هنگام بازگشت تب فعال هم refresh کن. درخواست‌ها overlap نشوند، در destroy پاک شوند و در تب hidden polling متوقف شود. پس از انقضای expiresAtUtc یا شکست refresh، قیمت را آنلاین معرفی نکن؛ loading/«نرخ موقتاً در دسترس نیست» و retry نمایش بده؛ مقدار صفر، نرخ ثابت یا دلار جایگزین نشان نده. در صورت وجود نرخ قبلی فقط با برچسب صریح «آخرین نرخ دریافتی» و زمان آن می‌توان نمایش داد.

محاسبه: amountToman = amountUsd × tomanPerUsdt. با حساب اعشاری دقیق محاسبه کن، فقط در نمایش نهایی به نزدیک‌ترین تومان با نیم به دور از صفر گرد کن؛ علامت منفی مصرف/اصلاحات را حفظ کن. نمایش با Intl.NumberFormat('fa-IR') و پسوند «تومان»؛ نرخ TMN از قبل تومان است، تقسیم بر۱۰ نکن. اعداد پایه USD را نگه دار تا refresh باعث تبدیل مجدد عدد تبدیل‌شده نشود.

در کارت پکیج، AiPricePerMinuteUsd پکیج انتخابی × نرخ را بزرگ و با عنوان «هزینه هر دقیقه AI» نشان بده؛ زیر همان قیمت، مقدار دلاری هر دقیقه را کوچک و کم‌رنگ بنویس. این تنها استثنای نمایش دلار است. در سایر بخش‌ها فقط تومان نمایش بده. اگر کارت انتخاب محصول شارژ AI است، مقدار اعتبار CreditAmountUsd × نرخ را با عنوان «معادل اعتبار AI» نشان بده؛ مبلغ واقعی خرید SellPriceAmount/SellPriceCurrency مستقل است و تومان قطعی پرداخت را با معادل اعتبار اشتباه نگیر.

موارد تبدیل: packages[].aiPricePerMinuteUsd؛ current/lastExpired/scheduled.aiRatePerMinuteUsd؛ panel.aiRatePerMinuteUsd؛ wallet.balance/reservedCredit/availableCredit در panel و GET /api/billing/ai-credit؛ GET /api/billing/ai-credit/transactions مقادیر amount/balanceBefore/balanceAfter؛ GET /api/billing/ai-usage مقادیر ratePerMinuteUsd/billedAmountUsd؛ financial-history فقط ردیف currency=USD و totals.creditPurchasedUsd/creditConsumedUsd؛ invoice.creditAmountUsd و نرخ AI داخل subscription؛ نرخ AI در quote/purchases و سایر محل‌های مصرف این فیلدها. نام فیلدها و واحد API را تغییر نده؛ مدل نمایش تومانی مستقل بساز. همه این اعداد با هر نرخ تازه با هم بازحساب شوند.

مبالغی که currency=IRT دارند، SellPriceAmount، CashPaidIrt، قیمت اشتراک، مالیات، تخفیف، هزینه صندلی و total پرداخت از قبل تومان‌اند و هرگز در نرخ ضرب نمی‌شوند. مبلغ پرداخت/فاکتور تاریخی ثابت است؛ معادل اعتبار تاریخی با نرخ امروز فقط با برچسب «معادل با نرخ فعلی» نمایش داده شود. زمان/دقیقه/estimatedAvailableMinutes با تغییر نرخ ارز تغییر نمی‌کند. نرخ هزینه مصاحبه از قرارداد snapshot جاری گرفته شود، نرخ پکیج تازه انتخاب‌شده صرفاً پیش‌نمایش است.

زیر قیمت اصلی پکیج زمان آخرین دریافت و منبع والکس را نشان بده. refresh بدون reload صفحه باشد. در تست‌ها نرخ ساختگی65000.5 و قیمت0.1 دلار باید6500.05 و در نمایش۶٬۵۰۰تومان شود؛ ورودی منفی، صفر، نرخ نامعتبر،503، انقضا، تغییر پکیج و جلوگیری از تبدیل دوباره IRT را بررسی کن. روی موبایل و RTL قیمت بزرگ تومان و دلار کوچک زیرش خوانا باشد.

## اعتبارسنجی و محدودیت

تست‌های provider با پاسخ مصنوعی، parsing رشته/عدد، واحد تومان، نرخ نامعتبر، cache/انقضا/خطا و بازیابی را بررسی می‌کنند. تماس واقعی والکس در محیط اجرا با خطای DNS مواجه شد؛ دسترسی عملی مقصد به والکس باید هنگام استقرار بررسی شود. پیاده‌سازی و بررسی UI در ریپوی فرانت انجام می‌شود. دریافت هر۵ثانیه polling است؛ streaming لحظه‌ای WebSocket پیاده نشده است.

نتیجه بررسی: build پروژه‌های Domain/Application/Infrastructure/Api/Tests موفق و۶تست WallexDisplayRateTests پاس شد (اجرای test runner خارج sandbox).

