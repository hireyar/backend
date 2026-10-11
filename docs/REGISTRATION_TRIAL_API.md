# دوره آزمایشی ثبت‌نام — به‌روزرسانی 2026-10-11

## تنظیمات مدت و هدیه

بخش `RegistrationTrial` در `src/HireYar.Api/appsettings.json` مدت و مبلغ ثبت‌نام‌های تازه را تعیین می‌کند؛ پیش‌فرض مشترک ۳ روز و ۱ دلار است. نمونه شارژ نیم‌دلار:

```json
"RegistrationTrial": {
  "DurationDays": 3,
  "CreditAmountUsd": 0.5
}
```

در JSON مبلغ به‌صورت `0.5` نوشته شود. DurationDays عدد صحیح از ۱ تا ۳۶۵۰؛ CreditAmountUsd عدد decimal مثبت، کمتر از 1000000000000 و با حداکثر شش رقم اعشار است. مقدار نامعتبر هنگام شروع برنامه رد می‌شود. override محیطی با `RegistrationTrial__DurationDays` و `RegistrationTrial__CreditAmountUsd` ممکن است؛ پس از تغییر تنظیمات برنامه دوباره راه‌اندازی شود.

این مقادیر در زمان ثبت‌نام به تاریخ انقضای اشتراک و CreditAmountUsd و ledger/کیف پول تبدیل و ذخیره می‌شوند. تغییر تنظیمات، حساب‌ها و دوره‌های قبلی را بازنویسی یا شارژ نمی‌کند. مثال با ۲ روز و ۰٫۵ دلار، بازه دقیق ۴۸ ساعت و مانده/ledger دقیق ۰٫۵ دلار دارد. با نرخ اولیه ۰٫۰۵ دلار/دقیقه، نیم‌دلار حداکثر ده دقیقه رزرو را پوشش می‌دهد.

migration جدید `20261011120218_ConfigureRegistrationTrial` پیش از اجرای نسخه جدید لازم است: مبلغ مثبت و مدت صحیح روز جای الزام ثابت یک دلار/۷۲ ساعت را می‌گیرند؛ trigger تطابق ledger با مبلغ snapshot را کنترل می‌کند. سوابق موجود دست‌نخورده‌اند؛ downgrade در حضور مبلغ یا مدت غیراستاندارد قدیمی صریحاً رد می‌شود. توضیح ۷۲ ساعت/یک دلار در ادامه مقدار پیش‌فرض است.

پس از اولین تأیید OTP و ساخت Owner/سازمان، در همان تراکنش ثبت‌نام:

- اشتراک آزمایشی از زمان ثبت‌نام UTC به مدت دقیق ۷۲ ساعت ایجاد می‌شود.
- کیف پول USD یک دلار می‌گیرد؛ یک ledger با Type=PromotionalCredit و ReferenceType=RegistrationTrial ثبت می‌شود.
- قابلیت‌های فعال تمام پکیج‌های فعال و عمومی در همان زمان، به‌صورت اجتماع snapshot می‌شوند. Boolean با یک نسخه فعال، Integer با بیشترین مقدار؛ Text با اولین پکیج در ترتیب retention نزولی و code صعودی. کاتالوگ آینده قرارداد آزمایشی موجود را بازنویسی نمی‌کند.
- نرخ AI از پکیج مرجع با بیشترین retention (تساوی: code صعودی) و retention از بیشترین مقدار پکیج‌های فعال عمومی snapshot می‌شوند؛ در کاتالوگ اولیه نرخ ۰٫۰۵ دلار/دقیقه و retention شصت روز است. محدودیت اعتبار و رزرو قبلی برقرار است: مثال مصاحبه پنج‌دقیقه‌ای ۰٫۲۵ دلار رزرو می‌کند؛ هدیه یک دلار رزرو بیش از بیست دقیقه با نرخ اولیه را پوشش نمی‌دهد.

این grant پرداخت نقدی، خرید ماهانه یا درآمد نیست؛ هیچ Payment یا SubscriptionPurchase جعلی ساخته نمی‌شود. FK دوره پکیج فقط مرجع کاتالوگ است و مدت trial را تعیین نمی‌کند. سازمان جدید تا commit برای تراکنش‌های دیگر قابل مشاهده نیست؛ ثبت‌نام نیاز به قفل مالی تو‌در‌تو ندارد. OTP زیر قفل موجود مصرف می‌شود. نبود کاتالوگ قابل استفاده خطای 503 با code=trial_catalog_unavailable دارد و تمام ایجاد حساب/سازمان/grant rollback می‌شود؛ همان OTP معتبر قابل retry است.

ورود بعدی، refresh و ویرایش پروفایل هدیه را تکرار یا تاریخ انقضا را تمدید نمی‌کنند. حساب‌های قبلی backfill نمی‌شوند. انقضای trial دسترسی تازه مصاحبه را می‌بندد؛ مانده هدیه USD حذف نمی‌شود و استفاده بعدی به اشتراک فعال نیاز دارد. جلسه شروع‌شده snapshot قابلیت/نرخ/retention قبلی و قواعد رزرو/تسویه خودش را حفظ می‌کند.

## قرارداد پنل

GET `/api/billing/subscription` و `/api/billing/panel` اشتراک آزمایشی را با `isTrial=true`، `packageCode=registration_trial`، زمان شروع/انقضا و همه features عرضه می‌کنند. وضعیت زمانی همان Active/Expired موجود است؛ `isTrial` نوع قرارداد است. پکیج خریداری‌شده `isTrial=false` دارد. GET کیف پول و تراکنش‌ها هدیه و مانده را از مسیرهای قبلی عرضه می‌کنند؛ financial-history هدیه را adjustment/اعتبار هدیه می‌خواند و مبلغ نقدی ندارد.

خرید عادی در طول trial از زمان انقضای ذخیره‌شده زمان‌بندی می‌شود؛ renewal فقط مطابق مرجع کاتالوگ قبلی پذیرفته می‌شود. upgrade برای trial با 400 و `trial_requires_purchase` رد می‌شود؛ پول پرداخت‌نشده credit روزشمار نمی‌سازد. برای ادامه یکی از پکیج‌ها با operation=purchase انتخاب شود.

## تشخیص در دیتابیس

`OrganizationSubscriptions.IsTrial` پیش‌فرض false است؛ فقط trial دارای PaymentId=null و بازه صحیح روز مطابق تنظیمات زمان ثبت‌نام است. `RegistrationTrials` با Id همان اشتراک، OrganizationId unique، مقدار هدیه و snapshot نرخ/retention؛ `RegistrationTrialEntitlement` snapshot تمام قابلیت‌ها را نگه می‌دارد. اتصال AspNetUsers.OrganizationId مشخص می‌کند کدام کاربران این نوع ثبت‌نام را داشته‌اند، حتی پس از انقضا یا خرید اشتراک:

```sql
SELECT u."Id" AS "UserId", u."OrganizationId", s."IsTrial",
       s."StartedAtUtc", s."ExpiresAtUtc",
       (s."StartedAtUtc" <= now() AND s."ExpiresAtUtc" > now()) AS "TrialActive",
       g."CreditAmountUsd"
FROM "AspNetUsers" u
JOIN "RegistrationTrials" g ON g."OrganizationId" = u."OrganizationId"
JOIN "OrganizationSubscriptions" s
  ON s."OrganizationId" = g."OrganizationId" AND s."Id" = g."Id";
```

Unique index یک trial در هر سازمان، FK مرکب منع رابطه بین سازمان‌ها، triggerهای immutable و constraint trigger تکمیل grant/ledger در commit اعمال می‌شوند. Down با تاریخچه trial صریحاً رد می‌شود. migration لازم پیش از کد: `20261010132215_AddRegistrationTrial`. اعمال migration مقصد و تغییر Angular این نوبت انجام نشده‌اند.

تست‌های مالی قدیمی برای سناریوی حساب موجود از helper ساخت Owner مصنوعی بدون trial استفاده می‌کنند؛ تست‌های ثبت‌نام واقعی، جریان OTP را در دیتابیس اختصاصی طی می‌کنند. درگاه/AI تست جعلی‌اند؛ SQL واقعی PostgreSQL است.
