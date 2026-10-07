using System.Net;
using Microsoft.EntityFrameworkCore;
using TrafficShared.Data;
using TrafficShared.Models;
using TrafficShared.Repositories;
using TrafficShared.Services;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}
var analysis = new AnomalyService(new AnalysisSettings());
Guid roadId = Guid.NewGuid();
DateTime now = DateTime.UtcNow;
TrafficReading Make(double speed, DateTime? at = null) => new()
{
    RoadId = roadId, CollectedAtUtc = at ?? now, CurrentSpeed = speed, FreeFlowSpeed = 80, Confidence = 0.95, Source = "Demo"
};
var history = Enumerable.Range(0, 12).Select(i => Make(60, now.AddDays(-7).AddMinutes(i - 6))).ToList();
Check(analysis.Analyze(Make(60), history, 3) == null, "Normal speed is not an anomaly");
Check(analysis.Analyze(Make(20), history, 3)?.SampleCount == 12, "Zero variance detects a meaningful drop");
Check(analysis.Analyze(Make(20), history.Take(5), 3) == null, "Insufficient history does not invent an anomaly");
var lowConfidence = Make(10); lowConfidence.Confidence = 0.2;
Check(analysis.Analyze(lowConfidence, history, 3) == null, "Low confidence is excluded");
var closed = Make(0); closed.RoadClosed = true;
Check(analysis.Analyze(closed, [], 3) != null, "Closure is detected without history");
var otherSource = Make(10); otherSource.Source = "TomTom";
Check(analysis.Analyze(otherSource, history, 3) == null, "Synthetic and live data never mix");
Check(analysis.Analyze(Make(10), history.Select(r => Make(60, now.AddDays(-1))), 3) == null, "Different weekdays are excluded");
Check(analysis.Analyze(Make(10), history.Select(r => Make(60, now.AddMinutes(-30))), 3) == null, "Same-day measurements excluded from baseline");
var invalid = Make(double.NaN);
try { TrafficValidator.Validate(invalid); Check(false, "NaN rejected"); }
catch (BusinessException) { Check(true, "NaN rejected"); }
var future = Make(40, now.AddHours(1));
try { TrafficValidator.Validate(future); Check(false, "Future timestamp rejected"); }
catch (BusinessException) { Check(true, "Future timestamp rejected"); }

string folder = Path.Combine(Path.GetTempPath(), "TrafficPulseTests-" + Guid.NewGuid());
Directory.CreateDirectory(folder);
try
{
    var repository = new DemoRepository(Path.Combine(folder, "test.json"), analysis);
    await repository.InitializeAsync();
    var user = new AppUser { Email = "test@example.test", Name = "Test", PasswordHash = "not-a-real-account" };
    await repository.AddUserAsync(user);
    var road = new Road { Id = roadId, Name = "Test road", Latitude = 32, Longitude = 34 };
    await repository.SaveRoadAsync(road);
    foreach (var reading in history) await repository.SaveReadingAsync(reading);
    var anomalous = Make(10);
    await repository.SaveReadingAsync(anomalous);
    Check(!await repository.SaveReadingAsync(anomalous), "Duplicate reading is idempotent");
    Check((await repository.GetAlertsAsync()).Count == 1, "Duplicate event does not duplicate alert");
    var concurrency = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => repository.SaveReadingAsync(anomalous)));
    Check(concurrency.All(value => !value), "Concurrent duplicate writes are rejected");
    var oldReading = Make(45, now.AddDays(-2)); await repository.SaveReadingAsync(oldReading);
    Check((await repository.GetLatestAsync()).Single().Id == anomalous.Id, "Out-of-order event does not replace latest");
    await repository.SetFavoriteAsync(user.Id, roadId, true);
    await repository.SetFavoriteAsync(user.Id, roadId, true);
    Check((await repository.GetFavoritesAsync(user.Id)).Count == 1, "Favorite is idempotent");
    var alert = (await repository.GetAlertsAsync()).Single();
    await repository.UpdateAlertAsync(alert.Id, "Resolved", alert.Version, user.Id);
    try { await repository.UpdateAlertAsync(alert.Id, "Open", alert.Version, user.Id); Check(false, "Stale alert update rejected"); }
    catch (BusinessException ex) { Check(ex.StatusCode == 409, "Stale alert update rejected"); }
    var before = await repository.GetUserAsync(user.Id);
    await repository.SetUserActiveAsync(user.Id, false);
    var after = await repository.GetUserAsync(user.Id);
    Check(!after!.IsActive && before!.SecurityStamp != after.SecurityStamp, "Disabling user revokes old credentials");
    var reopened = new DemoRepository(Path.Combine(folder, "test.json"), analysis);
    await reopened.InitializeAsync();
    Check((await reopened.GetAlertsAsync()).Single().Status == "Resolved", "Demo state survives restart");
}
finally { Directory.Delete(folder, true); }

var handler = new StubHandler(HttpStatusCode.OK, """
{"flowSegmentData":{"currentSpeed":28,"freeFlowSpeed":70,"confidence":0.9,"roadClosure":false}}
""");
using var http = new HttpClient(handler);
var source = new TomTomTrafficService(http, "test-key-not-real");
var response = await source.GetReadingAsync(new Road { Id = roadId, Latitude = 32.1, Longitude = 34.8 }, CancellationToken.None);
Check(response.CurrentSpeed == 28 && response.Source == "TomTom", "External API response maps correctly");
Check(handler.RequestUri!.Query.Contains("point=32.1,34.8"), "Coordinates use invariant formatting");
using var badHttp = new HttpClient(new StubHandler(HttpStatusCode.Unauthorized, "{}"));
try { await new TomTomTrafficService(badHttp, "invalid-key").GetReadingAsync(new Road(), CancellationToken.None); Check(false, "Provider auth failure"); }
catch (BusinessException ex) { Check(ex.StatusCode == 502, "Provider auth failure is surfaced without fake data"); }

// בדיקת מודל Pomelo ותרגום LINQ בלי לדרוש שרת MySQL פעיל.
var options = new DbContextOptionsBuilder<TrafficDbContext>()
    .UseMySql("Server=localhost;Database=translation_test;User=test", new MySqlServerVersion(new Version(8, 0, 0))).Options;
using var context = new TrafficDbContext(options);
string schema = context.Database.GenerateCreateScript();
Check(context.Model.GetEntityTypes().Count() == 5, "EF model contains exactly five business tables");
Check(schema.Contains("Favorites") && schema.Contains("FOREIGN KEY"), "Pomelo generates keys and relationships");
string latestQuery = context.Readings.GroupBy(r => r.RoadId).Select(g => g.OrderByDescending(r => r.CollectedAtUtc).ThenByDescending(r => r.Id).First()).ToQueryString();
Check(latestQuery.Contains("ROW_NUMBER"), "Latest-per-road LINQ translates to SQL");
Console.WriteLine($"\n{passed} tests passed. Live MySQL/Kafka/TomTom integration requires configured services.");

class StubHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _content;
    public Uri? RequestUri { get; private set; }
    public StubHandler(HttpStatusCode status, string content) { _status = status; _content = content; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestUri = request.RequestUri;
        return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_content) });
    }
}
