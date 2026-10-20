# مدیریت مشتریان سوپرادمین — قرارداد API

2026-10-20 / ADR-077. مبنا فایل superadmin_user_manage (2).html ارائه‌شده است؛ اعداد/تاریخ‌ها/اسکریپت‌های نمایشی داده نمونه‌اند. کاربر صریحاً دکمه «مشتری جدید» را حذف کرد: API ساخت دستی حساب یا لید جدید اضافه نشده است. Angular در ریپوی جداست؛ این قرارداد بک‌اند چهار تب مدیریت، فهرست و خروجی را فراهم می‌کند.

## هویت و فهرست

تمام مسیرهای `/api/admin/customers` فقط Bearer مستقل SystemAdmin فعلی را می‌پذیرند؛ Owner/Recruiter/Sales/anonymous پاسخ401 می‌گیرند. no-store و limiter موجود tickets (۶۰درخواست/IP/دقیقه) برقرار است. شناسه/سازمان/نقش مشتری از رکورد سرور خوانده می‌شوند و از body قابل تغییر نیستند. مدیریت هویت سوپرادمین از این فرم ممنوع است؛ CanEditIdentity=false و403/privileged_account_protected. مدیریت سوپرادمین‌های موجود مسیر مستقل قبلی را دارد.

| روش و مسیر | ورودی / خروجی |
|---|---|
| GET /api/admin/customers | AdminCustomerQuery → `{rows:Page<AdminCustomerDto>,stats:AdminCustomerStats}` |
| GET /api/admin/customers/{id} | AdminCustomerDetail شامل customer، subscription، subscriptionVersion، canManageSubscription، canEditIdentity |
| GET /api/admin/customers/export | همان فیلترها، فایل واقعی XLSX بدون pagination، حداکثر۱۰۰۰۰ردیف؛ بیش از آن413/export_too_large |
| PUT /api/admin/customers/{id}/profile | AdminCustomerProfileRequest → `{eventId,replayed}` |
| PUT /api/admin/customers/{id}/seller | AdminCustomerSellerRequest → `{eventId,replayed}` |
| GET /api/admin/customers/{id}/history | page/pageSize → Page<AdminCustomerHistoryDto> |
| POST /api/admin/customers/{id}/interview-credit | هدیه تعدادی مستقل و بدون سفارش/پورسانت |
| POST /api/admin/customers/{id}/subscription/set | تنظیم دستی بازه و مدت اشتراک، بدون فاکتور یا پورسانت |
| POST /api/admin/customers/{id}/subscription/days | افزودن/کاستن روز، بدون فاکتور یا پورسانت |
| POST /api/admin/customers/{id}/subscription/stop | توقف دسترسی فعلی و بازه‌های ازپیش‌تأییدشده، بدون حذف سوابق |
| GET /api/admin/customers/{id}/orders | فهرست سفارش‌های همان سازمان، Page<SubscriptionOrderDto> |
| POST /api/admin/customers/{id}/orders | CreateSubscriptionOrderRequest موجود → سفارش Pending برای همان مشتری |

فهرست، هر Owner سازمان را یک مشتری و هر SalesLead هنوز ثبت‌نام‌نشده را یک لید نشان می‌دهد. اعضای Recruiter ردیف مشتری مستقل نیستند؛ عضو همان سازمان‌اند. id در kind=User، شناسه Owner و در kind=Lead، شناسه لید است. پس از ثبت‌نام OTP، ردیف لید با ردیف Owner جایگزین می‌شود؛ leadId سابق در پاسخ مالک محفوظ است و دو ردیف تکراری نداریم. اطلاعات ثبت‌نام‌شده از Users/Organizations واقعی و اطلاعات لید از contactهای ثبت‌شده می‌آیند. هیچ داده ساختگی یا حساب بدون OTP ساخته نمی‌شود.

Query: page پیش‌فرض۱، pageSize۲۰ و۱تا۱۰۰؛ search حداکثر۲۵۶ بر نام/سازمان/موبایل/ایمیل، case-insensitive و تبدیل ارقام فارسی/عربی به ASCII. agentId اختیاری، hasSeller=true/false اختیاری، status یکی از Won/Registered/Waiting/Expired/Stopped/Expiring. ترتیب CreatedAtUtc نزولی سپس Id. آمار بر همه نتایج جستجو و فیلتر فروشنده **پیش از فیلتر status و صفحه‌بندی** است تا تب‌ها تعداد درست داشته باشند؛ rows.total تعداد پس از تمام فیلترهاست.

stats: total، won (دارای اشتراک پولی تأییدشده)، registered (Owner بدون خرید پولی اشتراک)، waiting (لید بدون ثبت‌نام)، expired، withoutSeller، expiringSoon (فعال با پایان کل پوشش حداکثر۷روز آینده). این شمارنده‌ها همپوشانی دارند؛ جمع آن‌ها total نیست.

AdminCustomerDto: id/kind/organizationId/leadId، fullName/organizationName/mobile/email/isActive، createdAtUtc/registeredAtUtc، registrationStatus، agentId/agentName، subscriptionStatus، subscriptionStartedAtUtc/subscriptionExpiresAtUtc/remainingDays، remainingInterviews/availableInterviews/reservedInterviews/consumedInterviews، latestBundleName و profileVersion. profileVersion یک مقدار opaque است و فقط برای ویرایش برگردانده شود.

registrationStatus مستقل: Waiting، Registered، Won. خرید موفق با انقضا یا توقف سابقه‌اش را از دست نمی‌دهد. subscriptionStatus مستقل: None/Trial/Active/Scheduled/Expired/Stopped. خرید زودهنگام در زمان trial، registrationStatus=Won دارد ولی تا پایان هدیه، subscriptionStatus=Trial است؛ پوشش خرید آینده در تاریخ پایان کل منظور می‌شود. هدیه سازمان‌های قدیمی هم منبع معتبر دسترسی است. اعتبار موجود، مجموع سهمیه سازمان است؛ latestBundleName صرفاً عنوان آخرین بسته خریداری‌شده است و به معنی «این تعداد باقی‌مانده از همین بسته» نیست. اعتبار هدیه و بسته‌ها تفکیک حساب جعلی ندارند و مصاحبه رزروشده با مصرف قطعی متفاوت است.

## ویرایش اطلاعات و فروشنده

Profile body: `{requestId,expectedVersion,fullName,organizationName,mobile,email,reason}`. expectedVersion از customer.profileVersion؛ UUID غیرخالی و دلیل۱تا۱۰۰۰کاراکتر اجباری. fullName حداکثر۲۰۰ و غیرخالی، mobile فقط09+۹رقم ASCII، email nullable/max256/ایمیل معتبر، organizationName برای مشتری ثبت‌نام‌شده اجباری/max200؛ برای لید اختیاری است. نام کامل منبع موجود سیستم است و firstName/lastName مستقل تاریخی ذخیره نشده‌اند. NUL و متن نامعتبر400 می‌گیرند. موبایل/ایمیل متعلق به حساب دیگر یا موبایل لید دیگر409، دعوت تیم باز با همان موبایل409/team_invitation_pending؛ تغییر جزئی انجام نمی‌شود.

تغییر موبایل یا ایمیل مشتری، security stamp و refreshهای قبلی را باطل می‌کند؛ کاربر با OTP شماره جاری دوباره وارد شود. تغییر ایمیل، EmailConfirmed را false می‌کند. این API رمز/نقش/سازمان متعلق به حساب/فعال‌بودن یا امتیاز سوپرادمین را تغییر نمی‌دهد. نام سازمان و نام مشتری همان سازمان با هم تراکنشی ثبت می‌شوند. داده‌های لید ثبت‌نام‌نشده هویت تأییدشده نیستند؛ ویرایش آن حساب نمی‌سازد. شماره اصلی لید متصل به سازمان، مدرک انتساب اولیه است و با تغییر شماره ورود مشتری بازنویسی نمی‌شود؛ انتساب آینده همچنان از organizationId ثابت پیروی می‌کند.

Seller body: `{requestId,expectedVersion,agentId,reason}`؛ agentId=null یعنی بدون فروشنده. مقصد غیرnull باید فعال باشد. فهرست گزینه‌ها از GET /api/admin/sales/agents. انتساب قبلی و جدید در تاریخچه ثبت می‌شود؛ پورسانت‌های قبلی، فروشنده دریافت‌کننده و payoutهایشان تغییر نمی‌کنند. خرید/تمدید تأییدشده بعدی به فروشنده جدید تعلق می‌گیرد و در حالت بدون فروشنده پورسانت تازه ساخته نمی‌شود. برای Owner بدون لید، انتخاب فروشنده، انتساب را به شماره تأییدشده و سازمان واقعی می‌سازد؛ پاداش تاریخی ساخته نمی‌شود. حذف فروشنده از لید، لید را حذف نمی‌کند و ثبت‌نام OTP بعدی همچنان به همان سازمان/لید متصل می‌شود.

همه mutationها requestId دارند. retry همان عملیات/بدنه با همان شناسه همان eventId و replayed=true می‌دهد؛ reuse برای عملیات یا بدنه دیگر409/idempotency_conflict. retry را با expectedVersion اولیه همان بدنه بفرستید. اگر درخواست تازه است و profileVersion عوض شده409/customer_changed؛ فرم را refetch کنید و تغییر قدیمی را کورکورانه تکرار نکنید.

## اشتراک دستی و تمدید پولی

Set body: `{requestId,expectedVersion,startedAtUtc,months,daysDelta,reason}`؛ expectedVersion از detail.subscriptionVersion. months فقط۱/۳/۶/۱۲؛ end=شروع+ماه تقویمی UTC+daysDelta، delta در-۳۶۵۰تا۳۶۵۰؛ شروع UTC با Z، حداکثر یک سال آینده و ده سال گذشته، end>start و حداکثر۱۱سال آینده. این تغییر **دسترسی دستی** است و فاکتور/درآمد/پورسانت ایجاد نمی‌کند. برای انتخاب «بدون اشتراک» از stop استفاده شود.

Days body: `{requestId,expectedVersion,days,reason}`؛ days غیرصفر، -۳۶۵۰تا۳۶۵۰؛ +۷/+۳۰ همان endpoint هستند. زمان به پایان کل پوشش موجود اضافه می‌شود؛ در نبود پوشش مثبت از اکنون شروع می‌شود. مقدار منفی بدون اشتراک فعال400/no_active_subscription و کاهش نامعتبر400/invalid_subscription_end. زمان شروع برنامه آینده با افزودن روز جلو کشیده نمی‌شود.

Stop body: `{requestId,expectedVersion,reason}`. توقف، دسترسی ناشی از trial/هدیه/اشتراک‌های تا آن زمان تأییدشده را متوقف می‌کند؛ از فعال‌شدن خودکار یک دوره قبلاً خریداری‌شده در آینده نیز جلوگیری می‌کند. موجودی مصاحبه، مصرف قبلی، دعوت‌ها، مبلغ سفارش و پورسانت قبلی حذف یا بازپرداخت نمی‌شوند. شروع دعوت جدید/مصرف نیازمند اشتراک فعال است. جایگزین دستی شامل snapshot ظرفیت تیم و retention است و ظرفیت فعلی را خودکار کم نمی‌کند. این‌ها روی همان منبع عملیاتی مصاحبه/تیم/داشبورد اثر دارند، نه صرفاً روی نمای سوپرادمین.

تمدید **پولی** با دکمه جدا انجام شود: quote از POST /api/subscriptions/catalog/quote با PurchaseSelection موجود، POST customers/{id}/orders با requestId/selection/نسخه کاتالوگ، نمایش مبلغ واقعی ریالی و سپس تأیید همان سفارش با POST /api/admin/subscriptions/orders/{orderId}/confirm و paymentReference واقعی. سفارش Pending بدون تأیید، دسترسی/پورسانت نمی‌سازد. همان reference در retry استفاده شود. API تعدادماه/مبلغ/درصد را از مرورگر قبول نمی‌کند؛ نرخ تمدید تنظیم هرنیرو است و لزوماً۵٪ ثابت نیست. خرید اول/تمدید فقط طبق قرارداد فروش و snapshot تأییدشده محاسبه می‌شود. سفارش Pending احتمالی در شکست تأیید از فهرست orders قابل ادامه است و retry همان requestId سفارش را دوباره نمی‌سازد.

اصلاحات دستی، snapshot سفارش و membershipهای خرید قبلی را ویرایش نمی‌کنند؛ یک جایگزین اداری مستقل اعمال می‌شود. شناسه سفارش‌های موجود هنگام اصلاح در excludedOrderIds ثبت می‌شود تا قاعده به clock وابسته نباشد؛ خریدهای پولی که بعد از اصلاح تأیید می‌شوند همچنان منبع معتبرند و خرید بعد از توقف می‌تواند از اکنون دسترسی بدهد. بدون override، قواعد قبلی بدون تغییر اجرا می‌شوند.

## اعتبار مصاحبه، تاریخچه و اکسل

Credit body: `{requestId,quantity,bundleCode,reason}`؛ quantity۱تا۱۰۰۰۰۰ و bundleCode بسته فعال کاتالوگ (dana/bina/zharfa/custom) به‌عنوان برچسب مبنای هدیه. تعداد هدیه لازم نیست برابر تعداد بسته خریدنی باشد؛ مثلاً هدیه۵مصاحبه با برچسب bina مجاز است. این تغییر رایگان/دستی و ledger مستقل audit است؛ فاکتور بسته، خرید جدید، پورسانت، پول یا قابلیت برداشت ایجاد نمی‌کند. سهمیه به موجودی سازمان اضافه، در مسیر رزرو/شروع واقعی مصرف و هنگام انقضای اشتراک حفظ می‌شود؛ برای استفاده اشتراک فعال لازم است. کم‌کردن دستی تعداد در این درخواست نیست. قفل سازمان و unique request مانع افزایش دوباره در رقابت/دوبارکلیک‌اند.

History، Page<AdminCustomerHistoryDto>: id/source/action/occurredAtUtc/actorId/description/amountRial/interviewQuantity/detailsJson. منبع Administration اطلاعات whitelisted قبل/بعد و دلیل دارد و append-only است. منبع Sales رخدادهای همان لید؛ Order وضعیت/مبلغ/عنوان سفارش، Registration ایجاد واقعی حساب، SignupTrial و ExistingOrganizationGrant هدیه‌ها هستند. source+id کلید ردیف است؛ id می‌تواند میان منابع متفاوت تکرار شود. سفارش لغوشده timestamp ایجاد دارد چون زمان لغو در مدل قدیمی ذخیره نشده؛ آن را زمان قطعی لغو معرفی نکنید. تاریخچه لید پس از ثبت‌نام در مشتری مرتبط نیز خواندنی است. GET بدون write/AI است.

XLSX خروجی واقعی با UTF-8/XML استاندارد، RTL، header ثابت و autofilter است؛ همه سلول‌ها inline string هستند تا شماره با صفر اولیه محفوظ و نام/ایمیلِ شروع‌شونده با= به فرمول تبدیل نشود. فایل تمام نتایج فیلتر را بدون توجه به page/pageSize می‌دهد، نه همه داده بدون فیلتر. دانلود با Bearer و Blob؛ لینک عمومی یا فایل PII ذخیره‌شده روی سرور ساخته نمی‌شود.

## خطاها و تحویل

| HTTP/code | رفتار فرانت |
|---|---|
|400/validation_failed، invalid_sales_text، account_rejected | اصلاح فیلدها |
|400/invalid_bundle، sales_agent_unavailable | تازه‌کردن گزینه‌های فعال |
|400/invalid_subscription_period، invalid_subscription_start، invalid_subscription_end، invalid_day_adjustment، no_active_subscription | اصلاح بازه/روز |
|401/unauthorized | ورود مجدد سوپرادمین |
|403/privileged_account_protected | فرم هویت این ردیف غیرفعال |
|404/not_found | مشتری/لید ناموجود یا ردیفی که پس از ثبت‌نام شناسه Owner گرفته؛ فهرست تازه شود |
|409/customer_not_registered | تب مالی برای لید بدون حساب غیرفعال |
|409/customer_changed، customer_subscription_changed | refetch و بازبینی فرم پیش از درخواست جدید |
|409/idempotency_conflict | requestId برای بدنه تازه مجدداً استفاده نشود |
|409/mobile_unavailable، email_unavailable، team_invitation_pending | نمایش تداخل بدون ذخیره جزئی |
|413/export_too_large | محدودترکردن فیلتر صادرات |
|429/rate_limited | صبر و retry محدود |

Migration `20261020133348_AddCustomerAdministration` دو جدول اداری و nullable فروشنده/اطلاعات تماس لید را افزایشی اضافه می‌کند. هیچ حساب/اشتراک/پورسانت قبلی backfill حدسی نمی‌شود. history و source/cross-org در PostgreSQL محافظت و rollback دارای سابقه/فروشنده حذف‌شده/اطلاعات تماس تازه رد می‌شود. پیش از API باید migration اجرا شود؛ مقصد/Angular/push/deploy در این تسک تغییر نکرده‌اند. ورود مستقل فروش اکنون [مسیر خصوصی salesadmin با SalesAdmin در appsettings](SALES_PANEL_API.md) است و کپچا ندارد. [پرامپت فرانت مدیریت مشتریان](ADMIN_CUSTOMERS_FRONTEND_PROMPT.md) مرجع اتصال رابط است.
