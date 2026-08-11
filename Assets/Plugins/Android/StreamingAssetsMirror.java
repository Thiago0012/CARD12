package com.arcaneduel.content;

import android.content.Context;
import android.content.res.AssetManager;

import java.io.BufferedInputStream;
import java.io.BufferedOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;

/**
 * Copies Unity StreamingAssets with Java-owned byte buffers. Passing a C#
 * byte[] through AndroidJavaObject does not guarantee that InputStream.read
 * mutations are copied back to the managed array on every Unity/Android
 * combination, which can silently corrupt binary databases.
 */
public final class StreamingAssetsMirror {
    private static final int BUFFER_SIZE = 64 * 1024;

    private StreamingAssetsMirror() {
    }

    public static long copyDirectory(
            Context context,
            String assetDirectory,
            String destinationDirectory) throws IOException {
        if (context == null) {
            throw new IOException("Android context is unavailable.");
        }

        AssetManager assets = context.getAssets();
        File destination = new File(destinationDirectory);
        ensureDirectory(destination);
        return copyDirectoryRecursive(assets, assetDirectory, destination);
    }

    public static long copyFile(
            Context context,
            String assetPath,
            String destinationPath) throws IOException {
        if (context == null) {
            throw new IOException("Android context is unavailable.");
        }
        return copyAssetFile(context.getAssets(), assetPath, new File(destinationPath));
    }

    private static long copyDirectoryRecursive(
            AssetManager assets,
            String assetDirectory,
            File destinationDirectory) throws IOException {
        ensureDirectory(destinationDirectory);
        String[] children = assets.list(assetDirectory);
        if (children == null) {
            throw new IOException("Unable to list packaged asset directory: " + assetDirectory);
        }

        long totalBytes = 0L;
        for (String child : children) {
            String childAsset = assetDirectory + "/" + child;
            File childDestination = new File(destinationDirectory, child);
            String[] nested = assets.list(childAsset);
            if (nested != null && nested.length > 0) {
                totalBytes += copyDirectoryRecursive(
                        assets,
                        childAsset,
                        childDestination);
            } else {
                totalBytes += copyAssetFile(assets, childAsset, childDestination);
            }
        }
        return totalBytes;
    }

    private static long copyAssetFile(
            AssetManager assets,
            String assetPath,
            File destination) throws IOException {
        File parent = destination.getParentFile();
        if (parent != null) {
            ensureDirectory(parent);
        }

        long written = 0L;
        byte[] buffer = new byte[BUFFER_SIZE];
        try (InputStream input = new BufferedInputStream(assets.open(assetPath));
             FileOutputStream fileOutput = new FileOutputStream(destination, false);
             OutputStream output = new BufferedOutputStream(fileOutput)) {
            int read;
            while ((read = input.read(buffer)) != -1) {
                output.write(buffer, 0, read);
                written += read;
            }
            output.flush();
            fileOutput.getFD().sync();
        }
        return written;
    }

    private static void ensureDirectory(File directory) throws IOException {
        if (!directory.isDirectory() && !directory.mkdirs() && !directory.isDirectory()) {
            throw new IOException("Unable to create destination directory: " + directory);
        }
    }
}
