using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ArcaneDuel.Game.Tournaments
{
    [Serializable]
    public sealed class TournamentConnectionTicket
    {
        public string tournamentId;
        public string lobbyId;
        public string lobbyCode;
        public string localPlayerId;
        public bool localPlayerIsOrganizer;
        public long updatedAtUtcTicks;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(tournamentId) &&
            !string.IsNullOrWhiteSpace(lobbyCode) &&
            !string.IsNullOrWhiteSpace(localPlayerId);
    }

    [Serializable]
    public sealed class TournamentPersistenceEnvelope
    {
        public int schemaVersion = 1;
        public TournamentState activeTournament;
        public TournamentConnectionTicket connectionTicket;
        public List<TournamentState> history = new List<TournamentState>();
    }

    /// <summary>
    /// Save local transacional com cópia de segurança. O host grava após cada
    /// mutação autoritativa; clientes guardam o último espelho para consulta e
    /// retomada do lobby.
    /// </summary>
    public sealed class TournamentPersistenceStore
    {
        public const int MaximumHistoryEntries = 20;
        private readonly string savePath;

        public TournamentPersistenceStore()
            : this(Path.Combine(
                Application.persistentDataPath,
                "ArcaneArena",
                "tournaments.json"))
        {
        }

        public TournamentPersistenceStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Caminho de save vazio.", nameof(path));
            savePath = Path.GetFullPath(path);
        }

        public string SavePath => savePath;

        public TournamentPersistenceEnvelope Load()
        {
            TournamentPersistenceEnvelope loaded = TryLoad(savePath);
            if (loaded == null)
                loaded = TryLoad(savePath + ".bak");
            loaded ??= new TournamentPersistenceEnvelope();
            Normalize(loaded);
            return loaded;
        }

        public void Save(TournamentPersistenceEnvelope envelope)
        {
            envelope ??= new TournamentPersistenceEnvelope();
            Normalize(envelope);
            string directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string temporaryPath = savePath + ".tmp";
            string backupPath = savePath + ".bak";
            File.WriteAllText(
                temporaryPath,
                JsonUtility.ToJson(envelope, true));
            if (!File.Exists(savePath))
            {
                File.Move(temporaryPath, savePath);
                return;
            }
            try
            {
                File.Replace(temporaryPath, savePath, backupPath, true);
            }
            catch (PlatformNotSupportedException)
            {
                PortableReplace(temporaryPath, backupPath);
            }
            catch (IOException)
            {
                PortableReplace(temporaryPath, backupPath);
            }
        }

        public void SaveActive(
            TournamentPersistenceEnvelope envelope,
            TournamentState state,
            TournamentConnectionTicket ticket)
        {
            envelope ??= new TournamentPersistenceEnvelope();
            envelope.activeTournament = Clone(state);
            envelope.connectionTicket = ticket;
            Save(envelope);
        }

        public void ArchiveActive(TournamentPersistenceEnvelope envelope)
        {
            envelope ??= new TournamentPersistenceEnvelope();
            if (envelope.activeTournament != null)
            {
                TournamentState archived = Clone(envelope.activeTournament);
                envelope.history.RemoveAll(item =>
                    item?.config != null && string.Equals(
                        item.config.tournamentId,
                        archived?.config?.tournamentId,
                        StringComparison.Ordinal));
                envelope.history.Insert(0, archived);
                if (envelope.history.Count > MaximumHistoryEntries)
                {
                    envelope.history.RemoveRange(
                        MaximumHistoryEntries,
                        envelope.history.Count - MaximumHistoryEntries);
                }
            }
            envelope.activeTournament = null;
            envelope.connectionTicket = null;
            Save(envelope);
        }

        public void ClearActive(TournamentPersistenceEnvelope envelope)
        {
            envelope ??= new TournamentPersistenceEnvelope();
            envelope.activeTournament = null;
            envelope.connectionTicket = null;
            Save(envelope);
        }

        public static TournamentState Clone(TournamentState state)
        {
            return state == null
                ? null
                : JsonUtility.FromJson<TournamentState>(
                    JsonUtility.ToJson(state));
        }

        private TournamentPersistenceEnvelope TryLoad(string path)
        {
            if (!File.Exists(path))
                return null;
            try
            {
                return JsonUtility.FromJson<TournamentPersistenceEnvelope>(
                    File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Tournament] Falha ao ler save local: " +
                    exception.GetBaseException().Message);
                return null;
            }
        }

        private void PortableReplace(string temporaryPath, string backupPath)
        {
            if (File.Exists(savePath))
                File.Copy(savePath, backupPath, true);
            File.Copy(temporaryPath, savePath, true);
            File.Delete(temporaryPath);
        }

        private static void Normalize(TournamentPersistenceEnvelope envelope)
        {
            envelope.schemaVersion = 1;
            envelope.history ??= new List<TournamentState>();
            envelope.history.RemoveAll(item => item == null || item.config == null);
            if (envelope.history.Count > MaximumHistoryEntries)
            {
                envelope.history.RemoveRange(
                    MaximumHistoryEntries,
                    envelope.history.Count - MaximumHistoryEntries);
            }
        }
    }
}
