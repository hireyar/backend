# قیمت‌گذاری پویا کاربران تیم — 2026-10-14

بک‌اند روی `dev`؛ Angular در ریپوی جدا و درگاه واقعی همچنان Disabled است. تست پرداخت فقط adapter مصنوعی محلی دارد؛ انتشار/دیتابیس مقصد تغییر نمی‌کند.

## سیاست پکیج

سه فیلد مستقل در POST/PUT پکیج سوپرادمین و GET کاتالوگ عمومی/ادمین:

| فیلد | عمومی seed | پیشرفته seed |
|---|---:|---:|
| `includedTeamUsers` | 3 | 5 |
| `paidTeamUserLimit` | 30 | 30 |
| `teamUserMonthlyPriceIrt` | 200000 | 200000 |

قیمت تومان است. سهمیه شامل Owner و Recruiter فعال است؛ دعوت Pending و منقضی‌نشده نیز یک ظرفیت رزرو می‌کند و پذیرش همان دعوت دوباره ظرفیت مصرف نمی‌کند. دعوت Cancelled/Expired و کاربر غیرفعال ظرفیت مصرف نمی‌کنند. غیرفعال‌سازی/لغو، بازپرداخت خودکار ندارد؛ ظرفیت پرداخت‌شده تا پایان اشتراک قابل استفاده مجدد است.

`paidTeamUserLimit` آخرین شماره کاربر هزینه‌دار است، نه سقف اعضا: عمومی برای ظرفیت۳۰ هزینه۲۷کاربر اضافه و پیشرفته هزینه۲۵کاربر اضافه دارند. پس از تأمین ظرفیت۳۰، `isUnlimited=true` و نفر۳۱ به بعد رایگان است. با آستانه۹، نفر۱۰ به بعد رایگان می‌شود. قیمت صفر هم ظرفیت نامحدود می‌دهد. runtime به code پکیج وابسته نیست؛ basic/advanced فقط در migration اولیه این سیاست تنظیم می‌شوند.

اعتبارسنجی: `includedTeamUsers>=1`، `includedTeamUsers<=paidTeamUserLimit<=10000`، قیمت صحیح تومان۰تا۱۰^۱۲. همان RowVersion و409 برای stale؛ PUT سند کامل است و فرانت باید سه فیلد را از آخرین GET ارسال کند. حذف فیلدها default۳/۳۰/۲۰۰۰۰۰ می‌گیرد، نه مقدار قبلی پکیج. قیمت/سهمیه در خرید snapshot می‌شوند؛ ویرایش ادمین اشتراک جاری، checkout قبلی یا فاکتور پرداخت‌شده را تغییر نمی‌دهد؛ خرید/تمدید بعدی مقادیر تازه را می‌گیرند.

## دوره و گردکردن

افزایش ظرفیت میانه اشتراک:

`amountIrt = ceil(additionalBillableUsers × monthlyPriceIrt × remainingUtcSeconds / 2592000)`

ماه مالی کاربران۳۰روز UTC است؛ پانزده روز دقیق برای یک کاربر۲۰۰هزارتومانی =۱۰۰۰۰۰تومان؛ ۹۰روز =۶۰۰۰۰۰تومان. ثانیه/کسری ثانیه سرور لحاظ و فقط مجموع نهایی به تومان کامل رو به بالا گرد می‌شود. اشتراک اصلی همچنان AddMonths تقویمی است؛ باقی‌مانده۳۱روز با مبنای مالی۳۰روزه۲۰۶۶۶۷تومان می‌شود. خرید ظرفیت زمان انقضای اشتراک را تمدید نمی‌کند.

خرید/تمدید دوره کامل: `billableUsers × monthlyPrice × period.months`. تخفیف دوره فقط قیمت پایه پکیج است. quote/checkout اشتراک پنل، مالیات تنظیم‌شده موجود را روی subtotal خالص شامل ظرفیت اعمال می‌کند؛ teamSeatAmountIrt هزینه پیش از مالیات است. خرید جداگانه ظرفیت تیم مالیات افزوده جدید ندارد؛ مثال۱۰۰۰۰۰تومان مبلغ همان checkout است. مسیر legacy خرید اشتراک هزینه ظرفیت را اعمال می‌کند و مانند قبل مالیات پنل ندارد.

## API مالک سازمان

مسیرها فقط Owner، JWT پنل، scope هویت سرور و no-store؛ Recruiter مجوز مالی ندارد. organizationId، مبلغ و موفقیت پرداخت از client پذیرفته نمی‌شوند.

`GET /api/billing/team-seats` → subscriptionId, includedTeamUsers, paidTeamUserLimit, teamUserMonthlyPriceIrt, capacity, isUnlimited, occupiedUsers, expiresAtUtc.

capacity بیشینه ظرفیت پایه قرارداد و افزایش‌های دارای Payment.Paid برای همان اشتراک است. خرید پیشاپیش ظرفیت ممکن است. بدون اشتراک جاری، پایه۳/۳۰/۲۰۰۰۰۰ و ظرفیت۳؛ افزایش پولی اشتراک فعال می‌خواهد. trial تازه سهمیه/آستانه بیشینه کاتالوگ فعال عمومی و نرخ مرجع trial را snapshot می‌کند؛ پیش‌فرض۵/۳۰/۲۰۰۰۰۰. trialهای قبلی زمان/هدیه تازه نمی‌گیرند.

`POST /api/billing/team-seats/quote`:

```json
{ "targetCapacity": 6 }
```

پاسخ TeamSeatQuoteDto: subscriptionId, previousCapacity, targetCapacity, additionalBillableUsers, monthlyPriceIrt, remainingBillingMonths, amountIrt, currency=IRT, quotedAtUtc, expiresAtUtc, quoteFingerprint.

target در آستانه هزینه cap می‌شود؛ درخواست۴۰ با آستانه۳۰ فقط خرید تا۳۰ را quote می‌کند و پس از پرداخت نامحدود است. کاهش/تکرار ظرفیت یا quote نامحدود400/team_capacity_not_increased؛ بدون اشتراک403/active_subscription_required.

`POST /api/billing/team-seats/checkout`:

```json
{
  "targetCapacity": 6,
  "quotedAtUtc": "<exact UTC timestamp from quote>",
  "quoteFingerprint": "<fingerprint from quote>"
}
```

قیمت با همان زمان quote محاسبه مجدد می‌شود؛ مهلت۵دقیقه. زمان آینده/قدیمی‌تر/پیش از شروع اشتراک یا precision ناسازگار400/team_quote_expired؛ فقدان دو فیلد400/quote_required؛ fingerprint متفاوت409/quote_changed. تغییر ظرفیت/قرارداد quote تازه می‌خواهد؛ مبلغ خارج precision دیتابیس400/payment_amount_out_of_range.

پاسخ `{checkout,summary}`؛ checkout همان PurchaseResult موجود و payment.type=TeamSeats است. checkout Pending اشتراک/تیم، خرید ظرفیت دوم را با409/team_checkout_pending رد می‌کند. تا تعیین تکلیف checkout تیم، خرید/تمدید/upgrade جدید هم رد می‌شوند. Disabled درگاه503/payment_gateway_unavailable و هیچ checkout تازه ایجاد نمی‌کند.

تأیید همان POST payments/{id}/verify یا callback موجود است. فقط پاسخ provider با مبلغ/ارز قرارداد و شناسه تراکنش، Payment را Paid می‌کند؛ verify تکراری/رقابتی ظرفیت یا وجه تازه تولید نمی‌کند. Failed/مبلغ ناسازگار ظرفیت را باز نمی‌کند. پول تأییدشده دیررس همان قرارداد را ثبت می‌کند؛ انقضای اولیه ثابت است، پس پرداخت بعد از انقضای اشتراک دسترسی تازه یا تمدید خودکار ندارد.

POST دعوت موجود و پذیرش OTP زیر قفل سازمان ظرفیت را بررسی می‌کنند. کسری409/team_capacity_payment_required؛ شکست دعوت/عضو نمی‌سازد و OTP معتبر را مصرف نمی‌کند. نقش/سازمان/شماره/نام/security stamp قبلی محفوظ‌اند. غیرفعال‌سازی عضو همان ابطال دسترسی قبلی را دارد.

## خرید، تمدید، ارتقا و تاریخچه

در subscription/quote، subscription/checkout و subscription/purchases فیلد اختیاری teamCapacity اضافه شده است. پیش‌فرض ظرفیت فعلی یا ظرفیت لازم اعضای فعال+دعوت‌های pending؛ کمتر از ظرفیت لازم400/team_capacity_too_small. هزینه کاربران تا آستانه cap می‌شود. quote/فاکتور اشتراک teamCapacity و teamSeatAmountIrt دارند. PriceAmountSnapshot و GET purchases قیمت پایه پکیج را نگه می‌دارند؛ Payment.Amount و invoice نقد واقعی‌اند.

تمدید ظرفیت را برای دوره جدید می‌خرد؛ افزایش‌های دوره قبلی به اشتراک تازه انتقال خودکار ندارند. صف زمانی اشتراک‌ها حفظ شده است. در upgrade، ارزش استفاده‌نشده ظرفیت اولیه و افزایش‌های Paid با قواعد روزشمار credit قبلی محاسبه می‌شود؛ ledger USD تغییر ندارد.

پرداخت تیم در GET payments موجود است. financial-history?creditType=teamSeats دسته مستقل تومان، amount منفی، cashPaidIrt و لینک invoice دارد؛ totals.teamSeatsPaidIrt اضافه شده است. ظرفیت داخل خرید اشتراک در دسته subscription است. invoice تیم type=TeamSeats، name=ظرفیت کاربران تیم و teamSeats snapshot دارد؛ سازمان دیگر404. credit USD/ledger مصنوعی برای ظرفیت ایجاد نمی‌شود.

## migration و سوابق قبلی

20261014060524_AddDynamicTeamSeatBilling پیش از API تازه لازم است. ستون‌های پکیج، snapshot خرید/trial و جدول immutable TeamSeatPurchases؛ FK مرکب سازمان/اشتراک/پرداخت، paymentId یکتا، check اعداد و constraint trigger تطابق قیمت/مبلغ/انقضا. mutation مالی/تیمی زیر قفل کوتاه Organizations؛ network درگاه خارج transaction. Repository/provider تازه نداریم.

پول/زمان/ledger/نام‌های قبلی ثابت‌اند. migration ظرفیت اعضای فعال+pending را حداقل سهمیه رایگان و حداکثر۳۰، بدون شارژ بازگشتی به قراردادهای قبلی می‌دهد؛ سهمیه۳/۵ از snapshot کد تاریخی، نرخ۲۰۰۰۰۰ و آستانه۳۰. trial قدیمی حداقل۵ تا ظرفیت موجود را سهمیه grandfather می‌گیرد. خرید بعدی سیاست جدید را می‌گیرد. Down در حضور خرید ظرفیت، هزینه/ظرفیت افزوده اشتراک، trial یا تنظیمات ویرایش‌شده ادمین رد می‌شود.

SQL delta idempotent: artifacts/team-seat-migrations.sql. مقصد در این تسک تغییر نمی‌کند.

## بخش نیازمند تصمیم صریح

قطع دسترسی سراسری Recruiter موجود هنگام کاهش/انقضای ظرفیت اعمال نشده؛ بازبینی خودکار به دلیل خطر قفل‌کردن اعضای فعلی و نبود مجوز صریح رد کرد. کنترل دعوت/پذیرش و محاسبه تمدید موجود است؛ enforcement برای تمام اعضای فعال قبلی کامل نیست. مثال باز: اشتراک بعدی با ظرفیت۳ قبلاً خریداری شده، سپس در دوره جاری ظرفیت۴ خریده و عضو چهارم فعال شود؛ دوره بعد دعوت تازه را می‌بندد ولی JWT عضو چهارم به دلیل کسری ظرفیت باطل نمی‌شود. سیاست قطع دسترسی، ترتیب اعضا و پیام محصول تأیید صریح می‌خواهند. درگاه واقعی Disabled و UI نیز تکمیل‌شده محسوب نمی‌شوند.

## پرامپت اتصال فرانت

سه فیلد «کاربران رایگان»، «آخرین کاربر هزینه‌دار» و «قیمت ماهانه هر کاربر اضافه (تومان)» را به مدیریت پکیج اضافه کن؛ PUT مقادیر GET و RowVersion تازه را حفظ کند. صفحه تیم مالک GET team-seats را بخواند و مصرف/ظرفیت/نامحدود را نشان دهد. در409/team_capacity_payment_required افزایش ظرفیت را نمایش بده؛ targetCapacity را quote کن و تعداد هزینه‌دار، زمان باقی‌مانده، تومان نهایی و انقضا را قبل از پرداخت نشان بده. quotedAtUtc/fingerprint را عیناً در checkout بفرست. پس از درگاه verify موجود و سپس refresh state؛ querystring موفقیت کافی نیست. نامحدود دعوت موجود را بدون checkout اضافه انجام دهد. تمدید teamCapacity، جزء teamSeatAmountIrt و مالیات/جمع سرور را نمایش دهد. دسته teamSeats و enum TeamSeats به تاریخچه/فاکتور افزوده شوند؛ Recruiter endpoint مالی را صدا نزند. اجرای فرانت این ریپو نیست.
