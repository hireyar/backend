# دعوت موبایلی زیرمجموعه سازمان

قرارداد جاری 2026-10-14؛ API فقط، Angular در ریپوی جدا. درخواست صریح کاربر برای زیرمجموعه جایگزین محدودیت پیشین «فقط Owner فعال» شد. یک Owner فعال، چند Recruiter و فقط یک سازمان برای هر حساب حفظ می‌شود؛ هیچ حساب تاریخی خودکار فعال/منتقل نمی‌شود.

## API مالک

همه مسیرهای زیر Bearer پنل سازمان با نقش Owner می‌خواهند؛ organizationId و role ورودی نیستند.

| مسیر | ورودی / نتیجه |
| --- | --- |
| GET /api/team/members | Page/PageSize؛ Page<TeamMemberDto> شامل id/fullName/mobile/role/isActive، فقط سازمان جاری |
| GET /api/team/invitations | Page/PageSize؛ Page<TeamInvitationDto> شامل id/mobile/firstName/lastName/createdAtUtc/expiresAtUtc/status |
| POST /api/team/invitations | `{ "mobile": "09xxxxxxxxx", "firstName": "سارا", "lastName": "آزمایشی" }`؛201 و TeamInvitationDto با شناسه دعوت |
| DELETE /api/team/invitations/{id} | لغو دعوت،204؛ تکرار لغو بی‌اثر، دعوت پذیرفته‌شده409 |
| POST /api/team/members/{id}/deactivate | غیرفعال‌سازی Recruiter،204؛ مالک قابل غیرفعال‌سازی نیست |

موبایل دقیقاً۱۱رقم ASCII با09، دعوت سه روز UTC اعتبار دارد. وضعیت‌ها Pending/Accepted/Cancelled/Expired هستند. این endpoint فقط دعوت را ثبت می‌کند؛ پیامک یا ایمیل دعوت به‌صورت خودکار نمی‌فرستد. مالک شناسه دعوت را برای پذیرش به دعوت‌شونده می‌رساند؛ فرانت می‌تواند لینک صفحه پذیرش خودش را با این id بسازد. id به‌تنهایی مجوز ورود نیست و نیازی به ذخیره token دعوت ندارد؛ OTP صحیح همان موبایل اجباری است. پیامک OTP از مسیر و تنظیمات فعلی استفاده می‌کند؛ این تغییر ارسال واقعی خارجی را آزمایش نکرده است.

موبایل حساب سازمان دیگر، Owner یا عضو فعال409/mobile_unavailable است؛ اطلاعات سازمان دیگر افشا نمی‌شود. موبایل با دعوت باز409/invitation_pending، حتی اگر دعوت متعلق به سازمان دیگری باشد. دعوت منقضی هنگام دعوت جدید بسته می‌شود. عضو غیرفعال Recruiter همان سازمان می‌تواند دعوت جدید دریافت کند. رکورد/شناسه/سازمان قبلی آن حفظ می‌شود؛ فعال‌سازی بدون پذیرش مجدد OTP نداریم.

## پذیرش صریح توسط دعوت‌شونده

1. id دعوت را از مالک دریافت کند.
2. GET /api/auth/csrf، نگهداری cookie و token.
3. POST /api/auth/otp/request با `{ "mobile": "09xxxxxxxxx" }` و X-CSRF-TOKEN.
4. پس از دریافت کد و تأیید پذیرش، POST /api/auth/team-invitations/{id}/accept با `{ "mobile": "09xxxxxxxxx", "code": "xxxxxx" }` و CSRF همان مرورگر؛ نیاز به Bearer قبلی نیست.
5. پاسخ200 همان AccessResponse قبلی شامل access token و expiresAtUtc، همراه refresh cookie HttpOnly. GET /api/auth/me با Bearer تازه نقش Recruiter و شناسه/نام سازمان را می‌دهد. CSRF را پس از ورود دوباره بگیرد.

کد/انقضا/حداکثر۵تلاش/مصرف یک‌باره و محدودیت نرخ OTP موجود ثابت‌اند. کد باید مربوط به موبایل خود دعوت باشد. پذیرش دعوت، ساخت حساب/فعال‌سازی، مصرف OTP و نشست در یک تراکنش هستند. سازمان جدید، trial و هدیه اضافه ساخته نمی‌شوند. دعوت غایب یا متعلق به موبایل دیگر پس از OTP معتبر404؛ لغوشده/منقضی/پذیرفته‌شده409/invitation_unavailable؛ کد نادرست یا مصرف‌شده401؛ CSRF نامعتبر400.

حساب پذیرفته‌شده برای ورود بعدی از otp/verify معمولی و refresh قبلی استفاده می‌کند. شماره تازه دارای دعوت باز اگر otp/verify معمولی بزند409/team_invitation_pending می‌گیرد تا اشتباهاً Owner سازمان جدید نشود؛ کد مصرف نمی‌شود و پذیرش صریح قابل ادامه است. برای عضو غیرفعال، ورود معمولی401 تا پذیرش دعوت جدید است.

## مجوزها

| قابلیت | Owner | Recruiter |
| --- | --- | --- |
| موقعیت، قالب، سؤال و ویزارد/پیشنهاد سؤال | مجاز | مجاز، با همان محدودیت قابلیت/اشتراک سازمان |
| دعوت کارجو، فهرست/جزئیات مصاحبه، لغو/حذف/resend و رسانه خصوصی | مجاز | مجاز، فقط همان سازمان |
| خواندن گزارش، status، review و چاپ | مجاز | مجاز، فقط همان سازمان |
| retry/regenerate گزارش و ثبت تصمیم انسانی | مجاز |403 |
| نام شخص در me | مجاز | مجاز |
| مشاهده اطلاعات پایه سازمان/داشبورد | مجاز | مجاز |
| تغییر نام سازمان، مدیریت تیم و تمام /api/billing | مجاز |403 |
| diagnostics و سوپرادمین | مجوز قبلی | دسترسی اضافه ندارد |

گزارش و اطلاعات سازمان دیگر404 هستند. غیرفعال‌سازی access را در validation هر درخواست رد و همه refreshها را revoke می‌کند؛ OTP عادی هم رد است. SecurityStamp جدید مانع معتبرشدن دوباره access قدیمی پس از پذیرش دعوت تازه است. نشست Owner قبلی فاقد account_stamp برای سازگاری پذیرفته می‌شود؛ Recruiter الزاماً stamp معتبر می‌خواهد. هیچ فیلد هویتی یا نقش از ورودی کاربر به claim تبدیل نمی‌شود.

## دیتابیس و انتشار

Migration `20261013092724_AddMobileTeamInvitations`: جدول جدید مستقل MobileTeamInvitations با FK سازمان و Restrict، موبایل pending یکتای سراسری و check قالب/انقضا/وضعیت؛ index کاربر برای فقط یک Owner فعال، حذف check منع Recruiter فعال. داده‌های قبلی، TeamInvitations ایمیلی تاریخی و دعوت کارجوی موجود محفوظ‌اند. downgrade دارای تاریخچه دعوت یا Recruiter فعال خطا می‌دهد و داده را حذف نمی‌کند.

قفل تراکنشی موبایل مشترک بین دعوت/signup/پذیرش/لغو/غیرفعال‌سازی، سپس سازمان، رقابت موبایل ثبت‌نشده را هم کنترل می‌کند؛ unique index پشتوانه DB است. زیرساخت توزیع‌شده یا Repository جدید اضافه نشده است. migration باید پیش از اجرای نسخه جدید در مقصد اعمال شود؛ این نوبت فقط PostgreSQL آزمایشی محلی تغییر کرده است. push/deploy/SMTP/SMS واقعی و فرانت انجام نشده‌اند.

## پرامپت اتصال فرانت

در تنظیمات حساب، بخش «اعضای سازمان» را فقط برای role=Owner بساز. فهرست اعضا و دعوت‌ها را با GET /api/team/members و GET /api/team/invitations و صفحه‌بندی بخوان. فرم شماره موبایل دقیقاً۱۱رقم ASCII با09 و دو ورودی اجباری نام و نام خانوادگی داشته باشد؛ mobile/firstName/lastName را به POST /api/team/invitations بفرستد. هر نام حداکثر۱۰۰کاراکتر و نام کامل پس از Trim و اتصال با یک فاصله حداکثر۲۰۰کاراکتر است.201 یعنی «دعوت ثبت شد»، نه «پیامک ارسال شد». با id برگشتی، لینک صفحه پذیرش در دامنه همین فرانت بساز و امکان کپی بده؛ API مسیر فرانت را تعیین نمی‌کند. لغو دعوت با DELETE invitations/{id}، غیرفعال‌سازی زیرمجموعه با POST members/{id}/deactivate و سپس refresh فهرست‌ها باشد؛ مالک دکمه غیرفعال‌سازی ندارد. حالت انتظار و خطاهای400/401/403/404/409 را نمایش بده.

صفحه پذیرش id را از URL بگیرد، موبایل و OTP و تأیید صریح پذیرش را بگیرد و طبق جریان CSRF بالا به POST /api/auth/team-invitations/{id}/accept وصل شود؛ در این صفحه از otp/verify معمولی استفاده نکن. token/cookie/code را در URL، لاگ یا ذخیره دائمی نگذار؛ روش مدیریت access و refresh پروژه را حفظ کن. بعد از ورود، me را بخوان و نام سازمان/role را در state مشترک به‌روز کن. برای Recruiter منوهای تیم/تنظیمات سازمان/مالی/diagnostics و retry گزارش/تصمیم انسانی را مخفی کن، خواندن گزارش و مسیرهای کاری را باز نگه دار. در تنظیمات حساب Recruiter فقط fullName را PUT کن، organizationName را ارسال نکن. کنترل UI جای مجوز API را نمی‌گیرد.409/team_invitation_pending در ورود معمولی به‌معنی نیاز به لینک پذیرش از مالک است؛ سازمان دیگری نساز.

## اعتبارسنجی نهایی 2026-10-13

`dotnet tool restore` و restore موفق؛ build نهایی صفر warning/error؛ کل suite384/384 موفق، صفر شکست/Skip، PostgreSQL18 اختصاصی loopback:55493 و FFmpeg واقعی محلی، providerهای مصنوعی/Disabled و SMTP loopback. هفت تست تازه دعوت/پذیرشOTP/CSRF، scope/مجوزها، ساخت موقعیت/قالب/دعوت کارجو و گزارش واقعی report-v3/چاپ، لغو/انقضا/تکرار/overposting، غیرفعال‌سازی و جلوگیری از بازگشت توکن قدیمی، دو رقابت invite/accept و signup/invite، یکتایی Owner و رد downgrade دارایتاریخچه را پوشش می‌دهند. guard قدیمی تست Cleanup مطابق تصمیم جدید برای یک Owner محفوظ و ورود Recruiter تاریخی غیرفعال همچنان رد است. نتیجه `tests/HireYar.Tests/TestResults/mobile-team-verified.trx`.

اجرای کامل اول377موفق/2شکست/5Skip ناشی از encoder معرفی‌نشده در دو fixture مستقل Production و نبود HIREYAR_TEST_FFMPEG بود؛ نسخه نهایی با AI__Provider=Disabled و FFmpeg معرفی‌شده به محیط فرایند، بدون تغییر config فایل‌ها، کامل موفق بازاجرا شد. شکست‌های اولیه تست تازه پس از تصحیح نام InterviewId و تکمیل پیش‌شرط خرید مصنوعی مالک/resumeText فرم رفع شدند؛ محدودیت billing/runtime قبلی حذف نشد. EF has-pending-model-changes بدون اختلاف و SQL idempotent در artifacts/mobile-team-migrations.sql تولید شد؛ فایل SQL جداگانه اجرا نشد، migration از طریق fixture واقعی اعمال و guard Down آزمایش شد. تغییرات موجود کاربر و نام سازمان قبلی حفظ شدند؛ فرانت، داده/تنظیمات واقعی مقصد، ارسال خارجی، commit/push/deploy و production تغییر نکردند.

## قابلیت‌های سازمان برای فرانت Owner و Recruiter — 2026-10-13

کمبود خواندن قابلیت‌ها برای Recruiter تکمیل شد. مسیر جدید مستقل از billing:

`GET /api/organization/features` با Bearer حساب Owner یا Recruiter، بدون organizationId ورودی و با Cache-Control:no-store.

فیلدهای پاسخ200: `hasActiveSubscription`، `isTrial`، `startedAtUtc`، `expiresAtUtc` و `features`. هر عضو features شامل `code`، `name`، `dataType` (Boolean/Integer/Text طبق enum قرارداد فعلی)، `value` است؛ همان snapshot و منبع CurrentAsync اشتراک فعلی. پاسخ فاقد قیمت، نرخAI، اعتبار/کیف‌پول، payment، خرید، شناسه یا نام پکیج است. inactive/expired پاسخ200 با hasActiveSubscription=false، isTrial=false، زمان‌هاnull و features=[] دارد. اشتراک آینده قبل از شروع فعال نیست؛ trial تا زمان پایان دقیق خودش فعال و قابلیت‌های snapshot اجتماع پکیج‌ها را دارد. اطلاعات سازمان جاری از حساب بررسی‌شده سرور خوانده می‌شود.

این endpoint فقط entitlement سازمان را می‌گوید؛ تضمین کافی‌بودن شارژ یا سقف رزرو نیست. تمام /api/billing برای Recruiter همچنان403 هستند. در خطای insufficient_ai_credit کاربر را به مالک برای شارژ هدایت کنید، نه خرید اجباری پکیج پیشرفته. schema/migration تازه یا تغییر محاسبه entitlement لازم نیست.

پرامپت تکمیلی فرانت: دریافت قابلیت‌ها برای هر دو نقش را به GET /api/organization/features تغییر بده؛ features از ریشه پاسخ خوانده شود، نه current.features. hasActiveSubscription و expiresAtUtc مبنای دسترسی جدید و features.code مبنای روشن‌کردن قابلیت‌ها هستند. مصاحبه عمومی hr_interview+ai_interview، تخصصی technical_interview+ai_interview؛ پیشنهاد سؤال AI و قالب موضوعی پیشرفته advanced_ai_workflow+adaptive_questions+adaptive_difficulty می‌خواهند. وابستگی به packageCode یا شرط role=Owner برای قابلیت مصاحبه/سؤالAI نگذار؛ نقش فقط مدیریت اعضا/تنظیمات/مالی و سایر موارد ماتریس بالا را محدود می‌کند. پس از تغییر اشتراک و هنگام ورود/بارگذاری پنل وضعیت را دوباره بخوان؛ خطای درخواست را حالت قابلیت‌نامعلوم با امکان retry نشان بده، نه بی‌اشتراکی قطعی. در نبود subscription یا feature مربوط، UI را محدود کن؛ بررسی نهایی API همیشه authoritative است. APIهای مالی مالک را برای بخش مالی خودش حفظ کن و برای Recruiter آن‌ها را فراخوانی نکن.

اعتبارسنجی تکمیل features: build صفر warning/error و42/42 تست مرتبط، صفر شکست/Skip روی PostgreSQL18 اختصاصی loopback55494؛ organization-features-verified.trx. DTO fields دقیقاً پنج فیلد غیرمالی، برابری قابلیت‌های خرید/trial با source فعلی، بی‌اشتراکی/انقضا، scope و no-store/401/403 مالی بررسی شدند. migration تازه لازم نیست؛ انتشار مقصد/تغییر فرانت انجام نشده است.

## نام و نام خانوادگی در دعوت — 2026-10-14

دعوت جدید اکنون هر سه فیلد را اجباری می‌گیرد:

```json
{
  "mobile": "09xxxxxxxxx",
  "firstName": "سارا",
  "lastName": "آزمایشی"
}
```

هر نام Required، غیرخالی/غیرصرفاً whitespace و حداکثر۱۰۰کاراکتر ورودی است؛ جمع طول دو مقدار Trim‌شده به‌علاوه یک فاصله باید حداکثر۲۰۰باشد. مثال100+100 با400 رد و100+99 پذیرفته می‌شود. فاصله اول/آخر در ذخیره حذف، فاصله داخلی/نیم‌فاصله حفظ می‌شود. درخواست قدیمی فقطmobile یا نام ناقص/بیش‌ازحد400 ValidationProblemDetails می‌گیرد و دعوت/حساب ساخته نمی‌شود.

POST201 و GET فهرست دعوت‌ها firstName/lastName را برمی‌گردانند. داده به MobileTeamInvitations افزوده و در دعوت pending نگهداری می‌شود؛ هنگام ایجاد دعوت حساب/نام پروفایل موجود تغییر نمی‌کند. پس از پذیرش صریح با همان OTP قبلی، FullName حساب جدید یا Recruiter غیرفعال همان سازمان از `firstName.Trim() + " " + lastName.Trim()` تنظیم می‌شود. سپس me و members همان FullName را دارند و شخص مثل قبل می‌تواند نام خودش را ویرایش کند. reinvite دارای نام تازه فقط هنگام پذیرش آن را روی FullName قبلی اعمال می‌کند؛ تا پذیرش نام قبلی محفوظ است. هیچ تغییر نقش/سازمان/نشست یا انتقال حساب اضافه نشده است.

برای دعوت‌های تاریخی، ستون‌های تازه null/null می‌مانند و داده حدسی backfill نمی‌شود. این دعوت‌ها با flow OTP قبلی قابل‌پذیرش‌اند؛ حساب موجود نام قبلی‌اش را نگه می‌دارد و حساب جدید legacy همان پیش‌فرض «کاربر» را می‌گیرد. فرانت در فهرست دعوت قدیمی برای نامnull از موبایل/عنوان جایگزین استفاده کند و آن را خطا نداند. پذیرش دعوت همچنان فقط mobile/code می‌خواهد؛ firstName/lastName در فرم پذیرش الزامی جدید نیستند.

Migration `20261014052638_AddMobileTeamInvitationNames` باید قبل از نسخه جدید API در مقصد اعمال شود. دو ستون nullableباmaxlength100 و check نامِ جفت/طول مجموع به جدول موجود اضافه می‌کند؛ داده‌های قدیمی محفوظ‌اند. Down در حضور هر نام ذخیره‌شده با خطا متوقف می‌شود تا نام‌ها حذف نشوند. schema حساب/FullName قبلی تغییر نکرده است. انتشار، تغییر دیتابیس مقصد و فرانت در این تسک انجام نشده‌اند.

پرامپت تکمیلی فرانت: فرم «دعوت عضو» فقط برای Owner، سه ورودی mobile، firstName با عنوان «نام» و lastName با عنوان «نام خانوادگی» داشته باشد؛ هر سه required و نام‌هاmax100 و combinedTrimLength+space<=200. همان POST /api/team/invitations را با هر سه فیلد بزن. خطاهای validation400 را کنار ورودی مربوط نمایش بده. نام/نام خانوادگی پاسخ را در فهرست دعوت‌ها نشان بده و null/null دعوت قدیمی را پشتیبانی کن؛ فهرست اعضا و حساب جاری همچنان fullName دارند. جریان لینک دعوت، OTP، پذیرش، role و organizationId را تغییر نده و نام‌ها را به درخواست accept اضافه نکن. هماهنگی انتشار فرانت با migration/API جدید لازم است؛ فرانت نام را به‌عنوان نمایش داده استفاده کند و در HTML escape کند.

اعتبارسنجی نام دعوت: build صفرwarning/error و42/42 تست مرتبط بدون شکست/Skip روی PostgreSQL18 اختصاصی محلی، سه regression تازه نام/Trim/200حد/validation/Swagger/reinvite/legacyUp/guardDown؛ team-invitation-names-verified.trx. مدل و snapshot بدون اختلاف؛ SQL delta idempotent در artifacts/team-invitation-names-migrations.sql تولید، جداگانه اجرا نشده است. مقصد/فرانت/انتشار تغییر نکردند.
