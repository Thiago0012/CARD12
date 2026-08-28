package com.arcaneduel.updater;

import android.app.Activity;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.PackageInfo;
import android.content.pm.PackageInstaller;
import android.content.pm.PackageManager;
import android.content.pm.Signature;
import android.net.Uri;
import android.os.Build;
import android.os.StatFs;
import android.provider.Settings;

import java.io.File;
import java.io.FileInputStream;
import java.io.OutputStream;
import java.lang.ref.WeakReference;
import java.security.MessageDigest;
import java.util.Locale;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * Native bridge used by Unity's full-client Android updater.
 *
 * Android intentionally owns the final installation confirmation. The bridge
 * persists its PackageInstaller session so an interruption always becomes an
 * explicit recovery state instead of leaving Unity on a frozen 100% screen.
 */
public final class AndroidUpdateBridge {
    private static final String ACTION_INSTALL_STATUS =
        "com.arcaneduel.updater.INSTALL_STATUS";
    private static final String PREFERENCES = "master_duel_updater";
    private static final String KEY_STATE = "state";
    private static final String KEY_PROGRESS = "progress";
    private static final String KEY_MESSAGE = "message";
    private static final String KEY_TARGET_VERSION = "targetVersionCode";
    private static final String KEY_SESSION_ID = "sessionId";
    private static final String KEY_TARGET_PACKAGE = "targetPackage";
    private static final String KEY_STATE_UPDATED_AT = "stateUpdatedAt";
    private static final AtomicBoolean INSTALLING = new AtomicBoolean(false);
    private static volatile WeakReference<Activity> currentActivity =
        new WeakReference<>(null);
    // Android does not allow persisting this Intent. It can only be reused
    // while this process still exists; otherwise we safely restart the flow.
    private static volatile Intent pendingConfirmationIntent;

    private AndroidUpdateBridge() { }

    public static long getInstalledVersionCode(Activity activity) {
        rememberActivity(activity);
        return getInstalledVersionCode((Context) activity);
    }

    private static long getInstalledVersionCode(Context context) {
        try {
            PackageInfo info = context.getPackageManager().getPackageInfo(
                context.getPackageName(),
                0);
            return versionCode(info);
        } catch (Exception exception) {
            return 0L;
        }
    }

    public static boolean canRequestPackageInstalls(Activity activity) {
        rememberActivity(activity);
        return Build.VERSION.SDK_INT < Build.VERSION_CODES.O ||
            activity.getPackageManager().canRequestPackageInstalls();
    }

    /**
     * Returns the conservative free space available to the app's private
     * update locations. The Unity client uses it before downloading a full
     * APK, which avoids spending mobile data on a package Android cannot stage.
     */
    public static long getAvailableUpdateBytes(Activity activity) {
        rememberActivity(activity);
        try {
            long available = availableBytes(activity.getFilesDir());
            File external = activity.getExternalFilesDir(null);
            if (external != null) {
                long externalAvailable = availableBytes(external);
                if (externalAvailable > 0L) {
                    available = available <= 0L
                        ? externalAvailable
                        : Math.min(available, externalAvailable);
                }
            }
            return Math.max(0L, available);
        } catch (Exception ignored) {
            return 0L;
        }
    }

    public static void openInstallPermissionSettings(Activity activity) {
        rememberActivity(activity);
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
            return;
        }
        Intent intent = new Intent(
            Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
            Uri.parse("package:" + activity.getPackageName()));
        activity.startActivity(intent);
    }

    public static String installPackage(
        Activity activity,
        String apkPath,
        String expectedPackageName,
        String expectedCertificateSha256) {
        rememberActivity(activity);
        File apk = new File(apkPath);
        if (!apk.isFile() || apk.length() <= 0L) {
            return "ERROR: APK validado não encontrado.";
        }
        if (!canRequestPackageInstalls(activity)) {
            return "ERROR: A instalação desta fonte ainda não foi autorizada.";
        }

        Context context = activity.getApplicationContext();
        reconcileCompletedInstall(context);
        String currentState = preferences(context).getString(KEY_STATE, "IDLE");
        if (isActiveState(currentState)) {
            return "INSTALL_PREPARING";
        }
        if (!INSTALLING.compareAndSet(false, true)) {
            return "INSTALL_PREPARING";
        }

        pendingConfirmationIntent = null;
        clearTrackedSession(context);
        writeState(context, "PREPARING", 0f, "Validando o APK.");
        Thread worker = new Thread(() -> {
            boolean committed = false;
            try {
                committed = installPackageInBackground(
                    context,
                    apk,
                    expectedPackageName,
                    expectedCertificateSha256);
            } catch (Exception exception) {
                failInstall(
                    context,
                    exception.getClass().getSimpleName() + ": " +
                        String.valueOf(exception.getMessage()));
            } finally {
                // A committed PackageInstaller session remains active until
                // its receiver reports SUCCESS/FAILED or an explicit recovery
                // cancels it. Releasing this too early caused duplicate flows.
                if (!committed) {
                    INSTALLING.set(false);
                }
            }
        }, "MasterDuel2PlusUltra-Installer");
        worker.start();
        return "INSTALL_PREPARING";
    }

    public static String getInstallState(Activity activity) {
        rememberActivity(activity);
        Context context = activity.getApplicationContext();
        reconcileCompletedInstall(context);
        return preferences(context).getString(KEY_STATE, "IDLE");
    }

    public static float getInstallProgress(Activity activity) {
        rememberActivity(activity);
        return preferences(activity).getFloat(KEY_PROGRESS, 0f);
    }

    public static String getInstallMessage(Activity activity) {
        rememberActivity(activity);
        return preferences(activity).getString(KEY_MESSAGE, "");
    }

    /** Called by Unity before a fresh retry after Android lost a confirmation. */
    public static boolean cancelPendingInstall(Activity activity) {
        rememberActivity(activity);
        Context context = activity.getApplicationContext();
        boolean cancelled = abandonTrackedSession(context);
        pendingConfirmationIntent = null;
        INSTALLING.set(false);
        writeState(
            context,
            "CANCELLED",
            0f,
            cancelled
                ? "A instalação pendente foi cancelada com segurança."
                : "Não havia instalação pendente para cancelar.");
        return cancelled;
    }

    public static boolean reopenPendingUserAction(Activity activity) {
        rememberActivity(activity);
        Context context = activity.getApplicationContext();
        reconcileCompletedInstall(context);
        if (!"AWAITING_CONFIRMATION".equals(
                preferences(context).getString(KEY_STATE, "IDLE"))) {
            return false;
        }

        Intent confirmation = pendingConfirmationIntent;
        if (confirmation == null) {
            abandonTrackedSession(context);
            pendingConfirmationIntent = null;
            INSTALLING.set(false);
            writeState(
                context,
                "RECOVERY_REQUIRED",
                0f,
                "O Android fechou a confirmação anterior. Toque em " +
                    "reiniciar instalação para criar uma sessão verificada.");
            return false;
        }
        try {
            activity.startActivity(confirmation);
            return true;
        } catch (Exception exception) {
            writeState(
                context,
                "AWAITING_CONFIRMATION",
                1f,
                "Não foi possível reabrir a confirmação: " +
                    String.valueOf(exception.getMessage()));
            return false;
        }
    }

    static void handlePendingUserAction(Context context, Intent confirmation) {
        pendingConfirmationIntent = confirmation;
        writeState(
            context,
            "AWAITING_CONFIRMATION",
            1f,
            "Confirme a atualização no Android.");
        Activity activity = currentActivity.get();
        try {
            if (activity != null && !activity.isFinishing()) {
                activity.runOnUiThread(() -> activity.startActivity(confirmation));
            } else {
                confirmation.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                context.startActivity(confirmation);
            }
        } catch (Exception exception) {
            writeState(
                context,
                "AWAITING_CONFIRMATION",
                1f,
                "Toque em abrir instalador: " +
                    String.valueOf(exception.getMessage()));
        }
    }

    static void completeInstall(Context context, String message) {
        pendingConfirmationIntent = null;
        INSTALLING.set(false);
        writeState(
            context,
            "SUCCESS",
            1f,
            message == null ? "Atualização instalada." : message);
        clearTrackedSession(context);
    }

    static void failInstall(Context context, String message) {
        pendingConfirmationIntent = null;
        INSTALLING.set(false);
        writeState(
            context,
            "FAILED",
            0f,
            message == null ? "O Android recusou a instalação." : message);
        clearTrackedSession(context);
    }

    static void writeState(
        Context context,
        String state,
        float progress,
        String message) {
        preferences(context).edit()
            .putString(KEY_STATE, state == null ? "IDLE" : state)
            .putFloat(KEY_PROGRESS, Math.max(0f, Math.min(1f, progress)))
            .putString(KEY_MESSAGE, message == null ? "" : message)
            .putLong(KEY_STATE_UPDATED_AT, System.currentTimeMillis())
            .apply();
    }

    private static void writeTrackedState(
        Context context,
        String state,
        float progress,
        String message,
        int sessionId,
        long targetVersion,
        String packageName) {
        preferences(context).edit()
            .putString(KEY_STATE, state == null ? "IDLE" : state)
            .putFloat(KEY_PROGRESS, Math.max(0f, Math.min(1f, progress)))
            .putString(KEY_MESSAGE, message == null ? "" : message)
            .putLong(KEY_STATE_UPDATED_AT, System.currentTimeMillis())
            .putInt(KEY_SESSION_ID, sessionId)
            .putLong(KEY_TARGET_VERSION, targetVersion)
            .putString(KEY_TARGET_PACKAGE, packageName == null ? "" : packageName)
            .apply();
    }

    private static void reconcileCompletedInstall(Context context) {
        SharedPreferences preferences = preferences(context);
        long target = preferences.getLong(KEY_TARGET_VERSION, 0L);
        String state = preferences.getString(KEY_STATE, "IDLE");
        if (target > 0L && getInstalledVersionCode(context) >= target &&
            !"SUCCESS".equals(state)) {
            completeInstall(context, "Atualização instalada. Reabra o jogo.");
            return;
        }

        if (!isActiveState(state)) {
            return;
        }
        int sessionId = preferences.getInt(KEY_SESSION_ID, -1);
        if (sessionId < 0) {
            long updatedAt = preferences.getLong(KEY_STATE_UPDATED_AT, 0L);
            if ("PREPARING".equals(state) &&
                System.currentTimeMillis() - updatedAt < 90000L) {
                return;
            }
            failInstall(
                context,
                "A sessão de instalação foi perdida. Toque em tentar " +
                    "novamente para baixar e validar o pacote de novo.");
            return;
        }

        try {
            PackageInstaller installer = context.getPackageManager()
                .getPackageInstaller();
            PackageInstaller.SessionInfo session = installer.getSessionInfo(sessionId);
            if (session == null) {
                long updatedAt = preferences.getLong(KEY_STATE_UPDATED_AT, 0L);
                // PackageInstaller can briefly hide a committed session before
                // its broadcast reaches us. Do not turn that normal hand-off
                // into a false failure; only recover after a real timeout.
                if (System.currentTimeMillis() - updatedAt < 90000L) {
                    return;
                }
                failInstall(
                    context,
                    "O Android encerrou a sessão de instalação antes da " +
                        "confirmação. Reinicie a instalação.");
                return;
            }

            // The confirmation Intent cannot survive a process recreation.
            // Retrying through a fresh verified session is the only supported
            // Android path; leaving the stale session would freeze the UI.
            if ("AWAITING_CONFIRMATION".equals(state) &&
                pendingConfirmationIntent == null) {
                try {
                    installer.abandonSession(sessionId);
                } catch (Exception ignored) { }
                pendingConfirmationIntent = null;
                INSTALLING.set(false);
                clearTrackedSession(context);
                writeState(
                    context,
                    "RECOVERY_REQUIRED",
                    0f,
                    "A confirmação do Android não pode ser retomada após " +
                        "o fechamento do aplicativo. Reinicie a instalação.");
            }
        } catch (Exception exception) {
            // A query failure must be visible and retryable; it must never
            // keep the main game behind a permanent loading overlay.
            failInstall(
                context,
                "Não foi possível verificar a sessão do Android: " +
                    String.valueOf(exception.getMessage()));
        }
    }

    private static boolean isActiveState(String state) {
        return "PREPARING".equals(state) ||
            "COPYING".equals(state) ||
            "COMMITTING".equals(state) ||
            "COMMITTED".equals(state) ||
            "AWAITING_CONFIRMATION".equals(state);
    }

    private static boolean abandonTrackedSession(Context context) {
        SharedPreferences preferences = preferences(context);
        int sessionId = preferences.getInt(KEY_SESSION_ID, -1);
        if (sessionId < 0) {
            clearTrackedSession(context);
            return false;
        }
        try {
            context.getPackageManager().getPackageInstaller()
                .abandonSession(sessionId);
        } catch (Exception ignored) {
            // The session can already have been closed by Android. Its local
            // state is still removed so the player gets a clean retry.
        }
        clearTrackedSession(context);
        return true;
    }

    private static void clearTrackedSession(Context context) {
        preferences(context).edit()
            .remove(KEY_SESSION_ID)
            .remove(KEY_TARGET_VERSION)
            .remove(KEY_TARGET_PACKAGE)
            .apply();
    }

    private static void deleteManagedDownload(Context context, File apk) {
        try {
            String candidate = apk.getCanonicalPath();
            File externalFiles = context.getExternalFilesDir(null);
            File internalFiles = context.getFilesDir();
            boolean inExternal = externalFiles != null && candidate.startsWith(
                externalFiles.getCanonicalPath() + File.separator);
            boolean inInternal = internalFiles != null && candidate.startsWith(
                internalFiles.getCanonicalPath() + File.separator);
            if ((inExternal || inInternal) && apk.isFile() && !apk.delete()) {
                writeState(
                    context,
                    "COMMITTING",
                    1f,
                    "Instalador preparado. O Android limpará o arquivo temporário.");
            }
        } catch (Exception ignored) {
            // A failed cleanup must never cancel a verified installation.
        }
    }

    private static boolean installPackageInBackground(
        Context context,
        File apk,
        String expectedPackageName,
        String expectedCertificateSha256) throws Exception {
        PackageInstaller.Session session = null;
        int sessionId = -1;
        try {
            PackageManager manager = context.getPackageManager();
            PackageInfo archive = readArchiveInfo(manager, apk.getAbsolutePath());
            if (archive == null || archive.packageName == null) {
                throw new IllegalArgumentException(
                    "O Android não reconheceu o APK baixado.");
            }
            if (!archive.packageName.equals(expectedPackageName)) {
                throw new SecurityException("O APK pertence a outro aplicativo.");
            }
            long candidateVersion = versionCode(archive);
            long installedVersion = getInstalledVersionCode(context);
            if (candidateVersion <= installedVersion) {
                throw new IllegalArgumentException(
                    "O APK não possui versionCode superior ao instalado.");
            }

            String expectedCertificate = normalizeHash(expectedCertificateSha256);
            if (!expectedCertificate.isEmpty()) {
                String actualCertificate = firstCertificateSha256(archive);
                if (!expectedCertificate.equals(actualCertificate)) {
                    throw new SecurityException(
                        "O certificado do APK não corresponde ao publicado.");
                }
            }

            PackageInstaller installer = manager.getPackageInstaller();
            abandonStaleSessions(installer, expectedPackageName);
            PackageInstaller.SessionParams params =
                new PackageInstaller.SessionParams(
                    PackageInstaller.SessionParams.MODE_FULL_INSTALL);
            params.setAppPackageName(expectedPackageName);
            params.setSize(apk.length());
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                params.setRequireUserAction(
                    PackageInstaller.SessionParams.USER_ACTION_REQUIRED);
            }
            sessionId = installer.createSession(params);
            writeTrackedState(
                context,
                "COPYING",
                0f,
                "Preparando instalação.",
                sessionId,
                candidateVersion,
                expectedPackageName);
            session = installer.openSession(sessionId);
            try (FileInputStream input = new FileInputStream(apk);
                 OutputStream output = session.openWrite(
                     "MasterDuel2PlusUltra.apk",
                     0,
                     apk.length())) {
                byte[] buffer = new byte[1024 * 1024];
                int read;
                long copied = 0L;
                while ((read = input.read(buffer)) >= 0) {
                    if (read > 0) {
                        output.write(buffer, 0, read);
                        copied += read;
                        if ((copied & ((8L * 1024L * 1024L) - 1L)) < read) {
                            float progress = Math.min(
                                0.99f,
                                copied / (float) apk.length());
                            session.setStagingProgress(progress);
                            writeState(
                                context,
                                "COPYING",
                                progress,
                                "Preparando instalação.");
                        }
                    }
                }
                session.fsync(output);
            }

            // PackageInstaller now owns an independent copy. Delete Unity's
            // temporary artifact before asking Android for confirmation.
            deleteManagedDownload(context, apk);
            writeState(context, "COMMITTING", 1f, "Abrindo o instalador.");
            Intent callback = new Intent(context, UpdateInstallReceiver.class);
            callback.setAction(ACTION_INSTALL_STATUS);
            callback.putExtra("sessionId", sessionId);
            int flags = PendingIntent.FLAG_UPDATE_CURRENT;
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                flags |= PendingIntent.FLAG_MUTABLE;
            }
            PendingIntent pending = PendingIntent.getBroadcast(
                context,
                sessionId,
                callback,
                flags);
            writeState(
                context,
                "COMMITTED",
                1f,
                "Aguardando a confirmação do Android.");
            session.commit(pending.getIntentSender());
            session.close();
            session = null;
            return true;
        } catch (Exception exception) {
            if (session != null) {
                try { session.abandon(); } catch (Exception ignored) { }
                try { session.close(); } catch (Exception ignored) { }
            } else if (sessionId >= 0) {
                try {
                    context.getPackageManager().getPackageInstaller()
                        .abandonSession(sessionId);
                } catch (Exception ignored) { }
            }
            clearTrackedSession(context);
            throw exception;
        }
    }

    private static void abandonStaleSessions(
        PackageInstaller installer,
        String expectedPackageName) {
        try {
            for (PackageInstaller.SessionInfo info : installer.getMySessions()) {
                if (expectedPackageName.equals(info.getAppPackageName())) {
                    try { installer.abandonSession(info.getSessionId()); }
                    catch (Exception ignored) { }
                }
            }
        } catch (Exception ignored) { }
    }

    private static void rememberActivity(Activity activity) {
        if (activity != null) {
            currentActivity = new WeakReference<>(activity);
        }
    }

    private static SharedPreferences preferences(Context context) {
        return context.getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE);
    }

    private static long availableBytes(File directory) {
        if (directory == null) {
            return 0L;
        }
        StatFs stat = new StatFs(directory.getAbsolutePath());
        return stat.getAvailableBytes();
    }

    private static PackageInfo readArchiveInfo(
        PackageManager manager,
        String apkPath) {
        int flags = Build.VERSION.SDK_INT >= Build.VERSION_CODES.P
            ? PackageManager.GET_SIGNING_CERTIFICATES
            : PackageManager.GET_SIGNATURES;
        return manager.getPackageArchiveInfo(apkPath, flags);
    }

    private static long versionCode(PackageInfo info) {
        return Build.VERSION.SDK_INT >= Build.VERSION_CODES.P
            ? info.getLongVersionCode()
            : (long) info.versionCode;
    }

    private static String firstCertificateSha256(PackageInfo info)
        throws Exception {
        Signature[] signatures;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P &&
            info.signingInfo != null) {
            signatures = info.signingInfo.hasMultipleSigners()
                ? info.signingInfo.getApkContentsSigners()
                : info.signingInfo.getSigningCertificateHistory();
        } else {
            signatures = info.signatures;
        }
        if (signatures == null || signatures.length == 0) {
            return "";
        }
        MessageDigest digest = MessageDigest.getInstance("SHA-256");
        byte[] bytes = digest.digest(signatures[0].toByteArray());
        StringBuilder result = new StringBuilder(bytes.length * 2);
        for (byte value : bytes) {
            result.append(String.format(Locale.US, "%02x", value & 0xff));
        }
        return result.toString();
    }

    private static String normalizeHash(String value) {
        return value == null
            ? ""
            : value.replace(":", "")
                .replace("-", "")
                .trim()
                .toLowerCase(Locale.US);
    }
}
