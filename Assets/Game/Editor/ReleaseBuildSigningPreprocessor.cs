using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace ArcaneDuel.Editor
{
    /// <summary>
    /// Applies the production Android identity even when a release APK is
    /// created through Unity's standard Build button rather than through the
    /// Arcane Duel menu. Android accepts an update only when its signing
    /// certificate matches the installed application, so a missing keystore
    /// must fail the release build before an unusable APK is produced.
    /// </summary>
    internal sealed class ReleaseBuildSigningPreprocessor :
        IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android)
                return;

            bool development = (report.summary.options &
                BuildOptions.Development) != 0;
            ReleaseBuildSigningConfiguration.ApplyAndroidSigning(
                required: !development);
        }
    }
}
