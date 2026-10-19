# حذف ساختار قدیمی پکیجینگ و مالی — 2026-10-19 / ADR-071

با درخواست صریح کاربر، ساختار قبلی پکیج، قیمت‌گذاری، اشتراک، مجوز قابلیت بر اساس خرید، trial، ظرفیت پولی تیم، کیف پول، پرداخت و حسابداری مصاحبه از runtime حذف شد. مدل جایگزین در این تغییر پیاده نمی‌شود. فرانت در ریپوی جداست و بعداً با پرامپت مستقل هماهنگ خواهد شد.

## رفتار فعلی بک‌اند

- ثبت‌نام فقط سازمان/کاربر/نشست می‌سازد؛ هدیه، کیف پول یا اشتراک آزمایشی ندارد.
- دعوت و پذیرش تیم محدودیت ناشی از صندلی خریداری‌شده ندارند؛ نقش Owner/Recruiter و مرز سازمان محفوظ‌اند.
- قالب موضوعی، پیشنهاد سؤال، دعوت، شروع، پردازش، بازیابی و پایان مصاحبه بدون اشتراک، موجودی، quote یا نرخ ارز انجام می‌شوند.
- تحلیل پاسخ، انتخاب تطبیقی، جلوگیری از تکرار موضوع و گزارش به قابلیت پکیج وابسته نیستند؛ قواعد اجرایی سؤال/زمان/lease و validation سرور محفوظ‌اند.
- دسترسی به گزارش و رسانه همچنان scoped و تابع قواعد هویت/ابطال/حذف است؛ مدت نگهداری خریداری‌شده از پکیج حذف شده و زمان انقضای دعوت کارجو همچنان معتبر است. این تغییر سیاست پاکسازی فایل یا دسترسی عمومی رسانه اضافه نمی‌کند.
- نوع مصاحبه فقط `general` یا `specialized` است. کاتالوگ قابل‌ویرایش نوع/تعرفه حذف شد؛ ورودی نوع نامعتبر400 است. فیلد `requiredCapabilityCode` حذف و `interviewType` ورودی صریح اجرای مصاحبه است؛ پیش‌فرض مسیر قدیمی ساخت دعوت `specialized` و ویزارد `general` است.
- قفل تراکنشی سازمان با نام `WithOrganizationLockAsync` حفظ شده است؛ وابستگی مالی ندارد و از رقابت حذف/دعوت/پذیرش/بازیابی جلوگیری می‌کند.

## APIهای حذف‌شده

تمام endpointهای کنترلرهای Pricing، AdminPricing، AdminInterviewTypes، InterviewPricing، Billing و PaymentCallbacks حذف‌اند، شامل `/api/pricing/*`، `/api/admin/pricing/*`، مدیریت نوع مصاحبه ادمین، `/api/interview-pricing/quote`، `/api/billing/*` و callback پرداخت. `GET /api/organization/features` نیز حذف شده است. این مسیرها داده یا رفتار مالی جایگزین ندارند و دیگر در OpenAPI نیستند. فرانت قبلی تا هماهنگ‌شدن نباید آن‌ها را فراخوانی یا قابلیت مصاحبه را با اشتراک/اعتبار gate کند.

خروجی `access.mediaState=Available` در گزارش است؛ expiry مالی `WithinRetention/Expired` دیگر از پکیج محاسبه نمی‌شود. شکل دقیق DTO گزارش همان قرارداد موجود است؛ URLها صرفاً مسیرهای دسترسی scoped هستند و وجود رسانه را تضمین نمی‌کنند.

## دیتابیس و ارتقا

Migration `20261019065637_RemoveLegacyPackagingAndBilling` این۱۹جدول را با داده‌هایشان حذف می‌کند:

`PricingPackages`, `PackagePeriods`, `FeatureDefinitions`, `PackageFeatures`, `InterviewTypes`, `PackageInterviewTypes`, `AiCreditPackages`, `Payments`, `TeamSeatPurchases`, `SubscriptionPurchases`, `SubscriptionEntitlement`, `OrganizationSubscriptions`, `SubscriptionReplacements`, `OrganizationAiWallets`, `AiCreditTransactions`, `AiCreditReservations`, `InterviewBillings`, `RegistrationTrials`, `RegistrationTrialEntitlement`.

FKها، indexهای نوع قیمت‌گذاری، triggerهای متصل به جدول‌ها و۱۱تابع مالی SQL نیز حذف می‌شوند. در `InterviewSessions`، ستون `PricingInterviewTypeCode` حذف و `RequiredCapabilityCode` به `InterviewType` تبدیل می‌شود؛ `hr_interview` به `general` و `technical_interview` به `specialized` می‌رود. کدهای سفارشی قالب‌ها پیش از حذف کاتالوگ به `EngineType` همان نوع تبدیل می‌شوند. سازمان، Identity، تیم، موقعیت، قالب، کارجو، جلسه، پاسخ، گزارش و کلیدهای رسانه حذف نمی‌شوند.

Migration یک‌طرفه است؛ Down به‌طور صریح خطا می‌دهد و بازگشت واقعی فقط با backup قبل از حذف امکان‌پذیر است. migrationها و SQL helperهای قبلی فقط در پوشه Migrations برای زنجیره ارتقای دیتابیس موجود باقی‌اند؛ runtime و مدل EF نهایی جدول مالی ندارند. حذف تاریخچه migrationهای اعمال‌شده عمداً انجام نمی‌شود.

اعمال migration و انتشار مقصد انجام نشده است. اجرای این migration روی مقصد، داده مالی قبلی را عمداً از بین می‌برد؛ پیش از اجرای انتشار باید backup نگه داشت. این سند و ADR-071 بر قراردادها و تصمیم‌های مالی قبلی مقدم‌اند.

## نتیجه بررسی

Build کل solution بدون خطا/هشدار و همه425تست با صفر شکست/Skip، PostgreSQL18 واقعی مستقل و FFmpeg واقعی محلی موفق‌اند. آزمون ارتقای داده‌دار و اجرای دوباره SQL idempotent، حذف schema مالی و حفظ اطلاعات غیرمالی را تأیید کرده است. EF drift و whitespace diff ندارند. نتیجه نهایی: `tests/HireYar.Tests/TestResults/removal-full-recheck.trx`.

حذف از کد و مدل/مسیر ارتقا کامل است؛ حذف جدول‌های دیتابیس مقصد تا اجرای migration انجام نشده است.
