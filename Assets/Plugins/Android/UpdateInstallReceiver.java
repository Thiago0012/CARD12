package com.arcaneduel.updater;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageInstaller;
import android.os.Build;
import android.util.Log;

/** Receives PackageInstaller status and opens Android's confirmation screen. */
public final class UpdateInstallReceiver extends BroadcastReceiver {
    private static final String TAG = "MasterDuelUpdater";

    @Override
    public void onReceive(Context context, Intent intent) {
        int status = intent.getIntExtra(
            PackageInstaller.EXTRA_STATUS,
            PackageInstaller.STATUS_FAILURE);
        String message = intent.getStringExtra(
            PackageInstaller.EXTRA_STATUS_MESSAGE);
        if (status == PackageInstaller.STATUS_PENDING_USER_ACTION) {
            Intent confirmation = Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU
                ? intent.getParcelableExtra(Intent.EXTRA_INTENT, Intent.class)
                : intent.getParcelableExtra(Intent.EXTRA_INTENT);
            if (confirmation != null) {
                AndroidUpdateBridge.handlePendingUserAction(
                    context,
                    confirmation);
            } else {
                AndroidUpdateBridge.writeState(
                    context,
                    "FAILED",
                    1f,
                    "O Android não forneceu a tela de confirmação.");
                Log.e(TAG, "PackageInstaller não forneceu a confirmação.");
            }
            return;
        }
        if (status == PackageInstaller.STATUS_SUCCESS) {
            AndroidUpdateBridge.writeState(
                context,
                "SUCCESS",
                1f,
                "Atualização instalada.");
            Log.i(TAG, "Atualização instalada com sucesso.");
        } else {
            AndroidUpdateBridge.writeState(
                context,
                "FAILED",
                1f,
                message == null
                    ? "O Android recusou a instalação (" + status + ")."
                    : message);
            Log.e(TAG, "Falha na instalação: status=" + status +
                ", message=" + message);
        }
    }
}
