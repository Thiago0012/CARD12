using System;
using System.Collections.Generic;
using ArcaneDuel.DuelEngine.Interop;
using ArcaneDuel.DuelEngine.Protocol;

namespace ArcaneDuel.DuelEngine.Abstractions
{
    public interface IDuelRulesEngine : IDisposable
    {
        bool IsStarted { get; }
        bool IsFinished { get; }
        OcgDuelStatus Status { get; }
        DuelPrompt CurrentPrompt { get; }
        IReadOnlyList<DuelEvent> EventHistory { get; }
        event Action<DuelEvent> EventReceived;
        void AddCard(byte team, uint code, uint location);
        void Start();
        OcgDuelStatus Process();
        void SubmitResponse(byte[] response);
    }
}
