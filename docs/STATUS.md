## اصلاح مسیر به متوسط و یک شاخه — 2026-10-10

طبق تصحیح صریح کاربر، دعوت موضوعی تازه فقط دو سؤال اصلی در هر موضوع دارد: متوسط، سپس سخت اگر درستی >=۰٫۷ و confidence >=۰٫۶؛ در غیر این صورت آسان. پاسخ سؤال دوم مستقل از نمره به موضوع تازه از متوسط می‌رود. بانک سه سؤال دارد ولی سقف جلسه/قالب/پیشنهاد ۲×موضوع (۱۰..۲۰) است. clarify/followUp جزو سؤال اصلی نیستند. `MediumFirstTopicQuestions` و prompt `v4-medium-first` سیاست جدید را snapshot می‌کنند؛ جلسه قدیمی و resend آن تغییر مسیر نمی‌دهند. ADR-033 و [ADVANCED_QUESTIONS_API](ADVANCED_QUESTIONS_API.md) قرارداد جاری‌اند؛ بخش قبلی زیر، نتیجه نسخه پیشین است.

اعتبارسنجی واقعی نهایی: build صفر warning/error، **۲۶۸/۲۶۸ تست موفق، صفر شکست/Skip** روی PostgreSQL 18 اختصاصی loopback:55481 و AI مصنوعی؛ `tests/HireYar.Tests/TestResults/medium-first-verified.trx`. هر دو نوع عمومی/تخصصی، دو شاخه، unknown/low confidence، تعامل، منع سؤال سوم حتی پس از پاسخ صحیح آسان، persisted grade و resend تأیید شدند. migration جدید `20261010085424_AddMediumFirstTopicQuestions`؛ drift=false، Down دارای داده رد و رفت‌وبرگشت schema خالی موفق، script idempotent کامل دوبار و یک history entry. DBهای fixture صفر باقی؛ scratch پاک و cluster متوقف شد. فقط خطای analyzer تست Assert.Single پیش از اجرا اصلاح شد. مقصد/AI واقعی/Angular/push/deploy این نوبت انجام نشده‌اند.

## پیشروی آسان تا سخت در همان موضوع — 2026-10-10 (نسخه قبلی)

درخواست تازه کاربر روی dev از Git پاک پیاده شد: دعوت موضوعی تازهٔ عمومی/تخصصی از آسان آغاز و با درستی >=۰٫۷ و اطمینان >=۰٫۶ به متوسط و سخت همان موضوع می‌رود. پاسخ ضعیف/unknown، سپس موضوع تازه؛ grade پاسخ نهایی مستقل از امتیاز مهارت ذخیره می‌شود. سرور و Domain پرش سطح/بازگشت به موضوع بسته را منع می‌کنند؛ پوشش و sufficient evidence سؤال سخت را حذف نمی‌کنند. دعوت قبلی و resend آن سیاست قدیمی را حفظ می‌کنند. سقف API قالب/پیشنهاد و جلسه ۳×موضوع (۱۵..۳۰) است؛ زمان و billing قبلی حفظ شدند. [ADVANCED_QUESTIONS_API](ADVANCED_QUESTIONS_API.md)، ADR-032.

اعتبارسنجی نهایی: build صفر warning/error؛ **۲۶۴/۲۶۴ تست موفق، صفر Failed/Skipped** با PostgreSQL 18 اختصاصی loopback:55481 و provider مصنوعی. TRX: `tests/HireYar.Tests/TestResults/topic-ladder-verified.trx`. تست‌های تازه مسیر کامل هر دو نوع، pass/fail/low confidence/unknown، تعامل، ممنوعیت پرش AI، persisted grade، resend، عدم پایان با evidence، رفتار legacy، SQL checkها، محافظ Down دارای داده و رفت‌وبرگشت schema خالی را پوشش می‌دهند. HasPendingModelChanges=false؛ script idempotent کامل دو بار روی scratch تصادفی، یک history entry؛ DB fixture صفر باقی، scratch حذف و cluster متوقف شد.

شکست‌های میانی نتیجه نهایی نبودند: InlineData عدد صحیح برای double nullable اصلاح شد؛ داده تست پیشنهاد موضوع تازه به‌جای hard فعلی شد؛ تست محافظ migration قبلی به fixture مستقل رفت تا محافظ جدید مانع رسیدن به محافظ قبلی نشود. در اجرای دوم، override ConnectionStrings__Default مخصوص EF tooling به process تست نشت کرده بود و حذف DBها را مختل کرد؛ override پیش از اجرای نهایی حذف و cluster تازه ساخته شد. نتیجه بالا پس از این اصلاح‌هاست. تنظیمات/secret مقصد تغییر نکردند. کیفیت واقعی تشخیص درست/غلط با مدل فارسی هنوز آزموده نشده؛ migration مقصد/فرانت Angular/انتشار انجام نشده‌اند.

## تکمیل بک‌اند گزارش HTML — 2026-10-10

اعتبارسنجی نهایی: tool restore و restore موفق؛ build صفر warning/error؛ **۲۵۰/۲۵۰ تست suite کامل موفق، صفر شکست/Skip** روی PostgreSQL 18 اختصاصی loopback:55481 و provider مصنوعی. TRX: `tests/HireYar.Tests/TestResults/report-v3-verified.trx`. هفت تست تازه metrics/citation/unknown/پرینت امن/وزن‌ها، review/تصمیم/replay/tenant FK، recording/offset/replay/media scope، regenerate/failure/رقابت/cached و محافظ واقعی downgrade را پوشش می‌دهند؛ EF HasPendingModelChanges=false است. SQL idempotent کامل دو بار روی scratch تصادفی موفق و migration جدید دقیقاً یک history entry داشت؛ scratch/DBهای fixture حذف و cluster متوقف شد.

اجرای‌های میانی نهایی محسوب نمی‌شوند: متن summary فارسی OpenAPI و assertion هدر چاپ اصلاح شدند؛ تست وزن جزئی مقدار پیش‌فرض صفر را کشف کرد که به ۱ اصلاح شد؛ ترتیب کلیدهای JSONB در Map ثابت شد تا پاسخ اولیه/cached یکسان باشند. تست regenerate اکنون موضوع واقعاً پرسیده‌شده را به‌جای فرض topic-0 انتخاب می‌کند. تست قدیمی resend با Order ذخیره‌شده تطبیق می‌دهد، چون ترتیب materialization navigation EF تضمین ندارد. نتیجه ۲۵۰ موفق پس از تمام این اصلاح‌هاست.

روی dev با حفظ تغییرات موجود کاربر در STATUS/BACKLOG/HANDOFF، HTML ارائه‌شده با کد تطبیق و report-v3 پیاده شد. هفت شاخص عمومی و سه بعد تخصصی با citation پاسخ نهایی یا null، fit مبتنی بر job_alignment، میانگین مدت اعلامی پاسخ و وزن/آستانه انتخاب صریح Owner در regenerate موجودند. یک تماس مدل، JSONB و providerهای موجود حفظ شدند؛ مدل/secret/config/نمرات زنده/صورتحساب تغییر نکردند. POST cached v1/v2 را بازنویسی نمی‌کند؛ regenerate نتیجه قبلی را تا commit موفق حفظ می‌کند.

پنج کسری بک‌اند بررسی قبلی اکنون مسیر عملی دارند: metrics چندبعدی/شاخص‌های HR و وزن‌ها؛ InterviewReviewEntries برای رضایت/رویداد و تصمیم انسانی با replay/caps/FK tenant؛ InterviewSessionRecordings برای آپلود خصوصی فایل جلسه و offsetهای recorder نسبت به فایل. داده ناموجود unknown است؛ وجود فایل یا نبود رویداد سلامت کامل ضبط را اثبات نمی‌کند. تصمیم advance فقط ثبت تصمیم است و دعوت واقعی ارسال نمی‌کند. برای آپلود پس از تکمیل خودکار، فقط POST events/recording تا ده دقیقه با دعوت همچنان معتبر مجازند؛ start/current/answer دوباره باز نمی‌شوند.

نسخه چاپی HTML فارسی با encode/CSP/no-store و print CSS برای Save as PDF مرورگر اضافه شد؛ server-side PDF/storage اضافه نشد. اشتراک ناشناسِ توکن‌دار توسط automatic approval review به دلیل داده حساس و نبود مجوز صریح خروج/مخاطب رد شد؛ هیچ کد/جدول/لینک آن اعمال نشد. Angular در این ریپو نیست؛ اتصال UI/recorder/چاپ باید طبق [INTERVIEW_REPORT_V3_API](INTERVIEW_REPORT_V3_API.md) در ریپوی فرانت انجام شود. AI واقعی/کیفیت فارسی/ضبط واقعی/browser مقصد هنوز تأیید نشده‌اند؛ push/deploy/production یا DB مقصد تغییر نکردند.

## بررسی کسری‌های evaluation-report پس از pull — 2026-10-10

بررسی اولیه روی dev قدیمی b2e9b87 انجام شد. کاربر سپس نسخه جدید را pull کرد؛ بازبینی روی HEAD=9947b6a شامل commit گزارش 7b0c0e3 انجام شد. نتیجه اولیه نبود endpoint با این نسخه جایگزین است: GET/POST /api/interviews/{id}/evaluation-report در InterviewsController موجودند. تغییرات pull کاربر حفظ شدند.

قرارداد report-v2، DTO، builder، prompt، validator و سرویس با فهرست فرانت تطبیق داده شدند. داده موجود: نمره ۰–۵ هر پاسخ نهایی، confidence، نقل‌قول/feedback/highlights/gaps، میانگین کل و موضوع در مقیاس ۱۰۰، توزیع، بررسی انسانی، nextRoundFocus، askedAtUtc و رسانه مستقل هر پاسخ. پنج کسری تأییدشده:

| درخواست فرانت | وضعیت نسخه جدید |
| --- | --- |
| عمق دانش، ارتباط، تطابق JD و وزن‌ها | QuestionEvaluations فقط یک Score دارد؛ OverallScore و Topic.Score میانگین ساده‌اند. سه بعد مستقل و وزن ارزیابی persist نمی‌شوند. JD ورودی مدل است ولی fit score مستقل نیست؛ InterviewSkill.Priority اولویت سؤال است. |
| هفت شاخص ثابت HR | عمومی topics واقعی سؤال‌های پرسیده‌شده را نشان می‌دهد؛ مدل/قرارداد ثابت هفت‌گانه ندارد. نام، تعریف، شواهد لازم، مقیاس و وزن‌ها در ریپو نیست؛ HTML مرجع در Git ذخیره نشده است. |
| کامل‌بودن ضبط، رضایت و قطعی | رویداد consent/disconnect و معیار کامل‌بودن ضبط وجود ندارد؛ وضعیت Completed، processing failure و وجود فایل اثبات این موارد نیستند. قرارداد دریافت رویداد از فرانت لازم است؛ تاریخچه فاقد شاهد unknown باقی بماند. |
| ویدیوی پیوسته و offset پاسخ‌ها | AudioUrl/VideoUrl مستقل هر پاسخ و askedAtUtc موجودند؛ فایل جلسه پیوسته و زمان‌بندی نسبت به رسانه وجود ندارند. durationSeconds/lastSpeechEndUtc metadata هستند، offset ویدیوی پیوسته نیستند. نیازمند ضبط/upload و قرارداد timeline با فرانت است. |
| ثبت تصمیم انسانی | nextRoundFocus پیشنهاد متنی مدل است؛ requiresHumanReview پرچم بررسی است. API رد/مرحله بعد و سابقه تصمیم انسانی وجود ندارد؛ cancel/revoke ابطال دعوت‌اند. |

ADR-030 و INTERVIEW_EVALUATION_API همین محدودیت‌ها را صریح ثبت کرده‌اند؛ نقص فهرست فرانت واقعی است، نه صرفاً فیلد با نام دیگر. کد/schema تغییر نکرد؛ برای پیاده‌سازی مطابق طرح، تعریف هفت شاخص و وزن‌ها/مقیاس از کاربر درخواست شد. سیاست بازتولید گزارش cached v1/v2 نیز هنگام گسترش قرارداد باید تعیین شود؛ POST فعلی cached را بازنویسی نمی‌کند.

اعتبارسنجی نسخه جدید: build موفق، صفر warning/error؛ InterviewHistoryAndReportTests و APIهای Evaluation_report: 9 موفق، صفر شکست، 3 Skip به علت نبود HIREYAR_TEST_POSTGRES. TRX: tests/HireYar.Tests/TestResults/evaluation-report-review-current.trx. نتیجه 93 تست بررسی اولیه متعلق به checkout قبل pull است. PostgreSQL، AI و مرورگر واقعی این نوبت تأیید نشده‌اند؛ فقط STATUS/BACKLOG/HANDOFF تغییر کردند؛ push/deploy/production تغییر نکردند.

## گزارش سؤال‌محور مطابق HTML — 2026-10-09

روی dev با Git شروع پاک، report-v2 به مسیر موجود اضافه شد: ارزیابی ۰–۵ هر پاسخ نهایی، confidence/feedback/quote/highlights/gaps و nextRoundFocus؛ جزئیات پرسیده‌شده، موضوع/سطح واقعی، نمره کل/توزیع/پوشش و رسانه احرازشده را سرور محاسبه می‌کند. یک تماس مدل، JSONB/claim موجود، بدون migration/جدول/worker تازه. v1 cached محفوظ، نمرات زنده/مالی دست‌نخورده. عمومی بدون رزومه/معرفی؛ سؤال بی‌پاسخ نمره null؛ وزن JD/fit score/شاخص HR حدسی/تصمیم استخدام/رویداد دستگاه ساخته نمی‌شوند. [INTERVIEW_EVALUATION_API](INTERVIEW_EVALUATION_API.md) قرارداد جاری است.

build نهایی صفر warning/error؛ ۱۴ تست مرتبط موفق، صفر شکست/Skip، روی PostgreSQL 18.3 محلی loopback:55469 با provider مصنوعی؛ report-v2-verified.trx. اجرای اولیه integration به علت قطع سرور آزمایشی connection refused داشت؛ پس از اجرای پایدار سرور و تست در یک فرمان، همه موفق شدند و cluster متوقف شد. آزمون واقعی مدل/کیفیت فارسی و Angular/PDF/اشتراک انجام نشده‌اند؛ production/push/deploy/secret/config تغییری نکردند.

## بررسی 503 گزارش ارزیابی — 2026-10-08

بررسی خواندنی روی dev پاک: appsettings مشترک AI:Provider=Liara، EvaluationModel=openai/gpt-6-luna و EvaluationTimeoutSeconds=90؛ مدل زنده فعلی google/gemini-2.5-flash-lite است و گزارش مستقل است. override محلی Development موجود Provider=NetArz دارد؛ overrideهای مقصد/محیط سرور بررسی نشده‌اند. GET models اتصال مشترک Liara پاسخ 200 و حضور هر دو مدل را تأیید کرد؛ مستند رسمی https://docs.liara.ir/ai/openai/ نیز مدل گزارش را فهرست می‌کند. GET models تنظیمات مؤثر توسعه NetArz پاسخ 404 داشت؛ این نتیجه موفقیت/شکست chat/completions را اثبات نمی‌کند.

یک POST آزمایشی واقعی فقط به Liara با همان system prompt report-v1 و context کاملاً مصنوعی بدون پاسخ/رزومه/داده واقعی انجام شد: HTTP 200، حدود ۹٫۳ ثانیه، content موجود، JSON قابل parse، سه فیلد مورد انتظار، findings=0 و limitations=1، زیر سقف اندازه. اعتبارسنجی کامل .NET و گزارش واقعی مقصد از این smoke نتیجه نمی‌شود. مقدار کلید، BaseUrl شناسه‌دار، متن پاسخ/پرامپت و داده کاربر چاپ یا persist نشدند. کد/تنظیمات/secretها تغییر نکردند.

evaluation_report_unavailable از تبدیل همه InterviewProviderExceptionها می‌آید: HTTP upstream/timeout/network، envelope/content نامعتبر، یا validator schema/citation/quote. پیام 503 به‌تنهایی علت دقیق را ندارد؛ provider HTTP success هم قبولی validator را اثبات نمی‌کند. لاگ operation=report همان درخواست از کاربر درخواست شده و هنوز در دست نیست؛ علت خطای مقصد مشخص نشده و تغییری به مدل بر مبنای حدس انجام نشد. build صفر warning/error و ۴۷ تست مستقل provider/history/report موفق، صفر Skip؛ evaluation-report-diagnosis.trx. SQL/گزارش واقعی مقصد/تغییر production/push/deploy انجام نشده‌اند.

## لاگ زنده آزمایشی مصاحبه — 2026-10-08

روی dev از Git پاک، SSE موقت برای مراحل/API/دستورهای EF PostgreSQL/تماس واقعی STT-LLM-TTS و cache/retry/fallback اضافه شد. InterviewDiagnostics:Enabled و CapturePrompts در appsettings برای آزمایش true هستند؛ config در startup خوانده می‌شود. DTO داوطلب و جزئیات HR diagnosticsEnabled/diagnosticsUrl دارند؛ HR diagnosticPromptsEnabled هم دارد. Disabled جمع‌آوری و endpoint را خاموش و نمایش DTO را false می‌کند. سایر تنظیمات و کلیدهای موجود با مقایسه JSON بدون نمایش مقدار عیناً محفوظ تأیید شدند.

بدنه واقعی LLM شامل system/context از همان JsonContent ارسالی در حافظه محدود و گذرا capture می‌شود؛ فقط stream Owner همان سازمان آن را می‌دهد. stream candidate حتی با query اضافی payload نمی‌دهد. SQL/parameters/DB values/header/token/raw response ثبت نمی‌شوند؛ فقط identifierهای schema مدل، route template، مسیر نسبی provider، نام مدل/هدف/وضعیت/مدت و کد خطای ثابت. recovery worker هم context مستقل دارد؛ stream خودش و recheck دسترسی trace نمی‌شوند. [INTERVIEW_DIAGNOSTICS_API](INTERVIEW_DIAGNOSTICS_API.md) قرارداد نهایی فرانت و محدودیت‌هاست.

build نهایی صفر warning/error؛ suite کامل **۲۲۷ موفق، صفر شکست/Skip** روی PostgreSQL 18.3 اختصاصی loopback:55469 و AI مصنوعی؛ tests/HireYar.Tests/TestResults/diagnostics-verified.trx. پس از اصلاح نهایی اعلان reset برای reconnect بعد از انقضای buffer، build و **۹ تست مرتبط موفق، صفر شکست/Skip** با همان SQL واقعی بازاجرا شدند؛ diagnostics-final.trx. پنج تست تازه (سه unit/provider و دو integration) disabled/scoped/redacted/exact-wire-before-completion/retention/replay/AsyncLocal/ظرفیت و SSE زنده/ابطال حین اتصال را بررسی می‌کنند. تعداد ۹ نهایی شامل caseهای تشخیص قبلی نیز هست. fixtureهای قبلی diagnostics را صریح false دارند. DBهای تصادفی صفر باقی ماندند و cluster اختصاصی متوقف شد.

Angular/AI واقعی/proxy مقصد/multi-replica routing بررسی یا تغییر نشده‌اند. حافظه ده دقیقه‌ای، ۲۵۶ رویداد/۱۲۸ جلسه، بودجه تقریبی ۶۴ MiB، prompt حداکثر ۵۲۴۲۸۸ کاراکتر با علامت truncation، چهار stream هر جلسه/۶۴ کل و اتصال ۳۰ثانیه‌ای قابل reconnect دارد. این لاگ مشاهده pipeline HTTP نوبتی است و provider مصاحبه Realtime نیست. migration/commit/push/deploy/production انجام نشده‌اند.

## تست میکروفون ویزارد داوطلب — 2026-10-08

روی dev از Git پاک، POST `/api/candidate/interviews/{id}/audio-check` با فرم audio و durationSeconds اختیاری اضافه شد. دسترسی دعوت/Created، اندازه/امضای فایل، طول کامل upload و recheck دسترسی پس از STT دارد؛ خروجی transcript/audioReceived/receivedBytes/speechRecognized/clarityVerified/message و OpenAPI صریح عرضه می‌شود. تست هیچ پاسخ، معرفی، زمان یا داده storage/DB ایجاد نمی‌کند. [INTERVIEW_AUDIO_CHECK_API](INTERVIEW_AUDIO_CHECK_API.md) قرارداد است؛ کیفیت صوت واقعی و Angular هنوز بررسی/پیاده نشده‌اند.

اعتبارسنجی نهایی: build صفر warning/error و **۲۲۲ تست موفق، صفر شکست/Skip** روی PostgreSQL 18.3 اختصاصی loopback:55467، provider جعلی؛ TRX در tests/HireYar.Tests/TestResults/audio-check-verified.trx. دو تست تازه scoped/transient/failure/Created و ابطال دسترسی حین STT را بررسی کردند. سرور آزمایشی پس از suite متوقف شد. یک اجرای قبلی suite به علت قطع سرور آزمایشی شکست خورد؛ اجرای پایدار بعدی یک assertion شمارنده تجمعی fixture داشت که به delta اصلاح و suite کامل بازاجرا شد. کد provider و تنظیمات/کلیدها تغییر نکردند؛ migration/push/deploy انجام نشد. صوت واقعی/provider و Angular تأیید نشده‌اند.

## آخرین تکمیل: دعوت شش‌رقمی و تشخیص توقف پاسخ صوتی — 2026-10-08

روی dev: کد شش‌رقمی با ذخیره رمز‌شده و قفل پنج تلاش، مشاهده لینک/کد در پنل، دعوت مجدد با جلسه تازه و حفظ تاریخچه، انقضای لینک Completed/جایگزین‌شده با پیام فارسی و status عمومی پیاده شد. مدل گزارش مستقل قبلی حفظ شد. STT صریح JSON و shape warning امن دارد؛ DTO خطای واقعی/وضعیت retry را عرضه می‌کند و رزرو بسته دیگر spinner/recovery پاسخ سؤال را فعال نگه نمی‌دارد. [INTERVIEW_INVITATIONS_API](INTERVIEW_INVITATIONS_API.md) قرارداد فرانت است.

build صفر warning/error؛ **۲۲۰ تست موفق، صفر Skip** با HIREYAR_TEST_POSTGRES روی cluster محلی اختصاصی PostgreSQL 18.3. تست‌های جدید نمایش کد/scoping/cipher، قفل، Completed/closing، concurrent resend، legacy، shape diagnostics و رزرو بسته را پوشش می‌دهند. migration `20261008135241_AddShortInterviewInvitations` و delta idempotent از AddInterviewEvaluationReports روی baseline خالی دوبار موفق، history count=1؛ model drift در تست بررسی شد. downgrade با داده واقعی اجرا نشده است. لاگ سرور HTTP 200 سپس stt_invalid_response را نشان می‌دهد؛ شکل/خالی بودن پاسخ و علت worker روی سرور قطعی نیست. Angular، provider واقعی، migration مقصد، commit/push/deploy انجام نشده‌اند.

## آخرین تکمیل: مدل گزارش مستقل و تاریخچه منتخب — 2026-10-08

روی `dev` پاک، `EvaluationModel=openai/gpt-6-luna` و timeout مستقل به هر دو provider افزوده شدند؛ مدل زنده/کلیدها/URL/STT/TTS و تمام مقادیر قبلی appsettings با مقایسه بدون نمایش secret حفظ شدند. POST/GET evaluation-report برای Owner همان سازمان، Completed و قابلیت قرارداد `interview_report`، با پرامپت مستقل، کل تاریخچه پردازش‌شده، citation واقعی پاسخ نهایی و quote دقیق، و گزارش cached در JSONB پیاده‌اند. claim/lease با قفل کوتاه سازمان، شبکه بیرون transaction، جلوگیری از تماس تکراری و commit generator کهنه/جلسه حذف‌شده دارند؛ گزارش score یا مالی جلسه را تغییر نمی‌دهد. تولید به درخواست پنل است و پاسخ زنده منتظر آن نمی‌ماند. [INTERVIEW_EVALUATION_API](INTERVIEW_EVALUATION_API.md) قرارداد اتصال فرانت است.

تاریخچه از قبل در دیتابیس موجود بود؛ selector فقط payload زنده را محدود می‌کند: تمام تعامل‌های سؤال جاری و گروه‌های اخیر، گروه‌های مرتبط قدیمی در بودجه نرم، و تاریخچه کامل برای اشاره صریح به گذشته/اصلاح یا با خاموش‌کردن گزینه. JD/رزومه/معرفی، وضعیت مهارتی/پوشش و تمام evidence تجمعی حفظ می‌شوند. بانک prompt به current+shortlist مجاز محدود است؛ snapshot کامل و endpoint تاریخچه تغییر حذف ندارند. metric شمارش تاریخچه و مدت/شکست مستقل گزارش اضافه شدند؛ خلاصه‌ساز LLM، embedding، Repository و Realtime جدید نداریم.

tool restore و restore موفق؛ build نهایی صفر warning/error؛ **۲۰۹/۲۰۹ تست PostgreSQL 18.3 اختصاصی محلی موفق، صفر شکست/Skip**، شامل ۶ case تازه history/report rules/wire و ۵ تست واقعی report/cache/scope/failure/retry/رقابت/حذف/reclaim/constraint tenant و metadata. تست قبلی ویزارد اکنون بانک منتخب را در prompt و حفظ همه پنج سؤال/شش نوبت در DB بررسی می‌کند. timestamp گزارش پیش از پاسخ اول به microsecond PostgreSQL normalize شده تا GET/POST cached دقیقاً زمان یکسان بدهند. TRX: `tests/HireYar.Tests/TestResults/history-report-verified.trx`؛ EF drift در تست واقعی false است. اجرای اولیه sandbox به testhost/loopback وصل نشد، و یک run پیش از فعال‌بودن cluster SQL failed شد؛ نتیجه نهایی پس از اجرای خارج sandbox جایگزین آن نتایج محیطی است.

migration `20261008120416_AddInterviewEvaluationReports` فقط جدول گزارش را اضافه می‌کند؛ delta idempotent `artifacts/evaluation-report-migrations.sql` دو بار روی baseline اختصاصی آخرین migration قبلی موفق و یک history entry دارد. baseline این بررسی بدون داده legacy بود؛ migration هیچ UPDATE/backfill تاریخچه ندارد. downgrade با گزارش موجود در کد migration رد می‌شود؛ اجرای downgrade با گزارش واقعی در این نوبت تست نشده است. DBهای fixture صفر باقی ماندند، scratch حذف و cluster اختصاصی متوقف شد.

AI واقعی، کیفیت فارسی/latency/تعداد توکن واقعی مدل منتخب، Angular، migration مقصد، commit/push/deploy و production انجام/تأیید نشده‌اند. تست مصنوعی کاهش بیش از نصف payload **بخش تاریخچه** را در نمونه بلند بررسی می‌کند، نه صرفه‌جویی تضمینی کل prompt/هزینه. حفظ محافظ‌های context معادل اثبات برابری کیفیت واقعی نیست؛ مترادف/ارجاع ضمنی نیاز به آزمون مقایسه‌ای دارد.

## تکمیل قبلی: معرفی صوتی و صوت مرحله‌ها — 2026-10-07

معرفی کارجو اکنون upload/replay/retry/recovery مستقل صوتی دارد و پیام‌های ثابت stageAudioUrl احرازشده دارند. 198/198 تست PostgreSQL واقعی و build موفق؛ migration جدید 20261007151257_AddVoiceIntroduction در مقصد لازم است. [VOICE_INTRODUCTION_API](VOICE_INTRODUCTION_API.md) مرجع اتصال فرانت و محدودیت‌های باقی‌مانده است؛ جزئیات نتیجه اجرای نهایی در انتهای این فایل ثبت‌اند. cluster موقت تست متوقف و DBهای تصادفی fixture و scratch پاک شدند. Angular/AI واقعی و rollout مقصد هنوز تأیید نشده‌اند.

## معرفی‌ها، context AI و بازیابی خودکار — 2026-10-07: Verified محلی

روی dev پاک شروع شد: معرفی کارجو با متن پیش‌فرض درخواست‌شده و معرفی شرکت با فعال‌سازی مستقل، DTO/validation/کپی قالب/snapshot و مرحله persistشده اضافه شدند. مستر پرامپت v2 در دعوت ذخیره و هنگام Questions به LLM عرضه می‌شود؛ JD، رزومه متنی، زبان/نوع، بانک snapshot و shortlist مجاز، وضعیت/زمان و سابقه پاسخ/تعامل context واقعی‌اند. عمومی ترتیب را حفظ و از sufficient evidence زود تمام نمی‌شود؛ clarification/follow-up/redirect همان سؤال را با answer مستقل نگه می‌دارند و فقط پاسخ نهایی نمره دارد. [INTERVIEW_CONTEXT_API](INTERVIEW_CONTEXT_API.md) قرارداد فرانت است.

PDF متنی/DOCX محلی استخراج می‌شوند؛ DOC قدیمی/PDF اسکن‌شده یا غیرقابل استخراج متن صریح resumeText لازم دارند و دعوت بدون context رزومه معتبر 400 می‌دهد. مسیر legacy بدون فایل برای سازگاری resume=null دارد؛ backfill تاریخی یا OCR انجام نشده. معرفی کارجو در stage/advance متن دریافت می‌کند؛ STT/آپلود صوت اختصاصی معرفی اضافه نشده است. پیام شرکت پیش از پایان Questions به داوطلب عرضه نمی‌شود.

worker داخلی با state PostgreSQL و قفل/lease/RowVersion، پاسخ پذیرفته‌شده پس از قطع درخواست/restart را بازیابی می‌کند: سه تلاش پیش‌فرض با backoff persistشده؛ STT آماده تکرار نمی‌شود. STT پس از سقف تلاش Failed و retry دستی می‌ماند؛ LLM پس از سقف با fallback بدون شواهد ساختگی ادامه می‌یابد. polling پانزده ثانیه و گزینه خاموش‌کردن دارد. پاسخ بی‌پاسخ کارجو خودکار ساخته نمی‌شود. Network خارج transaction، scope/ابطال/expiry/billing بررسی و سقف مالی حفظ‌اند.

tool restore و restore منبع رسمی NuGet موفق (feed خصوصی 401 داد)، build موفق؛ **۱۹۲/۱۹۲ تست PostgreSQL 18 محلی اختصاصی موفق، صفر شکست/Skip**؛ ۹ تست تازه context/rules/extraction/provider wire/معرفی و interaction/worker واقعی/retry bounded/رقابت claim. tests/HireYar.Tests/TestResults/interview-context-final.trx. EF بدون drift از آزمون PostgreSQL؛ migration `20261007105803_AddInterviewContextAndRecovery` و script idempotent در artifacts/interview-context-migrations.sql دو بار روی baseline قبلی با داده legacy مصنوعی موفق، transcript/مالی/مرحله نهایی محفوظ و constraintهای معرفی/تعامل نامعتبر رد شدند. downgrade در حضور تعامل ثبت‌شده تاریخچه را حذف نمی‌کند و صریحاً رد می‌شود.

Angular، AI واقعی، migration مقصد، commit/push/deploy و تنظیمات/کلیدهای موجود تغییر نکردند. اتصال واقعی provider از تست mocked نتیجه‌گیری نشده است. جزئیات rollout و محدودیت‌های ورودی در قرارداد ثبت شده‌اند؛ نتیجه حاضر جایگزین یادداشت تاریخی «رزومه به AI نمی‌رسد / retry فقط کلاینت» برای دعوت جدید ویزارد است.

## انتخاب پیاده‌سازی مستقل providerهای AI — 2026-10-07

روی dev، DI براساس AI:Provider یکی از NetArzClient، LiaraClient یا DisabledInterviewProvider را برای STT/LLM/TTS/پیشنهاد سؤال انتخاب می‌کند؛ مقدار ناشناخته در startup رد می‌شود. NetArzOptions و LiaraOptions مستقل‌اند، فقط تنظیمات provider انتخاب‌شده اعتبارسنجی می‌شود و lease از timeoutهای همان provider محاسبه می‌شود. Disabled هیچ HttpClient یا کلید provider نمی‌خواهد. جزئیات مشترک قرارداد HTTP در OpenAiCompatibleInterviewClient داخل Infrastructure باقی‌اند؛ نام provider در نتیجه/metrics/log از کلاس انتخاب‌شده می‌آید و property قدیمی NetArzOptions.Provider authoritative نیست.

تمام appsettingsها و مقادیر کلید موجود دست‌نخورده‌اند؛ انتخاب پیش‌فرض هنوز NetArz است. برای انتخاب Liara مقدار AI:Provider=Liara یا override محیطی AI__Provider=Liara لازم است. آدرس و مدل STT تنظیم‌شده طبق بررسی قبلی همچنان نیاز به تطبیق کنسول/تأیید واقعی دارند؛ تغییر حدسی انجام نشد. افزودن provider دیگری نیازمند options، client و شاخه ثبت DI مستقل است؛ انتخاب نام دلخواه پشتیبانی خودکار ایجاد نمی‌کند.

build صفر warning/error؛ 88/88 تست مرتبط provider/audio/selection/rules موفق، صفر شکست/Skip؛ tests/HireYar.Tests/TestResults/provider-selection.trx. هشت case تازه انتخاب چهار interface، عدم دخالت تنظیمات provider دیگر، کلید مستقل، lease، نام ناشناخته، Disabled و درخواست‌های مصنوعی Liara با آدرس/کلید/مدل/نتیجه مخصوص را بررسی می‌کنند. تماس واقعی AI، PostgreSQL/migration، commit/push/deploy انجام نشد؛ schema و قرارداد Application/API تغییر نکرده‌اند. تغییرات مستندی نوبت قبلی حفظ شدند.

## بررسی قابلیت استفاده از Liara — 2026-10-07

بررسی روی dev پاک انجام شد؛ کد runtime و تمام appsettingsها/توکن‌ها دست‌نخورده‌اند. AI:Provider فعلی NetArz است؛ AddInterviews فقط NetArz/Disabled را می‌پذیرد و AI:Liara را bind نمی‌کند. تغییر Provider به Liara در وضع فعلی startup را رد می‌کند. NetArzClient نام provider را در نتیجه/metrics/log ثابت دارد و اعتبارسنجی lease فقط timeoutهای NetArz را می‌خواند؛ پشتیبانی انتخاب Liara باید این موارد را نیز اصلاح کند.

مستند رسمی https://docs.liara.ir/ai/openai/ سازگاری chat/completions، audio/transcriptions و audio/speech، مدل openai/gpt-5.6-luna و openai/tts-1-hd و voice=nova را مستند کرده است. مدل تنظیم‌شده openai/gpt-transcribe در فهرست آن صفحه نیست؛ openai/whisper-large-v3 مستند است، اما نبود در فهرست اثبات عدم پشتیبانی نیست. نمونه BaseUrl در https://docs.liara.ir/ai/quick-start/ ترتیب /api/v1/{workspace} دارد؛ ترتیب تنظیم فعلی /api/{workspace}/v1 است و باید با آدرس واقعی کنسول تطبیق شود، نه تغییر حدسی.

درخواست GET models با کلید موجود فقط برای بررسی خواندنی انجام شد، اما HttpRequestException شبکه بدون پاسخ HTTP رخ داد؛ صحت کلید، پلن، آدرس و اجرای واقعی AI تأیید نشده‌اند. هیچ درخواست تولید متن/صوت یا ارسال داده کاربر انجام نشد و مقدار کلید/شناسه workspace در یادداشت ذخیره نشد. build صفر warning/error؛ 47/47 تست InterviewProviderTests و InterviewAudioTests موفق، صفر Skip؛ tests/HireYar.Tests/TestResults/liara-review.trx. PostgreSQL/migration، commit/push/deploy انجام نشد. نتیجه: Liara از نظر قرارداد API قابل اتصال است، اما هنوز provider قابل انتخاب پروژه نیست؛ اتصال واقعی و مدل STT باز هستند.

# وضعیت جاری

## گفت‌وگوی خودکار نوبتی داوطلب — 2026-10-06: Verified محلی

روی dev پاک، POST answers با فیلدهای قبلی و clientTurnId/lastSpeechEndUtc/ویدیوی اختیاری گسترش یافت؛ metadata ذخیره می‌شود، unique نوبت در PostgreSQL و replay بدون پردازش دوباره، transcript داوطلب، messages با id/time سرور، mediaCapabilities و دانلود خصوصی HR و ویدیو واقعی در لیست پنل اضافه شدند. Swagger فقط multipart با فایل‌های binary دارد. طبق تصحیح کاربر، WebRTC/Realtime در محدوده نیست. سقف پیش‌فرض صوت ۲۰ MiB، ویدیو ۴۰ MiB و مدت ۶۰۰ ثانیه است؛ زمان کلاینت سیاست مالی را تغییر نمی‌دهد. [CANDIDATE_TURN_API](CANDIDATE_TURN_API.md).

build صفر warning/error و **۱۷۵/۱۷۵ تست PostgreSQL 18 اختصاصی موفق، صفر شکست/Skip**، شامل ۵ تست جدید: replay/تاریخچه/ویدیو/scope، validation اندازه/type/UTC، رقابت همان شناسه، cleanup شکست video write و unique DB برای سؤال دیگر؛ آزمون OpenAPI نیز فرم binary را بررسی کرد. `tests/HireYar.Tests/TestResults/candidate-capture-final.trx`. EF بدون drift؛ script idempotent دو بار روی baseline قبلی با سؤال/پاسخ legacy مصنوعی، حفظ transcript/مالی و رد metadata ناقص ویدیو را تأیید کرد. DBهای تصادفی حذف شدند. migration `20261006170044_AddCandidateAnswerCapture` پیش از نسخه جدید در مقصد لازم است. Angular، ضبط/codec واقعی مرورگر، تنظیم proxy، AI واقعی، push/deploy و DB مقصد تغییر نکرده‌اند.

## رفع 500 ذخیره رزومه دعوت — 2026-10-06

لاگ مقصد برای `POST /api/panel-wizard/invitations` با traceId `00-84eab4a04b430563040f0607ad411f60-8e12eee948539082-00` علت را تأیید کرد: UnauthorizedAccessException در Directory.CreateDirectory داخل LocalInterviewAudioStorage.SaveAsync؛ SQLSTATE خالی است. Dockerfile قبلی فقط /keys را برای APP_UID آماده می‌کرد، نه storage پیش‌فرض زیر /app. روی dev پوشه خصوصی /app/storage/interviews با مالکیت APP_UID و مجوز 700 و volume پایدار interview-storage در هر دو Compose اضافه شد؛ مسیر storage صریح است. schema/runtime API تغییر نکرد.

build صفر warning/error؛ ۲۱ تست مرتبط PanelWizardTests، InterviewAudioTests و AdvancedQuestionTests روی PostgreSQL 18 محلی اختصاصی موفق، صفر شکست/Skip؛ `tests/HireYar.Tests/TestResults/invitations-storage-fix.trx`. هر دو Compose با config --quiet معتبرند (development با مقادیر مصنوعی). Docker daemon در محیط موجود نیست؛ build/run image و مالکیت volume روی Linux هنوز تأیید نشده‌اند. دیتابیس‌های fixture حذف شدند. رفع مقصد نیازمند rebuild/recreate مجاز و بررسی مسیر/مالکیت volume است؛ push/deploy یا تغییر production انجام نشده. فایل قبلی احتمالی پیش از mount تازه باید با حفظ keyهای نسبی منتقل شود؛ [INTERVIEWS](INTERVIEWS.md).

## سؤال‌های پیشرفته مطابق HTML — 2026-10-06: Verified محلی

روی dev: پیشنهاد کل بانک/بازسازی یک موضوع از JD scoped، سه سطح در ۵ تا ۱۰ موضوع، تأیید/ویرایش ویزارد با RowVersion، شمارنده‌های بانک/مصاحبه، snapshot و یک پرسش در هر موضوع اضافه شدند. معمولی و قالب‌های تاریخی محفوظ؛ entitlement در تولید/ذخیره/دعوت/شروع بررسی می‌شود. migration `20261006110718_AddAdvancedTopicQuestions` در مقصد لازم است؛ Angular در ریپو نیست. [ADVANCED_QUESTIONS_API](ADVANCED_QUESTIONS_API.md). restore و tool restore موفق؛ build صفر warning/error؛ **۱۷۰/۱۷۰ تست موفق، صفر شکست و صفر Skip** روی PostgreSQL 18 محلی، شامل ۶ تست جدید موضوعی و ۴ تست adapter پیشنهاد AI. `tests/HireYar.Tests/TestResults/advanced-questions-final.trx`. مسیر کامل ۱۵ سؤال بانک → ۵ پاسخ/پنج موضوع → Completed، scope، تأیید/ویرایش و stale version، Basic، خروجی خراب/timeout و افت اشتراک پیشرفته به معمولی پیش از start تأیید شدند. EF بدون drift؛ اسکریپت idempotent دو بار روی baseline قبلی با قالب/سؤال/جلسه مصنوعی اجرا شد و legacy/constraint حفظ شدند. دیتابیس تصادفی حذف شد. AI/پرداخت واقعی، push/deploy و DB مقصد تغییر نکرده‌اند.


## پاک‌سازی حساب و حذف پنل — 2026-10-06: Verified محلی

روی dev، تغییرات مستندی و اسکریپت تشخیص قبلی حفظ شدند. APIهای ایمیل/رمز و تیم و SMTP/Mailpit حذف شدند؛ حساب فعال فقط Owner با موبایل تأییدشده، حداکثر یک حساب فعال در هر سازمان. DELETE موقعیت/قالب/مصاحبه با حذف منطقی، scope، حفظ تاریخچه، منع دعوت تازه و تسویه مصرف/آزادسازی رزرو اضافه شد. [PANEL_CLEANUP_API](PANEL_CLEANUP_API.md) تحویل فرانت و شرایط انتقال Owner ایمیلی است. Angular/production تغییر نکردند؛ باگ entitlement ویزارد مشاهده‌شده در مرورگر هنوز اصلاح فرانت می‌خواهد و علت API آن این نوبت ادعا نشده است.

restore/tool restore موفق؛ build صفر warning/error؛ **۱۶۰/۱۶۰ تست PostgreSQL 18 محلی موفق و صفر Skip**، شامل ۱۰ تست cleanup و انتقال fixture از ایمیل به OTP. EF بدون drift؛ script idempotent روی baseline قبلی با Owner/Recruiter/refresh/دعوت مصنوعی دو بار موفق و حفظ هویت/ابطال دسترسی تیم تأیید شد. `tests/HireYar.Tests/TestResults/panel-cleanup-final.trx`. migration `20261006071854_SimplifyAccountsAndPanelDeletion` در مقصد لازم است. پیامک/AI/پرداخت واقعی، push/deploy یا تغییر DB مقصد انجام نشد.

## بررسی 500 پنل و داشبورد مقصد — 2026-10-06: در انتظار شواهد سرور

روی `dev` پاک، گزارش `unexpected_error` برای `/api/billing/panel` و `/api/dashboard` بررسی شد. هر دو مسیر وابسته به schema جدیدند: جدول SubscriptionReplacements در مالی و ستون‌های snapshot/ResumeStorageKey در داشبورد. Program/Docker migration خودکار ندارند؛ نبود migration محتمل است ولی بدون لاگ traceId و وضعیت schema مقصد علت قطعی نیست. build صفر warning/error و ۱۷ تست مرتبط PostgreSQL واقعی بدون Skip موفق شدند؛ `tests/HireYar.Tests/TestResults/panel-errors-diagnostic.trx`. اسکریپت فقط‌خواندنی [Check-PanelSchema.sql](../scripts/Check-PanelSchema.sql) روی baseline قبل از دو migration، اشیای missing و پس از migration همه OK را نشان داد؛ DB مصنوعی حذف شد. کد runtime تغییر نکرد؛ production/push/deploy انجام نشد. برای ادامه، ExceptionType/SQLSTATE/StackTrace همان درخواست و نتیجه schema check از همان DB/search_path API لازم است.

## اشتراک و اعتبار مطابق پنل — 2026-10-05

برنچ `codex/subscription-credit-panel` از `panel-wizard` پاک ساخته شد؛ APIهای چهار تب فایل `subscription_credit.html` آماده‌اند: `/api/billing/panel` وضعیت/صف/انقضا و wallet/rate/زمان تخمینی از اعتبار آزاد؛ quote/checkout با discount، tax تنظیمی پیش‌فرض ۱۰٪، fingerprint کنترل تغییر خلاصه و ارتقای روزشمار؛ financial-history با فیلتر نوع/تاریخ و totals مستقل از page؛ invoice JSON از پرداخت Paid scoped. owner خرید می‌کند، recruiter فقط مشاهده/preview. Swagger فارسی و مسیرهای قدیمی حفظ شدند؛ محاسبه مدت محدود سرور و billing جلسات قبلی تغییر نکرد.

ارتقا طبق فرض کاری «دوره کامل جدید با کسر ارزش روزهای کامل باقی‌مانده» با SubscriptionReplacement append-only و transaction/tenant FK/unique/trigger انجام می‌شود؛ تاریخچه قبلی بازنویسی نمی‌شود. صف آینده یا پرداخت Pending پیش از ارتقا باید حل شود. quote و read با runtime Disabled فعال‌اند ولی checkout جدید 503 است؛ درگاه واقعی اضافه نشده. migration `20261005133142_AddSubscriptionCreditPanel` دوره‌های seed دست‌نخورده ۳/۶ماهه را مطابق تخفیف ۵/۱۰٪ HTML تنظیم می‌کند، قیمت ویرایش‌شده مدیر و snapshotهای قبلی محفوظ‌اند. [SUBSCRIPTION_CREDIT_API](SUBSCRIPTION_CREDIT_API.md) قرارداد جدید فرانت است. نکات «اشتراک دست‌نخورده» در تحویل ویزارد پایین مربوط به محدوده نوبت قبلی‌اند.

بررسی واقعی: tool restore/restore موفق، build صفر warning/error؛ **۱۶۱ Passed، صفر Failed، صفر Skipped** روی PostgreSQL 18 مستقل loopback:55453 (شش تست تازه مالی پنل، provider جعلی). TRX نهایی `TestResults/subscription-credit-final.trx`. تست quote/tax/discount/invoice/renewal، upgrade هم‌زمان و immutable/cutover، failure/صف، عدم تکرار خرید credit، PaidAtUtc و فیلتر/totals، reservation estimate و مدت/نام داوطلب و gateway Disabled موفق‌اند. EF بدون pending changes؛ script idempotent دو بار روی baseline PanelWizard با خرید/اشتراک مصنوعی قدیمی و قیمت ویرایش‌شده مدیر موفق بود؛ operation backfill، مبلغ/tax قبلی و حفاظت قیمت مدیر بررسی شد. DBهای تصادفی حذف و cluster اختصاصی پس از بررسی متوقف شد. Angular، PDF فاکتور، پرداخت/AI واقعی، migration مقصد، commit/push/deploy و تغییر production انجام نشده‌اند.

## پنل ویزاردی — 2026-10-05

روی `dev` از Git پاک، براساس `panel_v5.html` ارسالی APIهای ساده ساخت/ویرایش موقعیت و قالب متنی و دعوت multipart در `api/panel-wizard` اضافه شدند. انتخاب نوع عمومی/تخصصی مستقل از قالب، حداقل پنج سؤال، ترتیب و پیام‌های شروع/پایان، اطلاعات کارجو و رزومه خصوصی تا ۱۰ MiB آماده‌اند. فهرست مصاحبه‌ها با جستجو/چند وضعیت/موقعیت/بازه تاریخ، metadata و درصد مبتنی بر evidence، صدور مجدد با ابطال توکن قدیمی، لغو، دانلود scoped رزومه و آمار/آخرین مصاحبه‌های داشبورد اضافه شدند. سؤال و شرح شغل و پیام‌ها هنگام دعوت snapshot می‌شوند؛ HR جدید نیاز به Skill/PositionSkill/BankQuestion ندارد. قالب همان سازمان برای موقعیت دیگر در دعوت کپی می‌شود تا FK پذیرفته‌شده حفظ شود.

قرارداد فرانت جدید: [PANEL_WIZARD_API](PANEL_WIZARD_API.md). مسیرهای قدیمی و همه عنوان‌های فارسی Swagger حفظ شده‌اند. subscription/wallet/pricing و سیاست مصرف اعتبار تغییری ندارند. Angular، ارسال واقعی ایمیل/SMS، ویدیو و تنظیمات مدت سازمانی اجرا نشده‌اند؛ delivery برابر NotConfigured و videoAvailable=false است تا UI موفقیت ارسال یا وجود ویدیو را ادعا نکند.

اعتبارسنجی واقعی: restore موفق؛ ابزار EF پس از خطای feed موجود با config موقت منبع عمومی بازیابی شد. build صفر warning/error؛ **۱۵۵ Passed، صفر Failed، صفر Skipped** با PostgreSQL 18 مستقل loopback:55451 و provider آزمایشی؛ TRX در `TestResults/panel-wizard-final.trx`. چهار تست جدید ویزارد، flow بدون ساخت مهارت، snapshot/ترتیب/پیام، stale version، scope، file/type/phone validation، reuse قالب، resend/expiry/cancel و entitlement عمومی/تخصصی را پوشش می‌دهند. تست فارسی OpenAPI هم موفق است. EF بدون pending model changes؛ migration `20261005130515_AddPanelWizard` و script idempotent با baseline قبلی و داده مصنوعی، دو بار موفق؛ backfill general و پیام‌ها بررسی شد. دیتابیس‌های تصادفی حذف و cluster مستقل تست پس از بررسی متوقف شد. migration مقصد، commit، push و deploy انجام نشده‌اند.

اسکریپت دسترسی همکاران برای تست: [Grant-ColleagueTestAccess.sql](../scripts/Grant-ColleagueTestAccess.sql) به سازمان‌های دارای کاربر فعال، پیش‌فرض ۱۰۰ USD اعتبار PromotionalCredit و Advanced یک‌ماهه می‌دهد. اجرای مجدد campaign تکراری شارژ نمی‌کند؛ Advanced فعال موجود حفظ می‌شود، اشتراک متداخل دیگر باعث rollback کامل است. سند پرداخت `InternalTestGrant` صرفاً اعطای غیرنقدی با مبلغ اسمی کاتالوگ است و باید از گزارش درآمد نقدی کنار گذاشته شود. روی DB کاربران اجرا نشده؛ PostgreSQL 18 با سه کاربر مصنوعی/دو سازمان، اجرای تکراری و حفظ موجودی پس از مصرف موفق؛ build صفر warning/error.

## Pricing و Billing — 2026-10-01

برنچ `codex/pricing` از وضعیت پاک با fast-forward به `origin/dev` در `d9d4d72` به‌روز شد؛ مصاحبهٔ موجود نیز در همین برنچ است. پکیج/دوره/feature پویا، snapshot قرارداد خرید، اشتراک صف‌شده بدون از دست‌رفتن زمان، AI wallet/ledger، رزرو حداکثر هزینه، settlement یک‌باره، API عمومی/admin/organization، مرز SystemAdmin و IPaymentGateway پیاده شدند. currency IRT/USD و مدت مالی window محدود سرور مستقل‌اند؛ راهنمای معتبر [BILLING](BILLING.md).

وضعیت: رفتار داخلی مالی با PostgreSQL **Verified**؛ درگاه واقعی و آمادگی پرداخت production **NotImplemented**. runtime provider غیرفعال است؛ pending quote ساخته می‌شود ولی verification برابر 503 و fulfillment مالی انجام نمی‌شود. محصولات اعتبار باید با مقدار و قیمت صریح توسط مدیر ایجاد شوند. فایل‌ها بر اساس retention snapshot از download خارج می‌شوند؛ حذف فیزیکی/backup/volume آماده‌سازی عملیاتی‌اند.

نتیجهٔ واقعی این نوبت:

- `dotnet tool restore` و `dotnet restore HireYar.slnx`: موفق پس از دسترسی به NuGet.Config؛ هیچ package/framework جدید اضافه نشد.
- `dotnet build HireYar.slnx --no-restore`: موفق، صفر warning/error؛ EF `has-pending-model-changes --no-build`: بدون اختلاف مدل.
- `dotnet test HireYar.slnx --no-restore` با PostgreSQL 18 واقعی مستقل loopback:55447: **۱۵۱ Passed، صفر Failed، صفر Skipped**؛ TRX: `TestResults/billing-complete.trx`.
- ۳۳ تست جدید: ۱۰ math case و ۲۳ تست PostgreSQL، شامل callback هم‌زمان، provider transaction تکراری، رزرو race، ledger/tenant constraint، snapshot، expiry، basic capabilities، rounding، retention و provider غیرفعال. تست‌های مصاحبهٔ موجود با خرید/اعتبار آزمایشی صریح ادامه یافتند؛ auto-funding runtime اضافه نشد.
- migration `20261001080208_AddPricingBilling` تولید شد؛ precision، unique، FK مرکب، Restrict و backfill بررسی شدند. script idempotent در `artifacts/billing-migrations.sql` دو بار روی DB تصادفی مستقل اعمال شد؛ هر دو موفق، کاتالوگ دو پکیج و هشت دوره داشت؛ DB بررسی حذف شد.
- constraint triggerهای deferred، wallet بدون ledger و رزرو بیش از موجودی را در commit رد می‌کنند. ثبت دورهٔ جدید، ساخت lazy wallet و limiter partition در جریان تست اصلاح شدند. snapshot پکیج در یک statement خوانده می‌شود تا split query قرارداد ترکیبی نسازد.

دیتابیس تصادفی script و تمام دیتابیس‌های fixture حذف شدند؛ count نهایی صفر بود. cluster آزمایشی مستقل همین نوبت متوقف شد. `git diff --check` بدون ایراد whitespace است.

اختلاف با یادداشت audit قبلی بررسی شد: source دریافتی NetArzOptions هنوز ApiKey غیرخالی داشت؛ default این نوبت خالی شد و تست الزام کلید موفق است. هیچ AI یا درگاه واقعی فراخوانی نشد؛ کلید محلی دست‌نخورده است. rotation کلید قبلاً ثبت‌شده در Git همچنان عملیاتی لازم است. push/deploy و تغییر production انجام نشده؛ تغییرات این نوبت commit نشده‌اند. بخش‌های تاریخی پایین مرجع وضعیت Pricing نیستند.

## audit پیش از staging — 2026-10-01

نتیجه فعلی در [INTERVIEW_AUDIT](INTERVIEW_AUDIT.md): دو نقص مسیر خطا (قطع response stream provider و خواندن cache صوت) اصلاح شدند؛ ApiKey از default فایل تحت Git به تنظیمات محلی ignored منتقل شد و تست الزام کلید موفق است. NetArz فقط در Development local فعال شد؛ endpoint/قرارداد/schema تغییر نکرد. build صفر warning/error؛ **۱۱۸ تست موفق، صفر شکست/Skip** با PostgreSQL 18 واقعی مستقل. EF بدون pending model changes. smoke واقعی TTS → STT → LLM با داده مصنوعی و decision معتبر موفق شد؛ گزارش‌های «NetArz آزمایش نشده» و «تست کلید شکست دارد» پایین تاریخی‌اند. rotation کلید قبلاً ثبت‌شده در Git پیش از staging هنوز تأیید نشده؛ volume/retention/backup عملیاتی باقی‌اند. migration مقصد طبق گفته کاربر قبلاً اجرا شده و در این audit تغییر نکرد.

## تحویل قرارداد فرانت‌اند — 2026-10-01

راهنمای مستقل [FRONTEND_INTERVIEWS_HANDOFF](FRONTEND_INTERVIEWS_HANDOFF.md) بر اساس DTOها و Controllerهای فعلی اضافه شد: جریان داوطلب، فرم حداقلی HR، گزارش، صوت خصوصی، retry، محدودیت APIها و پرامپت آماده Codex. این مرحله فقط مستندات است؛ فرانت‌اند، API یا دیتابیس تغییر نکرد. اعتبارسنجی این مرحله در HANDOFF ثبت می‌شود؛ نتیجه ۹۴ تست بخش قبلی مربوط به پیاده‌سازی بک‌اند است.

## جریان قابل اجرای مصاحبه — ادامه 2026-10-01

این بخش جایگزین محدودیت‌های «شروع ماژول» پایین است. مسیر کامل HR invitation → candidate token → start/current → private audio → STT → exact shortlist/LLM → evidence/fallback → next question/complete پیاده و روی PostgreSQL واقعی با provider جعلی آزمایش شد. retry همان پاسخ، lease/recovery، schema tenant FK، unique answer و optimistic concurrency، download خصوصی و TTS cache برقرارند. مدل‌ها دیگر صرفاً حافظه‌ای نیستند؛ تمام stateها entity رابطه‌ای مستقل با explicit mapping و Restrict هستند. جزئیات قرارداد و تنظیمات در [INTERVIEWS](INTERVIEWS.md).

Migration جدید `20261001061608_AddCandidateInterviewFlow` تولید و روی DB تصادفی fixture PostgreSQL 18 اعمال شد؛ migration قبلی یا جدول‌های قبلی recreate نشدند. EF has-pending-model-changes بدون اختلاف. build صفر warning/error؛ کل تست‌ها **۹۴ موفق، صفر شکست، صفر Skip** با HIREYAR_TEST_POSTGRES روی cluster آزمایشی مستقل localhost:55441. HTTPهای واقعی AI در suite فراخوانی نشده‌اند؛ NetArz با mocked handler بررسی شده و پیاده‌سازی واقعی HttpClientFactory آمادهٔ تنظیم است. کلید واقعی و smoke-test حساب NetArz، volume/retention خصوصی فایل‌ها و اعمال migration در محیط مقصد هنوز لازم‌اند. push/deploy/commit انجام نشده است.

## شروع ماژول مصاحبه داوطلب — 2026-10-01

طبق درخواست مستقل کاربر، پرامپت Candidate AI Interview روی برنچ `codex/candidate-ai-interview` از `dev` شروع شد؛ Git در شروع پاک بود. فقط هستهٔ Domain/Application این نوبت پیاده شده و ماژول کامل یا production-ready نیست.

- مدل جلسه با وضعیت Created/WaitingForAnswer/ProcessingAnswer/Completed/Cancelled؛ پاسخ مستقل با Uploaded/Transcribing/Transcribed/Evaluating/Processed/Failed، retry همان پاسخ و حفظ صوت و transcript.
- snapshot مستقل سؤال و مهارت، state مهارت، evidence با delta واقعی پس از clamp و coverage موضوع به تفکیک مهارت؛ امتیاز و confidence در بازه ۰..۱.
- سیاست shortlist بین ۵ تا ۲۰، حذف سؤال پرسیده‌شده/غیرفعال/موضوع پوشش‌یافته، اولویت کمبود evidence، required/priority و difficulty؛ fallback قطعی.
- DTO سخت‌گیر خروجی AI، validation و fallback بدون تغییر assessment نامعتبر؛ prompt نسخه v1. پیشنهاد تکمیل AI شرط پایان را تغییر نمی‌دهد.
- restore موفق (دسترسی NuGet.Config با escalation)، build صفر warning/error؛ `dotnet test HireYar.slnx --no-restore`: ۲۶ موفق، صفر شکست، ۲۷ Skip بدون HIREYAR_TEST_POSTGRES. ۲۴ مورد تست جدید Domain/Application موفق‌اند؛ این شواهد اثبات رفتار PostgreSQL نیستند.

مدل‌ها هنوز به EF متصل نیستند؛ جدول یا migration جدید، API، candidate access token، storage، NetArz HttpClient، observability و تست provider/دیتابیس اضافه نشده‌اند. فراخوانی واقعی AI یا تغییر دیتابیس صورت نگرفت. push/deploy/commit انجام نشد.

به‌روزرسانی 2026-09-27: `Kavenegar` در appsettings تعریف شد؛ `ApiKey`/`Template` خالی، `UseFixedOtp=false` در تنظیمات مشترک و کد ثابت `123456` با فلگ true فقط در Development. حالت ثابت SMS را کامل رد می‌کند و از همان challenge و محدودیت‌های OTP استفاده می‌کند. تست جدید آن روی PostgreSQL 18 اجرا شد؛ نتیجهٔ کل مجموعه ۲۹/۲۹ موفق، build صفر warning/error. ارسال واقعی کاوه‌نگار همچنان بدون کلید و قالب بررسی نشده است.

تغییر 2026-09-27: ورود یکپارچه با موبایل و OTP در `otp/request` و `otp/verify` پیاده شد؛ نبود کاربر باعث ساخت Owner و سازمان اولیه می‌شود. اطلاعات شرکت با `PUT /api/organization` تکمیل می‌شود. VerifyLookup کاوه‌نگار با تنظیمات محیطی و بدون ثبت کلید/کد در لاگ اضافه شد. migration `AddMobileOtp` و تست جدید روی PostgreSQL 18 محلی اجرا شد؛ مجموعهٔ کامل ۲۸/۲۸ موفق، build بدون warning/error، و EF `has-pending-model-changes` بدون اختلاف بود. ارسال واقعی کاوه‌نگار به علت نبود کلید و قالب آزمایش نشده است. مسیرهای ایمیل/رمز و دعوت Recruiter هنوز فعال‌اند تا تصمیم مهاجرت حساب‌های فعلی گرفته شود؛ بنابراین جایگزینی کامل هنوز انجام نشده است. به دیتابیس استقرار migration اعمال نشده و push/deploy انجام نشده است.

تغییر موقت 2026-09-26: برای نبود SMTP، ثبت‌نام Owner با `Security:AutoConfirmEmailOnRegistration=true` ایمیل را در DB تأییدشده ثبت می‌کند و ارسال ایمیل تأیید را رد می‌کند؛ ورود با همان حساب ممکن است. build موفق با صفر warning/error؛ تست بدون اتصال PostgreSQL: ۲ موفق، ۲۵ Skip، شامل تست جدید این مسیر. اجرای واقعی تست جدید روی PostgreSQL و اعمال migration در محیط هدف هنوز تأیید نشده است. تغییر محلی کاربر در `appsettings.Development.json` حفظ شد.

بررسی ثبت‌نام Production در 2026-09-26: پاسخ 500 کاربر فقط `unexpected_error` و `traceId` دارد و لاگ Docker همان درخواست هنوز دریافت نشده؛ علت قطعی مشخص نیست. مسیر ثبت‌نام پیش از ارسال ایمیل به PostgreSQL دسترسی دارد، ولی CSRF به دیتابیس نیاز ندارد. برای تشخیص، شناسهٔ لاگ و پاسخ یکسان شد و SQLSTATE خطای PostgreSQL بدون متن حساس در لاگ ثبت می‌شود. تنظیمات اتصال و کلید امضا از `appsettings.json` تحت Git حذف و در Compose به متغیرهای اجباری خارج Git منتقل شدند. build بدون هشدار/خطا، تست ۲ موفق و ۲۴ Skip به علت نبود PostgreSQL تست، و اعتبارسنجی Compose موفق شد. بررسی واقعی سرور، اتصال دیتابیس و migration پس از انتشار لازم است؛ push/deploy انجام نشده است.

بررسی 2026-09-26: stack trace سرور خطای `DefaultAntiforgery.CheckSSLConfig` را تأیید کرد؛ `IsHttps=False` با وجود هدر forwarded. طبق درخواست صریح کاربر، `KnownProxies` و متغیر IP از کد و Compose حذف شدند؛ API در Production فقط مقدار دقیق و یکتای `X-Forwarded-Proto: https` را بدون بررسی مبدأ برای scheme اعمال می‌کند. الزام کوکی Secure حفظ شد. دسترسی مستقیم به پورت HTTP داخلی باید در شبکه سرور محدود باشد. build بدون هشدار/خطا و تست: ۲ موفق، ۲۴ Skip به علت نبود PostgreSQL تست. رفتار سرور پس از انتشار هنوز تأیید نشده؛ push/deploy انجام نشده است.

آخرین به‌روزرسانی: 2026-09-25. مرحله ۱، بک‌اند سازمان و پنل HR. پایگاه داده جاری **PostgreSQL 18** است؛ تصمیم SQL Server با درخواست صریح کاربر جایگزین شد و داده قبلی نیاز به انتقال ندارد. تنظیمات محلی JWT و اتصال دیتابیس اکنون در `src/HireYar.Api/appsettings.Development.local.json` قرار می‌گیرند؛ فایل در Git نادیده گرفته می‌شود و متغیرهای محیطی اولویت بالاتر دارند.

## وضعیت قابلیت‌ها

| قابلیت | وضعیت | شواهد |
|---|---|---|
| ساختار، Controller، Services/DTOs/Interfaces | Verified | build Release با صفر warning/error |
| ثبت‌نام اتمیک، ایمیل یکتا، تأیید، ورود و lockout | Verified | تست روی PostgreSQL واقعی 18.3 |
| refresh rotation/replay و هم‌زمانی، logout/all | Verified | تک‌جانشینی و ابطال خانواده نشست روی PostgreSQL |
| reset/change password، پروفایل | Verified | تست موجود روی PostgreSQL موفق |
| سازمان، نقش، دعوت، انقضا/لغو/resend و فعال‌سازی | Verified | تست نقش جاری و پذیرش هم‌زمان موفق |
| tenant isolation، FK مرکب و Dashboard | Verified | رد دسترسی و رابطه بین دو سازمان روی PostgreSQL |
| موقعیت و قالب و سؤال | Verified | ایجاد، ویرایش، ترتیب، حفظ Id و تعارض نسخه موفق |
| migration PostgreSQL | Verified | InitialPostgres روی DB خالی اعمال شد؛ script idempotent دو بار اجرا شد |
| cookie، CSRF و CORS | Verified | تست‌های قبلی PostgreSQL موفق؛ تست ترکیبی Production با رفتار جدید OpenAPI هنوز دوباره اجرا نشده |
| OpenAPI در Production | Verified | Swagger و JSON هر دو پاسخ 200؛ راهنمای شروع و عنوان همهٔ عملیات‌ها به فارسی در JSON بررسی شد |
| Docker و SMTP واقعی | Implemented | compose معتبر؛ Docker daemon در دسترس نیست و build/up/SMTP واقعی اجرا نشده |
| اتصال توسعه و مستندات | Implemented | appsettings.Development.local.json خارج Git برای localhost:5433؛ اسکریپت‌های راه‌اندازی و migration |

Verified به معنی اجرای بررسی مربوط است؛ Implemented تنها وجود کد را نشان می‌دهد. ایمیل تست جایگزین حافظه‌ای است؛ ارسال واقعی SMTP تأیید نشده است.

## نتیجه واقعی آخرین بررسی‌ها

- `dotnet restore HireYar.slnx`: موفق. provider برابر Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3؛ EF/ASP.NET نسخه 10.0.12 و SDK 10.0.400.
- `dotnet build HireYar.slnx -c Release --no-restore`: موفق، صفر warning/error.
- `dotnet test HireYar.slnx -c Release --logger 'trx;LogFileName=postgres.trx' --results-directory TestResults` با HIREYAR_TEST_POSTGRES: **۲۳ موفق، ۱ شکست، صفر Skip**.
- شکست همان `Production_sets_secure_cookies_and_hides_openapi` است که پیش از مهاجرت هم وجود داشت؛ Program.cs در این تغییر دست‌نخورده است. نتیجه قدیمی ۲۴/۲۴ مرجع checkout فعلی نیست.
- تست‌ها با باینری PostgreSQL 18.3 نصب‌شده و یک cluster جدا در artifacts/pg-validation روی loopback:55432 اجرا شدند؛ سرویس PostgreSQL موجود و SQL Server قبلی تغییر نکردند. instance آزمایشی پس از کار متوقف شد؛ دیتابیس‌های تصادفی تست حذف شدند.
- migration جاری: `src/HireYar.Infrastructure/Persistence/Migrations/20260924081519_InitialPostgres.cs` و snapshot متناظر. InitialCreate مخصوص SQL Server جایگزین شده است؛ migration داده بین دو provider انجام نمی‌شود.
- `dotnet ef migrations has-pending-model-changes ... --configuration Release`: بدون اختلاف.
- `dotnet ef migrations script --idempotent ... --output artifacts/postgres-migrations.sql`: موفق؛ اسکریپت دو بار روی DB تصادفی مستقل با psql و ON_ERROR_STOP اجرا شد؛ هر دو موفق. DB بررسی حذف شد.
- `docker compose --env-file .env.example -f compose.dev.yaml config --quiet`: موفق؛ فقط اعتبارسنجی ساختار است.

## اجرا و محدودیت‌ها

Swagger اکنون راهنمای فارسی کوتاه برای مسیر `csrf → register → confirm-email → login` و مسیر ورود حساب موجود دارد. عنوان همهٔ عملیات‌های API فارسی است. Build بدون warning/error و تست مستقل Production OpenAPI موفق شد؛ تست‌های وابسته به PostgreSQL در این نوبت اجرا نشدند.

رفع خطای startup: `UserSecretsId` قدیمی از پروژه API حذف شد. ASP.NET Core دیگر در Development فایل User Secrets خراب/غیرقابل‌دسترس را پیش از بارگذاری appsettings محلی باز نمی‌کند. Build موفق؛ اجرای واقعی `dotnet run --launch-profile http` با `/health` و `/swagger/index.html` هر دو 200 بود. تست جاری ۱ موفق و ۲۴ Skip به علت نبود HIREYAR_TEST_POSTGRES است.

انتخاب کاربر برای استفاده روزمره PostgreSQL داخل Docker در localhost:5433 است. appsettings محلی تنظیم شده، اما Docker daemon در این محیط Access denied می‌دهد و کانتینرها و SMTP واقعی تأیید نشده‌اند. `dotnet tool restore` به دلیل Access denied به NuGet.Config اجرا نشد؛ اعتبارسنجی EF این نوبت انجام نشد.

بررسی تازه: کاربر `.env` را خالی کرد و Compose به‌درستی متغیرهای اجباری را نیافت. `scripts/Start-DevelopmentContainers.ps1` اکنون رمز دیتابیس و signing key را از appsettings محلی می‌خواند و فقط برای اجرای Compose در محیط فرایند قرار می‌دهد. `.env` از فهرست فایل‌های تحت پیگیری Git خارج شد، ولی محتوای نسخه‌های قبلی هنوز در تاریخچه است و مقادیر قبلی باید در محیط‌های مربوط چرخانده شوند. appsettings محلی از Docker context و خروجی publish کنار گذاشته شد؛ publish Release موفق و نبود فایل محلی در خروجی بررسی شد. اجرای اسکریپت تا فراخوانی Docker تأیید شد؛ daemon با Access denied مانع up واقعی شد. Build موفق و نتیجهٔ تست جاری ۱ موفق، ۲۴ Skip به دلیل نبود HIREYAR_TEST_POSTGRES بود. تست مستقل Production Swagger موفق شد. اجرای API روی میزبان و `/health` پاسخ 200 داشت؛ `/api/auth/me` بدون توکن پاسخ 401 داد.

rowVersion اکنون Base64 از ۱۶ بایت است؛ توکن قبلی SQL Server قابل استفاده نیست. جزئیات در ARCHITECTURE و README است. setup production/TLS و frontend همچنان خارج دامنه‌اند.

برنچ فعال dev و در شروع این نوبت با origin/dev همگام بود. تغییرات راهنمای فارسی Swagger هنوز commit/push نشده‌اند؛ production تغییر نکرد.

یادداشت اجرای نهایی مصاحبه: یک run با خطای transport اتصال PostgreSQL شکست خورد؛ اجرای مجدد با SSL Mode=Disable و GSS Encryption Mode=Disable صرفاً روی cluster محلی آزمایشی، ۹۴/۹۴ موفق بود. build نهایی صفر warning/error؛ DB تصادفی تست باقی نماند و cluster آزمایشی متوقف شد. اسکریپت idempotent در artifacts/candidate-interview-migrations.sql تولید شد ولی این اسکریپت جداگانه اجرا نشد؛ اعمال migration از fixture واقعی PostgreSQL بررسی شد.

## تنظیمات AI در appsettings — 2026-10-02

روی برنچ `codex/ai-appsettings` از `dev`، بخش `AI` در `src/HireYar.Api/appsettings.json` اضافه شد: Provider=NetArz، BaseUrl، مدل‌های STT/LLM/TTS، voice، speed و سه timeout. binding موجود `AI:NetArz` و انتخاب `AI:Provider` همین تنظیمات را مصرف می‌کنند؛ override محیطی/local همچنان اولویت دارد. ApiKey در محل فعلی و تمام فایل‌های کلید دست‌نخورده ماند؛ کلید تازه در appsettings اضافه یا کپی نشد. یادداشت‌های تاریخی Disabled برای پیش‌فرض مشترک دیگر وضعیت جاری این برنچ نیستند.

Build موفق با صفر warning/error؛ تست‌های InterviewProviderTests و InterviewAudioTests: ۴۳ موفق، صفر شکست/Skip. تست کلید خالی اکنون مقدار خالی را صریحاً فقط در fixture خود می‌دهد تا وابسته به default فعلی نباشد. تماس واقعی AI، تست PostgreSQL، migration، commit، push و deploy انجام نشدند.

## بررسی مرورگر مصاحبه مقصد — 2026-10-07

ورود داوطلب موفق بود. صفحه پس از بازیابی و reload یک پاسخ صوتی ثبت‌شده، صفر از پنج پاسخ پردازش‌شده و زمان صفر نشان داد؛ گزارش HR همان جلسه متن آماده‌نشده، صفر شاهد و پایان خالی داشت. کنسول مرورگر در بازه مشاهده خطایی ثبت نکرد. این شواهد توقف پیش از آماده‌شدن transcript را نشان می‌دهند، نه علت قطعی provider یا شبکه. کد فعلی current فقط وضعیت را می‌خواند؛ retry پاسخ persistشده مستقل است و complete در ProcessingAnswer پذیرفته نمی‌شود. UI مشاهده‌شده پس از پایان زمان گزینه retry نداشت؛ وضعیت دقیق answer/canRetry و failureCode مقصد هنوز دریافت نشده است. نیاز بعدی: پاسخ پالایش‌شده current و لاگ STT/lease همان درخواست، سپس تطبیق فرانت با قرارداد CANDIDATE_TURN_API. کلید، اطلاعات کارجو و شناسه جلسه در این یادداشت ذخیره نشدند.

runtime و تنظیمات تغییر نکردند؛ build صفر warning/error و 80/80 تست مرتبط InterviewRulesTests/InterviewProviderTests/InterviewAudioTests موفق، صفر Skip. این تست‌ها PostgreSQL یا AI واقعی مقصد را اثبات نمی‌کنند. push/deploy یا تغییر دستی DB مقصد انجام نشد.

### شواهد لاگ سرور برای بررسی مصاحبه — 2026-10-07

لاگ ارائه‌شده توسط کاربر توقف STT را تأیید کرد: NetArzClient با model=gpt-transcribe، httpStatusCode=404 و failureCode=stt_http_404؛ پس از آن CandidateInterviewService پاسخ را retry available اعلام کرده است. TTS نیز با model=tts-1-hd و tts_http_404 شکست خورده. بنابراین علت اولیه توقف، شکست HTTP سرویس صوت در provider فعال NetArz است؛ علت دقیق 404 (مسیر/base URL، مدل یا routing سرویس‌دهنده) هنوز معلوم نیست. برچسب کلاس runtime انتخاب NetArz را تأیید می‌کند، نه Liara. کد مسیرهای نسبی audio/transcriptions و audio/speech را به BaseUrl اضافه می‌کند؛ تنظیمات مؤثر سرور ممکن است با فایل ریپو متفاوت باشند. retry بدون اصلاح علت 404 دوباره شکست می‌خورد. runtime و secretها تغییر نکردند؛ نتیجه build/80 تست ثبت‌شده بالا مربوط به همین بررسی پیش از دریافت لاگ است.

### لاگ پس از سوییچ به Liara — 2026-10-07

لاگ جدید کاربر LiaraClient و شکست TTS/STT با HTTP 403 را تأیید کرد؛ انتخاب provider انجام شده ولی دسترسی صوت هنوز موفق نیست. 403 به‌تنهایی علت دقیق را تعیین نمی‌کند. مستند رسمی https://docs.liara.ir/ai/quick-start/ کلید AI با دسترسی پروژه منتخب، محدودیت مدل‌های پلن و الگوی BaseUrl برابر /api/v1/{project} را بیان می‌کند؛ https://docs.liara.ir/ai/openai/ مدل openai/whisper-large-v3 را برای صوت و openai/tts-1-hd را فهرست می‌کند، openai/gpt-transcribe در فهرست نیست. فایل مشترک ریپو همچنان NetArz پیش‌فرض دارد؛ آدرس Liara با الگوی مستند سازگار نیست و مدل STT آن openai/gpt-transcribe است. تنظیمات مؤثر مقصد و متن پالایش‌شده خطای provider برای تفکیک مجوز پروژه/پلن/مسیر لازم‌اند. فایل‌های تنظیمات/کلید و runtime تغییر نکردند؛ بررسی جدید صرفاً لاگ، مستندات و خواندن فیلدهای غیرمحرمانه است.

## اصلاح تنظیمات Liara طبق درخواست — 2026-10-07

در src/HireYar.Api/appsettings.json فقط AI:Liara:BaseUrl از /api/{project}/v1 به /api/v1/{project} و SttModel از openai/gpt-transcribe به openai/whisper-large-v3 اصلاح شد. شناسه پروژه و ApiKey موجود عیناً حفظ شدند؛ تمام سایر مقادیر تنظیمات از جمله Provider و TTS تغییر نکردند. تغییرات مستندی قبلی حفظ شدند. build صفر warning/error و 55/55 تست provider/selection/audio موفق، صفر شکست/Skip. اتصال واقعی Liara یا رفع 403 مقصد هنوز تأیید نشده؛ override محیطی ممکن است بر فایل اولویت داشته باشد. PostgreSQL/migration، commit/push/deploy انجام نشدند.

بازبینی appsettings — 2026-10-07: JSON فایل‌های موجود معتبر، BaseUrl و STT اصلاح‌شده Liara صحیح، ApiKey نسبت به HEAD یکسان و قیود timeout/speed/lease (پیش‌فرض 300 ثانیه) معتبرند. نکته باقی‌مانده: AI:Provider فایل مشترک هنوز NetArz است؛ local override توسعه و AI__Provider در محیط این بررسی وجود ندارند. انتخاب Liara سرور از لاگ قبلی تأیید شده ولی تنظیمات مؤثر مقصد این نوبت خوانده نشد. درخواست صرفاً بررسی بود؛ تنظیمات تغییر نکردند و build/test تکرار نشد (55 تست و build نوبت اصلاح موفق بودند).

## تنظیمات Liara مطابق curl موفق کاربر — 2026-10-07

شواهد مستقیم کاربر، STT موفق با BaseUrl دارای الگوی /api/{project}/v1 و model=openai/gpt-transcribe را نشان داد. بنابراین توصیه قبلی برای جابه‌جایی مسیر و تغییر مدل بر اساس فهرست مستندات، برای این پروژه نادرست بود و این یادداشت جایگزین نتیجه آن توصیه است. در appsettings.json آدرس دقیق ارائه‌شده کاربر بدون تغییر شناسه پروژه و مدل openai/gpt-transcribe اعمال و AI:Provider=Liara شد. ApiKey و همه مقادیر دیگر عیناً حفظ و به‌صورت بدون نمایش مقدار بررسی شدند. transcript واقعی کاربر یا کلید در مستندات ثبت نشد.

build صفر warning/error و 55/55 تست provider/selection/audio موفق، صفر شکست/Skip. STT واقعی طبق curl کاربر موفق است؛ تماس تازه AI توسط agent انجام نشد و موفقیت TTS یا مسیر کامل مصاحبه هنوز تأیید نشده. Overrideهای سرور ممکن است تنظیمات فایل را جایگزین کنند. مجوز کلید/پلن و علت 403 قبلی از این curl به‌تنهایی قابل تعیین نیست. push/deploy یا تغییر DB انجام نشد.

## بررسی معرفی صوتی، تلفظ و تأخیر — 2026-10-07

بررسی کد روی dev: appsettings مشترک AI:Provider=Liara، STT=openai/gpt-transcribe، LLM=openai/gpt-5.6-luna و TTS=openai/tts-1-hd با nova و speed=1 دارد؛ override مقصد بررسی نشد. یادداشت‌های قدیمی NetArz پیش‌فرض وضعیت فعلی فایل نیستند. معرفی کارجو فقط متن stage/advance می‌پذیرد و canRecord در CandidateIntroduction=false است؛ مسیر آپلود/STT معرفی و صوت stageMessage وجود ندارد. معرفی شرکت مرحله persistشده و متن snapshot دارد، اما audio URL مرحله ارائه نمی‌شود؛ Angular در این ریپو نیست.

مسیر پاسخ STT سپس LLM و Map(generateAudio=true) است؛ cache miss تولید کامل TTS سؤال بعدی را پیش از برگشت پاسخ منتظر می‌ماند. ResponseHeadersRead داخل provider به معنی streaming به کلاینت نیست؛ پاسخ کامل در MemoryStream جمع می‌شود. cache scoped صوت و durationMs عملیات stt/llm/tts از قبل وجود دارند. مستر پرامپت فقط به LLM می‌رسد؛ درخواست TTS فقط model/input/voice/speed/response_format دارد. طبق مستند رسمی OpenAI، instructions در tts-1-hd پشتیبانی نمی‌شود؛ پشتیبانی مدل جایگزین در Liara آزمایش نشده. اصلاح تلفظ وابسته به جمله، آماده‌سازی صوت ثابت پیش از نوبت و جداسازی آماده‌شدن صوت از پاسخ API گزینه‌های بررسی‌اند، هنوز پیاده نشده‌اند. context رزومه/بانک/گفتگو برای کاهش تأخیر حذف نشود.

تنظیمات، کلیدها و کد runtime تغییر نکردند؛ build نهایی صفر warning/error و 89/89 تست provider/selection/audio/rules موفق، صفر Skip. اجرای نخست build به‌علت تداخل با build تست قفل فایل داشت؛ اجرای ترتیبی مجدد موفق شد. PostgreSQL و AI واقعی و زمان‌های مقصد در این بررسی آزمایش نشدند؛ علت غالب تأخیر بدون durationMs مقصد معلوم نیست. push/deploy/تغییر production انجام نشد.

## تکمیل معرفی صوتی و صوت مرحله‌ها — 2026-10-07: Verified محلی

طبق قرارداد فرانت روی dev، POST multipart introduction بدون questionId با audio/clientTurnId الزامی و durationSeconds/lastSpeechEndUtc/video اختیاری اضافه شد. InterviewIntroduction مستقل، unique جلسه و FK مرکب سازمان/جلسه، RowVersion و lease، فایل خصوصی، replay بدون STT دوباره، retry بدون upload و recovery worker دارد. transcript معتبر تنها شرط انتقال به Questions است؛ متن خالی یا شکست STT fallback ساختگی ندارد. معرفی progress/evidence/نمره/زمان مالی سؤال ایجاد نمی‌کند؛ transcript در context و تاریخچه داوطلب و HR موجود است و صوت/ویدیوی HR scoped و زیر retention قابل دریافت‌اند.

stageAudioUrl برای Welcome/درخواست معرفی/CompanyIntroduction/Goodbye فقط در مرحله جاری با Bearer دعوت ارائه می‌شود؛ GET در cache miss صوت را lazy تولید می‌کند، TTS ناموفق 503 و مرحله/متن محفوظ است. شرکت پیش از پایان سؤال‌ها 404 دارد؛ ادامه بعد از playback با تأیید فرانت است. backgroundRetryPending پاسخ و معرفی در lease فعال و backoff true می‌ماند؛ پس از موفقیت/آخرین شکست false. مسیر معرفی متنی قدیمی محفوظ است ولی معرفی صوتی پذیرفته‌شده را bypass نمی‌کند. [VOICE_INTRODUCTION_API](VOICE_INTRODUCTION_API.md) قرارداد نهایی OpenAPI/فرانت است. یادداشت‌های قبلی «STT معرفی وجود ندارد / canRecord معرفی=false» وضعیت قبل از این تغییرند.

build نهایی صفر warning/error؛ 198/198 تست PostgreSQL 18 اختصاصی محلی موفق، صفر شکست/Skip، شامل شش تست جدید معرفی/replay/ویدیو/تاریخچه/رسانه HR و scope/constraint، lease فعال و جلوگیری از bypass، recovery worker واقعی/backoff، خروجی STT خالی و سقف تلاش/retry، قطع درخواست و رقابت claim، و multipart/OpenAPI. tests/HireYar.Tests/TestResults/voice-introduction-final.trx. migration 20261007151257_AddVoiceIntroduction تولید و در fixture واقعی اعمال شد؛ HasPendingModelChanges=false در تست. delta idempotent artifacts/voice-introduction-migrations.sql دو بار روی baseline اختصاصی قبل از migration موفق و فقط یک history entry داشت؛ baseline این بررسی بدون داده legacy بود، بازنویسی داده تاریخی در migration وجود ندارد. downgrade در حضور معرفی صریحاً رد می‌شود.

باقی: مصرف قرارداد جدید در Angular، اعمال migration و بررسی فعال‌بودن worker در مقصد با مجوز مستقل، آزمون فارسی/STT/TTS/سکوت/VAD و دوربین واقعی. بهینه‌سازی streaming/آماده‌سازی سؤال‌ها و واژه‌نامه تلفظ در این تغییر پیاده نشده‌اند؛ مدل/کلید/appsettings دست‌نخورده‌اند. AI واقعی، push/deploy/commit و تغییر production انجام نشده‌اند. داده تست مصنوعی و SQL فقط محلی بود.

## ارسال اختیاری دعوت — 2026-10-08

روی dev، ارسال SMTP ایمیل و SMS پترن کاوه‌نگار در دعوت ویزارد/قدیمی/resend اضافه شد. InterviewNotifications:Email/Sms فلگ/config مستقل؛ SendEmail/SendSms پیش‌فرض false. پترن دعوت مستقل، token=کد شش‌رقمی و token2=لینک کامل؛ ApiKey خالی SMS از کلید OTP استفاده می‌کند. SMTP UTF-8 شامل نام/شغل/کد/لینک/انقضای UTC است. هر دو فلگ مشترک فعلاً false؛ کلیدهای قبلی محفوظ‌اند. ارسال بعد از commit، خارج قفل مالی و با نتیجه مستقل هر کانال انجام می‌شود؛ خطا لینک را rollback نمی‌کند. بدون retry/صف/ذخیره وضعیت تحویل یا migration؛ قطع فرایند پس از commit ممکن است ارسال را ناتمام بگذارد.

[INTERVIEW_NOTIFICATION_API](INTERVIEW_NOTIFICATION_API.md) قرارداد فرانت و appsettings است. Angular در ریپو نیست؛ دو تیک باید به sendEmail/sendSms وصل شوند. ارسال واقعی یا پذیرش لینک کامل در پترن مقصد تست نشده؛ پترن و SMTP را کاربر تنظیم می‌کند. NotConfigured/NotRequested/Sent/Failed فقط نتیجه همان درخواست‌اند، Sent تأیید دریافت provider است نه تحویل نهایی. ساخت دعوت برای retry ارسال تکرار نشود؛ resend لینک قبلی را باطل می‌کند.

Verification: build صفر warning/error؛ suite نهایی 240/240 موفق، صفر Failed/Skipped، PostgreSQL 18.3 اختصاصی loopback:55473 و provider جعلی، SMTP فقط loopback. tests/HireYar.Tests/TestResults/invitation-notifications-verified.trx. دوازده case مستقل اعلان و یک integration دعوت/commit/resend اضافه شدند؛ شکست اولیه فقط assertion متن ایمیل Base64 بود، رمزگشایی تست اصلاح و suite کامل بازاجرا شد. تغییرات موجود docs و InterviewReportService دست‌نخورده حفظ شدند. commit/push/deploy/production/ارسال واقعی انجام نشد.
