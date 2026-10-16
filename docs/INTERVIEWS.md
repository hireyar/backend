# Candidate AI Interview

به‌روزرسانی 2026-10-06: [CANDIDATE_TURN_API](CANDIDATE_TURN_API.md) قرارداد جاری پاسخ نوبتی با clientTurnId اتمیک، lastSpeechEndUtc، duration ذخیره‌شده، ویدیوی اختیاری، transcript داوطلب و messages است. mediaCapabilities در پاسخ داوطلب سقف‌ها و پشتیبانی ویدیو را می‌دهد؛ دانلود HR همان scope/retention موجود را دارد. migration `20261006170044_AddCandidateAnswerCapture` لازم است. WebRTC/Realtime طبق تصحیح کاربر اضافه نشده؛ جریان STT/ارزیابی/TTS و سیاست مالی قبل حفظ‌اند.

به‌روزرسانی Pricing در 2026-10-01 روی `codex/pricing`: invite/start اکنون اشتراک فعال با قابلیت HR/فنی و ai_interview می‌خواهند؛ start اعتبار حداکثر هزینه را reserve می‌کند و completion/revoke/expiry settlement مربوط دارند. `requiredCapabilityCode` در CreateInterviewRequest اختیاری و پیش‌فرض `technical_interview` است؛ HR از `hr_interview` استفاده کند. rate/feature/retention snapshot و rule مدت مالی در [BILLING](BILLING.md) مرجع‌اند. گزارش تاریخی حفظ می‌شود ولی audio download بعد از retention قرارداد 404 می‌دهد. درگاه runtime غیرفعال است و provisioning اولیه بدون fake success لازم دارد. قرارداد domain/lease/provider قبلی حفظ شده است.

راهنمای مستقل قابل تحویل به تیم فرانت‌اند و پرامپت اجرای آن: [FRONTEND_INTERVIEWS_HANDOFF](FRONTEND_INTERVIEWS_HANDOFF.md).

نتیجه جاری بررسی پیش از staging: [INTERVIEW_AUDIT](INTERVIEW_AUDIT.md)؛ ۱۱۸ تست موفق روی PostgreSQL واقعی، model بدون drift و smoke واقعی TTS/STT/LLM با داده مصنوعی موفق. دو نقص مسیر خطا و secret default اصلاح شدند؛ قرارداد API و schema تغییر نکردند. آمار ۹۴ و وضعیت «Live آزمایش نشده» در گزارش قبلی پایین تاریخی‌اند.

این ماژول روی branch `codex/candidate-ai-interview` مسیر HR → دعوت → start → صوت → STT → ارزیابی → سؤال بعدی را از API اجرا می‌کند. تصمیم استخدام/رد خودکار ندارد. smoke واقعی NetArz با تنظیم موجود موفق شد؛ تست عادی همچنان از HTTP handler و provider آزمایشی استفاده می‌کند. کلید را خارج Git تنظیم کنید و کلید قبلاً ثبت‌شده در source را پیش از staging تعویض کنید.

## تنظیمات

نمونهٔ زیر را در تنظیمات **خارج Git** یا متغیرهای محیطی قرار دهید. فایل `appsettings.interviews.example.json` فقط مثال است و خودکار load نمی‌شود. در نبود `AI:Provider`، provider به طور پیش‌فرض Disabled است تا حساب‌های HR موجود بدون تنظیم AI همچنان بالا بیایند؛ کلید خالی با Provider=NetArz در startup خطای واضح می‌دهد. provider جعلی فقط از DI در تست ثبت می‌شود و گزینهٔ production نیست.

```json
{
  "AI": {
    "Provider": "NetArz",
    "NetArz": {
      "BaseUrl": "https://netarz.ir/api/ai/v1",
      "ApiKey": "",
      "SttModel": "gpt-transcribe",
      "LlmModel": "gpt-5.6-luna",
      "TtsModel": "tts-1-hd",
      "TtsVoice": "nova",
      "TtsSpeed": 1.0,
      "SttTimeoutSeconds": 60,
      "LlmTimeoutSeconds": 30,
      "TtsTimeoutSeconds": 30
    }
  },
  "InterviewAudio": {
    "MaxUploadBytes": 20971520,
    "MaxAnswerSeconds": 600,
    "ShortlistLimit": 10,
    "LeaseSeconds": 300
  },
  "InterviewStorage": { "RootPath": "storage/interviews" }
}
```

`AI__Provider=NetArz` و `AI__NetArz__ApiKey` متغیرهای فعال‌سازی اصلی‌اند. همه فیلدهای دیگر نیز با `__` override می‌شوند. root صوت خارج wwwroot و خصوصی است؛ در deployment باید volume پایدار و دسترسی فایل فقط برای حساب سرویس فراهم شود. فایل‌ها binary در PostgreSQL نیستند؛ keyهای نسبی با الگوی محدود، مستقل از physical path ذخیره می‌شوند.

Dockerfile پوشه `/app/storage/interviews` را پیش از `USER $APP_UID` با مالکیت همان کاربر و مجوز 700 می‌سازد. Composeهای production/development مسیر `InterviewStorage__RootPath` را به همین پوشه و volume دائمی `interview-storage` وصل می‌کنند؛ رزومه، پاسخ و صوت سؤال در آن حفظ می‌شوند. volume تازه مالکیت پوشه image را می‌گیرد؛ bind mount یا volume موجود باید جداگانه دسترسی نوشتن حساب سرویس را داشته باشد. پیش از وصل‌کردن volume تازه، اگر فایل قبلی در لایه کانتینر یا مسیر دیگری وجود دارد، آن را با حفظ keyهای نسبی و مالکیت منتقل کنید؛ mount تازه فایل‌های قبلی را خودکار مهاجرت نمی‌دهد.

## آماده‌سازی HR

تمام درخواست‌های HR با JWT معمول Owner/Recruiter و tenant جاری سرور انجام می‌شوند. OrganizationId ورودی ندارند.

1. موقعیت و قالب را با APIهای موجود بسازید. قالب مدت جلسه را تعیین می‌کند.
2. `POST /api/skills` با `{ "name": "SQL" }` → شناسه مهارت.
3. `PUT /api/job-positions/{positionId}/skills/{skillId}` با `{ "isRequired": true, "priority": 100, "targetDifficulty": 3 }`.
4. `POST /api/job-positions/{positionId}/question-bank` با `{ "skillId": "UUID", "text": "ایندکس چه اثری دارد؟", "topic": "indexing", "difficulty": 3, "isActive": true }`.
5. `POST /api/job-positions/{positionId}/applications` با `{ "firstName": "نام آزمایشی", "lastName": "نام خانوادگی آزمایشی", "email": "candidate@example.test", "phone": null }`.
6. `POST /api/job-positions/{positionId}/applications/{applicationId}/interviews` با `{ "templateId": "UUID", "maxQuestionCount": 12, "expiresInHours": 72 }`.

پاسخ دعوت شامل interviewId، token، expiresAtUtc و candidateRoute است. token تصادفی ۲۵۶بیتی فقط همان بار به caller برمی‌گردد؛ SHA-256 آن ذخیره می‌شود. این endpoint ایمیل یا SMS نمی‌فرستد؛ تحویل توکن از طریق frontend/مسیر امن مسئولیت مصرف‌کننده است. token را در query URL، log یا storage عمومی قرار ندهید.

بانک جدید مستقل از سؤال‌های editable قالب است و mapping صریح به PositionSkill دارد. قالب‌های قدیمی و ترتیب سؤال‌های آن‌ها تغییر نکرده‌اند؛ سؤال‌های جریان AI از بانک موقعیت می‌آیند، نه Competency متنی قالب قدیمی. pool دعوت حداکثر ۳۰ سؤال فعال با ترتیب Id است؛ مهارت required بدون سؤال باعث خطای `interview_plan_incomplete` می‌شود. snapshot متن، موضوع، مهارت و difficulty هنگام **ساخت دعوت** تثبیت می‌شود و start همان plan را فعال می‌کند؛ ویرایش بعدی بانک/قالب، حتی پیش از start، دعوت صادرشده را تغییر نمی‌دهد. برای plan تازه دعوت تازه صادر کنید.

`GET /api/job-positions/{positionId}/question-bank?page=1&pageSize=20` پاسخ `{items,total,pageNumber,pageSize}` دارد؛ اندازه۱تا۱۰۰ و دسترسی به تمام صفحات، مطابق [PAGINATION_API](PAGINATION_API.md). ویرایش با `PUT /api/job-positions/{positionId}/question-bank/{questionId}` و `{ "question": { ... }, "rowVersion": "BASE64" }` است؛ نسخه ۱۶بایتی و stale version برابر 409 است.

## درخواست‌های داوطلب

همهٔ درخواست‌ها و دانلود audio با `Authorization: Bearer <candidate-interview-token>` انجام می‌شوند. این opaque token با HR JWT متفاوت است. audioUrl یک endpoint احرازشده است؛ frontend برای پخش باید صوت را با همین header fetch و به Blob تبدیل کند.

```http
POST /api/candidate/interviews/INTERVIEW_UUID/start
Authorization: Bearer CANDIDATE_INTERVIEW_TOKEN
```

start دوباره plan یا سؤال جاری را reset نمی‌کند. شروع هم‌زمان می‌تواند 409 دهد؛ caller current را دوباره دریافت کند.

```http
GET /api/candidate/interviews/INTERVIEW_UUID/current
Authorization: Bearer CANDIDATE_INTERVIEW_TOKEN
```

```http
POST /api/candidate/interviews/INTERVIEW_UUID/answers
Authorization: Bearer CANDIDATE_INTERVIEW_TOKEN
Content-Type: multipart/form-data; boundary=EXAMPLE

--EXAMPLE
Content-Disposition: form-data; name="questionId"

CURRENT_QUESTION_UUID
--EXAMPLE
Content-Disposition: form-data; name="durationSeconds"

45
--EXAMPLE
Content-Disposition: form-data; name="audio"; filename="answer.webm"
Content-Type: audio/webm

<binary audio>
--EXAMPLE--
```

یا با curl و متغیرهای محلی موقت (توکن را داخل فایل versioned ننویسید):

```powershell
curl.exe -X POST "$api/api/candidate/interviews/$interview/start" -H "Authorization: Bearer $candidateToken"
curl.exe "$api/api/candidate/interviews/$interview/current" -H "Authorization: Bearer $candidateToken"
curl.exe -X POST "$api/api/candidate/interviews/$interview/answers" -H "Authorization: Bearer $candidateToken" -F "questionId=$question" -F "durationSeconds=45" -F "audio=@answer.webm;type=audio/webm"
```

پاسخ شامل status، canRecord، isProcessing، question با audioAvailable/audioUrl، progress و answer با id/status/canRetry/errorCode است. score/evidence یا reasoning به داوطلب داده نمی‌شود. خطای STT پاسخ موفق HTTP با `answer.status=Failed`، `canRetry=true` و `errorCode=answer_retry_available` برمی‌گرداند؛ فایل حفظ می‌شود.

```http
POST /api/candidate/interviews/INTERVIEW_UUID/answers/ANSWER_UUID/retry
Authorization: Bearer CANDIDATE_INTERVIEW_TOKEN
```

retry فایل جدید نمی‌خواهد. در نبود transcript، STT تکرار می‌شود؛ در حضور transcript مستقیماً evaluation انجام می‌شود. lease فعال 409 می‌دهد؛ lease منقضی‌شده قابل گرفتن است و worker قبلی حق commit ندارد. پس از crash، current شناسه پاسخ و canRetry را نشان می‌دهد. قطع درخواست تا انقضای lease ممکن است پاسخ را موقتاً processing نگه دارد؛ worker خودکار اضافه نشده است.

`POST /api/candidate/interviews/{id}/complete` فقط در WaitingForAnswer مجاز است و `CompletionReason=Manual` را ثبت می‌کند. پایان SufficientEvidence/NoQuestions/QuestionLimit/DurationLimit را backend پس از پردازش تعیین می‌کند. session state: Created → WaitingForAnswer → ProcessingAnswer → WaitingForAnswer یا Completed؛ Cancelled مسیر Domain است و endpoint عمومی cancellation اضافه نشده.

صوت webm/wav/mp3/m4a/ogg با MIME و signature ابتدایی بررسی می‌شود. این signatureها codec/container را کامل decode نمی‌کنند. مدت واقعی فایل بدون library رسانه استخراج نمی‌شود؛ durationSeconds ادعای client است و سقف حجم و زمان کلی جلسه server-side enforce می‌شوند. ضبط browser باید این محدودیت را رعایت کند.

## HR و حریم خصوصی

`GET /api/interviews/{id}` شامل metadata، snapshot سؤال، transcript، state، evidence و coverage است. `POST /api/interviews/{id}/revoke` دسترسی داوطلب را فوراً باطل می‌کند؛ HR همچنان report را می‌بیند. دانلودها:

- HR: `/api/interviews/{id}/answers/{answerId}/audio` و `/api/interviews/{id}/questions/{questionId}/audio`.
- Candidate: `/api/candidate/interviews/{id}/questions/{questionId}/audio` فقط برای سؤال جاری یا پاسخ‌داده‌شده.

token خارج جلسه و HR خارج tenant پاسخ 404/401 مناسب می‌گیرند. هیچ raw audio، transcript، key یا token در logging جدید ثبت نمی‌شود. raw provider JSON و chain-of-thought ذخیره/عرضه نمی‌شوند؛ evidence کوتاه و قابل مشاهده ذخیره می‌شود. نگهداری/حذف داده و فایل‌های orphan پس از crash و backup/retention باید در سیاست عملیاتی deployment تعیین شوند؛ cleanup خودکار افزوده نشده.

## AI و cache

shortlist واقعی همان attempt (۵ تا ۲۰) به LLM فرستاده و عین شناسه‌ها به validator داده می‌شود. 401/429/5xx/timeout/JSON نامعتبر/شناسه خارج shortlist به fallback می‌رسد و evidence نامعتبر ذخیره نمی‌شود. توصیه shouldComplete شرط پایان را دور نمی‌زند. بعد از اعمال evidence، policy دوباره سؤال پیشنهادی را بررسی می‌کند.

TTS cache سازمانی با SHA-256 آرایهٔ متن normalize‌شده/model/voice/speed ساخته می‌شود و میان جلسه‌ها reuse می‌شود. تغییر متن/پروفایل hash تازه می‌سازد؛ صوت snapshot قبلی حفظ می‌شود. current فقط cache را می‌خواند؛ start/پاسخ، cache miss را تولید می‌کنند. TTS ناموفق موجب سؤال متنی می‌شود. cache missهای هم‌زمان ممکن است دو درخواست TTS بسازند؛ unique cache key فقط یک مرجع را نگه می‌دارد و فایل loser حذف می‌شود. برای کاهش latency، اولین مرور هر سؤال را پیش از مصاحبه‌های واقعی انجام دهید.

هیچ transaction هنگام STT/LLM/TTS باز نمی‌ماند. هر SaveChanges یک commit کوتاه است؛ session RowVersion و lease در هر stage write تغییر می‌کنند. پاسخ session/question unique است. تماس خارجی خودکار retry نمی‌شود تا هزینه/اجرای تکراری POST ایجاد نشود؛ retry کنترل‌شدهٔ داوطلب و fallback مسیر بازیابی‌اند.

## بررسی و migration

نمونهٔ خروجی LLM (UUIDها باید دقیقاً با skill سؤال جاری و availableQuestions همان attempt تطبیق داشته باشند):

```json
{
  "answerAssessment": { "relevance": 0.9, "clarity": 0.8, "evidenceQuality": 0.7 },
  "skillUpdates": [
    { "skillId": "00000000-0000-0000-0000-000000000001", "delta": 0.08, "confidence": 0.8, "evidence": "Candidate explained the effect of indexes on reads and writes." }
  ],
  "coveredTopics": [ { "topic": "indexing", "coverage": 0.85 } ],
  "nextQuestionId": "00000000-0000-0000-0000-000000000002",
  "shouldComplete": false,
  "reason": "Another configured topic needs evidence."
}
```

Migration: `20261001061608_AddCandidateInterviewFlow`؛ فقط جدول‌های جدید و alternate key قالب را اضافه می‌کند، جدول قبلی را recreate نمی‌کند. تمام FKها Restrict هستند. آن را طبق فرمان EF موجود و با connection string مقصد خارج Git اعمال کنید؛ این نوبت migration در محیط پروژه/production اعمال نشده است.

نتیجهٔ بررسی این نوبت: build صفر warning/error؛ ۹۴ تست موفق، صفر fail/Skip با PostgreSQL 18 مستقل روی loopback:55441 و provider جعلی/HTTP mocked. DBهای fixture تصادفی‌اند و حذف می‌شوند؛ SQL InMemory/SQLite استفاده نشده. تست بدون HIREYAR_TEST_POSTGRES تست‌های PostgreSQL را Skip می‌کند. EF has-pending-model-changes بدون اختلاف است. مدل‌های واقعی، tenant FK، answer unique، concurrency دو context، token scope/expiry/revocation، retry/STT reuse، lease recovery، TTS cache/failure و رفتار HTTP provider بررسی شده‌اند. NetArz زنده هنوز با حساب مقصد آزمایش نشده است.
