# Giorgio AI Product Studio — אפיון ותוכנית מימוש

**Project:** ShopManager / George Backend + shop-manager
**Feature:** צילום מוצר בטלפון → תמונת מוצר ברמת צלם מקצועי → מוצר מוכן לחנות בפחות משתי דקות
**Reference:** התוסף `oc-snap` של גורג' (WooCommerce), `WooCommerceEngines/GeorgeGithub/oc-snap`, דף נחיתה https://ocpluginz.com/oc-snap.html
**Date:** 2026-10-29
**Status:** Proposal (for review) — מימוש מתחיל אחרי אישור ההחלטות בסעיף 12

---

## תקציר מנהלים

התוסף של גורג' עושה את הדבר הנכון בזרימה (סריקה → צילום → AI → מחיר → פרסום) אבל מייצר תמונות חלשות כי הוא **גוזר את המוצר (remove.bg) ומדביק אותו על רקע שנוצר בנפרד**. התאורה של המוצר והרקע לא תואמות, הצל מזויף, הרקע נוצר ב-1024px באיכות `medium`, והתוצאה נראית "מודבקת". בנוסף, המודל שהתוסף משתמש בו (`gpt-image-1`) **הוצא משירות ב-23.10.2026** — המצב הישיר (DirectProvider) של התוסף כנראה כבר לא עובד.

ב-Giorgio נבנה את זה אחרת:

1. **מעבר אחד במודל עריכה מתקדם** — שולחים למודל את תמונת המוצר המקורית + תמונת רפרנס של הסגנון (למשל תמונה אמיתית מ-deliz.co.il), והמודל מרנדר מחדש את הסצנה כולה: תאורה, צללים, השתקפויות, עומק שדה — עם המוצר עצמו שמור. אין גזירה והדבקה.
2. **מודל ייעודי לצילום מוצר:** Google **Gemini 3 Pro Image (Nano Banana Pro)** כמנוע ראשי — הטוב ביותר בשימור המוצר ובריאליזם פיזיקלי של חומרים ותאורה, פלט 2K/4K. **OpenAI gpt-image-2.5-sunburst** כמנוע משני למוצרים ארוזים עם טקסט על האריזה. שניהם מאחורי ממשק אחד (`IImageModelProvider`) כך שאפשר להחליף.
3. **Claude Opus 5.5 (ראייה ממוחשבת)** לזיהוי המוצר, כתיבת שם/תיאור בעברית, הצעת אפשרויות משקל, ו**שופט איכות** שמשווה את התוצאה למקור ופוסל סטייה במוצר.
4. **ארבעה "מראים" + שיפור תמונה קיימת + יצירה ללא תצלום:** רקע לבן סטודיו, רקע כהה (deliz), שיש בהיר (meat-shop), אווירה (תלוי מוצר). לכל חשבון אפשר להגדיר רפרנס משלו. למוצרים שאין להם תצלום (קטלוג גלובלי, ייבוא CSV) אותם מנועים מייצרים תמונה מתיאור, עם אזהרה למוצרים ממותגים.
5. **מהירות = אפקט ה-WOW:** תצוגה מקדימה ב-1K תוך ~6–10 שניות, גרסה סופית ב-2K ברקע. הטלפון לא מחכה — ממשיכים למלא את הטופס.
6. **שני מודלי שימוש לחשבון:** *קהילתי/חינם* (התמונות נתרמות למאגר הגלובלי שכבר קיים ב-Giorgio — Media.IsGlobal) או *פרטי/בתשלום* (קרדיטים לפי עלות טוקנים, חבילות נרכשות בקארדקום בדיוק כמו חבילות ה-SMS השיווקי).

**מסקנה:** רוב התשתית קיימת (Media library גלובלי/חשבוני, העלאת קבצים, hosted services, SignalR, ledger קרדיטים וחבילות של המודול השיווקי, feature-gate לחשבון). העבודה החדשה היא מנוע התמונות, הזרימה הניידת, ומסך ההגדרות.

---

## 1. מה oc-snap עושה היום, ולמה האיכות חלשה

| שלב | מימוש ב-oc-snap | הבעיה |
|---|---|---|
| צילום | PWA, הקטנה ל-2048px JPEG 86% בצד הלקוח | טוב, נשמור את הרעיון |
| גזירת רקע | remove.bg (`type=product`), fallback ל-`gpt-image-1` עם רקע שקוף | ה-fallback **מצייר מחדש** את המוצר (`regenerated=true`); remove.bg חותך קצוות רכים (פרווה, אדים, שומן בבשר) |
| רקע | `gpt-image-1`, 1024px, `quality=medium`, פרומפט "empty backdrop" | רזולוציה נמוכה; הרקע נוצר **בלי לראות את המוצר**, אז התאורה לא תואמת |
| הרכבה | Imagick: resize + `shadowImage` + `compositeImage` | צל גנרי, אין השתקפות, אין אינטראקציה עם המשטח — נראה מודבק |
| פלט | WebP 86 / JPEG 88, 2048px max | הרקע מוגדל מ-1024 → מריחה |
| שיפור | `autoLevel`, `sigmoidalContrast`, `unsharp` | פילטר דטרמיניסטי, לא "שיפור" אמיתי |
| מודלים | `gpt-image-1`, `gpt-4.1-mini` | `gpt-image-1` הוצא משירות 23.10.2026 |

הרעיונות שכן שווה לקחת: הזרימה בת 5 הצעדים, מפתח idempotency ליצירת מוצר, תור עבודות עם refund בכישלון, "נסה שוב" בחינם פעם אחת, החלפה בין "AI" ל"מקורית", והטקסט המוצע מהתמונה עם חיפוש ברשת לפי ברקוד.

---

## 2. יעדים

- **איכות:** תמונה שנראית כאילו צולמה בסטודיו מקצועי. המוצר **זהה** למקור (צורה, צבע, טקסט על האריזה). בלי ארטיפקטים בקצוות.
- **מהירות:** תצוגה מקדימה ראשונה ≤ 10 שניות מלחיצת "צלם". גרסה סופית ≤ 30 שניות ברקע.
- **פשטות:** "צילמתי, קיבלתי, פרסמתי" — ברירת מחדל חכמה, אפשרויות רק למי שרוצה.
- **כלכליות:** עלות מודל ידועה לכל פעולה, תקרות יומיות לחשבון, refund אוטומטי בכישלון.

---

## 3. בחירת מודלים (מחקר 29.10.2026)

| תפקיד | מודל | למה | עלות (Standard) |
|---|---|---|---|
| **מנוע תמונה ראשי (אחרי ה-spike)** | `gpt-image-2.5-sunburst` | ניצח ב-spike (סעיף 14): נאמנות גבוהה יותר גם בבשר וגם באריזות, שמירה מושלמת על טקסט עברי/לועזי, מציית להנחיות "הסר את נייר הכסף". `low` = preview ב-~15 שניות, `high` = final ב-~45 שניות | ~$0.01 (low 1024) / ~$0.05 (medium) / ~$0.2 (high 1536) לתמונה |
| **מנוע משני** | `gemini-3-pro-image` (Nano Banana Pro) | מהיר פי 2 ב-2K (~26 שניות), אסתטיקה מצוינת לבשר/דגים, אבל סוטה מהמוצר (ציור מחדש של חזה עוף, שאריות ציוד סטודיו) ונחלש באריזות. ישמש כרזרבה/A-B ולמצב `generate` | $0.134 ל-1K/2K, $0.24 ל-4K; Batch/Flex חצי |
| **מנוע זול למצב הקהילתי (אופציה)** | `gemini-nano-banana-2.1` | זול פי 4; לא נבדק עדיין | $0.034 (1K), $0.050 (2K), $0.113 (4K) |
| **רזרבה / self-host** | FLUX.2 Pro / Kontext (Black Forest Labs) | זול ($0.03–0.045/MP), משקולות פתוחות; אם נרצה להוריד עלויות בעתיד | $0.03–0.08 |
| **ראייה + טקסט + שופט** | `claude-opus-5-5` (Anthropic SDK ל-C#) | זיהוי מוצר, שם/תיאור בעברית, קטגוריה, הצעת משקלים, ציון נאמנות של התוצאה מול המקור | ~$0.01–0.02 לקריאה |

> **לא להשתמש ב-remove.bg.** במודלים של 2026 הגזירה מיותרת — המודל מקבל את התמונה המלאה ומבין בעצמו מה המוצר.

**החלטה מומלצת (עודכן אחרי ה-spike, 29.10.2026):** `gpt-image-2.5-sunburst` כברירת מחדל לכל המראים, גם ל-preview (איכות `low`) וגם ל-final (איכות `high`). אותו מנוע לשני השלבים כדי שה-final ייראה כמו ה-preview שהמשתמש אישר. Gemini נשאר מאחורי `IImageModelProvider` כרזרבה ולמצב `generate`, והניתוב לפי מראה/קטגוריה נשמר בפריסט כך שאפשר להחליף בלי קוד.

**SynthID:** כל תמונה מ-Gemini מכילה סימן מים בלתי נראה. אין השפעה ויזואלית; כדאי לציין בתנאי השימוש.

---

## 4. ארכיטקטורה

```
Phone / Desktop (shop-manager)
  צילום → הקטנה ל-2048 בצד הלקוח → POST /General/File/Upload (קיים)
  → POST /AiStudio/Jobs { sourceMediaId, look, options }
        │
        ▼
AiStudioService (scoped)
  בדיקת feature-gate + קרדיטים → AiImageJob(queued) → debit ledger
        │
        ▼
AiStudioJobHostedService (BackgroundService, תבנית MarketingDispatchHostedService)
  1. PromptBuilder: look + category + brand preset + reference images
  2. IImageModelProvider.EditAsync(source, refs, prompt, size=1K)   ← preview
  3. QualityJudge (Claude): fidelity score, 1 retry אם < סף
  4. ImagePostProcess (Magick.NET, כבר בפרויקט): resize, EXIF strip, WebP/JPEG q92
  5. IFileStorage.Upload → Media(Origin=ai, AiJobId) → job.done(preview)
  6. ברקע: אותו prompt ב-2K → job.final  (או 4K לפי בקשה)
        │
        ▼
SignalR AiStudioHub ("JobUpdated") + GET /AiStudio/Jobs/{id} (polling fallback)
```

**עקרונות:** שימוש חוזר בתשתית הקיימת (Media, IFileStorage, hosted services, SignalR, PlatformBilling); ממשק ספק אחד להחלפת מודלים; הטלפון לעולם לא מחכה לספק; כל חיוב דרך ledger עם refund בכישלון.

---

## 5. המראים (Looks) והנדסת הפרומפט

הפרומפטים באנגלית (המודלים קוראים אותה הכי טוב), התוויות בעברית. כל מראה = פרומפט + תמונות רפרנס + פרמטרים. הפריסטים גלובליים (super-admin) עם אפשרות override לחשבון/אתר.

| מראה | תווית | רפרנס ברירת מחדל | מפתחות בפרומפט |
|---|---|---|---|
| `studio_white` | רקע לבן | ללא (פרומפט בלבד) | seamless white sweep, large softbox key light upper-left, fill right, soft contact shadow, slight floor reflection, 85mm, f/8, neutral white balance |
| `dark` | רקע כהה | 2–3 תמונות מ-deliz.co.il | charcoal/black slate, single dramatic rim light, deep shadows, glossy moist surfaces for meat, low-key, premium |
| `marble` | שיש בהיר | 2–3 תמונות מ-meat-shop.co.il | white marble with soft grey veins, window daylight from the side, airy, high-key, shallow depth of field |
| `ambience` | אווירה | נבחר לפי קטגוריה | סצנה + אביזרים (Props table למטה), eye-level or 45°, product is the hero and in focus, props out of focus |
| `enhance` | שיפור תמונה | — | keep the exact composition and background; fix exposure, white balance, sharpness, remove noise and glare; no content changes |
| `generate` | צור תמונה מתיאור | לפי המראה שנבחר (לבן/כהה/שיש/אווירה) | **ללא תמונת מקור.** Claude מרחיב את שם המוצר + הקטגוריה לתיאור ויזואלי (חתך, צבע, מרקם, כמות, אופן הגשה) והמודל מייצר מאפס. מתאים למוצרים גנריים (חזה עוף, פילה סלמון, זיתים) ולמוצרי הקטלוג הגלובלי שאין להם תצלום. **לא מתאים** למוצרים ממותגים עם אריזה: המודל יצייר אריזה מומצאת |

**מצב `generate` (יצירה ללא תצלום):** אותו `IImageModelProvider`, קריאת `GenerateAsync` בלי `SourceBytes`. הזרימה: Claude מקבל שם, קטגוריה, מותג ותיאור ומחזיר `visualBrief` (באנגלית) + `isBrandedPackaged`. אם המוצר ממותג/ארוז, ה-UI מזהיר ("התמונה תהיה המחשה, לא המוצר עצמו") ודורש אישור. השופט במצב הזה בודק רק אסתטיקה והתאמה לתיאור, כי אין מקור להשוות אליו. התמונה מסומנת `Media.Origin=ai_generated` (לעומת `ai` לעריכה) כדי שבמאגר הקהילתי יהיה ברור שזו המחשה. נקודות כניסה: כפתור "✨ צור תמונה" בגלריית המוצר כשאין תמונות, פעולה מרובה בקטלוג הגלובלי ובייבוא CSV למוצרים ללא תמונה.

**חוקים קבועים בכל פרומפט:**

```
Keep the product EXACTLY as in the input photo: same shape, proportions, colours,
texture, label text and logos. Do not add, remove, rotate or restyle the product.
No text, no watermark, no people, no hands. Photorealistic, commercial product
photography, high resolution, sharp focus on the product.
```

**טבלת אביזרים למראה "אווירה" (לפי קטגוריה, Claude בוחר):**

| קטגוריה | סצנה | אביזרים |
|---|---|---|
| בשר / עוף | wooden butcher block, dark backdrop | garlic cloves, rosemary sprig, cracked pepper, coarse salt |
| דגים | crushed ice tray, cool light | lemon wedge, dill, sea salt |
| שמן זית / רטבים | rustic table, warm light | olives, bread, linen cloth |
| פטריות / ירקות | linen on wood, daylight | fresh herbs, soil hints, a knife |
| מאפים | marble counter, morning light | flour dust, wheat ears, coffee cup |
| גבינות | slate board | grapes, honey, walnuts |
| ריהוט | styled room matching the item, natural window light | plant, rug, book |
| קוסמטיקה | glossy surface, pastel backdrop | water droplets, petals |
| ברירת מחדל | neutral studio table | none |

**אסטרטגיית רזולוציה (נמדד):** preview = gpt-image-2.5 `low` 1024px (~15 שניות, איכות מספיקה לאישור), final = `high` 1536–2048px (~45 שניות, ברקע). 4K/3840px באופציה לתמונת hero. יחס צדדים לפי הגדרת האתר (1:1 ברירת מחדל, 4:5, 3:4).

**הנחיית "לא חלק מהמוצר" (ממצא מרכזי של ה-spike):** המודלים מצייתים ל"שמור על המוצר בדיוק" עד כדי כך שהם משאירים נייר כסף, מגש קלקר או דלפק שנוגעים במוצר. לכן שלב הזיהוי של Claude (`IdentifyAsync`) מחזיר גם `incidental`: תיאור של מה שבתמונה **אינו** המוצר (נייר כסף, מגש, יד, דלפק), ו-`PromptBuilder` מזריק משפט כמו "The crumpled aluminium foil and the counter are NOT part of the product. Remove them and place the chicken breast directly on the new surface." בלי זה: חזה עוף על נייר כסף על שיש. איתו: חזה עוף על שיש. לעומת זאת אריזה אמיתית (מגש בשר טחון עם תווית, שקית ואקום) היא כן המוצר ונשמרת.

**בקרת איכות:** לכל preview Claude מקבל את המקור והתוצאה ומחזיר JSON: `fidelity 0–1`, `aesthetic 0–1`, `issues[]`. מתחת ל-0.8 נאמנות → ריצה חוזרת אוטומטית פעם אחת (על חשבון הפלטפורמה). הציון נשמר ב-job למעקב. המשתמש יכול לסמן 👍/👎 — זה המשוב שיכוון את הפרומפטים בהמשך.

---

## 6. חוויית המשתמש

### 6.1 "מוצר מהיר" (הזרימה הניידת, עמוד חדש `QuickProduct`)

עמוד ב-shop-manager שמותאם לטלפון (לא PWA נפרד — אותו login, אותו JWT, אותם API). שלבים כמו ב-oc-snap:

1. **סריקה (אופציונלי)** — ברקוד/PLU במצלמה. נמצא מוצר קיים → קופצים לעריכת התמונות שלו. לא נמצא → ממשיכים, הברקוד נשמר ב-SKU/Barcode.
2. **צילום** — `<input type="file" accept="image/*" capture="environment">`, הקטנה ל-2048 בצד הלקוח (כמו `image.js` בתוסף), העלאה מיידית.
3. **בחירת מראה** — ארבעה אריחים (לבן / כהה / שיש / אווירה) + "שיפור בלבד". ברירת המחדל של האתר מתחילה לרוץ **מיד כשהתמונה עולה**, עוד לפני שבוחרים. האריח מציג shimmer ואז את ה-preview. לחיצה על אריח אחר מריצה מראה נוסף. מתג "AI / מקורית".
4. **פרטים** — שם, תיאור קצר, קטגוריה, מותג: כפתור "✨ כתוב ב-AI" ממלא מהתמונה (Claude, בעברית, טון לפי הגדרת החשבון, חיפוש ברשת לפי ברקוד כשיש).
5. **מחיר ומשקל** — ראו 6.2.
6. **שמירה** — טיוטה/פרסום לפי הרשאה, סנכרון ל-Woo דרך הזרימה הקיימת. מסך סיום: "המוצר באוויר" + "צור מוצר דומה".

### 6.2 שדה משקל ומתג "מוצר שקיל" (ההערה של גורג')

מתחת לתיאור: שדה **משקל** (ק"ג, מספר). משמאלו מתג **מוצר שקיל**.

- מתג כבוי → המוצר `setup_type=standard`, `Product.Weight` = הערך בשדה.
- מתג דלוק → שדה המשקל נעלם ובמקומו רשימה נגללת של עד **4 אפשרויות משקל** (למשל 0.5 / 1 / 1.5 / 2 ק"ג), כל אחת עם: תווית, משקל, מחיר מחושב מ-₪/ק"ג (ניתן לעריכה), פעיל/לא פעיל. ממופה ל-`setup_type=by_unit`, `unit_weight_mode=variable`, `WeightConfig.WeightOptions` בפורמט הקיים (`weightOptionsCodec.ts`, active/inactive עם `##`). Claude מציע את 4 האפשרויות לפי הקטגוריה (בשר: 0.5/1/1.5/2; דגים: 1/1.5/2/3).
- הטופס המלא (`TemplateProductForm`) נשאר כפי שהוא; הזרימה המהירה היא "חזית פשוטה" לאותם שדות.

> שאלה פתוחה לגורג'/אפיף: האם "4 אפשרויות" הן אפשרויות משקל ליחידה (כמו `WeightOptions` היום) או וריאנטים עם מחיר נפרד לכל אחת? האפיון מניח את הראשון.

### 6.3 שיפור תמונה קיימת

- ב-`ImageGallery` של טופס המוצר: תפריט ⋮ על כל תמונה → "✨ שפר ב-AI" → תת-תפריט של המראים. התוצאה נוספת כתמונה חדשה לצד המקור (לא מחליפה), עם תג "AI".
- בספריית המדיה: בחירה מרובה → "שפר ב-AI" (עבודות batch, מחיר Batch של Gemini = חצי).

### 6.3א יצירת תמונה ללא תצלום (מצב `generate`)

- בגלריית המוצר: כפתור "✨ צור תמונה מתיאור" (תמיד זמין כשהמודול פעיל) פותח דיאלוג עם **שדה "מה צריך להופיע בתמונה?"** (ממולא מראש מהתיאור הקצר, ובעורך המארז מרשימת הרכיבים: "מארז X הכולל: 3 × אנטריקוט, 4 × המבורגר…") ו**בחירת רקע** (לבן / כהה / שיש / אווירה, `background` בבקשה). Claude בונה מהשם + הקטגוריה + התיאור brief ויזואלי באנגלית; אם מהמילים לא ברור מה לצייר (מארז בלי תכולה, שם קוד) הוא מחזיר `ambiguous=true` + שאלה בעברית, והעבודה נכשלת לפני שמוציאים קרדיט עם השאלה כהודעה. הפרומפט של הרקע זהה לזה של מראה העריכה המקביל (כולל תמונות הרפרנס של כהה/שיש) כדי שתמונה מיוצרת תשב ליד תמונה ערוכה באותה חנות. למוצר ממותג/ארוז מוצגת אזהרה שהתמונה היא המחשה. (לקח מ-QA 29.10.2026: מארז שנשלח עם שם בלבד יצא כחבילת נייר טואלט.)
- "שפר ב-AI" על תמונה קיימת: כפתור ✨ על כל תמונה (וגם בתפריט ⋮) פותח את אותו דיאלוג עם אריחי המראים במקום תת-תפריט.
- בקטלוג הגלובלי ובייבוא CSV: פעולה מרובה "צור תמונות למוצרים ללא תמונה" (batch, חצי מחיר).
- הזרימה הניידת לא משתנה: שם מוצר ללא צילום → המערכת מציעה "ליצור תמונה מתיאור?" במקום אריחי המראים.

### 6.4 הגדרות

- **לחשבון** (Settings → סטודיו AI): מראה ברירת מחדל, יחס צדדים, רזולוציה סופית, טון טקסט, שפה, תמונות רפרנס למראה (העלאה), מצב שימוש (קהילתי/פרטי), תקרה יומית.
- **Super-admin** (עמוד בסגנון `SmsAdminPage`): פריסטים גלובליים (פרומפט לכל מראה + טבלת אביזרים), ספק/מודל לכל מראה, מחירון קרדיטים, חבילות, יתרות לכל חשבון, יומן עבודות עם ציוני השופט ועלות טוקנים.

---

## 7. מודל עסקי וקרדיטים

| מצב | מי משלם | מה קורה לתמונה |
|---|---|---|
| **קהילתי (חינם)** | הפלטפורמה (מודל מהיר, 1K→2K) | `Media.IsGlobal=true`, `LicensePool=true`, זמינה לכל החשבונות בטאב "גלריה ראשית" הקיים ב-MediaPicker; המשתמש מאשר תרומה בפעם הראשונה (checkbox + תנאים). תמונות עם זיהוי מותג/תווית פרטית מסומנות ע"י Claude ולא נכנסות למאגר. |
| **פרטי (בתשלום)** | החשבון, בקרדיטים | `Media.IsGlobal=false`, שייכת לחשבון/אתר בלבד |

**קרדיט ≈ ₪0.50 לפני מע"מ** (החלטה 29.10: מחיר = עלות ספק ×2; ה-₪0.10 של התוסף נמוך מהעלות). חבילות ההתחלה: 100/₪50, 300/₪135, 1000/₪400, 3000/₪1050 (`AiCreditPackage`, הסופר-אדמין עורך). מחירון בקרדיטים:

| פעולה | קרדיטים | עלות ספק משוערת |
|---|---|---|
| שיפור תמונה | 2 | $0.03–0.05 |
| לבן / כהה / שיש (preview 1K + final 2K, Pro) | 4 | ~$0.27 |
| אווירה (כולל בחירת אביזרים ב-Claude) | 6 | ~$0.30 |
| שדרוג ל-4K | +3 | +$0.24 |
| צור תמונה מתיאור (ללא תצלום, Pro, 2K) | 5 | ~$0.16 |
| טקסט מהתמונה | 1 | ~$0.02 |
| טקסט + חיפוש ברשת | 2 | ~$0.04 |
| נסה שוב (אותו מראה) | חינם פעם אחת | — |

מימוש: `AiCreditLedger` (עותק של `MarketingQuotaLedger`: allocation / purchase / consumption / refund, יתרה = SUM), `AiCreditPackage` (עותק של `MarketingPackage`), רכישה דרך `PlatformBilling` + Cardcom בדיוק כמו `MarketingService.Purchases`. Feature-gate: `Account.AiStudioEnabled` (תבנית `MarketingEnabled`) + `Account.AiStudioMode` (`community` | `paid`).

---

## 8. מודל נתונים (SQL ב-`Core/George.DB/Scripts`, להריץ על prod לפני deploy)

```
AiImageJob
  Id, GuidId, CreationTime, StartedAt, FinishedAt
  AccountId, SiteId, UserId, ProductId?
  SourceMediaId? (NULL במצב generate), Look (studio_white|dark|marble|ambience|enhance|generate), Stage (preview|final)
  VisualBrief nvarchar(2000)? (התיאור הוויזואלי ש-Claude בנה במצב generate)
  Options (JSON: ratio, size, scene, categoryName, providerOverride)
  Status (queued|running|done|failed), Error
  Provider, Model, PromptHash, Prompt (nvarchar(max))
  PreviewMediaId?, ResultMediaId?
  Credits, Refunded bit, RetryOfJobId?
  FidelityScore decimal(4,3)?, AestheticScore?, JudgeIssues (JSON)
  InputTokens, OutputTokens, ProviderCostUsd decimal(10,5)
  UserRating tinyint? (1 = 👍, 0 = 👎)
  IsShared bit (נתרם למאגר)

AiCreditLedger      -- כמו MarketingQuotaLedger (AccountId, EntryType, Amount, RefJobId, Note)
AiCreditPackage     -- כמו MarketingPackage (Credits, Price, IsActive, SortOrder, Badge)
AiCreditPurchase    -- כמו MarketingPurchase (Cardcom LowProfile → ledger entry)

AiStylePreset
  Id, AccountId? (NULL = גלובלי), SiteId?, Look, Label, Prompt, NegativePrompt
  Provider?, Model?, Ratio, PreviewSize, FinalSize
  ReferenceMediaIds (JSON), CategoryPropsJson (למראה אווירה), IsActive, SortOrder

Account  + AiStudioEnabled bit, AiStudioMode nvarchar(20), AiStudioDailyCap int?
Site     + AiDefaultLook, AiImageRatio, AiTextTone
Media    + Origin (upload|ai|ai_generated|import), AiJobId?, LicensePool bit
```

---

## 9. Backend (George.Services/AiStudio)

| קובץ | תפקיד |
|---|---|
| `IImageModelProvider.cs` | `Task<ImageResult> EditAsync(ImageEditRequest)` + `Task<ImageResult> GenerateAsync(ImageGenerateRequest)`; `ImageEditRequest { SourceBytes, ReferenceImages[], Prompt, Ratio, Size, Model }`; `ImageGenerateRequest { ReferenceImages[], Prompt, Ratio, Size, Model }`; `ImageResult { Bytes, Mime, Model, InputTokens, OutputTokens, CostUsd }` |
| `GeminiImageProvider.cs` | REST `POST https://generativelanguage.googleapis.com/v1beta/interactions` עם `input` (text + images base64) ו-`response_format { type: image, aspect_ratio, image_size }`; `previous_interaction_id` לשדרוג preview→final באותו הקשר |
| `OpenAiImageProvider.cs` | `POST /v1/images/edits` multipart (`image[]`, `prompt`, `quality`, `size`) |
| `ProductVisionService.cs` | Anthropic SDK (`claude-opus-5-5`, adaptive thinking, structured outputs): `IdentifyAsync` (שם, תיאור, קטגוריה, מותג, isPackagedWithText, suggestedWeightOptions, sceneProps), `BriefAsync` (שם+קטגוריה → visualBrief באנגלית + isBrandedPackaged, למצב generate), `JudgeAsync` (fidelity, aesthetic, issues) |
| `PromptBuilder.cs` | פריסט + קטגוריה + אביזרים + חוקים קבועים → prompt |
| `ImagePostProcess.cs` | Magick.NET (`Magick.NET-Q8-AnyCPU` כבר ב-George.Services): resize ל-FinalSize, strip metadata, WebP q92 (או JPEG לפי הגדרה), שמירת ICC |
| `AiStudioService.cs` | יצירת job, בדיקת gate/קרדיטים/תקרה יומית, debit, קריאה/ביטול, דירוג, הצעות טקסט (סינכרוני, 5–8 שניות) |
| `AiCreditService.cs` | ledger, חבילות, רכישה (העתקה מ-`MarketingService.Purchases`) |
| `AiStudioJobHostedService.cs` | BackgroundService; claim אטומי (`UPDATE ... WHERE Status='queued'`), מקביליות 3, timeout 120s, job תקוע >3 דקות → failed+refund, backoff בכישלון ספק |
| `AiStudioController.cs` | `POST /AiStudio/Jobs`, `GET /AiStudio/Jobs/{id}`, `POST /AiStudio/Jobs/{id}/Retry`, `POST /AiStudio/Jobs/{id}/Rate`, `POST /AiStudio/Suggest` (טקסט מהתמונה, **בוצע בשלב 2**: `AiSuggestReq { SourceUrl, ProductName?, CategoryName?, Categories[], Barcode? }` → `AiSuggestRes { Name, ShortDescription, LongDescription, CategoryName, BrandName, IsPackaged, IsWeighted, WeightOptionsKg[], UnitWeightKg, Credits }`), `GET /AiStudio/Credits`, `GET/POST /AiStudio/Packages`, `GET/PUT /AiStudio/Presets`, `GET /AiStudio/Settings` |
| `Hubs/AiStudioHub.cs` | `JoinAccount(accountId)` → `JobUpdated { id, status, stage, previewUrl, resultUrl, error }` |

**קונפיגורציה (`appsettings.User.json`, לא לקומיט):**

```json
"AiStudio": {
  "Gemini":    { "ApiKey": "", "ProModel": "gemini-3-pro-image", "FastModel": "gemini-nano-banana-2.1" },
  "OpenAI":    { "ApiKey": "", "Model": "gpt-image-2.5-sunburst" },
  "Anthropic": { "ApiKey": "", "Model": "claude-opus-5-5" },
  "DefaultProvider": "gemini",
  "PreviewSize": "1K", "FinalSize": "2K",
  "MaxConcurrentJobs": 3, "JobTimeoutSeconds": 120,
  "CreditPriceAgorot": 10
}
```

**חבילות NuGet להוסיף:** `Anthropic` (SDK רשמי) בלבד. עיבוד תמונה עם `Magick.NET` שכבר קיים; ל-Gemini/OpenAI מספיק `HttpClient` (יש `AddHttpClient` בתבנית של Cardcom/PayPlus).

---

## 10. Frontend (shop-manager)

| פריט | פירוט |
|---|---|
| `src/api/aiStudioApi.ts` | עטיפת ה-endpoints; `subscribeJob(id)` עם SignalR + polling fallback כל 2 שניות |
| `src/pages/QuickProductPage.tsx` | הזרימה הניידת (6.1), מצב full-screen, RTL, מקלדת מספרית למחיר |
| `src/components/aistudio/LookPicker.tsx` | 4 אריחים + שיפור; shimmer; מתג AI/מקורית; עלות בקרדיטים על כל אריח |
| `src/components/aistudio/WeightedToggle.tsx` | שדה משקל + מתג "מוצר שקיל" + 4 אפשרויות (6.2) — משתמש ב-`weightOptionsCodec` |
| `src/components/aistudio/AiEnhanceMenu.tsx` | תפריט "שפר ב-AI" ל-`ImageGallery` ולספריית המדיה |
| `src/pages/settings/AiStudioSettings.tsx` | הגדרות חשבון + רכישת חבילה (העתקת מסך חבילות ה-SMS) |
| `src/pages/AiStudioAdminPage.tsx` | super-admin: פריסטים, מחירון, יתרות, יומן עבודות |
| `useAiStudioEnabled.ts` | כמו `useMarketingEnabled` |
| ניווט | "מוצר מהיר" בסרגל + קיצור בכרטיס הבית; בטלפון זה הכניסה הראשית |

---

## 11. שלבי מימוש

| שלב | תוכן | הערכה |
|---|---|---|
| **0. Spike איכות** | **בוצע 29.10.2026**, ראו סעיף 14. 10 מוצרים (5 תמונות סטוק מה-DB + 5 צילומי טלפון חובבניים) × 5 מראים × 2 מנועים = 100 תמונות עם שופט Claude. | בוצע |
| **1. מנוע + שיפור תמונה קיימת** | **בוצע 29.10.2026 (לא נפרס).** `AiImageJob` + `Account.AiStudioEnabled/Mode/DailyCap` + `Media.Origin/AiJobId/LicensePool` (`Core/George.DB/Scripts/AiStudio_Phase1_Install.sql`, הורץ על George.QA, חשבון 18 הופעל במצב קהילתי); `George.Services/AiStudio/*` (OpenAI + Gemini providers, Claude identify/brief/judge, PromptBuilder, Magick post-process, AiStudioService, AiStudioJobRunner, hosted service); `AiStudioController` (`/AiStudio/Status`, `/Jobs`, `/Jobs/{id}`, `Retry`, `Cancel`, `Rate`, `Jobs/List`); `AiStudioHub` (`/hubs/aistudio`, אירוע `AiJobUpdated`); frontend: `aiStudioApi.ts`, `aiStudioRealtime.ts`, `useAiStudio.ts`, `AiJobsStrip.tsx`, תת-תפריט "שפר ב-AI" + "צור תמונה מתיאור" ב-`ImageGallery`, כרטיס סופר-אדמין ב-Accounts. מבחן קצה-לקצה מול QA: preview 15 ש', final 55 ש'. ללא קרדיטים (gate + מכסה יומית בלבד). **לפני deploy לפרוד:** להריץ את ה-SQL, להעתיק את מקטע `AiStudio` (מפתחות + `ReferenceUrls`) ל-appsettings.User.json של השרת, ולהפעיל `AiStudioEnabled` לחשבונות הפיילוט. | בוצע |
| **1א. תיקוני סבב QA ראשון** | **בוצע 29.10.2026 (לא נפרס).** אחרי שאפיף העלה ל-QA: (א) `generate` קיבל רק שם מוצר → נוסף דיאלוג `AiStudioDialog` (תיאור + בחירת רקע), `AiJobCreateReq.Background`, תבניות generate לכל רקע (`AiStudioLooks.GenerateTemplateFor`), רפרנסים לפי רקע, `BriefAsync` מחזיר `ambiguous/question` והעבודה נכשלת עם השאלה לפני חיוב; עורך המארז שולח את רשימת הרכיבים כתיאור, טופס המוצר שולח את שם הקטגוריה; HTML של Quill מנוקה בשרת. (ב) בחירת הרקע לא הייתה גלויה → כפתור ✨ על כל תמונה + אותו דיאלוג. (ג) `AiStudio:PollMilliseconds` לעבודה מקומית מול DB של QA (שני workers על אותה טבלה; ה-claim אטומי, המהיר זוכה). בדיקה מקומית: שם לא ברור נעצר תוך 7 ש' עם שאלה; מארז עם תיאור על רקע כהה: preview 15 ש', final 44 ש'. | בוצע |
| **2. זרימה ניידת** | **בוצע 30.10.2026 (לא נפרס, לא בוצע commit).** `src/pages/QuickProductPage.tsx` (נתיב `/quickproduct`, רשום ב-`pages.config.ts`; במסך צר מסתיר את ה-Layout דרך אותה מחלקת root שמשמשת את הקנבן), `components/aistudio/LookPicker.tsx` (אריחים עם shimmer, תמונה גדולה של הנבחר, "מקורית" כאריח, HD כשה-final הגיע, "נסה שוב" על אריח שנכשל), `components/aistudio/WeightedToggle.tsx` (שדה משקל ↔ מתג "מוצר שקיל" → עד 4 אפשרויות משקל ב-`weightOptionsCodec` עם מחיר מחושב מ-₪/ק"ג, פעיל/לא פעיל). עמוד אחד נגלל, לא אשף: ברקוד (הקלדה או `BarcodeDetector` במצלמה; מק"ט קיים → "פתח לעריכה") → צילום (`capture=environment`, הקטנה ל-2048 בצד הלקוח, העלאה + Media לחשבון, המראה האחרון שנבחר מתחיל מיד) → מראה → פרטים ("✨ כתוב ב-AI" = `POST /AiStudio/Suggest`: `ProductVisionService.DescribeAsync` מחזיר שם, תיאור קצר/מפורט, קטגוריה מתוך רשימת הקטגוריות של החנות, מותג, שקיל + 4 משקלים, משקל נטו לארוז; נספר במכסה היומית) → מחיר ומשקל → "שמור ופרסם" / "טיוטה" דרך `createProduct` כמו הטופס המלא (`setup_type=by_unit`, `unit_weight_mode=variable`, `weight_options`; תמונה = המראה שנבחר, המתנה עד 90 ש' ל-final עם "שמור עם התצוגה המקדימה"). מסך סיום: "צור מוצר דומה" (שומר קטגוריה/מותג/שקיל), "מוצר חדש", "ערוך בטופס המלא". ללא צילום: "צרו תמונה מתיאור" פותח את `AiStudioDialog`. כניסות: פריט "מוצר מהיר" בקטלוג (רק כשהמודול פעיל) + כפתור ✨ בסרגל הטלפון של "המוצרים שלי" (בדסקטופ שורת הכפתורים כבר מלאה ונשברה לשתי שורות, הוסר 30.10). בדיקה מקומית headless מול QA (חשבון 18, `spike/in/p01.jpg`): אריחים אחרי 0.5 ש', Suggest 6.7 ש' ("שוקי עוף טריים", קטגוריה "עוף הודו ואווז", שקיל 0.5/1/1.5/2), preview 14 ש', final 34 ש' (fid 0.88), מוצר 13233 נשמר כטיוטה עם by_unit/variable ונמחק. לא בוצע: חיפוש ברשת לפי ברקוד (Claude מקבל את הברקוד כרמז בלבד), ברירת מחדל של מראה לאתר (`Site.AiDefaultLook`, שלב 4; בינתיים localStorage), סריקה ב-iOS Safari (אין `BarcodeDetector`, נשארת הקלדה). **עיצוב מחדש 30.10 אחרי משוב QA:** המסך נבנה מחדש בשפת העיצוב של מסכי השיווק (רקע אפור, כרטיסים לבנים, שלבים ממוספרים 1-4, ReportHeader עם חזרה ויתרה, 2 עמודות בדסקטופ, סרגל שמירה דביק בתחתית). באג הגלילה: המסך הוסיף את `orders-kanban-fullscreen` ל-html, שנועל את הגלילה של `#layout-main-scroll` (נועד לקנבן בגובה קבוע) - הוסר; אין להשתמש במחלקה הזו למסך גלילה. **סבב RTL ומסך סיום 30.10 (ערב):** הקשה על התמונה הגדולה פותחת תצוגה מוגדלת (`LookPicker`, דיאלוג על כל המסך בטלפון); מסך הסיום נבנה מחדש כפי שהמערכת מציגה כרטיסים (שורת כותרת עם ✓ ותג פורסם/טיוטה, שורת מוצר עם תמונה ממוזערת, מחיר ומשקלים, "מה הלאה?" עם שני כפתורים וקישורים); תיקוני RTL: חיצי הכותרת הפכו ל-←, שדות מספריים (ברקוד, מחיר, משקל) מוקלדים LTR עם יישור לימין וה-₪ צמוד למספר, הכפתור הראשי "שמור ופרסם" עבר לצד ימין (כמו בדיאלוגים של המערכת), מחיר במסך הסיום בפורמט המטבע של המערכת. נבדק headless ב-430 וב-1400 פיקסלים. | בוצע |
| **3. קרדיטים ומאגר קהילתי** | **בוצע 30.10.2026 (לא נפרס, לא בוצע commit).** SQL: `Core/George.DB/Scripts/AiStudio_Phase3_Install.sql` (הורץ על George.QA; קובץ עם BOM כי יש בו עברית ב-seed): `AiCreditLedger` (allocation/purchase/consumption/refund, יתרה = SUM), `AiCreditPackage` (seed 100/₪50, 300/₪135, 1000/₪400, 3000/₪1050 לפני מע"מ, קרדיט ≈ ₪0.50 = עלות ספק ×2; מחליף את ₪0.10 של §7), `AiCreditPurchase` (העתק MarketingPurchase), `Account.AiStudioPoolConsentAt`, `AiImageJob.LedgerEntryId`. Backend: `AiCreditStorage`, `AiCreditService` (יתרה והיסטוריה, קטלוג, רכישה בקארדקום על מסוף הפלטפורמה עם webhook `/Webhooks/Cardcom/AiStudio` ו-return `/aistudio/purchase/{id}/return`, חשבונית, טעינה ידנית, סקירת אדמין, יומן עבודות); `AiStudioService`: במצב פרטי חיוב לפני יצירת העבודה (ledger קודם, אחר כך Job עם `LedgerEntryId`, כדי שה-worker לא יתפוס עבודה שהחיוב שלה באוויר), "אין מספיק קרדיטים" כשחסר, ניסיון חוזר ראשון חינם, ביטול לפני preview מזכה, Suggest = 1 קרדיט; במצב קהילתי Credits=0 וחובה `PoolConsentAt` (`POST /AiStudio/PoolConsent`); runner: עבודה שנכשלה מזכה (`RefundJobAsync` אטומי על הדגל Refunded) ומוצר ארוז/ממותג לא נכנס למאגר (`LicensePool` רק כש-`IsPackaged != true`). `AccountReq.AiStudioDailyCap` (0 = ברירת מחדל). Frontend: `AiPoolConsentDialog` (תנאים + checkbox; נפתח לפני העבודה הראשונה בגלריה ובמוצר מהיר), `BuyCreditsDialog`, `AiStudioPurchaseReturnPage`, `settings/AiStudioSettingsSection` (הגדרות › סטודיו AI: מצב, יתרה, רכישה, היסטוריה, מחירון), `AiStudioAdminPage` (/aistudioadmin, סופר-אדמין: KPI, טבלת חשבונות עם ציון שופט ועלות ספק, `AiCreditPackagesAdmin`, יומן 60 עבודות), כרטיס האדמין ב-Accounts: מכסה יומית + `AiStudioAccountAdminCard` (יתרה וטעינה ידנית / סטטוס תנאים). בדיקה מקומית מול QA (חשבון 18): עבודה לפני אישור תנאים נדחית; אישור דרך ה-UI נחתם; במצב פרטי 0 קרדיטים נדחה, טעינת 9, enhance עם מקור 404 → נכשל וזוכה (7→9), generate 5 קרדיטים → 4, 4<5 נדחה, הצליח ב-65 ש' (fid 0.88) בלי חיוב נוסף, retry ראשון 0 קרדיטים; קטלוג 4 חבילות עם מע"מ; LowProfile/Create הגיע לקארדקום ונדחה רק בגלל webhook localhost (Invalid IndicatorUrl, צפוי מקומית). **לא נבדק:** תשלום אמיתי בדף קארדקום (אין סיסמת sandbox עובדת, ראו זיכרון) והחזרה ל-return page. **לפני deploy:** להריץ את ה-SQL, `Billing:Cardcom` כבר קיים לשיווק; חשבון 18 הוחזר למצב קהילתי בלי אישור תנאים כדי שהדיאלוג ייבדק ב-QA. **ארנק ספקים 30.10:** אף ספק (OpenAI, Google, Anthropic) לא חושף יתרה ב-API (OpenAI: רק costs ב-Admin API; Anthropic: cost_report; Gemini: רק דף החיוב ב-AI Studio). לכן נוספו `AiProviderSpend` (יומן עלות לכל קריאה בתשלום: preview/final של מנוע התמונה, identify/brief/judge/describe של Claude לפי מחירון Opus 5.5 $4/$20 למיליון, ניתן לשינוי ב-`AiStudio:AnthropicInputUsdPerMTok`) ו-`AiProviderTopUp` (המנהל מזין את היתרה שראה בקונסול; יתרה משוערת = היתרה שהוזנה פחות ההוצאה מאז; רמות ok/low/empty, התראה מתחת ל-$20 או פחות מ-14 יום לפי קצב 30 יום). `ProviderCostUsd` של עבודה כולל מעתה גם את Claude. API: `GET /AiStudio/Admin/Overview` מחזיר `providers[]`, `POST /AiStudio/Admin/Providers/TopUp`, `GET /AiStudio/Admin/Jobs` עם accountId/status/fromUtc/toUtc/skip/take ומחזיר `{items,total}`. מסך /aistudioadmin: כרטיס "ארנק ספקים" (3 ספקים, יתרה משוערת, הוצאה בתקופה, ממוצע ליום, ימים שנותרו, קישור לקונסול, דיאלוג עדכון יתרה), טבלת עבודות עם סינון חשבון/סטטוס/תקופה, רענון, דפדוף 20 בעמוד ומונה. היומן מתחיל מהפריסה - עבודות ישנות לא נספרות בארנק (כן בעלות ספקים של העבודות). | בוצע |
| **4. ליטוש** | **בוצע 30.10.2026 (לא נפרס, לא בוצע commit).** SQL: `Core/George.DB/Scripts/AiStudio_Phase4_Install.sql` (הורץ על George.QA): `Site.AiDefaultLook/AiImageRatio/AiFinalSize` + טבלת `AiStylePreset` (שורה גלובלית למראה: תווית, תיאור, פרומפט, מנוע, מחיר, פעיל, סדר; `AccountId` לדריסה פר-חשבון - נתמך בשרת, אין לו UI עדיין). **4K:** `AiJobCreateReq.Hd` (null = ברירת המחדל של הסניף) → `JobOptions.Hd`; `AiStudio:HdPx` (2880, תקרת הפיקסלים של OpenAI לריבוע; 16:9 מגיע ל-3840×2160) ו-`AiStudio:HdCredits` (3); Gemini מקבל `image_size 4K`; `AiJobRes.Hd`. נמדד: final 4K של OpenAI 2880×2880, 1.2MB, ~$0.19 (5,930 טוקנים) ב-55 ש'. **ברירות מחדל לסניף** (הגדרות › סטודיו AI › ברירות מחדל): מראה ברירת מחדל (מתחיל לבד במוצר מהיר ונבחר מראש בדיאלוג), יחס תמונה, 2K/4K; `GET /AiStudio/Status` מחזיר `defaults` + `hdCredits`, ה-UI שולח את `selectedSiteFilter` כ-siteId. **פריסטים (סופר-אדמין):** כרטיס "מראים ופרומפטים" ב-/aistudioadmin - לכל מראה: עבודות, הצליחו/נכשלו, נאמנות/אסתטיקה ממוצעות, 👍/👎, ניסיונות חוזרים, עלות, ו-10 התלונות השכיחות של השופט בתקופה (`JudgeIssues` מקובצות); עריכה של שם/תיאור/פרומפט (אנגלית, עם {category} {ratio} {scene})/מנוע/מחיר/פעיל, "שחזר ברירת מחדל"; `GET/PUT /AiStudio/Admin/Looks`. `AiStudioLooks.All` = ברירות המחדל בקוד + דריסת `AiStylePreset` (`Apply`), מרוענן ב-/Status הראשון, אחרי כל שמירה, וכל 5 דקות ב-worker; מראה לא פעיל לא מוצע לחנויות אבל עדיין מתרגם תוויות של עבודות ישנות. **batch בספריית המדיה:** בחירה מרובה → "שפר N ב-AI" (עד 20) → אותו דיאלוג עם תמונות ממוזערות, מראה ו-4K → עבודה לכל תמונה (`sourceMediaId`, השם בלי סיומות מראה קודמות) → כרטיס התקדמות צף (`AiBatchProgress`) → רענון הספרייה כשהכול הסתיים; התוצאות נשמרות כ-Media חדשות לצד המקור. אין הנחת batch (OpenAI אין Batch לעריכת תמונות). **i18n:** he/en בפריטי (303 מפתחות ב-aiStudio, תואמים). בדיקה מקומית headless מול QA: עריכת "רקע לבן" ל-5 קרדיטים נראתה מיד ב-/Status ושוחזרה; ברירות מחדל כהה+4K נשמרו לסניף 20 ו-/Status החזיר `look=dark, lookExplicit=true, finalSize=4k`; batch של 2 תמונות ב-4K: שתיהן done (נאמנות 0.93/0.88), 55 ש' כל אחת. נתוני הבדיקה נוקו (Site 20 חזר ל-NULL, Media מקומיות סומנו מחוקות, 0 פריסטים). **לא נעשה:** `AiTextTone`/שפה לטקסטים, תמונות רפרנס למראה בהעלאה מהחשבון (נשאר `AiStudio:ReferenceUrls` בקונפיג), UI לפריסט פר-חשבון, batch בקטלוג הגלובלי/ייבוא CSV ("צור תמונות למוצרים ללא תמונה"). | בוצע |
| **5. השלמות** | **בוצע 31.10.2026 (בענף `feature/ai-product-studio`, לא נפרס לפרוד).** SQL: `Core/George.DB/Scripts/AiStudio_Phase5_Install.sql` (הורץ על George.QA): `Site.AiTextTone/AiTextLanguage`, `AiStylePreset.ReferenceUrls/StyleNotes`. **(10) טון ושפה:** הגדרות › סטודיו AI › ברירות מחדל - טון (חם / ענייני / פרימיום / קליל) ושפה (עברית / אנגלית / ערבית / רוסית) לטקסטים של "כתוב ב-AI"; `DescribeAsync` מקבל אותם מברירות המחדל של הסניף. **(11) תמונות רפרנס:** במקום URL-ים בקונפיג - עד 3 תמונות מספריית המדיה לכל מראה, בשלוש שכבות: שורת החשבון → השורה הגלובלית (סופר-אדמין, מהגלריה הראשית) → `AiStudio:ReferenceUrls`; נבחרות ב-MediaPicker (כולל העלאה), בלי deploy. **(12) פריסט פר-חשבון:** הגדרות › סטודיו AI › "המראים שלי" - לכל מראה: כיבוי לחנות, הערות סגנון (עד 500 תווים, בכל שפה, מתווספות לפרומפט אחרי המראה ולפני החוקים הקבועים) ותמונות רפרנס; פרומפט/מנוע/מחיר נשארים בפלטפורמה. `GET/PUT /AiStudio/Presets`; מראה שכובה בחשבון לא מוצע ב-/Status ונדחה ביצירה. **(13) מוצרים ללא תמונה:** באנר ב"המוצרים שלי" ("ל-N מוצרים אין תמונה") → דיאלוג רקע + 4K + עלות → `POST /AiStudio/Jobs/ForProducts` (עד 50; generate לכל מוצר מהשם, הקטגוריה הראשית והתיאור הקצר; בדיקת יתרה לכל האצווה מראש) → כשה-final מגיע הראנר מצרף אותו כתמונת המוצר (רק אם עדיין אין) ומסנכרן ל-Woo של הסניפים (`SyncProductToWooCommerceForAssignedSitesAsync`); כרטיס התקדמות צף והרשימה נטענת מחדש. מוצרים שכבר קיבלו תמונה מדולגים. (הקטלוג הגלובלי/ייבוא CSV: המוצרים המיובאים מופיעים ב"המוצרים שלי" ללא תמונה, אז אותו באנר מכסה אותם.) **(14) חיפוש ברשת לפי ברקוד:** Suggest עם ברקוד קמעונאי (8+ ספרות) מפעיל את `web_search_20260209` של Claude (עד 3 חיפושים, עדיפות לקמעונאים ישראליים, 729 = ישראל); מחיר `AiStudio:WebSearchCredits` (2) רק כשנמצא משהו, אחרת 1; `AiSuggestRes.WebSearched/FoundOnWeb/WebSource` והטוסט במוצר מהיר מציין את המקור; עלות החיפושים ($0.01) נרשמת ביומן הספקים. **(15) סריקה ב-iOS Safari:** `BarcodeScanDialog` נופל ל-ZXing (`@zxing/browser`, נטען דינמית) כשאין `BarcodeDetector`; נבדק headless עם מצלמה מזויפת שמציגה EAN-13 סינתטי - זוהה תוך שנייה. **כלל גורג׳ (31.10):** עבודה על תמונה שכבר קיימת בחשבון (✨ בגלריה, batch בספרייה) מחויבת בקרדיטים גם במצב קהילתי (`AiStudio:ExistingImageAlwaysPaid`); רק צילום טרי במוצר מהיר (`AiJobCreateReq.Capture` שמאומת מול שורת ה-Media: אותו משתמש, אותו חשבון, Origin=upload, עד 30 דקות) ו-generate הולכים לפי מצב החשבון. תוצאה ששולמה היא פרטית (לא נתרמת). `/Status` מחזיר `balance` גם בקהילתי + `existingImagePaid`; בהגדרות במצב קהילתי יש כרטיס יתרה + רכישה; הדיאלוגים מציגים מחיר ויתרה. בדיקה מקומית מול QA: סטטוס/פריסטים/רפרנסים/דחיית תמונה קיימת ללא קרדיטים/טון+שפה (Suggest באנגלית-פרימיום עם חיפוש ברשת, 16 ש׳)/אצוות מוצר ללא תמונה (מוצר בדיקה 13239 קיבל תמונה תוך 60 ש׳, fid 0.85, ריצה שנייה דילגה; נמחק); UI ב-1400 (הגדרות, המראים שלי, באנר ודיאלוג במוצרים שלי) וב-430 (סורק ZXing). | בוצע |

---

## 12. החלטות שצריך לקבל לפני התחלה

1. **מפתחות API:** Gemini (Google AI Studio, חיוב) + Anthropic. OpenAI אופציונלי לשלב 1. יש כבר Azure OpenAI בקונפיג — לא רלוונטי לתמונות.
2. **מחירון קרדיטים ומחירי חבילות** (סעיף 7 הוא הצעה).
3. **המאגר הקהילתי:** ניסוח תנאי התרומה; האם לסנן מוצרים ממותגים; האם לאפשר לחשבון להסיר תרומה בדיעבד.
4. **אפשרויות המשקל:** 4 אפשרויות ליחידה (`WeightOptions`) או וריאנטים עם מחיר — ראו 6.2.
5. **רזולוציה סופית:** 2K כברירת מחדל (מומלץ) או 4K לכולם (פי 2 עלות).
6. **מראה ברירת מחדל לפלטפורמה:** לבן (בטוח לכל חנות) או לפי סוג עסק (בשר → כהה).
7. **לספר לגורג'** ש-`gpt-image-1` הוצא משירות ושה-DirectProvider של oc-snap צריך מעבר ל-`gpt-image-2.5-sunburst` (או ל-Gemini).

---

## 13. סיכונים

| סיכון | טיפול |
|---|---|
| סטייה במוצר (טקסט על אריזה משתנה) | שופט Claude + ריצה חוזרת; ניתוב ל-gpt-image-2.5 לאריזות; תמיד אפשרות "מקורית" |
| עלות בורחת | תקרה יומית לחשבון, preview במודל המהיר, final רק אחרי אישור המשתמש (אופציה) |
| rate limits של הספק | תור עם מקביליות מוגבלת, backoff, הודעה ברורה "עומס, נסה בעוד דקה" |
| זמן תגובה ארוך בשעות עומס | preview 1K תמיד קודם; UI ממשיך לטופס; התראה כשמוכן |
| סימן מים SynthID / זכויות | ציון בתנאי השימוש; תמונות קהילתיות מסומנות Origin=ai |
| שינוי מודלים תכוף (gpt-image-1 נסגר אחרי שנה) | ממשק ספק אחד; שמות מודלים בקונפיג, לא בקוד |

---

## 14. תוצאות ה-spike (29.10.2026)

דף ההשוואה (פרטי): https://claude.ai/artifact/Gip6YYw1JLP2LsW8cCDGWJ. סקריפטים: `spike/run.mjs`, `spike/build.mjs` (scratchpad של Claude Code; להעתיק ל-`Tools/AiStudioSpike` כשמתחילים שלב 1). מפתחות ב-`appsettings.User.json` → `AiStudio`.

**מה נבדק:** 5 תמונות מוצר מה-DB (שוקי עוף, רוטב JD, קופסת Blue Elephant, לוקוס, בשר טחון) + 5 צילומי טלפון חובבניים מ-Wikimedia Commons (מגש שוקיים מטושטש, חזה עוף על נייר כסף על כיריים, בשר טחון בצלחת, מגש בשר טחון ארוז עם תווית, פילה דג קפוא בוואקום על נירוסטה). 5 מראים (לבן, כהה, שיש, אווירה, שיפור) × 2 מנועים. Gemini ב-2K, OpenAI ב-`high` 1536px. שופט: Claude Opus 5.5.

| | gpt-image-2.5-sunburst | gemini-3-pro-image |
|---|---|---|
| זמן ממוצע ליצירה | 45 שניות (high) · 15 שניות (low) · 20 שניות (medium 1536) | 26 שניות (2K) |
| תמונות סטוק: נאמנות / אסתטיקה / ship | 0.88 / 0.88 / 17 מ-20 | 0.83 / 0.85 / 10 מ-20 |
| צילומי טלפון, בשר: נאמנות / ship | 0.79 / 8 מ-15 | 0.75 / 8 מ-15 |
| צילומי טלפון, ארוז: נאמנות / ship | 0.86 / 7 מ-10 | 0.73 / 0 מ-10 |
| טקסט על אריזה (עברית, תאית, לועזית) | מושלם | לפעמים משתבש, לפעמים נשארים שאריות סטודיו |
| ציות ל"הסר את נייר הכסף" | כן | חלקי, וצייר מחדש חזה עוף אחר |

**מסקנות שנכנסו לאפיון:** OpenAI כמנוע ראשי; preview ב-`low`; הנחיית `incidental`; "אווירה" לדגים צריכה רקע שקט יותר (שוק דגים יצא עמוס); צילום מטושטש בלי מוצר שלם (r01) מייצר המצאה בשני המנועים, ולכן ה-UI צריך להנחות "צלם את כל המוצר, מרחק 30–50 ס"מ" ו-Claude צריך לדחות צילום כזה לפני שמבזבזים קרדיט.

**עלות ה-spike:** כ-100 תמונות, ~$12 OpenAI + ~$3 Gemini + ~$2 Claude.

## מקורות (מחקר מודלים, 29.10.2026)

- Gemini image generation docs: https://ai.google.dev/gemini-api/docs/image-generation
- Gemini pricing: https://ai.google.dev/gemini-api/docs/pricing
- OpenAI image generation guide (gpt-image-2.5): https://developers.openai.com/api/docs/guides/image-generation
- OpenAI gpt-image-1 model page (shutdown 23.10.2026): https://developers.openai.com/api/docs/models/gpt-image-1
- OpenAI image model lineup: https://www.aifreeapi.com/en/posts/openai-image-generation-api-models
- Comparisons: https://aimlapi.com/blog/10-best-ai-photo-editing-models , https://www.hiapi.ai/en/blog/best-image-to-image-apis-2026 , https://www.teamday.ai/blog/best-ai-image-models-2026 , https://pixverse.ai/en/blog/gpt-image-2-vs-nano-banana-2
- FLUX pricing: https://invideo.io/blog/flux-ai-image-generator/ , https://developer.puter.com/tutorials/flux-api-pricing/
- Nano Banana Pro pricing: https://usagebox.com/articles/nano-banana-pro-api-pricing-cost-per-image-2026
