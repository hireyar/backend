# ورود سوپرادمین و مدیریت پکیج‌ها

قرارداد بک‌اند روی `codex/super-admin` از `dev`، 2026-10-12. پنل Angular در این ریپو نیست؛ صفحهٔ ورود و جدول مدیریت باید در ریپوی فرانت ساخته شوند. بک‌اند HTML صفحهٔ `/admin/{randomkey}` را سرو نمی‌کند.

## آدرس و تنظیمات

آدرس فرانت: `https://hireyar.com/admin/{randomkey}`. `{randomkey}` جایگزین مقدار `SuperAdmin:RandomKey` می‌شود؛ آکولاد جزو آدرس واقعی نیست. HTTPS در مقصد طبق سیاست فعلی API الزامی است.

```json
{
  "SuperAdmin": {
    "Enabled": false,
    "RandomKey": "",
    "AccessMinutes": 15,
    "AuthRequestsPerMinute": 10
  }
}
```

فایل مشترک عمداً خاموش و بدون کلید است. در فایل ignored مخصوص محیط یا متغیرهای محیطی، Enabled=true و کلید تصادفی مستقل ۳۲ تا ۱۲۸ کاراکتر از حروف انگلیسی، رقم، `_` و `-` بدهید. تولید مناسب: ۳۲ بایت با CSPRNG و نمایش hex؛ مقدار را فقط در تنظیمات خصوصی ذخیره کنید. کلید واقعی، رمز و توکن در Git/لاگ ثبت نشوند. عمر نشست ۱ تا ۳۰ دقیقه، سقف درخواست ورود ۱ تا ۱۰۰۰ در دقیقه است؛ پیش‌فرض ۱۵ دقیقه و ۱۰ درخواست.

تنظیمات IOptions هنگام شروع خوانده می‌شوند؛ تغییر کلید/خاموش‌کردن پنل در مقصد نیاز به restart دارد. توکن‌ها به fingerprint کلید فعلی وابسته‌اند و پس از گردش کلید یا خاموش‌کردن پنل نامعتبرند. این کلید جای رمز و مجوز سوپرادمین را نمی‌گیرد.

## قرارداد ورود فرانت

کلید از segment مسیر فرانت خوانده و در header `X-Admin-Entry` به همهٔ endpointهای `/api/admin/auth` ارسال شود؛ کلید در URL API یا body قرار نگیرد. بک‌اند کلید صحیح را به فرانت بازنمی‌گرداند و endpoint کشف مسیر ندارد.

1. route guard فرانت پیش از نمایش login، `GET /api/admin/auth/entry` را با header صدا بزند. موفق: `200 {"enabled":true}`؛ کلید غایب/اشتباه یا تنظیم خاموش: 404. در 404 خود صفحهٔ ورود نمایش داده نشود.
2. `GET /api/admin/auth/captcha` با همان header: `{id,imageDataUrl,expiresAtUtc}`. تصویر `data:image/png;base64,...` را در `<img>` نمایش دهید، شش رقم انگلیسی بگیرید. پاسخ plaintext، متن SVG یا دادهٔ قابل انتخابِ پاسخ ارسال نمی‌شود.
3. `POST /api/admin/auth/login` با JSON زیر و header ورود:

```json
{
  "mobile": "<existing verified admin mobile>",
  "password": "<operator-provisioned password>",
  "captchaId": "<challenge UUID>",
  "captchaAnswer": "<six ASCII digits>"
}
```

موفق: `{accessToken,expiresAtUtc}`. موبایل قالب `09` و ۹ رقم دیگر دارد؛ رمز ۱۲ تا ۱۲۸ کاراکتر. خطای کپچا 400/`invalid_captcha`؛ رمز/حساب/مجوز نامعتبر یا قفل‌شده 401/`invalid_credentials`؛ محدودیت درخواست 429. خطای کپچا و رمز هر دو به کپچای تازه نیاز دارند. ورودی‌های نامعتبر مدل 400/`validation_failed` دارند.

کپچا دو دقیقه، یک‌بارمصرف و در حافظهٔ محدود یک فرایند است؛ حتی پاسخ اشتباه آن را مصرف می‌کند. پس از restart، challenge تازه لازم است. این کپچای تصویری ساده، مقاومت در برابر OCR یا تشخیص انسان توسط سرویس خارجی را اثبات نمی‌کند؛ محدودیت IP و قفل حساب دفاع‌های مستقل‌اند. پنل سراسری فعلاً زیرساخت چند replica ندارد.

4. توکن فقط در حافظهٔ فرانت و `Authorization: Bearer ...` درخواست ادمین استفاده شود. refresh token/cookie و تمدید خودکار صادر نمی‌شوند؛ بعد از انقضا دوباره login لازم است. `GET /api/admin/auth/me` نام و شناسهٔ حساب را می‌دهد. `POST /api/admin/auth/logout` همهٔ نشست‌های ادمین همان حساب را با security stamp باطل می‌کند؛ توکن محلی نیز حذف شود. هر دو header ورود را می‌خواهند.

توکن ادمین audience مستقل دارد و endpoint سازمان/داوطلب را باز نمی‌کند؛ حتی توکن OTP یک حساب دارای `IsSystemAdmin=true` نمی‌تواند endpoint ادمین را باز کند. در هر درخواست ادمین، فعال‌بودن حساب، تأیید موبایل، نقش Owner موجود، مجوز DB، security stamp و lockout دوباره بررسی می‌شوند. حذف مجوز/غیرفعال‌کردن حساب/تعویض رمز فوراً توکن‌های قبلی را رد می‌کند. پنج رمز اشتباه قفل پانزده‌دقیقه‌ای Identity می‌دهد؛ mutationها زیر قفل PostgreSQL کاربر serialize می‌شوند. خطای کاربر ناشناخته و فاقد مجوز یکسان است.

`/api/admin/pricing` کلید ورود را نمی‌خواهد؛ توکن اختصاصی ادمین از ورود صحیح، مجوز هر درخواست آن است. توکن‌های HR و Candidate پاسخ 401 دارند. همهٔ پاسخ‌ها no-store هستند. route فرانت حاوی کلید را در analytics/access log ثبت نکنید؛ تنظیم CORS همان originهای صریح موجود است. صفحات دروغین با key اشتباه ممکن است در سمت کلاینت ساخته شوند، اما داده/ورود واقعی از API دریافت نمی‌کنند.

## راه‌اندازی حساب توسط operator

مدل موجود `ApplicationUser.IsSystemAdmin` حفظ شده؛ سازمان/کاربر تازه یا عضویت چندسازمانی ساخته نمی‌شود. ابتدا حساب Owner فعال با موبایل تأییدشده باید موجود باشد. فرمان محلی زیر از composition فعلی و دیتابیس تنظیم‌شده استفاده می‌کند:

```powershell
dotnet run --project src/HireYar.Api --no-launch-profile -- --provision-super-admin
```

فرمان شمارهٔ حساب موجود و رمز جدید را تعاملی می‌گیرد؛ رمز روی صفحه، argument یا لاگ چاپ نمی‌شود. ورودی redirected پذیرفته نیست. رمز با PasswordHasher استاندارد Identity ذخیره، IsSystemAdmin=true، قفل/تعداد شکست صفر و security stamp تازه می‌شود؛ همه در یک تراکنش. سرور HTTP شروع نمی‌شود و migration خودکار ندارد. رمز ضعیف، حساب غایب یا غیر Owner عملیات را rollback می‌کند. این فرمان صریحاً دادهٔ حساب انتخاب‌شده در دیتابیس تنظیم‌شده را تغییر می‌دهد؛ در این پیاده‌سازی روی دیتابیس مقصد اجرا نشده است.

برچسب سوپرادمین از هیچ register/profile DTO پذیرفته نمی‌شود. برای حذف مجوز، operator مقدار IsSystemAdmin همان حساب را false می‌کند؛ endpoint عمومی ساخت/اعطای مجوز نداریم. command provisioning فقط در اختیار operator محیط اجراست و تعویض رمز موجود را هم انجام می‌دهد.

## مدیریت پکیج و قیمت

API فعلی و DTOهای [BILLING](BILLING.md) استفاده می‌شوند؛ جدول/Repository/migration جدید اضافه نشده است:

| عملیات | endpoint |
|---|---|
| فهرست همه پکیج‌ها، از جمله غیرعمومی و غیرفعال | GET `/api/admin/pricing/packages` |
| جزئیات | GET `/api/admin/pricing/packages/{id}` |
| ساخت و ویرایش نام/شرح/وضعیت/نرخ دقیقه USD/retention | POST `/packages`؛ PUT `/packages/{id}` |
| ساخت دوره و تغییر قیمت تومان/تخفیف/مدت/وضعیت | POST `/packages/{id}/periods`؛ PUT `/packages/{id}/periods/{periodId}` |
| تعریف قابلیت و اتصال به پکیج | GET/POST/PUT `/features`؛ PUT `/packages/{id}/features` |
| محصولات اعتبار AI | GET/POST/PUT `/ai-credit-packages` |

مسیرهای کوتاه جدول نسبت به `/api/admin/pricing` هستند. ویرایش پکیج `{package,rowVersion}`، ویرایش دوره `{period,rowVersion}` می‌گیرد. RowVersion نسخهٔ Base64 شانزده‌بایتی parent از آخرین پاسخ است؛ بعد از هر save پاسخ جدید را جایگزین کنید. ویرایش stale با 409 رد می‌شود و نیاز به reload دارد. حذف سخت پکیج نداریم؛ `isActive=false` برای غیرفعال‌کردن، `isPublic=false` برای مخفی‌کردن از کاتالوگ عمومی است.

قیمت `priceAmount` عدد صحیح تومان با currency=`IRT`؛ نرخ مصاحبه `aiPricePerMinuteUsd` با حداکثر شش اعشار؛ `discountPercent` صفر تا صد با دو اعشار است. قیمت و نرخ جدید روی قراردادهای خرید، اشتراک و مصاحبهٔ snapshotشده اثر بازگشتی ندارند؛ تست‌های PostgreSQL موجود این رفتار را بررسی می‌کنند. endpointهای بررسی مالی سازمان/adjustment موجود هم همان سیاست سوپرادمین اختصاصی را دارند؛ UI جدید این درخواست فعلاً پکیج‌هاست.

## اعتبارسنجی

نتیجهٔ نهایی build/test در STATUS و HANDOFF ثبت می‌شود. integration از PostgreSQL واقعی محلی و CAPTCHA مصنوعی صرفاً fixture استفاده می‌کند؛ تست واحد تصویر/expiry/مصرف و ظرفیت، اجرای واقعی CAPTCHA سرویس runtime را بررسی می‌کند. UI Angular، OCR، deployment، فعال‌سازی مقصد و ساخت حساب واقعی در این ریپو تأیید نشده‌اند.
