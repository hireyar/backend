# معرفی‌ها، context هوش مصنوعی و بازیابی مصاحبه — 2026-10-07

تکمیل صوتی همین روز: [VOICE_INTRODUCTION_API](VOICE_INTRODUCTION_API.md) قرارداد upload/retry معرفی، وضعیت پردازش و صوت پیام‌های ثابت است؛ مسیر متنی پایین برای سازگاری باقی است.

این قرارداد بک‌اند روی `dev` است؛ Angular در این ریپو نیست. migration `20261007105803_AddInterviewContextAndRecovery` پیش از اجرای نسخه جدید لازم است. داده و فایل‌های تاریخی بازنویسی نمی‌شوند.

## قالب

POST/PUT قالب ویزارد و قالب معمولی این فیلدها را می‌پذیرند و GET قالب آن‌ها را برمی‌گرداند:

| فیلد | پیش‌فرض | کاربرد |
|---|---|---|
| `candidateIntroductionEnabled` | false | فعال‌سازی معرفی کارجو |
| `candidateIntroductionMessage` | متن زیر | متن ثابت، حداکثر ۲۰۰۰ کاراکتر |
| `companyIntroductionEnabled` | false | فعال‌سازی معرفی شرکت |
| `companyIntroductionMessage` | رشته خالی | متن ثابت، حداکثر ۲۰۰۰ کاراکتر؛ هنگام فعال‌سازی الزامی |

متن پیش‌فرض معرفی کارجو:

> قبل از شروع سؤال‌ها، لطفاً خودتون رو معرفی کنید و درباره سوابق کاری و پروژه‌هایی که روی آن‌ها کار کردین برای ما توضیح بدین

هنگام دعوت، پیام‌ها، پرچم‌ها، زبان، JD، بانک سؤال و مستر پرامپت v2 snapshot می‌شوند؛ تغییر قالب بعدی دعوت را تغییر نمی‌دهد. کپی قالب برای موقعیت دیگر این فیلدها را نیز حفظ می‌کند. مستر پرامپت داخلی است و در API داوطلب یا لاگ عرضه نمی‌شود. صدور دعوت، تماس LLM ایجاد نمی‌کند؛ snapshot همان دعوت در هر ارزیابی مرحله Questions به LLM ارسال می‌شود.

## رزومه

دعوت multipart ویزارد همچنان `resume` دارد؛ فیلد اختیاری `resumeText` تا ۳۲۰۰۰ کاراکتر اضافه شد. PDF متنی تا ۵۰ صفحه و DOCX به‌صورت محلی در Infrastructure استخراج می‌شوند. PDF اسکن‌شده/رمزدار/خراب، DOC قدیمی یا متن بیش از حد مجاز نیاز به متن تأییدشده در `resumeText` دارند؛ بدون متن قابل استفاده، دعوت با 400 و `resume_text_required` رد می‌شود. OCR و parsing فایل DOC اضافه نشده‌اند. فایل اولیه همچنان خصوصی ذخیره می‌شود؛ متن برای همین جلسه snapshot می‌شود. نام اصلی فایل یا محتوای رزومه لاگ نمی‌شود.

مسیر قدیمی applications/interviews فایل رزومه ندارد و برای سازگاری، context رزومه آن null است؛ این مسیر را برای دعوت دارای رزومه استفاده نکنید. رزومه دعوت‌های تاریخی خودکار استخراج/backfill نشده است. ویزارد مسیر تضمین context رزومه برای دعوت تازه است.

## مرحله‌های داوطلب

پاسخ‌های `start/current/answers/retry/complete` فیلدهای `stage`, `stageMessage`, `candidateIntroductionEnabled`, `companyIntroductionEnabled`, `recoveryAttempts`, `backgroundRetryPending` دارند.

اگر حداقل یکی از معرفی‌ها فعال باشد:

`Welcome → CandidateIntroduction (اختیاری) → Questions → CompanyIntroduction (اختیاری) → Goodbye → Finished`

در Welcome سؤال و canRecord عرضه نمی‌شوند؛ در CandidateIntroduction بدون معرفی پذیرفته‌شده، canRecord برای ضبط معرفی true است و سؤال ندارد. معرفی شرکت فقط بعد از Completed در stageMessage همان مرحله عرضه می‌شود. پیام ثابت از snapshot خوانده می‌شود؛ TTS متن را تغییر نمی‌دهد. stageAudioUrl صوت احرازشده مرحله جاری را می‌دهد. بدون معرفی فعال، شروع قبلی با welcomeMessage و سؤال نخست حفظ شده است؛ جلسه تاریخی مرحله جدیدی به ابتدا اضافه نمی‌کند.

برای تأیید نمایش پیام یا ثبت معرفی کارجو:

```http
POST /api/candidate/interviews/{interviewId}/stage/advance
Authorization: Bearer <candidate token>
Content-Type: application/json

{ "stage": "CandidateIntroduction", "introduction": "متن معرفی خود کارجو" }
```

برای Welcome، CompanyIntroduction و Goodbye فقط `stage` لازم است. معرفی کارجو متن الزامی تا ۳۲۰۰۰ کاراکتر دارد؛ این endpoint آپلود صوت/STT معرفی نیست. متن را UI یا مسیر transcription مصرف‌کننده فراهم می‌کند. متن معرفی به ارزیاب می‌رسد ولی خود معرفی نمره مهارتی ایجاد نمی‌کند. تکرار تأیید مرحله قبلی، مرحله جاری را برمی‌گرداند؛ مرحله آینده را نمی‌توان با یک درخواست رد کرد. `current` پس از reload مرحله persistشده را بازیابی می‌کند. ارسال پاسخ/complete قبل از Questions پذیرفته نمی‌شود.

## تعامل سؤال

پاسخ اصلی و تعامل‌های همان سؤال رکوردهای مستقل answer دارند. در clarification/follow-up/redirect شناسه سؤال و متن اصلی ثابت می‌مانند؛ `question.interactionMessage` متن کوتاه تعامل است و صوت سؤال جاری همین متن را می‌خواند. `messages` همه نوبت‌ها و پاسخ assistant را نگه می‌دارد. برای پاسخ تازه clientTurnId تازه بسازید؛ برای replay همان ارسال، شناسه قبلی را حفظ کنید. interactionMessage را هنگام وجود، به‌جای خواندن دوباره متن اصلی نمایش/پخش کنید.

در ویزارد حداکثر دو تعامل برای هر سؤال مجاز است؛ قالب مسیر قدیمی از MaxFollowUpQuestionsPerQuestion خود استفاده می‌کند. کمتر از ۱۵ ثانیه باقی‌مانده یا پایان سقف تعامل، policy اجازه تعامل تازه نمی‌دهد. تعامل مهارت/evidence را دوباره نمره نمی‌دهد؛ پاسخ نهایی با سابقه همه نوبت‌ها و اصلاحات ارزیابی می‌شود. شمارنده پیشرفت فقط پاسخ نهایی سؤال را می‌شمارد. زمان مالی همچنان window محدود سرور برای هر نوبت و با سقف کل جلسه است؛ retry زمان/هزینه دوباره ایجاد نمی‌کند.

Schema داخلی v2 علاوه بر فیلدهای قبلی، `action` و `candidateMessage` دارد. action مجاز: advance/clarify/followUp/redirect. برای تعامل، nextQuestionId=null، shouldComplete=false و skillUpdates/coveredTopics خالی‌اند. متن بیش از ۲۰۰۰ کاراکتر، action ناشناخته، امتیاز همراه تعامل یا انتخاب سؤال خارج shortlist رد و backend fallback می‌شوند. JSON نسخه قبلی بدون دو فیلد جدید همچنان advance محسوب می‌شود.

عمومی: سؤال‌های ساده دقیقاً به ترتیب، بدون پایان زودهنگام به علت sufficient evidence. بانک موضوعی عمومی طبق قرارداد یک سؤال در هر موضوع، سطح متوسط و ترتیب موضوع را حفظ می‌کند. تخصصی: انتخاب فقط در shortlist مجاز بانک snapshot با محدودیت‌های policy و capability قرارداد همان جلسه. LLM، JD/رزومه/معرفی، زبان/نوع، زمان، بانک کامل و shortlist مجاز، skill/topic state و نوبت‌های قبلی را دریافت می‌کند. رزومه context است، نه اثبات مهارت. داده context دستور سیستمی محسوب نمی‌شود.

## بازیابی

worker داخلی از PostgreSQL پاسخ‌های ProcessingAnswer با lease آزاد/منقضی را پیدا می‌کند. گزینه‌های `InterviewAudio`:

| گزینه | پیش‌فرض |
|---|---|
| BackgroundRecoveryEnabled | true |
| RecoveryPollSeconds | 15 |
| MaxRecoveryAttempts | 3 |

اولین retry خطای provider پس از ۳۰ ثانیه؛ تأخیر بعدی بر حسب تلاش persistشده، ۶۰/۱۲۰/۲۴۰ ثانیه است. قطع درخواست پس از commit یا restart فرایند، با انقضای lease و polling بازیابی می‌شود. فایل صوت و transcript ثبت‌شده دوباره مصرف می‌شوند؛ STT آماده‌شده تکرار نمی‌شود. claim زیر قفل کوتاه سازمان و commit قبل network است؛ worker/درخواست هم‌زمان یک lease می‌گیرند و RowVersion از نتیجه کهنه جلوگیری می‌کند. سازمان/جلسه، revoked/deleted/expiry و billing در claim دوباره بررسی می‌شوند.

پس از سه شکست STT، وضعیت Failed و retry دستی باقی می‌ماند؛ مصاحبه بدون transcript معتبر ادامه داده نمی‌شود. LLM unavailable ابتدا retry می‌شود و پس از سقف تلاش، backend بدون evidence ساختگی سؤال بعد را انتخاب می‌کند. JSON نامعتبر از ابتدا fallback می‌شود. شکست TTS متن را حفظ می‌کند. worker پس از قطع اتصال سؤال‌های بی‌پاسخ را خودکار جواب نمی‌دهد؛ پردازش پاسخ پذیرفته‌شده را تمام می‌کند و جلسه منتظر کارجو باقی می‌ماند. APIهای retry موجود محفوظ‌اند. worker اسکن چندسازمانی داخلی است؛ endpoint مستقل recovery یا دسترسی HttpContext در Infrastructure اضافه نشده است.

برای deployment، migration و API را همراه مصرف قرارداد جدید فرانت هماهنگ کنید. rollout/AI واقعی/Angular در این نوبت انجام نشده‌اند؛ این سند مجوز تغییر مقصد نیست.
