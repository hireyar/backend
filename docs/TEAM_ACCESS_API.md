# دعوت اعضا و سطوح دسترسی سازمان

تاریخ: 2026-10-22. این قرارداد بک‌اند است؛ پنل Angular در این مخزن نیست.
مرجع طراحی `invite_admin.html` است. **موبایل اجباری باقی است**؛ ایمیل نیز در دعوت تکی و هر ردیف گروهی اجباری و کانال ارسال دعوت‌نامه است. ورود و پذیرش همیشه OTP همان موبایل دعوت‌شده را می‌خواهد.

## مدل و سازگاری

- هویت `role: Owner | Recruiter` برای سازگاری و تشخیص مالک حفظ شده؛ مجوز UI را از آن حدس نزنید.
- `GET /api/auth/me` علاوه بر مشخصات قبلی `accessRole` و `permissions: [{code,access}]` می‌دهد. permissions مجوز مؤثر و مرجع نهایی UI است؛ فقط `Read` یا `Write` در آن وجود دارد، غیبت یعنی عدم دسترسی. اطلاعات JWT/localStorage مبنای مجوز نیست.
- کاتالوگ اولیه: `admin`، `interview_manager` و `viewer`. اعضای Recruiter قدیمی و دعوت‌های قبلی به interview_manager نگاشت می‌شوند؛ مالک همچنان مالک یکتای سازمان است. مدیر مصاحبه اکنون مجوز نوشتن گزارش و ارزیابی هم دارد.
- بخش‌ها: dashboard، jobs، templates، questions، interviews، reports، settings، subscriptions، tickets و team. Read فقط GET/HEAD؛ Write شامل خواندن و ایجاد/ویرایش/حذف است. استثنا: GET /api/interviews/{id}/access هم Write می‌خواهد چون کد ورود کارجو را برمی‌گرداند؛ کد و لینک کارجو و canResend/canCancel در فهرست مصاحبه برای Read حذف/false هستند. درخواست POST پیشنهاد سؤال حتی اگر رکورد نهایی نسازد، Write می‌خواهد.
- این‌ها فقط پیش‌فرض قابل تغییرند و نام نقش هیچ مجوز ثابتی تحمیل نمی‌کند. مدیر مصاحبه: dashboard خواندن؛ jobs/templates/questions/interviews/reports/tickets نوشتن؛ settings/subscriptions/team بدون دسترسی. ناظر: dashboard/jobs/templates/questions/interviews/reports/tickets فقط خواندن؛ سه بخش حساس بدون دسترسی. ناظر فقط تیکت‌های ایجادشده توسط خودش را می‌خواند.
- مالک برای جلوگیری از قفل‌شدن سازمان دسترسی کامل دارد؛ تغییر کاتالوگ او را قفل نمی‌کند. نقش admin برای اعضای دعوت‌شده مطابق کاتالوگ قابل تنظیم است.
- `isAdministrator` برچسب اختیاری حفاظت تخصیص است و سوپرادمین می‌تواند آن را ایجاد یا ویرایش کند؛ با دسترسی سراسری SystemAdmin متفاوت است. `requiresOwnerAssignment` در خروجی محاسبه می‌شود: برچسب=true **یا هر سطح غیرNone در team/settings/subscriptions**. مقدار مشتق‌شده فقط خواندنی است و در بدنه PUT ارسال نشود. نقش حساس فقط توسط مالک دعوت/تخصیص/تغییر/غیرفعال و دعوتش لغو/بازارسال می‌شود؛ پاک‌کردن برچسب، حفاظت ناشی از مجوز واقعی را برنمی‌دارد. مالک تغییر نقش یا غیرفعال نمی‌شود.
- سوپرادمین می‌تواند **برای تمام نقش‌های سازمانی، از جمله admin/viewer/interview_manager، تمام بخش‌ها را آزادانه None/Read/Write کند**؛ هیچ وابستگی اجباری به isAdministrator یا نام نقش وجود ندارد و ماتریس نباید به این دلیل disabled شود. حساس‌بودن نقش، محدودیت واگذاری توسط اعضای سازمان است، نه محدودیت تعریف نقش توسط سوپرادمین.
- دارنده team Write که مالک نیست فقط نقش غیرحساس با مجوزهای حداکثر برابر مجوز مؤثر خودش را دعوت/واگذار/مدیریت می‌کند؛ Read نمی‌تواند Write یا بخش فاقد مجوز بدهد. همین کنترل برای نقش فعلی هدف هنگام تغییر/غیرفعال/دعوت مجدد و لغو/بازارسال دعوت برقرار است. مجوزهای خام نقش (حتی بخش فعلاًغیرفعال) در این کنترل محافظه‌کارانه محاسبه می‌شوند.
- نقش/بخش غیرفعال بلافاصله در درخواست بعدی از مجوز مؤثر عضو حذف می‌شود؛ مالک مستثناست. تغییر نقش نیز با همان access token فعلی اعمال می‌شود. حساب/خروج/خواندن اعلان شخصی مستقل‌اند.

## سوپرادمین

همه با نشست مستقل SystemAdmin موجود؛ توکن HR حتی برای مالک کافی نیست.

- `GET /api/admin/access/roles`: تمام نقش‌ها و ماتریس بخش‌هایشان.
- `PUT /api/admin/access/roles`: ایجاد یا ویرایش کامل یک نقش. `{code,name,description,isActive,isAdministrator,revision,sections:[{code,access}]}`. code انگلیسی `[a-z][a-z0-9_]{1,63}`، نام حداکثر۱۰۰، شرح۱۰۰۰، access یکی از None/Read/Write. ایجاد revision=null و ویرایش revision آخرین GET. isAdministrator نیز در ویرایش آزاد است؛ هیچ بخش یا سطحی به آن قفل نمی‌شود. حذف یک بخش از لیست ارسالی معادل None است؛ کد ناشناس یا تکراری خطا دارد. code بعد ایجاد تغییر نمی‌کند. غیرفعال‌سازی جای حذف تاریخچه را می‌گیرد.
- `GET /api/admin/access/sections`: نام، شرح، وضعیت و revision بخش‌ها.
- `PUT /api/admin/access/sections/{code}`: `{name,description,isActive,revision}`. نام/شرح و فعال بودن قابل تغییرند؛ کد امنیتی بخش و اتصال آن به endpointها ثابت و در کد هستند. ایجاد «بخش بدون API» مجوز امنیتی واقعی نمی‌سازد.
- تغییرات تعریف نقش/بخش با قفل انحصاری کاتالوگ و عملیات عضویت/پذیرش با قفل اشتراکی تراکنشی هماهنگ‌اند؛ ترتیب کاتالوگ→موبایل→سازمان→عضو رعایت می‌شود. کنترل واگذاری پس از قفل سازمان از مجوز جاری DB خوانده می‌شود تا درخواست منتظر با مجوز قبلی، پس از تنزل نقش اجرا نشود. SMTP بعد commit است.
- هم‌زمانی با revision و پاسخ409؛ پس از409 کاتالوگ دوباره خوانده شود. تغییر نقش/بخش و عملیات عضویت audit غیرقابل ویرایش دارند. تعریف نقش در سطح کل سامانه و توسط سوپرادمین است، نه تعریف مستقل سازمانی.

## تیم و دعوت

endpointهای زیر نیازمند مجوز team مربوط هستند و همگی scoped سازمان جاری‌اند. GETها Read، بقیه Write. فقط مالک عملیات مربوط به نقش دارای requiresOwnerAssignment را انجام می‌دهد؛ سایر مدیران تیم سقف مجوز خود را رعایت می‌کنند.

- `GET /api/team/invitation-capacity`: پاسخ بدون cache با `{hasActiveSubscription,teamLimit,activeMembers,pendingInvitations,occupiedSlots,availableSlots,unlimitedTeam,maxBatchSize}`. activeMembers شامل مالک و اعضای فعال؛ pending فقط دعوت پذیرفته‌نشده/لغونشده/منقضی‌نشده است. occupiedSlots جمع این دو است. availableSlots ظرفیت باقی‌مانده غیرمنفی؛ برای اشتراک نامحدود null و maxBatchSize=50؛ بدون اشتراک فعال availableSlots=0 و maxBatchSize=0. teamLimit برای نامحدود/بدون اشتراک null است. maxBatchSize برابر min(50,availableSlots) و مرجع تعداد ردیف قابل نمایش/افزودن/ارسال فرانت است. نمونه محدود: `{hasActiveSubscription:true,teamLimit:5,activeMembers:1,pendingInvitations:2,occupiedSlots:3,availableSlots:2,unlimitedTeam:false,maxBatchSize:2}`. بعد ایجاد/پذیرش/لغو/انقضا/غیرفعال‌سازی/تغییر اشتراک و پاسخ409 ظرفیت دوباره خوانده شود. در409 داده تایپ‌شده حفظ شود و هیچ زیرمجموعه‌ای خودکار ارسال نشود.
- `GET /api/team/access-roles`: نقش‌های فعال برای selector و کارت توضیح.
- `GET /api/team/access-sections`: برچسب/شرح قابل تنظیم بخش‌ها برای ماتریس.
- `GET /api/team/members` و `/invitations`: pagination قبلی. اعضا accessRoleCode دارند؛ دعوت‌ها accessRoleCode/email/language دارند. مالک با accessRoleCode=admin نمایش داده می‌شود اما ownership مستقل است.
- `POST /api/team/invitations`: `{mobile,firstName,lastName,accessRoleCode,email,language?,message?}`. mobile حتماً `09` و۹رقم ASCII دیگر؛ نام و نام خانوادگی هرکدام الزامی و۱۰۰کاراکتر، مجموع۲۰۰. accessRoleCode نقش فعال و ترجیحاً در UI انتخاب صریح؛ پیش‌فرض سازگاری سرور interview_manager است. email اجباری/معتبر/حداکثر۲۵۶کاراکتر، انتخاب زبان حذف شده؛ language فقط برای سازگاری اختیاری و فقطfa قابل قبول است و نباید در فرم نمایش داده شود، message حداکثر۲۰۰۰.
- موفقیت201؛ پاسخ شامل `emailDelivery: Sent | Failed | NotConfigured | NotRequested`. NotRequested فقط برای رکوردهای قدیمی/پاسخ فهرست است؛ دعوت جدید بدون ایمیل رد می‌شود. SMTP غیرفعال/ناقص NotConfigured. شکست ارسال، دعوت ذخیره‌شده را حذف نمی‌کند. در GETهای بعدی این فیلد وضعیت ارسال تاریخی نیست؛ نتیجه ارسال فقط پاسخ عملیات است.
- `POST /api/team/invitations/batch`: `{invitations:[همان بدنه دعوت]}`،۱تاmaxBatchSize عضو و سقف فنی۵۰. هر ورودی موبایل و ایمیل اجباری خودش را دارد. اعتبارسنجی تمام ورودی‌ها پیش از عملیات است؛ ایمیل نامعتبر هر ردیف400 و بدون ثبت هیچ دعوتی است. تعداد بیشتر از ظرفیت جاری409 با team_capacity_exceeded و بدون ثبت هیچ دعوتی است. بدون اشتراک فعال403 است. ظرفیت و ثبت زیر یک قفل سازمان و تراکنش انجام می‌شوند؛ هر ردیف savepoint دارد تا خطای کسب‌وکاری همان ردیف برگردد و موفق‌ها حفظ شوند. نتیجه هر آیتم `{mobile,invitation,status,errorCode}`؛ SMTP پس از commit اجرا می‌شود. پس از timeout ابتدا لیست را بخوانید و همه موارد موفق را دوباره ارسال نکنید.
- `POST /api/team/invitations/{id}/resend`: ارسال مجدد ایمیل فقط برای دعوت دارای ایمیل و در انتظار/منقضی‌نشده. مهلت اصلی تمدید نمی‌شود؛ لغو/پذیرفته‌شده/منقضی409. نتیجه ارسال در emailDelivery.
- `DELETE /api/team/invitations/{id}`: لغو قبلی، با حفاظت نقش ادمین.
- `PUT /api/team/members/{id}/access`: `{accessRoleCode}`؛ موفقیت204. عضو سازمان دیگر404، مالک400، ارتقای ادمین توسط غیرمالک403. access token موجود نقش جدید را در درخواست بعدی می‌گیرد.
- `POST /api/team/members/{id}/deactivate`: غیرفعال‌سازی قبلی همراه ابطال نشست و audit.
- محدودیت ظرفیت تیم فعلی و قفل‌های mobile→organization→member حفظ شده‌اند؛ ایمیل شماره ورود را تغییر نمی‌دهد و هویت ایمیلِ حساب از روی دعوت تأیید نمی‌شود.

## پذیرش و ایمیل

لینک ایمیل: `{Security:FrontendUrl}/team-invitations/{id}`؛ Angular باید این route را اضافه کند. شناسه دعوت با OTP شماره دعوت‌شده ترکیب می‌شود و به‌تنهایی نشست یا دسترسی ایجاد نمی‌کند.

۱. دریافت شماره موبایل در صفحه پذیرش؛ فرم شماره دعوت‌شده را می‌خواهد.
۲. `POST /api/auth/otp/request` با `{mobile}`.
۳. `POST /api/auth/team-invitations/{id}/accept` با `{mobile,code}` و CSRF/cookie طبق قرارداد auth موجود.
۴. گرفتن access token و `GET /api/auth/me`؛ سپس ورود به مسیر مجاز.

توکن OTP برای همان شماره، یک‌بارمصرف و با محدودیت تلاش موجود است؛ دعوت۷۲ساعت از زمان ایجاد اعتبار دارد، یک‌بار پذیرفته می‌شود و پس از لغو/انقضا قابل پذیرش نیست. نقش هنگام پذیرش باید هنوز فعال باشد. لینک هیچ OTPای در خود ندارد.

SMTP از تنظیمات موجود `InterviewNotifications:Email` استفاده می‌کند؛ سرویس یا رمز جدید لازم نیست. پیکربندی Enabled/Host/Port/EnableSsl/Username/Password/FromAddress/FromName/TimeoutSeconds همان قبلی است. بدون SMTP فعال، دعوت ساخته می‌شود و NotConfigured برمی‌گردد. قالب team مجزا، جدول‌محور، فقط فارسی و راست‌به‌چپ، متن ساده جایگزین، رنگ تیره/فیروزه‌ای و HTML encoding نام/سازمان/نقش/پیام است. ارسال واقعی به کاربران در تست انجام نمی‌شود.

## اتصال فرانت و انتشار

[پرامپت پیاده‌سازی Angular](TEAM_ACCESS_FRONTEND_PROMPT.md). migration `20261022101526_AddOrganizationAccessControl` باید پیش از API جدید روی مقصد اجرا شود؛ روی مقصد در این تسک اجرا نشده. SQL افزایشی idempotent در `artifacts/organization-access-migrations.sql` آماده است. Down در صورت وجود سابقه دسترسی/تخصیص جدید/ایمیل یا پیام دعوت ممنوع و نیازمند backup است.
