using System.IO;
using System.Linq;
using ArcaneDuel.DuelEngine.Diagnostics;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class RuntimeDiagnosticRecorderEditModeTests
    {
        [Test]
        public void RecorderPersistsStructuredEvidenceAndRedactsSecrets()
        {
            string path = RuntimeDiagnosticRecorder.CurrentLogPath;
            Assert.That(path, Does.Contain(
                Path.Combine("Logs", "CardAuditRuntime")));

            RuntimeDiagnosticRecorder.Record(
                "F08",
                "Multiplayer",
                "RecorderTest",
                "roomCode=ABC123 seed=987654",
                cardCode: 89631139,
                seat: 1,
                mode: "test");

            Assert.That(File.Exists(path), Is.True);
            string line = File.ReadLines(path).Last();
            Assert.That(line, Does.Contain("\"failureCode\":\"F08\""));
            Assert.That(line, Does.Contain("\"cardCode\":89631139"));
            Assert.That(line, Does.Contain("\"seat\":1"));
            Assert.That(line, Does.Not.Contain("ABC123"));
            Assert.That(line, Does.Not.Contain("987654"));
            Assert.That(line, Does.Contain("<redacted>"));
        }
    }
}
