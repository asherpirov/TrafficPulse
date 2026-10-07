# TrafficPulse — פרויקט MVC ב־C#

הפרויקט הפעיל והמעודכן הוא **TrafficPulseMvc**. הוא מחליף את טיוטת TrafficPulse הקודמת.

מערכת לניטור מקטעי כביש, השוואה להיסטוריה, זיהוי חריגות וניהול התרעות.
מבוססת על סגנון FieldIntelligencePlatform: Controllers, Models, Repositories, Services.
הממשק הוא **ASP.NET Core MVC + Razor Views + CSS**, ללא קוד JavaScript משלכם וללא Blazor.
הגישה למסד היא **Entity Framework Core + Pomelo + TrafficDbContext + LINQ**.

## להתחיל כאן

1. פתחו את התיקייה הזו ב־VS Code: File → Open Folder.
2. פתחו Terminal בתיקייה והפעילו:

```powershell
dotnet run --project TrafficWeb --launch-profile TrafficWeb
```

3. פתחו http://localhost:5187.
4. במצב ההדגמה בלבד:

| תפקיד | דואר | סיסמה |
|---|---|---|
| מנהל | admin@traffic.demo | TrafficDemo2026! |
| משתמש | user@traffic.demo | TrafficDemo2026! |

ההדגמה משתמשת בקובץ מקומי `.data/demo.json`, ואינה מוכיחה חיבור MySQL/Kafka/TomTom.
היא מסומנת בצהוב בראש כל מסך. היא מותרת רק בסביבת Development.
בהרצה הראשונה נוצרות 3 נקודות פיקטיביות והיסטוריה סינתטית. אין להסיק מהן נתוני תנועה אמיתיים.
הנתונים נשמרים בין הפעלות ומתעדכנים פעם בדקה. לרענון התצוגה לחצו על רענון או טענו את העמוד מחדש.

## מה יש במערכת

- התחברות, הרשמה והתנתקות; משתמש חדש תמיד מקבל User.
- תמונת מצב, מועדפים אישיים והיסטוריה עם סינון תאריכים וגרף CSS פשוט.
- רשימת התרעות עם הסבר, ממוצע, סטיית תקן וכמות המדידות ששימשו להשוואה.
- מנהל: יצירה/עריכה/השבתה של מקטעים, סף רגישות לכל מקטע, עדכון סטטוס התרעות והשבתת משתמשים.
- API לקריאה: נתוני מקטעים, מדידות והתרעות.
- Producer ב־C# עם ספק TomTom אמיתי או Demo מסומן.
- Consumer ב־C# עם אישור Kafka אחרי שמירה, מניעת כפילויות ו־dead-letter topic לנתונים פגומים.
- חמש טבלאות עסקיות וקשרים ביניהן. EF מוסיף גם טבלת ניהול Migrations.

## מבנה התיקיות

```text
TrafficPulse.sln
TrafficWeb/
  Controllers/          קבלת בקשות והחזרת View או JSON
  ViewModels/           הנתונים שטופס/מסך מקבלים
  Views/                מסכי cshtml
    Account/            כניסה, הרשמה, אין הרשאה
    Traffic/            תמונת מצב והיסטוריה
    Alerts/             חריגות ועדכון טיפול
    Admin/              משתמשים ומקטעים
    Shared/             תפריט ועיצוב משותף
  Services/             אתחול, תצוגת זמן והדגמה
  Middlewares/          טיפול מרוכז בשגיאות
  wwwroot/css/          עיצוב האתר
  Program.cs            הגדרות ושירותים
TrafficShared/
  Models/               מחלקות המידע וההגדרות
  Data/                 TrafficDbContext והגדרת קשרים
  Migrations/           גרסאות סכימת מסד הנתונים
  Repositories/         LINQ ו-SaveChangesAsync; מתאם הדגמה נפרד
  Services/             אלגוריתם, ולידציה וחיבור TomTom
TrafficProducer/        קבלת נתונים ושליחתם ל-Kafka
TrafficConsumer/        עיבוד הודעות ושמירה
TrafficTests/           בדיקות אוטומטיות שאפשר לקרוא ולהריץ
docs/                   הסבר בעברית, תרשימים ותכנון בדיקות
scripts/                בדיקות HTTP
```

TrafficShared היא ספרייה משותפת כדי ששלושת הרכיבים ישתמשו באותן מחלקות. אין לה שרת משלה.

## חמש הטבלאות

| טבלה | מטרה | קשרים |
|---|---|---|
| Users | חשבונות, גיבוב סיסמה, תפקיד ומצב | משתמש יכול לשמור מועדפים ולעדכן התרעות |
| Roads | שם, מיקום, סף רגישות ומצב מקטע | מקטע כולל מדידות והתרעות |
| Readings | מהירות, אמינות הספק, זמן איסוף ומקור | שייכת למקטע |
| Alerts | חריגה, הסבר, נתוני השוואה ומצב טיפול | שייכת למדידה ולמקטע; מתעדת מנהל מעדכן |
| Favorites | קישור בין משתמש למקטע | מפתח משולב מונע כפילות |

אין סיסמת MySQL בתוך קוד C#. אין מפתח TomTom בקוד. חשבונות ההדגמה הם נתונים פיקטיביים מוצהרים.
MySqlConnector מופיע כתלות פנימית של Pomelo, ומשמש בקוד רק לזיהוי שגיאות ספק. אין MySqlCommand או SQL ידני ב־Repository.

## הפעלה עם MySQL ו־Kafka

דרישות: .NET 8 SDK, Docker Desktop פעיל במצב Linux containers, והרשאה לגשת למנוע Docker.

1. העתיקו `.env.example` אל `.env`.
2. מלאו סיסמאות נפרדות עבור MySQL ומשתמש מנהל ראשוני. סיסמת מנהל חייבת לכלול 12–128 תווים, אות ומספר. כדי לשמור על פורמט connection string פשוט בחרו סיסמאות מסד ללא נקודה־פסיק.
3. `TRAFFIC_PROVIDER=Demo` יפעיל **נתונים סינתטיים דרך Kafka ו־MySQL אמיתיים**. זה שונה ממצב ההדגמה המקומי של האתר.
4. הריצו:

```powershell
docker compose up --build -d
docker compose ps
docker compose logs --tail=50 web producer consumer
```

5. התחברו ב־http://localhost:5187 עם חשבון המנהל שהגדרתם ב־`.env`.
6. הוסיפו מקטע במסך ניהול מקטעים. אין מקטעים אוטומטיים במצב זה.
7. בתוך מחזור האיסוף הבא תופיע מדידה. התרעה סטטיסטית דורשת היסטוריה מספקת.

MySQL מפורסם רק ל־localhost:3307 ו־Kafka ל־localhost:9092. התשתית מיועדת למחשב פיתוח, עם broker יחיד ובלי TLS פנימי. זו אינה תצורת ענן ציבורית.
המנהל מוגדר רק כאשר מאגר המשתמשים ריק. שינוי משתני Bootstrap לאחר מכן אינו משנה את הסיסמה הקיימת.
עצירה ללא מחיקת נתונים: `docker compose stop`. אל תמחקו volumes אם ברצונכם לשמור את מסד הנתונים.

## חיבור ל־TomTom

קבעו ב־`.env`:

```text
TRAFFIC_PROVIDER=TomTom
TOMTOM_API_KEY=<your-own-key>
```

הפעילו מחדש את ה־Producer באמצעות `docker compose up -d producer`.
הקוד קורא ל־Flow Segment Data. צריך לבדוק בחשבון הספק את הכיסוי בישראל, מכסת הבקשות, עלויות והרשאות שמירת הנתונים.
הספק מחזיר מקטע הקרוב לנקודה שנבחרה; יש לאמת שהוא המקטע והכיוון הרצויים. אין כאן חישוב מסלול מלא.
`CollectedAtUtc` הוא זמן האיסוף אצלנו, ולא טענה שהמדידה אצל הספק בוצעה בדיוק אז.
אם הספק נכשל, אין מעבר שקט לנתונים פיקטיביים. הנתון האחרון יוצג כלא עדכני לאחר חמש דקות.

## הגדרות ו־DbContext

באתר ובתהליכי הרקע פרטי החיבור נקראים מ־`ConnectionStrings:MySql` (או `ConnectionStrings__MySql` כמשתנה סביבה).
`AddDbContext<TrafficDbContext>` רושם הקשר Scoped, ו־`UseMySql` בוחר ב־Pomelo.
`OnModelCreating` מגדיר קשרים, אינדקסים, מפתחות ייחודיים ושדות Version להגנה מעדכונים מתנגשים.
`MigrateAsync` מפעיל את ה־Migration הראשוני בעת הפעלת האתר. ה־Workers אינם משנים סכימה.

ליצירת שינוי סכימה חדש, התקינו/השתמשו ב־dotnet-ef 8.0.29, הגדירו את משתני הסביבה והפעילו:

```powershell
$env:ConnectionStrings__MySql = '<connection string>'
$env:Database__ServerVersion = '8.0.0' # התאימו לגרסת השרת שלכם
dotnet ef migrations add MeaningfulChange --project TrafficShared --startup-project TrafficShared
```

הסיסמה שלכם אינה נשמרת ב־Migration. אל תשתפו מסוף שמציג סודות.
פרמטרי הניתוח נמצאים ב־AnalysisSettings ובמקטע Analysis בהגדרות. בשימוש ב־Docker יש להגדיר אותם ב־Consumer, שבו מתבצע הניתוח.

## בדיקות

```powershell
dotnet build TrafficPulse.sln
dotnet run --project TrafficTests
python scripts/http_checks.py http://localhost:5187
```

בדיקות HTTP מיועדות למצב ההדגמה המקומי בלבד. הן יוצרות חשבון ומקטע זמניים בשם בדיקה ומשביתות אותם בסיום.
הבדיקות ב־TrafficTests הן תוכנית Console עם בדיקות שנכשלות באמצעות exit code, ללא תלות במסגרת בדיקות נוספת.
ראו `docs/VERIFICATION.md` לתוצאות בפועל ולבדיקות שלא ניתן היה לבצע בסביבה זו.

## סדר לימוד מומלץ

1. `Models/Road.cs` ו־`TrafficReading.cs`.
2. `TrafficDbContext.cs` — איך המחלקות הופכות לטבלאות.
3. `TrafficRepository.GetRoadsAsync` — שאילתת LINQ קצרה.
4. `TrafficController.Index` — בניית המודל למסך.
5. `Views/Traffic/Index.cshtml` — לולאה המציגה אותו.
6. התחברות והרשאות; לאחר מכן Producer ו־Consumer.
7. `AnomalyService` והבדיקות שלו.

קראו את `docs/LEARNING.md` יחד עם הקוד. מסמכי הפרויקט כאן הם תיעוד עבודה והסבר לקוד; הם אינם ספר הגמר בן 50 העמודים.
