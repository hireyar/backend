# API تیکتینگ و پیوست خصوصی اروان

قرارداد بک‌اند روی dev، 2026-10-16. پنل Angular در این ریپو نیست. باکت خصوصی `hireyar-ticketing` در اروان ساخته و با فایل ساختگی PUT/GET، تطابق بایت‌ها و رد دسترسی anonymous آزموده شد؛ فایل آزمایشی حذف شد. نام `ticketing` در حساب موجود نبود و اروان آن را با BucketAlreadyExists رد کرد؛ نام جایگزین با تأیید صریح کاربر انتخاب شد.

## دسترسی و مسیرها

Owner و Recruiter با JWT معتبر پنل، تمام تیکت‌های سازمان فعلی را می‌بینند و پاسخ می‌دهند؛ تیکت شخصیِ جدا از سازمان نداریم. سازمان و نویسنده فقط از هویت سرور گرفته می‌شوند. دسترسی به شناسه تیکت/پیوست سازمان دیگر 404 است؛ پیوستِ تیکت دیگر حتی در همان سازمان 404 است. پشتیبانی به اشتراک فعال یا اعتبار مالی وابسته نیست.

سوپرادمین با همان policy=SystemAdmin و JWT مستقل فعلی، فهرست سراسری، جزئیات، پیام‌ها، پاسخ، وضعیت و دانلود را دارد. نقش/توکن HR یا فیلد administrator در body مجوز ادمین نمی‌دهد؛ JWT ادمین نیز JWT پنل نیست. این مشاهده و پاسخ سراسری استثنای صریحِ پشتیبانیِ درخواستی است؛ سازمان حساب ادمین یا تیکت جابه‌جا نمی‌شود. Candidate و anonymous دسترسی ندارند. auth کلید مسیر/ورود تغییری نکرده است.

| عملیات | مسیر پنل | مسیر سوپرادمین |
|---|---|---|
| فهرست | GET /api/tickets | GET /api/admin/tickets |
| ثبت تیکت | POST /api/tickets | وجود ندارد |
| جزئیات | GET /api/tickets/{id} | GET /api/admin/tickets/{id} |
| پیام‌ها | GET /api/tickets/{id}/messages | GET /api/admin/tickets/{id}/messages |
| پاسخ | POST /api/tickets/{id}/messages | POST /api/admin/tickets/{id}/messages |
| تغییر وضعیت | PUT /api/tickets/{id}/status | PUT /api/admin/tickets/{id}/status |
| دانلود | GET /api/tickets/{id}/attachments/{attachmentId} | GET /api/admin/tickets/{id}/attachments/{attachmentId} |

ثبت و پاسخ multipart/form-data هستند. ثبت: subject اجباری۱تا۲۰۰، body اجباری۱تا۸۰۰۰، priority اختیاری Low/Normal/High با پیش‌فرض Normal؛ files اختیاری با تکرار همان نام field. پاسخ: body و files. متن پس از Trim نباید خالی باشد. ثبت201 و Location، پاسخ200، هر دو TicketDto برمی‌گردانند؛ برای نمایش پیام تازه GET messages را بخوانید. پیوست به یک پیام مشخص تعلق دارد و آپلود جداگانه یا فایل عمومی نداریم.

فهرست پارامتر page پیش‌فرض۱ و pageSize پیش‌فرض۲۰ و حداکثر۵۰، status و priority اختیاری دارد؛ مرتب‌سازی updatedAtUtc نزولی و id است. پیام‌ها صفحه‌بندی مشابه دارند، به ترتیب createdAtUtc/id صعودی؛ برای مشاهده ادامه صفحه بعد بخوانید. پاسخ صفحه: items/page/pageSize/total.

TicketDto: id، organizationId، organizationName، createdByUserId، subject، status، priority، revision، createdAtUtc، updatedAtUtc. پیام: id، authorUserId، isStaff، body، createdAtUtc، attachments. metadata پیوست: id، fileName، contentType، length (بایت). کلید داخلی باکت یا URL عمومی در DTO نیست. نام نویسنده در این نسخه ارائه نمی‌شود؛ isStaff را برای برچسب پشتیبانی استفاده کنید.

وضعیت‌ها: Open یعنی منتظر پشتیبانی؛ Answered یعنی پاسخ پشتیبانی رسیده و منتظر کاربر؛ Closed یعنی بسته. ثبت/پاسخ کاربر Open و پاسخ سوپرادمین Answered می‌کند. در Closed هر دو طرف باید پیش از پاسخ بازگشایی کنند؛ پاسخ409/ticket_closed است. تغییر وضعیت JSON شامل status و revision آخرین TicketDto است. پنل فقط Open/Closed و ادمین هر سه وضعیت را می‌تواند تنظیم کند؛ Answered پنل403 است. revision قدیمی409/concurrency_conflict دارد و باید reload شود. پاسخ‌ها به‌صورت append ثبت می‌شوند؛ ویرایش/حذف پیام یا پیوست و یادداشت داخلی پشتیبانی نداریم. ارسال مجدد POST پیام جدید می‌سازد؛ فرانت هنگام پاسخ نامشخص شبکه ابتدا پیام‌ها را بازخوانی کند.

## امنیت فایل و سهمیه

- هر پیام حداکثر۳فایل، هر فایل۱بایت تا۵MiB؛ کل request حداکثر۱۶MiB، form fieldها و headerهای multipart محدود.
- فقط PNG، JPEG، PDF و TXT UTF-8؛ پسوند و MIME باید منطبق باشند. PNG/JPEG دارای signature و پایان container، PDF دارای header/EOF و TXT دارای UTF-8 معتبر و بدون کنترل‌های باینری است. SVG/HTML/اسکریپت/executable/ZIP/Office مجاز نیستند.
- نام۱تا۱۸۰کاراکتر، بدون مسیر، traversal، colon/ADS، control، Unicode format/bidi، نام مخفی یا فاصله/نقطه انتهایی. نام اصلی برای نمایش/دانلود است و هرگز کلید یا مسیر ذخیره‌سازی نمی‌شود.
- کلید با CSPRNG Guid در namespace سازمان ساخته می‌شود: tickets/{organizationGuidN}/{randomGuidN}، بدون استفاده از نام کلاینت.
- دیتابیس فقط metadata و StorageKey دارد؛ هیچ bytea، Base64، blob یا محتوای فایل در schema/رکورد ذخیره نمی‌شود. باکت خصوصی جدا از ویدیوهاست؛ credential، bucket یا URL از ورودی کاربر پذیرفته نمی‌شود.
- bucket ACL و policy پیش از read/write بررسی می‌شوند؛ دسترسی AllUsers/AuthenticatedUsers یا Allow با principal عمومی، پاسخ503/ticket_storage_not_private دارد. policy غایب NoSuchBucketPolicy معتبر است؛ خطای نامشخص fail closed است. آبجکت با ACL=Private و application/octet-stream ذخیره می‌شود؛ fallback محلی یا دیتابیسی وجود ندارد.
- دانلود فقط با Authorization و scope سازمان/ادمین از API، با Content-Disposition=attachment، application/octet-stream، no-store، nosniff و CSP sandbox؛ لینک عمومی یا presigned URL صادر نمی‌شود. فایل در فرانت inline/HTML نمایش داده نشود و پیام‌ها با text binding نمایش یابند.
- سقف هر تیکت۲۰۰پیام و۱۰۰MiB پیوست؛ هر سازمان۲۰تیکت غیربسته،۱۰۰۰تیکت کل و۵۰۰MiB پیوست. سهمیه‌ها زیر قفل سازمان موجود دوباره کنترل می‌شوند؛ بازگشایی نیز سقف تیکت باز را رعایت می‌کند. سقف‌ها فعلاً ثابت‌اند و بستن، محتوا/سهمیه بایت را حذف نمی‌کند.
- limiter مستقل۶۰درخواست در دقیقه به‌ازای IP برای همه endpointهای تیکت، بدون صف. این سقف با limiter ورود ادمین مستقل است؛ polling کم‌تکرار و صفحه‌بندی لازم است. proxy باید IP واقعی را طبق تنظیمات معتبر فعلی تحویل دهد.
- I/O شبکه بیرون قفل DB؛ پس از upload وضعیت و سهمیه دوباره کنترل می‌شوند. شکست آپلود/ذخیره/بسته‌شدن هم‌زمان باعث cleanup محدود۱۰ثانیه‌ای می‌شود. پیش از حذف پس از شکست transaction، نبود metadata مجدداً بررسی می‌شود تا commit با پاسخ گم‌شده فایل معتبر را حذف نکند. outage یا crash می‌تواند orphan خصوصی ایجاد کند؛ حذف قطعی در outage تضمین نشده و reconciliation عملیاتی باید فقط کلیدهای بدون metadata و با فاصله زمانی کافی را پاک کند.
- این بررسی signature، آنتی‌ویروس، parse کامل PDF/تصویر یا تضمین بی‌خطر بودن فایل نیست. در صورت نیاز عملیاتی به تشخیص malware باید اسکن قرنطینه پیش از انتشار فایل اضافه شود. سرور فایل را decode/extract/اجرا نمی‌کند.

خطاهای مهم: validation_failed/ticket_text، ticket_attachment_count، ticket_file_name/type/content با400؛ ticket_file_size یا body بزرگ413؛ ticket_limit/ticket_storage_limit/ticket_message_limit/ticket_closed و نسخه قدیمی409؛ storage unavailable503؛ rate429. پاسخ‌ها ProblemDetails با code/traceId هستند.

## تنظیم و انتشار

تنظیم مستقل TicketStorage با Endpoint، Region، Bucket=hireyar-ticketing، AccessKey و SecretKey. پیش‌فرض endpoint=https://s3.ir-central1.arvanstorage.ir و region=ir-central1 است. credential از فایل ignored مخصوص محیط یا TicketStorage__AccessKey / TicketStorage__SecretKey خوانده شود. مقدار واقعی در Git/لاگ نیست. client S3 با نام ticketing مستقل از client ویدیو است؛ credential مشترک محلی فقط در تنظیم ignored برای باکت تازه استفاده شد. اصل least privilege برای runtime: read/write/delete object و read ACL/policy همین باکت، بدون نیاز به ایجاد باکت در اجرای API. API به‌صورت خودکار باکت نمی‌سازد.

Migration افزایشی 20261015205303_AddSupportTickets باید پیش از نسخه API در مقصد اعمال شود. FK مرکب سازمان/تیکت/پیام، طول/نوع، کلید یکتا، index صفحه‌بندی و revision concurrency دارد؛ Down دارای تاریخچه با خطا متوقف می‌شود. تنظیم خصوصی محیط توسعه محلی تکمیل شده؛ تنظیم محیط سرور، اعمال migration مقصد و deploy در این درخواست انجام نشده‌اند.

Angular جدا باید صفحه ثبت تیکت و فهرست/جزئیات سازمان، و صفحه مدیریت سراسری در سوپرادمین بسازد؛ هر کدام JWT مربوط به خود را به API بدهند. در multipart مرورگر Content-Type را دستی تنظیم نکنید تا boundary صحیح ساخته شود. برای دانلود authenticated Blob بگیرید، نام را از Content-Disposition بخوانید و object URL موقت را بعد از دانلود revoke کنید. نام/متن را HTML نکنید. خطای413/429/503 را با پیام روشن و retry آگاهانه نشان دهید.

