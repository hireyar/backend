# ارسال اختیاری دعوت مصاحبه — 2026-10-08

بک‌اند روی dev؛ Angular در ریپوی جدا است. ارسال در دعوت ویزارد، دعوت قدیمی و دعوت مجدد پشتیبانی می‌شود.

## قرارداد فرانت

در فرم multipart `POST /api/panel-wizard/invitations` فیلدهای `sendEmail` و `sendSms` را مستقل ارسال کنید. در JSON دعوت قدیمی و `POST /api/interviews/{id}/resend` نیز همین دو bool پذیرفته می‌شوند. پیش‌فرض هر دو false است؛ انتخاب کاربر بدون فعال‌بودن و کامل‌بودن تنظیمات کانال ارسال ایجاد نمی‌کند.

پاسخ ویزارد/resend فیلدهای `emailDelivery` و `smsDelivery` دارد؛ پاسخ دعوت قدیمی همین فیلدها را کنار لینک/کد دارد:

| مقدار | معنی |
|---|---|
| NotConfigured | کانال خاموش یا تنظیمات لازم ناقص است |
| NotRequested | کانال آماده است ولی کاربر انتخاب نکرده |
| Sent | SMTP یا API کاوه‌نگار درخواست را پذیرفته؛ اثبات تحویل به گیرنده نیست |
| Failed | خطای ارسال یا گیرنده نامعتبر؛ دعوت ساخته‌شده معتبر باقی است |

UI بعد از Failed نباید ساخت دعوت را خودکار تکرار کند. resend جلسه جدید می‌سازد و لینک قبلی را باطل می‌کند؛ retry ارسال روی همان جلسه در این درخواست اضافه نشده است.

## appsettings

`InterviewNotifications:Email` مستقل است: Enabled، Host، Port، EnableSsl (STARTTLS)، Username، Password، FromAddress، FromName و TimeoutSeconds. Host/FromAddress الزامی، Port بین 1 و 65535، timeout بین 1 و 60؛ Username/Password یا هر دو خالی برای SMTP بدون احراز هویت یا هر دو مقدار دارند. ایمیل متن UTF-8 با نام داوطلب، عنوان شغل، لینک، کد و انقضای UTC دارد. برای SMTP محلی می‌توان Port=1025 و EnableSsl=false گذاشت. تست‌ها فقط SMTP loopback مصرف می‌کنند.

`InterviewNotifications:Sms` مستقل است: Enabled، ApiKey و Template. اگر ApiKey خالی باشد از `Kavenegar:ApiKey` موجود OTP استفاده می‌کند؛ Template دعوت همیشه مستقل و الزامی است. UseFixedOtp در کد دعوت اثری ندارد. هر دو Enabled پیش‌فرض false هستند.

پترن کاوه‌نگار برای `verify/lookup.json`:

- `token`: کد شش‌رقمی ورود مصاحبه، با حفظ صفر آغازین.
- `token2`: لینک کامل صفحه مصاحبه؛ کد در URL نیست.
- `type`: sms؛ `receptor`: موبایل داوطلب به فرم 09 و 11 رقم.

نمونه متن پترن: «برای مصاحبه دعوت شده‌اید. کد ورود: %token — لینک: %token2». نام پترن تأییدشده را در InterviewNotifications:Sms:Template قرار دهید؛ جایگذاری مطابق [SDK رسمی کاوه‌نگار](https://github.com/kavenegar/kavenegar-php/blob/master/src/KavenegarApi.php) است. پذیرش لینک کامل در پترن حساب مقصد باید با داده مصنوعی بررسی شود.

کلید/رمز واقعی را در فایل local خارج Git یا متغیرهای محیطی بگذارید، مانند `InterviewNotifications__Email__Password` و `InterviewNotifications__Sms__ApiKey`. تنظیمات/کلیدهای قدیمی تغییر نکرده‌اند. بعد از تغییر تنظیمات سرویس را restart کنید.

## اجرا و محدودیت

Application فقط interface اعلان را مصرف می‌کند؛ SMTP و HTTP در Infrastructure هستند. ارسال بعد از commit و خارج قفل مالی انجام می‌شود؛ هر کانال مستقل، بدون retry خودکار، صف، migration یا ذخیره وضعیت تحویل. خطای کانال دیگر را متوقف نمی‌کند. تماس خارجی timeout دارد و CancellationToken درخواست را مصرف می‌کند؛ قطع درخواست/فرایند پس از commit ممکن است ارسال را نیمه‌کاره کند. وضعیت‌ها پاسخ همان درخواست هستند و در DB ماندگار نیستند.

بدنه پیام، گیرنده، کد، لینک، کلید و پاسخ provider لاگ نمی‌شوند؛ HttpClient بدون factory است چون کلید کاوه‌نگار داخل URL است. ارسال واقعی ایمیل/SMS و تنظیم مقصد در این تغییر انجام نشده‌اند.