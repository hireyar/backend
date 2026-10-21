## اصلاح فروش — 2026-10-21 / ADR-081

- [x] فقط بک‌اند؛ حذف کامل مشتری از محدوده لغو شد.
- [x] جستجوی نام/موبایل سروری در فهرست فروشنده و مدیریت با حفظ scope و فیلترها.
- [x] پیام دقیق فروشنده دیگر با اولویت حتی پس از خرید؛ audit قبلی محفوظ.
- [x] پاسخ قطعی هدیه/تریال فعال و منقضی: قابل افزودن؛ فقط اشتراک پولی مؤثر فعلی رد می‌شود.
- [x] حفظ ثبت شماره جدید Waiting، حساب بدون خرید Registered و خرید منقضی Won؛ بدون پورسانت تاریخی.
- [x] قفل سازمان قبل از فروش برای جلوگیری از رقابت تأیید خرید و ثبت؛ بدون migration جدید.
- [x] قرارداد API و پرامپت مستقل SALES_CUSTOMER_FIXES_FRONTEND_PROMPT آماده شد؛ نرخ DB و پورسانت مستقل خرید/تمدید محفوظ‌اند.
- [x] build با۰هشدار/۰خطا و۴۶Pass/۰Fail/۰Skip روی PostgreSQL واقعی؛ نتیجه نهایی sales-corrections-verified.trx، fixtureها صفر و cluster موقت متوقف شد.
- [ ] اتصال Angular در ریپوی جدا طبق پرامپت؛ این فهرست مجوز انتشار نیست.

## اصلاح دامنه اعلان‌ها — 2026-10-20 / ADR-080

طبق درخواست جدید کاربر، اعلان‌ها برای همه سازمان‌ها شامل دمو، تریال و بدون اشتراک فعال تولید می‌شوند؛ گیرنده همچنان Owner فعال و تأییدشده همان سازمان است. شرط اشتراک از شروع/پایان مصاحبه و کمبود اعتبار حذف شد. انقضا فقط با پایان پوشش مؤثر آینده و همان آستانه‌های۵/۲روز؛ dedup، scope و حفظ تاریخچه برقرارند. این تصمیم جایگزین محدودیت دمو/تریال ADR-079 است. قرارداد و راهنمای فرانت به‌روز شدند؛ migration جدید لازم نیست. انتشار و تغییر Angular انجام نشده‌اند.

## اعلان‌های داشبورد — محدوده درخواست جاری 2026-10-20

- [x] تأیید کاربر برای SignalR و PostgreSQL فعلی، سرویس ساده داخل همین برنامه.
- [x] اعلان شروع و پایان مصاحبه؛ پایان با target گزارش، بدون backfill تاریخچه.
- [x] یادآوری انقضای۵/۲روزه و اعتبار قابل استفاده<۲ با worker۶۰ثانیه و جلوگیری از تکرار.
- [x] دمو هیچ اعلانی ندارد؛ گیرنده Owner فعال با اشتراک غیر‌دموی همان سازمان.
- [x] API فهرست/شمارنده/read-unread، scope سازمان/گیرنده، migration و تست واقعی PostgreSQL/WebSocket.
- [x] قرارداد NOTIFICATIONS_API و پرامپت NOTIFICATIONS_FRONTEND_PROMPT برای زنگ بالای سمت چپ/dropdown.
- [ ] اتصال UI و کلاینت SignalR در ریپوی جدا Angular طبق قرارداد؛ این مخزن UI ندارد.
- [ ] اعمال migration و انتشار در تسک مستقل؛ تنظیم WebSocket و حذف query توکن از لاگ proxy مقصد. این backlog مجوز push/deploy نیست.

## مدیریت مشتریان و مسیر فروش — 2026-10-20 / ADR-077 و ADR-078

- انجام‌شده بک‌اند با۸۱/۸۱تست مرتبط موفق: فهرست و مدیریت مشتریان/لیدهای موجود، خروجی XLSX، چهار تب مالی/پروفایل/تاریخچه، audit و اصلاحات اداری مستقل از خرید؛ ورود خصوصی فروش با appsettings و بدون کپچا. جزئیات و شواهد STATUS/HANDOFF.
- «مشتری جدید» طبق دستور صریح حذف است؛ ایجاد دستی حساب یا لید، حذف حساب، تغییر نقش/سازمان، و فعال‌سازی/مسدودی حساب از این فرم در محدوده نیستند. ثبت‌نام همان OTP مشتری است.
- باقی فرانت در ریپوی جدا: صفحه بر اساس [ADMIN_CUSTOMERS_FRONTEND_PROMPT](ADMIN_CUSTOMERS_FRONTEND_PROMPT.md)، اتصال به [قرارداد کامل](ADMIN_CUSTOMERS_API.md) و تغییر router ورود فروش به `/salesadmin/{randomkey}` با X-Sales-Entry طبق [SALES_PANEL_FRONTEND_PROMPT](SALES_PANEL_FRONTEND_PROMPT.md)؛ کپچا اضافه نشود. آدرس زنده مقصد هنوز توسط این تسک تغییر نکرده است.
- باقی انتشار مستقل: اجرای migration20261020133348_AddCustomerAdministration پیش از API، تنظیم SalesAdmin.Enabled/RandomKey واقعی در تنظیمات خصوصی محیط و هماهنگی فرانت. SQL آماده artifacts/admin-customer-management-migrations.sql؛ هیچ کلید واقعی در فایل tracked قرار نگرفته. این فهرست مجوز push/deploy یا تغییر مقصد نیست.
## پنل فروش — 2026-10-20 / ADR-076

- انجام‌شده بک‌اند و تأیید محلی: هویت مستقل فروش بدون کپچا و حساب‌سازی توسط سوپرادمین، لید scoped و اتصال ثبت‌نام OTP، پیگیری/بی‌سرانجام/انتقال/audit، پورسانت فقط اشتراک با نرخ هرنیرو، فرم فیلتر تاریخ ثبت‌نام و جمع کل، درخواست واریز غیرتکراری هر سفارش و ردیف مستقل تمدید، بررسی دستی سوپرادمین، migration و محافظ‌های PostgreSQL؛ آخرین۶۳تست یکتای مرتبط موفق و build۰warning/error، شرح HANDOFF.
- باقی در ریپوی فرانت: پیاده‌سازی دو نما و ورود مستقل طبق [SALES_PANEL_FRONTEND_PROMPT](SALES_PANEL_FRONTEND_PROMPT.md) و [SALES_PANEL_API](SALES_PANEL_API.md) و آزمون رابط؛ Angular در این ریپو ساخته نشده است.
- باقی برای انتشار مستقل: اعمال migration20261020121951_AddSalesPanel پیش از API و هماهنگی فرانت؛ SQL قابل بازبینی artifacts/sales-panel-migrations.sql. مقصد، commit/push/deploy/production تغییر نکرده‌اند؛ این فهرست مجوز انتشار نیست.
- مرز فعلی: ثبت نتیجه واقعی واریز توسط ادمین، بدون انتقال بانکی/زمان‌بندی واریز، بدون SMS یادآور یا تنظیم هدیه جدید؛ Returned همان دوره را آزاد نمی‌کند و درخواست بعدی فقط برای پورسانت سفارش تمدید تازه است. این موارد به‌عنوان قابلیت انجام‌شده معرفی نشوند.
## سوابق مصرف مصاحبه — 2026-10-20 / ADR-075

- انجام‌شده بک‌اند: API فهرست/جزئیات scoped فقط Owner، فیلتر و صفحه‌بندی، زمان‌های مصاحبه، مصرف تعدادی قطعی، timeline ماندگار و حفظ سابقه حذف/ارسال مجدد؛ ثبت انقضا توسط worker داخل برنامه، backfill مشخص‌شده با isHistorical و guardهای PostgreSQL.
- تحویل فرانت: [پرامپت مستقل Angular](INTERVIEW_USAGE_HISTORY_FRONTEND_PROMPT.md) و [قرارداد API](INTERVIEW_USAGE_HISTORY_API.md). پیاده‌سازی UI در ریپوی جدا باقی است.
- باقی برای انتشار مستقل: اعمال migration 20261020093013_AddInterviewUsageHistory پیش از نسخه API و اتصال فرانت؛ هیچ‌کدام روی مقصد انجام نشده‌اند و این فهرست مجوز push/deploy نیست.
- اعتبارسنجی تکمیل‌شده: build صفر warning/error و EF بدون drift؛ آخرین نتیجه۱۴۱تست مرتبط Passed، صفر Skip، با139Pass در اجرای گسترده و بازاجرای موفق۵تست شامل دو مورد اصلاح‌شده. PostgreSQL18 واقعی، SQL idempotent دوبار اجرا و rollback/immutability/scope آزموده شدند؛ جزئیات HANDOFF. کل suite اجرا نشده.

## اصلاح کاراکتر صفر در ساخت مصاحبه — 2026-10-19

دو لاگ ارسالی یک شکست SaveChanges در PanelWizardService با SQLSTATE=22021 هستند؛ PostgreSQL متن دارای U+0000 را رد می‌کند. فایل واقعی رزومه/درخواست در دسترس نیست و نام فیلد از parameter $45 به‌تنهایی قابل اثبات نیست. نقص مسیر رزومه با PDF مصنوعی دارای گلیف صفر و resumeText دستی بازتولید شد: هر دو پیش از snapshot از NUL پاک می‌شوند؛ Trim و متن فارسی/نیم‌فاصله/خط جدید محفوظ‌اند. خروجی خالی با400/resume_text_required و متن بیش از32000 با400/invalid_resume_text رد می‌شود. مشخصات هویتی دارای NUL بدون تغییر خودکار نام با400 رد می‌شوند. فایل اصلی رزومه تغییر نمی‌کند؛ schema/migration و قرارداد موفق تغییر ندارند.

اعتبارسنجی: tool restore و restore موفق؛ build کل solution با0Warning/0Error؛ هر31تست مرتبط ResumeTextTests/PanelWizardTests/InterviewContextRulesTests با31Passed/0Failed/0Skipped روی PostgreSQL18 مستقل loopback55550 و FFmpeg واقعی محلی موفق شدند. چهار مورد unit (از جمله PDF با اثبات NUL در خروجی خام PdfPig) و دو regression HTTP جدید، ذخیره متن دستی/PDF و رد متن صرفاً NUL/نام نامعتبر بدون ایجاد جلسه/کارجو را پوشش می‌دهند. providerهای OTP/AI/notification مصنوعی‌اند. شواهد: tests/HireYar.Tests/TestResults/resume-nul-final.trx و artifacts/resume-nul-build.log و artifacts/resume-nul-tests.log. کل suite تکرار نشده است. git diff --check موفق؛ migration، commit/push/deploy، production و Angular تغییر نکرده‌اند. باقی برای مقصد: انتشار مستقل این اصلاح؛ اصالت فیلد معیوب در درخواست واقعی بدون فایل/بدنه اصلی قطعی نیست.

## هدیه سازمان‌های موجود و تخفیف مدت — 2026-10-19 / ADR-074

اعتبارسنجی نهایی ADR-074: build کل solution صفر Warning/Error؛ کل۴۴۳تست با۰Failed/۰Skipped روی PostgreSQL18 مستقل loopback55548 و FFmpeg واقعی محلی موفق شدند. سه regression تازه اعطای یک‌باره هنگام upgrade از schema دارای سازمان خالی/پولی/trial، حفظ قیمت سفارشی/سابقه/نسخه، اجرای دوباره SQL و عدم هدیه۱۰۰تایی به ثبت‌نام بعدی، guard rollback، مصرف سهمیه هدیه/تمدید، درصدهای اولیه/تغییر ادمین/عدم reset با درخواست قدیمی/quote409/ثبات سفارش قبلی را پوشش دادند. providerهای OTP/AI/storage مصنوعی بودند. EF بدون pending model changes و git diff --check موفق؛ SQL ترکیبی در artifacts/commerce-gifts-periods-migrations.sql و TRX در tests/HireYar.Tests/TestResults/commerce-gifts-periods-full.trx. لاگ‌های build/full زیر artifacts/commerce-gifts-periods-* هستند. اجرای مقصد، commit/push/deploy و Angular انجام نشده‌اند.

درخواست جدید: هر سازمان موجود در زمان migration یک ماه تقویمی UTC و۱۰۰مصاحبه اضافه دریافت می‌کند؛ ماه هدیه از پایان اعتبار فعلی یا اکنون شروع و سهمیه قبلی حفظ می‌شود. جدول ExistingOrganizationGrants مستقل از سفارش پرداخت و immutable با PK سازمان، ظرفیت/retention snapshot است. ثبت‌نام بعدی همان۴۸ساعت/۲مصاحبه را دارد. migration GrantExistingOrganizationsMonthAndInterviews آماده است و روی مقصد اجرا نشده.

تخفیف اشتراک۳/۶/۱۲ماه به‌ترتیب۵/۱۰/۱۵درصد، یک‌ماهه۰؛ سه ستون تنظیمی decimal(5,2) در SubscriptionPlans و مدیریت با PUT plan قبلی/RowVersion. PlanDto.periods منبع عمومی درصدهاست؛ تخفیف کل مبلغ اشتراک شامل تیم را سرور quote و در سفارش snapshot می‌کند. migration AddSubscriptionPeriodDiscounts قیمت سفارشی و سفارش قبلی را حفظ و نسخه کاتالوگ را تازه می‌کند. فرانت جدا و مقصد/push/deploy تغییر نکرده‌اند. قرارداد و پرامپت COUNT_BASED_SUBSCRIPTIONS به‌روز شدند؛ عبارت تاریخی بدون تخفیف مدت با این تصمیم جایگزین است.

## نام سازمان در پاسخ سفارش‌ها — 2026-10-19

اعتبارسنجی: build کل solution صفر warning/error؛۱۵/۱۵ تست CountBasedSubscriptionTests روی PostgreSQL18 اختصاصی loopback55546 و FFmpeg محلی با providerهای مصنوعی موفق، صفر Failed/Skipped. regression تازه نام دو سازمان در فهرست/فیلتر، عدم نشت بین سازمان‌ها، rename، جزئیات و retry ایجاد/تأیید را پوشش می‌دهد. TRX: tests/HireYar.Tests/TestResults/order-organization-name.trx؛ build/test logs در artifacts/order-organization-name-*.log. کل suite تکرار نشده؛ migration جدید، commit/push/deploy و تغییر Angular انجام نشده‌اند. کاربر اعلام کرد مایگریشن trial را اجرا کرده و خطای قبلی current/dashboard رفع شده؛ عامل مقصد را تغییر نداده است.

فیلد organizationName به SubscriptionOrderDto اضافه شد؛ ایجاد، جزئیات، فهرست و تأیید سفارش و پاسخ idempotent همگی نام جاری Organizations.Name را می‌دهند. فهرست با JOIN در همان کوئری صفحه خوانده می‌شود و N+1 ندارد؛ فیلتر/صفحه‌بندی و scope سازمانی قبلی حفظ است. نام snapshot خرید نیست؛ rename در خواندن بعدی دیده می‌شود. schema/migration تغییری ندارد. قرارداد API و پرامپت Angular برای نمایش نام در جدول سوپرادمین به‌روز شدند؛ فرانت جداست.

## هدیه ثبت‌نام۴۸ساعته و۲مصاحبه — 2026-10-19 / ADR-073

اعتبارسنجی ADR-073: build کل solution صفر warning/error؛۳۴تست مرتبط متمایز آخرین نتیجه Passed و صفر Skipped دارند، روی PostgreSQL18 مستقل loopback55544 و FFmpeg واقعی محلی با OTP/AI/storage مصنوعی. اجرای signup-trial-final دارای۳۲Passed و۲Fail تست زمان بود؛ پس از اصلاح fixture زمان/JWT (بدون تغییر کد محصول)، هر۲تست در signup-trial-expiry-final با۰Fail پاس شدند. سه regression جدید هدیه یک‌باره۴۸ساعت/۲مصاحبه، مصرف/رزرو/لغو، انقضا و تمدید را پوشش می‌دهند؛ ثبت‌نام rollback و رقابت، پذیرش تیم، dashboard و migration regression نیز اجرا شدند. کل suite تکرار نشده است. EF بدون drift و git diff --check موفق؛ SQL در artifacts/signup-trial-migrations.sql، TRXها در tests/HireYar.Tests/TestResults هستند. migration مقصد، Angular، commit/push/deploy انجام نشده‌اند.

با درخواست صریح کاربر، ثبت‌نام موفق Owner و سازمان جدید در همان تراکنش هدیهٔ یک‌باره می‌دهد:۴۸ساعت دسترسی و۲مصاحبه با قواعد رزرو/مصرف قبلی. ورود/refresh/عضو تیم هدیه نمی‌سازد؛ سازمان‌های قبلی backfill ندارند. SignupTrials مستقل از سفارش پولی است و PK سازمان/قیود/immutable trigger دارد؛ خرید و fulfillment دست‌نخورده‌اند. ظرفیت تیم و retention از کاتالوگ snapshot می‌شوند. سهمیه مصرف‌نشده حفظ و پس از انقضا با تمدید قابل استفاده است؛ تمدید زودهنگام از پایان trial آغاز می‌شود. current/dashboard فیلد isTrial دارند. migration `20261019085750_AddSignupTrial`، قرارداد API، ثبت‌نام و پرامپت Angular به‌روز شدند؛ مقصد/فرانت/push/deploy اجرا نشده‌اند.

## اشتراک واحد و بسته‌های تعدادی — 2026-10-19 / ADR-072

طبق HTML ارائه‌شده و پاسخ صریح کاربر: یک اشتراک ماهانه۹میلیون‌تومان و چهار بسته دانا۱۰/۴میلیون، بینا۲۰/۷٫۵میلیون، ژرفا۵۰/۱۹میلیون و دلخواه۵۰تا۳۰۰ با گام۱۰/هرمصاحبه۴۰۰هزار؛ DB/HTTP ریال. تخفیف‌ها۰/۶٫۲۵/۵/۰درصد‌اند. رزرو هنگام دعوت، مصرف هنگام شروع، آزادسازی دعوت شروع‌نشده، بدون انقضای سهمیه؛ هزینه تیم بالاتر از۳۰ افزایش ندارد؛ خرید فقط با تأیید سوپرادمین فعال می‌شود. سقف۳۰دقیقه و رسانه۶۰روزه با حفظ متن گزارش. قرارداد جاری [COUNT_BASED_SUBSCRIPTIONS_API](COUNT_BASED_SUBSCRIPTIONS_API.md) است. شش جدول جدید و migration AddCountBasedSubscriptions جایگزین ساختار حذف‌شده‌اند؛ داده یا API قدیمی مالی برنگشته است. طبق اعلام کاربر migration حذف قبلی اجرا و کد آن push شده؛ عامل مقصد را بررسی نکرده و migration جدید/انتشار را اعمال نکرده است.

اعتبارسنجی نهایی ADR-072: tool restore و restore موفق؛ build کل solution صفر warning/error؛ کل suite با436Passed/0Failed/0Skipped روی PostgreSQL18 مستقل loopback55542 و FFmpeg واقعی محلی موفق شد.۱۱تست جدید، قیمت/درصد HTML و واحد ریال، حدود و گام دلخواه و سقف هزینه تیم، ثبت بدون اعطای دسترسی و تأیید یک‌باره/مرجع یکتا، ثبات snapshot و نسخه قیمت، scope و guardهای تاریخچه، رقابت رزرو و شروع، لغو/ارسال مجدد/انقضای دعوت، حفظ سهمیه پس از انقضای اشتراک، تمدید زودهنگام و ماه تقویمی UTC با session PostgreSQL در Asia/Tehran،۶۰روز رسانه با حفظ گزارش متنی، شمارنده داشبورد و وضعیت سفارش را پوشش دادند. providerهای AI/OTP/storage صرفاً مصنوعی بودند. baseline خرید تست‌های قدیمی فقط fixture مصنوعی است؛ ثبت‌نام محصول هیچ هدیه/اشتراک/سهمیه خودکاری نمی‌دهد و تست‌های تجاری صریحاً بدون baseline شروع می‌شوند.

EF has-pending-model-changes بدون drift؛ git diff --check موفق. شواهد: `tests/HireYar.Tests/TestResults/count-subscriptions-final.trx`، `artifacts/subscriptions-final.log`، `artifacts/subscriptions-final-build.log` و SQL delta idempotent `artifacts/count-based-subscriptions.sql`. اجرای متمرکز اول۵Pass/۲انتظار نامناسب تست FK/رقابت شروع داشت که اصلاح شدند؛ اجرای کامل بعدی۴۳۴Pass/۱Fail فقط summary فارسی OpenAPI داشت که اضافه شد. بررسی آخر مرز ماه، guard را مستقل از TimeZone و منطبق بر UTC کرد و regression مستقل اضافه شد؛ نتیجه۴۳۶تایی نهایی مرجع است.

پرامپت کامل فرانت در [COUNT_BASED_SUBSCRIPTIONS_FRONTEND_PROMPT](COUNT_BASED_SUBSCRIPTIONS_FRONTEND_PROMPT.md) آماده است؛ Angular در این ریپو پیاده نشده است. migration جدید `20261019082300_AddCountBasedSubscriptions` روی مقصد اجرا نشده و commit/push/deploy/production تغییر نکرده‌اند. باقی برای انتشار: اجرای migration جدید و هماهنگی فرانت طبق قرارداد؛ این یادداشت مجوز اجرای خودکار انتشار نیست.

## حذف ساختار قدیمی پکیجینگ و مالی — 2026-10-19 / ADR-071

با درخواست صریح کاربر، کد و APIهای پکیج/تعرفه/اشتراک/trial/صندلی پولی/کیف‌پول/پرداخت/حسابداری مصاحبه حذف شدند. ثبت‌نام، تیم، مصاحبه و گزارش بدون وابستگی مالی کار می‌کنند؛ scope و نقش/هویت محفوظ‌اند. migration `RemoveLegacyPackagingAndBilling` حذف۱۹جدول و۱۱تابع SQL، حذف وابستگی نوع قیمت‌گذاری و تبدیل نوع اجرایی تاریخی را انجام می‌دهد؛ Down فقط از طریق backup ممکن است. مدل جایگزین و فرانت جدا در این تسک نیستند. قرارداد جاری: [PACKAGING_REMOVAL](PACKAGING_REMOVAL.md). بخش‌های مالی قدیمی پایین، سابقه تاریخی‌اند. اعمال migration مقصد، push و deploy انجام نشده‌اند.

اعتبارسنجی نهایی حذف — 2026-10-19: tool restore و restore موفق؛ build کل solution صفر warning/error؛ کل suite با425Passed/0Failed/0Skipped روی PostgreSQL18 مستقل loopback55540 و FFmpeg واقعی محلی موفق شد. providerهای OTP/AI/storage مصنوعی بودند. تست جدید حذف API/OpenAPI/schema، ثبت‌نام و دعوت۷عضو بدون کاتالوگ مالی، ارتقای دیتابیس قدیمیِ دارای داده مالی و قالب سفارشی، حفظ جلسه/کارجو، حذف۱۹جدول/۱۱تابع، اجرای دوباره SQL idempotent و منع rollback ناقص را بررسی کرد. EF بدون pending model changes و git diff --check موفق‌اند. شواهد: `tests/HireYar.Tests/TestResults/removal-full-recheck.trx`، `artifacts/removal-full-recheck.log`، `artifacts/removal-verified-build.log` و SQL بازبینی `artifacts/remove-legacy-packaging-billing.sql`.

اجرای اولیه423تست،402Pass/14Fail/7Skip داشت؛ انتظارهای تاریخی endpoint مالی/retention/rollback و جداسازی تنظیمات محلی آزمون امنیت اصلاح شدند. اجرای متمرکز35تست،32Pass/3Fail داشت؛ دو انتظار مسیر حذف‌شده و سناریوی تاریخی worker اصلاح و suite کامل425تایی موفق شد. یک اجرای بعدی پیش از شروع تست به خطای راه‌اندازی GC خورد و اجرای دیگری با توقف فرایندهای محیط نیمه‌کاره ماند؛ اجرای نهایی با GC محدود و تک‌پردازشی نتیجه معتبر نهایی است. migration مقصد، Angular، مدل قیمت‌گذاری جدید، commit/push/deploy و production تغییر نکرده‌اند. کلاستر آزمایشی اختصاصی پس از پایان تست‌ها متوقف و حذف شد.

باقی‌مانده انتشار ADR-070: اعمال migration NormalizePackageInterviewTypes روی مقصد و اتصال رابط Angular سوپرادمین/انتخابگر نوع به DYNAMIC_PACKAGE_INTERVIEW_TYPES_API. بک‌اند و 117 تست مرتبط موفق‌اند؛ سه تست صوتی FFmpeg در این محیط تنظیم نشده‌اند.

به‌روزرسانی 2026-10-18 / ADR-070: انواع مصاحبه و تعرفه پکیج نرمال شدند. InterviewTypes کاتالوگ داینامیک و PackageInterviewTypes رابطه چندنوعی با قیمت USD و وضعیت مستقل هستند؛ ستون‌های قیمت عمومی/تخصصی از PricingPackages حذف می‌شوند. نرخ اولیه صریح کاربر عمومی0.03 و تخصصی0.05 دلار/دقیقه است؛ پایه عمومی و پیشرفته هر دو نوع. quote و رزرو از رابطه DB می‌خوانند و خروجی زنده ریالی است. مدیریت نوع/قیمت/وضعیت با API مستقل سوپرادمین، نسخه همزمانی، قالب و دعوت با کد داینامیک و راهبرد اجرایی عمومی/تخصصی موجود تکمیل شد. صورتحساب/رزرو قبلی ثابت؛ رابط Angular و migration/انتشار مقصد انجام نشده‌اند. قرارداد جاری: [DYNAMIC_PACKAGE_INTERVIEW_TYPES_API](DYNAMIC_PACKAGE_INTERVIEW_TYPES_API.md). این تصمیم جایگزین ADR-069 است.

## دو قیمت مستقل HR/تخصصی داخل پکیج — 2026-10-18 / ADR-069

- تکمیل بک‌اند تأییدشده: HR موجود و تخصصی مستقل در همان PricingPackages؛ انتخاب قیمت با پکیج و نوع در preview و رزرو؛ ادمین و کاتالوگ دو قیمت؛ basic فقط HR و advanced هر دو قابلیت. build صفر warning/error و مدل بدون drift؛ آخرین نتیجه۱۲۷تست متمایز مرتبط موفق (۱۲۵موفق اولیه و بازاجرای۲/۲ تست اصلاح‌شده، صفر Failed/Skipped نهایی). تصمیم «نرخ مشترک هر دو نوع» قبلی جایگزین است.
- باقی تنظیم داده/انتشار: migration AddPackageSpecializedInterviewPrice و ثبت قیمت تخصصی هر پکیج در خود DB/تنظیمات ادمین؛ مقدار اولیه عددی از کاربر دریافت نشده و هیچ حدس یا کپی HR انجام نشده است. null یعنی تنظیم نشده و برای تخصصی409 دارد. روی مقصد اعمال نشده؛ backlog مجوز push/deploy نیست.
- اتصال Angular جدا: نمایش چهار فیلد نوعی کاتالوگ، quote با templateId/interviewType/packageId، درخواست تازه با تغییر هرکدام، همان نوع در multipart دعوت، نمایش جداگانه403/قیمت‌تنظیم‌نشده و کمبود اعتبار؛ دو ورودی USD در ادمین. پرامپت کامل در RIAL_AI_WALLET_API و تصمیم در ADR-069 است.

## تاریخی؛ جایگزین‌شده با ADR-069 — قیمت اصلی پکیج، نوع برای دسترسی — 2026-10-18 / ADR-068

- تکمیل بک‌اند: منبع یگانه AiPricePerMinuteUsd موجود همان پکیج؛ packageId اختیاری یا پکیج اشتراک فعال، interviewType برای قابلیت؛ کاتالوگ و رزرو جدید هماهنگ. build صفر warning/error، بدون model drift و۱۲۵/۱۲۵ تست مرتبط موفق با صفر Failed/Skipped. مدل تعرفه مستقل حذف و migration افزایشی UseOriginalPackageAiPrice آماده است. ADR-067 و کار باقی اتصال به فیلدهای نوعی قبلی جایگزین شدند.
- باقی اتصال فرانت جدا: quote با templateId/interviewType/packageId انتخابی؛ مصرف aiPricePerMinuteUsd/Rial اصلی کاتالوگ، پاک‌کردن پاسخ دیررس و نمایش عدم دسترسی403، ارسال همان نوع در دعوت، نرخ پایدار۵دقیقه‌ای؛ پرامپت کامل RIAL_AI_WALLET_API. تغییر نوعِ مجاز در همان پکیج نرخ را عوض نمی‌کند؛ تغییر پکیج قیمت همان پکیج را می‌گیرد.
- باقی انتشار: migration20261018113051_UseOriginalPackageAiPrice جدول جدا را حذف می‌کند و قیمت اصلی/تاریخچه را تغییر نمی‌دهد. روی مقصد اجرا نشده؛ backlog مجوز push/deploy نیست. بدهی۸تست downgrade تاریخی قبلی همچنان خارج این درخواست است.

## تاریخی؛ جایگزین‌شده با ADR-068 — تعرفه دیتابیس بر اساس نوع مصاحبه — 2026-10-18

- تکمیل بک‌اند: پاسخ کاربر ابهام یادداشت قبل را رفع کرد؛ InterviewTariffs با seed عمومی۰٫۳ و تخصصی۰٫۸ دلار، ورودی interviewType همراه packageId، بررسی دسترسی پکیج، کاتالوگ دو تعرفه و snapshot رزرو واقعی؛ build صفر warning/error و آخرین نتیجه۱۲۵تست متمایز مرتبط موفق (۱۰۵موفق اولیه، سپس۴۸/۴۸ موفق شامل تست اصلاح‌شده و موارد تازه؛ جزئیات در STATUS/HANDOFF). ADR-067 و RIAL_AI_WALLET_API مرجع جاری‌اند.
- باقی اتصال: فرانت جدا باید نوع و پکیج انتخابی را هم‌زمان در quote بفرستد، همان نوع را در multipart دعوت بفرستد، پاسخ دیررس را کنار بگذارد و چهار فیلد نوعی کاتالوگ را مصرف کند. تغییر پکیج لزوماً مبلغ را عوض نمی‌کند چون تعرفه نوع مشترک است. انتشار نیازمند migration AddInterviewTypeTariffs است؛ در این تسک روی مقصد اجرا نشده است.
- بدهی تست کشف‌شده:۸تست downgrade قدیمی در suite گسترده، از latest پس از cutover برگشت می‌خواهند و به guard غیرقابل‌برگشت PersistRialAiCredit برخورد می‌کنند. fixture این تست‌ها باید از schema پیش از cutover شروع شود؛ مجوز حذف guard یا بازنویسی داده مالی نیست. admin API مدیریت تعرفه جدید در این درخواست اضافه نشده است.

## ابهام قیمت پکیج/نوع مصاحبه — 2026-10-18

- تشخیص: quote نوع را از قالب می‌خواند؛ basic برای قالب تخصصی403/package_feature_required دارد. مدل فعلی تعرفه واحد هر پکیج است؛ تعرفه مستقل HR/تخصصی در آن نیست.
- منتظر پاسخ کاربر: صرفاً نمایش مقایسه با وضعیت عدم پشتیبانی، یا مدل تعرفه جدا و اثر بر محاسبه واقعی. هیچ‌کدام از نقل‌قول فرانت خودکار اجرا نشد؛ کد و مقصد تغییر نکردند.

## قیمت پکیج انتخابی — 2026-10-18

- تکمیل: packageId اختیاری در quote دعوت؛ پیش‌فرض قرارداد فعال، ورودی قیمت پکیج عمومی انتخابی، scope/قابلیت/خطاهای صریح و حفظ رزرو واقعی؛ ADR-066 و RIAL_AI_WALLET_API. build صفر warning/error و۸۴/۸۴ تست مرتبط موفق، صفر Failed/Skipped.
- باقی: اتصال query در Angular جدا برای انتخاب/حذف انتخاب پکیج، لغو درخواست قدیمی و برچسب پیش‌نمایش. انتشار در تسک مستقل؛ migration تازه لازم نیست و backlog مجوز push/deploy نیست.

## نرخ پنج‌دقیقه‌ای و fallback — 2026-10-18

- تکمیل بک‌اند: refresh خودکار۵دقیقه، cache/gate مشترک،200 با آخرین قیمت در خطای provider، retry اولیه در سرور تا موفقیت، metadata مشترک quote دعوت و حفظ snapshot؛ ADR-065 و RIAL_AI_WALLET_API مرجع‌اند. build صفر warning/error و۸۱/۸۱ تست مرتبط موفق، صفر Failed/Skipped.
- باقی: فرانت جدا باید polling/countdown را از refreshAfterSeconds=300 و ExpiresAtUtc پاسخ بگیرد و برای isStale پیام خطا/دکمه retry فوری نسازد؛ انتشار API/Angular در تسک مستقل. schema/migration تازه ندارد؛ backlog مجوز push/deploy نیست.

## اعتبار واقعی ریالی و رزرو هنگام دعوت — 2026-10-17

با تصحیح صریح کاربر، مدل قبلی معادل ریالیِ متغیر جایگزین شد: کیف پول/ledger/شارژ/هدیه/محصول اعتبار واقعی IRR و ثابت هستند. نرخ فقط هنگام ساخت دعوت اخذ و همراه قیمت ریالی هر دقیقه و حداکثر مدت snapshot و رزرو می‌شود؛ شروع بدون نرخ تازه و بدون رزرو دوم، تسویه واقعی و آزادسازی باقی‌مانده، لغو/انقضا و جایگزینی اتمیک. لندینگ پویا باقی است. GET /api/interview-pricing/quote فقط JWT پنل Owner/Recruiter، scoped قالب و refresh60ثانیه؛ کارجو هیچ نرخ/مبلغ نمی‌بیند. نام تأمین‌کننده از DTO نرخ حذف شد. حلقه کوچک شمارش معکوس در پرامپت Angular مستند شد؛ UI در ریپوی جداست.

طبق درخواست بعدی، مصرف USD فقط در DB: InterviewBillings.BilledAmountUsd و AiCreditTransactions.AmountUsd (منفی برای مصرف) نگه داشته می‌شود؛ DTOهای usage/history/transactions/invoice آن را ارسال نمی‌کنند. نرخ/بدهی/رزرو ریالی snapshot مستقل‌اند و با تغییر نرخ ارز بازحساب نمی‌شوند. API شارژ و گزینه RegistrationTrial اکنون CreditAmountRial دارند؛ appsettings هدیه۵۰۰هزارریال است. مبالغ نقدی درگاه قبلیIRT و خروجیHTTP آنIRR باقی‌اند. قیمت ریالی دقیقه/دقایق تخمینی در داشبورد بدون quote معتبر null هستند؛ از quote فرم دعوت استفاده شود.

Migration20261017195309_PersistRialAiCredit با نرخ cutover صحیح ریال/دلار صریح در همان session برای داده قدیمی لازم است؛ بدون آن rollback می‌شود. اصل USD مصرف تاریخی حفظ، ledger/Balance/reservations با ضریب واحد تبدیل و سازگاری checked، توابع و triggerهای مالی با نام ستون‌های تازه هماهنگ‌اند. rollback ارز با Down ممنوع و نیازمند backup است. migration مقصد/انتشار و فرانت انجام نشده‌اند. قرارداد جاری و پرامپت: [RIAL_AI_WALLET_API](RIAL_AI_WALLET_API.md)؛ اسناد قبلی به آن ارجاع می‌دهند.
## ثبت‌نام مستقل — 2026-10-17

- پیاده‌شده: endpoint ثبت‌نام مستقل با OTP/ایمیل/نام/نام خانوادگی و معرف اختیاری، ورود فقط حساب موجود، حفظ trial/هدیه و پذیرش تیم، migration nullable/guardDown و قرارداد REGISTRATION_API/ADR-063؛ build صفر warning/error و۳۱/۳۱ تست نهایی مرتبط موفق؛ آخرین نتیجه هر۵۰۲ شناسه تست suite+بازاجرا Passed است. جزئیات اجراها در STATUS/HANDOFF ثبت شده‌اند.
- باقی برای محصول: اتصال دو فرم Angular در ریپوی جدا و اعمال migration20261017110941 پیش از انتشار API، در تسک انتشار مستقل. این درخواست مجوز push/deploy نیست.
- آینده و اجرا‌نشده: اعتبارسنجی کد معرف/ارتباط معرف/پاداش ریفرال و تأیید ایمیل. در حال حاضر فقط کد ورودی ثبت‌نام ذخیره می‌شود؛ حساب‌های تاریخی backfill نمی‌شوند.

## ترمیم موردی تأییدشده گزارش تاریخی — 2026-10-17

با تأیید صریح کاربر برای ارسال دوباره همان یک مصاحبه به provider فعلی Liara، marker فقط همان جلسه از سازمان خوانده‌شده سرور فعال و سرویس‌های موجود بازیابی/روایت اجرا شدند؛ هیچ worker سراسری در ابزار نگهداری شروع نشد و تاریخچه دیگر opt-in نشد. کاربر migration را اعمال کرده بود؛ اتصال پایدار و ثبت 20261017093755_AutomateFutureReportCompletion در همان DB به‌صورت خواندنی تأیید شد؛ عامل migration یا deploy اجرا نکرد.

خروجی واقعی read model سازمانی: knowledge_depth/communication دارای مقدار و Available، job_alignment دارای مقدار و Partial به‌دلیل پاسخ‌های فاقد شاهد، narrative=Available و processing=Idle. عدد، متن، شناسه یا نقل‌قول واقعی کارجو در Git ثبت نشده است. snapshot نمره/شاهد/بودجه اصلی و سابقه تصمیم انسانی پیش و پس از پردازش برابر بود؛ نمره‌های اصلی بازنویسی نشدند. کمبود صریح شواهد با عدد حدسی پر نشد. تولید روایت با یک تلاش موفق و cache/lease موجود انجام شد؛ ابزار اولیه در پایان، lease روایت را Processing دید و بررسی خواندنی بعدی موفقیت را تأیید کرد؛ درخواست موازی موفق بازتولید نشد.

ابزار تک‌موردی ignored در artifacts/approved-single-report-repair، بدون جعل Owner یا چاپ secret/متن کارجو، build صفر warning/error داشت؛ read model نهایی verification_exit=0. اتصال ابتدا قطع و ابزار خواندنی قبلی معطل rollback بود؛ فقط همان process با مسیر بررسی‌شده متوقف و پس از اعلام کاربر اتصال دوباره برقرار شد. هیچ کد محصول در این ادامه تغییر نکرد؛ نتیجه suite قبلی۴۹۴/۴۹۴ محفوظ و suite بدون تغییر کد تکرار نشد. git diff --check موفق. این نتیجه جایگزین وضعیت «منتظر تأیید/ترمیم نشده» در یادداشت قبلی است؛ commit/push/deploy و بازپردازش سایر مصاحبه‌ها انجام نشده‌اند.
## تکمیل خودکار گزارش — 2026-10-17

بازیابی شاخص مفقود/خراب و تولید روایت پس از ارزیابی پاسخ‌ها به worker موجود وصل شد؛ فقط پایان‌های جدید opt-in می‌شوند و migration تاریخچه را false نگه می‌دارد. polling تا پایان شاخص/روایت ادامه دارد؛ GET بدون AI/write، endpoint دستی Owner، سقف تلاش و نمره معتبر محفوظ‌اند. [AUTOMATIC_REPORT_COMPLETION_API](AUTOMATIC_REPORT_COMPLETION_API.md) قرارداد جاری است. تشخیص خواندنی گزارش تاریخی:۱۰ارزیابی موفق answer-quality/1، بدون dimensions و narrative؛ ترمیم داده مقصد انجام نشده. طرح پردازش کل تاریخچه توسط auto-review رد و دامنه محدود شد. migration/انتشار مقصد و push انجام نشده؛ اعتبارسنجی نهایی: build صفر warning/error؛ کل suite۴۹۴/۴۹۴ Passed، صفر Failed/Skipped روی PostgreSQL18 اختصاصی loopback55528 و FFmpeg واقعی، providerهای صرفاً ساختگی، موفق شد. پنج regression تازه، تکمیل بدون GET، تقدم ارزیابی اولیه، بازیابی مستقل، historical opt-out/سازمان/حذف، سقف تلاش و رقابت دستی/خودکار را پوشش می‌دهند. نتیجه tests/HireYar.Tests/TestResults/automatic-report-full.trx است. اجرای مرتبط اولیه۹۰موفق/۳شکست داشت؛ فرض قدیمی تعداد تماس worker و آخرین migration، و انتظار نمره برای سؤال بی‌پاسخ اصلاح شدند؛ suite کامل پس از اصلاح موفق شد. EF بدون drift؛ default=false تاریخچه و اجرای دوباره script در fixture واقعی تأیید شدند. SQL آماده بازبینی artifacts/automatic-future-report-completion.sql؛ به مقصد اعمال نشده است. صفر DB fixture باقی بود و کلاستر اختصاصی متوقف/حذف شد؛ git diff --check موفق. تشخیص مورد واقعی فقط خواندنی بود؛ هیچ پاسخ واقعی به AI ارسال یا گزارش مقصد بازنویسی نشد. ترمیم موردی گزارش تاریخی منتظر تأیید صریح کاربر برای ارسال مجدد به Liara است؛ auto-review بازپردازش کل تاریخچه را رد کرده بود. commit/push/deploy انجام نشده‌اند.
## فیلتر آخرین تصمیم انسانی مصاحبه — 2026-10-17

`GET /api/interviews` پارامتر چندانتخابی humanDecisions با reject/advance/review و خروجی humanDecision nullable دارد؛ آخرین تصمیم انسان از هر دو مسیر ثبت، مستقل از AI/وضعیت انجام، قبل از total/صفحه‌بندی و scoped سازمان فیلتر می‌شود. قرارداد و کار لازم Angular جدا در [INTERVIEW_DECISION_FILTER_API](INTERVIEW_DECISION_FILTER_API.md) است. schema/migration تازه، push/deploy یا تغییر مقصد ندارد. اعتبارسنجی نهایی: build صفر warning/error؛۲۶/۲۶ تست مرتبط PanelWizard/Pagination/تصمیم انسانی و نسخه گزارش، صفر Failed/Skipped، روی PostgreSQL18 اختصاصی loopback55527 موفق شدند. دو regression تازه فیلتر/ورودی نامعتبر و assertion مسیر review نسخه‌دار، هر سه تصمیم، تغییر انتخاب با ترتیب قطعی زمان یکسان، رویداد جدید کارجو، null، ترکیب فیلترها، total/page، scope/مجوز و حذف را پوشش می‌دهند. نتیجه tests/HireYar.Tests/TestResults/interview-decision-filter.trx؛ suite کامل تکرار نشد. git diff --check موفق؛ صفر DB fixture باقی بود و کلاستر اختصاصی متوقف/حذف شد. Angular، push/deploy و داده مقصد تغییر ندارند.
## تکمیل داده‌های باقی‌مانده گزارش — 2026-10-17

اعتبارسنجی نهایی این تغییر: build صفر warning/error؛ کل suite۴۸۷/۴۸۷ Passed، صفر Failed/Skipped، روی PostgreSQL18 اختصاصی loopback55526 و FFmpeg واقعی موفق شد؛ نتیجه tests/HireYar.Tests/TestResults/report-completion-verified.trx است. ۲۲case تازه برای علت null/بازیابی مستقل و بودجه/حفظ شاخص موفق، provider روایت، شواهد و سابقه صریح، هر دو نوع گزارش، مجوز دو نقش، version/review snapshot، stale input/lease/cache و migration اضافه شد. fixtureهای AI/OTP/storage ساختگی‌اند؛ مدل واقعی و گزارش مقصد smoke نشده‌اند. تست‌های نقش/روایت/Swagger پس از اصلاح در suite نهایی نیز موفق‌اند. دو نسخه HTML/JSON و PNG در artifacts/report-completion-preview از API و renderer واقعی با داده صرفاً آزمایشی تولید و بصری بازبینی شدند.

اجرای کامل اولیه۴۸۰موفق/۴شکست داشت: URL جاافتاده در fixture provider تازه، فرض قدیمی «آخرین migration متعلق به تیکت است» و دو مسیر محیطی FFmpeg Production. URL fixture اصلاح، guard تیکت به migration خودش هدف‌گذاری و FFmpeg فقط در محیط تست تنظیم شد. اجرای بعدی۴۸۶موفق/یک شکست داشت؛ پیام تشخیصی تست Swagger روشن کرد شرح دو اکشن قدیمی AdminUsers.List/Revoke نیز غایب بوده؛ صرفاً شرح آن‌ها و دو endpoint تازه گزارش تکمیل شد و رفتار ادمین تغییر نکرد. تست‌های قدیمی policy با رفتار404 پذیرفته‌شده و خروجی policy مخفی هماهنگ شدند؛ تست فساد policy در سطح داخلی و guard واقعی محفوظ است. قاعده fingerprint نیز metadata نمایشی بازیابی را حذف می‌کند تا Owner/Recruiter نسخه و روایت مشترک ببینند؛ تولید روایت هنوز فقط Owner است.

EF has-pending-model-changes بدون drift؛ migration در fixture واقعی اعمال، script آن دوبار idempotent اجرا و downgrade داده‌دار باP0001 رد شد. SQL افزایشی برای بازبینی در artifacts/complete-report-evidence-and-narrative.sql تولید شد و به مقصد اعمال نشده است. صفر دیتابیس fixture باقی ماند و کلاستر اختصاصی متوقف/حذف شد؛ git diff --check موفق. push/deploy/production/تنظیمات خصوصی/داده مقصد تغییر ندارند. برای بررسی علت همان گزارش واقعی، شناسه مصاحبه/داده تشخیصی آن هنوز لازم است. برای استفاده محصول، migration و اتصال Angular به endpointهای صریح بازیابی/روایت لازم‌اند؛ GET خودبه‌خود رکورد تاریخی یا متن AI تولید نمی‌کند.

در ادامه درخواست کاربر، تناسب محاسبه‌شده در خروجی و section12 بازگشت؛ expectedLevel شمارش مستند سطح سؤال پاسخ‌دار در برابر target snapshot است، بدون درصد حدسی. دلیل null شاخص‌ها به تفکیک سؤال/کد در metrics.issues روشن شد؛ بازیابی مستقل Owner با سه‌تلاش و حفظ نمره/تلاش اصلی اضافه شد. روایت AI مستقل، پیشنهاد تمرکز مرحله بعد، دو سؤال تکمیلی و نقل‌قول سابقه اعلام‌شده با منبع دقیق، تولید صریح و cache/lease/fingerprint ماندگار دارند؛ GET همچنان بدون AI/write است. روایت وارد نسخه و snapshot تصمیم انسانی می‌شود و تصمیم قبلی پس از تغییر روایت نیازمند بازبینی است. [REPORT_COMPLETION_API](REPORT_COMPLETION_API.md) قرارداد جاری و جایگزین بخش داده‌ای کمبودهای قبلی است. علت null در یک گزارش واقعی بدون شناسه/داده تشخیصی همان جلسه تأیید نشده؛ رفع عمومی علت‌های قابل بازتولید، اثبات ترمیم رکورد مقصد نیست. migration20261017083027 لازم، مقصد/فرانت/commit/push/deploy تغییر نکرده‌اند؛ نتیجه نهایی بالاتر ثبت شده است.
## تطبیق نمای HTML گزارش با فایل مرجع — 2026-10-17

اعتبارسنجی نهایی این تغییر: build موفق با صفر warning/error؛۱۶/۱۶ تست مرتبط، صفر Failed/Skipped، شامل۵تست HTTP گزارش روی PostgreSQL18 اختصاصی loopback55525 و۱۱تست محاسبه/خروجی/قالب، موفق شدند. پنج case جدید قالب دو نوع، null/رادار ناقص، XSS و hash اسکریپت چاپ را پوشش می‌دهند. نتیجه tests/HireYar.Tests/TestResults/report-template-final.trx؛ suite کامل تکرار نشد. پیش‌نمایش HTML هر دو نوع از JSONهای ساختگی موجود و renderer جاری تولید و تصاویر Chrome headless هر دو نما بازبینی شد؛ فونت fallback است. ابزار اولیه exec/node/CUA خطای sandbox داشت؛ خواندن/اجرای مجاز shell و رندر headless محلی انجام شد. صفر دیتابیس fixture باقی بود؛ کلاستر اختصاصی تست متوقف و حذف شد. git diff --check موفق؛ commit/push/deploy و داده مقصد تغییر نکردند.

نمای عمومی و تخصصی /report/print از CSS و چیدمان فایل ارسالی بازسازی شد و فقط داده ثبت‌شده SessionReportDocument را نمایش می‌دهد. سؤال‌ها details native، نمودارها وابسته به مقدار واقعی، null نامعلوم، داده escape و PDF با چاپ مرورگر و اسکریپت ثابت hash‌شده CSP هستند. [REPORT_TEMPLATE_PARITY](REPORT_TEMPLATE_PARITY.md) نگاشت و فهرست کامل کمبودهاست. Angular در این ریپو نیست؛ دکمه‌های پخش/بازگشت/تصمیم و تغییر نوع جلسه غیرفعال‌اند، سابقه کار/فونت/توصیه روایی AI/سلامت کامل ضبط/شاخص مستقل سطح مورد انتظار ساخته نشده‌اند. PositionFit/policy حذف‌شده از خروجی عمومی بازگردانده نشدند؛ کد جاری نسبت به یادداشت قدیمی «۱۲ بخش قابل ویرایش» متفاوت است و سرویس فعلی۱۱بخش/PUT policy404 دارد. schema/migration/provider/مقصد و push/deploy تغییر ندارند.

اعتبارسنجی نهایی2026-10-16: build صفر warning/error؛22/22 تست AdminManagementTests/AdminAuthTests/AdminCaptchaTests/PaginationTests موفق، صفر شکست/Skip، PostgreSQL18 اختصاصی loopback55523 و provider/CAPTCHA ساختگی. چهار regression تازه شامل بازیابی JWT در مرورگر تازه، جستجو/صفحه‌بندی/مجوز/لغو و رقابت حذف متقابل‌اند. نتیجه tests/HireYar.Tests/TestResults/admin-management-verified.trx؛ suite کامل تکرار نشد. اجرای اولیه در sandbox به اتصال loopback دسترسی نداشت؛ اجرای مجاز با DB اختصاصی موفق شد. git diff --check موفق؛ schema/migration، Angular، انتشار و commit/push تغییر نکردند.
## مدیریت سوپرادمین و جستجوی کاتالوگ — 2026-10-16

GET /api/admin/users/super-admins با page/pageSize/search و Page<AdminUserDto> اضافه شد؛ جستجوی نام/موبایل، نمایش وضعیت فعال/تأیید موبایل/LockoutEnd، بدون رمز یا stamp. DELETE /api/admin/users/super-admins/{id} فقط امتیاز سوپرادمین را لغو و security stamp را rotate می‌کند؛ حساب Owner و سازمان حذف/غیرفعال نمی‌شوند. خودحذفی400 cannot_revoke_self، هدف ناموجود/غیرادمین404، موفق204؛ احراز مستقل SystemAdmin موجود و no-store محفوظ. قفل دو کاربر با ترتیب Id و recheck مدیر درخواست‌کننده مانع حذف متقابل همزمان هر دو مدیر است. افزودن با POST موجود؛ تغییر رمز/پروفایل/تعلیق حساب API تازه ندارد.

دو GET مدیریتی admin/pricing/packages و admin/pricing/ai-credit-packages پارامتر search اختیاری با حداکثر256 دارند؛ Trim، case-insensitive substring، در پکیج Name/Code/Description و در اعتبار Name. شرط پیش از Count/Skip/Take در SQL اجرا می‌شود؛ total تعداد منطبق است؛ جستجوی خالی همه، % و _ متن عادی، ترتیب SortOrder/Id محفوظ. کاتالوگ عمومی تغییر ندارد؛ migration لازم نیست.

رفرش سوپرادمین: طراحی جاری عمداً JWT پانزده‌دقیقه‌ای (قابل تنظیم1..30) بدون refresh/cookie و نگهداری فقط در حافظه فرانت دارد؛ بند4 قرارداد SUPER_ADMIN_API همین را الزام کرده است. رفرش حافظه را پاک می‌کند؛ API توکن موجود را بر اثر رفرش باطل نمی‌کند. GET me علاوه بر Bearer، X-Admin-Entry می‌خواهد؛ نبود آن404 است و نباید به‌اشتباه به انقضای JWT تعبیر شود. Angular در این ریپو نیست و رفتار مقصد بررسی نشده. حفظ نشست پس از رفرش نیازمند بازنگری نگهداری/بازیابی فرانت است؛ احراز و مدت نشست بک‌اند در این تغییر عوض نشده‌اند.

فرانت: جدول از GET بالا، افزودن با POST موجود، دکمه لغو با DELETE و سپس fetch دوباره؛ برای ردیف حساب جاری دکمه حذف غیرفعال. فیلتر جستجو page را1 کند و total پاسخ را مصرف کند. توکن/رمز در لاگ یا analytics ثبت نشود. انتشار/فرانت/commit/push انجام نشده‌اند.

## صفحه‌بندی فهرست‌های ضروری — 2026-10-16

پیاده‌سازی محلی بانک سؤال و دو فهرست مدیریتی پکیج اشتراک/اعتبار AI انجام شد؛ صفحه‌بندی‌های قبلی حفظ و binding پارامتر PageQuery اصلاح شد. اعتبارسنجی نهایی: tool restore/restore موفق؛ build صفر warning/error؛ suite مرتبط۱۲۵/۱۲۵ و اجرای جداگانه پنل۹/۹ موفق، صفر شکست/Skip، روی PostgreSQL18 اختصاصی loopback55519 و FFmpeg واقعی، providerهای ساختگی. شش تست BillingPanel در هر دو اجرا مشترک‌اند؛۱۲۸ شناسه یکتای موفق داریم. دو regression جدید، بیش از۱۰۰رکورد، ترتیب دارای تساوی، total، defaults، ورودی نامعتبر، صفحه خالی/شماره بزرگ، scope/مجوز و query binding مسیرهای قبلی را بررسی کردند. نتیجه‌ها pagination-final.trx و pagination-panel.trx در TestResults هستند؛ suite کامل کل پروژه تکرار نشد. اجرای اولیه۱۱۱موفق/۴شکست داشت: دو شکست prefix binding اصلاح شد و دو fixture Production با مسیر FFmpeg محیطی بازاجرا شدند؛ runtime/config نامرتبط تغییر نکرد. باقی: اتصال paginator و تغییر مصرف آرایه به Page<T> فقط برای سه GET جدید، مطابق پرامپت [PAGINATION_API](PAGINATION_API.md)، در ریپوی Angular جدا؛ انتشار مستقل. فهرست قابلیت‌ها/اشتراک‌های آینده و کاتالوگ عمومی در این درخواست تغییر نکردند. این یادداشت مجوز push/deploy نیست.

## تیکتینگ و پیوست اروان — 2026-10-16

اعتبارسنجی: build صفر warning/error،۱۱/۱۱ بازاجرای نهایی و آخرین outcome همه۴۵۳تست موجود موفق؛ توضیح اجرای کامل/بازاجراها در HANDOFF ثبت است.

بک‌اند ثبت و گفت‌وگوی سازمانی، پاسخ/مدیریت سراسری سوپرادمین، وضعیت نسخه‌دار، اولویت، pagination و دانلود خصوصی با quota و محدودیت upload پیاده است؛ [TICKETING_API](TICKETING_API.md) و ADR-056 مرجع‌اند. فایل فقط در باکت تازه خصوصی hireyar-ticketing است؛ DB metadata و کلید دارد. ساخت باکت و smoke ساختگی واقعی انجام و شیء تست حذف شد؛ نام پس از رد ticketing و تأیید کاربر انتخاب شد.

باقی برای محصول: ساخت UI سازمان/سوپرادمین در Angular جدا، اعمال migration20261015205303 و تنظیم TicketStorage محیط سرور پیش از انتشار در تسک مستقل. عملیاتی: least-privilege credential، پایش ظرفیت و رسیدگی به orphanهای خصوصیِ crash/outage با تطبیق metadata؛ signature validation آنتی‌ویروس نیست و در صورت نیاز اسکن قرنطینه باید جدا طراحی شود. این فهرست مجوز خودکار مراحل دیگر، نشر یا تغییر production نیست.


## بررسی عملیاتی خطای ارزیابی — 2026-10-16

بررسی read-only مقصد نشان داد نصب migration جدید به‌تنهایی موفقیت مدل را تضمین نمی‌کند: جلسه کامل و transcript موجود می‌تواند با شکست همه assessmentها، امتیاز/پروفایل فاقد داده داشته باشد. در اتصال محلی تنظیم‌شده به provider، probe کاملاً ساختگی HTTP402 با نشانه خطای اعتبار/موجودی برگشت؛ تنظیمات runtime سرور از این probe قابل اثبات نیست. داده واقعی، متن پاسخ و secret در این یادداشت ثبت نشده‌اند؛ هیچ رکورد مقصد، تنظیمات سرور یا deployment تغییر نکرد.

نقص تشخیصی تأییدشده در کد: HTTP402 و سایر ردهای دائمی provider به assessment_provider_rejected عمومی تبدیل می‌شوند؛ Provider/Model نیز فقط در موفقیت ذخیره می‌شوند. Failed خودکار دوباره اجرا نمی‌شود؛ retry دستی حداکثر سه تلاش کل دارد، پس retry پیش از رفع علت می‌تواند بودجه را تمام کند. کار باز: تفکیک کدهای امن اعتبار/احراز هویت/مدل، نمایش علت عملیاتی روشن، و طراحی بازیابی کنترل‌شده پس از رفع تنظیمات بدون دورزدن بی‌صدای سقف تلاش. این نوبت صرفاً تشخیص است؛ کد اجرایی تغییر نکرده و build/test suite مجدد اجرا نشده است.

## تکمیل تمام بخش‌های بک‌اند گزارش — 2026-10-15

کار روی dev با حفظ تمام تغییرات uncommitted اصلاح readiness قبلی ادامه یافت. با دستور صریح «همه بخش‌ها»، تمام۱۲بخش جدید به داده واقعی وصل شدند؛ دیگر بخش جاری NotStarted/NotImplemented وجود ندارد و HasUnimplementedSections=false است. امتیاز تخصصی میانگین وزن‌دار مهارت‌ها، عمومی میانگین پاسخ‌های نمره‌دار، تناسب براساس سطح هدف snapshot و پوشش، پروفایل موضوعی/شاخص‌ها، توزیع نمرات، summary محاسباتی، قوت/بهبود با شاهد و dataGaps مستقل پیاده شدند. مقادیر فاقد شاهد null/InsufficientEvidence و نمره ناقص Partial است؛ این‌ها نتیجه معتبرِ کمبود داده‌اند، نه قابلیت باقی‌مانده.

Policy مالک با revision، آستانه‌ها/وزن‌های صریح و fallback امن JSON خراب اضافه شد. POST report/review تصمیم انسانی را با requestId idempotent، کنترل fingerprint در قفل سازمان، scope/Owner و snapshot نمره/پوشش/تناسب/policy ذخیره می‌کند؛ سابقه، تصمیم قدیمی بدون نسخه، و تغییر گزارش پس از بررسی آشکارند. APIهای مشترک قبلی حفظ‌اند. GET همچنان pure read، بدون مدل/صف جدید یکپارچه است؛ محاسبات/خلاصه قطعی‌اند و شکست یک پاسخ کل گزارش را پنهان نمی‌کند.

answer-quality/2 با همان provider و مدل مستقل، rubricهای۳شاخص تخصصی و۷شاخص عمومی را به همان پاسخ و excerptهای سرور متصل می‌کند. خطای یک شاخص فقط همان را Unavailable می‌کند؛ نمره اصلی محفوظ است. شاخص‌های HR صرفاً شواهد شغلی صریح‌اند؛ ویژگی شخصیتی/صفات حفاظت‌شده یا تناسب فرهنگی جمعیتی استنباط نمی‌شود. نتیجه موفق قبلی بازتولید نمی‌شود؛ نداشتن شاخص در داده قدیمی صریحاً InsufficientEvidence است. Deadline/سه‌تلاش/lease/fencing/retry۲۰۲ نوع‌دار حفظ‌اند.

چاپ کامل RTL از ابتدا با تمام بخش‌ها، جدول/رادار/نوارهای واقعی، فرمول‌ها، پوشش، سؤال/پاسخ/شاهد و سابقه انسانی نوشته شد؛ null صفر رسم نمی‌شود و HTML escape/CSP محفوظ‌اند. دو نمونه کاملاً ساختگی HTML و JSON از renderer و builder واقعی در artifacts/comprehensive-report-specialized.* و comprehensive-report-general.* تولید شدند. مرورگر ابزار به دلیل خطای kernel/sandbox اجرا نشد؛ QA بصری screenshot تأیید نشده است، ولی تولید HTML و تست متن/ساختار/XSS/شواهد موفق‌اند. تماس واقعی provider یا داده واقعی کارجو برای smoke انجام نشد؛ تست‌های مدل synthetic/HTTP handler هستند.

Migration افزایشی20261015165557_CompleteSessionReports ستون شاخص‌ها، fingerprint/snapshot بررسی و SessionReportPolicies را اضافه می‌کند؛ history/مالی/نمره موفق حذف نشده‌اند. Down در صورت داده جدید با P0001 متوقف می‌شود. SQL idempotent در artifacts/complete-session-reports.sql آماده است؛ مقصد migrate نشده، push/deploy/production انجام نشده است. Angular خارج این ریپو باید [COMPREHENSIVE_REPORT_API](COMPREHENSIVE_REPORT_API.md) و JSON نمونه را متصل کند؛ تمام بخش‌های بک‌اند و چاپ اینجا تکمیل‌اند.

Build نهایی صفر warning/error. کل suite443/443 Passed، صفر Failed/Skipped روی PostgreSQL18 اختصاصی loopback55509 و FFmpeg واقعی موفق شد: artifacts/test-results/comprehensive-report-final/comprehensive-report-final.trx. اجرای اول442موفق/یک شکست داشت؛ guard افزایشی به‌جای P0001 مورد انتظار مسیر downgrade مشترک،23514 داده بود. guard جدید با P0001 هماهنگ شد و هیچ assertion مشترک رسانه ضعیف یا حذف نشد. Snapshot بررسی انسانی نیز افزوده و model snapshot/designer همسان شد؛ تست واقعی HasPendingModelChanges=false و migration idempotent دوبار اجراشدن و امتناع از downgrade داده‌دار موفق‌اند. پس از صریح‌ترشدن عبارت شواهد شغلی در prompt، build و27تست نهایی provider/protocol/calculation نیز موفق شدند (comprehensive-protocol-final.trx)؛ این شمارش با suite همپوشانی دارد. نه تست تازه برای فرمول‌های متفاوت عمومی/تخصصی، target-level fit، شواهد ناقص، fingerprint، شاخص خراب، هر دو نوع گزارش واقعی، scope/revision/Owner و audit migration افزوده شد. تست‌های قبلی lease/deadline/fencing/worker/media/قالب/مالی محفوظ و موفق‌اند. صفر fixture DB باقی بود؛ کلاستر تست و سرور preview موقت متوقف و حذف شدند.

## پیگیری گزارش فرانت — 2026-10-15

- [x] علت NotStarted ثابت بخش‌های ساخته‌نشده مشخص شد؛ وضعیت NotImplemented و metadata صریح polling در بک‌اند اضافه شد.
- [x] response موفق retry در Swagger به202 همراه DTO واقعی اصلاح شد.
- [ ] Angular جدا: توقف timer براساس processing.shouldPoll، نمایش NotImplemented/Paused بدون spinner سراسری.
- [x] پیاده‌سازی واقعی امتیاز کل/تناسب/پروفایل‌ها و جمع‌بندی/قوت/بهبود/بخش بررسی انسانی طبق دستور تکمیل همه بخش‌ها انجام شد؛ COMPREHENSIVE_REPORT_API مرجع جاری است.

## رفع انتظار بی‌پایان نمایشی گزارش و Swagger retry — 2026-10-15

شروع از dev تمیز. گزارش فرانت در کد تأیید شد: score/positionFit/profiles/summary/strengthsAndImprovements/humanReview در خروجی SessionReportService مقادیر ثابت NotStarted بودند و هیچ job برایشان وجود نداشت. این بررسی شاهد خرابی یک job مشخص در مقصد نیست؛ DB/لاگ مقصد خوانده نشد. تحلیل جامع هنوز پیاده نشده است و با انتظار آماده نمی‌شود.

اصلاح محدود: این بخش‌ها و DTOهای کلی متن/امتیاز/notes اکنون NotImplemented با پیام صریح «در صف نیست» دارند؛ پروفایل نامرتبط NotApplicable محفوظ. sections[].shouldPoll، questions[].assessment.isPending و processing در ریشه اضافه شدند. فقط Queued/Processing/RecoveryPending ارزیابی پاسخ با WorkerEnabled روشن، Pending/shouldPoll=true و pollAfterSeconds>=3 می‌سازد. با worker خاموش، Paused/assessment_worker_disabled و بدون timer؛ بدون کار ناتمام، Idle. Idle معادل گزارش جامع تکمیل‌شده نیست؛ HasUnimplementedSections صریح است. GET همچنان read-only و بدون AI است؛ موتور ارزیابی و سقف/recovery/lease دست‌نخورده‌اند. چاپ پیام readiness را نمایش می‌دهد.

RetryAssessment اکنون ActionResult<RetryAnswerAssessmentResponse> و ProducesResponseType صریح202 دارد. wire shape قبلی interviewId/answerId/assessment، Location، scope، Owner و رفتار idempotent حفظ شدند؛ موفقیت200 نامشخص از مستندات حذف می‌شود. قرارداد کامل و راهنمای Angular در [REPORT_READINESS_API](REPORT_READINESS_API.md)، تصمیم ADR-054 است. Angular در این ریپو نیست و باید NotImplemented/processing.shouldPoll/Paused را مصرف کند؛ تغییر بک‌اند به تنهایی spinner کد فرانت را بازنویسی نمی‌کند. امتیاز کل/fit/profiles/summary/strengths/improvements و اتصال بررسی انسانی در گزارش همچنان کار مراحل بعدند؛ API مشترک review/decisions موجود حفظ است.

Build نهایی صفر warning/error و کل suite 434/434 Passed، صفر Failed/Skipped، روی PostgreSQL18 اختصاصی loopback55508 و FFmpeg واقعی موفق شد؛ artifacts/test-results/report-readiness/report-readiness.trx. چهارده case جدید شامل ماتریس pending/terminal، Paused، عدم polling برای بخش پیاده‌نشده، body/Location واقعی202، نبود200 و وجود schema کامل در OpenAPI تولیدشده و worker واقعی فعال تا پایان ارزیابی است. در تست hosted واقعی، ارزیابی Succeeded و polling تمام شد درحالی‌که بخش‌های NotImplemented همچنان بدون pending باقی ماندند؛ worker پیش از حذف DB اختصاصی متوقف شد. تست‌های قبلی lease/deadline/fencing/scope/media نیز در suite محفوظ و موفق‌اند. بررسی model drift موجود در suite موفق است؛ schema/migration تغییر ندارد. در پایان صفر DB fixture باقی بود و کلاستر موقت متوقف و حذف شد.

Schema/migration/تنظیمات مقصد تغییر نکردند و migration تازه لازم نیست؛ زنجیره migration مراحل قبل محفوظ است. push/deploy/production و داده واقعی مقصد دست نخورده‌اند. Backlog مجوز اجرای خودکار مراحل بعد نیست.

## گزارش جدید، مرحله دوم — 2026-10-15: ارزیابی مستقل پاسخ‌ها

شروع از dev تمیز پس از اعلام push مرحله اول توسط کاربر؛ موتور قبلی بازنگشت. AnswerReportAssessment/AnswerAssessmentService/Protocol/Worker/Provider و DTO مستقل اضافه شدند. worker جلسه Completed و پاسخ نهایی دارای transcript را از PostgreSQL کشف می‌کند؛ بازکردن گزارش یا تولید یکپارچه لازم نیست. GET /report فقط خواندن داده پایه و نتایج ذخیره‌شده است؛ assessment هر سؤال وضعیت/تلاش/علت/شاهد خود را دارد. چاپ جدید نیز نمره پاسخ، توضیح و شواهد را encode می‌کند. امتیاز کل/fit/پروفایل/summary هنوز پیاده نشده‌اند.

هر پاسخ حداکثر سه تلاش؛ transient با فاصله۵/۳۰ثانیه، deadline پیش‌فرض۷۵ و HTTP۶۰ثانیه. lease و AttemptId جلوی commit تلاش دیررس/قدیمی را می‌گیرند؛ hash ورودی قبل commit دوباره کنترل می‌شود. expiry در GET فقط نمایش RecoveryPending/Failed دارد و worker بعد restart بازیابی می‌کند. Provider غیرهمکار با WaitAsync محدود و fault دیررس observe می‌شود. خطای خروجی/شبکه/پیکربندی/غیرمنتظره کد ثابت امن دارد؛ هیچ متن پاسخ/خروجی/exception message در لاگ جدید نیست. POST retry مخصوص Owner و بدون reset سقف، تکراری idempotent، نتیجه موفق/InsufficientEvidence بازتولید نمی‌شود.

سرور قطعات همان پاسخ و شناسه/offset/quote شواهد را می‌سازد؛ مدل فقط شماره قطعه را انتخاب می‌کند. اثر خارج بازه یا مرجع نامعتبر فقط همان پاسخ Failed است. کمبود شواهد InsufficientEvidence/null و خرابی شاهد ذخیره‌شده Unavailable/null است. کیفیت معنایی مدل واقعی بررسی نشده؛ همه تماس‌های تست مصنوعی با fake/HTTP handler بوده‌اند. قرارداد مالی، org scope، رسانه/retention، مصاحبه زنده و نمره لحظه‌ای تغییر کاربرد نداشته‌اند.

Migration 20261015083205_AddAnswerReportAssessments فقط جدول مستقل و FK/check/indexهای آن را اضافه می‌کند؛ روی مقصد اعمال نشده است. SQL idempotent از migration بلافاصله قبل در artifacts/add-answer-report-assessments.sql ساخته شده. [ANSWER_ASSESSMENT_API](ANSWER_ASSESSMENT_API.md) قرارداد کامل حالت‌ها/کدها/فرانت/config/retry است. در provider منتخب AnswerAssessmentModel و AnswerAssessmentTimeoutSeconds مستقل افزوده شدند؛ تنظیمات محرمانه و مدل‌های زنده حفظ شده‌اند. Angular در ریپوی جدا باید وضعیت‌های هر پاسخ، polling و retry را متصل کند. push/deploy/production انجام نشده‌اند.

Build نهایی صفر warning/error. suite کامل 419/419 Passed، صفر Failed/Skipped، PostgreSQL18 اختصاصی loopback55507 و FFmpeg واقعی موفق شد (artifacts/test-results/answer-assessment/answer-assessment.trx). پس از اضافه‌شدن یک تست fencing/cancellation و حذف GET از setup تست کشف worker، build و اجرای مرتبط 72/72 نیز موفق بود (artifacts/test-results/answer-assessment-final/answer-assessment-final.trx)؛ این دو اجرا همپوشانی دارند و مجموع آن‌ها تعداد تست یکتا نیست. 32 case جدید پروتکل/provider/flow در خروجی نهایی وجود دارد. تست‌ها شامل همزمانی claim و retry، حفظ نتیجه موفق کنار خروجی نامعتبر، timeout غیرهمکار، نتیجه دیررس، lease منقضی و سقف تلاش سوم، backoff، تغییر ورودی حین پردازش، FK چندسازمانی، خرابی شواهد ذخیره، InsufficientEvidence و کشف worker بدون GET هستند. migration افزایشی دوبار idempotent اجرا و model drift صفر شد؛ تست حذف جدول گزارش قبلی نیز با هدف صریح همان migration حفظ شد. خطای compile میانی به‌علت تداخل نام AnswerAssessment با DTO مسیر زنده بود و entity جدید AnswerReportAssessment نام گرفت؛ کد زنده عوض نشد. پس از تست صفر DB fixture باقی بود و کلاستر موقت متوقف و حذف شد.

## ترتیب ادامه گزارش جدید — پس از مرحله اول 2026-10-15

- [x] حذف موتور قبلی و ارتباط تولید گزارش با completion/billing؛ قرارداد و چاپ مستقل اطلاعات پایه.
- [x] طراحی ۱۲ بخش و وضعیت صریح تحلیل انجام‌نشده؛ migration محدود حذف نتیجه قدیمی.
- [x] مرحله دوم: ارزیابی مستقل هر پاسخ با شواهد متعلق به همان پاسخ، وضعیت/بازیابی محدود و ماندگار هر بخش.
- [x] مرحله سوم: محاسبات قطعی، امتیاز با پوشش معلوم، پروفایل‌ها و تناسب/نمودار.
- [x] مرحله چهارم: جمع‌بندی مستند، قوت/بهبود و بخش جدید بررسی انسانی.
- [ ] Angular در ریپوی جدا: نمایش تمام بخش‌ها از اولین پاسخ، بدون spinner سراسری یا صفر ساختگی، اتصال schema جدید؛ چاپ/PDF نهایی و QA بصری.
- [ ] انتشار مستقل با توقف نسخه قدیمی، اعمال migration و اجرای API جدید. backlog مجوز push/deploy یا اجرای خودکار مرحله بعد نیست.

## بازسازی مستقل گزارش — 2026-10-15: مرحله اول

با درخواست صریح کاربر، موتور گزارش قدیمی حذف و طراحی از ابتدا آغاز شد. سرویس/DTO، prompt/validator/source resolver/metric catalog/lifecycle، worker/صف، روش report در provider، چاپ و entity/config گزارش حذف شده‌اند؛ هیچ‌یک در گزارش جدید استفاده نمی‌شوند. تصمیم‌های تاریخی 050/051 و اسناد evaluation/v3/lifecycle بازنشسته‌اند؛ [SESSION_REPORT_API](SESSION_REPORT_API.md) و ADR-052 مرجع جاری‌اند.

SessionReportService و SessionReportsController و SessionReportPage مستقل‌اند: GET /api/interviews/{id}/report و /report/print. قرارداد session-report/1 با ۱۲ بخش، اطلاعات قطعی جلسه را بدون AI یا write در GET می‌دهد؛ Completed برابر BaseReady و سایر جلسه‌ها InterviewIncomplete است. سؤال‌های واقعاً پرسیده‌شده، پاسخ/تعامل، آمار واقعی، رسانه خصوصی/retention و رویدادهای خام کارجو خوانده می‌شوند. تحلیل‌ها NotStarted، امتیاز/اطمینان/جمع‌بندی null؛ پروفایل نامرتبط NotApplicable. Failed transcript یا timeline خراب/رسانه منقضی کل گزارش را حذف نمی‌کند. نبود telemetry برابر Unknown است، نه سلامت. BaseReady به معنی ارزیابی کامل نیست.

مسیر قدیمی scoped410 می‌دهد؛ POST آن فقط Owner است. قرارداد مالی و tenant scope محفوظ؛ review/decision/events/recording/media و تست‌های مشترک آن‌ها حفظ شده‌اند. فقط تست‌های حذف‌شده مربوط به موتور/قرارداد بازنشسته بودند؛ تست تاریخچه زنده به InterviewHistoryTests منتقل شد. شش تست جدید پایه جایگزین رفتار جدید شدند؛ تست‌های تیم/trial به خواندن قرارداد جدید منتقل شدند.

Migration 20261015080151_RemoveLegacyEvaluationReports فقط InterviewEvaluationReports را DROP می‌کند. Down جدول خالی می‌سازد؛ بازیابی نتیجه ندارد. migrationهای قبلی دست‌نخورده‌اند. برای انتشار ابتدا تمام نسخه‌های قبلی API/worker متوقف، سپس migration، سپس API جدید اجرا شود. Angular باید مسیر/schema را تغییر دهد؛ repo حاضر Angular ندارد. SQL delta idempotent فقط این حذف در artifacts/remove-legacy-evaluation-reports.sql تولید شده است. DB مقصد، push/deploy و production در این تغییر دست نخورده‌اند.

Build نهایی صفر warning/error؛ suite نهایی 388/388 Passed، صفر Failed/Skipped، روی PostgreSQL18 اختصاصی loopback55505 و FFmpeg محلی واقعی موفق شد: artifacts/test-results/report-redesign-final/report-redesign-final.trx. شش تست جدید شامل دسترس‌پذیری بدون AI، tenant/auth/NoStore/410، STT failed، timeline خراب/retention، escape چاپ و migration حذف محدود/idempotent است. SQL migration در تست اختصاصی دوبار اجرا شد، جدول قدیمی حذف و جلسه/رویداد محفوظ ماند؛ model drift صفر. اجرای اول 386 موفق/دو خطای عنوان OpenAPI و مسیر POST در تست تیم داشت؛ نگاشت عنوان و انتظار مسیر بازنشسته اصلاح و کل suite دوباره موفق شد. build همزمان میانی به‌علت قفل DLL توسط testhost ناموفق بود؛ build نهایی پس از اتمام تست، مستقل و موفق اجرا شد. هیچ تست مشترک ضبط/تصمیم/رویداد برای عبور حذف نشد. پس از پایان، صفر DB fixture باقی بود و cluster اختصاصی متوقف و حذف شد.

نمونه چاپ با داده کاملاً ساختگی در artifacts/session-report-preview.html از renderer جدید تولید شد؛ مرورگر ابزار با خطای kernel راه نیفتاد، بنابراین QA بصری تأیید نشده است. تست امنیت چاپ/XSS موفق شد. هیچ داده واقعی/secret در فایل جدید یا لاگ ثبت نشد. مستندات پاک‌سازی قبلی پایین محفوظ‌اند.

## پاک‌سازی داده‌های مصاحبه — 2026-10-14: انجام‌شده با تأیید صریح محدوده

پس از رد اولیه automatic approval review، کاربر صریحاً حذف برای تمام سازمان‌های DB متصل و اقلام دقیق اعلام‌شده را تأیید کرد. همان طرح با این مجوز دوباره بررسی و اجرا شد؛ رد قبلی دور زده نشد. محدوده شامل موقعیت، قالب، مصاحبه، درخواست کارجو، پاسخ/معرفی/شواهد/snapshot/گزارش/تصمیم/metadata رسانه، سؤال و بانک/ارتباط مهارت/مهارت، کش صوت و رکوردهای وابسته رزرو/تسویه مصاحبه بود. حذف صرفاً DB است؛ فایل‌های صوت/ویدیو/رزومه روی storage بیرون DB حذف نشدند.

در یک transaction با lock timeout و statement timeout و قفل هدف، نبود رزرو Active/billing Reserved و trigger ناشناخته TRUNCATE دوباره بررسی شد. فقط22جدول صریح، بدون CASCADE، تغییر schema یا حذف حساب‌ها، TRUNCATE شدند.25موقعیت،32قالب و69مصاحبه همراه داده‌های وابسته پاک شدند؛ تمام22جدول پیش از COMMIT صفر بودند. داده مالی حفظ‌شده به‌صورت row equality با EXCEPT ALL داخل تراکنش قبل/بعد مقایسه و برابری پرداخت، ledger، کیف‌پول/موجودی، اشتراک، خرید اشتراک و trial تأیید شد. raw داده فقط در temp table همان transaction بود و باCOMMIT حذف شد؛ credential/اطلاعات شخصی/متن واقعی در فایل یا Git/لاگ ثبت نشدند.

COMMIT موفق؛ آزمون read-only روی اتصال تازه خالی بودن تمام22جدول را تأیید کرد.10کاربر و9سازمان باقی‌اند؛ schema/index/FK/trigger، تنظیمات و کد runtime تغییر نکردند. Build تازه صفر warning/error موفق شد؛ آزمون مرتبط این عملیات، کنترل واقعی PostgreSQL پیش/پس از COMMIT بود. suite .NET دوباره اجرا نشد؛431/431 قبلی متعلق به اصلاح گزارش است و به‌عنوان آزمون تازه پاک‌سازی ادعا نمی‌شود.

STATUS/BACKLOG/HANDOFF به‌روز شدند. commit/push/deploy یا تغییر برنچ production انجام نشد؛ تغییر داده DB همان درخواست صریح جدید بود. داده‌های پاک‌شده از این DB قابل خواندن نیستند؛ حساب/پرداخت/موجودی بازنشانی یا بازپرداخت نشد. گزارش خطادار قدیمی نیز همراه سایر مصاحبه‌ها حذف شد، ولی این عملیات دلیل رفع کیفیت مدل نیست. ادامه محصول از داده مصاحبه تازه آغاز می‌شود؛ حذف فایل‌های storage یا تغییر runtime نیازمند درخواست مستقل است.

## درخواست پاک‌سازی داده‌های مصاحبه — 2026-10-14: متوقف تا تعیین محدوده

کاربر حذف داده‌های قالب، موقعیت و مصاحبه را خواست. DB متصل فقط‌خواندنی بررسی شد؛ FKها حذف پاسخ، گزارش، snapshot، داده کارجو، بانک سؤال/ارتباط مهارت و رکوردهای رزرو/تسویه مصاحبه را لازم می‌کنند. رزرو Active یا billing Reserved در snapshot بررسی وجود نداشت. طرح حذف اتمیک جدول‌های مشخص، بدون CASCADE یا تغییر schema و با مقایسه قبل/بعد پرداخت، ledger، کیف‌پول و اشتراک آماده شد؛ خام داده، شناسه اشخاص یا credential در فایل ثبت نشد.

Automatic approval review اجرای حذف همه سازمان‌ها و جدول‌های مشترک را به دلیل نبود تأیید صریح محدوده cross-tenant و اقلام وابسته رد کرد. فرمان حذف اصلاً اجرا نشد؛ هیچ داده‌ای حذف/تغییر نکرد و COMMIT انجام نشد. سؤال تأیید محدوده تمام سازمان‌ها در برابر یک سازمان و اقلام دقیق حذف به کاربر ارسال شد. ادامه فقط پس از پاسخ صریح کاربر؛ رد ایمنی نباید با SQL یا مسیر اجرای جایگزین دور زده شود. حساب‌ها/سازمان‌ها، پرداخت‌ها، تراکنش و موجودی کیف‌پول و اشتراک هدف حذف نیستند. فایل‌های رسانه بیرون DB در طرح فعلی حذف نمی‌شوند.

این نوبت تغییر کد/schema، build/test جدید، commit/push/deploy یا تغییر production ندارد؛ بررسی SQL واقعی صرفاً read-only و شمارش/روابط بود. وضعیت بررسی‌های قبلی گزارش مستقل است و پاک‌سازی مشکل مدل را رفع‌شده ثابت نمی‌کند.

## HY-301 — حذف دو مرجع حقیقت در شواهد گزارش — 2026-10-14

source_reference_conflicting_fields پس از repair، شرط کنترل‌شده مقایسه فیلدهای اضافی مدل با sourceRef سرور بود. اصلاح محدود انجام شد: reference معتبر فقط به داده اصلی سرور تبدیل و quote/answerId مدل کنار گذاشته می‌شوند؛ داده ساختگی persist نمی‌شود. source ناشناخته/مبهم/interaction یا پاسخ دیگر و سایر کنترل‌های validator محفوظ‌اند. report-v3.4 با schema عمومی قبلی؛ migration/فرانت ندارد، صف/repair/timeout/cache/config ثابت‌اند.

Build صفر warning/error؛ suite کامل431/431 موفق، صفر Failed/Skipped با PostgreSQL و FFmpeg واقعی محلی؛ report-authoritative-full.trx. حفاظت قابلیت‌های قبلی با اجرای کل suite بررسی شد. باقی: انتشار مستقل API و retry گزارش واقعی؛ تضمین همه خروجی‌های مدل یا صحت معنایی داده واقعی ادعا نمی‌شود. DB/config/model مقصد، push/deploy/production تغییر نکردند.

## HY-301 — repair محدود خروجی گزارش — 2026-10-14

source_reference_invalid مقصد از مدل فعلی Luna، احتمال نقص شکلی یا reference ناشناخته را تأیید کرد؛ زیرعلت خروجی قبلی موجود نیست. پذیرش فقط معادل‌های قابل اثبات و فیلدهای اضافی دقیقاً منطبق، فهرست صریح مرجع‌ها و یک repair خودکار داخل همان claim با اعتبارسنجی کامل پیاده شدند. حداکثر دو تماس در بودجه مشترک، بدون تکرار HTTP failure/تغییر مدل/حدس شاهد؛ شکست نهایی Failed است. report-v3.3 با schema عمومی قبلی، بدون migration/فرانت؛ ADR-051 مرجع است.

build صفر warning/error و145/145 تست مرتبط PostgreSQL واقعی موفق، صفر Failed/Skipped؛ smoke عمومی Luna معتبر و smoke تخصصی با reference عمداً خراب، در تماس دوم اصلاح و معتبر شد؛ داده‌ها مصنوعی‌اند. باقی: انتشار مستقل و retry گزارش Failed توسط Owner و بررسی کیفیت واقعی؛ هیچ گزارش یا تنظیم مقصد عوض نشد، push/deploy/production انجام نشد. تمام خروجی‌های LLM تضمین‌شده نیستند و شواهد ساختگی موفق اعلام نمی‌شوند.

## HY-301 — رفع ارجاع نامعتبر شواهد شاخص‌ها — 2026-10-14

لاگ واقعی metric_citation_invalid پس از HTTP200 علت شکست گزارش را مشخص کرد؛ تغییر report-v3.1 فقط محافظ پردازش/تشخیص را کامل کرده بود. اصلاح report-v3.2 با sourceRef/answerRef محلی و حل قطعی به متن شاهد و answerId اصلی پیاده شد؛ کنترل‌های strict و schema عمومی v3، cached، نمره و lifecycle حفظ شدند. build صفر warning/error؛137/137 تست مرتبط PostgreSQL واقعی موفق، صفر Skip/Failed و دو smoke داده مصنوعی با Liara/Gemini واقعی عمومی/تخصصی معتبر شدند. ADR-050 و قرارداد چرخه گزارش به‌روزند؛ migration و تغییر فرانت لازم نیست.

باقی: انتشار مستقل API، retry گزارش‌های Failed توسط Owner و ارزیابی کیفیت معنایی گزارش‌های واقعی. مرجع نامعتبر یا JSON خراب همچنان شکست امن دارد؛ موفقیت تمام خروجی‌های مدل ادعا نمی‌شود. DB مقصد فقط خوانده شد؛ retry واقعی، config/model مقصد، push/deploy/production تغییر نکردند و backlog مجوز اجرای آن‌ها نیست.

## HY-301 — محافظ پردازش گزارش و تشخیص شکست — 2026-10-14

مقاوم‌سازی بک‌اند Verified محلی: معیار مشترک lease غایب/منقضی و سقف۱۵۰ثانیه از start؛ persist بازیابی در GET status، polling مستقل recovery، deadline۱۳۰ثانیه شامل مراحل پس از claim و WaitAsync برای provider بی‌اعتنا به لغو. مالکیت lease، cached، retry فقط پس از Failed و محافظ‌های شواهد محفوظ‌اند. promptVersion=report-v3.1 و reason ثابت امن اعتبارسنجی اضافه شدند؛ schema/report-v3 و مدل/provider ثابت، migration تازه ندارد. build صفر warning/error و125/125 تست مرتبط PostgreSQL واقعی موفق، صفر Failed/Skipped؛ report-safety-verified.trx.

باقی این مسئله: انتشار مستقل و بررسی status مستقیم/پنل Angular؛ metadata نمونه مقصد هنگام بررسی Failed در Validating بود. علت دقیق رد خروجی قبلی با metadata موجود تعیین نمی‌شود؛ بررسی reason امن تلاش بعدی نسخه جدید و کیفیت مدل واقعی باقی است. retry، تغییر داده یا تنظیمات مقصد، تماس AI واقعی، push/deploy/production انجام نشد. این فهرست مجوز انتشار یا بازتولید خودکار تاریخچه نیست.

## قیمت پویا کاربران تیم — 2026-10-14

بک‌اند مجاز Verified محلی: سیاست پویای هرپکیج۳/۵، نرخ۲۰۰۰۰۰تومان ماهانه، proration۳۰روزه، رایگان‌شدن پس از۳۰، snapshot، checkout TeamSeats، کنترل دعوت/پذیرش و ظرفیت تمدید/ارتقا، فاکتور/تاریخچه آماده‌اند. Build صفرwarning/error و suite کامل۴۰۱/۴۰۱ موفق، صفرFailed/Skipped روی PostgreSQL واقعی؛ team-seat-verified.trx. SQL delta دوبار، legacy حفظ‌شده و EF drift=false تأیید شدند. [TEAM_SEAT_BILLING_API](TEAM_SEAT_BILLING_API.md) و ADR-049 مرجع‌اند.

باقی نیازمند تأیید صریح: سیاست کنترل سراسری دسترسی اعضای فعلی در کسری ظرفیت؛ بازبینی خودکار اعمال gate در احراز هویت را به دلیل احتمال قفل کاربران و نبود مجوز رد کرد. حالت عضوی که پس از خرید اشتراک آینده کم‌ظرفیت فعال می‌شود صریحاً باز است؛ تکمیل enforcement ادعا نمی‌شود. باقی محصول: اتصال سه تنظیم پکیج و quote/checkout/تاریخچه تیم در Angular جدا، درگاه واقعی، migration20261014060524 و انتشار مستقل API؛ این backlog مجوز اجرای آن‌ها نیست.

## پیگیری صدای قبل از شروع و پیام‌های Gemini — 2026-10-13

بررسی بک‌اند محلی و دو smoke مصنوعی واقعی Gemini، تولید خوشامدگویی/معرفی MP3 را تأیید کرد؛ 61 تست مرتبط بدون شکست/Skip. runtime تغییر نکرد. audio-check فقط STT دارد، نه صدای نمونه اسپیکر؛ منبع tts-hd پیش از شروع در فرانت/asset/cache یا مسیر دیگری نامعلوم است. Welcome/معرفی به GET stageAudioUrl با Authorization و پخش صریح فرانت نیاز دارند؛ TTS در current/start برای این مراحل اجرا نمی‌شود. خاموش بودن هر دو فلگ snapshot معرفی، Welcome را طبق سیاست قبلی رد می‌کند.

باقی برای تعیین علت گزارش‌شده: محل اجرا محلی/منتشرشده، مسیر ریپوی فرانت، درخواست Network مرحله Welcome/CandidateIntroduction و status/code پالایش‌شده؛ اگر200 بررسی Blob/audio element/play rejection، اگر503 بررسی failureCode امن provider/encoder، اگر404 بررسی stage جاری و advance زودهنگام، اگر401 بررسی header دعوت. بدون شواهد، خرابی قطعی فرانت یا رفع مقصد ادعا نمی‌شود؛ اضافه‌کردن endpoint نمونه یا تغییر سیاست مراحل در این بررسی مجاز تلقی نشده است.

## قالب ایمیل دعوت — 2026-10-13

HTML فارسی/RTL با Tahoma، لوگوی HireYar داخل ایمیل، دکمه/کد/لینک جایگزین، انقضای شمسی تهران و fallback متن پیاده و build/14 تست SMTP محلی موفق شدند. پیش‌نمایش Chromium دسکتاپ و موبایل320/390 بدون overflow تأیید شد. رنگ‌ها و لوگو از بستهٔ محلی لندینگ/mockup برند هستند؛ کد داشبورد Angular جاری در دست نیست.

پیگیری باقی: تطبیق دقیق با داشبورد پس از دسترسی به مسیر ریپوی فرانت؛ بررسی ظاهری Gmail و Outlook با داده مصنوعی در تسک مجاز مربوط. این درخواست SMTP/config مقصد را عوض یا ایمیل خارجی ارسال نکرد؛ انتشار مستقل است. بدنهٔ ایمیل واقعی/کد/لینک/گیرنده در Git یا لاگ ثبت نشود.

## Gemini TTS پیاده‌شده — 2026-10-13

درخواست پیاده‌سازی انجام شد: انتخاب provider محفوظ، Liara/Gemini PCM→MP3، سرعت محلی، محافظ‌های حجم/ظرفیت/deadline/process، probe/startup و runtime Docker؛ GEMINI_TTS و ADR-044. build و ۱۹۵ تست مرتبط با PostgreSQL و encoder واقعی موفق، صفرSkip؛ smoke مصنوعی واقعی Liara حدود۴٫۸ثانیه و MP3 قابلdecode. امکان‌سنجی پایین تاریخی است و دیگر مانع مجوز پیاده‌سازی نیست.

باقی برای انتشار مستقل: ساخت image دارای ffmpeg، حفظ/تأیید environment منتخب TtsModel/TtsVoice و نبود override قدیمی، startup/health و آزمون صوت مرورگر در مقصد؛ تست بار و تلفظ/تأخیر متن‌های طولانی. daemon Docker این نوبت در دسترس نبود؛ deploy یا restart مقصد مجاز تلقی/انجام نشد. migration/تغییر Angular لازم نیست. مسیر encoder توسعه ignored و binary داخل artifacts محفوظ بماند یا با نصب معمول جایگزین شود. مقادیر قبلی config و داده مقصد محفوظ‌اند.

## امکان‌سنجی Gemini TTS لیارا — 2026-10-13

امکان مستند است؛ راه‌اندازی با تغییر config تنها ممکن نیست، چون خروجی Gemini PCM و مسیر فعلی MP3 است. HANDOFF بررسی code/provider/cache/MIME را ثبت می‌کند. پیشنهاد برای حفظ API فعلی: تبدیل در Infrastructure با encoder runtime؛ گزینه WAV به قرارداد MIME و metadata نیاز دارد. اجرای این تغییر در درخواست بررسی مجاز تلقی نشد و هنوز انجام نشده است. ادامه در درخواست پیاده‌سازی: فرمت، تبدیل، payload بدون پارامتر تأییدنشده، محدودیت/cancellation، آزمون مصنوعی و smoke صوت با متن مصنوعی؛ کیفیت فارسی/سرعت/پشتیبانی speed و هزینه واقعی تأیید نشده‌اند. build و ۴۱ تست provider موفق؛ gemini-tts-feasibility.trx. مقصد/secret/انتشار تغییر نکردند.

## بررسی اعتبارسنجی تولید سؤال در فرانت — 2026-10-13

کد جاری پاسخ موفق را به موضوع‌های سه‌سطحی معتبر محدود می‌کند؛ level رشته کوچک و شمارنده‌های کل بانک T/3T/2T، تک‌موضوع1/3/2 هستند. تفاوت bankQuestionCount و interviewQuestionCount نقص بانک نیست و ترتیب سطح‌ها از مدل ثابت نیست. تست HTTP خام و توضیح اتصال در ADVANCED_QUESTIONS_API افزوده شد؛ تغییر runtime/schema/config لازم نشد.

پیگیری باز: JSON همان پاسخ مشکل‌دار از مقصد، mapper/validator Angular برای حروف level و مقایسه شمارنده‌ها، جایگزینی فقط موضوع انتخاب‌شده و عدم اعمال حداقل۵موضوع روی پاسخ تک‌موضوعی. پاسخ مقصد و کد فرانت در دست نیستند؛ خطای گزارش‌شده قطعی رفع‌شده تلقی نمی‌شود. انتشار و تماس واقعی provider انجام نشده است.

اعتبارسنجی: build بدون warning/error و ۵۵/۵۵ تست مرتبط روی PostgreSQL محلی موفق، صفر Skip؛ question-suggestion-wire-final.trx. صحت HTTP محلی تأیید شده، رفع پیام مقصد هنوز وابسته به شواهد فرانت است.

## چرخه گزارش و شفافیت وضعیت — 2026-10-13

بک‌اند روی dev محلی تأیید شد: شروع خودکار بعد از completion، صف و worker، مراحل ماندگار، علت و مرحله شکست، retry فقط در Failed، API وضعیت و رفع GET404 برای گزارش آماده‌نشده. ADR-043 و INTERVIEW_REPORT_LIFECYCLE_API مرجع جاری و جایگزین تولید همزمان و بازتولید گزارش موفق تاریخی هستند. build بدون warning/error و ۲۰۶ تست مرتبط PostgreSQL موفق، بدون شکست یا Skip؛ report-lifecycle-final.trx.

باقی برای محصول: Angular جدا باید هر سه ثانیه status را تا موفقیت یا شکست بخواند، message و مرحله را نمایش دهد، دکمه را از canRetry کنترل و POST202 را به polling وصل کند. پس از refresh از status ادامه دهد و گزارش آماده را از GET بخواند. انتشار مستقل به migration20261013054737_AddReportGenerationLifecycle پیش از نسخه جدید، restart و بررسی نسخه و وضعیت HTTP در پراکسی مقصد نیاز دارد؛ آزمون واقعی تأخیر و کیفیت provider و ظرفیت صف باقی است. این موارد انجام نشده و backlog مجوز push/deploy نیست. تنظیمات و داده مقصد محفوظ‌اند؛ منشأ HTTP404 همراه body409 مقصد قطعی نیست.

## سوپرادمین محدود به ورود و مدیریت پکیج — 2026-10-12

درخواست بک‌اند روی `codex/super-admin` از `dev`: Verified محلی؛ ورود password+PNG CAPTCHA، کلید appsettings و entry=404 نامعتبر، JWT مستقل و DB permission/stamp/lockout، CLI تعاملی provisioning، همان API پویا و RowVersion قیمت‌گذاری. [SUPER_ADMIN_API](SUPER_ADMIN_API.md) و ADR-039 مرجع‌اند. schema/Repository/عضویت چندسازمانی اضافه نشده؛ سوابق مالی snapshot محفوظ‌اند.

باقی برای تحویل محصول: ساخت route guard و صفحه `/admin/{randomkey}`، تصویر کپچا/ورود، جدول پکیج/دوره/قیمت و مدیریت409 در ریپوی Angular؛ انتخاب و provision حساب واقعی، key خصوصی/Enabled مقصد و بررسی proxy/access-log مسیر خصوصی، آزمون UI واقعی. این اقدامات روی مقصد انجام نشده‌اند؛ سایر مدیریت‌های پلتفرم مجوز توسعه خودکار ندارند. فایل مشترک خاموش/کلید خالی، migration لازم نیست، commit/push/deploy نشده.

Verification نهایی: restore/build صفر warning/error؛ ۳۲۵/۳۲۵ موفق، صفر Skip، PostgreSQL18 اختصاصی محلی و provider/CAPTCHA integration مصنوعی؛ `super-admin-final.trx`. تصویر PNG واقعی به‌صورت synthetic نمایش و بررسی شد؛ OCR/فرانت واقعی ادعا نمی‌شود.

## ویدیوی خصوصی آروان — 2026-10-12

درخواست جاری: اتصال ویدیوهای تازه به S3 آروان، نگهداری credential در appsettings محلی ignored، multipart بک‌اند با حافظه محدود، پخش Owner scoped با Range و کنترل خصوصی‌بودن bucket اجرا شد؛ [INTERVIEW_VIDEO_STORAGE](INTERVIEW_VIDEO_STORAGE.md) مرجع است. migration/انتقال فایل قدیمی لازم نیست و volume صوت/رزومه/فایل تاریخی باید حفظ شود.

باقیِ پیشنهاد حجم بالا: API جلسه آپلود مستقیم، امضای UploadPart/resume/finalize با quota و مالکیت سرور، CORS محدود، cleanup orphan و مصرف در Angular جدا. این پیشنهاد مجوز اجرای خودکار مرحله دیگر نیست. عملیاتی: credential مقصد، lifecycle فقط AbortIncompleteMultipartUpload، بررسی فضای temp/proxy/timeout، بار/codec/ضبط طولانی و player authenticated Range در فرانت؛ هیچ‌کدام روی production اعمال نشده‌اند.

اعتبارسنجی نهایی: restore از NuGet عمومی و build موفق با صفر warning/error؛ suite کامل ۳۱۲/۳۱۲ موفق، صفر Failed/Skipped، PostgreSQL 18 اختصاصی loopback:55486 و S3/provider مصنوعی. TRX: tests/HireYar.Tests/TestResults/s3-video-final.trx. ۱۶ case واحد تازه و یک integration S3 شامل multipart محدود، Empty/oversize، private ACL/public policy/NotPrincipal، 404 XML امن/DTD ممنوع، abort لغو و پاسخ گم‌شده Complete، cleanup، فایل تاریخی/traversal، HTTP206/no-store و JWT/Owner/scope سازمان‌اند. اجرای اولیه suite دو شکست فقط در تنظیمات S3 fixtureهای مستقل Production داشت؛ override Local در آن‌ها اضافه و suite کامل موفق بازاجرا شد.

Smoke واقعی آروان با داده مصنوعی 8 MiB+16 بایت، دو UploadPart/Complete، خواندن seek/Range احرازشده و GET بدون امضا موفق: anonymous=403؛ فایل مصنوعی حذف شد. GetBucketAcl=200 و نبود policy با HTTP404/XML NoSuchBucketPolicy تأیید شدند. لاگ ignored artifacts/s3-video-smoke.log فاقد credential/key فایل یا محتوای کاربر است. آزمون real codec/فرانت/فایل چندگیگابایتی/بار مقصد انجام نشده؛ محدودیت 512 MiB و IFormFile/ترافیک بک‌اند محفوظ‌اند. هیچ migration یا تغییر schema/data مقصد، bucket policy/ACL مقصد، push/deploy/production انجام نشده است.
## جلوگیری از عنوان تکراری — 2026-10-12

درخواست حاضر در Create/Update موقعیت و Create/Update/SaveWizard قالب اجرا شد؛ scope سازمان، case/Trim، self-exclusion، آرشیو/حذف و رقابت با قفل موجود بررسی می‌شوند. فرانت جدا باید detail یا code پاسخ 409 (job_title_duplicate / template_title_duplicate) را کنار ورودی عنوان نمایش دهد؛ تغییر Angular اینجا انجام نشده است. migration لازم نیست، داده‌های قبلی و کپی داخلی قالب دعوت حذف/بازنویسی نشدند؛ ADR-037 مرجع حدود تضمین است.

اعتبارسنجی نهایی: tool restore و restore موفق؛ build صفر warning/error؛ suite کامل ۲۹۵/۲۹۵ موفق، صفر شکست/Skip، روی PostgreSQL 18 اختصاصی loopback:55485 و provider مصنوعی. چهار تست جدید ایجاد/ویرایش، اشتراک قاعده بین مسیرهای قدیمی/ویزارد، scope سازمان، self-edit، آرشیو/حذف، عدم تغییر سؤال/RowVersion در شکست و رقابت create/update را بررسی کردند. TRX: tests/HireYar.Tests/TestResults/title-uniqueness-final.trx. تست‌های دعوت cross-position و snapshot قبلی هم موفق‌اند. داده مصنوعی قالب دوم trial و موقعیت/قالب‌های مستقل billing عنوان مستقل گرفتند؛ assertions مالی/قابلیت‌ها حذف نشدند. نتیجه اولیه suite موفق نبود؛ نتیجه نهایی پس از تطبیق داده تست و حفظ عنوان فعلی در ویرایش است.

## پاک‌سازی کد — 2026-10-12

پاک‌سازی درخواست حاضر: حذف واسطه حساب، wiring توکن ایمیلی بی‌مصرف، متد تمدید دعوت بدون caller، projection/property/usingهای بی‌اثر؛ یکسان‌سازی اجرای recovery و حذف مرتب‌سازی مکرر shortlist تاریخی. بهسازی بدون Repository/لایه عمومی جدید و بدون حذف قرارداد API یا migration انجام شد؛ ADR-036 و STATUS مرجع نتیجه‌اند.

اعتبارسنجی واقعی: dotnet tool restore و restore موفق؛ build صفر warning/error؛ suite کامل ۲۹۱/۲۹۱ موفق، صفر شکست/Skip، روی PostgreSQL 18 اختصاصی loopback:55485 و providerهای مصنوعی. گزارش tests/HireYar.Tests/TestResults/cleanup-verified.trx و لاگ ignored artifacts/cleanup-tests.log. Git diff --check موفق است؛ کیفیت AI واقعی با این تست‌ها اثبات نمی‌شود.

حذف جدول دعوت تیم یا شاخه‌های لازم برای جلسات/گزارش‌های تاریخی در این تغییر انجام نمی‌شود؛ این بخش‌ها با «کد بلااستفاده قابل حذف بدون تغییر داده» یکسان نیستند. کارهای قبلی backlog همچنان همان وضعیت قبلی دارند؛ قابلیت تازه‌ای از backlog اجرا نشد.

## تأیید دسترسی همه پکیج‌ها در دمو — 2026-10-11

کاربر صریحاً انتخاب کرد همه امکانات باز باشند و مصرف از شارژ هدیه کم شود. بررسی کد نشان داد grant ثبت‌نام اجتماع features فعال پکیج‌های عمومی را ذخیره و RequireAsync/ویزارد/قالب موضوعی/start از همان snapshot استفاده می‌کنند؛ خرید پکیج لازم نیست و تغییر runtime لازم نشد. چهار integration مستقل برای عمومی/تخصصی با قالب عادی/موضوعی پیشرفته، حساب واقعی OTP trial با ۰٫۵ دلار و دو روز، پیشنهاد سؤال، دعوت، start/answer/complete، گزارش، کسر هزینه و رد جلسه شصت‌دقیقه‌ای با insufficient_ai_credit اضافه شدند. هیچ شارژ مصنوعی اضافی یا cash purchase در این سناریوها وجود ندارد.

REGISTRATION_TRIAL_API قرارداد فرانت را صریح می‌کند: current.features در subscription/panel، فعال‌کردن پیشرفته بدون شرط packageCode=advanced، پایان زمانی و پیام شارژ مستقل از خرید پکیج. Angular در ریپوی جداست؛ تغییر UI اینجا انجام نشده. build صفر warning/error؛ چهار تست مرتبط ۴/۴ موفق، صفر Failed/Skipped، روی PostgreSQL 18 اختصاصی loopback:55484 و AI/گزارش مصنوعی. TRX: tests/HireYar.Tests/TestResults/trial-package-access-verified.trx. suite کامل قبلی ۲۸۷ موفق متعلق به درخواست تنظیمات است و این نوبت تکرار نشد. اجرای اولیه فقط assertion مصرف شکست خورد چون clock پس از ثبت پاسخ جلو رفته بود؛ clock پیش از answer جلو رفت تا زمان پاسخ واقعی قابل محاسبه باشد و چهار مسیر کامل دوباره پاس شدند. fixture DB صفر باقی ماند و cluster اختصاصی متوقف شد؛ کیفیت/دسترسی provider واقعی با این تست‌ها اثبات نمی‌شود. push/deploy/production یا migration تازه انجام نشده است.

## تنظیم مدت و مبلغ trial — 2026-10-11

روی dev، بخش RegistrationTrial با DurationDays=3 و CreditAmountUsd=1.0 به appsettings اضافه شد؛ 0.5 و مبلغ مثبت با حداکثر شش اعشار پشتیبانی می‌شوند. مقدار نامعتبر هنگام startup رد می‌شود. تغییر سیاست با restart فقط ثبت‌نام‌های جدید را تحت تأثیر می‌گذارد؛ snapshot/سوابق قبلی و ورود مجدد دست‌نخورده‌اند. قرارداد REGISTRATION_TRIAL_API و ADR-035 به‌روز شدند.

migration 20261011120218_ConfigureRegistrationTrial الزام ثابت دیتابیس را به مدت صحیح روز و مبلغ مثبت تغییر می‌دهد؛ ledger با مبلغ grant تطبیق دارد و downgrade ناسازگار با تاریخچه رد می‌شود. migration مقصد هنوز اعمال نشده؛ push/deploy/production انجام نشده‌اند. build نهایی صفر warning/error؛ suite کامل نهایی ۲۸۷/۲۸۷ موفق، صفر Failed/Skipped، با PostgreSQL 18 اختصاصی loopback:55484 و provider مصنوعی؛ TRX: tests/HireYar.Tests/TestResults/trial-settings-complete.trx. دوازده case جدید binding/validation، ثبت نیم‌دلار و دو روز، عدم تکرار، انقضا، downgrade و تطابق ledger را بررسی می‌کنند. drift=false و rollback/apply migration در تست موجود موفق‌اند. اسکریپت delta idempotent artifacts/trial-settings-migrations.sql تولید شد؛ اجرای جداگانه اسکریپت انجام نشد و migration در fixture واقعی اعمال شد. تمام مقادیر قبلی appsettings بدون نمایش secret با HEAD برابر تأیید شدند؛ fixture DB باقی نماند و cluster اختصاصی متوقف شد. شکست اجرای اولیه فقط assertion تست محافظ بود: پیش از trial trigger، wallet mismatch اجرا می‌شد؛ مانده کیف پول synthetic با ledger اشتباه همسان شد تا trial trigger مستقل آزمایش شود و suite کامل بازاجرا شد.

## دوره آزمایشی ثبت‌نام — درخواست 2026-10-10

بک‌اند trial سه‌روزه همه قابلیت‌های فعال عمومی و یک دلار اعتبار هدیه با تشخیص DB/API پیاده شد؛ ورود تکراری و حساب‌های قدیمی هدیه تازه نمی‌گیرند. migration `20261010132215_AddRegistrationTrial` و قرارداد [REGISTRATION_TRIAL_API](REGISTRATION_TRIAL_API.md) آماده‌اند. build بدون warning/error؛ suite کامل **۲۷۵ موفق، صفر شکست/Skip** روی PostgreSQL واقعی، شامل هفت تست جدید؛ guardهای واقعی، drift=false و SQL idempotent دوبار موفق‌اند. TRX: `registration-trial-complete.trx`؛ Verified محلی.

باقی مقصد: اعمال migration پیش از نسخه جدید با مجوز انتشار مربوط؛ Angular جدا برای نمایش isTrial/مهلت/مانده و انتخاب operation=purchase پس از trial قرارداد موجود را مصرف کند. نرخ seed=۰٫۰۵ و یک دلار حداکثر بیست دقیقه رزرو مصاحبه را پوشش می‌دهد؛ وعده اعتبار نامحدود وجود ندارد. این باقی‌مانده مجوز اجرای خودکار انتشار یا تغییر فرانت نیست.

## مسیر متوسط و دو سؤال در هر موضوع — اصلاح صریح 2026-10-10: Verified محلی

متوسط اول؛ پاسخ درست → سخت، پاسخ ناقص/ضعیف/unknown → آسان؛ پس از سؤال دوم موضوع بسته می‌شود. سقف ۲×موضوع و بانک سه‌سطحی، policy snapshot مستقل و سازگاری جلسه/resend قبلی پیاده شدند. ADR-033 و [ADVANCED_QUESTIONS_API](ADVANCED_QUESTIONS_API.md). build صفر warning/error و **۲۶۸ موفق، صفر شکست/Skip** با PostgreSQL واقعی محلی؛ migration/up/down/guard و SQL idempotent دوبار تأیید شدند.

باقی مقصد: migration `20261010085424_AddMediumFirstTopicQuestions` پیش از کد، نمایش سقف ۱۰..۲۰ سؤال اصلی در Angular جدا و سنجش کیفیت پاسخ‌سنجی فارسی مدل واقعی. مجوز انتشار یا تماس واقعی provider صادر نشده؛ بخش سه‌سطحی زیر سابقه نسخه قبلی است.

## پیشروی سه‌سطحی موضوع — درخواست مستقل 2026-10-10: Verified محلی (نسخه قبلی)

دعوت موضوعی تازه با آسان ← متوسط ← سخت پس از پاسخ قابل‌قبول، grade persisted مستقل، سرور کنترل‌کننده سطح و مدل انتخاب‌کننده موضوع تازه پیاده شد. نسخه قبلی جلسه/resend محفوظ؛ سقف counters برابر ۳×موضوع، عمومی به ترتیب قالب و تخصصی shortlist. [ADVANCED_QUESTIONS_API](ADVANCED_QUESTIONS_API.md)، ADR-032 و migration `20261010075455_AddProgressiveTopicQuestions`.

build صفر warning/error و suite کامل **۲۶۴ موفق، صفر شکست/Skip** با PostgreSQL 18 محلی، script idempotent دوبار، drift=false و محافظ rollback واقعی تأیید شدند. باقی برای مقصد: اجرای migration قبل از کد، تطبیق نمایش counters/زمان در Angular جدا، ارزیابی واقعی کیفیت تشخیص پاسخ فارسی و آستانه ۰٫۷/۰٫۶. این باقی‌مانده مجوز انتشار یا تماس واقعی provider نیست؛ ارزیابی خودکار تصمیم استخدام ایجاد نمی‌کند.

## HY-301 — تکمیل بک‌اند طرح گزارش HTML (2026-10-10)

Verification محلی تکمیل: build صفر warning/error؛ suite کامل PostgreSQL **۲۵۰ موفق، صفر Failed/Skipped**؛ `report-v3-verified.trx`. migration واقعی، tenant FK و محافظ downgrade بررسی شدند؛ script idempotent دو بار روی scratch تصادفی موفق بود، یک history entry. frontend/AI واقعی/مقصد و اشتراکِ نیازمند مجوز، تکمیل‌شده محسوب نمی‌شوند.

report-v3، هفت شاخص عمومی/سه بعد تخصصی مستند، وزن/آستانه صریح و regenerate حفظ‌کننده cached، سابقه تصمیم انسانی، consent/device events گزارش‌شده و upload ویدیوی جلسه/timeline خصوصی پیاده‌اند. چاپ HTML/print CSS مسیر PDF مرورگر را فراهم می‌کند. [INTERVIEW_REPORT_V3_API](INTERVIEW_REPORT_V3_API.md) قرارداد همه مسیرها و مقیاس‌هاست؛ ADR-031 جای محدودیت‌های قبلی بک‌اند را برای نسخه جدید روشن می‌کند. وزن پیش‌فرض هر موضوع ۱ و بدون شواهد نمره null است؛ وزن JD حدس زده نمی‌شود.

باقی برای استفاده کامل محصول: اتصال در Angular جدا (metrics/null نمودار، وزن‌ها/regenerate، decision، consent/events، recorder و offset واقعی، fetch رسانه با Bearer، print Blob)، اعمال migration مقصد با مجوز انتشار مستقل، آزمون فارسی واقعی مدل/میکروفون/دوربین/پیوستگی ضبط. آستانه/وزن‌ها از انتخاب HR می‌آیند؛ schema به معنی کیفیت اثبات‌شده مدل نیست. مقایسه آماری معتبر/استخدام خودکار وعده داده نمی‌شود. اشتراک خارج سازمان blocked by automatic approval review است؛ برای endpoint ناشناس توکن‌دار باید مجوز صریح کاربر درباره خروج داده و روش اشتراک گرفته شود. این کار اعمال نشده و بدون مجوز ادامه نمی‌یابد.

## HY-301 — کسری‌های طرح مرجع evaluation-report (2026-10-10): بررسی انجام شد

پس از pull کاربر، endpoint GET/POST /api/interviews/{id}/evaluation-report و report-v2 روی dev=9947b6a بررسی شدند؛ نتیجه نبود endpoint در checkout قدیمی دیگر وضعیت جاری نیست. گزارش سؤال‌محور/موضوعی موجود است ولی پنج مورد فرانت همچنان کم‌اند: سه بعد مستقل و وزن‌ها، هفت شاخص ثابت HR، ثبت رضایت/قطعی و معیار کامل‌بودن ضبط، ویدیوی پیوسته با offset، و API تصمیم انسانی. تطبیق کد و داده موجود در STATUS ثبت شد؛ ADR-030/INTERVIEW_EVALUATION_API نیز محدودیت‌ها را تأیید می‌کنند.

ادامه پیاده‌سازی مطابق طرح نیازمند نام/تعریف/مقیاس هفت شاخص و وزن‌های ارزیابی است؛ HTML مرجع در Git نیست. ضبط پیوسته و telemetry جلسه به قرارداد دریافت داده از فرانت نیاز دارند؛ unknown تاریخی نباید با وضعیت/امتیاز فرضی backfill شود. برای گزارش cached باید سازگاری v1/v2 و سیاست بازتولید تعیین شود. runtime/schema این نوبت تغییر نکردند. build موفق؛ 9 تست report/history موفق و 3 API PostgreSQL Skip؛ نتیجه DB ادعا نشده. push/deploy/production تغییر نکردند.

## گزارش مطابق HTML — 2026-10-09

بک‌اند report-v2 و قرارداد [INTERVIEW_EVALUATION_API](INTERVIEW_EVALUATION_API.md) آماده؛ build و ۱۴ تست مرتبط PostgreSQL واقعی موفق. فرانت جدا باید details nullable برای v1، نمودار topics واقعی، نمره null برای سؤال بی‌پاسخ، confidence/بررسی انسانی، transcript/feedback و رسانه با Bearer مالک را مصرف کند. میانگین ساده/آستانه پاسخ ۳ را روشن نمایش دهد؛ fit score/رتبه‌بندی معتبر/شاخص‌های HR بدون شواهد/رضایت و هشدار دستگاه را از نمونه ثابت کپی نکند. وزن شغلی/rubric اختصاصی در snapshot موجود نیست. PDF، اشتراک، بازتولید v1 و بررسی کیفیت واقعی مدل نیازمند درخواست مستقل‌اند؛ این فهرست مجوز اجرای خودکار آن‌ها نیست.

## پیگیری 503 گزارش ارزیابی — 2026-10-08

مدل مشترک گزارش Liara/openai/gpt-6-luna با timeout=90 است؛ مدل در GET models موجود و smoke گزارش با context مصنوعی HTTP 200/حدود ۹٫۳ ثانیه/JSON قابل parse داشت. override توسعه محلی NetArz است؛ GET models آن 404 می‌دهد، بدون اثبات علت خطای گزارش. build و ۴۷ تست مستقل موفق. کد/تنظیمات/کلیدها عوض نشدند.

برای تعیین علت خطای واقعی کاربر، لاگ همان trace با operation=report و provider/model/status/httpStatusCode/durationMs لازم است. HTTP failure/timeout را از JSON/schema/citation rejection پس از HTTP 200 تفکیک کنید؛ متن رزومه/پاسخ/کلید/پرامپت واقعی در Git یا لاگ قرار نگیرد. پیام عمومی evaluation_report_unavailable همه این علت‌ها را یکسان می‌کند؛ در صورت نیاز به اصلاح تشخیص، کد علت ثابت امن را در logging/SSE گزارش نگه دارید. نبود مدل یا رفع خطای مقصد ادعا نشده است؛ این یادداشت مجوز تغییر مدل/مقصد یا انتشار نیست.

## لاگ زنده آزمایشی — 2026-10-08

بک‌اند و قرارداد [INTERVIEW_DIAGNOSTICS_API](INTERVIEW_DIAGNOSTICS_API.md) تکمیل شدند: flag/DTO نمایش، SSE scoped، metadata واقعی EF/provider/مراحل و capture همان prompt فقط Owner در حافظه محدود. build موفق؛ ۲۲۷ تست suite PostgreSQL واقعی موفق و پس از اصلاح cursor نهایی ۹ تست مرتبط مجدد موفق، صفر Skip/شکست.

باقی تحویل همین درخواست: Angular ریپوی جدا فقط طبق diagnosticsEnabled UI موقت را نمایش دهد؛ fetch streaming با Bearer و parser SSE، dedup/cursor/reconnect/closed/reset و پاک‌کردن UI هنگام خاموش‌شدن flag را مصرف کند. نمایش prompt فقط در ابزار احرازشده Owner، نه با کد دعوت؛ credential مالک در صفحه عمومی قرار نگیرد. در مقصد restart پس از تغییر flag، buffering/timeouts proxy و در صورت چند replica routing همان process بررسی شوند. وقتی آزمایش تمام شد Enabled=false؛ حذف نهایی کد به درخواست مستقل. AI واقعی و rollout انجام نشده‌اند؛ این فهرست مجوز اجرای خودکار Angular یا انتشار نیست.

## تست میکروفون داوطلب — 2026-10-08

API گذرای audio-check قبل از start پیاده و با build بدون هشدار/خطا و ۲۲۲ تست موفق PostgreSQL واقعی تأیید شد؛ قرارداد [INTERVIEW_AUDIO_CHECK_API](INTERVIEW_AUDIO_CHECK_API.md). تحویل فرانت باقی است: نمونه کوتاه، ارسال multipart با کد دعوت، نمایش transcript، تطبیق جمله توسط کاربر و retry خطا. کیفیت واقعی STT فارسی/سکوت/نویز/قطع ضبط باید با provider واقعی بررسی شود. سنجش مستقل شفافیت و کامل‌بودن جمله پیاده نشده؛ clarityVerified=false است. این فهرست مجوز توسعه خودکار موارد دیگر یا انتشار نیست.

## آخرین تکمیل: دعوت جدید و خطای STT — 2026-10-08

درخواست کد شش‌رقمی، نمایش لینک/کد در API پنل، reinvite با حفظ جلسه قبلی و پیام انقضا تکمیل و با ۲۲۰ تست موفق تأیید شد. وضعیت processing/retry و FailureStage/FailureCode برای فرانت روشن شد؛ STT JSON صریح و تشخیص امن پاسخ نامعتبر اضافه شد؛ recovery پاسخ با رزرو بسته متوقف است.

باقی تحویل: Angular ریپوی جدا باید فرم کد رشته‌ای، access-status/410 و processingMessage/isProcessing/answer.canRetry را مصرف کند. بررسی reason جدید روی Liara واقعی و آزمون صوت واقعی لازم است تا علت پاسخ نامعتبر گزارش‌شده تأیید شود. migration مقصد و انتشار نیاز به درخواست مستقل دارند. [INTERVIEW_INVITATIONS_API](INTERVIEW_INVITATIONS_API.md) مرجع قرارداد است؛ این فهرست مجوز توسعه مراحل دیگر نیست.

## آخرین تکمیل: مدل گزارش مستقل و انتخاب تاریخچه — 2026-10-08

درخواست مستقل کاربر روی dev تکمیل شد: EvaluationModel/timeout مستقل، گزارش کامل Completed با generation به درخواست پنل و cache پایدار، citation/quote معتبر و lease/tenant/حذف/reclaim؛ تاریخچه کامل DB موجود حفظ و ورودی زنده با selector محلی و محافظ context/شواهد تجمعی محدود می‌شود. build صفر warning/error و **۲۰۹ تست PostgreSQL واقعی موفق، صفر Skip**؛ migration جدید و delta idempotent روی baseline قبلی دوبار موفق. [INTERVIEW_EVALUATION_API](INTERVIEW_EVALUATION_API.md) قرارداد است و جزئیات تست در STATUS/HANDOFF هستند.

باقی این تحویل: مصرف POST/GET گزارش در Angular ریپوی جدا، migration مقصد با مجوز مستقل، تأیید پشتیبانی واقعی `openai/gpt-6-luna` و آزمون مقایسه‌ای کیفیت فارسی، ارجاع ضمنی/مترادف، duration/token واقعی و صرفه‌جویی کل prompt. گزارش خودکار در completion، بازتولید گزارش cached، embedding یا خلاصه‌ساز AI اضافه نشده‌اند. این فهرست مجوز توسعه خودکار یا انتشار نیست؛ درخواست اصلی ذخیره تاریخچه به داده‌های persistشده موجود متصل است و جدول موازی گفتگو لازم نشد.

## تکمیل قبلی: معرفی صوتی و صوت مرحله‌ها — 2026-10-07

کسری STT/upload/retry/recovery معرفی و صوت ثابت مرحله‌ها در بک‌اند رفع و با 198 تست PostgreSQL واقعی تأیید شد. [VOICE_INTRODUCTION_API](VOICE_INTRODUCTION_API.md) مرجع جدید است. باقی این تحویل: اتصال Angular به قرارداد صوتی، migration مقصد/فعال‌بودن worker و آزمون صوت/دوربین واقعی. بهینه‌سازی صوت جریانی/آماده‌سازی سؤال‌ها و تلفظ هنوز باز هستند؛ یادداشت‌های متنی پایین وضعیت تاریخی پیش از این تکمیل‌اند. این فهرست مجوز اجرای مراحل دیگر یا rollout نیست.

# معرفی‌ها، context AI و recovery — 2026-10-07: Verified محلی

درخواست مستقل ساختار بک‌اند اجرا شد: دو معرفی با فعال‌سازی/snapshot و مرحله persistشده، مستر پرامپت v2، استخراج محلی PDF متنی/DOCX یا resumeText صریح، ارسال context کامل ویزارد، ترتیب عمومی، تعامل محدود همان سؤال و worker بازیابی پاسخ پذیرفته‌شده. **۱۹۲ تست PostgreSQL واقعی موفق و صفر Skip**، ۹ تست تازه؛ migration روی legacy مصنوعی با script idempotent دوبار بررسی شد و EF drift ندارد. [INTERVIEW_CONTEXT_API](INTERVIEW_CONTEXT_API.md) مرجع قرارداد است.

باقی‌مانده اتصال/عملیات: مصرف stage/advance و interactionMessage و خطای resume_text_required در Angular ریپوی جدا، اعمال migration مقصد با مجوز مستقل و آزمون provider واقعی/کیفیت فارسی. OCR/DOC parsing، STT اختصاصی معرفی و backfill رزومه جلسات تاریخی اجرا نشده‌اند؛ معرفی کارجو ورودی متنی و مسیر legacy بدون فایل resume=null دارد. پس از سه شکست STT، retry دستی حفظ می‌شود؛ متن/evidence ساختگی برای جلو رفتن ساخته نمی‌شود. این موارد مجوز توسعه خودکار یا push/deploy نیستند.

## انتخاب پیاده‌سازی مستقل providerهای AI — 2026-10-07

روی dev، DI براساس AI:Provider یکی از NetArzClient، LiaraClient یا DisabledInterviewProvider را برای STT/LLM/TTS/پیشنهاد سؤال انتخاب می‌کند؛ مقدار ناشناخته در startup رد می‌شود. NetArzOptions و LiaraOptions مستقل‌اند، فقط تنظیمات provider انتخاب‌شده اعتبارسنجی می‌شود و lease از timeoutهای همان provider محاسبه می‌شود. Disabled هیچ HttpClient یا کلید provider نمی‌خواهد. جزئیات مشترک قرارداد HTTP در OpenAiCompatibleInterviewClient داخل Infrastructure باقی‌اند؛ نام provider در نتیجه/metrics/log از کلاس انتخاب‌شده می‌آید و property قدیمی NetArzOptions.Provider authoritative نیست.

تمام appsettingsها و مقادیر کلید موجود دست‌نخورده‌اند؛ انتخاب پیش‌فرض هنوز NetArz است. برای انتخاب Liara مقدار AI:Provider=Liara یا override محیطی AI__Provider=Liara لازم است. آدرس و مدل STT تنظیم‌شده طبق بررسی قبلی همچنان نیاز به تطبیق کنسول/تأیید واقعی دارند؛ تغییر حدسی انجام نشد. افزودن provider دیگری نیازمند options، client و شاخه ثبت DI مستقل است؛ انتخاب نام دلخواه پشتیبانی خودکار ایجاد نمی‌کند.

build صفر warning/error؛ 88/88 تست مرتبط provider/audio/selection/rules موفق، صفر شکست/Skip؛ tests/HireYar.Tests/TestResults/provider-selection.trx. هشت case تازه انتخاب چهار interface، عدم دخالت تنظیمات provider دیگر، کلید مستقل، lease، نام ناشناخته، Disabled و درخواست‌های مصنوعی Liara با آدرس/کلید/مدل/نتیجه مخصوص را بررسی می‌کنند. تماس واقعی AI، PostgreSQL/migration، commit/push/deploy انجام نشد؛ schema و قرارداد Application/API تغییر نکرده‌اند. تغییرات مستندی نوبت قبلی حفظ شدند.

## بررسی قابلیت استفاده از Liara — 2026-10-07

بررسی روی dev پاک انجام شد؛ کد runtime و تمام appsettingsها/توکن‌ها دست‌نخورده‌اند. AI:Provider فعلی NetArz است؛ AddInterviews فقط NetArz/Disabled را می‌پذیرد و AI:Liara را bind نمی‌کند. تغییر Provider به Liara در وضع فعلی startup را رد می‌کند. NetArzClient نام provider را در نتیجه/metrics/log ثابت دارد و اعتبارسنجی lease فقط timeoutهای NetArz را می‌خواند؛ پشتیبانی انتخاب Liara باید این موارد را نیز اصلاح کند.

مستند رسمی https://docs.liara.ir/ai/openai/ سازگاری chat/completions، audio/transcriptions و audio/speech، مدل openai/gpt-5.6-luna و openai/tts-1-hd و voice=nova را مستند کرده است. مدل تنظیم‌شده openai/gpt-transcribe در فهرست آن صفحه نیست؛ openai/whisper-large-v3 مستند است، اما نبود در فهرست اثبات عدم پشتیبانی نیست. نمونه BaseUrl در https://docs.liara.ir/ai/quick-start/ ترتیب /api/v1/{workspace} دارد؛ ترتیب تنظیم فعلی /api/{workspace}/v1 است و باید با آدرس واقعی کنسول تطبیق شود، نه تغییر حدسی.

درخواست GET models با کلید موجود فقط برای بررسی خواندنی انجام شد، اما HttpRequestException شبکه بدون پاسخ HTTP رخ داد؛ صحت کلید، پلن، آدرس و اجرای واقعی AI تأیید نشده‌اند. هیچ درخواست تولید متن/صوت یا ارسال داده کاربر انجام نشد و مقدار کلید/شناسه workspace در یادداشت ذخیره نشد. build صفر warning/error؛ 47/47 تست InterviewProviderTests و InterviewAudioTests موفق، صفر Skip؛ tests/HireYar.Tests/TestResults/liara-review.trx. PostgreSQL/migration، commit/push/deploy انجام نشد. نتیجه: Liara از نظر قرارداد API قابل اتصال است، اما هنوز provider قابل انتخاب پروژه نیست؛ اتصال واقعی و مدل STT باز هستند.

# Backlog

## HY-019 — پاسخ خودکار نوبتی داوطلب (2026-10-06): Verified محلی

قرارداد فرانت در [CANDIDATE_TURN_API](CANDIDATE_TURN_API.md) پیاده شد: audio قبلی، video اختیاری، clientTurnId اتمیک و replay، lastSpeechEndUtc و duration metadata، transcript/messages، capability/limits و Swagger binary. ۱۷۵ تست واقعی PostgreSQL و build موفق؛ migration/script و حفظ legacy تأیید شدند. طبق تصحیح صریح کاربر WebRTC/Realtime از این درخواست خارج شد و زیرساخت آن اضافه نشده است.

باقی تحویل: اعمال migration `20261006170044_AddCandidateAnswerCapture` در مقصد، اتصال Angular با MediaRecorder/تشخیص ۲ ثانیه سکوت و محدودیت mediaCapabilities، بررسی واقعی مجوز/codec مرورگر و timeout/حجم proxy، ضبط ویدیو و retention/cleanup عملیاتی. این موارد مجوز push/deploy یا شروع خودکار Realtime نیستند؛ مالی قبلی، مدت server و AI متن/صوت حفظ‌اند.

## رفع 500 دعوت ویزارد — 2026-10-06: اصلاح محلی؛ مقصد باز

لاگ traceId گزارش‌شده UnauthorizedAccessException هنگام ساخت پوشه رزومه را تأیید کرد. Dockerfile و هر دو Compose اکنون storage خصوصی متعلق به APP_UID و volume پایدار دارند؛ ۲۱ تست مرتبط PostgreSQL واقعی، build و config هر دو Compose موفق‌اند. اجرای Docker image به علت نبود daemon تأیید نشد. باقی: rebuild/recreate در مقصد با مجوز مستقل، حفظ/انتقال فایل قبلی احتمالی پیش از mount volume تازه، بررسی نوشتن با حساب سرویس و ارسال دعوت از پنل. migration تازه لازم نیست؛ push/deploy/production تغییر نکرده‌اند.

## HY-018 — بانک سؤال موضوعی پیشرفته (2026-10-06): Verified محلی

پیاده‌سازی backend پیشنهاد AI از JD، تولید کل/یک موضوع، ذخیره بانک سه‌سطحی کامل ۵ تا ۱۰ موضوع، ویرایش/حذف پیش‌نویس از PUT کامل، snapshot جلسه/کپی قالب و جلوگیری تکرار موضوع انجام شده؛ معمولی محفوظ است. [ADVANCED_QUESTIONS_API](ADVANCED_QUESTIONS_API.md). باقی تحویل خارج ریپو: Angular لیست/آکاردئون، loading/retry، تأیید و counters؛ اجرای migration و تست AI واقعی در محیط مقصد توسط مالک محیط. این باقی‌مانده مجوز push/deploy یا تماس واقعی provider نیست. build بدون warning/error، ۱۷۰ تست موفق بدون Skip، EF بدون drift و migration idempotent دوباره روی داده legacy مصنوعی تأیید شدند.


## HY-017 — حساب تک‌کاربر و حذف پنل (2026-10-06): Verified محلی

ورود فقط OTP، حذف API ایمیل/رمز/تیم، DB constraint Owner فعال واحد، بازنشسته‌کردن دسترسی تیم تاریخی و DELETE موقعیت/قالب/مصاحبه با حفظ history/مالی/lease انجام شد. ۱۶۰ تست PostgreSQL واقعی و build موفق؛ [PANEL_CLEANUP_API](PANEL_CLEANUP_API.md) قرارداد فرانت. حذف سؤال از قالب قبلاً با PUT وجود داشت و حفظ شد. مدل چند Recruiter در یادداشت‌های قدیمی دیگر قرارداد محصول نیست.

باقی مقصد: اتصال Owner ایمیلی فاقد شماره به موبایل معتبر همان حساب، اجرای migration تازه، حذف صفحات ایمیل/تیم و دکمه DELETE در Angular، اصلاح entitlement ویزارد گزارش‌شده در مرورگر. این موارد در مقصد/ریپوی فرانت اجرا نشدند؛ backlog مجوز اجرای خودکار قابلیت دیگر نیست.

## خطای 500 پنل و داشبورد مقصد — 2026-10-06: تشخیص مقصد باز است

build و ۱۷ تست مرتبط PostgreSQL محلی موفق‌اند؛ وابستگی هر دو مسیر به migrationهای AddPanelWizard و AddSubscriptionCreditPanel تأیید شد، ولی اجرای آن‌ها در مقصد معلوم نیست. [Check-PanelSchema.sql](../scripts/Check-PanelSchema.sql) تشخیص فقط‌خواندنی schema است و روی DB قدیمی/جدید مصنوعی بررسی شد. دریافت لاگ traceId گزارش‌شده و نتیجه این اسکریپت از DB واقعی API، سپس اصلاح بر اساس علت تأییدشده باقی است؛ این یادداشت مجوز تغییر production نیست.

## HY-016 — API اشتراک و اعتبار HTML (2026-10-05): Verified

روی `codex/subscription-credit-panel` از panel-wizard، overview/زمان از اعتبار آزاد، quote/checkout با tax و discount و کنترل stale summary، ارتقای روزشمار با cutover append-only، فیلتر/جمع یکپارچه سوابق و invoice JSON پیاده شدند. Swagger فارسی، scope/Owner و snapshot مالی حفظ‌اند؛ ۱۶۱ تست PostgreSQL واقعی موفق، شش تست جدید و migration/script دوباره با داده قدیمی مصنوعی بررسی شدند. [SUBSCRIPTION_CREDIT_API](SUBSCRIPTION_CREDIT_API.md) قرارداد فرانت است. فرض کاری ارتقا دوره کامل جدید با کسر روزهای کامل مانده است؛ صف آینده یا pending باید پیش از upgrade حل شود. صرف expiry نمایش quote مجوز نادیده‌گرفتن پول احتمالی نیست.

باقی‌مانده عملیاتی: adapter و recovery/reconciliation درگاه واقعی (HY-403)، اعمال migration و تنظیم tax مقصد، PDF/فاکتور بیرونی و frontend در ریپوی خودش. این‌ها در این نوبت اجرا نشده‌اند؛ gateway runtime همچنان Disabled و checkout جدید 503 است. این backlog مجوز push/deploy یا شروع خودکار قابلیت بعدی نیست. HY-015 و متن «اشتراک دست‌نخورده» زیر مربوط به نوبت پیشین‌اند.

## HY-015 — تطبیق API با پنل ویزاردی (2026-10-05): Verified

درخواست مستقل HTML پنل انجام شد: API ساده موقعیت و قالب، دعوت multipart با رزومه و انتخاب مستقل نوع، snapshot، فهرست/فیلتر/metadata، resend/cancel، دانلود scoped و داشبورد. راهنمای [PANEL_WIZARD_API](PANEL_WIZARD_API.md)، migration جدید و ۱۵۵ تست موفق واقعی PostgreSQL (چهار تست جدید ویزارد) آماده‌اند؛ Swagger فارسی حفظ است. اشتراک و اعتبار طبق خواست کاربر دست‌نخورده‌اند. این تکمیل frontend یا تمام امکانات HTML نیست: integration ارسال ایمیل/SMS با لینک فرانت، ویدیو، تنظیمات مدت سازمانی و migration محیط مقصد جداگانه باقی‌اند؛ backlog مجوز اجرای خودکارشان نیست. پاسخ API نبود delivery/video را صریح می‌گوید. فرم جدید نیاز به ساخت دستی مهارت/بانک موقعیت ندارد؛ محدودیت قدیمی راهنمای HR برای این مسیر superseded است.

پیشرفت HY-404: اسکریپت دستی اعطای Advanced و اعتبار تست همکاران در `scripts/Grant-ColleagueTestAccess.sql` با ledger و idempotency روی PostgreSQL واقعی بررسی شد. اجرای مقصد و provisioning عملیاتی همچنان انجام نشده‌اند؛ این اعطای غیرنقدی با provider داخلی باید از درآمد نقدی مستثنا شود.

## Pricing — نتیجه 2026-10-01

| شناسه | عنوان | وضعیت | شواهد/باقی‌مانده |
|---|---|---|---|
| HY-401 | کاتالوگ پویا، snapshot، اشتراک و API billing/admin | Verified | PostgreSQL واقعی، tenant scope، کاتالوگ و snapshot، queued purchase و حداقل مرز SystemAdmin |
| HY-402 | کیف پول، ledger، رزرو و billing مصاحبه | Verified | race/idempotency/constraint، نرخ و capability snapshot، زمان سرور، cancellation/expiry، retention؛ جزء مجموعهٔ ۱۵۱ تست موفق |
| HY-403 | adapter پرداخت واقعی | NotImplemented | IPaymentGateway + pending record آماده؛ Disabled runtime. adapter، verification/recovery/reconciliation واقعی و تست sandbox provider لازم‌اند |
| HY-404 | آماده‌سازی عملیاتی Billing | NotStarted | provisioning مدیر، محصولات اعتبار، اشتراک/اعتبار اولیه، migration مقصد، پایان جلسات legacy، backup/cleanup رسانه و rotation کلید قدیمی |

قواعد و محدودیت‌های پذیرفته‌شده در [BILLING](BILLING.md): اندازه‌گیری window سرور با سقف ۱۲۰ ثانیه هر پاسخ، lazy expiry reservation، وضعیت اشتراک time-derived، قیمت IRT و accounting USD. precision decoder/capture و automation cleanup نیازمند تسک جدا هستند. proration، refund provider، forex، invoice/tax و Angular جزو این درخواست اجرا نشدند. این backlog مجوز push/deploy یا شروع خودکار کار آینده نیست.

audit پیش از staging 2026-10-01: [INTERVIEW_AUDIT](INTERVIEW_AUDIT.md) مرجع نتیجه فعلی است. IOException provider و خطای خواندن cache اصلاح شدند؛ secret default حذف و local settings حفظ شد؛ شکست تست الزام کلید رفع شد. تست کامل ۱۱۸/۱۱۸ با PostgreSQL واقعی و smoke زنده TTS/STT/LLM موفق‌اند. نیاز به smoke اولیه NetArz که در یادداشت قبلی باز بود، اکنون انجام شد؛ آزمون ظرفیت/کیفیت طولانی‌مدت همچنان انجام نشده است. schema/API بدون تغییر و migration تازه لازم نیست. کار باقی‌مانده واقعی: rotation کلید قبلی پیش از staging، secret استقرار خارج Git، volume/retention/orphan cleanup/backup و تنظیم timeout proxy. قابلیت آینده‌ای خودکار فعال یا اضافه نشد.

تحویل فرانت‌اند 2026-10-01: قرارداد و پرامپت اجرای حداقلی در [FRONTEND_INTERVIEWS_HANDOFF](FRONTEND_INTERVIEWS_HANDOFF.md) آماده است. پیاده‌سازی فرانت همچنان در ریپوی خودش انجام می‌شود؛ این مستند تکمیل HY-301 یا مجوز توسعه endpointهای فهرست جدید نیست. کمبود APIهای فهرست در راهنما صریح ثبت شده است.

بررسی همین تحویل: تست الزام کلید NetArz شکست دارد چون default فعلی ApiKey خالی نیست. بازگرداندن default خالی و تأمین کلید خارج Git نیاز به اصلاح مستقل دارد؛ مقدار موجود در مستند/خروجی ثبت نشد. نتیجه فعلی تست مرتبط ۵۸ موفق/۱ شکست است و نتیجه تاریخی ۹۴ موفق را نباید نتیجه checkout فعلی دانست.

ادامهٔ 2026-10-01: vertical slice درخواست‌شده پیاده شد؛ HY-101 از منظر چرخه جلسه/توکن/API و HY-201 از منظر جریان صوتی/lease/fallback/cache اکنون Verified با ۹۴ تست روی PostgreSQL واقعی و provider آزمایشی‌اند. NetArz زنده هنوز به‌عنوان dependency محیط مقصد بررسی نشده است. فهرست «کار باقی‌مانده» در یادداشت شروع پایین تاریخی است؛ items مربوط به EF/API/storage/HttpClient/concurrency/retry این نوبت انجام شدند. گزارش HR شامل شواهد اولیه است؛ گزارش محصولی HY-301 یا frontend کامل ساخته نشده است.

پیگیری‌های عملیاتی همین قابلیت: smoke-test NetArz با کلید/مدل حساب مقصد، migration مقصد در تسک مجاز مستقل، volume خصوصی مشترک در صورت چند instance و سیاست retention/cleanup فایل orphan. duration واقعی صوت decode نمی‌شود و محدودیت فعلی در INTERVIEWS مستند است. ایمیل دعوت داوطلب ارسال نمی‌شود؛ response توکن و route را به caller معتبر HR می‌دهد. پیش‌تولید batch صوت و جلوگیری کامل از TTS هم‌زمان cache miss توسعهٔ بعدی‌اند؛ cache hit عادی اکنون provider را فراخوانی نمی‌کند.

به‌روزرسانی 2026-10-01: درخواست مستقل ماژول Candidate AI Interview دریافت و روی `codex/candidate-ai-interview` شروع شد. HY-101 و HY-201 اکنون InProgress هستند؛ وضعیت NotStarted در جدول قدیمی پایین مربوط به پیش از این درخواست است. هستهٔ مدل و policy و validation با ۲۴ تست جدید آماده است؛ قابلیت قابل استفاده از API هنوز وجود ندارد.

کار باقی‌ماندهٔ همین درخواست: مدل scoped CandidateApplication و Skill/PositionSkill، بانک سؤال قابل تنظیم و snapshot از قالب؛ EF mapping/FK مرکب/unique answer و concurrency و migration تولیدشده؛ سرویس‌های start/current/submit/retry/complete با commit کوتاه قبل و بعد AI؛ توکن دعوت اختصاصی جلسه و تفکیک از HR JWT؛ storage خصوصی و اعتبارسنجی صوت و دانلود مجاز؛ NetArz STT/LLM/TTS با options/timeouts/diagnostics؛ cache صوت با hash متن/model/voice/speed؛ endpointها و قرارداد browser؛ تست HttpClient، PostgreSQL و race/recovery؛ راهنمای تنظیمات و نمونه HTTP. HY-301 گزارش HR و frontend همچنان خارج کار این نوبت هستند.

| HY-014 | انتقال احراز هویت به موبایل و OTP | ۱ | InProgress | تصمیم مهاجرت حساب‌های ایمیلی/Recruiter، کلید و قالب کاوه‌نگار | حذف یا انتقال مسیرهای قدیمی، اجرای VerifyLookup واقعی، migration استقرار و تست انتهابه‌انتها |

پیاده‌سازی `otp/request`، `otp/verify`، migration و تست PostgreSQL انجام شد. `PUT /api/organization` موجود برای تکمیل نام شرکت استفاده می‌شود. تصمیم انتقال حساب‌های قبلی و دعوت اعضا هنوز باز است؛ مسیرهای قدیمی تا آن زمان باقی می‌مانند.

حالت کد ثابت برای Development افزوده و روی PostgreSQL آزمایش شد. تنظیم کلید و قالب واقعی کاوه‌نگار و بررسی ارسال واقعی همچنان در HY-014 باز است.

پیگیری موقت: پس از فراهم‌شدن SMTP، `Security:AutoConfirmEmailOnRegistration` را false کنید و ثبت‌نام/تأیید ایمیل را روی PostgreSQL واقعی دوباره بررسی کنید. تا آن زمان ایمیل حساب‌های تازه بدون اثبات مالکیت تأیید می‌شود.

وضعیت‌ها مطابق [STATUS](STATUS.md). تسک آینده نیازمند تعریف و درخواست مستقل است.

| شناسه | عنوان | مرحله | وضعیت | وابستگی | معیار پذیرش |
|---|---|---|---|---|---|
| HY-001 | ساختار Solution و persistence | ۱ | Verified | — | build و migration واقعی PostgreSQL موفق |
| HY-002 | حساب سازمان و نشست امن | ۱ | Verified | HY-001 | ثبت‌نام اتمیک، تأیید، login، rotation/replay/رقابت، reset/logout تست شوند |
| HY-003 | سازمان و تیم | ۱ | Verified | HY-002 | Owner/Recruiter، دعوت یک‌بارمصرف و scope سازمان تست شوند |
| HY-004 | موقعیت و قالب | ۱ | Verified | HY-001, HY-002 | CRUD دامنه، ترتیب و شناسه سؤال و RowVersion بررسی شوند |
| HY-005 | داشبورد و قرارداد HTTP | ۱ | Verified | HY-003, HY-004 | آمار واقعی scoped، cookie/CSRF/CORS و ProblemDetails |
| HY-006 | اجرای Docker و SMTP محلی | ۱ | Blocked | Docker Linux engine فعال؛ PostgreSQL:5433 | compose build/up، اعمال migration و دریافت سه نوع ایمیل در Mailpit؛ بدون ایمیل واقعی |
| HY-007 | بازبینی اتصال مصرف‌کننده | ۱ | NotStarted | HY-005 | UI مستندات و نمونه HTTP با cookie jar؛ یک مرورگر روی HTTPS یک سایت برای flow refresh/CSRF بررسی شود |
| HY-008 | مستندات تحویل | ۱ | Implemented | HY-001 تا HY-005 | README/AGENTS/PROJECT/ARCHITECTURE/STATUS/BACKLOG/HANDOFF با کد و تست هماهنگ |
| HY-101 | داوطلب، دعوت مصاحبه و چرخه جلسه | ۲ | NotStarted | پایان ۱ و تعریف تسک | مدل وضعیت و snapshot قالب طراحی و با معیار مستقل تأیید شود |
| HY-201 | مصاحبه صوتی نوبتی STT/LLM/TTS | ۳ | NotStarted | HY-101 و تعریف تسک | جریان صوتی و خطا/بازیابی با دامنه مستقل مشخص شود |
| HY-301 | ارزیابی و گزارش مستند HR | ۴ | NotStarted | HY-201 و تعریف تسک | گزارش شواهد قابل بررسی و تصمیم انسانی |
| HY-F01 | پنل Angular | مسیر جدا | NotStarted | API مرحله ۱ | قرارداد احراز هویت و API در تسک frontend مستقل مصرف شود |

نزدیک‌ترین کارها: HY-006 و سپس HY-007؛ نواقص واقعی مرحله اول بر توسعه قابلیت آینده اولویت دارند. فعال‌شدن Docker مانع HY-006 را رفع می‌کند. هیچ push، deploy یا تغییر production جزو این backlog به‌تنهایی مجاز نیست.
پیشرفت HY-007: خطای 401 UI مستندات رفع و redirect/HTML/CSS با تست HTTP تأیید شد؛ بررسی مرورگر HTTPS و جریان کامل نمونه‌ها همچنان باقی است.

راهنمای فارسی کوتاه شروع، عنوان فارسی همهٔ عملیات‌ها و ترتیب حساب جدید/موجود در Swagger اضافه و با تست OpenAPI بررسی شد. بررسی عملی جریان مرورگر HTTPS همچنان در HY-007 باز است.

تنظیمات محلی توسعه: JWT signing key و اتصال PostgreSQL در appsettings.Development.local.json خارج Git قرار گرفتند؛ اجرای واقعی Docker/SMTP و تست‌های PostgreSQL همچنان در HY-006 باز هستند.

| HY-009 | مرتب‌سازی Controllers/Services/DTOs/Interfaces | ۱ | Verified | HY-001 | build پس از جابه‌جایی موفق و دسترسی دیتابیس فعلی حفظ شود |

| HY-010 | تعیین تکلیف انتشار OpenAPI در Production | ۱ | Verified | درخواست دسترسی Swagger روی سرور | Swagger و OpenAPI عمومی در Production؛ تست مستقل دسترسی 200 و حفاظت API 401 موفق |

| HY-011 | انتقال از SQL Server به PostgreSQL | ۱ | Verified | تأیید عدم نیاز به داده قدیمی | provider، migration جدید، قفل و RowVersion، compose و ۲۳ تست مرتبط PostgreSQL موفق؛ شکست مستقل HY-010 پابرجاست |

| HY-012 | CSRF پشت reverse proxy | ۱ | Implemented | هدر `X-Forwarded-Proto: https` و محدودیت شبکه پورت داخلی در سرور | تست محلی Production با forwarded HTTPS پاسخ 200 و کوکی Secure؛ پس از انتشار پاسخ واقعی سرور بررسی شود |
| HY-013 | تشخیص 500 ثبت‌نام Production | ۱ | InProgress | لاگ همان درخواست و دسترسی سرور | همسان‌سازی traceId لاگ و پاسخ و حذف secretهای تنظیمات Git انجام شد؛ علت با exception و SQLSTATE سرور، اتصال PostgreSQL و migration تأیید شود |

## تنظیمات AI در appsettings — 2026-10-02

روی برنچ `codex/ai-appsettings` از `dev`، بخش `AI` در `src/HireYar.Api/appsettings.json` اضافه شد: Provider=NetArz، BaseUrl، مدل‌های STT/LLM/TTS، voice، speed و سه timeout. binding موجود `AI:NetArz` و انتخاب `AI:Provider` همین تنظیمات را مصرف می‌کنند؛ override محیطی/local همچنان اولویت دارد. ApiKey در محل فعلی و تمام فایل‌های کلید دست‌نخورده ماند؛ کلید تازه در appsettings اضافه یا کپی نشد. یادداشت‌های تاریخی Disabled برای پیش‌فرض مشترک دیگر وضعیت جاری این برنچ نیستند.

Build موفق با صفر warning/error؛ تست‌های InterviewProviderTests و InterviewAudioTests: ۴۳ موفق، صفر شکست/Skip. تست کلید خالی اکنون مقدار خالی را صریحاً فقط در fixture خود می‌دهد تا وابسته به default فعلی نباشد. تماس واقعی AI، تست PostgreSQL، migration، commit، push و deploy انجام نشدند.

## پیگیری بررسی مرورگر مصاحبه — 2026-10-07

باز: پاسخ صوتی مقصد ثبت شده ولی transcript/ارزیابی آماده نشده؛ UI با صفر پاسخ پردازش‌شده و زمان صفر، بازیابی retry را عرضه نمی‌کند. علت اولیه با شواهد UI قطعی نیست. پیش از تغییر runtime، وضعیت answer/canRetry در current و failureCode/لاگ STT یا lease مقصد بررسی شود؛ سپس اولویت نمایش ProcessingAnswer/retry نسبت به اتمام زمان در فرانت تطبیق داده شود. این پیگیری مجوز deploy یا ارسال مجدد داده واقعی به AI نیست.

شواهد جدید 2026-10-07: لاگ مقصد stt_http_404 و tts_http_404 با provider=NetArz را تأیید کرد؛ علت اولیه اکنون شکست HTTP سرویس صوت است. پیگیری باز: تطبیق provider/BaseUrl/مدل مؤثر سرور با قرارداد همان سرویس‌دهنده و اصلاح نمایش retry در فرانت. دلیل دقیق 404 هنوز بدون response پالایش‌شده provider مشخص نیست؛ انتخاب Liara از این لاگ قابل ادعا نیست.

پیگیری Liara 2026-10-07: provider طبق لاگ Liara است ولی STT/TTS هر دو 403 می‌گیرند. کلید AI و scope همان پروژه، مدل‌های مجاز پلن و BaseUrl دقیق کنسول با /api/v1/{project} بررسی شوند. STT مستند openai/whisper-large-v3 است؛ مدل جاری openai/gpt-transcribe مستند نشده. علت قطعی 403 نیازمند error code/message پالایش‌شده مقصد است؛ تغییر حدسی کلید/پلن یا retry واقعی انجام نشد.

تکمیل محلی دو اصلاح مجاز Liara — 2026-10-07: BaseUrl و STT در appsettings.json اصلاح شدند؛ ApiKey و سایر تنظیمات حفظ شدند. build و 55 تست مرتبط موفق. تأیید رفع 403 در مقصد، scope کلید/پلن و ایراد retry فرانت همچنان باز هستند؛ override مقصد باید با تنظیمات اصلاح‌شده تطبیق داده شود.

بازبینی appsettings — 2026-10-07: JSON فایل‌های موجود معتبر، BaseUrl و STT اصلاح‌شده Liara صحیح، ApiKey نسبت به HEAD یکسان و قیود timeout/speed/lease (پیش‌فرض 300 ثانیه) معتبرند. نکته باقی‌مانده: AI:Provider فایل مشترک هنوز NetArz است؛ local override توسعه و AI__Provider در محیط این بررسی وجود ندارند. انتخاب Liara سرور از لاگ قبلی تأیید شده ولی تنظیمات مؤثر مقصد این نوبت خوانده نشد. درخواست صرفاً بررسی بود؛ تنظیمات تغییر نکردند و build/test تکرار نشد (55 تست و build نوبت اصلاح موفق بودند).

## تنظیمات Liara مطابق curl موفق کاربر — 2026-10-07

شواهد مستقیم کاربر، STT موفق با BaseUrl دارای الگوی /api/{project}/v1 و model=openai/gpt-transcribe را نشان داد. بنابراین توصیه قبلی برای جابه‌جایی مسیر و تغییر مدل بر اساس فهرست مستندات، برای این پروژه نادرست بود و این یادداشت جایگزین نتیجه آن توصیه است. در appsettings.json آدرس دقیق ارائه‌شده کاربر بدون تغییر شناسه پروژه و مدل openai/gpt-transcribe اعمال و AI:Provider=Liara شد. ApiKey و همه مقادیر دیگر عیناً حفظ و به‌صورت بدون نمایش مقدار بررسی شدند. transcript واقعی کاربر یا کلید در مستندات ثبت نشد.

build صفر warning/error و 55/55 تست provider/selection/audio موفق، صفر شکست/Skip. STT واقعی طبق curl کاربر موفق است؛ تماس تازه AI توسط agent انجام نشد و موفقیت TTS یا مسیر کامل مصاحبه هنوز تأیید نشده. Overrideهای سرور ممکن است تنظیمات فایل را جایگزین کنند. مجوز کلید/پلن و علت 403 قبلی از این curl به‌تنهایی قابل تعیین نیست. push/deploy یا تغییر DB انجام نشد.

## بررسی معرفی صوتی، تلفظ و تأخیر — 2026-10-07

بررسی کد روی dev: appsettings مشترک AI:Provider=Liara، STT=openai/gpt-transcribe، LLM=openai/gpt-5.6-luna و TTS=openai/tts-1-hd با nova و speed=1 دارد؛ override مقصد بررسی نشد. یادداشت‌های قدیمی NetArz پیش‌فرض وضعیت فعلی فایل نیستند. معرفی کارجو فقط متن stage/advance می‌پذیرد و canRecord در CandidateIntroduction=false است؛ مسیر آپلود/STT معرفی و صوت stageMessage وجود ندارد. معرفی شرکت مرحله persistشده و متن snapshot دارد، اما audio URL مرحله ارائه نمی‌شود؛ Angular در این ریپو نیست.

مسیر پاسخ STT سپس LLM و Map(generateAudio=true) است؛ cache miss تولید کامل TTS سؤال بعدی را پیش از برگشت پاسخ منتظر می‌ماند. ResponseHeadersRead داخل provider به معنی streaming به کلاینت نیست؛ پاسخ کامل در MemoryStream جمع می‌شود. cache scoped صوت و durationMs عملیات stt/llm/tts از قبل وجود دارند. مستر پرامپت فقط به LLM می‌رسد؛ درخواست TTS فقط model/input/voice/speed/response_format دارد. طبق مستند رسمی OpenAI، instructions در tts-1-hd پشتیبانی نمی‌شود؛ پشتیبانی مدل جایگزین در Liara آزمایش نشده. اصلاح تلفظ وابسته به جمله، آماده‌سازی صوت ثابت پیش از نوبت و جداسازی آماده‌شدن صوت از پاسخ API گزینه‌های بررسی‌اند، هنوز پیاده نشده‌اند. context رزومه/بانک/گفتگو برای کاهش تأخیر حذف نشود.

تنظیمات، کلیدها و کد runtime تغییر نکردند؛ build نهایی صفر warning/error و 89/89 تست provider/selection/audio/rules موفق، صفر Skip. اجرای نخست build به‌علت تداخل با build تست قفل فایل داشت؛ اجرای ترتیبی مجدد موفق شد. PostgreSQL و AI واقعی و زمان‌های مقصد در این بررسی آزمایش نشدند؛ علت غالب تأخیر بدون durationMs مقصد معلوم نیست. push/deploy/تغییر production انجام نشد.

## تکمیل معرفی صوتی و صوت مرحله‌ها — 2026-10-07: Verified محلی

طبق قرارداد فرانت روی dev، POST multipart introduction بدون questionId با audio/clientTurnId الزامی و durationSeconds/lastSpeechEndUtc/video اختیاری اضافه شد. InterviewIntroduction مستقل، unique جلسه و FK مرکب سازمان/جلسه، RowVersion و lease، فایل خصوصی، replay بدون STT دوباره، retry بدون upload و recovery worker دارد. transcript معتبر تنها شرط انتقال به Questions است؛ متن خالی یا شکست STT fallback ساختگی ندارد. معرفی progress/evidence/نمره/زمان مالی سؤال ایجاد نمی‌کند؛ transcript در context و تاریخچه داوطلب و HR موجود است و صوت/ویدیوی HR scoped و زیر retention قابل دریافت‌اند.

stageAudioUrl برای Welcome/درخواست معرفی/CompanyIntroduction/Goodbye فقط در مرحله جاری با Bearer دعوت ارائه می‌شود؛ GET در cache miss صوت را lazy تولید می‌کند، TTS ناموفق 503 و مرحله/متن محفوظ است. شرکت پیش از پایان سؤال‌ها 404 دارد؛ ادامه بعد از playback با تأیید فرانت است. backgroundRetryPending پاسخ و معرفی در lease فعال و backoff true می‌ماند؛ پس از موفقیت/آخرین شکست false. مسیر معرفی متنی قدیمی محفوظ است ولی معرفی صوتی پذیرفته‌شده را bypass نمی‌کند. [VOICE_INTRODUCTION_API](VOICE_INTRODUCTION_API.md) قرارداد نهایی OpenAPI/فرانت است. یادداشت‌های قبلی «STT معرفی وجود ندارد / canRecord معرفی=false» وضعیت قبل از این تغییرند.

build نهایی صفر warning/error؛ 198/198 تست PostgreSQL 18 اختصاصی محلی موفق، صفر شکست/Skip، شامل شش تست جدید معرفی/replay/ویدیو/تاریخچه/رسانه HR و scope/constraint، lease فعال و جلوگیری از bypass، recovery worker واقعی/backoff، خروجی STT خالی و سقف تلاش/retry، قطع درخواست و رقابت claim، و multipart/OpenAPI. tests/HireYar.Tests/TestResults/voice-introduction-final.trx. migration 20261007151257_AddVoiceIntroduction تولید و در fixture واقعی اعمال شد؛ HasPendingModelChanges=false در تست. delta idempotent artifacts/voice-introduction-migrations.sql دو بار روی baseline اختصاصی قبل از migration موفق و فقط یک history entry داشت؛ baseline این بررسی بدون داده legacy بود، بازنویسی داده تاریخی در migration وجود ندارد. downgrade در حضور معرفی صریحاً رد می‌شود.

باقی: مصرف قرارداد جدید در Angular، اعمال migration و بررسی فعال‌بودن worker در مقصد با مجوز مستقل، آزمون فارسی/STT/TTS/سکوت/VAD و دوربین واقعی. بهینه‌سازی streaming/آماده‌سازی سؤال‌ها و واژه‌نامه تلفظ در این تغییر پیاده نشده‌اند؛ مدل/کلید/appsettings دست‌نخورده‌اند. AI واقعی، push/deploy/commit و تغییر production انجام نشده‌اند. داده تست مصنوعی و SQL فقط محلی بود.

## ارسال اختیاری دعوت — 2026-10-08

روی dev، ارسال SMTP ایمیل و SMS پترن کاوه‌نگار در دعوت ویزارد/قدیمی/resend اضافه شد. InterviewNotifications:Email/Sms فلگ/config مستقل؛ SendEmail/SendSms پیش‌فرض false. پترن دعوت مستقل، token=کد شش‌رقمی و token2=لینک کامل؛ ApiKey خالی SMS از کلید OTP استفاده می‌کند. SMTP UTF-8 شامل نام/شغل/کد/لینک/انقضای UTC است. هر دو فلگ مشترک فعلاً false؛ کلیدهای قبلی محفوظ‌اند. ارسال بعد از commit، خارج قفل مالی و با نتیجه مستقل هر کانال انجام می‌شود؛ خطا لینک را rollback نمی‌کند. بدون retry/صف/ذخیره وضعیت تحویل یا migration؛ قطع فرایند پس از commit ممکن است ارسال را ناتمام بگذارد.

[INTERVIEW_NOTIFICATION_API](INTERVIEW_NOTIFICATION_API.md) قرارداد فرانت و appsettings است. Angular در ریپو نیست؛ دو تیک باید به sendEmail/sendSms وصل شوند. ارسال واقعی یا پذیرش لینک کامل در پترن مقصد تست نشده؛ پترن و SMTP را کاربر تنظیم می‌کند. NotConfigured/NotRequested/Sent/Failed فقط نتیجه همان درخواست‌اند، Sent تأیید دریافت provider است نه تحویل نهایی. ساخت دعوت برای retry ارسال تکرار نشود؛ resend لینک قبلی را باطل می‌کند.

Verification: build صفر warning/error؛ suite نهایی 240/240 موفق، صفر Failed/Skipped، PostgreSQL 18.3 اختصاصی loopback:55473 و provider جعلی، SMTP فقط loopback. tests/HireYar.Tests/TestResults/invitation-notifications-verified.trx. دوازده case مستقل اعلان و یک integration دعوت/commit/resend اضافه شدند؛ شکست اولیه فقط assertion متن ایمیل Base64 بود، رمزگشایی تست اصلاح و suite کامل بازاجرا شد. تغییرات موجود docs و InterviewReportService دست‌نخورده حفظ شدند. commit/push/deploy/production/ارسال واقعی انجام نشد.

## بررسی تایم‌اوت ایمیل دعوت — 2026-10-12

بررسی خواندنی روی dev با Git اولیه پاک: تنظیمات مشترک Email.Enabled=true، SMTP=mail.hireyar.com:587، EnableSsl=true (STARTTLS)، TimeoutSeconds=10 و Sms.Enabled=false است؛ تنظیمات اعلان در فایل‌های local یا متغیرهای محیطی این اجرا override ندارند. تنظیمات مؤثر سرور مقصد بررسی نشده‌اند. مسیر دعوت/ویزارد/resend پس از commit ایمیل را منتظر می‌ماند؛ خطای SMTP/timeout فقط Failed می‌شود و جزئیات تشخیصی ثبت نمی‌شود.

DNS خارج sandbox برای mail.hireyar.com دو A عمومی 185.143.233.132 و 185.143.234.132 و MX دامنه به همین mail را برگرداند. probe فقط اتصال TCP، بدون احراز هویت یا ارسال ایمیل، روی پورت‌های 587 و 465 هر دو پس از حدود 6 ثانیه timeout شدند. این شواهد شکست اتصال از محیط فعلی را نشان می‌دهد؛ علت نهایی میان DNS/proxy، سرویس SMTP و فایروال/مسیر شبکه، و رفتار خود سرور مقصد هنوز تفکیک نشده است. مقصد DNS شبیه IPهای anycast آروان است؛ فعال‌بودن proxy باید در پنل DNS بررسی شود، قطعی فرض نشده. Host باید endpoint واقعی SMTP باشد؛ افزایش timeout یا تغییر رمز بدون بررسی اتصال اثبات رفع مشکل نیست. 465 در SmtpClient فعلی implicit TLS پشتیبانی نمی‌شود؛ تغییر صرف Port به 465 راه‌حل نیست.

Build واقعی موفق با صفر warning/error؛ InvitationNotificationTests: 12 موفق، صفر شکست/Skip، SMTP فقط loopback. خطاهای DNS/build اجرای sandbox با اجرای خارج sandbox برطرف شدند و شاهد مشکل DNS واقعی نیستند. کد، تنظیمات و secretها تغییر نکردند؛ ارسال واقعی، تماس احراز هویت SMTP، PostgreSQL، push/deploy و production انجام نشدند. ادامه: بررسی proxy رکورد mail و endpoint/پورت اعلامی سرویس ایمیل و probe اتصال از میزبان API؛ پس از اتصال، بررسی STARTTLS و سپس احراز هویت با حفظ داده حساس.

بررسی تکمیلی SMTP — 2026-10-12: با مجوز صریح کاربر، یک تلاش ارسال ایمیل آزمایشی با تنظیمات مشترک appsettings انجام شد؛ گیرنده و credentials ثبت نشده‌اند. ارسال موفق نشد: SocketException و TimeoutReached=true، زمان واقعی حدود 42 ثانیه با TimeoutSeconds=10. لغو SendMailAsync در مرحله اتصال جاری سقف قطعی 10 ثانیه برای زمان انتظار ایجاد نکرد؛ علت طولانی‌شدن درخواست دعوت نیز می‌تواند همین مرحله باشد. SMTP پذیرش پیام را تأیید نکرد. هیچ دعوتی ساخته نشد، تنظیمات/کد تغییر نکردند و تلاش مجدد خودکار انجام نشد.

## بررسی ترتیب پرسش‌ها — 2026-10-12

کد جاری InterviewPolicy، InterviewSession.GetProgressiveQuestions، CandidateInterviewService و snapshot ویزارد با ADVANCED_QUESTIONS_API تطبیق داده شد: دعوت موضوعی تازه از متوسط اولین موضوع در ترتیب قالب شروع می‌شود؛ correctness>=0.7 و confidence>=0.6 فقط سخت همان موضوع را مجاز می‌کنند، سایر حالت‌ها آسان همان موضوع. بعد از سؤال دوم موضوع بسته است. عمومی ترتیب موضوع قالب را حفظ می‌کند؛ تخصصی با adaptive_questions می‌تواند متوسط موضوع بعد را از shortlist موضوع‌های نپرسیده انتخاب کند و در نبود پیشنهاد معتبر اولین گزینه مرتب‌شده انتخاب می‌شود. سؤال اول همیشه انتخاب سرور است. تعامل clarify/followUp/redirect سؤال اصلی را عوض نمی‌کند. قالب ساده عمومی بر اساس Order است؛ تخصصی بر اساس شواهد کمتر، الزامی/اولویت مهارت، فاصله سطح هدف و سپس Order مرتب می‌شود و AI فقط از گزینه‌های مجاز انتخاب می‌کند. دعوت/resend تاریخی سیاست snapshot قبلی را حفظ می‌کنند.

Build جاری پیش‌تر در همین بررسی‌ها با صفر warning/error موفق بود؛ اجرای مرتبط تازه InterviewRulesTests و ProgressiveTopicRulesTests: 48 موفق، صفر شکست/Skip. بررسی فقط کد و تست محلی بود؛ جلسه واقعی مقصد/Angular بررسی نشده، کد و تنظیمات تغییر نکردند و push/deploy انجام نشد.

## بررسی گزارش انتخاب نادرست سؤال — 2026-10-12

مرور مسیر کامل validator → FinishAnswer → بازانتخاب policy و snapshot ویزارد انجام شد. چهار case تشخیصی به ProgressiveTopicRulesTests اضافه شدند. بازتولید قطعی محلی: دو تصمیم با correctness=0.9 و confidence=0.9؛ اگر پیشنهاد easy همان موضوع مجاز باشد، نمره حفظ و سرور hard را تحمیل می‌کند؛ اگر پیشنهاد medium موضوع دیگر خارج shortlist باشد، validator کل تصمیم را fallback می‌کند، نمره null می‌شود و سرور easy همان موضوع را می‌پرسد. بنابراین خرابی صرف انتخاب nextQuestionId مدل می‌تواند همراه ارزیابی عددی صحیح مسیر را به آسان ببرد. این نتیجه رفتار فعلی رد کل تصمیم نامعتبر است؛ علت جلسه گزارش‌شده بدون شناسه/شواهد مقصد هنوز معلوم نیست و سیاست fallback پذیرفته‌شده بازطراحی نشد.

تست ترتیب نیز تأیید کرد بعد از دو سؤال موضوع اول، HR فقط متوسط موضوع دوم را مجاز می‌کند ولی تخصصی چهار متوسط باقی‌مانده را مجاز می‌کند؛ ترتیب خطی همه موضوع‌ها برای تخصصی تضمین نیست. کنترل سرور برای متوسط→یکی از آسان/سخت همان موضوع و منع سؤال سوم برقرار است. کیفیت تشخیص correctness توسط مدل، متن خروجی STT، صحت محتوای برچسب easy/medium/hard و snapshot تاریخی توسط تست synthetic اثبات نمی‌شوند. تست‌های قبلی نمره مدل جعلی/مستقیم دارند؛ موفقیت آن‌ها اثبات مسیر واقعی AI نیست. ارسال topics=null و بانک تخت، سیاست ساده می‌سازد؛ resend سیاست تاریخی را نگه می‌دارد.

Build تازه صفر warning/error؛ 93/93 تست ProgressiveTopicRulesTests، InterviewRulesTests و InterviewProviderTests موفق، صفر شکست/Skip، شامل چهار case جدید. git diff --check موفق. کد runtime/تنظیمات/DB تغییر نکرد؛ تست PostgreSQL تازه یا AI واقعی/Angular/مقصد اجرا نشد. از کاربر نوع انحراف، شناسه جلسه و زمان تست درخواست شد؛ ادامه برای تعیین علت واقعی با flagهای snapshot، difficulty/topic سؤال‌های واقعی، grade ذخیره‌شده و SelectionSource/FailureCode پالایش‌شده همان جلسه است. push/deploy/production انجام نشد.

## اصلاح انتخاب سؤال با حفظ ارزیابی معتبر — 2026-10-12: Verified محلی

با درخواست صریح کاربر، در InterviewDecisionValidator خطای صرف nextQuestionId برای advance معتبر در جلسه progressive به حذف پیشنهاد سؤال محدود شد؛ نمره/اطمینان و شواهد معتبر حفظ می‌شوند. CandidateInterviewService پس از FinishAnswer همان policy موجود را enforce می‌کند: متوسط صحیح به سخت همان موضوع، ضعیف/اطمینان پایین به آسان، سپس متوسط موضوع تازه. diagnostics موجود policy_reselection انتخاب سرور را مشخص می‌کند. JSON، ارزیابی یا شواهد نامعتبر همچنان fallback کامل هستند؛ تعامل با nextQuestionId غیرnull رد می‌شود و رفتار غیرprogressive محفوظ است. ADR-039 و ADVANCED_QUESTIONS_API قرارداد جاری‌اند. بازتولید قدیمیِ حذف نمره در بخش بررسی قبل، وضعیت پیش از این اصلاح است.

تست تشخیصی به regression اصلاح تبدیل شد: پیشنهاد آسان همان موضوع، موضوع دیگر، UUID ناشناخته، سؤال جاری و null با نمره معتبر؛ آستانه دقیق و پاسخ ضعیف/اطمینان پایین؛ حفظ شواهد و جلوگیری از پذیرش نمره نامعتبر/ناقص، مهارت نامرتبط و تعامل نامعتبر. تست API واقعی PostgreSQL نیز پیشنهاد UUID ناشناخته را بعد از clarify ارسال و ذخیره correctness/confidence=0.8، انتخاب hard، policy_reselection و ادامه موضوع‌های بعد تا پایان را بررسی کرد.

Build نهایی صفر warning/error؛ 150/150 تست مرتبط rules/provider/context/advanced/API flow موفق، صفر شکست/Skip؛ PostgreSQL 18 اختصاصی فقط loopback:55479، مدل/STT/TTS جعلی و داده مصنوعی. فایل نتیجه tests/HireYar.Tests/TestResults/question-selection-fix.trx. git diff --check موفق. تغییر DB/schema/migration یا تنظیمات لازم نیست؛ کیفیت نمره AI واقعی، Angular و علت جلسه گزارش‌شده مقصد هنوز تأیید نشده‌اند. تغییرات موجود مستندات/تست قبلی حفظ شدند؛ commit/push/deploy/production انجام نشد.

## آماده‌سازی حساب سوپرادمین — 2026-10-12

با درخواست صریح کاربر و شماره حساب تعیین‌شده توسط او، حساب Owner موجود/فعال/موبایل‌تأییدشده در دیتابیس اتصال فایل اصلی appsettings.json بررسی و از طریق AdminAccounts.ProvisionAsync به سوپرادمین تجهیز شد. رمز تصادفی با Identity هش و بررسی مجدد CheckPasswordAsync موفق شد؛ IsSystemAdmin=true تأیید شد. اطلاعات هویتی، رمز و اتصال در مستندات/Git ثبت نشده‌اند. ابزار موقت در artifacts ignored اجرا و حذف شد؛ کد runtime تغییر نکرد. تنظیم مشترک SuperAdmin.Enabled=false و RandomKey خالی است؛ صرف آماده‌سازی حساب به معنی فعال‌بودن endpoint مقصد نیست. فعال‌سازی پنل/کلید ورود، restart، push/deploy و تغییر production branch انجام نشدند. رمز فقط به خود کاربر در پاسخ تحویل می‌شود.

## افزودن سوپرادمین با موبایل از پنل — 2026-10-12: Verified بک‌اند محلی

روی dev در worktree artifacts/dev-sync، POST /api/admin/users/super-admins با AdminGrantRequest.mobile و توکن مستقل policy=SystemAdmin اضافه شد. فقط حساب Owner موجود/فعال/موبایل‌تأییدشده مقصد قابل اعطا است؛ سازمان/عضویت آن تغییر نمی‌کند. رمز تصادفی 128-bit با CSPRNG تولید، از Identity هش و در پاسخ no-store همان grant برای تحویل به کاربر برگردانده می‌شود. مجوز/stamp و قفل/شمارنده اتمیک به‌روز می‌شوند. حساب غایب 404، نامناسب 400/admin_account_ineligible و سوپرادمین موجود 409/already_super_admin بدون reset رمز/نشست دارد. FOR UPDATE درخواست‌های هم‌زمان را سریال می‌کند. Controller/DTO/interface مطابق لایه‌های موجود؛ منطق مشترک با CLI استخراج شد. OpenAPI scheme=SuperAdmin و خلاصه فارسی دارد. migration، تغییر تنظیمات یا DB واقعی لازم نبود و انجام نشد.

Build نهایی صفر warning/error؛ suite مرتبط AdminAuthTests/AdminCaptchaTests/AuthTests/BillingTests: 46/46 موفق، صفر شکست/Skip. بعد از افزودن assertionهای نهایی OpenAPI و رد token باطل/OTP سوپرادمین، دو تست مربوط دوباره موفق شدند. PostgreSQL 18 اختصاصی loopback:55479 و داده/کپچای مصنوعی؛ ورود با رمز صادرشده، حفظ سازمان و hash/stamp، رد عادی/غایب/غیرفعال/تأییدنشده، درخواست تکراری و رقابت دو grant بررسی شدند. DB تست باقی نماند و cluster موقت متوقف/حذف شد. نتایج tests/HireYar.Tests/TestResults/admin-grant-verified.trx و admin-grant-final-authorization.trx؛ git diff --check موفق. خطای اولیه build فقط analyzer xUnit2031 بود و پیش از اجرای suite اصلاح شد.

ADR-040، PROJECT و SUPER_ADMIN_API به‌روز شدند؛ توضیح قدیمی «فقط CLI» با این API محدود و مجاز تکمیل شده است. تغییرات مستندات موجود حفظ شدند. Angular در این ریپو نیست؛ فرم موبایل و نمایش/کپی رمز فقط در حافظه طبق قرارداد باید در فرانت جدا پیاده شود. رمز/شماره واقعی کاربر در کد/Git/لاگ ثبت نشدند؛ هیچ حساب واقعی در این تغییر اعطا/بازنشانی نشد. فعال‌سازی مقصد/SMTP/SMS، commit/push/deploy و production branch تغییر نکردند.

## یکتایی عنوان با فاصله/نیم‌فاصله — 2026-10-12: Verified محلی

روی dev با Git پاک، TitleComparison مشترک در Application.Common به بررسی عنوان JobService و TemplateService اضافه شد. تمام whitespaceهای Unicode و U+200C در هر جای ورودی و عنوان ذخیره‌شده، همراه case انگلیسی، از مقایسه حذف می‌شوند؛ پس «برنامه نویس»، «برنامه‌نویس» و «برنامهنویس» تکراری هستند. expression مشترک Replace در EF به SQL ترجمه و EXISTS scoped اجرا می‌شود. نمایش عنوان به‌جز Trim قبلی محفوظ، normalized خالی 400/invalid_title است. Create/Update قدیمی/ویزارد و SaveWizard زیر قفل موجود سازمان اجرا می‌شوند. self-edit معادل، حفظ تاریخچه/کپی داخلی، scope سازمان، آرشیو/حذف ADR-037 محفوظ‌اند؛ داده‌های قبلی پاک/بازنویسی نمی‌شوند و migration نداریم.

Build واقعی صفر warning/error؛ 31/31 تست مرتبط عنوان/ویزارد/موضوعی موفق، صفر شکست/Skip روی PostgreSQL18 اختصاصی loopback:55479 و provider مصنوعی. دو تست جدید فاصله/نیم‌فاصله/بدون فاصله، تب/newline/NBSP/فاصله باریک و تمام‌عرض، create/update مسیرهای قدیمی/ویزارد، self-edit، سازمان دیگر، عنوان فقط نیم‌فاصله و حفظ Title/RowVersion/questions پس از conflict را بررسی کردند. تست رقابت موجود با دو شکل فاصله/نیم‌فاصله اصلاح و یک winner برای موقعیت و قالب تأیید شد. تست‌های قبلی archive/delete، رقابت rename و کپی دعوت نیز موفق‌اند. TRX: tests/HireYar.Tests/TestResults/title-spacing-verified.trx. DB تست باقی نماند و cluster موقت متوقف/حذف شد؛ git diff --check موفق.

ADR-041، PROJECT و PANEL_WIZARD_API به‌روزند؛ متن قدیمی «فقط Trim» وضعیت پیش از این اصلاح است. پاسخ 409 و codeهای job_title_duplicate/template_title_duplicate تغییر نکردند؛ Angular جدا باید همان پیام قبلی را کنار عنوان نشان دهد. DB/تنظیمات واقعی، secretها، commit/push/deploy و production تغییر نکردند.

## CORS و ادامه ضبط ویدیو بعد از رفرش — 2026-10-12: Verified بک‌اند محلی

با حفظ تغییرات موجود یکتایی عنوان روی dev، CORS API چهار هدر Content-Range/Accept-Ranges/ETag/Content-Disposition را برای originهای صریح expose می‌کند. پخش و دانلود=true ضبط/معرفی/پاسخ و بخش جدید دارای disposition مناسب و ETag هستند؛ S3 از HEAD، بخش جدید از hash واقعی، local تاریخی tag ضعیف دارد. token/سازمان/retention و مسیر API خصوصی محفوظ، origin ناشناس Allow-Origin ندارد. آزمون واقعی HTTP206، preflight Authorization/Range/If-Range، 401 با Origin و download attachment انجام شد؛ private media در S3 همچنان provider مصنوعی در integration داشت.

طبق انتخاب کاربر، API GET/POST candidate recording/segments، manifest مدیر و GET بخش خصوصی اضافه شد؛ فایل مستقل حداکثر40MiB، body42MiB، مجموع512MiB/14400ثانیه و1000بخش، شماره پیوسته و replay با uploadId/hashSHA256/metadata. metadata و cumulative limits زیر قفل سازمان، network خارج قفل، recheck مجوز قبلcommit و پاک‌سازی فایل loser/ناموفق هستند. حالت تک‌فایلی و بخش‌بندی مخلوط نمی‌شوند. Review/چاپ بخش‌ها را بدون ادعای کامل‌بودن نشان می‌دهند. مجوز completion فقط GET/POST بخش‌ها در grace10دقیقه قبلی را اضافه کرد؛ شروع دوباره/current/پاسخ جدید بسته‌اند. فایل‌های تاریخی تبدیل نشدند؛ زمان‌ها و سلامت codec اعلامی‌اند، فریم ذخیره‌نشده قبل refresh بازیابی نمی‌شود و فرانت باید Blobهای مستقل و pending را دوره‌ای persist/upload کند. RECORDING_SEGMENTS_API قرارداد ریپوی Angular جداست؛ ادغام فایل واحد طبق انتخاب کاربر انجام نشد.

Migration additive 20261012193519_AddInterviewRecordingSegments با PK جلسه/شماره، uploadId unique، FK سازمان/جلسه و check هر بخش ساخته شد؛ HasPendingModelChanges=false، اعمال در fixture واقعی PostgreSQL و رد downgrade دارای داده تأیید شدند. delta idempotent artifacts/recording-segments-migrations.sql تولید شد؛ خود delta جداگانه اجرا نشد. Docker temp خصوصی/volume مستقل، کلیدهای S3 الزامی از environment در production Compose و نمونه Nginx سقف514MiB بدنۀ multipart/timeout600s/no proxy buffering آماده‌اند. Docker image/ظرفیتtemp/CDN/proxy/credential/bucket مقصد و deploy/restart واقعی تأیید یا اعمال نشده‌اند. دستورهای compose config --quiet برای production و development با مقادیر مصنوعی موفق بودند؛ daemon/image واقعی اجرا نشد.

Build نهایی صفر warning/error؛ suite نهایی84/84 tests media/flow/auth/S3/audio موفق، صفر شکست/Skip روی PostgreSQL18 اختصاصی loopback:55479 و provider/CAPTCHA مصنوعی. چهار تست جدید segment resume/replay exact content، completion grace/انقضا، concurrency/totals، FK/سقف512MiB با metadata مصنوعی و S3 playlist اضافه شدند؛ تست تک‌فایلی قبلی CORS/download/عدم ترکیب حالت‌ها را بررسی می‌کند. تست media signature به معنی بررسی decoder/codec و پلیر واقعی نیست. اجرای نخست دو شکست ناشی از whitelist قدیمی مسیر تک‌فایلی در completion داشت؛ مسیر بخش‌ها با همان محدودیت زمانی اضافه و suite کامل مرتبط بازاجرا شد. نتیجه tests/HireYar.Tests/TestResults/video-resume-cors-final.trx. DB fixture صفر باقی ماند و cluster موقت متوقف/حذف شد. git diff --check موفق؛ secret/داده واقعی در فایل‌ها یا لاگ جدید ثبت نشد. ARCHITECTURE ADR-042، قرارداد ویدیو و PROJECT به‌روزند. باقی: migration و تنظیمات در مقصد با تسک انتشار، اتصال Angular/MediaRecorder مستقل و بررسی ویدیوی واقعی/رفرش/بار. commit/push/deploy و production branch تغییر نکردند.

## نام سازمان در تنظیمات حساب — 2026-10-13

بک‌اند Verified محلی: PUT /api/auth/me اکنون organizationName اختیاری را کنار fullName می‌پذیرد؛ نام روی Organization.Name موجود با scope حساب سرور و مجوز Owner ذخیره می‌شود. هر دو تغییر اتمیک، Trim و اعتبارسنجی۱تا۲۰۰/غیرwhitespace؛ حذف فیلد/null نام فعلی را حفظ می‌کند. GET me/organization مقدار مشترک جدید را می‌خوانند. [ACCOUNT_SETTINGS_API](ACCOUNT_SETTINGS_API.md) قرارداد فرانت است؛ migration لازم نیست. build صفر warning/error و21/21 تست مرتبط روی PostgreSQL18 اختصاصی محلی، صفر شکست/Skip، شامل دو regression تازه موفق شدند. تغییرات قبلی مستندات و GeminiStageAudioTests حفظ شدند؛ فرانت طبق درخواست کاربر تغییر نمی‌کند و پرامپت اتصال تحویل می‌شود. UI/داده واقعی/تنظیمات مقصد، commit/push/deploy و production تغییر نکردند؛ اتصال فرانت/انتشار انجام‌نشده و مستقل‌اند.

## دعوت موبایلی زیرمجموعه — 2026-10-13: Verified بک‌اند محلی

طبق درخواست صریح کاربر، API تیم Owner برای دعوت/فهرست/لغو/غیرفعال‌سازی و پذیرش صریح OTP همان موبایل به سازمان اضافه شد. یک Owner فعال و چند Recruiter، بدون انتقال حساب سازمان دیگر یا سازمان/trial/هدیه تازه برای عضو؛ تاریخچه و حساب‌های غیرفعال قبلی خودکار باز نمی‌شوند. Recruiter موقعیت/قالب/دعوت کارجو را مدیریت و گزارش/چاپ/رسانه همان سازمان را می‌خواند؛ تیم/نام سازمان/تمام billing/diagnostics و retry گزارش/تصمیم انسانی Owner هستند. deactivate همه refreshها را revoke و access را با IsActive/stamp باطل می‌کند؛ توکن قدیمی پس از reinvite هم معتبر نمی‌شود. دعوت سه‌روزه ثبت می‌شود، ارسال خودکار SMS/email دعوت اضافه نشده؛ OTP مسیر موجود است. [TEAM_MOBILE_API](TEAM_MOBILE_API.md) قرارداد کامل و پرامپت فرانت، ADR-046 تصمیم جاری است و یادداشت قبلی تیم حذف‌شده را supersede می‌کند.

Build صفر warning/error؛ suite کامل384/384 موفق، صفر شکست/Skip روی PostgreSQL18 اختصاصی loopback55493 و FFmpeg واقعی، provider مصنوعی/Disabled و SMTP محلی. هفت regression تازه شامل flow واقعی دعوت کارجو/گزارش، مجوز/scope، پذیرش/لغو/expiry/duplicate/OTP/CSRF، رقابت و stamp/reinvite و migrationguard؛ tests/HireYar.Tests/TestResults/mobile-team-verified.trx. اجرای کامل نخست به encoder دو fixture Production و نبود env codec برخورد کرد؛ بازاجرای ایزوله محیطی، بدون تغییر config/runtime نامرتبط، کامل موفق بود. restore/toolrestore و git diff --check موفق؛ EF مدل بدون drift. Migration20261013092724_AddMobileTeamInvitations در fixture واقعی اعمال، یکتایی Owner و guardDown تست و SQL idempotent artifacts/mobile-team-migrations.sql تولید شد؛ خود SQL جداگانه اجرا نشد.

باقی برای استفاده مقصد: اتصال پنل/صفحه پذیرش در Angular جدا طبق پرامپت قرارداد، رساندن id دعوت توسط مالک، اعمال migration پیش از نسخه جدید در تسک انتشار مستقل. محدودیت مالی/قابلیت‌های سازمان حفظ شده؛ SMS دعوت و آزمایش سرویس واقعی به این تغییر اضافه نشده‌اند. دیتابیس‌های fixture حذف و cluster موقت متوقف/پاک شد؛ تنظیمات/داده واقعی، تغییرات قبلی کاربر (از جمله GeminiStageAudioTests و مستندات/نام سازمان)، commit/push/deploy و production محفوظ‌اند.

## قابلیت‌های غیرمالی سازمان برای Recruiter — 2026-10-13: Verified محلی

کمبود گزارش‌شده فرانت تأیید و در API رفع شد: GET /api/organization/features برای Owner/Recruiter، DTO مجزای hasActiveSubscription/isTrial/startedAtUtc/expiresAtUtc/features(code/name/dataType/value)، از همان CurrentAsync و snapshot خرید/trial موجود؛ scope حساب سرور و no-store. نبود یا انقضای اشتراک200 با features=[]؛ داده قیمت/نرخ/کیف‌پول/پرداخت/پکیج در پاسخ نیست. تمام billing همچنانOwner، دسترسی مصاحبه/سؤالAI نقشRecruiter مستقل ازOwner و تابعقابلیت سازمان است؛ insufficient_ai_credit به مالک ارجاع می‌شود. TEAM_MOBILE_API و REGISTRATION_TRIAL_API قرارداد/پرامپت فرانت به‌روز، تکمیل ADR-046 ثبت شد؛ endpoint قدیمی financial عوض نشد، schema/migration/config تازه ندارد.

build نهایی صفر warning/error؛42/42 تست مرتبط MobileTeamTests/RegistrationTrialTests/BillingPanelTests/AuthTests موفق، صفر شکست/Skip، PostgreSQL18 اختصاصی loopback55494 و provider مصنوعی/Disabled. regression تازه paid+trial، برابری source billing مالک، پاسخ بدونفیلدمالی، scope/queryignored، no-store، بی‌اشتراکی/انقضا/401 و حفظ403 billingRecruiter را بررسی کرد؛ organization-features-verified.trx. اجرای اولیه تنها تست انقضا به JWT ساخته‌شده در آینده برخورد کرد؛ clock فقط برای expiry entitlement جلو رفت و suite کامل مرتبط موفق بازاجرا شد. suite کامل384 قبلی مربوط به تغییر دعوت است و این نوبت تکرار نشد. دیتابیس‌های fixture صفر باقی ماند و cluster موقت متوقف/حذف شد؛ git diff --check موفق. فرانت/داده یا تنظیمات مقصد، commit/push/deploy/production تغییر نکردند. باقی برای محصول: انتشار نسخه API در تسک جدا و تغییر fetch فرانت از billing.current.features به organization.features طبق قرارداد؛ backlog مجوز انتشار نیست.

## بازبررسی مدل آزمایش صدا — 2026-10-13

روی dev با Git پاک، کد/DI و فقط فیلدهای غیرمحرمانه provider/model/voice از config بررسی شدند: shared AI.Provider=Liara، Liara.TtsModel=google/gemini-3.8-flash-tts و voice=Aoede؛ Development و local روی این فیلدها override ندارند، فایلProduction/local آن در این checkout موجود نیست و override فرایند بررسی‌شده برای provider/model وجود ندارد. NetArz.TtsModel=tts-1-hd و default کلاس LiaraOptions=openai/tts-1-hd برای سازگاری قبلی باقی‌اند، ولی وجود default به معنی مصرف آن در provider منتخب نیست.

POST candidate audio-check در کد جاری فقط TranscribeAsync/STT است و TTS تولید نمی‌کند؛ خوشامد/معرفی/سؤال با ITextToSpeechService منتخب و cache hash متن/model/voice/speed هستند. در این بک‌اند endpoint مستقل صدای نمونه اسپیکر پیش از شروع وجود ندارد. فایل/کش/منبع دکمه تست اسپیکر Angular و provider/config مؤثر سرور مقصد در دسترس این بررسی نیستند؛ استفاده یا عدم استفاده واقعی آن‌ها از OpenAI با کد محلی تأیید نمی‌شود. ادامه تشخیص با مسیر درخواست Network تست اسپیکر، بدون query/token/لینک دعوت حساس، و نسخه/config غیرمحرمانه مقصد است؛ درخواست صرف بررسی، مجوز تغییر policy یا مدل نیست.

Build تازه صفر warning/error؛78/78 تست مستقل provider/selection/audio/Gemini با FFmpeg واقعی محلی و HTTP مصنوعی موفق، صفر شکست/Skip؛ tests/HireYar.Tests/TestResults/audio-test-provider-recheck.trx. تماس واقعی AI/SMTP/SMS یا دیتابیس/PostgreSQL تازه و بررسی فرانت/سرور انجام نشد. runtime/config/schema، کاربران، push/deploy/production تغییر نکردند؛ صرفاً نتیجه در مستندات ثبت شد. نتایج آزمون قبلی مرحله صوت در بخش‌های تاریخی نیز دلیل تأیید مقصد نیستند.

## API نمونه ثابت اسپیکر — 2026-10-13: Verified محلی

طبق درخواست صریح کاربر، GET /api/candidate/interviews/{interviewId}/speaker-test/audio با scheme دعوت/limiter موجود، فقطCreated پیش ازstart، متن ثابت دقیق «سلام وقت بخیر»، MP3/Range/no-store و رسانه audio/mpeg در Swagger اضافه شد. ITextToSpeechService منتخب و cache خصوصی سازمانی/text/model/voice/speed موجود؛ config فعلیLiara/Gemini/Aoede، بدون hardcode provider یا فایل صوتی ثابت مستقل. start/advance/answer/STT/LLM/رزرو و تسویه مصاحبه تغییر نمی‌کنند؛ پس ازgeneration دسترسی/Created دوباره بررسی و failure503 typed است. کد/دعوت نامعتبر یا باطل401، منقضی/تمام‌شده/جایگزین‌شده410، پس ازشروع409؛ هیچ auth grace تازه ندارد. مهاجرت/schema/config تازه لازم نیست؛ INTERVIEW_AUDIO_CHECK_API قرارداد و پرامپت Blob/پخش فرانت، GEMINI_TTS و ADR-047 به‌روزند. یادداشت پیشین «endpoint اسپیکر نداریم» تاریخی و با این درخواست supersede شده است.

toolrestore/restore و build صفر warning/error؛ suite نهایی59/59 موفق، صفر شکست/Skip، PostgreSQL18 اختصاصی loopback55495 و FFmpeg واقعی محلی، adapter Liara با handler/credential/PCM مصنوعی؛ نتیجه tests/HireYar.Tests/TestResults/speaker-test-final.trx. دو تست جدید SpeakerTestAudioTests شامل exact input، model/voice/PCM payload، MP3، retry پس از503، cache/model metadata و reuse همان سازمان/جداسازی سازمان، حفظCreated/نبودanswer/billing، ردJWTپنل/anonymous/sessionدیگر/start/revoke و race ابطال حینTTS، summary/mediaType Swagger را بررسی کردند. نسخه اولیه درج تست در فایل با line-ending متفاوت بی‌اثر بود؛ نتیجه57 قبلی فقطregression بود و تأیید API محسوب نشد؛ تست‌ها در فایل مستقل افزوده و واقعاً اجرا شدند. یک assertion410 برای revoked به401 قرارداد موجود اصلاح و هر59تست نهایی بازاجرا شدند. صدای/کیفیت provider واقعی یا سایت مقصد باPCMمصنوعی تأیید نشده است.

دیتابیس‌های fixture صفر باقی ماندند و cluster موقت متوقف/حذف شد؛ git diff --check موفق. تغییرات مستندی موجود بازبررسی صدا حفظ شدند؛ runtime/config/DB مقصد، فرانت Angular، تماس واقعیAI/SMTP/SMS، commit/push/deploy/production تغییر نکردند. باقی برای محصول: اتصال دکمه تست اسپیکر به GET احرازشده/Blob/پخش صریح کاربر طبق قرارداد و انتشار مستقل نسخه API. backlog مجوز انتشار نیست.

## نام و نام خانوادگی در دعوت عضو — 2026-10-14: Verified محلی

طبق درخواست صریح کاربر، mobile/firstName/lastName در POST /api/team/invitations اجباری شدند؛ هرنامmax100 و غیرwhitespace، نام کامل Trim‌شده با یک فاصلهmax200. firstName/lastName در دعوت ذخیره و POST201/GET فهرست برگردانده می‌شوند. فقط هنگام پذیرشOTP، FullName عضو جدید یا reactivated از آن‌ها تنظیم می‌شود؛ pending پروفایل موجود را عوض نمی‌کند، ویرایش شخصی بعدی محفوظ. nullable در DB فقط برای سازگاری دعوت قبلی است؛ legacy موجود نام قبلی را نگه می‌دارد و legacy حساب جدید همان«کاربر». نقش/سازمان/OTP/مجوزهای محدود Recruiter/trial بدون تغییر؛ TEAM_MOBILE_API و ADR-048 مرجع‌اند و پرامپت فرانت سه‌فیلدی به‌روز است.

Migration20261014052638_AddMobileTeamInvitationNames دو ستون nullable و check جفت/طول اضافه می‌کند؛ backfill حدسی ندارد و Down دارای نام ذخیره‌شده را رد می‌کند. SQL delta idempotent آماده: artifacts/team-invitation-names-migrations.sql، از migration20261013092724؛ فایلSQL جداگانه اجرا نشد، Up/legacyقبل‌ازUp/پذیرش نامnull و محافظDown در fixture واقعی PostgreSQL بررسی شدند. قبل از نسخهAPI، migration در مقصد لازم است؛ درخواست قدیمی فقطmobile پس از این نسخه400 خواهدبود و باید فرانت هماهنگ شود.

toolrestore/restore و build نهایی صفرwarning/error؛42/42 تست مرتبط MobileTeamTests/AuthTests/MobileOtpTests/PanelCleanupTests موفق، صفر شکست/Skip، PostgreSQL18 اختصاصی loopback55496 و provider مصنوعی. سه تست جدید نام‌ها/فهرست/me/members، حفظ نام تاپذیرش و اعمالreinvite، null/ناقص/whitespace/101/مجموع201 رد ومرز200 پذیرفته، requiredهایSwagger، migrationlegacy و عدم حذف داده را بررسی کردند؛ tests/HireYar.Tests/TestResults/team-invitation-names-verified.trx. compileابتدایی سازنده تست با متغیر نامناسب اصلاح شد؛ یک assertion برابری کامل DTO به دقت timestamp PostgreSQL برخورد کرد و به فیلدهای مرتبط دعوت/نام محدود و suite بازاجرا شد. EF has-pending-model-changes بدون اختلاف و git diff --check موفق. suite کامل قبلی تکرار نشد؛ همین42مرتبط تازه موفق‌اند.

دیتابیس‌های fixture صفر باقی ماندند و cluster موقت متوقف/پاک شد. Git ازdev پاک آغاز شد؛ تغییر فرانت، schemaحساب، داده/تنظیمات واقعی، ارسالSMS/ایمیل خارجی، commit/push/deploy/production انجام نشد. باقی: اعمال migration و انتشارنسخهAPI در تسک جدا و اتصال نام/نام‌خانوادگی فرم دعوت طبق قرارداد؛ backlog مجوز اجرای انتشار نیست.

## پنهان‌سازی موقت معیارها و وزن مهارت‌ها — 2026-10-16
با درخواست کاربر، policy و وزن مهارت‌ها و فرمول/آستانه‌ها از خروجی عمومی گزارش و snapshotهای تصمیم انسانی حذف شدند؛ چاپ نیز این اطلاعات را نمایش نمی‌دهد. PUT report/policy پس از کنترل دسترسی گزارش 404 می‌دهد. داده ذخیره‌شده و محاسبات داخلی محفوظ‌اند؛ migration لازم نیست. این تنظیمات توسط AI تولید نمی‌شوند و فراخوانی AI مستقلی نداشتند؛ ارزیابی پاسخ‌ها تغییر نکرد. Angular در مخزن جداست و باید کارت «معیارها و وزن مهارت‌ها» را حذف کند و نبود policy را بپذیرد. انتشار انجام نشده است.

## حذف موقت تناسب با نیازهای موقعیت — 2026-10-16
طبق درخواست کاربر، بخش positionFit از فهرست بخش‌ها و چاپ حذف شد؛ positionFit و fit داخل snapshot تاریخچه در خروجی عمومی HTTP ارسال نمی‌شوند. جمع‌بندی دیگر وعده نمایش جداگانه تناسب را نمی‌دهد. محاسبات و داده‌های داخلی برای بازگرداندن آینده محفوظ‌اند؛ AI درخواست جداگانه‌ای برای این بخش ندارد. Angular جدا باید کارت/جدول مهارت‌های این بخش، لینک ناوبری، چاپ و وابستگی به positionFit را حذف کند؛ امتیاز کل و پروفایل تخصصی مستقل حفظ شوند. migration و انتشار ندارد.


## مانع تأییدشده E2E — 2026-10-17
[P1] هماهنگ‌کردن guard و نمایش گزارش Angular با حذف عمدی policy/positionFit/section positionFit؛ API واقعی پنج ارزیابی موفق دارد ولی پنل آن را نامعتبر می‌داند. اصلاح و انتشار فرانت در این آزمون انجام نشد. پس از اصلاح، بازاجرای مسیر مرورگر با سؤال ثابت و اعتبار سازمان آزمایشی لازم است. [شواهد و بازتولید](../../candidate-e2e/REPORT.md).

تغییر 2026-10-17: API عمومی /api/pricing/ai-display-rate از بازار USDTTMN والکس با cache5ثانیه، no-store و503 هنگام نبود نرخ معتبر اضافه شد؛ حسابداری/پرداخت ثابت. قرارداد و پرامپت Angular: AI_TOMAN_DISPLAY_API.md. فرانت جدا و تماس واقعی فعلاً به علت DNS تأیید نشده است.
اصلاح واحد تبدیل AI — 2026-10-17: خروجی ai-display-rate اکنون currency=IRR و rialPerUsdt است؛ lastPrice بازار USDTTMN والکس در۱۰ ضرب می‌شود. cache و refresh پنج‌ثانیه‌ای محفوظ؛ تبدیل اختیاری ریال به تومان فقط در فرانت با تقسیم بر۱۰. مبالغ قبلی پرداخت IRT و حسابداری USD تغییر نمی‌کنند. قرارداد AI_TOMAN_DISPLAY_API.md به‌روز شد.

تکمیل تبدیل خروجی مالی — 2026-10-17: طبق تأیید صریح کاربر «معادل ریالی با نرخ روز تغییر کند»، APIهای pricing/billing/admin-pricing اکنون DTO مستقل Rial... و خروجی IRR دارند. USD×نرخ والکس و IRT×۱۰ در مرز HTTP، شامل کیف پول/مصرف/تراکنش/تاریخچه/فاکتور/پکیج/اشتراک/صندلی/پرداخت؛ metadata نرخ یکسان در پاسخ، no-store، preflight نرخ قبل mutation. دیتابیس/ledgerUSD و snapshot/درگاهIRT محفوظ و مبلغ نقدی ثابت است؛ حساب ریالی ثابت خواسته نشده است. درخواست‌های نوشتن ادمین همچنان واحد صریح منبع USD/IRT دارند؛ فرانت باید پاسخ جدید را مصرف کند و تبدیل دلار انجام ندهد. API پاسخ breaking است و انتشار هماهنگ فرانت لازم است. قرارداد/پرامپت جاری: AI_RIAL_DISPLAY_API.md؛ سند قبلی به آن ارجاع می‌دهد. Angular/انتشار/دسترسی واقعی والکس انجام نشده‌اند.

اعتبارسنجی نهایی تبدیل ریالی: build همه پروژه‌ها با صفر هشدار/خطا موفق؛ در اجرای PostgreSQL واقعی۷۱سناریو پاس و یک انتظار قدیمی واحد در تست migration اصلاح شد؛ بازاجرای۲۱سناریو شامل همه۲۰تست نرخ/خروجی/API جدید و همان migration با۲۱Pass/۰Fail/۰Skip موفق شد. مجموع۷۲سناریوی یکتای مرتبط تأیید شد. شواهد: artifacts/rial-billing-tests.log، artifacts/rial-billing-final.log و TestResults/rial-billing-final.trx در پروژه تست. تغییر نرخ650005→700000، پاسخIRR کیف پول/ادمین/پرداخت، ثباتledgerUSD و snapshotIRT، عدم mutation هنگام503 و cash-read بدون والکس در PostgreSQL بررسی شد. تست‌ها نرخ مصنوعی دارند؛ اتصال واقعی والکس/فرانت/انتشار تأیید نشده‌اند.

اعتبارسنجی نهایی2026-10-17: build همه پروژه‌ها با صفر خطا/هشدار موفق؛ اجرای گسترده۸۸تست،۸۵Pass و۳انتظار قدیمی تست داشت که اصلاح شدند. بازاجرای۲۳تست PostgreSQL شامل هر۳مورد، رزرو/تسویه/انقضا/لغو/rollback ارسال مجدد، privacy کارجو و audit دلاری با۲۳Pass/۰Fail/۰Skip موفق شد. پس از تفکیک ادمین ثابت از کاتالوگ پویا،۱۸تست خروجی/نرخ با۱۸Pass موفق شد. اسکریپت idempotent تبدیل موجودی قدیمی نیز با نرخ صریح روی PostgreSQL اجرا و دوباره اجرا شد؛۱Pass، بدون تبدیل دوباره و با حفظledger وauditUSD. مدل EF بدون pending changes است و git diff --check موفق است. شواهد: rial-wallet-verified.trx، rial-wallet-output-final.trx و rial-cutover-script.trx در tests/HireYar.Tests/TestResults؛ لاگ‌های artifacts/rial-wallet-*.log. کل suite خارج این محدوده اجرا نشده است. اسکریپت قابل بازبینی: artifacts/rial-ai-wallet-migration.sql. سرویس PostgreSQL آزمایشی پس از تست خاموش شد؛ مقصد/فرانت/انتشار تغییر نکرده‌اند.
