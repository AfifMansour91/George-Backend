# Giorgio Partner API (v1) - מדריך אינטגרציה לספקי הזמנות חיצוניים

**קהל:** צוות הפיתוח של ספק חיצוני שבונה סוכן הזמנות (למשל סוכן WhatsApp) מעל מערכת Giorgio.
**Base URL:** `https://api.giorgio.co.il/Partner/v1` (סביבה אחת, פרודקשן. המפתח יימסר בנפרד).
**Swagger:** `https://api.giorgio.co.il/swagger` → בחרו את המסמך **Partner API**.
**גרסה:** v1 (2026-09-28).

---

## 1. עקרונות

1. **המפתח מזהה את החנות.** כל קריאה נושאת `X-Api-Key: pk_...` (או `Authorization: Bearer pk_...`). המפתח שייך לסניף אחד (Site). אין פרמטר `siteId` בשום קריאה. המפתח לא נחשף ללקוח הקצה ולא נשלח בדפדפן.
2. **המחירים נקבעים בשרת בלבד.** שורות הזמנה מכילות `productId`, `productVariantId`, `quantity`, `notes`. שדות מחיר אינם קיימים בבקשה. מבצעים, קופונים ודמי משלוח מחושבים על ידי Giorgio. הסוכן מציג ללקוח את מה שחוזר מ-`POST Orders/Quote`.
3. **Quote לפני Create.** תמיד הציגו ללקוח סיכום מתוך Quote וקבלו אישור מפורש לפני `POST Orders`. שני ה-endpoints משתמשים באותו מנוע תמחור, לכן הסכומים זהים.
4. **Idempotency.** שלחו `partnerRef` ייחודי לכל הזמנה (למשל מזהה השיחה + גרסת העגלה). שליחה חוזרת של אותו `partnerRef` מחזירה את ההזמנה הקיימת עם `alreadyExisted: true` ולא יוצרת כפילות. Retry הוא בטוח.
5. **מוצרים במשקל.** `soldBy: "weight"` → `quantity` בק"ג (1.5 = קילו וחצי). הסכום הסופי נקבע בליקוט לפי המשקל בפועל, לכן `hasWeightLines: true` בסיכום = "הסכום הסופי עשוי להשתנות מעט".
6. **וריאציות.** `requiresVariant: true` → חובה `productVariantId` מתוך `variants[]`. אחרת שגיאה `VARIANT_REQUIRED` (עם רשימת האפשרויות בטקסט השגיאה).
7. **מארזים (Bundles) אינם נתמכים ב-v1** ואינם מופיעים בקטלוג.

---

## 2. מעטפת תשובה

כל התשובות מוחזרות עם HTTP 200 והמעטפת הבאה. **בדקו `isSuccessful`, לא את קוד ה-HTTP.**

```json
{
  "isSuccessful": true,
  "statusCode": 0,
  "description": null,
  "data": { ... }
}
```

בשגיאה: `isSuccessful: false`, `statusCode` ≠ 0, ו-`description` בפורמט `"CODE: הודעה"`, למשל
`"VARIANT_REQUIRED: Product 10943 requires a ProductVariantId. Options: 14488=פרוסות | 14487=פרוסות ללא עור"`.

קודי HTTP שאינם 200: `401` מפתח חסר/שגוי, `429` חריגה ממכסת הקריאות (ראו §8), `5xx` תקלה בשרת (נסו שוב).

תאריכים: `yyyy-MM-dd` בבקשות (תאריך לוח ישראלי), ISO-8601 UTC (`Z`) בתשובות עבור חותמות זמן.

---

## 3. Endpoints

### 3.1 חנות

| Method | Path | תיאור |
|---|---|---|
| GET | `/Site` | פרטי החנות: שם, טלפון, כתובת איסוף, כשרות, דמי משלוח (ברירת מחדל + לפי עיר), סף משלוח חינם, אמצעי תשלום מופעלים, ימים סגורים, האם webhook מוגדר |
| GET | `/Availability?deliveryType=Pickup|Shipping&days=14` | לכל יום מהיום והלאה: `available` + `reason` (עברית) כאשר החנות סגורה. `suggestedTimeWindows` הן הצעות בלבד; `pickupTime`/`deliveryTime` הם טקסט חופשי |

`paymentMethods[].code` הם הערכים המותרים ב-`paymentMethod` בעת יצירת הזמנה (ראו §4).

### 3.2 קטלוג

| Method | Path | תיאור |
|---|---|---|
| GET | `/Categories` | קטגוריות פעילות לפי סדר תצוגה (`parentCategoryId` לעץ) |
| GET | `/Products?search=&categoryId=&inStockOnly=false&skip=0&take=50` | קטלוג הסניף במחירים אפקטיביים. `take` עד 500. מומלץ למשוך את כל הקטלוג פעם בשעה ולשמור בקאש; החיפוש בטקסט חופשי אינו סמנטי |
| GET | `/Products/{productId}` | מוצר בודד |

שדות מוצר חשובים: `effectivePrice` (המחיר לחיוב עכשיו, כולל מבצע קטלוגי פעיל), `soldBy` (`unit`/`weight`), `inStock`, `requiresVariant`, `variants[]` (`id`, `title`, `effectivePrice`, `inStock`), `options[]` (שם התכונה והערכים, למשל "צורת חיתוך": פרוסות / שלם), `imageUrl`, `approxUnitWeightGrams` (משקל משוער ליחידה כשידוע).

### 3.3 לקוחות

| Method | Path | תיאור |
|---|---|---|
| GET | `/Customer?phone=` | זיהוי לקוח לפי טלפון (כל פורמט: 05x, +972). `found: false` ללקוח חדש. מחזיר שם, כתובת ברירת מחדל, מספר הזמנות, `hasSavedCard` (+4 ספרות), `marketingSms` |
| GET | `/Customer/LastOrderItems?phone=` | פריטי ההזמנה האחרונה ("אותו דבר כמו פעם שעברה") |
| GET | `/Customer/Orders?phone=&skip=0&take=10` | היסטוריית הזמנות של הלקוח בסניף (כל הערוצים, גם אתר), מהחדשה לישנה. לשאלת "מה עם ההזמנה שלי?" |

### 3.4 הזמנות

| Method | Path | תיאור |
|---|---|---|
| POST | `/Orders/Quote` | תמחור עגלה ללא יצירה. מחזיר `isValid`, `errors[]` (לפי שורה), `lines[]` עם מחיר/הנחה, `subTotal`, `discountTotal`, `shippingCost`, `freeShippingApplied`, `total`, `couponApplied`/`couponMessage`, `promotionsApplied[]`, `promotionsNearby[]` ("הוסף עוד X כדי לקבל…") |
| POST | `/Orders` | יצירת הזמנה (ראו §4) |
| GET | `/Orders/{orderId}` | הזמנה לפי מזהה Giorgio (חייבת להשתייך לסניף של המפתח) |
| GET | `/Orders/ByRef/{partnerRef}` | הזמנה לפי ה-`partnerRef` שלכם |
| POST | `/Orders/{orderId}/Cancel` | ביטול. מותר רק להזמנות שנוצרו דרך ה-API ורק בסטטוס `New`. אחרת `ORDER_NOT_CANCELLABLE` (הלקוח צריך לפנות לחנות) |
| POST | `/Orders/{orderId}/PaymentLink` | דף תשלום מאובטח (Cardcom/PayPlus). מחזיר `paymentUrl` לשליחה בצ'אט, או `alreadyPaid: true` |

---

## 4. יצירת הזמנה - `POST /Orders`

```json
{
  "partnerRef": "wa:972501234567:conv-88:v3",
  "customerName": "משה כהן",
  "customerPhone": "0501234567",
  "customerEmail": null,
  "marketingSms": true,
  "deliveryType": "Shipping",
  "deliveryDate": "2026-10-01",
  "deliveryTime": "16:00-19:00",
  "deliveryStreet": "הרצל 12",
  "deliveryCity": "רחובות",
  "deliveryApartment": "4",
  "deliveryFloor": "2",
  "deliveryEntranceCode": "1234#",
  "paymentMethod": "PaymentLink",
  "couponCode": null,
  "customerNote": "לצלצל לפני",
  "items": [
    { "productId": 12673, "productVariantId": 14488, "quantity": 1.5, "notes": "בלי עור" },
    { "productId": 13197, "quantity": 2 }
  ]
}
```

כללים:

- `deliveryType`: `Pickup` (חובה `pickupDate`) או `Shipping` (חובה `deliveryDate`, `deliveryStreet`, `deliveryCity`).
- תאריך האספקה חובה תמיד, לא בעבר, ולא ביום שהחנות סגורה (`GET /Availability`).
- `paymentMethod` (ברירת מחדל `Cash`), חייב להיות מתוך `GET /Site → paymentMethods`:

| קוד | משמעות | מה הסוכן עושה אחרי היצירה |
|---|---|---|
| `Cash` | תשלום במסירה/איסוף | כלום |
| `PaymentLink` | אשראי בדף תשלום מאובטח | `POST /Orders/{id}/PaymentLink` ושליחת ה-URL ללקוח. תשלום מתקבל → webhook `order.payment_changed` עם `paymentStatus: "Paid"` |
| `SavedCard` | כרטיס שמור של הלקוח (רק כאשר `Customer.hasSavedCard = true`) | כלום. החנות תופסת מסגרת ומחייבת בסיום הליקוט |
| `OnAccount` / `BankTransfer` | הקפה / העברה בנקאית (אם החנות הפעילה) | כלום |

- `couponCode`: קופון לא תקף לא מפיל את ההזמנה; בדקו `couponApplied` ב-Quote לפני.
- התשובה היא אובייקט הזמנה (§5) עם `orderNumber` להצגה ללקוח.

---

## 5. אובייקט הזמנה

```json
{
  "orderId": 2044, "orderNumber": "88", "partnerRef": "wa:...", "alreadyExisted": false,
  "source": "WhatsApp",
  "status": "New",                 
  "paymentStatus": "Unpaid",       
  "paymentMethod": "CreditSms",    
  "paymentSettleStatus": "None",   
  "paidAt": null, "invoiceUrl": null,
  "deliveryType": "Shipping", "deliveryDate": "2026-10-01T00:00:00", "deliveryTime": "16:00-19:00",
  "pickupDate": null, "pickupTime": null,
  "deliveryAddress": "הרצל 12, רחובות", "deliveryStreet": "הרצל 12", "deliveryCity": "רחובות",
  "deliveryTrackingLink": null, "deliveryStatus": null,
  "customerName": "משה כהן", "customerPhone": "0501234567", "customerNote": "לצלצל לפני",
  "couponCode": null,
  "subTotal": 250.00, "discountTotal": 0.0, "shippingCost": 15.00, "total": 265.00,
  "bagsCount": null, "canCancel": true,
  "createdAt": "2026-09-28T09:12:00Z", "updatedAt": null,
  "inTreatmentAt": null, "readyAt": null, "completedAt": null, "cancelledAt": null,
  "items": [ { "orderItemId": 1, "productId": 12673, "productVariantId": 14488, "title": "סלמון", "variantTitle": "פרוסות",
               "quantity": 1.5, "pickedQuantity": null, "pricePerUnit": 120.00, "totalPrice": 180.00, "discountAmount": null, "notes": "בלי עור" } ]
}
```

| שדה | ערכים | הסבר ללקוח |
|---|---|---|
| `status` | `New` → `InTreatment` → `Ready` → `Completed`, או `Cancelled` | התקבלה → בהכנה/ליקוט → מוכנה (לאיסוף / יצאה למשלוח) → נמסרה |
| `paymentStatus` | `Unpaid`, `Paid`, `Refunded` | |
| `paymentSettleStatus` | `None`, `Initiated`, `Authorized` (מסגרת נתפסה), `Captured` (חויב), `Failed`, `Voided` | רלוונטי לאשראי בלבד |
| `paymentMethod` | `Cash`, `CreditSms` (= PaymentLink), `SavedCard`, `OnAccount`, `BankTransfer`, `WooCommerce` (הזמנות אתר) | |
| `pickedQuantity` | ממולא רק משלב `Ready` | המשקל/כמות בפועל |
| `total` | הסכום הסופי לאחר ליקוט משתנה למוצרי משקל | |

---

## 6. Webhooks (אירועים יוצאים)

Giorgio שולח `POST` ל-URL שתמסרו לנו (מוגדר לכל סניף) **רק עבור הזמנות שנוצרו דרך ה-Partner API** (`source` = `WhatsApp`/`Partner`). הזמנות אתר/טלפון לא נשלחות. ללא webhook, ניתן לבצע polling על `GET /Orders/{id}`.

| Event | מתי |
|---|---|
| `order.status_changed` | כל שינוי `status` (כולל יצירה: `New`, וביטול) |
| `order.payment_changed` | שינוי במצב התשלום (מסגרת נתפסה, חויב, נכשל, זיכוי). חתימת קישור התשלום ע"י הלקוח מגיעה כאן |
| `order.delivery_changed` | סטטוס שליח / לינק מעקב התעדכן (כשהחנות עובדת עם חברת משלוחים) |
| `webhook.test` | נשלח ידנית מהמערכת לבדיקת החיבור |

Headers: `X-Partner-Event` (שם האירוע), `X-Partner-Delivery` (מזהה ייחודי לדה-דופליקציה), `X-Partner-Signature: sha256=<hex HMAC-SHA256 של גוף הבקשה עם ה-secret>`, `Content-Type: application/json; charset=utf-8`.

גוף:

```json
{ "event": "order.status_changed", "eventId": "9f2c…", "occurredAt": "2026-09-28T10:00:00Z", "siteId": 45, "order": { ...אובייקט הזמנה מלא (§5)... } }
```

התנהגות: השרת שלכם צריך להחזיר 2xx תוך 15 שניות. אחרת ננסה שוב עד 3 פעמים (2 ש', 10 ש'). ייתכנו כפילויות ואירועים שלא בסדר כרונולוגי. השתמשו ב-`eventId` לדה-דופליקציה וב-`order.updatedAt`/`status` כמקור אמת. אימות חתימה:

```
expected = "sha256=" + hex(HMAC_SHA256(secret, raw_request_body_bytes))
compare constant-time with header X-Partner-Signature
```

---

## 7. קודי שגיאה

| קוד | מקור | משמעות / מה לעשות |
|---|---|---|
| `EMPTY_CART` | Quote/Create | אין פריטים |
| `INVALID_QUANTITY` | שורה | כמות חייבת להיות חיובית |
| `PRODUCT_NOT_FOUND` | שורה | מוצר לא קיים / לא בסניף / מוסתר. רעננו קטלוג |
| `VARIANT_REQUIRED` | שורה | למוצר יש וריאציות. ההודעה מכילה `id=שם` לכל אפשרות - שאלו את הלקוח |
| `VARIANT_NOT_FOUND` | שורה | ה-`productVariantId` לא שייך למוצר |
| `OUT_OF_STOCK` | שורה | אזל. הציעו חלופה |
| `NO_PRICE` | שורה | למוצר אין מחיר בסניף - פנו לחנות |
| `INVALID_DELIVERY_TYPE` | Create/Quote/Availability | רק `Pickup`/`Shipping` |
| `SUPPLY_DATE_REQUIRED` | Create | חסר `pickupDate`/`deliveryDate` |
| `SUPPLY_DATE_IN_PAST` | Create/Quote | תאריך עבר |
| `SHOP_CLOSED` | Create/Quote | החנות סגורה ביום הזה לסוג האספקה. הציעו יום אחר מ-`/Availability` |
| `ADDRESS_REQUIRED` | Create | משלוח בלי רחוב/עיר |
| `CUSTOMER_REQUIRED` / `PHONE_REQUIRED` | Create/Customer | חסר שם/טלפון |
| `INVALID_SOURCE` | Create | `source` אינו `WhatsApp`/`Partner` |
| `PAYMENT_METHOD_NOT_ALLOWED` | Create | לא ברשימת `GET /Site → paymentMethods` |
| `SAVED_CARD_MISSING` | Create | `SavedCard` ללקוח בלי כרטיס. השתמשו ב-`PaymentLink` |
| `GATEWAY_NOT_CONFIGURED` | PaymentLink | לחנות אין סליקה. הציעו מזומן |
| `ORDER_NOT_FOUND` | Orders | לא קיים או שייך לסניף אחר |
| `ORDER_NOT_CANCELLABLE` | Cancel | כבר בטיפול/מוכנה, או לא נוצרה דרך ה-API |
| `INVALID_REQUEST` | כללי | ולידציה כללית, ראו את ההודעה |

---

## 8. מכסות, לוגים, סביבות

- **Rate limit:** 300 קריאות לדקה לכל מפתח (חלון גולש). מעבר לכך `429` עם `Retry-After: 10`. משיכת קטלוג מלאה נחשבת קריאה אחת (`take=500`).
- **לוגים:** כל קריאה נרשמת אצלנו (בקשה, תשובה, משך) ונגישה לצוות Giorgio לצורך תחקור משותף. אל תשלחו נתונים שאינם נחוצים.
- **סביבה:** סביבת פרודקשן בלבד, אין סביבת בדיקות נפרדת. לבדיקות משתמשים בהזמנות אמיתיות עם הערה "בדיקה" ומבטלים אותן (`POST /Orders/{id}/Cancel` בסטטוס `New`); תאמו איתנו לפני סבב בדיקות כדי שהחנות תדע.
- **החלפת מפתח:** המפתח ניתן לביטול/החלפה מיידית מצד Giorgio. שמרו אותו בסוד ניהולי, לא בקוד.

---

## 9. זרימה מומלצת לסוכן

1. `GET /Site` פעם בהתחלה (ובקאש שעה): שעות סגירה, אמצעי תשלום, דמי משלוח.
2. עם תחילת שיחה: `GET /Customer?phone=` → פנייה בשם, כתובת ברירת מחדל, `GET /Customer/LastOrderItems` ל"כמו פעם שעברה".
3. בניית עגלה מהקטלוג (`GET /Products`, קאש שעה). שאלת וריאציה כשנדרש, אימות יחידות למוצרי משקל.
4. `POST /Orders/Quote` → הצגת סיכום (שורות, הנחות, משלוח, סה"כ, הערת "לפי משקל בפועל"). אישור מפורש מהלקוח.
5. `POST /Orders` עם `partnerRef` ייחודי. אם `PaymentLink` → `POST /Orders/{id}/PaymentLink` ושליחת הקישור.
6. הודעות המשך ללקוח מ-webhooks: "ההזמנה בהכנה", "מוכנה לאיסוף", "התשלום התקבל", "יצאה למשלוח" (לינק מעקב).
7. "מה עם ההזמנה שלי?" → `GET /Customer/Orders?phone=`.
8. "בטל" → `POST /Orders/{id}/Cancel` (רק ב-`New`, אחרת להפנות לטלפון החנות מ-`GET /Site`).
