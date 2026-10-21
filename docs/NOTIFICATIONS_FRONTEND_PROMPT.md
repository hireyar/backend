# پرامپت یکپارچه فرانت اعلان‌های داشبورد

اعلان‌های Angular را با تغییرات جدید بک‌اند هماهنگ کن. مرجع قرارداد [NOTIFICATIONS_API.md](NOTIFICATIONS_API.md) است. این تحویل شامل دسترسی همه اعضا و یادآوری انقضا در۷،۳و۱روز است.

زنگ اعلان را در بالای سمت چپ داشبورد همه کاربران سازمان با نقش Owner و Recruiter نمایش بده؛ شرط Owner را از component، guard، service و اتصال SignalR بردار. دمو، تریال، اشتراک منقضی و سازمان بدون اشتراک فعال هم زنگ و تاریخچه دارند. نقش و سازمان از نشست معتبر فعلی خوانده شوند. وضعیت خواندن و badge هر عضو مستقل است؛ خواندن یک نفر وضعیت دیگری را تغییر نمی‌دهد. تاریخچه قدیمی مالک برای اعضا بازتولید نمی‌شود؛ عضویت تازه اعلان‌های آینده را می‌گیرد.

APIها با JWT فعلی پنل:

- `GET /api/notifications?status=all&page=1&pageSize=20`؛ status یکی از `all|read|unread`، page از۱ و pageSize از۱تا۱۰۰. پاسخ `{items,total,unreadCount,page,pageSize}`؛ total مربوط به فیلتر جاری و unreadCount مربوط به همه اعلان‌های خود کاربر است.
- هر item: `{id,type,title,target,interviewId,createdAtUtc,readAtUtc}`؛ شناسه Guid، تاریخ UTC، و readAtUtc=null یعنی خوانده‌نشده. عنوان سرور را به‌صورت متن معمولی نمایش بده؛ innerHTML استفاده نکن.
- `GET /api/notifications/unread-count` با پاسخ `{unreadCount}` برای badge.
- `PUT /api/notifications/{id}/read` با `{isRead:true}` یا `{isRead:false}` و پاسخ204. کلیک روی اعلان آن را خوانده‌شده کند؛ گزینه خوانده‌نشده‌کردن هم وجود داشته باشد. بازکردن dropdown یا GET همه را خوانده‌شده نکند. پس از تغییر موفق داده سرور تازه شود؛ شکست درخواست badge را نادرست تغییر ندهد.

زنگ badge مستقل با مخفی‌شدن صفر و نمایش99+ داشته باشد. dropdown راست‌به‌چپ با تب همه/خوانده‌نشده/خوانده‌شده، صفحه‌بندی واقعی، loading/empty/error، بستن با کلیک بیرون و Escape، focus/keyboard و aria-label بساز. تغییر فیلتر page را۱ کند؛ سازمان/گیرنده را در درخواست نفرست. 401 با refresh موجود مدیریت شود؛ 404 ممکن است اعلان یا مقصد حذف‌شده یا خارج از scope باشد.

چهار نوع اعلان:

| type | رفتار | target |
|---|---|---|
| InterviewStarted | شروع مصاحبه | Interview |
| InterviewCompleted | پایان مصاحبه؛ کلیک مستقیم گزارش همان interviewId | InterviewReport |
| SubscriptionExpiring | یادآوری۷،۳و۱روز تا پایان پوشش مؤثر | Subscription |
| InterviewCreditsLow | اعتبار قابل استفاده کمتر از۲ | InterviewCredits |

برای انقضا، فرانت زمان‌بندی یا اعلان محلی نسازد؛ عنوان دریافتی از سرور را نمایش بده. بک‌اند هر۶۰ثانیه با پنجره‌های `(3,7]`، `(1,3]` و `(0,1]` روز بررسی می‌کند؛ هر تاریخ انقضا/آستانه/گیرنده فقط یک اعلان دارد. اگر نخست در روزآخر بررسی شود فقط هشدار۱روزه می‌آید؛ هشدار عقب‌افتاده۷/۳روزه ساخته نمی‌شود. پس از انقضا هشدار تازه ساخته نمی‌شود و تاریخچه می‌ماند. هشدارهای قدیمی۵/۲روزه ممکن است در تاریخچه باشند؛ آن‌ها را فیلتر یا بازنویسی نکن. تمدید بر پایان مؤثر سرور محاسبه می‌شود، نه شمارش محلی مرورگر.

target را به route واقعی پروژه نگاشت کن؛ از title آدرس استخراج نکن. Interview و InterviewReport از interviewId استفاده کنند. پایان مصاحبه تضمین تکمیل تحلیل گزارش نیست؛ processing/readiness موجود رعایت شود. برای Owner، Subscription به اشتراک/تمدید و InterviewCredits به خرید اعتبار برود؛ برای Recruiter این دو مقصد پیام هماهنگی با مالک نمایش دهند، چون مجوز مالی همچنان Owner است. تاریخ UTC برای نمایش محلی تبدیل شود.

با `@microsoft/signalr` برای هر دو نقش به `/hubs/notifications` وصل شو: accessTokenFactory از auth موجود، withAutomaticReconnect و callback رویداد `NotificationsChanged` پیش از start. رویداد بدون payload است؛ پس از اتصال اولیه و reconnect شمارنده و فهرست تازه شوند. هنگام دریافت رویداد badge و اگر dropdown باز است صفحه فعلی را دوباره بگیر. retry شروع اولیه جدا از reconnect، ادغام یا لغو fetchهای همزمان و جلوگیری از overwrite پاسخ تازه با پاسخ قدیمی لازم است. logout اتصال را stop و state را پاک کند. configureLogging(LogLevel.None)؛ توکن و URL حاوی access_token در console، telemetry یا شواهد ثبت نشوند. polling فرانت اضافه نکن.

تحویل را با Owner و دو Recruiter، خواندن مستقل/چندتب، دریافت شروع/پایان و مقصد گزارش، یادآوری۷/۳/۱روز با داده آزمایشی بک‌اند، reconnect، refresh/401، empty/error/pagination، دمو/تریال، محدودیت مقصد مالی Recruiter و جداسازی دو سازمان بررسی کن. ارسال ایمیل/SMS، صدای خودکار و toast اجباری به این کار اضافه نکن. نتیجه تغییرات و تست‌های واقعاً اجراشده را گزارش بده.
