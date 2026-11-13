package com.aidrum.candyflick;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;
import android.widget.Toast;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Paint;
import android.graphics.RectF;
import android.graphics.pdf.PdfDocument;
import android.os.CancellationSignal;
import android.os.ParcelFileDescriptor;
import android.print.PageRange;
import android.print.PrintAttributes;
import android.print.PrintDocumentAdapter;
import android.print.PrintDocumentInfo;
import android.print.PrintManager;
import android.print.pdf.PrintedPdfDocument;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;

/** Android replacement for the original iOS save-image action. No storage permission. */
public final class MarkerExportActivity extends Activity {
    public static void save(Activity owner) {
        owner.startActivity(new Intent(owner, MarkerExportActivity.class));
    }
    public static void print(Activity owner) {
        owner.startActivity(new Intent(owner, MarkerExportActivity.class).putExtra("print", true));
    }
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        if (getIntent().getBooleanExtra("print", false)) { if (state == null) startPrint(); return; }
        if (state != null) return;
        Intent intent = new Intent(Intent.ACTION_CREATE_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("image/png");
        intent.putExtra(Intent.EXTRA_TITLE, "candy-flick-marker.png");
        startActivityForResult(intent, 26);
    }
    private void startPrint() {
        PrintManager manager = (PrintManager) getSystemService(PRINT_SERVICE);
        PrintAttributes defaults = new PrintAttributes.Builder()
            .setMediaSize(PrintAttributes.MediaSize.ISO_A4)
            .setColorMode(PrintAttributes.COLOR_MODE_COLOR).build();
        manager.print("Candy Flick target card", new PrintDocumentAdapter() {
            private PrintAttributes attributes;
            @Override public void onLayout(PrintAttributes oldAttributes, PrintAttributes newAttributes,
                CancellationSignal cancellation, LayoutResultCallback callback, Bundle extras) {
                if (cancellation.isCanceled()) { callback.onLayoutCancelled(); return; }
                attributes = newAttributes;
                callback.onLayoutFinished(new PrintDocumentInfo.Builder("candy-flick-marker.pdf")
                    .setContentType(PrintDocumentInfo.CONTENT_TYPE_DOCUMENT).setPageCount(1).build(),
                    !newAttributes.equals(oldAttributes));
            }
            @Override public void onWrite(PageRange[] ranges, ParcelFileDescriptor output,
                CancellationSignal cancellation, WriteResultCallback callback) {
                if (cancellation.isCanceled()) { callback.onWriteCancelled(); return; }
                PrintedPdfDocument document = new PrintedPdfDocument(MarkerExportActivity.this, attributes);
                try (InputStream source = getAssets().open("Raw/marker.png");
                     FileOutputStream destination = new FileOutputStream(output.getFileDescriptor())) {
                    Bitmap bitmap = BitmapFactory.decodeStream(source);
                    PdfDocument.Page page = document.startPage(0);
                    // Native target is 200 units: print the unmodified image 200 mm wide.
                    float width = 200f * 72f / 25.4f;
                    float height = width * bitmap.getHeight() / bitmap.getWidth();
                    float left = (page.getInfo().getPageWidth() - width) / 2;
                    float top = (page.getInfo().getPageHeight() - height) / 2;
                    page.getCanvas().drawBitmap(bitmap, null, new RectF(left, top, left + width, top + height),
                        new Paint(Paint.FILTER_BITMAP_FLAG));
                    document.finishPage(page); bitmap.recycle();
                    if (cancellation.isCanceled()) { callback.onWriteCancelled(); return; }
                    document.writeTo(destination);
                    callback.onWriteFinished(new PageRange[]{PageRange.ALL_PAGES});
                } catch (Exception error) { callback.onWriteFailed("Could not prepare target card"); }
                finally { document.close(); }
            }
            @Override public void onFinish() { MarkerExportActivity.this.finish(); }
        }, defaults);
    }
    @Override protected void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request == 26 && result == RESULT_OK && data != null && data.getData() != null) {
            try (InputStream source = getAssets().open("Raw/marker.png");
                 OutputStream destination = getContentResolver().openOutputStream(data.getData())) {
                byte[] buffer = new byte[8192]; int count;
                while ((count = source.read(buffer)) != -1) destination.write(buffer, 0, count);
                Toast.makeText(this, "Target card saved", Toast.LENGTH_SHORT).show();
            } catch (Exception error) {
                Toast.makeText(this, "Could not save target card", Toast.LENGTH_LONG).show();
            }
        }
        finish();
    }
}
