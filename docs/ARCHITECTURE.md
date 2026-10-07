## ADR-024 — معرفی‌ها، context دعوت و recovery پایدار (2026-10-07)

طبق درخواست مستقل کاربر، پیام معرفی کارجو با متن پیش‌فرض و معرفی شرکت با پرچم‌های مستقل در قالب و snapshot دعوت افزوده شدند. Stage مستقل از InterviewStatus مالی/پردازش، ترتیب معرفی‌ها را persist می‌کند؛ معرفی شرکت فقط پس از پایان سؤال‌ها عرضه می‌شود. مرحله‌های ثابت با تأیید کلاینت جلو می‌روند؛ متن معرفی کارجو بدون امتیاز مهارتی به context می‌رسد. مسیر بدون معرفی و جلسات تاریخی آغاز قبلی را حفظ می‌کنند. [INTERVIEW_CONTEXT_API](INTERVIEW_CONTEXT_API.md) قرارداد فرانت است.

مستر پرامپت v2 در دعوت snapshot و در system message LLM با schema معتبر ترکیب می‌شود؛ JD، متن رزومه، زبان/نوع، بانک/shortlist، وضعیت/زمان و گفتگو داده untrusted هستند. PDF/DOCX محلی با abstraction IResumeTextExtractor استخراج و snapshot می‌شوند؛ فایل غیرقابل استخراج متن صریح لازم دارد، بدون OCR/ارسال فایل خام. PdfPig فقط Infrastructure است. دعوت فایل‌دار بدون متن معتبر رد می‌شود. مسیر legacy بدون فایل/رزومه همچنان null دارد و backfill تاریخچه انجام نمی‌شود.

تعامل محدود با همان سؤال و answer مستقل، IsInteraction و AssistantMessage ذخیره می‌شود؛ unique partial یک پاسخ نهایی/درحال‌پردازش برای هر سؤال، unique clientTurnId و RowVersion محفوظ‌اند. فقط پاسخ نهایی نمره/evidence دارد و progress نهایی را می‌شمارد؛ schema درخواست AI اجازه گسترش shortlist یا نمره در تعامل ندارد. عمومی سؤال ساده را به ترتیب و بدون sufficient-evidence completion می‌پرسد؛ موضوعی عمومی یک سطح متوسط در هر موضوع با ترتیب ثابت. سیاست تخصصی موضوعی پذیرفته‌شده حفظ شده است. زمان مالی هر نوبت از window محدود سرور و زیر سقف کل جلسه است؛ retry مصرف تازه ثبت نمی‌کند.

BackgroundService در Infrastructure فقط recovery پاسخ پذیرفته‌شده را poll می‌کند؛ queue توزیع‌شده یا Repository جدید نیست. NextRecoveryAtUtc/RecoveryAttempts و lease در DB هستند؛ claim با pair سازمان/جلسه و قفل کوتاه سازمان، recheck دسترسی/billing و commit پیش از network انجام می‌شود. STT شکست‌خورده تا سه تلاش خودکار، سپس retry دستی؛ LLM unavailable تا سقف تلاش و سپس fallback بدون شواهد ساختگی. لغو درخواست/فرایند با lease منقضی قابل recovery است. متن transcript آماده حفظ و دوباره STT نمی‌شود؛ رقابت workerها همان policy RowVersion/lease را دارد. schema قدیمی migration لازم دارد؛ downgrade در حضور تعامل ثبت‌شده به‌جای حذف تاریخچه رد می‌شود.

## تکمیل ADR-017 — انتخاب provider AI (2026-10-07)

انتخاب AI:Provider در composition Infrastructure به client مستقل NetArz/Liara/Disabled وصل می‌شود؛ هر provider options مستقل دارد. به دلیل قرارداد wire یکسان OpenAI-compatible، پردازش HTTP، محدودیت پاسخ، cancellation و logging امن در base داخلی مشترک‌اند و کلاس concrete هویت provider را تعیین می‌کند. فقط تنظیمات provider فعال و lease متناسب با timeoutهای آن اعتبارسنجی می‌شوند؛ Disabled کلید یا HttpClient ندارد. Application و API همچنان فقط interfaceهای موجود را مصرف می‌کنند. provider ناشناخته startup error است؛ fallback به provider دیگر یا retry خودکار تماس خارجی اضافه نشده. تنظیمات و کلیدهای موجود دست‌نخورده‌اند و اتصال واقعی Liara هنوز تأیید نشده است.
# معماری و تصمیم‌ها

## ADR-023 — پاسخ نوبتی با ویدیو و شناسه کلاینت (2026-10-06)

طبق قرارداد ارسالی فرانت، جریان HTTP فعلی با clientTurnId، lastSpeechEndUtc، durationSeconds ذخیره‌شده و ویدیوی اختیاری گسترش یافت؛ کاربر انتخاب اولیه WebRTC را تصحیح و Realtime را از این درخواست حذف کرد. سرویس/provider جدید بلادرنگ اضافه نشده. زمان پایان speech و مدت کلاینت metadata هستند و سیاست مالی محدود سرور تغییر نکرده است.

clientTurnId رشته opaque تا ۱۲۸ کاراکتر و در هر جلسه unique با partial index PostgreSQL است؛ null برای کلاینت قدیمی مجاز. replay همان شناسه/سؤال همان answer با وضعیت جاری می‌دهد و پردازش/هزینه دوباره ندارد؛ استفاده برای سؤال دیگر conflict است. upload/network بیرون transaction، persistence با قفل سازمان موجود، recheck دسترسی/مالی/state پس از قفل و optimistic RowVersion برای processing حفظ‌اند. نخستین commit برنده و فایل‌های بازنده حذف می‌شوند؛ retry پردازش همچنان مسیر answer.id مستقل است.

storage خصوصی موجود category=videos را با type/size metadata و constraint کامل‌بودن سه فیلد می‌پذیرد؛ WebM/MP4 با signature محدود، بدون decode یا ارسال ویدیو به AI. دانلود فقط HR scoped زیر retention موجود و range processing است؛ global filter یا Repository تازه نداریم. mediaCapabilities در پاسخ candidate و videoAvailable مبتنی بر وجود فایل در لیست پنل‌اند. transcript خود داوطلب و messages فقط سؤال‌های مطرح‌شده/پاسخ‌های همان جلسه را عرضه می‌کنند؛ timestamp سؤال هنگام مطرح‌شدن persist و برای legacy از زمان سرور fallback می‌شود، نه backfill حدسی. Swagger فرم و فایل‌های binary را صریح توصیف می‌کند. [CANDIDATE_TURN_API](CANDIDATE_TURN_API.md) قرارداد فعلی فرانت است.

## ADR-022 — بانک موضوعی پیشرفته و پیشنهاد گذرای AI (2026-10-06)

ساختار گروهی opt-in در ویزارد است؛ قابلیت‌های موجود advanced_ai_workflow/adaptive_questions/adaptive_difficulty تعیین دسترسی می‌کنند، نه package code. قالب‌های ساده و جلسات قدیمی تغییر خودکار نمی‌کنند. گروه با TopicOrder/TopicName و Difficulty=1/3/5 روی سؤال ذخیره می‌شود؛ unique سطح در گروه و check PostgreSQL، اعتبارسنجی بانک کامل در Application. TopicOrder ترتیب آرایه و نام موضوع یکتا است؛ موجودیت اضافی برای گروه بی‌متادیتا لازم نیست. UseTopicQuestions قالب و AskOneQuestionPerTopic جلسه، snapshot ساختار را مستقل از ویرایش آینده نگه می‌دارند. Domain و Policy یک سؤال در هر موضوع را enforce می‌کنند؛ هر موضوع skill snapshot مستقل و سطح هدف از میانگین شواهد معتبر موضوع‌های قبلی، شروع متوسط. پیشنهاد AI صرفاً پاسخ گذرا، از JD scoped، با JSON محدود/اعتبارسنجی و تأیید صریح در save است؛ network زیر قفل سازمان نیست. قراردادهای مالی immutable و قابلیت جلسه پس از شروع از خرید billing خودش باقی‌اند. [ADVANCED_QUESTIONS_API](ADVANCED_QUESTIONS_API.md).


## ADR-021 — حساب موبایلی تک‌کاربر و حذف منطقی پنل (2026-10-06)

طبق درخواست کاربر، API ایمیل/رمز و تیم و سرویس SMTP حذف شدند. Identity برای هویت/refresh باقی است. JWT/refresh/OTP فقط Owner فعال با موبایل تأییدشده را می‌پذیرند؛ DB unique سازمان برای IsActive و check نقش Owner مانع چند حساب فعال و Recruiter فعال‌اند. migration دسترسی Recruiter و refresh/invitation معلق را بازنشسته می‌کند ولی سابقه را پاک نمی‌کند. Entity/enum تاریخی برای سازگاری schema باقی است. انتقال Owner ایمیلی به شماره باید با بررسی مالکیت همان حساب باشد، نه تطبیق حدسی یا انتقال tenant.

DeletedAtUtc از archive مجزاست؛ فیلترها در Application صریح‌اند و global filter نداریم، تا FK و مالی تاریخچه همچنان دیده شوند. حذف موقعیت قالب‌های خودش را در همان transaction پنهان می‌کند؛ دعوت قبلی snapshot دارد. ثبت قالب/دعوت و حذف زیر قفل موجود سازمان سریال‌اند؛ upload/network خارج transaction دعوت می‌ماند. ReplaceQuestions در transaction caller شرکت می‌کند و فقط transaction خودش را commit می‌کند؛ Repository جدید نیست.

حذف جلسه زیر قفل مالی، دسترسی و lease را باطل، جلسه غیرنهایی را Cancel و مصرف پاسخ persistشده را تسویه می‌کند؛ باقی رزرو آزاد و history immutable است. پاسخ‌ها پس از گرفتن قفل بارگذاری می‌شوند تا انتظار قفل موجب تسویه داده کهنه نشود. حذف فیزیکی فایل/هویت یا restore اضافه نشد؛ این حذف از پنل است. cancel/revoke قدیمی مستقل‌اند. Swagger فارسی/tenant 404 حفظ و counters تیم از DTO داشبورد حذف شدند.

## ADR-020 — اشتراک و اعتبار پنل و ارتقای روزشمار (2026-10-05)

درخواست مستقل `subscription_credit.html` روی برنچ مشتق از panel-wizard اجرا شد. APIهای panel، quote/checkout، financial-history و invoice داده چهار تب را می‌دهند؛ Controller → Application Service → EF abstraction و SQL فقط Infrastructure باقی‌اند. کاتالوگ پویا و feature-based است؛ ارتقا با حفظ تمام قابلیت‌ها و افزایش امکانات/محدودیت/retention تشخیص داده می‌شود، نام پکیج شرط runtime نیست. قیمت دوره قبلی final بعد از discount است؛ tax تنظیمی پیش‌فرض ۱۰٪ فقط checkout جدید و بر خالص پس از credit اعمال می‌شود. fingerprint محاسبه سرور تغییر قیمت/صف/اعتبار/tax بین preview و checkout را 409 می‌کند؛ داده مالی از client authoritative نیست.

فرض کاری ارتقا: شروع دوره کامل جدید در verification و کسر ارزش روزهای کامل باقی‌مانده snapshot خالص قرارداد قبلی، floor تومان و بدون بازپرداخت نقدی. اعتبار غیرنقدی InternalTestGrant به اعتبار روزشمار تبدیل نمی‌شود. صف اشتراک/پرداخت Pending پیش از ارتقا باید تعیین تکلیف شود؛ pending upgrade مانع checkout اشتراک دیگر از هر دو مسیر جدید و قدیمی است. انقضای نمایشی به معنی تأیید عدم پرداخت نیست. callback/verify معتبر همان quote ثابت را fulfill می‌کند؛ pendingهای نامعلوم recovery درگاه می‌خواهند.

OrganizationSubscription و SubscriptionPurchase قبلی همچنان immutable‌اند. SubscriptionReplacement رکورد append-only با FK مرکب سازمان، unique source/payment و زمان قطع دسترسی مؤثر است؛ trigger، payment Paid و quote جایگزین را بررسی و وجود اشتراک جدید را در commit enforce می‌کند. trigger هم‌پوشانی از effective end (کمینه expiry و replacement) استفاده می‌کند. Current entitlement رکورد پایان‌یافته را حذف می‌کند؛ billing جلسات شروع‌شده همچنان purchase اصلی را می‌خواند. قفل سازمان، transaction کوتاه و عدم network داخل transaction حفظ‌اند.

گزارش مالی یک projection SQL از پرداخت Paid نقدی و ledger بدون duplicate CreditPurchase است؛ زمان payment=PaidAtUtc، مصرف=تسویه ledger، مجموع‌ها پیش از page و ارزها مستقل‌اند. InternalTestGrant درآمد نقدی نیست؛ adjustment/هدیه در category مستقل تراکنش می‌ماند. کارت اعتبار زمان را از AvailableCredit و rate قرارداد جاری تخمین می‌زند؛ بدون قرارداد rate/time=null. BillableSeconds محدود سرور ثابت ماند و decoded audio ادعا نمی‌شود. Invoice داده JSON از snapshot است؛ PDF یا adapter پرداخت واقعی اضافه نشده. مسیرهای قبلی compatible و Swagger فارسی باقی‌اند.

## ADR-019 — قرارداد ساده ویزارد پنل (2026-10-05)

براساس HTML ارسالی و درخواست مستقل کاربر، مسیرهای `api/panel-wizard` ورودی ساده موقعیت، قالب متنی و دعوت multipart دارند؛ مسیرهای قدیمی سازگار باقی‌اند. Service → IApplicationDbContext حفظ شده، Repository/MediatR یا persistence draft اضافه نشده. فرانت شناسه و داده مراحل را نگه می‌دارد. بانک UI همان مجموعه سؤال‌های قالب است؛ HR نیازی به تعریف مهارت یا بانک موقعیت ندارد. موتور موجود با یک مهارت snapshot محلی «ارزیابی شغلی» کار می‌کند؛ سؤال/ترتیب/شرح شغل/پیام‌های قالب در صدور دعوت ثابت‌اند. نوع دعوت مستقل از نوع پیشنهادی قالب، با entitlement موجود بررسی می‌شود؛ Billing تغییری ندارد.

رابطه مرکب قالب/موقعیت تصمیم پذیرفته‌شده باقی است: انتخاب قالب همان سازمان برای موقعیت دیگر در دعوت یک کپی می‌سازد، مبدأ تغییر نمی‌کند. کارجو، جلسه و کپی اختیاری در یک SaveChanges ثبت می‌شوند. رزومه از abstraction storage خصوصی موجود با category مستقل resumes استفاده می‌کند، bounded size و signature/type بررسی می‌شود و دانلود فقط HR scoped است. فایل تازه در شکست persistence حذف می‌شود؛ نام اصلی کاربر ذخیره نمی‌شود. این اعتبارسنجی اسکن ضدبدافزار نیست و parsing/ارسال رزومه به AI اضافه نشده است.

Resend فقط Created و غیرلغوشده است؛ token hash و expiry را با RowVersion جدید عوض می‌کند. توکن خام دوباره فقط در پاسخ caller HR عرضه می‌شود. delivery ایمیل/SMS صریحاً NotConfigured است؛ بدون adapter لینک/پیام تازه ادعای ارسال نمی‌شود. ویدیو همچنان وجود ندارد. فهرست وضعیت مؤثر expiry را زمان خواندن محاسبه و score فقط با evidence واقعی عرضه می‌کند. جستجو/فیلتر/صفحه‌بندی در SQL و mapping دستی‌اند. قرارداد جدید [PANEL_WIZARD_API](PANEL_WIZARD_API.md) مرجع فرانت جدید است؛ قرارداد صوتی قبلی معتبر می‌ماند.

## ADR-018 — Pricing، اشتراک و اعتبار مستقل AI (2026-10-01)

درخواست مستقل Pricing روی `codex/pricing` پس از fast-forward به `origin/dev` (`d9d4d72`) اجرا شد؛ مصاحبهٔ موجود همان پایه حفظ شد. پکیج/دوره/feature/محصول اعتبار entity پویا در PostgreSQL هستند، نه enum پکیج یا شرط Basic/Advanced. seed فقط migration یک‌باره است. مسیر Service → IApplicationDbContext و لایه‌ها حفظ شد؛ SQL/locking/query splitting در Infrastructure می‌ماند. `PricingCatalog` فقط سیاست خواندن تک‌statement قرارداد را تأمین می‌کند؛ Repository جدید نیست.

SubscriptionPurchase و entitlements در checkout immutable snapshot می‌شوند؛ تغییر قیمت/قابلیت آینده قرارداد پرداخت قدیمی یا pending را تغییر نمی‌دهد. fulfillment پس از verification مستقیم درگاه، اشتراک را اکنون یا انتهای صف اشتراک فعلی زمان‌بندی می‌کند. بازه‌ها با UTC و AddMonths هستند؛ وضعیت مؤثر از زمان خوانده می‌شود. snapshot نرخ/retention و reference اشتراک در start مصاحبه تثبیت می‌شود و پایان اشتراک مانع پایان مصاحبهٔ شروع‌شده نیست. capabilityهایی چون analysis/adaptive/difficulty در پردازش از همین قرارداد خوانده می‌شوند.

IRT تومان مستقیم، USD واحد accounting کیف پول است. محصولات اعتبار مبلغ فروش و مقدار USD صریح دارند و نسبت مؤثر قرارداد snapshot می‌شود؛ forex زنده یا نرخ مشترک اضافه نشد. ledger immutable با compensating entry و Balance cache، reservation کامل حداکثر هزینه و unique reference مالی دارد. تمام mutationهای مالی و read مرکب wallet زیر قفل FOR UPDATE سازمان و transaction هستند؛ DB triggerهای deferred تطابق ledger/balance/reservations و trigger عدم هم‌پوشانی اشتراک را enforce می‌کنند. FK مرکب tenant و Restrict تاریخچه را حفظ می‌کنند.

مدت مالی نسخه اول window پاسخ‌گویی سرور بین آماده‌شدن سؤال پس از TTS و رسیدن پاسخ است، با floor ثانیه و سقف ۱۲۰ ثانیهٔ هر پاسخ و سقف کل جلسه. پاسخ بی‌پاسخ و processing/retry زمان مالی ندارند؛ DurationSeconds کلاینت حقیقت مالی نیست. هزینهٔ نهایی decimal با شش رقم و ToEven گرد می‌شود. این قاعده زمان دقیق صوت را ادعا نمی‌کند؛ جزئیات محدودیت و توسعهٔ decoder/capture در [BILLING](BILLING.md) است.

IPaymentGateway فقط abstraction Application و Disabled implementation runtime دارد؛ fake فقط تست. callback صرفاً محرک verify است و success/price از client نمی‌گیرد. DB unique Provider/TransactionId، PaymentId fulfillment و reference ledger به همراه قفل سازمان، تکرار callback را بی‌اثر می‌کنند. حداقل مرز مدیریت پلتفرم IsSystemAdmin از DB است؛ JWT claim آن در هر درخواست بازسازی و ثبت‌نام/profile هرگز آن را نمی‌پذیرند. Owner/Recruiter مدیر پلتفرم نیستند.

retention دسترسی دانلود رسانه را از snapshot کنترل می‌کند؛ حذف فیزیکی و shared-cache cleanup عملیاتی‌اند. expiry رزرو به صورت lazy در wallet/start/adjustment تسویه می‌شود؛ worker/زیرساخت آینده ساخته نشد. proration، refund درگاه و payment adapter واقعی کار مستقل‌اند. مستندات audit پایه دربارهٔ خالی‌بودن ApiKey با source دریافتی اختلاف داشت؛ default در این تغییر خالی و الزام کلید دوباره تست شد، secret محلی تغییر نکرد. تصمیم‌های قدیمی دربارهٔ موکول‌بودن پرداخت اکنون برای این درخواست مستقل superseded هستند.

## ADR-017 — اجرای مصاحبه با persistence و provider (2026-10-01)

Domain اولیه با constructor خصوصی و backing field به EF متصل شد؛ QuestionSnapshot/SkillSnapshot/SkillState/SkillEvidence/CoveredTopic entity رابطه‌ای مستقل‌اند، نه JSON و نه owned navigation. این انتخاب drift مشاهده‌شدهٔ snapshot حذف ownedها را رفع کرد و FK بین answer/question/skill را صریح کرد. tenant-owned CandidateApplication/Skill/PositionSkill/BankQuestion/Session با کلیدهای مرکب به موقعیت/قالب/مهارت همان سازمان وصل‌اند؛ همهٔ delete behaviorها Restrict است.

بانک موقعیت مستقل از editable questionهای قالب است؛ template feature و delete/insert آن تغییر نمی‌کنند. متن/موضوع/مهارت/difficulty و plan در زمان **صدور دعوت** snapshot می‌شوند، سپس start همان plan را فعال می‌کند. این تثبیت زودتر از start مانع تغییر بی‌صدای دعوت پس از ویرایش HR می‌شود. بانک می‌تواند تغییر کند اما session فقط snapshot خودش را ارزیابی می‌کند.

Candidate token تصادفی ۲۵۶بیتی با SHA-256 ذخیره، expiry و revoke است. scheme اختصاصی Interview در Api فقط یک claim شناسه جلسه صادر می‌کند و CurrentAccountValidation HR را استفاده نمی‌کند. HR policy همچنان JWT معتبر و نقش server-side دارد؛ توکن candidate دسترسی HR ندارد. توکن از Authorization header می‌آید و در URL/log قرار نمی‌گیرد. invite token فقط یک بار در پاسخ HR عرضه می‌شود؛ ارسال پیام دعوت اضافه نشده.

صدای پاسخ و سؤال در storage خصوصی خارج wwwroot ذخیره می‌شود؛ key و signature و حجم enforce می‌شوند و دانلود از endpoint scoped است. cache سازمانی صوت با hash متن normalize/model/voice/speed میان جلسه‌ها reuse می‌شود. TTS ناموفق integrity جلسه را خراب نمی‌کند و متن برمی‌گردد. concurrent cache miss می‌تواند درخواست تکراری ایجاد کند ولی unique cache key یک winner نگه می‌دارد. Local storage برای استقرار تک instance نیازمند volume خصوصی پایدار است.

پاسخ مستقل unique session/question است. processing state و transcript و diagnostics persist می‌شوند. Session lease با expiry و optimistic RowVersion ۱۶بایتی در هر stage نوشته می‌شود؛ stale lease با retry همان پاسخ قابل بازیابی است. worker قبلی پیش از commit بعد از network، lease معتبر را دوباره بررسی می‌کند. transaction طولانی، worker پس‌زمینه یا distributed lock اضافه نشده است. قطع درخواست می‌تواند تا انقضای lease processing بماند؛ current امکان retry را مشخص می‌کند.

AI abstraction در Application، NetArz typed HttpClientFactory و options/timeouts در Infrastructure است؛ key خالی با NetArz در startup رد می‌شود و Disabled فراخوانی خارجی ندارد. LLM advisory است؛ shortlist دقیق همان attempt اعتبارسنجی می‌شود، policy پس از evidence دوباره hard limits/coverage را اعمال می‌کند. invalid JSON/401/429/5xx/timeout → deterministic fallback بدون evidence نامعتبر. خطای STT → retryable همان صوت. score evidence و justification کوتاه قابل مشاهده‌اند؛ hidden reasoning و hiring/rejection خودکار وجود ندارند. هیچ transaction هنگام AI باز نیست و retry خودکار POST خارجی انجام نمی‌شود.

metricها فقط provider/source کم‌تنوع دارند؛ شناسه‌ها فقط در structured log مجاز، بدون transcript/audio/token/key. مدت واقعی صوت بدون decoder تأیید نمی‌شود؛ سقف حجم و مدت کلی server-side هستند و duration client صرفاً محدودیت کمکی است. این محدودیت و سیاست عملیاتی retention/orphan cleanup در INTERVIEWS ثبت شده‌اند.

## ADR-016 — شروع هستهٔ مصاحبه (2026-10-01)

در پاسخ به درخواست مستقل Candidate AI Interview، Domain/Interviews و سرویس policy/validator در Application اضافه شدند؛ تصمیم پیشین درباره موکول‌بودن AI به تسک مستقل اکنون برای این درخواست فعال شده است. معماری Service → IApplicationDbContext و پوشه‌بندی مسئولیتی حفظ می‌شود. مدل جلسه snapshot سؤال/مهارت را مستقل نگه می‌دارد تا جایگزینی سؤال‌های قالب، تاریخچه جواب و evidence را تغییر ندهد. core score/confidence/coverage برابر ۰..۱ و delta برابر -۱..۱ است؛ delta ذخیره‌شده اثر واقعی پس از clamp است. topic با SkillId شناسایی می‌شود.

LLM فقط decision support است؛ shortlist و completion از policy می‌آیند. validation همه assessment را قبل از mutation بررسی می‌کند؛ پاسخ نامعتبر evidence نمی‌سازد و fallback قطعی دارد. evidence فعلاً به مهارت و موضوع همان سؤال محدود می‌شود. prompt نسخه v1 صرفاً شواهد شغلی می‌خواهد و تصمیم استخدام یا hidden reasoning نمی‌خواهد.

این تصمیم فعلاً فقط در Domain/Application پیاده شده است. persistence، schema، candidate auth و token scope، audio storage، NetArz و HTTP endpoints هنوز آماده نیستند. constructorهای مدل‌ها باید هنگام EF integration با materialization سازگار شوند. تمام tenant ownership از Application و identity/token معتبر سرور resolve خواهد شد؛ Guidهای constructor ورودی HTTP قابل اعتماد محسوب نمی‌شوند.

## تغییر احراز هویت موبایلی (در حال انتقال، 2026-09-27)

تنظیمات ارسال در بخش `Kavenegar` فایل `appsettings.json` تعریف شده‌اند: `ApiKey` و `Template` خالی هستند تا کلید واقعی در Git قرار نگیرد. `UseFixedOtp` در تنظیمات مشترک false است؛ در `appsettings.Development.json` مقدار true و `FixedOtpCode` برابر `123456` است. وقتی فلگ فعال باشد، همان کد شش‌رقمی برای challenge ذخیره می‌شود و هیچ درخواست SMS انجام نمی‌شود. اعتبار پنج دقیقه، محدودیت ۶۰ ثانیه، پنج تلاش و مصرف یک‌باره در هر دو حالت برقرارند. هنگام فعال‌سازی، کد باید دقیقاً شش رقم ASCII باشد و در startup بررسی می‌شود. مقدارهای محیطی همچنان می‌توانند appsettings را override کنند.

مسیر جدید `POST /api/auth/otp/request` و `POST /api/auth/otp/verify` شماره موبایل ایران با قالب `09` و ۱۱ رقم را می‌پذیرد. کد شش‌رقمی با CSPRNG تولید و فقط HMAC-SHA256 آن با کلید امضای سرور در PostgreSQL ذخیره می‌شود. اعتبار پنج دقیقه، فاصله درخواست ۶۰ ثانیه برای هر موبایل و حداکثر پنج تلاش اشتباه است. مصرف challenge زیر قفل `FOR UPDATE` انجام می‌شود. کاوه‌نگار VerifyLookup از Infrastructure با HTTPS و `receptor`/`token`/`template` فراخوانی می‌شود؛ کلید API و قالب از `Kavenegar__ApiKey` و `Kavenegar__Template` خارج Git می‌آیند. چون کلید در URL سرویس‌دهنده است، این HttpClient از logging خودکار URI استفاده نمی‌کند. شکست ارسال پاسخ 503 می‌دهد و متن کد یا شماره در لاگ ثبت نمی‌شود.

پس از OTP معتبر، حساب موبایلی موجود وارد می‌شود؛ در نبود آن، Owner و سازمان اولیه در یک تراکنش ساخته و نشست JWT/refresh صادر می‌شود. Owner نام سازمان را با `PUT /api/organization` تکمیل می‌کند. ایمیل برای حساب جدید موبایلی null است و پاسخ پروفایل/اعضا فیلد موبایل دارد. migration `AddMobileOtp` ستون‌های ایمیل را nullable، موبایل را unique و جدول challenge را اضافه می‌کند. مسیرهای ایمیل/رمز و دعوت Recruiter فعلاً برای جلوگیری از قطع حساب‌های موجود حفظ شده‌اند؛ حذف آن‌ها به تصمیم مهاجرت حساب‌های فعلی وابسته است. این تصمیم موقت جایگزین ADR-010 درباره نبود OTP شده و مستلزم بازنویسی نهایی آن پس از تعیین تکلیف مهاجرت است.

## تصمیم موقت ثبت‌نام بدون SMTP

گزینهٔ `Security:AutoConfirmEmailOnRegistration` هنگام ثبت‌نام Owner جدید، `EmailConfirmed` را در همان تراکنش ساخت حساب برابر true می‌کند و ایمیل تأیید نمی‌فرستد. پیش‌فرض کد false است؛ فعلاً در `appsettings.json` مقدار true دارد. این گزینه تنها برای حساب‌های تازه است و حساب‌های قدیمیِ تأییدنشده را تغییر نمی‌دهد. پس از آماده‌شدن SMTP باید false شود تا جریان تأیید ایمیل دوباره فعال شود. تا آن زمان مالکیت نشانی ایمیل بررسی نمی‌شود.

## ساختار واقعی

`HireYar.slnx` شامل Domain (Organizations/JobPositions/InterviewTemplates)، Application (سرویس‌ها، DTO، validation و قراردادها)، Infrastructure (Identity، SQL/DbContext/mapping/migrations، JWT، SMTP، دعوت) و Api (Controller، HTTP، مجوزها، CSRF، composition) و `tests/HireYar.Tests` است.

وابستگی: Application → Domain؛ Infrastructure → Application/Domain؛ Api → Application و Infrastructure. مسیر درخواست Controller → Application Service → DbContext abstraction یا مرز Identity/SMTP است. سرویس Auth مرز Identity را فراخوانی می‌کند؛ UserManager و تراکنش‌های وابسته به آن در Infrastructure می‌مانند. HttpContext فقط در Api است.

## تصمیم‌های پذیرفته‌شده

| شناسه | تصمیم، دلیل و پیامد |
|---|---|
| ADR-001 | .NET 10، EF Core 10، PostgreSQL و Controller؛ یک برنامه و یک دیتابیس برای سادگی عملیاتی |
| ADR-002 | Clean Architecture سبک؛ EF DbSet در IApplicationDbContext مجاز است تا repository عمومی نسازیم؛ Application به EF وابسته می‌شود ولی به SQL provider نه |
| ADR-003 | Identity فقط Infrastructure؛ یک کاربر/سازمان و نقش enum محدود Owner/Recruiter؛ unique index ایمیل نرمال و Owner سازمان |
| ADR-004 | JWT با HS256، issuer/audience/expiry/jti؛ وضعیت و نقش در هر درخواست از DB دوباره خوانده می‌شود؛ هزینه یک query در مقابل اثر فوری مجوز |
| ADR-005 | refresh تصادفی ۲۵۶بیتی، ذخیره SHA-256، یک family در هر login؛ تمام mutationهای نشست زیر قفل SELECT FOR UPDATE کاربر و تراکنش؛ replay خانواده همان نشست را باطل می‌کند |
| ADR-006 | cookie host-only محدود به auth و CSRF استاندارد ASP.NET؛ SameSite Strict برای deployment زیر یک سایت؛ SPA توکن CSRF را با هویت فعلی تازه می‌کند |
| ADR-007 | queryها صریحاً OrganizationId سرور را شرط می‌کنند؛ query filter عمومی نداریم. کلید مرکب JobPosition/Organization در FK قالب از اتصال بین سازمان‌ها در DB جلوگیری می‌کند |
| ADR-008 | RowVersion تصادفی ۱۶بایتی application-managed و 409؛ ویرایش parent و جایگزینی سؤال‌ها تراکنشی، شناسه سؤال‌های باقی‌مانده حفظ می‌شود؛ delete/insert داخلی سؤال امکان جابه‌جایی ترتیب زیر unique index می‌دهد |
| ADR-009 | دعوت Recruiter با hash و انقضای سه‌روزه؛ قفل سازمان عملیات دعوت را سریالی می‌کند؛ unique index رقابت ایمیل و دعوت باز را نیز پوشش می‌دهد |
| ADR-010 | بدون MediatR/CQRS/Generic Repository/worker/OTP؛ توسعه آینده فقط با تسک مستقل |

همه Guid و تاریخ‌ها DateTimeOffset UTC هستند. cascade delete تمام روابط Restrict است. enumهای وضعیت و نقش در SQL به متن محدود و check constraint ذخیره می‌شوند. DTOها اجازه overposting ندارند و Entity مستقیم برنمی‌گردد. pagination پیش‌فرض ۲۰، سقف ۱۰۰ و ترتیب CreatedAtUtc سپس Id است.

حدود متن: نام شخص/سازمان/عنوان ۲۰۰، ایمیل ۲۵۶، شرح موقعیت ۱۰۰۰۰، seniority صد، language بیست، سؤال ۲۰۰۰، competency دویست، راهنمای ارزیابی ۴۰۰۰. رمز ۱۲ تا ۱۲۸ و الزامات استاندارد Identity. قالب ۱ تا ۳۰ سؤال، ترتیب پیوسته از ۱، زمان ۵ تا ۶۰ و follow-up صفر تا سه.

Identity token provider استاندارد با اعتبار دو ساعت و Data Protection دائمی استفاده می‌شود. تأیید دوباره ایمیل با flag و قفل رد می‌شود؛ reset با security stamp یک‌بارمصرف است. ساخت سازمان و Owner تراکنشی؛ ارسال ایمیل پس از commit است. شکست SMTP داده ناقص نمی‌سازد و resend ممکن است. forgot/resend همیشه پاسخ عمومی یکسان دارند.

ProblemDetails/ValidationProblemDetails با code و traceId؛ ۴۰۰ validation، ۴۰۱ authentication، ۴۰۳ role، ۴۰۴ رکورد خارج scope، ۴۰۹ conflict، ۴۲۹ rate limit، ۵۰۰ خطای عمومی. لاگ درخواست حساس یا body/token فعال نیست؛ رخداد ارسال ناموفق بدون recipient/link ثبت می‌شود.

ADR-013: Swagger UI و OpenAPI JSON در همه محیط‌ها بدون توکن در دسترس‌اند تا قرارداد API روی سرور قابل مشاهده باشد؛ Swagger middleware پیش از fallback authorization اجرا می‌شود. endpointهای داده و حساب همچنان سیاست احراز هویت و نقش خود را دارند. تنظیمات اتصال و کلید امضا برای هر محیط جداگانه و خارج Git نگهداری می‌شوند.

ADR-014: طبق انتخاب صریح کاربر، `KnownProxies` و تنظیم IP proxy حذف شد. یک middleware کوچک در Production فقط مقدار دقیق و یکتای `X-Forwarded-Proto: https` را برای scheme درخواست اعمال می‌کند؛ سایر مقادیر نادیده گرفته می‌شوند. `CookieSecurePolicy.Always` حفظ شد. این سادگی به معنی امکان جعل هدر توسط هر اتصال مستقیم به پورت داخلی است؛ دسترسی به آن پورت باید در شبکه یا firewall به proxy محدود شود. برای عیب‌یابی 500، نوع exception، inner type، stack trace و نشانه‌های بولی HTTPS/forwarded proto با traceId و بدون متن exception یا داده درخواست لاگ می‌شوند.

ADR-015: شناسهٔ `Activity.Current.Id` در پاسخ ProblemDetails و لاگ خطای 500 یکسان است؛ در نبود Activity از `HttpContext.TraceIdentifier` استفاده می‌شود. جزئیات exception در پاسخ عمومی منتشر نمی‌شوند. برای خطای PostgreSQL فقط SQLSTATE به لاگ ساخت‌یافته اضافه می‌شود. connection string و signing key در تنظیمات تحت Git نگهداری نمی‌شوند؛ Compose آن‌ها را از محیط استقرار می‌گیرد.

موضوع باز محیطی: صحت Docker و SMTP واقعی محلی تا اجرای آن‌ها قابل تأیید نیست؛ [STATUS](STATUS.md). روش حفاظت کلیدها و TLS میزبان production باید هنگام تسک deployment تعیین شود. این موارد تصمیم پیاده‌شده تلقی نمی‌شوند.

توسعه آینده AI و فایل از قرارداد مصرف‌شده Application و implementation Infrastructure انجام می‌شود؛ اکنون abstraction یا جدول آن‌ها نداریم. جلسه آینده به snapshot سؤال و معیار نیاز دارد؛ engine نسخه‌بندی اکنون ساخته نمی‌شود.


## پوشه‌بندی پس از بازآرایی

طبق انتخاب صریح کاربر، این تغییر فقط سازمان‌دهی فایل‌هاست؛ مسیر Service → IApplicationDbContext حفظ شده و Repository جدید ساخته نشده است.

```text
src/
  HireYar.Api/
    Controllers/       # هر شش کنترلر
    DTOs/              # پاسخ‌های مخصوص HTTP
    Security/
    Errors/
  HireYar.Application/
    Services/          # Auth, Members, Organizations, JobPositions, InterviewTemplates, Dashboard
    DTOs/              # درخواست/پاسخ و validation بر اساس قابلیت
    Interfaces/        # IAccounts, ITeamMembers, ICurrentUser, IEmailSender, IApplicationDbContext
    Common/            # AppException, Page, PageQuery
  HireYar.Infrastructure/
    Services/          # Auth, Members, Email
    Auth/              # مدل Identity و تنظیمات امنیتی
    Members/           # مدل دعوت
    Persistence/       # DbContext، mapping و migrations
  HireYar.Domain/       # مدل‌های مستقل بر اساس قابلیت
```

ADR-011: پوشه‌ها بر اساس مسئولیت و سپس قابلیت مرتب شدند. namespaceهای موجود برای حفظ هویت typeها ثابت ماندند؛ نام پوشه الزاماً namespace نیست. API routeها، DI و مدل دیتابیس تغییر نکردند. سرویس/DTO/interface اکنون فایل مستقل دارند و AuthService دیگر داخل Contracts.cs نیست.


ADR-012: طبق درخواست کاربر، SQL Server با PostgreSQL و Npgsql EF Core 10.0.3 جایگزین شد؛ داده قبلی نیاز به انتقال ندارد. migration InitialPostgres مبنای دیتابیس خالی است. قفل user/organization در تراکنش Read Committed با SELECT FOR UPDATE تا commit حفظ می‌شود؛ خواندن توکن پس از گرفتن قفل انجام می‌شود. check constraint و partial indexها از شناسه‌های quoted PostgreSQL استفاده می‌کنند؛ FK مرکب و unique email حفظ شده‌اند.

RowVersion از نوع bytea و concurrency token مدیریت‌شده توسط AppDbContext است؛ هر insert/update موقعیت/قالب با Guid تصادفی جدید ۱۶بایتی ذخیره می‌شود. شرط UPDATE شامل نسخه اصلی است؛ stale version پاسخ 409 می‌دهد. استفاده از این روش نوع JSON قبلی Base64 را حفظ می‌کند؛ قرارداد طول از ۸ به ۱۶ بایت تغییر کرده است. تاریخ‌ها timestamptz و UTC هستند. خطای 23505 به conflict و 40P01/40001 به concurrency conflict نگاشت می‌شود.

## تنظیمات AI در appsettings — 2026-10-02

روی برنچ `codex/ai-appsettings` از `dev`، بخش `AI` در `src/HireYar.Api/appsettings.json` اضافه شد: Provider=NetArz، BaseUrl، مدل‌های STT/LLM/TTS، voice، speed و سه timeout. binding موجود `AI:NetArz` و انتخاب `AI:Provider` همین تنظیمات را مصرف می‌کنند؛ override محیطی/local همچنان اولویت دارد. ApiKey در محل فعلی و تمام فایل‌های کلید دست‌نخورده ماند؛ کلید تازه در appsettings اضافه یا کپی نشد. یادداشت‌های تاریخی Disabled برای پیش‌فرض مشترک دیگر وضعیت جاری این برنچ نیستند.

Build موفق با صفر warning/error؛ تست‌های InterviewProviderTests و InterviewAudioTests: ۴۳ موفق، صفر شکست/Skip. تست کلید خالی اکنون مقدار خالی را صریحاً فقط در fixture خود می‌دهد تا وابسته به default فعلی نباشد. تماس واقعی AI، تست PostgreSQL، migration، commit، push و deploy انجام نشدند.
