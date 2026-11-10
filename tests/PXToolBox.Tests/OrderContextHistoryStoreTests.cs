#if NET48
// Só compila na perna net48 — PXToolBoxDev (host) só existe lá. Contextos de produção recentes:
// só caminhos, mais recente primeiro, sem repetir, no máximo 10; leitura nunca lança. O arquivo é
// sempre temporário (SetFilePathForTesting), nunca o real do usuário.
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PXToolBoxDev.Integrations;
using Xunit;

namespace PXToolBox.Tests
{
    [Collection("Order context history static test seam")]
    public class OrderContextHistoryStoreTests : IDisposable
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "px-ctx-history-" + Guid.NewGuid().ToString("N"));
        private readonly string _file;

        public OrderContextHistoryStoreTests()
        {
            _file = Path.Combine(_dir, "config", "order-context-history.json");
            OrderContextHistoryStore.SetFilePathForTesting(_file);
        }

        public void Dispose()
        {
            OrderContextHistoryStore.SetFilePathForTesting(null);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Fact]
        public void WithRecorded_PutsNewestFirst_MovesRepeatedPathToTop_IgnoringCase()
        {
            var list = OrderContextHistoryStore.WithRecorded(Array.Empty<OrderContextHistoryStore.Entry>(), @"Z:\ctx\a.json", T0);
            list = OrderContextHistoryStore.WithRecorded(list, @"Z:\ctx\b.json", T0.AddMinutes(1));
            list = OrderContextHistoryStore.WithRecorded(list, @" z:\CTX\A.json ", T0.AddMinutes(2));

            Assert.Equal(new[] { @"z:\CTX\A.json", @"Z:\ctx\b.json" }, list.Select(e => e.Path));
            Assert.Equal(T0.AddMinutes(2), list[0].LoadedAtUtc);
        }

        [Fact]
        public void WithRecorded_KeepsAtMostTen_DroppingTheOldest()
        {
            var list = Enumerable.Range(1, 12).Aggregate(
                (System.Collections.Generic.IReadOnlyList<OrderContextHistoryStore.Entry>)Array.Empty<OrderContextHistoryStore.Entry>(),
                (acc, i) => OrderContextHistoryStore.WithRecorded(acc, $@"Z:\ctx\{i}.json", T0.AddMinutes(i)));

            Assert.Equal(OrderContextHistoryStore.MaxEntries, list.Count);
            Assert.Equal(@"Z:\ctx\12.json", list[0].Path);
            Assert.Equal(@"Z:\ctx\3.json", list.Last().Path);
        }

        [Fact]
        public void RecordRemoveClear_RoundTripThroughTheFile_OnlyPathsAndDates()
        {
            Assert.Empty(OrderContextHistoryStore.Load());

            OrderContextHistoryStore.Record(@"Z:\ctx\a.json", T0);
            OrderContextHistoryStore.Record(@"Z:\ctx\b.json", T0.AddMinutes(5));
            var loaded = OrderContextHistoryStore.Load();
            Assert.Equal(new[] { @"Z:\ctx\b.json", @"Z:\ctx\a.json" }, loaded.Select(e => e.Path));
            Assert.Equal(T0.AddMinutes(5), loaded[0].LoadedAtUtc);

            var root = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(_file), new JsonSerializerSettings { DateParseHandling = DateParseHandling.None })!;
            Assert.Equal(OrderContextHistoryStore.FormatId, (string?)root["formatId"]);
            Assert.Equal(1, (int)root["schemaVersion"]!);
            Assert.Equal("2026-11-06T12:05:00Z", (string?)root["entries"]![0]!["loadedAtUtc"]);
            Assert.All(((JArray)root["entries"]!).OfType<JObject>(), e => Assert.Equal(new[] { "path", "loadedAtUtc" }, e.Properties().Select(p => p.Name)));

            OrderContextHistoryStore.Remove(@"z:\CTX\B.JSON");
            Assert.Equal(new[] { @"Z:\ctx\a.json" }, OrderContextHistoryStore.Load().Select(e => e.Path));

            OrderContextHistoryStore.Clear();
            Assert.Empty(OrderContextHistoryStore.Load());
        }

        [Theory]
        [InlineData("não é json")]
        [InlineData("[]")]
        [InlineData("{ \"formatId\": \"Outro\", \"schemaVersion\": 1, \"entries\": [] }")]
        [InlineData("{ \"formatId\": \"PxToolBoxDevOrderContextHistory\", \"schemaVersion\": 2, \"entries\": [ { \"path\": \"Z:\\\\a.json\" } ] }")]
        [InlineData("{ \"formatId\": \"PxToolBoxDevOrderContextHistory\", \"schemaVersion\": 1, \"entries\": \"x\" }")]
        public void Load_BadOrForeignFile_IsEmpty_NeverThrows(string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, content);

            Assert.Empty(OrderContextHistoryStore.Load());
        }

        [Fact]
        public void Parse_SkipsBlankAndRepeatedPaths_AndToleratesMissingDate()
        {
            var json = "{ \"formatId\": \"PxToolBoxDevOrderContextHistory\", \"schemaVersion\": 1, \"entries\": [" +
                       "{ \"path\": \"Z:\\\\a.json\", \"loadedAtUtc\": \"2026-11-06T12:00:00Z\" }, { \"path\": \"  \" }, 7, " +
                       "{ \"path\": \"z:\\\\A.JSON\" }, { \"path\": \"Z:\\\\b.json\", \"loadedAtUtc\": \"ontem\" } ] }";

            var entries = OrderContextHistoryStore.Parse(json);

            Assert.Equal(new[] { @"Z:\a.json", @"Z:\b.json" }, entries.Select(e => e.Path));
            Assert.Equal(T0, entries[0].LoadedAtUtc);
            Assert.Equal(DateTimeOffset.MinValue, entries[1].LoadedAtUtc);
        }
    }
}
#endif
