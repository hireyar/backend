# اتصال اعلان‌های داشبورد Angular

قرارداد الزام‌آور: [NOTIFICATIONS_API.md](NOTIFICATIONS_API.md). API و SignalR آماده‌اند؛ این مخزن Angular ندارد.

در بالای سمت چپ داشبورد Owner همه سازمان‌ها، شامل تریال و دمو،، زنگ اعلان با badge تعداد خوانده‌نشده قرار بده. نمایش زنگ را به وضعیت یا نوع اشتراک محدود نکن. صفر badge را مخفی کند، تعداد زیاد را99+ نشان بده. با کلیک روی زنگ، همان‌جا dropdown راست‌به‌چپ با loading/empty/error و صفحه‌بندی باز شود. تب‌ها همه / خوانده‌نشده / خوانده‌شده باشند و در config کوچک فرانت تعریف شوند؛ نوع اعلان با وضعیت خواندن قاطی نشود. کلیک بیرون و Escape پنل را ببندد، keyboard/focus و aria-label رعایت شود.

فهرست از GET /api/notifications، badge از GET /api/notifications/unread-count؛ فیلتر و total از سرور. وضعیت خوانده‌شده با readAtUtc مشخص است؛ بازکردن dropdown همه را نخواند. روی انتخاب اعلان، PUT {id}/read با isRead=true و سپس مسیریابی target؛ گزینه علامت‌گذاری خوانده‌نشده با isRead=false داشته باشد. خطا badge را خوش‌بینانه خراب نکند.

targetهای Interview و InterviewReport را با interviewId به مسیر واقعی مصاحبه/گزارش همین پروژه وصل کن؛ برای InterviewCompleted مستقیماً گزارش باز شود و processing گزارش طبق قرارداد موجود نمایش داده شود. Subscription صفحه اشتراک/تمدید و InterviewCredits صفحه خرید اعتبار مصاحبه را باز کند. URL فرانت را از بک‌اند یا متن title استخراج نکن. تاریخ UTC را برای نمایش محلی تبدیل کن و عنوان را متن معمولی render کن، نه innerHTML.

با @microsoft/signalr به /hubs/notifications وصل شو؛ JWT فعلی HR در accessTokenFactory و refresh نشست از سرویس auth موجود. withAutomaticReconnect، callback NotificationsChanged بدون payload پیش از start، و reload بعد از اتصال اولیه/reconnect ضروری‌اند. هنگام باز بودن پنل، رویداد فهرست و badge را refresh کند؛ هنگام بسته‌بودن فقط badge. fetchهای همزمان را ادغام یا قبلی را cancel کن. برای شکست start اولیه retry محدود با وضعیت اتصال داشته باش؛ logout اتصال را stop و state را پاک کند. ثبت توکن/URL در console یا telemetry ممنوع؛ configureLogging(LogLevel.None). تایمر polling فرانت اضافه نکن.

بررسی‌های زمان‌دار بک‌اند هر۶۰ثانیه‌اند؛ شروع/پایان فوری push می‌شوند. اعلان پایان لزوماً به معنی تکمیل همه تحلیل‌های گزارش نیست. بدون toast اجباری/صدای خودکار/ارسال ایمیل یا SMS؛ محدوده همین زنگ و dropdown است.

تحویل با تست واقعی اتصال، reconnect، چند تب، read/unread، 401/refresh، target گزارش، دریافت اعلان برای دمو و تریال و نبود نشت بین دو سازمان؛ request URL حاوی access_token را در شواهد ذخیره نکن.
