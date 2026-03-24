using System.Text.Json;
using TUnit.Core;
using Wollax.Cupel;
using Wollax.Cupel.Diagnostics;
using Wollax.Cupel.Testing;

namespace Wollax.Cupel.Testing.Tests;

[NotInParallel(Order = 1)]
public class SnapshotTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cupel-snap-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string FakeCallerPath(string tempDir) => Path.Combine(tempDir, "FakeTest.cs");

    private static string SnapshotDir(string tempDir) => Path.Combine(tempDir, "__snapshots__");

    private static void CleanupTempDir(string tempDir)
    {
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, recursive: true);
    }

    private static ContextItem MakeItem(string content = "x", int tokens = 10, ContextKind? kind = null)
        => new() { Content = content, Tokens = tokens, Kind = kind ?? ContextKind.Message };

    private static IncludedItem MakeIncluded(ContextItem? item = null, double score = 0.5, InclusionReason reason = InclusionReason.Scored)
        => new() { Item = item ?? MakeItem(), Score = score, Reason = reason };

    private static SelectionReport MakeReport(
        IReadOnlyList<IncludedItem>? included = null,
        IReadOnlyList<ExcludedItem>? excluded = null,
        int totalCandidates = 5,
        int totalTokensConsidered = 100)
        => new()
        {
            Events = [],
            Included = included ?? [],
            Excluded = excluded ?? [],
            TotalCandidates = totalCandidates,
            TotalTokensConsidered = totalTokensConsidered,
        };

    // ── Test: Create ────────────────────────────────────────────────────────

    [Test]
    public void Create_FirstCallWritesSnapshotFile()
    {
        var tempDir = CreateTempDir();
        try
        {
            var report = MakeReport(
                included: [MakeIncluded(MakeItem("hello", 42, ContextKind.Document))],
                totalCandidates: 1,
                totalTokensConsidered: 42);

            report.Should().MatchSnapshotCore("create-test", FakeCallerPath(tempDir));

            var snapshotPath = Path.Combine(SnapshotDir(tempDir), "create-test.json");
            if (!File.Exists(snapshotPath))
                throw new Exception($"Snapshot file was not created at {snapshotPath}");

            var content = File.ReadAllText(snapshotPath);

            // Verify it's valid JSON
            JsonDocument.Parse(content);

            // Verify key fields are present (camelCase)
            if (!content.Contains("\"hello\""))
                throw new Exception("Snapshot JSON does not contain expected content field value");
            if (!content.Contains("\"totalCandidates\""))
                throw new Exception("Snapshot JSON does not contain totalCandidates field (camelCase)");
            // ContextKind is a class with its own JsonConverter, serializes as PascalCase
            if (!content.Contains("\"Document\""))
                throw new Exception("Snapshot JSON does not contain ContextKind value 'Document'");
        }
        finally
        {
            CleanupTempDir(tempDir);
        }
    }

    // ── Test: Match ─────────────────────────────────────────────────────────

    [Test]
    public void Match_IdenticalReportDoesNotThrow()
    {
        var tempDir = CreateTempDir();
        try
        {
            var report = MakeReport(
                included: [MakeIncluded(MakeItem("stable", 20))],
                totalCandidates: 1,
                totalTokensConsidered: 20);

            // First call creates the snapshot
            report.Should().MatchSnapshotCore("match-test", FakeCallerPath(tempDir));

            // Second call with identical report should not throw
            report.Should().MatchSnapshotCore("match-test", FakeCallerPath(tempDir));
        }
        finally
        {
            CleanupTempDir(tempDir);
        }
    }

    // ── Test: Fail ──────────────────────────────────────────────────────────

    [Test]
    public void Fail_DifferentReportThrowsSnapshotMismatchException()
    {
        var tempDir = CreateTempDir();
        try
        {
            var reportA = MakeReport(totalCandidates: 5, totalTokensConsidered: 100);
            var reportB = MakeReport(totalCandidates: 99, totalTokensConsidered: 999);

            // Create snapshot from report A
            reportA.Should().MatchSnapshotCore("fail-test", FakeCallerPath(tempDir));

            // Call with report B should throw
            try
            {
                reportB.Should().MatchSnapshotCore("fail-test", FakeCallerPath(tempDir));
                throw new Exception("Expected SnapshotMismatchException but no exception was thrown");
            }
            catch (SnapshotMismatchException ex)
            {
                if (!ex.Message.Contains("fail-test"))
                    throw new Exception($"Exception message does not contain snapshot name. Got: {ex.Message}");
                if (!ex.SnapshotName.Equals("fail-test"))
                    throw new Exception($"SnapshotName property incorrect. Got: {ex.SnapshotName}");
                if (!ex.Expected.Contains("\"totalCandidates\": 5"))
                    throw new Exception($"Expected field does not contain original report data. Got: {ex.Expected}");
                if (!ex.Actual.Contains("\"totalCandidates\": 99"))
                    throw new Exception($"Actual field does not contain new report data. Got: {ex.Actual}");
            }
        }
        finally
        {
            CleanupTempDir(tempDir);
        }
    }

    // ── Test: Update ────────────────────────────────────────────────────────

    [Test]
    public void Update_EnvVarOverwritesStaleSnapshot()
    {
        var tempDir = CreateTempDir();
        try
        {
            var reportOld = MakeReport(totalCandidates: 1, totalTokensConsidered: 10);
            var reportNew = MakeReport(totalCandidates: 42, totalTokensConsidered: 420);

            // Create snapshot from old report
            reportOld.Should().MatchSnapshotCore("update-test", FakeCallerPath(tempDir));

            // Set env var to enable updates
            Environment.SetEnvironmentVariable("CUPEL_UPDATE_SNAPSHOTS", "1");

            // Call with new report should NOT throw — should update the file instead
            reportNew.Should().MatchSnapshotCore("update-test", FakeCallerPath(tempDir));

            // Verify the file now contains the new report data
            var snapshotPath = Path.Combine(SnapshotDir(tempDir), "update-test.json");
            var content = File.ReadAllText(snapshotPath);

            if (!content.Contains("\"totalCandidates\": 42"))
                throw new Exception($"Snapshot file was not updated with new report. Content: {content[..Math.Min(200, content.Length)]}");
            if (content.Contains("\"totalCandidates\": 1"))
                throw new Exception("Snapshot file still contains old report data");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CUPEL_UPDATE_SNAPSHOTS", null);
            CleanupTempDir(tempDir);
        }
    }

    // ── Test: No-Update Without Env Var ─────────────────────────────────────

    [Test]
    public void NoUpdate_WithoutEnvVarMismatchThrows()
    {
        var tempDir = CreateTempDir();
        try
        {
            var reportOld = MakeReport(totalCandidates: 1, totalTokensConsidered: 10);
            var reportNew = MakeReport(totalCandidates: 77, totalTokensConsidered: 770);

            // Create snapshot from old report
            reportOld.Should().MatchSnapshotCore("noupdate-test", FakeCallerPath(tempDir));

            // Ensure env var is NOT set
            Environment.SetEnvironmentVariable("CUPEL_UPDATE_SNAPSHOTS", null);

            // Call with different report should throw — must NOT silently update
            try
            {
                reportNew.Should().MatchSnapshotCore("noupdate-test", FakeCallerPath(tempDir));
                throw new Exception("Expected SnapshotMismatchException but no exception was thrown");
            }
            catch (SnapshotMismatchException)
            {
                // Expected — verify the file was NOT changed
                var snapshotPath = Path.Combine(SnapshotDir(tempDir), "noupdate-test.json");
                var content = File.ReadAllText(snapshotPath);
                if (!content.Contains("\"totalCandidates\": 1"))
                    throw new Exception("Snapshot file was modified despite env var not being set");
            }
        }
        finally
        {
            CleanupTempDir(tempDir);
        }
    }
}
