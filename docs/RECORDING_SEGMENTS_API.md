# ضبط بخش‌بندی‌شده و ادامه بعد از رفرش — 2026-10-12

بک‌اند روی dev؛ Angular جداست. طبق انتخاب کاربر، ویدیوهای مستقل پشت‌سرهم در پلیر نمایش داده می‌شوند؛ ادغام فایل‌ها/دانلود یک فایل واحد انجام نمی‌شود. مسیر ضبط تک‌فایلی قبلی و فایل‌های تاریخی محفوظ‌اند. یک جلسه فقط یکی از این دو روش را می‌پذیرد؛ ترکیب آن‌ها 409/recording_mode_conflict دارد.

## قرارداد داوطلب

Bearer دعوت همان جلسه برای هر درخواست الزامی است:

- `GET /api/candidate/interviews/{id}/recording/segments`: وضعیت ماندگار، `nextSegmentNumber` از ۱، `totalSizeBytes`, `totalDurationSeconds`, `segments`, `legacyRecordingUploaded`.
- `POST /api/candidate/interviews/{id}/recording/segments`: multipart با `video`, `segmentNumber`, `uploadId` (Guid ثابت برای retry همین بخش), `durationSeconds` و `timeline` (JSON string، پیش‌فرض `[]`).

هر بخش یک فایل مستقل و قابل پخش WebM/MP4 با header معتبر است، حداکثر 40 MiB. سقف بدنه HTTP برای overhead فرم 42 MiB است. کل بخش‌های جلسه حداکثر 512 MiB، ۱۴۴۰۰ ثانیه اعلامی و ۱۰۰۰ بخش هستند. شماره‌ها از ۱ و پیوسته‌اند؛ خارج ترتیب 409/recording_segment_order. فایل خام timeslice دوم MediaRecorder که header/container مستقل ندارد، یک بخش قابل پخش محسوب نمی‌شود؛ بایت‌های فایل‌های مستقل نیز با concat ساده به یک فایل معتبر تبدیل نمی‌شوند.

`timeline` آرایه `{answerId,startSeconds,endSeconds}` است؛ offsetها نسبت به ابتدای **همین بخش**، غیرمنفی، پایان بزرگ‌تر از شروع و حداکثر duration آن‌اند. answerId فقط پاسخ همان جلسه، حداکثر ۲۰۰ مورد و بدون تکرار در هر بخش است. یک پاسخ می‌تواند در بخش‌های مختلف حضور داشته باشد. این زمان‌ها اعلام کلاینت هستند؛ duration/پیوستگی واقعی codec با این API اثبات نمی‌شود.

پاسخ موفق همان `InterviewRecordingSegmentDto` با `uploadId`, `segmentNumber`, `videoUrl`, `durationSeconds`, `sizeBytes`, `timeline` است. URL خصوصی مدیر است؛ دریافت آن به داوطلب اجازه مشاهده فایل از endpoint مدیر نمی‌دهد. Replay با شماره/شناسه/نوع/حجم/duration/timeline و SHA-256 **همان بایت‌ها** پذیرفته می‌شود و storage را تکرار نمی‌کند. استفاده همان شماره یا uploadId برای داده متفاوت 409 دارد. رقابت دو درخواست زیر قفل سازمان فقط یک ثبت ایجاد می‌کند؛ فایل loser پاک می‌شود. کل فایل روی بافر حافظه جمع نمی‌شود؛ hash از stream فایل فرم خوانده می‌شود.

## جریان پیشنهادی فرانت

۱. پس از start، وضعیت بخش‌ها را بگیرید؛ اگر `legacyRecordingUploaded=true` بود مسیر بخش‌بندی را شروع نکنید. ضبط دوربین جدید فقط با رضایت و اجازه دستگاه از همان مرحله جاری جلسه ادامه یابد؛ مصاحبه از نو start/ساخته نشود.

۲. به‌صورت دوره‌ای فایل مستقل را ببندید و ارسال کنید؛ برای فایل مستقل از stop/start یک MediaRecorder با تنظیمات codec ثابت استفاده کنید، یا راهکار فرانت که استقلال container هر فایل را تضمین کند. timeslice به‌تنهایی این تضمین نیست. `uploadId`، Blob و metadata بخش تأییدنشده را در IndexedDB تا پاسخ موفق نگه دارید؛ token دعوت در URL یا ذخیره فایل قرار نگیرد.

۳. پس از refresh دوباره manifest سرور را بخوانید. بخش‌های ثبت‌شده را دوباره نسازید. درخواست گم‌شده را با همان Blob/uploadId/number بازپخش کنید؛ پس از تأیید، داده pending محلی را حذف و شماره بعدی را از سرور هماهنگ کنید. چند tab/رکوردکننده نباید هم‌زمان بخش‌های متفاوت با یک شماره تولید کنند؛ در conflict وضعیت را تازه بگیرید.

۴. بعد از پایان ضبط، بخش آخر را ارسال کنید. آپلود از شروع جلسه تا زمان معتبر دعوت و حداکثر ۱۰ دقیقه بعد از completion مجاز است؛ پس از حذف/لغو/دعوت مجدد/انقضا یا retention نامعتبر رد می‌شود. API جلسه یا زمان مصاحبه را بازنشانی نمی‌کند. قطع برق/رفرش قبل از persist شدن Blob یا ارسال موفق، بخش ذخیره‌نشده را بازیابی نمی‌کند. رضایت/شروع/توقف ضبط را همچنان با endpoint events ثبت کنید.

## پلیر مدیر و CORS

`GET /api/interviews/{id}/recording/segments` manifest مرتب با همان totals است. `GET /api/interviews/{id}/review` نیز `recordingSegments` و `recordingStatus=uploaded_segments_unverified` دارد؛ این وضعیت کامل‌بودن/سلامت واقعی ضبط را ثابت نمی‌کند. ضبط قدیمی همچنان در `recording` و وضعیت قبلی می‌آید.

هر `videoUrl` به `GET /api/interviews/{id}/recording/segments/{segmentNumber}/video` اشاره دارد؛ Owner معتبر همان سازمان و retention الزامی‌اند. Range/206/no-store و ETag قوی SHA-256 پشتیبانی می‌شوند. برای دانلود **یک بخش** `?download=true` اضافه کنید؛ بدون آن Content-Disposition=inline است. پلیر بخش‌ها را بر اساس segmentNumber پشت‌سرهم پخش کند؛ offset کلی نمایشی برابر جمع duration بخش‌های قبل + offset محلی است. فاصله واقعی بین توقف و شروع دوباره ضبط با جمع duration حذف می‌شود؛ فرانت آن را به‌عنوان ویدیوی بی‌وقفه واقعی معرفی نکند.

Originهای صریح تنظیم‌شده API می‌توانند Content-Range، Accept-Ranges، ETag و Content-Disposition را از Access-Control-Expose-Headers بخوانند. preflight شامل Authorization/Range/If-Range مجاز است؛ origin ناشناس Allow-Origin ندارد. نبود expose در درخواست بدون Origin، به‌تنهایی آزمون CORS مرورگر نیست. هدر CORS جای توکن/مجوز نیست و 401 هم فایل را قابل خواندن نمی‌کند.

ETag فایل‌های S3 قدیمی از HEAD همان object است؛ فایل محلی تاریخی tag ضعیفِ هویت immutable رسانه دارد و به معنی hash بایت‌ها/پشتیبانی If-Range قوی نیست. فایل‌های بخش‌بندی جدید hash واقعی دارند. فایل‌های تک‌فایلی/معرفی/پاسخ نیز download=true و Content-Disposition را پشتیبانی می‌کنند. URL عمومی یا GET امضاشده S3 اضافه نشده است.

## دیتابیس و انتشار

Migration `20261012193519_AddInterviewRecordingSegments` فقط جدول جدید با PK شماره در جلسه، unique uploadId در جلسه، FK مرکب سازمان/جلسه و محدودیت‌های هر بخش را اضافه می‌کند؛ داده قدیمی تبدیل/جابجا نمی‌شود. Down در حضور بخش ضبط‌شده صریحاً رد می‌شود. قبل از انتشار API، migration مقصد را طبق دستورهای ریپو اعمال کنید. جزئیات عملیاتی S3/proxy/temp در [INTERVIEW_VIDEO_STORAGE](INTERVIEW_VIDEO_STORAGE.md) است؛ مقصد در این درخواست deploy نشده است.

مرجع رفتار expose در مرورگر: [MDN Access-Control-Expose-Headers](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Access-Control-Expose-Headers).
