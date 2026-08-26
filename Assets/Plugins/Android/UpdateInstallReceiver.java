package com.arcaneduel.updater;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageInstaller;
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
        context.getSharedPreferences("master_duel_updater", Context.MODE_PRIVATE)
            .edit()
            .putInt("last_status", status)
            .putString("last_message", message == null ? "" : message)
            .apply();

        if (status == PackageInstaller.STATUS_PENDING_USER_ACTION) {
            Intent confirmation = intent.getParcelableExtra(Intent.EXTRA_INTENT);
            if (confirmation != null) {
                confirmation.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                context.startActivity(confirmation);
            } else {
                Log.e(TAG, "PackageInstaller não forneceu a confirmação.");
            }
            return;
        }
        if (status == PackageInstaller.STATUS_SUCCESS) {
            Log.i(TAG, "Atualização instalada com sucesso.");
        } else {
            Log.e(TAG, "Falha na instalação: status=" + status +
                ", message=" + message);
        }
    }
}
