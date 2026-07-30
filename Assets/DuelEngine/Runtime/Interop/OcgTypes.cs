using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ArcaneDuel.DuelEngine.Interop
{
    internal enum OcgDuelCreationStatus
    {
        Success = 0,
        NoOutput = 1,
        NotCreated = 2,
        NullDataReader = 3,
        NullScriptReader = 4,
        IncompatibleLuaApi = 5,
        NullRngSeed = 6
    }

    public enum OcgDuelStatus
    {
        End = 0,
        Awaiting = 1,
        Continue = 2
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void OcgDataReader(IntPtr payload, uint code, IntPtr data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void OcgDataReaderDone(IntPtr payload, IntPtr data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int OcgScriptReader(IntPtr payload, IntPtr duel, IntPtr name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void OcgLogHandler(IntPtr payload, IntPtr message, int type);

    [StructLayout(LayoutKind.Sequential)]
    internal struct OcgCardData
    {
        internal uint Code;
        internal uint Alias;
        internal IntPtr Setcodes;
        internal uint Type;
        internal uint Level;
        internal uint Attribute;
        internal ulong Race;
        internal int Attack;
        internal int Defense;
        internal uint LeftScale;
        internal uint RightScale;
        internal uint LinkMarker;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct OcgPlayer
    {
        internal uint StartingLifePoints;
        internal uint StartingDrawCount;
        internal uint DrawCountPerTurn;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct OcgDuelOptions
    {
        internal ulong Seed0;
        internal ulong Seed1;
        internal ulong Seed2;
        internal ulong Seed3;
        internal ulong Flags;
        internal OcgPlayer Team1;
        internal OcgPlayer Team2;
        internal OcgDataReader CardReader;
        internal IntPtr CardReaderPayload;
        internal OcgScriptReader ScriptReader;
        internal IntPtr ScriptReaderPayload;
        internal OcgLogHandler LogHandler;
        internal IntPtr LogHandlerPayload;
        internal OcgDataReaderDone CardReaderDone;
        internal IntPtr CardReaderDonePayload;
        internal byte EnableUnsafeLibraries;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct OcgNewCardInfo
    {
        internal byte Team;
        internal byte Duelist;
        internal uint Code;
        internal byte Controller;
        internal uint Location;
        internal uint Sequence;
        internal uint Position;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct OcgQueryInfo
    {
        internal uint Flags;
        internal byte Controller;
        internal uint Location;
        internal uint Sequence;
        internal uint OverlaySequence;
    }

    internal sealed class OcgDuelSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal OcgDuelSafeHandle() : base(true)
        {
        }

        internal void Initialize(IntPtr value)
        {
            SetHandle(value);
        }

        protected override bool ReleaseHandle()
        {
            OcgCoreNative.DestroyDuel(handle);
            return true;
        }
    }
}
