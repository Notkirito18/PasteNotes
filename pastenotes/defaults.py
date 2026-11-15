from __future__ import annotations

import textwrap


def _t(title: str, content: str) -> tuple[str, str]:
    return title, textwrap.dedent(content).strip()


DEFAULT_GROUPS = (
    {
        "name": "WhatsApp - Customer Care (AR)",
        "hotkey": "Ctrl+Alt+1",
        "color": "green",
        "templates": (
            _t(
                "Greeting - Thank You",
                """\
                شكرًا لتواصلكم مع الفردان للصرافة، نتمنى لكم يومًا سعيدًا!
                """,
            ),
            _t(
                "Anything Else?",
                """\
                هل هناك أي شيء آخر يمكننا مساعدتك به، من فضلك؟
                """,
            ),
            _t(
                "Clarify Inquiry",
                """\
                الرجاء توضيح استفسارك
                """,
            ),
            _t(
                "QID - Request Number",
                """\
                الرجاء تزويدنا برقم البطاقة الشخصية للتحقق
                """,
            ),
            _t(
                "QID - Request Front & Back",
                """\
                الرجاء تزويدنا بصورة البطاقة الشخصية القطرية من الامام والخلف
                """,
            ),
            _t(
                "Discount - No Active Code",
                """\
                لا يوجد رمز خصم شغال حالياً، الرجاء ترك الخانة فاضية
                """,
            ),
            _t(
                "Discount - Leave Field Empty",
                """\
                الرجاء ترك خانة رمز الخصم فارغة. فقط قم بتحديد المبلغ الذي تريد تحويله في الخانة السوداء الموجودة اعلى الشاشة بدون نقاط ولا فواصل بعملة الريال القطري او بالعملة الاجنبية حتى يسمح لك التطبيق باتمام العملية
                """,
            ),
            _t(
                "App Transfer - Steps",
                """\
                يرجى التسجيل في التطبيق؛ وبعد إتمام التسجيل، قم بتسجيل الدخول إلى حسابك، ثم اضغط على خيار "إرسال الأموال" وأضف المستفيد، واختر وجهة المستفيد وطريقة الاستلام، ثم أدخل بيانات المستفيد المطلوبة.
                """,
            ),
            _t(
                "Screen Recording - Error",
                """\
                الرجاء تزويدنا بفيديو تصوير الشاشة يوضح الخطأ الذي يظهر معك.
                """,
            ),
            _t(
                "Screenshot - Error",
                """\
                يرجى تزويدنا بصورة للشاشة توضح الخطأ الذي يظهر معك
                """,
            ),
            _t(
                "Screen Recording - Steps",
                """\
                الرجاء تزويدنا بفيديو تصوير الشاشة يوضح الخطوات التي تقوم بها حتى يظهر معك الخطأ.
                """,
            ),
            _t(
                "Escalated - Data Sent",
                """\
                تم ارسال البيانات الى القسم المختص، وبمجرد استلامنا للتحديث، سنبقيك على اطلاع.
                نحن نقدر صبرك على هذا الأمر.
                """,
            ),
        ),
    },
    {
        "name": "WhatsApp - Transfers (AR)",
        "hotkey": "Ctrl+Alt+2",
        "color": "blue",
        "templates": (
            _t(
                "Cancellation - Receipt & QID",
                """\
                الرجاء تزويدنا بايصال التحويل و صورة البطاقة الشخصية من الامام والخلف لرفع طلب الالغاء
                """,
            ),
            _t(
                "Cancellation - QID Only",
                """\
                الرجاء تزويدنا بصورة البطاقة الشخصية من الامام والخلف لرفع طلب الالغاء
                """,
            ),
            _t(
                "Cancellation - Forwarded",
                """\
                تم إرسال طلب الالغاء إلى القسم المختص. يتواصل القسم مع وكيل المراسلة للحصول على آخر المستجدات، وبمجرد استلامنا للتحديث، سنبقيك على اطلاع.
                نحن نقدر صبرك على هذا الأمر.
                """,
            ),
            _t(
                "Amendment - Receipt, QID & Name",
                """\
                الرجاء تزويدنا بايصال التحويل و صورة البطاقة الشخصية من الامام والخلف والاسم الصحيح كامل بالانجليزية لرفع طلب التعديل
                """,
            ),
            _t(
                "Amendment - Forwarded",
                """\
                تم إرسال طلب التعديل إلى القسم المختص. يتواصل القسم مع وكيل المراسلة للحصول على آخر المستجدات، وبمجرد استلامنا للتحديث، سنبقيك على اطلاع.
                نحن نقدر صبرك على هذا الأمر.
                """,
            ),
            _t(
                "Resend - Confirm Current Rate",
                """\
                يرجى تأكيد موافقتك على اعادة الارسال بسعر الصرف الحالي
                """,
            ),
            _t(
                "Resend - Processing",
                """\
                جاري العمل على طلبك، سيتم تزويدك بايصال جديد فور اتمام الاجراءات
                """,
            ),
            _t(
                "Transfer - Held by Egypt Bank",
                """\
                الحوالة مرسلة من طرفنا وهي حالياً معلقة مع البنك الوسيط بمصر، تم ارسال طلب استفسار للحصول على آخر المستجدات بشانها وسيتم اعلامك فور تلقينا الرد.
                """,
            ),
            _t(
                "Transfer - Sent, Inquiry Pending",
                """\
                يرجى العلم ان الحواله مرسله بنجاح وسيتم ارسال طلب استعلام للوكيل وسنوافيك بالرد في حال استلامنا التحديث
                """,
            ),
            _t(
                "Transfer - Processing with Agent",
                """\
                يرجى العلم ان الحوالة تمت من طرفنا وهي قيد الاجراء مع الوكيل
                """,
            ),
            _t(
                "Transfer - Sent, Polite Follow-Up",
                """\
                عميلنا العزيز يرجى العلم ان الحواله مرسله بنجاح وسيتم ارسال طلب استعلام للوكيل وسنوافيك بالرد في حال استلامنا التحديث
                """,
            ),
            _t(
                "Transfer - Held, Inquiry Sent",
                """\
                الحوالة معلقة مع البنك الوسيط، تم إرسال طلب استفسار للوكيل للحصول على آخر المستجدات، وبمجرد استلامنا للتحديث، سنبقيك على اطلاع. نحن نقدر صبرك على هذا الأمر.
                """,
            ),
            _t(
                "Transfer - Egypt Winter Delay",
                """\
                نود إفادتك بأن الحوالة قيد الإجراء حالياً ومُعلقة بمصر بشكل مؤقت، يرجع هذا التأخير إلى تحديث التوقيت الشتوي الرسمي في مصر
                """,
            ),
            _t(
                "Transfer - Processing, Follow-Up",
                """\
                يرجى العلم أن الحوالة مرسله بنجاح وهي قيد المعالجة من طرف الوكيل، لقد قمنا بارسال طلب استفسار ونحن في انتظار التحديث بشأن الحالة النهائية للحوالة، نؤكد لكم أننا نتابع الموضوع بشكل مستمر، نشكر لكم صبركم وتفهمكم.
                """,
            ),
            _t(
                "Transfer - 1-3 Business Days",
                """\
                يرجى العلم ان الحوالة تمت من طرفنا وهي قيد الاجراء مع الوكيل، الحوالات البنكية تأخذ من يوم الى 3 أيام عمل بنكي بدون احتساب ايام نهاية الاسبوع. نحن نقدر صبرك على هذا الأمر.
                """,
            ),
            _t(
                "Transfer - Wrong IBAN",
                """\
                يرجى العلم ان الحوالة معلقة بسسب رقم الايبان خطأ. ادا كنت تريد التعديل يرجى ارسال رقم الحوالة وصورة الهوية الشخصية من الامام والخلف. شكرا
                """,
            ),
            _t(
                "Transfer - Executed, Agent Processing",
                """\
                يرجى العلم بأن الحوالة قد تم تنفيذها من طرفنا وهي حالياً قيد المعالجة لدى الوكيل. كما تم تحويل استفساركم إلى القسم المختص، والذي يتواصل بشكل مباشر مع الوكيل للحصول على آخر المستجدات المتعلقة بالحوالة. نؤكد لكم أننا نتابع الموضوع بشكل مستمر، نشكر لكم صبركم وتفهمكم.
                """,
            ),
            _t(
                "Follow-Up - Closing Note",
                """\
                نؤكد لكم أننا نتابع الموضوع بشكل مستمر، نشكر لكم صبركم وتفهمكم.
                """,
            ),
            _t(
                "Follow-Up - End of Day",
                """\
                بامكانك المتابعة معنا خلال نهاية اليوم وسيتم المتابعة من طرفنا مع الوكيل كدلك
                """,
            ),
        ),
    },
    {
        "name": "WhatsApp - Rates & Gold (AR)",
        "hotkey": "Ctrl+Alt+3",
        "color": "orange",
        "templates": (
            _t(
                "Rate - USD 1,000 to Syria (Example)",
                """\
                سعر تحويل 1000 دولار الى سوريا شاملاً العمولة 3732 ريال قطري
                """,
            ),
            _t(
                "Rate - EGP & EUR (Example)",
                """\
                1 QAR = 14.10 EGP سعر التحويل اليوم
                4,500 EGP = 350.75 QAR شاملةً العمولة
                1 EUR = 4.46 QAR
                1 EUR = 4.11 QAR
                """,
            ),
            _t(
                "Gold - 20g 24K (Example)",
                """\
                يبلغ سعر الذهب سبيكة عيار 24 وزن 20 جرامًا اليوم 10,998 ريال قطري
                يبلغ سعر اعادة بيع السبيكة عيار 24 وزن 20 جرامًا اليوم 10,038 ريال قطري

                ملاحظة: الأسعار قابلة للتغيير. عندما تكون في الفرع، يرجى التحقق من السعر مرة أخرى قبل إجراء المعاملة.
                """,
            ),
            _t(
                "USD - Not Available",
                """\
                يرجى العلم أن عملة الدولار الامريكي غير متوفرة حالياً
                """,
            ),
            _t(
                "USD - Not Available at Branches",
                """\
                يرجى العلم أن عملة الدولار الامريكي غير متوفرة بأي فرع مأقتاً
                """,
            ),
            _t(
                "USD - Daily Limit",
                """\
                نعم الدولار متوفر في حدود 1000 دولار باليوم لكل عميل
                """,
            ),
        ),
    },
    {
        "name": "WhatsApp - Cards & Wallets (AR)",
        "hotkey": "Ctrl+Alt+4",
        "color": "purple",
        "templates": (
            _t(
                "Mastercard - Apply & Fees",
                """\
                للحصول على ماستركارد، التكلفة 20 ريال قطري. ما عليك سوى زيارة أي فرع من فروع الفردان للصرافة ومعك بطاقة الهوية الشخصية الأصلية الخاصة بك. يمكنك شحن البطاقة بحد أدنى 10 ريال قطري يصل إلى 40000 ريال قطري. يمكنك التسجيل على https://www.alfardanexchange.com.qa/travelcard-sign-ups ثم زيارة الفردان للصرافة لاستلام بطاقتك. للحصول على مساعدة خدمة العملاء، يرجى تقديم رقم الاتصال الخاص بك أو يمكنك الاتصال بنا على 4453777.
                """,
            ),
            _t(
                "Mastercard - Check Balance",
                """\
                للتحقق من رصيد بطاقة ماستر كارد، يمكنك التسجيل عبر الموقع الإلكتروني المدوّن على الجهة الخلفية من البطاقة، ثم تسجيل الدخول للاطلاع على الرصيد وجميع تفاصيل البطاقة.
                """,
            ),
            _t(
                "Mastercard - Registration Help",
                """\
                اذا واجهت مشكلة في التسجيل بامكانك التواصل مع الرقم المدوّن على الجهة الخلفية من البطاقة من أجل المساعدة.
                """,
            ),
            _t(
                "Vodafone Cash - Send Steps",
                """\
                بإمكانك الارسال عبر خدمة فودافون كاش من خلال التطبيق، اضغط على ارسال أموال ثم اضافة مستفيد ثم اختار الدولة مصر ثم اختار خدمة فودافون كاش ثم أضف بيانات المستفيد (الاسم الكامل و رقم المحفظة الصحيح) ثم حدد المبلغ وتابع الخطوات حتى تصل الى مرحلة الدفع
                """,
            ),
            _t(
                "WishMoney - Send Steps",
                """\
                بإمكانك الارسال عبر خدمة ويشموني من خلال التطبيق، اضغط على ارسال أموال ثم اضافة مستفيد ثم اختار الدولة لبنان ثم اختار خدمة محفظة الجوال ثم أضف بيانات المستفيد (الاسم الكامل و رقم المحفظة الصحيح) ثم حدد المبلغ وتابع الخطوات حتى تصل الى مرحلة الدفع
                """,
            ),
            _t(
                "Western Union - Edit Beneficiary",
                """\
                يمكنك التعديل من خلال التطبيق، في الصفحة الرئيسية يوجد 3 نقاط مكتوب المزيد، بعدها سجل المعاملات، تجد الحوالة و الاسم و 3 نقاط امامه، الخيار الاول تعديل اسم المستفيد
                """,
            ),
            _t(
                "Islamic Bank - Online Payment",
                """\
                للدفع باستخدام بطاقة بنكية لبنك المصرف الاسلامي يرجى تفعيل الدفع اونلاين من تطبيق البنك. اذا واجهتك صعوبة في هذه العملية يرجى التواصل مع البنك.
                """,
            ),
        ),
    },
    {
        "name": "Email - Operations (EN)",
        "hotkey": "Ctrl+Alt+5",
        "color": "red",
        "templates": (
            _t(
                "Recon - Refund Confirmation",
                """\
                Dear Team,

                Please confirm if the amount has been debited/credited back to our account.
                """,
            ),
            _t(
                "Recon - SOA Update",
                """\
                Dear Team,

                Kindly update as per the latest SOA.
                """,
            ),
            _t(
                "Refund - Ready at Counter",
                """\
                Dear Team,

                Kindly inform CPU to mark the transaction as ready to refund cash over the counter.

                The SMS has been sent to the customer.
                """,
            ),
            _t(
                "Fraud - Dispute Check",
                """\
                Dear Team,

                Kindly confirm if there is any dispute or fraud-related issue reported on the transaction.
                """,
            ),
            _t(
                "Refund - Al Nasr Branch",
                """\
                Kindly note that the customer requested to collect the cash over the counter refund from Al Nasr Branch.
                """,
            ),
            _t(
                "Transfer - Jordan Delay",
                """\
                Please be advised that the transfer has been processed from our side and is currently being handled by the agent in Jordan.

                Your inquiry has been forwarded to the relevant department, which is coordinating directly with the agent to obtain the latest update regarding the transfer. Please note that there is currently a general processing delay affecting transfers in Jordan, which may be contributing to the longer-than-usual turnaround time.

                As soon as we receive any new information, we will keep you informed.

                We appreciate your patience and understanding regarding this matter.
                """,
            ),
            _t(
                "Transfer - Jordan Processing",
                """\
                Please be advised that transfer number [TRANSFER NUMBER] has been initiated by us and is currently being processed by our agent in Jordan. Bank transfers typically take up to 3 business days to complete. We appreciate your patience and understanding.
                """,
            ),
            _t(
                "Fraud - Reprocessing Check",
                """\
                Dear Team,

                Kindly confirm if there is any dispute or if transaction [TRANSACTION NUMBER] is fraud related. The customer asked for reprocessing.
                """,
            ),
            _t(
                "WU - Cancellation Request",
                """\
                Dear Valued Customer,

                Greetings from Alfardan Exchange.

                Thank you for reaching out to us.

                You may request cancellation of a money transfer only if it has not been collected by the receiver in the destination country.

                If you wish to cancel your money transfer, please provide the following details:

                MTCN/TRACKING NUMBER:
                RECEIVER NAME:
                DESTINATION COUNTRY:
                REASON FOR REFUND:
                DEBIT CARD ISSUING BANK:
                """,
            ),
            _t(
                "WU - Amendment Request",
                """\
                Dear Valued Customer,

                Greetings from Alfardan Exchange.

                Kindly provide the following details to process your amendment request:

                MTCN/TRACKING NUMBER:
                OLD NAME OF RECEIVER:
                NEW NAME OF RECEIVER:
                DESTINATION COUNTRY:
                AMENDMENT REASON:
                """,
            ),
        ),
    },
    {
        "name": "Refunds & Payments (EN)",
        "hotkey": "Ctrl+Alt+6",
        "color": "pink",
        "templates": (
            _t(
                "Cancellation - Under Process",
                """\
                Dear Valued Customer,

                Thank you for contacting Alfardan Exchange. Kindly be informed that your cancellation request is under process.
                """,
            ),
            _t(
                "Refund - Branch Collection",
                """\
                Your transaction has been refunded as per your request. To receive your payment refund, please visit an Alfardan Exchange branch along with the original receipt and a valid QID.
                """,
            ),
            _t(
                "Refund - Digital Contact",
                """\
                Your transaction has been refunded as per your request. To receive your payment refund, kindly contact us by call or WhatsApp for the refund process on 44537777.
                """,
            ),
            _t(
                "Rejected - Branch or Contact",
                """\
                Your transaction has been rejected. To receive your payment refund, please visit an Alfardan Exchange branch with the original receipt and a valid QID, or contact us on 44537777.
                """,
            ),
            _t(
                "Rejected - Contact Support",
                """\
                Your transaction has been rejected. For the payment of refund, kindly contact us by call or WhatsApp for the refund process on 44537777.
                """,
            ),
            _t(
                "Rejected - Refund Processing",
                """\
                Your transaction has been rejected. Your refund is currently being processed, and we will notify you as soon as we receive the funds from the correspondent bank.
                """,
            ),
            _t(
                "Beneficiary ID - Copy Required",
                """\
                Your transaction is held up due to additional information required (Beneficiary ID copy). Please visit an Alfardan Exchange branch or contact us through WhatsApp on 44537777.
                """,
            ),
            _t(
                "Cancellation - Unable to Cancel",
                """\
                Dear Valued Customer,

                Unfortunately, we are unable to cancel the transaction as the funds have already been credited to the beneficiary's account.
                """,
            ),
            _t(
                "Credited - Bank Account",
                """\
                We are pleased to inform you that your transaction has been successfully credited to the beneficiary's account.
                """,
            ),
            _t(
                "Credited - Wallet",
                """\
                We are pleased to inform you that your transaction has been successfully PAID to the beneficiary's wallet.
                """,
            ),
            _t(
                "Payout - Ready",
                """\
                We are pleased to inform you that your transaction is ready for payout under reference number:
                """,
            ),
            _t(
                "Refund - Manager Escalation",
                """\
                Dear Valued Customer,

                Your refund request has been forwarded to our internal team. The team is communicating with the correspondent agent for an update. As soon as we receive an update, we will keep you informed. We appreciate your patience with this matter.
                """,
            ),
        ),
    },
    {
        "name": "Verification (EN)",
        "hotkey": "Ctrl+Alt+7",
        "color": "gray",
        "templates": (
            _t(
                "Unclear QID",
                """\
                Dear Valued Customer,

                Greetings from Alfardan Exchange.

                Your profile verification was declined due to an unclear copy of the Qatar ID. Please upload a clear and valid Qatar ID along with your selfie. Thank you.
                """,
            ),
            _t(
                "Expired QID",
                """\
                Dear Valued Customer,

                Greetings from Alfardan Exchange.

                Your profile verification was declined due to an expired copy of the Qatar ID uploaded. Please upload a clear and valid Qatar ID along with your selfie. Thank you.
                """,
            ),
            _t(
                "Unclear Name on QID",
                """\
                Dear Valued Customer,

                Greetings from Alfardan Exchange.

                Your profile verification was declined due to an unclear name on the QID. Please upload a clear and valid Qatar ID along with your selfie. Thank you.
                """,
            ),
            _t(
                "Missing QID Back",
                """\
                Dear Valued Customer,

                Greetings from Alfardan Exchange.

                Your profile verification was declined due to the missing back side of the Qatar ID. Please upload a clear and valid Qatar ID along with your selfie. Thank you.
                """,
            ),
            _t(
                "Inappropriate Selfie",
                """\
                Dear Valued Customer,

                Greetings from Alfardan Exchange.

                Your profile verification was declined due to an inappropriate selfie uploaded. Please upload a clear and valid Qatar ID along with your selfie. Thank you.
                """,
            ),
            _t(
                "QID Number Mismatch",
                """\
                Dear Valued Customer,

                Greetings from Alfardan Exchange.

                Your profile verification was declined due to a Qatar ID number mismatch. Please upload a clear and valid Qatar ID front and back along with your selfie and fill in the correct information. Thank you.
                """,
            ),
            _t(
                "Manual Verification - Branch Visit",
                """\
                Dear Valued Customer,

                Greetings from Alfardan Exchange.

                Manual verification is required. Please visit the branch for verification.
                """,
            ),
            _t(
                "Verification Declined - Visit Branch",
                """\
                Dear Valued Customer,

                Greetings from Alfardan Exchange.

                Your profile verification was declined. Kindly visit the nearest Alfardan Exchange branch for further assistance. Thank you.
                """,
            ),
        ),
    },
    {
        "name": "Internal - Notes & Prompts (EN)",
        "hotkey": "Ctrl+Alt+8",
        "color": "yellow",
        "templates": (
            _t(
                "Branch Visit - Al Khor",
                """\
                Dear Team,

                Please note that the customer will visit Al Khor Branch.
                """,
            ),
            _t(
                "Fraud - Digital Support Check",
                """\
                @Afx Digital Support

                Please confirm if there is no dispute or fraud-related issue reported on this transaction.
                """,
            ),
            _t(
                "Prompt - Name in English Caps",
                """\
                WRITE THIS NAME SPELLED IN ENGLISH IN CAPS:
                """,
            ),
            _t(
                "Prompt - Closest Occupation",
                """\
                FROM THE LIST, WHAT OCCUPATION IS CLOSEST TO:
                """,
            ),
        ),
    },
)
