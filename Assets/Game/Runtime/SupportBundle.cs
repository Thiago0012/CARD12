using System;
using System.IO;
using System.Text;
using ArcaneDuel.DuelEngine.Diagnostics;
using UnityEngine;

namespace ArcaneDuel.Game
{
    public static class SupportBundle
    {
        public static string Export()
        {
            string directory = Path.Combine(
                Application.persistentDataPath,
                "Support");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(
                directory,
                $"ArcaneDuel-Support-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt");

            var text = new StringBuilder();
            text.AppendLine("ARCANE DUEL SUPPORT BUNDLE");
            text.AppendLine($"Generated UTC: {DateTime.UtcNow:O}");
            text.AppendLine($"Project: {ProjectIdentity.ProjectVersion}");
            text.AppendLine($"Unity runtime: {Application.unityVersion}");
            text.AppendLine($"Platform: {Application.platform}");
            text.AppendLine($"Operating system: {SystemInfo.operatingSystem}");
            text.AppendLine($"Processor: {SystemInfo.processorType}");
            text.AppendLine($"Memory MB: {SystemInfo.systemMemorySize}");
            text.AppendLine($"Graphics: {SystemInfo.graphicsDeviceName}");
            text.AppendLine($"Core API: {ReadCoreVersion()}");
            text.AppendLine($"Core commit: {ProjectIdentity.CoreCommit}");
            text.AppendLine($"CardScripts commit: {ProjectIdentity.CardScriptsCommit}");
            text.AppendLine($"BabelCDB commit: {ProjectIdentity.BabelCdbCommit}");
            text.AppendLine($"Active deck: {DeckRepository.ActiveDeckPath}");
            text.AppendLine();
            text.AppendLine("PLAYER LOG");
            text.AppendLine(new string('-', 72));

            string logPath = Application.consoleLogPath;
            if (!string.IsNullOrWhiteSpace(logPath) && File.Exists(logPath))
            {
                text.AppendLine(File.ReadAllText(logPath));
            }
            else
            {
                text.AppendLine($"Player log not found at '{logPath}'.");
            }

            File.WriteAllText(path, text.ToString(), Encoding.UTF8);
            return path;
        }

        private static string ReadCoreVersion()
        {
            try
            {
                return OcgCoreVersionProbe.Read().ToString();
            }
            catch (Exception exception)
            {
                return $"unavailable ({exception.GetBaseException().Message})";
            }
        }
    }
}
