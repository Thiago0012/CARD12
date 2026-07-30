using System;
using System.IO;

namespace ArcaneDuel.DuelEngine.Scripts
{
    internal sealed class ScriptRepository
    {
        private readonly string customRoot;
        private readonly string scriptsRoot;
        private readonly string officialRoot;

        internal ScriptRepository(string ygoRoot)
        {
            customRoot = Path.Combine(ygoRoot, "CustomScripts");
            scriptsRoot = Path.Combine(ygoRoot, "Scripts");
            officialRoot = Path.Combine(scriptsRoot, "official");
        }

        internal bool TryRead(string requestedName, out byte[] bytes)
        {
            string safeName = Path.GetFileName(requestedName);
            if (!string.Equals(safeName, requestedName, StringComparison.Ordinal))
            {
                bytes = null;
                return false;
            }

            string[] candidates =
            {
                Path.Combine(customRoot, safeName),
                Path.Combine(scriptsRoot, safeName),
                Path.Combine(officialRoot, safeName)
            };
            foreach (string path in candidates)
            {
                if (File.Exists(path))
                {
                    bytes = File.ReadAllBytes(path);
                    return true;
                }
            }
            bytes = null;
            return false;
        }
    }
}
