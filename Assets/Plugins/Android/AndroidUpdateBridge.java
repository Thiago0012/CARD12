package com.arcaneduel.updater;

import android.app.Activity;
import android.app.PendingIntent;
import android.content.Intent;
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
import java.security.MessageDigest;
import java.util.Locale;

/** Native bridge used by Unity's full-client Android updater. */
public final class AndroidUpdateBridge {
    private static final String ACTION_INSTALL_STATUS =
        "com.arcaneduel.updater.INSTALL_STATUS";

    private AndroidUpdateBridge() { }

    public static long getInstalledVersionCode(Activity activity) {
        try {
            PackageInfo info = activity.getPackageManager().getPackageInfo(
                activity.getPackageName(),
                0);
            return versionCode(info);
        } catch (Exception exception) {
            return 0L;
        }
    }

    public static boolean canRequestPackageInstalls(Activity activity) {
        return Build.VERSION.SDK_INT < Build.VERSION_CODES.O ||
            activity.getPackageManager().canRequestPackageInstalls();
    }

    public static void openInstallPermissionSettings(Activity activity) {
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
        PackageInstaller.Session session = null;
        int sessionId = -1;
        try {
            File apk = new File(apkPath);
            if (!apk.isFile() || apk.length() <= 0L) {
                return "ERROR: APK validado não encontrado.";
            }
            if (!canRequestPackageInstalls(activity)) {
                return "ERROR: A instalação desta fonte ainda não foi autorizada.";
            }

            PackageManager manager = activity.getPackageManager();
            PackageInfo archive = readArchiveInfo(manager, apkPath);
            if (archive == null || archive.packageName == null) {
                return "ERROR: O Android não reconheceu o APK baixado.";
            }
            if (!archive.packageName.equals(expectedPackageName)) {
                return "ERROR: O APK pertence a outro aplicativo.";
            }
            long candidateVersion = versionCode(archive);
            long installedVersion = getInstalledVersionCode(activity);
            if (candidateVersion <= installedVersion) {
                return "ERROR: O APK não possui versionCode superior ao instalado.";
            }

            String expectedCertificate = normalizeHash(expectedCertificateSha256);
            if (!expectedCertificate.isEmpty()) {
                String actualCertificate = firstCertificateSha256(archive);
                if (!expectedCertificate.equals(actualCertificate)) {
                    return "ERROR: O certificado do APK não corresponde ao publicado.";
                }
            }

            PackageInstaller installer = manager.getPackageInstaller();
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
            try (FileInputStream input = new FileInputStream(apk);
                 OutputStream output = session.openWrite(
                     "MasterDuel2PlusUltra.apk",
                     0,
                     apk.length())) {
                byte[] buffer = new byte[1024 * 1024];
                int read;
                while ((read = input.read(buffer)) >= 0) {
                    if (read > 0) {
                        output.write(buffer, 0, read);
                    }
                }
                session.fsync(output);
            }

            Intent callback = new Intent(activity, UpdateInstallReceiver.class);
            callback.setAction(ACTION_INSTALL_STATUS);
            callback.putExtra("sessionId", sessionId);
            int flags = PendingIntent.FLAG_UPDATE_CURRENT;
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                flags |= PendingIntent.FLAG_MUTABLE;
            }
            PendingIntent pending = PendingIntent.getBroadcast(
                activity,
                sessionId,
                callback,
                flags);
            session.commit(pending.getIntentSender());
            session.close();
            return "INSTALL_STARTED:" + sessionId;
        } catch (Exception exception) {
            if (session != null) {
                try {
                    session.abandon();
                } catch (Exception ignored) { }
                try {
                    session.close();
                } catch (Exception ignored) { }
            }
            return "ERROR: " + exception.getClass().getSimpleName() +
                ": " + String.valueOf(exception.getMessage());
        }
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
