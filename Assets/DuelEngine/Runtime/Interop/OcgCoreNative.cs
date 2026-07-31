using System;
using System.Runtime.InteropServices;
using System.Security;

namespace ArcaneDuel.DuelEngine.Interop
{
    [SuppressUnmanagedCodeSecurity]
    internal static class OcgCoreNative
    {
        internal const string LibraryName = "ocgcore";

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true, EntryPoint = "OCG_GetVersion")]
        internal static extern void GetVersion(out int major, out int minor);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true, EntryPoint = "OCG_CreateDuel")]
        internal static extern int CreateDuel(out IntPtr duel, ref OcgDuelOptions options);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true, EntryPoint = "OCG_DestroyDuel")]
        internal static extern void DestroyDuel(IntPtr duel);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true, EntryPoint = "OCG_DuelNewCard")]
        internal static extern void DuelNewCard(OcgDuelSafeHandle duel, ref OcgNewCardInfo info);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true, EntryPoint = "OCG_StartDuel")]
        internal static extern void StartDuel(OcgDuelSafeHandle duel);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true, EntryPoint = "OCG_DuelProcess")]
        internal static extern int DuelProcess(OcgDuelSafeHandle duel);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true, EntryPoint = "OCG_DuelGetMessage")]
        internal static extern IntPtr DuelGetMessage(OcgDuelSafeHandle duel, out uint length);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true, EntryPoint = "OCG_DuelSetResponse")]
        internal static extern void DuelSetResponse(OcgDuelSafeHandle duel, byte[] response, uint length);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true, EntryPoint = "OCG_DuelQuery")]
        internal static extern IntPtr DuelQuery(
            OcgDuelSafeHandle duel,
            out uint length,
            ref OcgQueryInfo info);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true, EntryPoint = "OCG_LoadScript")]
        internal static extern int LoadScript(IntPtr duel, byte[] script, uint length, [MarshalAs(UnmanagedType.LPStr)] string name);
    }
}
