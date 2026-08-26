using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using ArcaneArena.Frontend;
using UnityEditor;
using UnityEngine;

namespace ArcaneDuel.Editor.RemoteUpdates
{
    /// <summary>
    /// Prepares the single release envelope consumed by Android and Windows.
    /// It classifies repository changes and refuses publications that would
    /// leave either platform without a valid update path.
    /// </summary>
    public sealed class RemoteReleasePublisherWindow : EditorWindow
    {
        [Serializable]
        private sealed class YgoPatchArchiveManifest
        {
            public int schemaVersion = 1;
            public string[] files = Array.Empty<string>();
            public string[] deletedFiles = Array.Empty<string>();
        }

        private const string MenuPath =
            "Master Duel 2 Plus Ultra/Atualizações/Central de Publicação";
        private const string DefaultPackageBaseUrl =
            "https://github.com/Thiago0012/CARD12/releases/download/{tag}";

        private static readonly string[] IgnoredReleasePrefixes =
        {
            ".git/",
            ".utmp/",
            "Backend/",
            "Builds/",
            "ContentStaging/",
            "Docs/",
            "Logs/",
            "TestResults/"
        };

        private Vector2 _scroll;
        private readonly List<string> _changedFiles = new List<string>();
        private readonly List<string> _clientChanges = new List<string>();
        private readonly List<string> _contentChanges = new List<string>();
        private readonly List<string> _outsideChanges = new List<string>();
        private string _latestClientVersion = "1.2.0";
        private int _androidVersionCode = 1;
        private int _protocolVersion = 1;
        private string _contentVersion = "0.0.0";
        private string _windowsUrl = string.Empty;
        private string _androidUrl = string.Empty;
        private string _fallbackUrl = string.Empty;
        private string _packageBaseUrl = DefaultPackageBaseUrl;
        private string _releaseNotes = string.Empty;
        private string _status = "ANÁLISE AINDA NÃO EXECUTADA";
        private MessageType _statusType = MessageType.Info;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<RemoteReleasePublisherWindow>();
            window.titleContent = new GUIContent("Atualizações");
            window.minSize = new Vector2(720f, 680f);
            window.LoadPublishedDefaults();
            window.RefreshChangeClassification();
            window.Show();
        }

        private void OnEnable()
        {
            LoadPublishedDefaults();
            RefreshChangeClassification();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawHeader();
            EditorGUILayout.Space(12f);
            DrawClassification();
            EditorGUILayout.Space(12f);
            DrawReleaseForm();
            EditorGUILayout.Space(12f);
            EditorGUILayout.HelpBox(_status, _statusType);
            DrawActions();
            EditorGUILayout.Space(18f);
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField(
                "CENTRAL DE PUBLICAÇÃO",
                new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 19,
                    normal = { textColor = new Color(0.14f, 0.82f, 1f) }
                });
            EditorGUILayout.LabelField(
                "MASTER DUEL 2 PLUS ULTRA  •  ANDROID + WINDOWS",
                EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "Toda versão publicada é obrigatória. Conteúdo YGO pode ser " +
                "instalado dentro do jogo. Código, cenas, interface, shaders, " +
                "plug-ins e configurações exigem uma nova build do aplicativo.",
                MessageType.Info);
        }

        private void DrawClassification()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    "1  •  ALTERAÇÕES DETECTADAS",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    $"Aplicativo: {_clientChanges.Count}   |   " +
                    $"Conteúdo remoto: {_contentChanges.Count}   |   " +
                    $"Fora da build: {_outsideChanges.Count}");

                if (_clientChanges.Count > 0)
                    DrawFileGroup("NOVA BUILD OBRIGATÓRIA", _clientChanges);
                if (_contentChanges.Count > 0)
                    DrawFileGroup("PACOTE DE CONTEÚDO", _contentChanges);
                if (_changedFiles.Count == 0)
                    EditorGUILayout.LabelField(
                        "O Git não encontrou alterações pendentes.",
                        EditorStyles.miniLabel);

                if (GUILayout.Button("REANALISAR O PROJETO", GUILayout.Height(30f)))
                    RefreshChangeClassification();
            }
        }

        private static void DrawFileGroup(
            string title,
            IReadOnlyList<string> files)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            int shown = Math.Min(files.Count, 8);
            for (int index = 0; index < shown; index++)
                EditorGUILayout.LabelField("• " + files[index],
                    EditorStyles.miniLabel);
            if (files.Count > shown)
                EditorGUILayout.LabelField(
                    $"… e mais {files.Count - shown} arquivo(s)",
                    EditorStyles.miniLabel);
        }

        private void DrawReleaseForm()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    "2  •  VERSÃO DO APLICATIVO",
                    EditorStyles.boldLabel);
                _latestClientVersion = EditorGUILayout.TextField(
                    "Versão pública",
                    _latestClientVersion);
                _androidVersionCode = EditorGUILayout.IntField(
                    "Código Android",
                    _androidVersionCode);
                _protocolVersion = EditorGUILayout.IntField(
                    "Protocolo online",
                    _protocolVersion);
                EditorGUILayout.LabelField(
                    "Versão instalada no projeto: " + Application.version +
                    "  •  código Android atual: " +
                    PlayerSettings.Android.bundleVersionCode,
                    EditorStyles.miniLabel);

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField(
                    "DOWNLOAD DAS NOVAS BUILDS",
                    EditorStyles.miniBoldLabel);
                _windowsUrl = EditorGUILayout.TextField(
                    "Windows",
                    _windowsUrl);
                _androidUrl = EditorGUILayout.TextField(
                    "Android",
                    _androidUrl);
                _fallbackUrl = EditorGUILayout.TextField(
                    "Alternativo",
                    _fallbackUrl);
                EditorGUILayout.LabelField(
                    "Artefatos locais esperados:",
                    EditorStyles.miniBoldLabel);
                EditorGUILayout.SelectableLabel(
                    Path.GetFullPath(ExpectedWindowsArtifactPath()),
                    EditorStyles.miniLabel,
                    GUILayout.Height(18f));
                EditorGUILayout.SelectableLabel(
                    Path.GetFullPath(ExpectedAndroidArtifactPath()),
                    EditorStyles.miniLabel,
                    GUILayout.Height(18f));

                EditorGUILayout.Space(12f);
                EditorGUILayout.LabelField(
                    "3  •  CONTEÚDO INSTALÁVEL NO JOGO",
                    EditorStyles.boldLabel);
                _contentVersion = EditorGUILayout.TextField(
                    "Versão do conteúdo",
                    _contentVersion);
                _packageBaseUrl = EditorGUILayout.TextField(
                    "Endereço dos pacotes ({tag} automático)",
                    _packageBaseUrl);

                EditorGUILayout.Space(12f);
                EditorGUILayout.LabelField(
                    "4  •  NOTAS DA ATUALIZAÇÃO",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "Uma novidade por linha (não cria tela adicional no login).",
                    EditorStyles.miniLabel);
                _releaseNotes = EditorGUILayout.TextArea(
                    _releaseNotes,
                    GUILayout.MinHeight(76f));
            }
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        "VALIDAR PUBLICAÇÃO",
                        GUILayout.Height(38f)))
                {
                    ValidateAndReport();
                }
                if (GUILayout.Button(
                        "GERAR PACOTE E MANIFESTO",
                        GUILayout.Height(38f)))
                {
                    GeneratePublication();
                }
            }
            if (GUILayout.Button(
                    "APLICAR VERSÃO ÀS PRÓXIMAS BUILDS",
                    GUILayout.Height(32f)))
            {
                ApplyVersionToBuilds();
            }
        }

        private void LoadPublishedDefaults()
        {
            try
            {
                string path = ProductionEnvelopePath;
                if (!File.Exists(path))
                    return;
                RemoteReleaseEnvelope envelope = JsonUtility.FromJson<
                    RemoteReleaseEnvelope>(File.ReadAllText(path));
                RemoteReleaseManifest manifest = envelope?.payload;
                if (manifest == null)
                    return;
                _latestClientVersion = EmptyFallback(
                    manifest.latestClientVersion,
                    Application.version);
                _contentVersion = EmptyFallback(
                    manifest.contentVersion,
                    "0.0.0");
                _windowsUrl = manifest.windowsUpdateUrl ?? string.Empty;
                _androidUrl = manifest.androidUpdateUrl ?? string.Empty;
                _fallbackUrl = manifest.fallbackUpdateUrl ?? string.Empty;
                _androidVersionCode = Math.Max(
                    manifest.android?.versionCode ??
                    PlayerSettings.Android.bundleVersionCode,
                    PlayerSettings.Android.bundleVersionCode);
                _protocolVersion = Math.Max(1, manifest.protocolVersion);
                _packageBaseUrl = EditorPrefs.GetString(
                    PackageUrlPreference,
                    DefaultPackageBaseUrl);
                if (_packageBaseUrl.StartsWith(
                        "https://raw.githubusercontent.com/Thiago0012/CARD12/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    _packageBaseUrl = DefaultPackageBaseUrl;
                }
            }
            catch (Exception exception)
            {
                SetStatus(
                    "O manifesto atual não pôde ser lido: " +
                    exception.Message,
                    MessageType.Warning);
            }
        }

        private void RefreshChangeClassification()
        {
            _changedFiles.Clear();
            _clientChanges.Clear();
            _contentChanges.Clear();
            _outsideChanges.Clear();
            try
            {
                IEnumerable<string> tracked = RunGit(
                    "diff --name-only HEAD --");
                IEnumerable<string> untracked = RunGit(
                    "ls-files --others --exclude-standard");
                _changedFiles.AddRange(tracked.Concat(untracked)
                    .Select(NormalizePath)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));

                foreach (string path in _changedFiles)
                {
                    if (IsRemoteContent(path))
                        _contentChanges.Add(path);
                    else if (IsOutsidePlayerBuild(path))
                        _outsideChanges.Add(path);
                    else
                        _clientChanges.Add(path);
                }
                SetStatus(
                    _clientChanges.Count > 0
                        ? "A versão contém alterações do aplicativo: gere e " +
                          "hospede builds novas para Android e Windows."
                        : _contentChanges.Count > 0
                            ? "As alterações podem ser entregues como pacote " +
                              "de conteúdo dentro do jogo."
                            : "Nenhuma alteração publicável foi detectada.",
                    _clientChanges.Count > 0
                        ? MessageType.Warning
                        : MessageType.Info);
            }
            catch (Exception exception)
            {
                SetStatus(
                    "Não foi possível consultar o Git: " + exception.Message,
                    MessageType.Error);
            }
            Repaint();
        }

        private void ValidateAndReport()
        {
            string error = ValidatePublication();
            SetStatus(
                string.IsNullOrEmpty(error)
                    ? "PUBLICAÇÃO VÁLIDA • Android e Windows possuem rota de " +
                      "atualização e a entrada permanecerá bloqueada até a " +
                      "instalação."
                    : error,
                string.IsNullOrEmpty(error)
                    ? MessageType.Info
                    : MessageType.Error);
        }

        private string ValidatePublication()
        {
            if (_clientChanges.Count == 0 && _contentChanges.Count == 0)
                return "Não existem alterações do jogo para publicar.";
            if (!IsVersion(_latestClientVersion))
                return "Informe uma versão pública numérica, como 1.3.0.";
            if (_protocolVersion <= 0)
                return "O protocolo online deve ser maior que zero.";
            RemoteReleaseManifest publishedManifest =
                ReadProductionManifest();
            if (_clientChanges.Count > 0 &&
                RemoteUpdateRuntime.SemanticVersion.Compare(
                    _latestClientVersion,
                    publishedManifest?.latestClientVersion ?? "0.0.0") <= 0)
            {
                return "Mudanças de aplicativo exigem uma versão maior que " +
                       (publishedManifest?.latestClientVersion ?? "0.0.0") + ".";
            }
            if (_clientChanges.Count > 0 &&
                _androidVersionCode <=
                (publishedManifest?.android?.versionCode ?? 0))
            {
                return "O código Android deve ser maior que " +
                       (publishedManifest?.android?.versionCode ?? 0) + ".";
            }
            if (_clientChanges.Count > 0 &&
                !string.Equals(
                    PlayerSettings.bundleVersion,
                    _latestClientVersion.Trim(),
                    StringComparison.Ordinal))
                return "Aplique a versão às builds antes de construí-las.";
            if (_clientChanges.Count > 0 &&
                PlayerSettings.Android.bundleVersionCode != _androidVersionCode)
                return "Aplique o código Android às builds antes de construí-las.";
            if (_clientChanges.Count > 0 &&
                !IsDirectArtifactUrl(_windowsUrl, ".zip"))
                return "Informe o link HTTPS direto do ZIP para Windows.";
            if (_clientChanges.Count > 0 &&
                !IsDirectArtifactUrl(_androidUrl, ".apk"))
                return "Informe o link HTTPS direto do APK para Android.";
            if (_clientChanges.Count > 0 &&
                !File.Exists(ExpectedWindowsArtifactPath()))
                return "Construa o pacote Windows esperado antes de publicar.";
            if (_clientChanges.Count > 0 &&
                !File.Exists(ExpectedAndroidArtifactPath()))
                return "Construa o APK Android esperado antes de publicar.";
            if (_clientChanges.Count > 0 &&
                ReleaseSigningConfiguration.AndroidCertificateSha256().Length != 64)
                return "O certificado Android local não possui SHA-256 válido.";
            if (_contentChanges.Count > 0)
            {
                if (!IsVersion(_contentVersion))
                    return "Informe uma versão numérica para o conteúdo.";
                RemoteReleaseManifest current = ReadProductionManifest();
                string published = current?.contentVersion ?? "0.0.0";
                if (RemoteUpdateRuntime.SemanticVersion.Compare(
                        _contentVersion,
                        published) <= 0)
                {
                    return "A versão do conteúdo deve ser maior que " +
                           published + ".";
                }
                if (!IsWebUrl(_packageBaseUrl))
                    return "Informe o endereço HTTPS onde o pacote será hospedado.";
                string contentError = ValidateYgoSource();
                if (!string.IsNullOrEmpty(contentError))
                    return contentError;
            }
            return string.Empty;
        }

        private void GeneratePublication()
        {
            RefreshChangeClassification();
            string error = ValidatePublication();
            if (!string.IsNullOrEmpty(error))
            {
                SetStatus(error, MessageType.Error);
                return;
            }

            try
            {
                RemoteReleaseManifest previous = ReadProductionManifest();
                long sequence = Math.Max(
                    1,
                    (previous?.sequenceNumber ?? 0) + 1);
                string clientVersion = _latestClientVersion.Trim();
                string contentReleaseTag = ContentReleaseTag(
                    clientVersion,
                    sequence);
                RemoteContentPackage[] packages =
                    Array.Empty<RemoteContentPackage>();
                if (_contentChanges.Count > 0 && _clientChanges.Count == 0)
                {
                    packages = new[] { BuildYgoPackage(
                        ResolvePackageBaseUrl(contentReleaseTag),
                        _contentChanges) };
                }

                string effectiveContentVersion = _contentChanges.Count > 0
                    ? _contentVersion.Trim()
                    : previous?.contentVersion ?? "0.0.0";
                string timestamp = DateTime.UtcNow.ToString(
                    "yyyy-MM-ddTHH:mm:ssZ");
                string releaseId = "release-" +
                                   clientVersion.Replace('.', '-') + "-" +
                                   DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                string[] notes = (_releaseNotes ?? string.Empty)
                    .Split(new[] { '\r', '\n' },
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(note => note.Trim())
                    .Where(note => note.Length > 0)
                    .Distinct()
                    .ToArray();

                RemoteClientArtifact windowsArtifact =
                    _clientChanges.Count > 0
                        ? BuildClientArtifact(
                            "windows",
                            clientVersion,
                            0,
                            _windowsUrl,
                            ExpectedWindowsArtifactPath(),
                            "MasterDuel2PlusUltra.exe")
                        : previous?.windows ?? new RemoteClientArtifact();
                RemoteClientArtifact androidArtifact =
                    _clientChanges.Count > 0
                        ? BuildClientArtifact(
                            "android",
                            clientVersion,
                            _androidVersionCode,
                            _androidUrl,
                            ExpectedAndroidArtifactPath(),
                            string.Empty)
                        : previous?.android ?? new RemoteClientArtifact();
                if (_clientChanges.Count > 0)
                    androidArtifact.signingCertificateSha256 =
                        ReleaseSigningConfiguration.AndroidCertificateSha256();

                var envelope = new RemoteReleaseEnvelope
                {
                    schemaVersion = 2,
                    payload = new RemoteReleaseManifest
                    {
                        schemaVersion = 2,
                        releaseId = releaseId,
                        publishedUtc = timestamp,
                        sequenceNumber = sequence,
                        channel = "production",
                        expiresUtc = DateTime.UtcNow.AddDays(365).ToString(
                            "yyyy-MM-ddTHH:mm:ssZ"),
                        protocolVersion = _protocolVersion,
                        minimumClientVersion = clientVersion,
                        latestClientVersion = clientVersion,
                        requiredClientUpdate = true,
                        title = "ATUALIZAÇÃO DO MASTER DUEL 2 PLUS ULTRA",
                        summary = _clientChanges.Count > 0
                            ? "Nova versão disponível para entrar no jogo."
                            : "Conteúdo atualizado dentro do jogo.",
                        changes = notes,
                        windowsUpdateUrl = _windowsUrl.Trim(),
                        androidUpdateUrl = _androidUrl.Trim(),
                        fallbackUpdateUrl = string.IsNullOrWhiteSpace(_fallbackUrl)
                            ? _windowsUrl.Trim()
                            : _fallbackUrl.Trim(),
                        windows = windowsArtifact,
                        android = androidArtifact,
                        contentVersion = effectiveContentVersion,
                        requiredContentUpdate = true,
                        packages = packages
                    }
                };

                ReleaseSigningConfiguration.SignEnvelope(envelope);

                WriteTextAtomically(
                    ProductionEnvelopePath,
                    JsonUtility.ToJson(envelope, true) + Environment.NewLine);
                WriteTextAtomically(
                    BundledEnvelopePath,
                    JsonUtility.ToJson(envelope, true) + Environment.NewLine);
                EditorPrefs.SetString(
                    PackageUrlPreference,
                    _packageBaseUrl.TrimEnd('/'));
                AssetDatabase.Refresh();
                SetStatus(
                    _clientChanges.Count > 0
                        ? "PUBLICAÇÃO PREPARADA • envie o manifesto, o pacote " +
                          "e as builds ao GitHub."
                        : "PACOTE INCREMENTAL PREPARADO • publique o manifesto " +
                          "e o ZIP; o jogo baixará apenas as alterações.",
                    MessageType.Info);
                EditorUtility.RevealInFinder(ProductionEnvelopePath);
            }
            catch (Exception exception)
            {
                SetStatus(
                    "Falha ao preparar a publicação: " +
                    exception.GetBaseException().Message,
                    MessageType.Error);
            }
        }

        private void ApplyVersionToBuilds()
        {
            if (!IsVersion(_latestClientVersion) || _androidVersionCode <= 0)
            {
                SetStatus(
                    "Informe versão e código Android válidos.",
                    MessageType.Error);
                return;
            }
            PlayerSettings.bundleVersion = _latestClientVersion.Trim();
            PlayerSettings.Android.bundleVersionCode = _androidVersionCode;
            if (!IsDirectArtifactUrl(_windowsUrl, ".zip"))
                _windowsUrl = DefaultReleaseAssetUrl(
                    Path.GetFileName(ExpectedWindowsArtifactPath()));
            if (!IsDirectArtifactUrl(_androidUrl, ".apk"))
                _androidUrl = DefaultReleaseAssetUrl(
                    Path.GetFileName(ExpectedAndroidArtifactPath()));
            if (string.IsNullOrWhiteSpace(_fallbackUrl) ||
                _fallbackUrl.Contains("/releases/latest"))
            {
                _fallbackUrl =
                    "https://github.com/Thiago0012/CARD12/releases/tag/v" +
                    _latestClientVersion.Trim();
            }
            AssetDatabase.SaveAssets();
            SetStatus(
                "VERSÃO APLICADA • construa Android e Windows antes de gerar " +
                "o manifesto.",
                MessageType.Info);
        }

        private RemoteClientArtifact BuildClientArtifact(
            string platform,
            string version,
            int versionCode,
            string url,
            string localPath,
            string executableName)
        {
            var file = new FileInfo(localPath);
            return new RemoteClientArtifact
            {
                platform = platform,
                versionName = version,
                versionCode = versionCode,
                minimumVersionCode = versionCode,
                protocolVersion = _protocolVersion,
                url = url.Trim(),
                sizeBytes = file.Length,
                sha256 = ComputeSha256(file.FullName),
                executableName = executableName
            };
        }

        private RemoteContentPackage BuildYgoPackage(
            string packageBaseUrl,
            IEnumerable<string> changedFiles)
        {
            string packagesDirectory = Path.Combine(
                ProjectRoot,
                "ContentStaging",
                "production",
                "packages");
            Directory.CreateDirectory(packagesDirectory);
            string fileName = "ygo-patch-" + _contentVersion.Trim() + ".zip";
            string destination = Path.Combine(packagesDirectory, fileName);
            string temporary = destination + ".tmp";
            if (File.Exists(temporary))
                File.Delete(temporary);

            string sourceRoot = Path.Combine(
                ProjectRoot,
                "Assets",
                "StreamingAssets",
                "Ygo");
            var files = new List<string>();
            var deletedFiles = new List<string>();
            foreach (string changed in changedFiles ?? Array.Empty<string>())
            {
                string normalized = NormalizePath(changed);
                const string prefix = "Assets/StreamingAssets/Ygo/";
                if (!normalized.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string relative = normalized.Substring(prefix.Length).Trim('/');
                if (!IsSafePatchPath(relative))
                    throw new InvalidDataException(
                        "O patch contém um caminho de conteúdo inválido: " +
                        relative);
                string source = Path.Combine(
                    sourceRoot,
                    relative.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(source))
                    files.Add(relative);
                else
                    deletedFiles.Add(relative);
            }
            files = files.Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            deletedFiles = deletedFiles.Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(path => !files.Contains(path,
                    StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (files.Count == 0 && deletedFiles.Count == 0)
            {
                throw new InvalidDataException(
                    "Nenhuma alteração de conteúdo válida foi encontrada.");
            }

            using (var stream = new FileStream(
                       temporary,
                       FileMode.CreateNew,
                       FileAccess.ReadWrite,
                       FileShare.None))
            using (var zip = new ZipArchive(
                       stream,
                       ZipArchiveMode.Create,
                       false))
            {
                foreach (string relative in files)
                {
                    string source = Path.Combine(
                        sourceRoot,
                        relative.Replace('/', Path.DirectorySeparatorChar));
                    ZipArchiveEntry entry = zip.CreateEntry(
                        relative,
                        System.IO.Compression.CompressionLevel.Optimal);
                    using Stream input = File.OpenRead(source);
                    using Stream output = entry.Open();
                    input.CopyTo(output);
                }
                ZipArchiveEntry manifestEntry = zip.CreateEntry(
                    "patch-manifest.json",
                    System.IO.Compression.CompressionLevel.Optimal);
                using Stream manifestOutput = manifestEntry.Open();
                using var writer = new StreamWriter(manifestOutput);
                writer.Write(JsonUtility.ToJson(new YgoPatchArchiveManifest
                {
                    files = files.ToArray(),
                    deletedFiles = deletedFiles.ToArray()
                }, true));
            }
            if (File.Exists(destination))
                File.Delete(destination);
            File.Move(temporary, destination);

            return new RemoteContentPackage
            {
                packageId = "ygo-patch-" + _contentVersion.Trim(),
                version = _contentVersion.Trim(),
                platform = "any",
                target = "ygo-patch",
                url = packageBaseUrl.TrimEnd('/') + "/" + fileName,
                sizeBytes = new FileInfo(destination).Length,
                sha256 = ComputeSha256(destination)
            };
        }

        private static string ValidateYgoSource()
        {
            string root = Path.Combine(
                ProjectRoot,
                "Assets",
                "StreamingAssets",
                "Ygo");
            string[] essentials =
            {
                Path.Combine(root, "Data", "cards.bin"),
                Path.Combine(root, "Data", "card-texts.json"),
                Path.Combine(root, "Scripts", "constant.lua"),
                Path.Combine(root, "Scripts", "utility.lua"),
                Path.Combine(root, "Visual", "card-visuals.json")
            };
            string missing = essentials.FirstOrDefault(file => !File.Exists(file));
            return missing == null
                ? string.Empty
                : "O conteúdo YGO está incompleto. Arquivo ausente: " + missing;
        }

        private static IEnumerable<string> RunGit(string arguments)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "-C \"" + ProjectRoot + "\" " + arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit(10000);
            if (!process.HasExited)
            {
                process.Kill();
                throw new TimeoutException("O Git não respondeu em 10 segundos.");
            }
            if (process.ExitCode != 0)
                throw new InvalidOperationException(error.Trim());
            return output.Split(new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .ToArray();
        }

        private static bool IsRemoteContent(string path)
        {
            return path.StartsWith(
                "Assets/StreamingAssets/Ygo/",
                StringComparison.OrdinalIgnoreCase) &&
                   !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase);
        }

        private string ResolvePackageBaseUrl(string tag)
        {
            return (_packageBaseUrl ?? string.Empty).Trim()
                .Replace("{tag}", tag)
                .TrimEnd('/');
        }

        private static string ContentReleaseTag(
            string clientVersion,
            long sequence)
        {
            return "content-" + clientVersion.Trim() + "-s" + sequence;
        }

        private static bool IsSafePatchPath(string value)
        {
            string clean = (value ?? string.Empty).Replace('\\', '/').Trim('/');
            return clean.Length > 0 &&
                   !clean.Equals("patch-manifest.json",
                       StringComparison.OrdinalIgnoreCase) &&
                   clean.IndexOf("..", StringComparison.Ordinal) < 0 &&
                   !Path.IsPathRooted(clean) &&
                   clean.Split('/').All(segment =>
                       !string.IsNullOrWhiteSpace(segment));
        }

        private static bool IsOutsidePlayerBuild(string path)
        {
            return IgnoredReleasePrefixes.Any(prefix => path.StartsWith(
                       prefix,
                       StringComparison.OrdinalIgnoreCase)) ||
                   path.Equals(".gitignore", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            string core = value.Trim().Split('+')[0].Split('-')[0];
            string[] parts = core.Split('.');
            return parts.Length >= 2 && parts.Length <= 4 &&
                   parts.All(part => int.TryParse(part, out int number) &&
                                     number >= 0);
        }

        private static bool IsWebUrl(string value)
        {
            string candidate = (value ?? string.Empty).Trim().Replace(
                "{tag}",
                "release-test");
            return Uri.TryCreate(candidate, UriKind.Absolute, out Uri uri) &&
                   uri.Scheme == Uri.UriSchemeHttps;
        }

        private static bool IsDirectArtifactUrl(
            string value,
            string extension)
        {
            return IsWebUrl(value) &&
                   Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri uri) &&
                   uri.AbsolutePath.EndsWith(
                       extension,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeSha256(string path)
        {
            using Stream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static RemoteReleaseManifest ReadProductionManifest()
        {
            if (!File.Exists(ProductionEnvelopePath))
                return null;
            return JsonUtility.FromJson<RemoteReleaseEnvelope>(
                File.ReadAllText(ProductionEnvelopePath))?.payload;
        }

        private static void WriteTextAtomically(string path, string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ProjectRoot);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, contents);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temporary, path);
        }

        private void SetStatus(string value, MessageType type)
        {
            _status = value;
            _statusType = type;
            Repaint();
        }

        private static string EmptyFallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }

        private static string ProjectRoot =>
            Directory.GetParent(Application.dataPath)?.FullName ??
            Application.dataPath;

        private static string ProductionEnvelopePath => Path.Combine(
            ProjectRoot,
            "ContentStaging",
            "production",
            "v2",
            "release-envelope.json");

        private static string BundledEnvelopePath => Path.Combine(
            ProjectRoot,
            "Assets",
            "Resources",
            "RemoteUpdates",
            "BundledReleaseEnvelope.json");

        private string ExpectedWindowsArtifactPath()
        {
            return Path.Combine(
                ProjectRoot,
                "ContentStaging",
                "production",
                "artifacts",
                "MasterDuel2PlusUltra-Windows-v" +
                _latestClientVersion.Trim() + ".zip");
        }

        private string ExpectedAndroidArtifactPath()
        {
            return Path.Combine(
                ProjectRoot,
                "ContentStaging",
                "production",
                "artifacts",
                "MasterDuel2PlusUltra-Android-v" +
                _latestClientVersion.Trim() + ".apk");
        }

        private string DefaultReleaseAssetUrl(string fileName)
        {
            return "https://github.com/Thiago0012/CARD12/releases/download/v" +
                   _latestClientVersion.Trim() + "/" + fileName;
        }

        private static string PackageUrlPreference =>
            "MasterDuel2PlusUltra.RemoteUpdates.PackageBaseUrl." +
            Application.cloudProjectId;
    }
}
