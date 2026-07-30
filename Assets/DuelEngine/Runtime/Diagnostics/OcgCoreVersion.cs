using ArcaneDuel.DuelEngine.Interop;

namespace ArcaneDuel.DuelEngine.Diagnostics
{
    public readonly struct OcgCoreVersion
    {
        public OcgCoreVersion(int major, int minor)
        {
            Major = major;
            Minor = minor;
        }

        public int Major { get; }

        public int Minor { get; }

        public override string ToString()
        {
            return $"{Major}.{Minor}";
        }
    }

    public static class OcgCoreVersionProbe
    {
        public static OcgCoreVersion Read()
        {
            OcgCoreNative.GetVersion(out int major, out int minor);
            return new OcgCoreVersion(major, minor);
        }
    }
}
