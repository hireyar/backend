# ذخیره و پخش خصوصی ویدیوی مصاحبه

به‌روزرسانی: 2026-10-12؛ بک‌اند روی dev. Angular در ریپوی جداست. API بخش‌بندی برای ادامه ضبط پس از رفرش اضافه شد؛ [RECORDING_SEGMENTS_API](RECORDING_SEGMENTS_API.md) قرارداد جدید است. انتشار مقصد همچنان در این درخواست انجام نشده است.

## تنظیمات

`InterviewStorage:VideoProvider=ArvanS3` و `InterviewStorage:S3` در appsettings.json هستند. Endpoint سرویس `https://s3.ir-central1.arvanstorage.ir`، Bucket برابر `hireyar` و Region برابر `ir-central1` است. آدرس ارائه‌شده `https://hireyar.s3.ir-central1.arvanstorage.ir` آدرس bucket است؛ SDK از endpoint سرویس و ForcePathStyle استفاده می‌کند تا نام bucket دوبار وارد مسیر نشود.

AccessKey و SecretKey در فایل مشترک خالی‌اند؛ مقادیر ارائه‌شده طبق درخواست کاربر فقط در `src/HireYar.Api/appsettings.Development.local.json` تنظیم شدند. این فایل در Git و Docker context نادیده گرفته می‌شود و Program آن را در Development می‌خواند. هیچ کلید یا لینک خصوصی در این سند ثبت نمی‌شود. مقادیر دیگر appsettings حفظ شده‌اند.

در مقصد Production باید همان تنظیمات را در appsettings.Production.local.json روی میزبان یا متغیرهای `InterviewStorage__S3__AccessKey` و `InterviewStorage__S3__SecretKey` فراهم کنید؛ فایل محلی توسعه در image نیست. شروع برنامه با provider فعال و credential خالی/endpoint نامعتبر رد می‌شود. override محیطی اولویت بالاتر دارد. تنظیمات singleton هستند و تغییرشان نیازمند restart است؛ این تغییر deploy نشده است.

## ذخیره و حریم خصوصی

فقط category=videos برای ویدیوی پاسخ، معرفی و ضبط کامل جلسه به S3 منتقل می‌شود. صوت، سؤال TTS و رزومه محلی باقی‌اند. کلید جدید به شکل `s3/videos/{sessionId:N}/{randomGuid:N}.audio` است؛ نام واقعی فایل/داوطلب وارد key نمی‌شود. پسوند تاریخی audio در key نوع واقعی فایل را تعیین نمی‌کند؛ API از ContentType اعتبارسنجی‌شدهٔ دیتابیس استفاده می‌کند.

کلیدهای قبلی بدون پیشوند s3 همچنان از storage محلی خوانده/حذف می‌شوند؛ volume قدیمی را نگه دارید. فایل یا دادهٔ قبلی خودکار منتقل نمی‌شود و migration لازم نیست. پس از ثبت فایل S3، provider را به Local برنگردانید مگر با انتقال صریح داده؛ Local کلیدهای S3 را نمی‌خواند. S3 ناموفق نیز به ذخیره محلی fallback نمی‌کند.

پیش از هر ذخیره/بازکردن فایل S3، GetBucketAcl و GetBucketPolicy احرازشده بررسی می‌شوند. grant عمومی AllUsers/AuthenticatedUsers یا Allow با Principal عمومی/NotPrincipal رد می‌شود؛ سیاست شرط‌دار با Principal=* نیز عمداً رد می‌شود. نبود policy فقط با خطای مشخص NoSuchBucketPolicy قابل قبول است؛ پاسخ مبهم، خطای شبکه، دسترسی ناکافی یا policy نامعتبر به 503 منتهی می‌شود. SDK v4 پاسخ 404 این عملیات را گاهی در response با XML برمی‌گرداند؛ فقط XML امن با Code=NoSuchBucketPolicy پذیرفته می‌شود و DTD ممنوع است. این بررسی جای اختیار operator برای خصوصی نگه‌داشتن bucket در آینده را نمی‌گیرد.

فایل با ACL=Private ساخته می‌شود. credential سرویس به GetBucketAcl/GetBucketPolicy و ایجاد، UploadPart، Complete/Abort، GetObject/HeadObject و DeleteObject در bucket/prefix مربوط نیاز دارد؛ کلید به فرانت فرستاده نمی‌شود. درخواست HTTP سرویس با HTTPS/امضای SDK است؛ خطای خام provider، token و URL امضاشده در پاسخ API ثبت نمی‌شوند.

## آپلود فعلی

قرارداد HTTP موجود حفظ شده است: POST multipart به answers/introduction و `POST /api/candidate/interviews/{id}/recording` با Bearer دعوت همان مصاحبه. signature/نوع/حجم فایل، دسترسی دعوت و retention قبل از ذخیره بررسی و پیش از commit دوباره کنترل می‌شوند. token و id ورودی مجوز دسترسی به مصاحبه دیگری نمی‌دهند.

انتقال بک‌اند به S3 چندبخشی با قطعات 8 MiB، بافر محدود یک قطعه و ترتیب قطعی است؛ کل فایل در MemoryStream جمع نمی‌شود. سقف واقعی خوانده‌شده enforce می‌شود و stream خالی رد می‌شود. SDK برای خطاهای قابل تکرار retry محدود دارد. بعد از خطا/لغو، Abort و Delete برای همان key تصادفی با cancellation مستقل و مهلت ۱۰ ثانیه تلاش می‌شوند؛ پاسخ گم‌شدهٔ Complete نیز به پاک‌سازی key منجر می‌شود. قطع کامل فرایند/شبکه ممکن است upload ناقص یا فایل بی‌مرجع باقی بگذارد؛ پیشنهاد عملیاتی، Lifecycle فقط برای AbortIncompleteMultipartUpload پس از یک روز است. این policy در bucket اعمال نشده؛ lifecycle حذف کلی فایل‌ها پیشنهاد نمی‌شود چون retention سازمان‌ها متفاوت است.

**محدودیت:** کلاینت هنوز فایل را به API می‌فرستد؛ IFormFile در ASP.NET ممکن است آن را روی دیسک موقت buffer کند. این نسخه browser-to-S3 نیست. مسیر تک‌فایلی قبلی resume بایت‌های فایل ندارد؛ مسیر جدید بخش‌بندی retry یک بخش مستقل را با hash و شناسه ثابت می‌پذیرد و manifest برای ادامه بعد از refresh دارد. سقف ضبط کامل/مجموع بخش‌ها 512 MiB، هر بخش جدید 40 MiB و پاسخ/معرفی همان تنظیمات قبلی‌اند. از concatenate خام فایل‌های WebM/MP4 مستقل یک فایل معتبر حاصل نمی‌شود؛ کاربر نمایش پشت‌سرهم بخش‌ها را انتخاب کرده است.

## آماده‌سازی انتشار؛ اعمال مقصد انجام نشده

- Production Compose اکنون `HIREYAR_S3_ACCESS_KEY` و `HIREYAR_S3_SECRET_KEY` را الزام می‌کند و به تنظیمات SDK می‌رساند؛ این متغیرها در محیط خصوصی میزبان قرار گیرند. کلیدها در image/Git یا خروجی config غیرquiet چاپ نشوند. روش استقرار دیگر باید InterviewStorage__S3__AccessKey/SecretKey را فراهم کند. endpoint/bucket/region مشترک و ACL=Private حفظ شده‌اند؛ grantهای عمومی یا policy نامعلوم در مسیر ذخیره/خواندن رد می‌شوند. bucket را هنگام انتشار با مجوزهای مستند بالا بررسی کنید؛ هیچ policy مقصد خودکار تغییر نکرده است.
- Migration `20261012193519_AddInterviewRecordingSegments` پیش از API جدید لازم است. برای baseline دارای ConfigureRegistrationTrial، delta idempotent در artifacts/recording-segments-migrations.sql تولید می‌شود؛ baseline دیگری ابتدا migrationهای قبلی را نیاز دارد. اجرای migration مقصد در تسک انتشار جداست.
- سقف بایت فایل ضبط یکپارچه در Application دقیقاً 536870912 است؛ بدنه multipart در API حداکثر 514 MiB برای overhead. در proxy سقف بدنه حداقل 514 MiB تنظیم شود تا فایل 512 MiB به‌دلیل envelope رد نشود؛ مسیر بخش‌ها بدنه 42 MiB/فایل 40 MiB دارد و مجموع 512 MiB زیر قفل enforce می‌شود. ظرفیت و سقف‌ها در تمام hopها (CDN/ingress/proxy/API) بررسی شوند.
- Dockerfile دایرکتوری خصوصی و قابل نوشتن `/app/upload-temp` برای APP_UID و ASPNETCORE_TEMP می‌سازد؛ Compose volume اختصاصی upload-temp دارد. برای هر آپلود تک‌فایلی هم‌زمان دست‌کم 514 MiB فضای آزاد به‌اضافه حاشیه، سایر آپلودها و لاگ‌ها در نظر بگیرید؛ برای بخش‌ها نیز حجم هم‌زمان را حساب کنید. volume در مقصد از نظر quota/فضای آزاد/permission بررسی شود. به buffer پیش‌فرض روی root filesystem کوچک تکیه نکنید.
- نمونه Nginx قابل تطبیق در [video-upload.nginx.conf](examples/video-upload.nginx.conf) سقف 514m، inactivity timeoutهای 600s و خاموش‌بودن buffering پراکسی را نشان می‌دهد؛ upstream و location باید با proxy واقعی تطبیق داده و قبل reload اعتبارسنجی شوند. این snippet نصب/reload نشده و timeout واقعی به شبکه/سرعت/پلتفرم وابسته است؛ تنظیم آن IFormFile buffering در API را حذف نمی‌کند.
- آزمون انتشار با ویدیوی مصنوعی شامل POST، GET احرازشده Range=bytes=0-1023 →206، Origin مجاز و هدرهای expose، GET بدون JWT →401، سازمان دیگر →404، کنترل سقف/فضای temp و خصوصی‌بودن bucket انجام شود. تغییر CORS API به CORS عمومی bucket نیاز ندارد؛ مرورگر هنوز از API می‌خواند.

## مشاهدهٔ مدیر

URL خروجی همچنان مسیر API است، نه آدرس مستقیم S3:

- ضبط کامل: `GET /api/interviews/{id}/recording/video`
- ویدیوی معرفی: `GET /api/interviews/{id}/introduction/video`
- ویدیوی پاسخ: `GET /api/interviews/{id}/answers/{answerId}/video`

هر درخواست، از جمله Range، JWT معتبر Owner و سازمان بررسی‌شدهٔ سرور می‌خواهد؛ سازمان دیگر 404 و درخواست بدون JWT معتبر 401 دارد. کنترل حذف و retention موجود برقرار است. داوطلب با token دعوت نمی‌تواند endpoint مدیر را باز کند. URL فایل به‌تنهایی مجوز نیست؛ URL امضاشدهٔ GET، endpoint عمومی، redirect به S3 یا token در query اضافه نشده است.

stream برای FileStreamResult قابلیت seek دارد. HEAD اندازه/ETag را می‌گیرد و خواندن با Rangeهای حداکثر 8 MiB و If-Match همان ETag از S3 انجام می‌شود؛ کل فایل برای پخش دانلود/در حافظه/روی دیسک کپی نمی‌شود. پاسخ Range استاندارد 206/Content-Range و Cache-Control:no-store دارد. بایت‌ها از API عبور می‌کنند؛ پهنای باند سرور همچنان هزینهٔ این انتخاب امنیتی است. تغییر ETag/پاسخ ناقص باعث قطع پخش می‌شود؛ مرورگر می‌تواند درخواست تازه با احراز هویت بزند.

Angular باید Authorization را در **همه** درخواست‌های فایل و Range بفرستد و seek را با player سازگار با authenticated fetch پیاده کند. `<video src>` ساده نمی‌تواند Bearer header دلخواه اضافه کند. برای فایل کوتاه Blob حاصل از fetch احرازشده قابل استفاده است؛ برای ضبط حجیم، دانلود کامل Blob توصیه نمی‌شود و انتخاب player/codec سازگار با Range/MediaSource نیازمند کار در ریپوی فرانت است. داده گزارش/Review شامل همین URL داخلی و timeline قبلی است؛ ساخت player Angular در این ریپو انجام نشده است.

## پیشنهاد برای حجم بسیار بالا؛ هنوز پیاده نشده

آپلود مستقیم browser-to-S3 به روش Multipart مناسب‌تر است: قطعات 8–16 MiB و حدود سه درخواست موازی، retry فقط قطعه شکست‌خورده و resume با شناسه آپلود ثابت. API بعد از اعتبارسنجی دعوت/سازمان/retention، key و UploadId را خودش می‌سازد و جلسه آپلود را به همان مصاحبه، سقف حجم و انقضا متصل می‌کند. فقط URL امضاشدهٔ کوتاه‌عمر **UploadPart** برای قطعه مشخص داده می‌شود؛ کلاینت اجازه انتخاب key/bucket/UploadId شخص دیگر، GetObject یا Complete مستقیم ندارد.

Finalize باید مجوز جاری را دوباره بررسی کند، قطعات را از S3 بخواند، تعداد/اندازهٔ واقعی را با سقف و اعلام کلاینت تطبیق دهد، Complete را سرور انجام دهد، سپس HEAD/امضای فایل/metadata و timeline را اعتبارسنجی و ثبت اتمیک/idempotent کند؛ شکست به Abort/Delete منجر شود. CORS مخزن فقط origin دقیق پنل/داوطلب و روش/headerهای لازم باشد و ETag برای فرانت expose شود؛ CORS مجوز خواندن یا جایگزین خصوصی‌بودن نیست. پاک‌سازی session منقضی و orphan لازم است.

پخش حتی در این طرح از API احرازشده باقی می‌ماند. لینک امضاشدهٔ GET یک bearer capability است: دارندهٔ لینک تا انقضا می‌تواند بخواند، بنابراین با شرط کاربر «حتی با آدرس نتوانند ویدیوی همدیگر را ببینند» سازگار نیست. APIهای initiate/sign-part/finalize/resume و تغییر فرانت جزو پیاده‌سازی فعلی نیستند؛ پیشنهاد آپلود حجیم هستند.

مراجع فنی: [S3 multipart](https://docs.aws.amazon.com/AmazonS3/latest/userguide/mpuoverview.html)، [Presigned URL access](https://docs.aws.amazon.com/AmazonS3/latest/userguide/using-presigned-url.html)، [SDK رسمی .NET](https://www.nuget.org/packages/AWSSDK.S3).

## اعتبارسنجی

نتیجه نهایی در STATUS و HANDOFF ثبت می‌شود. دادهٔ اتصال آروان مصنوعی است؛ smoke شامل multipart، خواندن Range، GET بدون امضا و حذف همان فایل است. بررسی مرورگر واقعی/کیفیت codec/ضبط طولانی/بار هم‌زمان و Angular جدا از این smoke هستند.
