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
import android.provider.Settings;

import java.io.File;
import java.io.FileInputStream;
import java.io.OutputStream;
import java.lang.ref.WeakReference;
import java.security.MessageDigest;
import java.util.Locale;
import java.util.concurrent.atomic.AtomicBoolean;

/** Native bridge used by Unity's full-client Android updater. */
public final class AndroidUpdateBridge {
    private static final String ACTION_INSTALL_STATUS =
        "com.arcaneduel.updater.INSTALL_STATUS";
    private static final String PREFERENCES = "master_duel_updater";
    private static final AtomicBoolean INSTALLING = new AtomicBoolean(false);
    private static volatile WeakReference<Activity> currentActivity =
        new WeakReference<>(null);
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
        if (!INSTALLING.compareAndSet(false, true)) {
            return "INSTALL_PREPARING";
        }

        Context context = activity.getApplicationContext();
        pendingConfirmationIntent = null;
        writeState(context, "PREPARING", 0f, "Validando o APK.");
        Thread worker = new Thread(() -> {
            try {
                installPackageInBackground(
                    context,
                    apk,
                    expectedPackageName,
                    expectedCertificateSha256);
            } catch (Exception exception) {
                writeState(
                    context,
                    "FAILED",
                    0f,
                    exception.getClass().getSimpleName() + ": " +
                        String.valueOf(exception.getMessage()));
            } finally {
                INSTALLING.set(false);
            }
        }, "MasterDuel2PlusUltra-Installer");
        worker.start();
        return "INSTALL_PREPARING";
    }

    public static String getInstallState(Activity activity) {
        rememberActivity(activity);
        return preferences(activity).getString("state", "IDLE");
    }

    public static float getInstallProgress(Activity activity) {
        rememberActivity(activity);
        return preferences(activity).getFloat("progress", 0f);
    }

    public static String getInstallMessage(Activity activity) {
        rememberActivity(activity);
        return preferences(activity).getString("message", "");
    }

    public static boolean reopenPendingUserAction(Activity activity) {
        rememberActivity(activity);
        Intent confirmation = pendingConfirmationIntent;
        if (confirmation == null) {
            return false;
        }
        try {
            activity.startActivity(confirmation);
            return true;
        } catch (Exception exception) {
            writeState(
                activity,
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

    static void writeState(
        Context context,
        String state,
        float progress,
        String message) {
        preferences(context).edit()
            .putString("state", state == null ? "IDLE" : state)
            .putFloat("progress", Math.max(0f, Math.min(1f, progress)))
            .putString("message", message == null ? "" : message)
            .apply();
    }

    private static void installPackageInBackground(
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
                throw new SecurityException(
                    "O APK pertence a outro aplicativo.");
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
            session = installer.openSession(sessionId);
            writeState(context, "COPYING", 0f, "Preparando instalação.");
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
        } catch (Exception exception) {
            if (session != null) {
                try {
                    session.abandon();
                } catch (Exception ignored) { }
                try {
                    session.close();
                } catch (Exception ignored) { }
            }
            throw exception;
        }
    }

    private static void abandonStaleSessions(
        PackageInstaller installer,
        String expectedPackageName) {
        try {
            for (PackageInstaller.SessionInfo info : installer.getMySessions()) {
                if (expectedPackageName.equals(info.getAppPackageName())) {
                    try {
                        installer.abandonSession(info.getSessionId());
                    } catch (Exception ignored) { }
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
