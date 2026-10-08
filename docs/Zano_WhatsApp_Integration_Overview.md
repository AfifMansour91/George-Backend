# סוכן WhatsApp לזנו דגים - מפת האינטגרציה

**קהל:** אדיר, מפתח הבוט.
**גרסה:** 1.0, 8 באוקטובר 2026.
**מסמכים נלווים:** `Giorgio Partner API - מדריך אינטגרציה` (הזמנות, לקוחות, קטלוג, תשלום), `API חוקי אספקה של זנו דגים` (אזורי משלוח, חלונות זמן, הגבלות מוצר).

מסמך זה לא מחליף את שני המסמכים המפורטים. הוא עונה על שאלה אחת: לכל פעולה של הבוט, לאיזה API פונים, ומה מקור האמת כשיש חפיפה.

---

## 1. שני מקורות, שני תפקידים

הבוט עובד מול שתי מערכות:

| מערכת | כתובת | אימות | מה היא יודעת |
|---|---|---|---|
| **Giorgio** (מערכת הניהול של החנות) | `https://api.giorgio.co.il/Partner/v1` | `X-Api-Key: pk_...` | לקוחות, הזמנות, תמחור, מבצעים וקופונים, תשלום, סטטוס ליקוט ומשלוח, webhooks |
| **אתר WooCommerce** (zano-dagim.co.il) | `https://zano-dagim.co.il/wp-json/oc-shipping/v1` | `X-Api-Key` נפרד | אזורי משלוח וערים, מינימום הזמנה לאזור, חלונות זמן ושעות חיתוך, ימים סגורים, הגבלות מוצר (איסוף בלבד, ימי אספקה) |

שני המפתחות נפרדים ולא ניתנים להחלפה. ה-API של האתר הוא קריאה בלבד. ההזמנה עצמה נוצרת **תמיד** ב-Giorgio.

---

## 2. מקור האמת לכל נושא

| נושא | מקור אמת | הערה |
|---|---|---|
| האם ניתן לשלוח לעיר, ובאיזה אזור היא | **Woo** `GET /cities/{code}` | ל-Giorgio אין מושג "אזור". |
| חלונות זמן למשלוח / איסוף, ושעת החיתוך (`order_by`) | **Woo** `GET /availability` | Giorgio מחזיר רק ימים סגורים, בלי שעות. השעה שהלקוח בחר נשלחת ל-Giorgio כטקסט חופשי ב-`deliveryTime` / `pickupTime`. |
| ימים שהחנות סגורה בהם | **Woo** (ימים חסרים ב-`days`) | Giorgio `GET /Availability` מכיר את אותם ימים, אפשר להשתמש בו כבדיקה נוספת, אבל אין צורך בשתי קריאות. |
| מינימום הזמנה לאזור | **Woo** `areas[].min_order` | Giorgio לא אוכף מינימום. הבוט צריך לבדוק לפני יצירת ההזמנה (`POST /quote` באתר, או השוואה מקומית). |
| הגבלות מוצר: איסוף בלבד, ימי אספקה, ימי הכנה | **Woo** `restrictions` | Giorgio לא מכיר את ההגבלות האלה ולא יחסום הזמנה שמפרה אותן. |
| מחיר ליחידה / לק"ג, מחיר מבצע | **Giorgio** `GET /Products` | המחירים באתר הם העתק מסונכרן של Giorgio. |
| מלאי | **Giorgio** `inStock` | כנ"ל. |
| מבצעים, קופונים, הנחות | **Giorgio** `POST /Orders/Quote` | מחושב בשרת בלבד. |
| דמי משלוח שייגבו בפועל | **Giorgio** `POST /Orders/Quote` → `shippingCost` | ראו סעיף 4. |
| זיהוי לקוח, כתובת ברירת מחדל, כרטיס שמור, "כמו פעם שעברה" | **Giorgio** `GET /Customer*` | |
| יצירת הזמנה, ביטול, קישור תשלום | **Giorgio** `POST /Orders*` | |
| סטטוס הזמנה, משקל בפועל, לינק מעקב שליח | **Giorgio** webhooks / `GET /Orders/{id}` | |

---

## 3. זרימה מומלצת לשיחה

1. **אתחול (פעם בשעה, בקאש):** Giorgio `GET /Site`, `GET /Products?take=500`, `GET /Categories`; Woo `GET /areas`, `GET /products/restrictions`, `GET /version`. אם `rules_version` באתר השתנה, לרענן את החוקים.
2. **פתיחת שיחה:** Giorgio `GET /Customer?phone=`. אם נמצא: פנייה בשם, הצעת הכתובת השמורה ו-`GET /Customer/LastOrderItems`.
3. **כתובת למשלוח:** לקבל מהלקוח עיר, לתרגם לקוד עיר (Woo `GET /cities?search=`), ואז Woo `GET /cities/{code}` לאזור, לדמי משלוח ולמינימום. עיר שמחזירה 404 היא מחוץ לאזורי המשלוח, להציע איסוף.
4. **בניית סל:** מתוך הקטלוג של Giorgio. לכל מוצר שנוסף, לבדוק ב-Woo `restrictions` שהוא ניתן למשלוח לעיר (`!pickup_only || area_id in pickup_only_except_areas`) ושיום האספקה מותר (`delivery_weekdays`). מוצר עם `requiresVariant` ב-Giorgio דורש שאלת וריאציה.
5. **מועד אספקה:** Woo `GET /availability?city_code=&product_ids=` (או `method=pickup&location_id=`). להציג רק חלונות שה-`order_by` שלהם עדיין בעתיד.
6. **סיכום:** Giorgio `POST /Orders/Quote` עם הסל, סוג האספקה, העיר והקופון. להציג ללקוח את `lines`, `discountTotal`, `shippingCost`, `total`, ואם `hasWeightLines` להוסיף "הסכום הסופי לפי משקל בפועל". לוודא ש-`subTotal − discountTotal` עומד במינימום האזור.
7. **אישור ויצירה:** Giorgio `POST /Orders` עם `partnerRef` ייחודי. התאריך והחלון שנבחרו נשלחים ב-`deliveryDate` + `deliveryTime` (או `pickupDate` + `pickupTime`).
8. **תשלום:** אם `paymentMethod: "PaymentLink"`, Giorgio `POST /Orders/{id}/PaymentLink` ושליחת הקישור. אישור התשלום מגיע ב-webhook `order.payment_changed`.
9. **עדכונים ללקוח:** webhooks של Giorgio: `order.status_changed` (בהכנה / מוכנה / נמסרה), `order.delivery_changed` (לינק מעקב).
10. **"מה עם ההזמנה שלי?":** Giorgio `GET /Customer/Orders?phone=`. **"בטל":** Giorgio `POST /Orders/{id}/Cancel`, רק בסטטוס `New`.

---

## 4. מזהים ונקודות חפיפה שצריך להכיר

**מזהי מוצר שונים בין המערכות.** `productId` ב-Giorgio (למשל 12673) אינו ה-`product_id` של האתר (למשל 17745). שני ה-API-ים מחזירים `sku`, וזה שדה ההתאמה: לבנות מפה SKU → {Giorgio id, Woo id} באתחול. ההזמנה ל-Giorgio נשלחת עם מזהי Giorgio; הבדיקות ב-Woo (`product_ids` ב-`/availability`, `/quote`) עם מזהי האתר. אם יידרש, נוסיף את מזהה האתר ישירות לתשובת `GET /Products` של Giorgio.

**דמי משלוח.** לאתר יש 7 אזורים עם דמי משלוח ומינימום לאזור. ל-Giorgio יש טבלת דמי משלוח לפי עיר (כ-30 ערים) ודמי משלוח ברירת מחדל. הסכום שנגבה מהלקוח הוא זה שחוזר מ-Giorgio ב-`Quote`. אנחנו מיישרים את הטבלה של Giorgio לאזורי האתר לפני העלייה לאוויר; עד אז, אם הבוט רואה פער בין `shippingCost` של Giorgio לדמי המשלוח של האזור באתר, זו הודעה לנו ולא באג בבוט.

**שעות.** Giorgio לא מודל חלונות זמן. `deliveryTime` / `pickupTime` הם טקסט חופשי שמודפס על ההזמנה בחנות. לשלוח את החלון שנבחר בפורמט `HH:MM-HH:MM`.

**זמינות.** Giorgio מחזיר `SHOP_CLOSED` רק לימים שסומנו כסגורים בחנות. חלון שעבר את ה-`order_by` באתר לא ייחסם ב-Giorgio, ולכן בדיקת `order_by` היא באחריות הבוט לפני `POST /Orders`.

**קטלוג.** מספר המוצרים זהה בשתי המערכות (125). מוצרים שמסומנים `hidden` ב-Giorgio או מארזים (bundles) לא מופיעים ב-`GET /Products` של Giorgio ולא ניתנים להזמנה דרך הבוט בגרסה זו.

---

## 5. סביבות ומפתחות

| | Giorgio | Woo |
|---|---|---|
| Production | `https://api.giorgio.co.il/Partner/v1`, מפתח פרודקשן | `https://zano-dagim.co.il/wp-json/oc-shipping/v1` |
| Rate limit | 300 קריאות לדקה למפתח | 120 קריאות לדקה למפתח |
| Swagger / OpenAPI | `https://api.giorgio.co.il/swagger` → מסמך "Partner API" | `openapi.yaml` בתיקיית התוסף באתר |

המפתחות נשלחים בערוץ נפרד מהמייל. כל קריאה ל-Giorgio נרשמת אצלנו (בקשה, תשובה, משך) וזמינה לתחקור משותף.

---

## 6. מה לא קיים בגרסה זו

- מארזים (bundles) דרך הבוט.
- חלונות זמן ב-Giorgio (רק באתר).
- אכיפת מינימום הזמנה והגבלות מוצר ב-Giorgio (רק באתר, באחריות הבוט).
- webhooks על הזמנות שלא נוצרו דרך הבוט (הזמנות אתר/טלפון נראות רק ב-`GET /Customer/Orders`).
