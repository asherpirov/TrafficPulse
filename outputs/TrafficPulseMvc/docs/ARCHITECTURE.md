# תרשימי המערכת

## זרימת נתונים

```mermaid
flowchart TD
    Provider[TomTom API] --> Producer[C# Producer]
    Producer --> Kafka[Kafka traffic-readings]
    Kafka --> Consumer[C# Consumer]
    Consumer --> Validation[בדיקת מדידה]
    Validation --> Repository[TrafficRepository]
    Repository --> Analysis[AnomalyService]
    Repository --> DB[(MySQL via Pomelo DbContext)]
    Validation --> DLQ[Kafka dead-letter topic]
    DB --> Web[Controller]
    Web --> View[Razor View]
    Web --> Api[REST JSON API]
```

## קשרים בין חמש הטבלאות

```mermaid
erDiagram
    Users ||--o{ Favorites : saves
    Roads ||--o{ Favorites : tracked
    Roads ||--o{ Readings : measured
    Roads ||--o{ Alerts : alerts
    Readings ||--o| Alerts : triggers
    Users o|--o{ Alerts : updates
```

## אישור הודעה

```mermaid
sequenceDiagram
    participant K as Kafka
    participant C as Consumer
    participant R as Repository
    participant D as MySQL
    K->>C: Reading JSON
    C->>C: Validate
    C->>R: SaveReadingAsync
    R->>D: Begin transaction
    R->>D: Check duplicate and load history
    R->>R: Analyze
    R->>D: Save Reading + optional Alert
    R->>D: Commit transaction
    R-->>C: Saved / duplicate
    C->>K: Commit offset
```

## החלטות תכנון

- MVC מוכר לצוות, במקום מסגרת Frontend נפרדת.
- MySQL הוא מקור האמת. אין Redis או MongoDB שאינם נדרשים לתכולה שנבחרה.
- Producer ו־Consumer הם תהליכים נפרדים; Shared מכיל רק קוד משותף.
- Repository Scoped עם DbContext Scoped: אין שיתוף DbContext בין threads או בקשות.
- עדכוני מקטעים והתרעות משתמשים ב־Version כדי לא לדרוס שינוי מקביל.
- אין מחיקה של מקטעים בעלי היסטוריה; משביתים אותם.
- הנתונים מאוחסנים ב־UTC ומוצגים לפי ישראל. אותו יום בשבוע נקבע באזור הזמן של הניתוח.
- בעת כשל מקור הנתונים נשארת המדידה האחרונה עם סימון חוסר עדכניות.
- השליטה בניתוח נמצאת בהגדרות Analysis ובסף המקטע, לא במספרים הפזורים בקונטרולרים.

## גבולות הגרסה

- רשימת משתמשים מוגבלת ל־1,000, התרעות ל־200, והיסטוריה למסך/API ל־500 מדידות; אין כרגע דפדוף עמודים.
- כל מדידה חריגה יכולה ליצור התרעה; אין עדיין איחוד סדרת מדידות לאירוע רציף אחד.
- אין שחזור סיסמה בדואר או אימות כתובת דואר.
- אין מפה גאוגרפית; יש רשימה, פרטים וגרף מהירות.
- ההדגמה בקובץ מיועדת לתהליך יחיד ולהיקף קטן, ואין לה מדיניות מחיקה אוטומטית.
- ב־MySQL אין עדיין ארכוב היסטוריה מתוזמן. בפריסה ממושכת יש להגדיר מדיניות שמירה וגיבוי.
- ה־Producer מנסה לפרסם עד שלוש פעמים; כשל ממושך לפני אישור Kafka עלול לאבד את המדידה שנאספה. הוא נרשם בלוג. לאחר אישור Kafka, השחזור מוגבל לתקופת השמירה של broker.
- Broker יחיד במחשב פיתוח אינו תשתית High Availability.
