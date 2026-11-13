package com.aidrum.candyflick;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.ContentValues;
import android.net.Uri;
import android.os.Environment;
import android.provider.MediaStore;
import java.io.FileInputStream;
import java.io.InputStream;
import java.io.OutputStream;

/**
 * Android replacement for 2.6 NativeLibs.SaveScreenshotToCameraRoll. Copies the PNG written by
 * ScreenCapture into the shared Pictures collection through MediaStore (API 29+, no permission).
 */
public final class ScreenshotSaver {
    private ScreenshotSaver() {}

    public static boolean save(Activity owner, String path, String name) {
        ContentResolver resolver = owner.getContentResolver();
        ContentValues values = new ContentValues();
        values.put(MediaStore.Images.Media.DISPLAY_NAME, name);
        values.put(MediaStore.Images.Media.MIME_TYPE, "image/png");
        values.put(MediaStore.Images.Media.RELATIVE_PATH, Environment.DIRECTORY_PICTURES + "/Candy Flick");
        values.put(MediaStore.Images.Media.IS_PENDING, 1);
        Uri uri = resolver.insert(MediaStore.Images.Media.EXTERNAL_CONTENT_URI, values);
        if (uri == null) return false;
        try (InputStream source = new FileInputStream(path);
             OutputStream destination = resolver.openOutputStream(uri)) {
            byte[] buffer = new byte[64 * 1024];
            int read;
            while ((read = source.read(buffer)) > 0) destination.write(buffer, 0, read);
        } catch (Exception error) {
            resolver.delete(uri, null, null);
            return false;
        }
        values.clear();
        values.put(MediaStore.Images.Media.IS_PENDING, 0);
        resolver.update(uri, values, null, null);
        return true;
    }
}
