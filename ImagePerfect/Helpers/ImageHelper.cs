using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Image = SixLabors.ImageSharp.Image;
using Size = SixLabors.ImageSharp.Size;
using VipsImage = NetVips.Image;

namespace ImagePerfect.Helpers
{
    public static class ImageHelper
    {
        static ImageHelper()
        {
            // Prevent ImageSharp from spinning up internal parallelism per-image;
            // we already parallelize across images at the call site.
            Configuration.Default.MaxDegreeOfParallelism = 1;
        }
        public static Bitmap LoadFromResource(Uri resourceUri)
        {
            return new Bitmap(AssetLoader.Open(resourceUri));
        }

        public static Bitmap LoadFromFileSystem(string resourcePath)
        {
            return new Bitmap(resourcePath);
        }

        public static async Task<Bitmap?> LoadFromWeb(Uri url)
        {
            using var httpClient = new HttpClient();
            try
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var data = await response.Content.ReadAsByteArrayAsync();
                return new Bitmap(new MemoryStream(data));
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"An error occurred while downloading image '{url}' : {ex.Message}");
                return null;
            }
        }

        //need to rotate portrait images and resize for screen
        public static async Task<Bitmap> FormatImage(string path)
        {
            if (File.Exists(path))
            {
                //any file that starts with a . will cause an ImageSharp error -- need filename here not path
                if (PathHelper.GetFileNameFromImagePath(path).StartsWith("."))
                {
                    return LoadFromResource(new Uri("avares://ImagePerfect/Assets/missing_image.png"));
                }
                //this will fail if an image is corrupted
                try
                {
                    var options = new DecoderOptions
                    {
                        //reduce decode cost before the resize pipeline runs
                        //Only need a 600px thumbnail for the app so decode directly to smaller size first is more performant
                        //TargetSize is treated a fit-within box so make this 2x the goal and resize later preserving aspect ratio 
                        TargetSize = new Size(1200, 1200)
                        // no Configuration set -> uses Configuration.Default, which now has MDOP = 1
                    };

                    using (MemoryStream ms = new MemoryStream())
                    using (var image = await Image.LoadAsync(options, path))
                    {
                        image.Mutate(x =>
                        {
                            x.AutoOrient();
                            x.Resize(600, 0);
                        });
                        await image.SaveAsBmpAsync(ms);
                        //set stream to begining after writing
                        ms.Seek(0, SeekOrigin.Begin);
                        Bitmap img = new Bitmap(ms);
                        ms.Close();
                        return img;
                    }
                }
                catch
                {
                    return LoadFromResource(new Uri("avares://ImagePerfect/Assets/missing_image.png"));
                }
            }
            else
            {
                return LoadFromResource(new Uri("avares://ImagePerfect/Assets/missing_image.png"));
            }

        }

        //need to rotate portrait images and resize for screen
        //NetVips version of FormatImage for benchmarking against ImageSharp in parallel callers.
        public static async Task<Bitmap> FormatImageNetVips(string path)
        {
            if (File.Exists(path))
            {
                //any file that starts with a . will cause an error -- need filename here not path
                if (PathHelper.GetFileNameFromImagePath(path).StartsWith("."))
                {
                    return LoadFromResource(new Uri("avares://ImagePerfect/Assets/missing_image.png"));
                }

                //this will fail if an image is corrupted or libvips cannot decode it
                try
                {
                    // libvips images are lazy pipelines -- nothing actually decodes/executes until
                    // you ask for output (e.g. WriteToMemory below). Each line here just extends
                    // the pipeline description; it's cheap.
                    using var thumb = VipsImage.Thumbnail(path, 600);
                    using var oriented = thumb.Autorot();
                    using var sharpened = oriented.Sharpen(); // small, cheap, noticeably crisper thumbnails

                    // Avalonia's Bgra8888 format requires exactly 4 bands (B, G, R, A).
                    // Some source images (JPEGs, mostly) decode as 3 bands (just R,G,B) with no
                    // alpha channel at all. If that's the case, tack on a 4th band that's a
                    // constant 255 (fully opaque) so the pixel layout matches what Avalonia expects.
                    using var rgba = sharpened.Bands == 3
                        ? sharpened.Bandjoin(255)    // RGB -> RGBA, alpha = fully opaque
                        : sharpened;                 // already 4 bands (e.g. PNG with alpha), leave as-is

                    // Reorder R,G,B,A -> B,G,R,A to match Avalonia's Bgra8888 layout
                    // libvips decodes pixels in R, G, B, A band order.
                    // Avalonia's Bgra8888 format expects bytes in B, G, R, A order per pixel.
                    // rgba[n] pulls out a single band as its own 1-band image:
                    // rgba[0] = R channel, rgba[1] = G channel, rgba[2] = B channel, rgba[3] = A channel
                    // Bandjoin glues bands back together into one multi-band image, in the order given.
                    // So this rebuilds the image as B,G,R,A -- same pixels, reordered channels --
                    // to match what Avalonia's pixel format expects. Nothing is resized or recolored,
                    // just the byte order per pixel.
                    using var bgra = rgba[2].Bandjoin(rgba[1], rgba[0], rgba[3]);

                    // This is where the pipeline actually executes. WriteToMemory<byte> forces
                    // libvips to run everything queued above (thumbnail, autorot, sharpen, band reorder)
                    // and hand back a flat byte array: lengthArray = width * height * 4 bytes, tightly packed, 4bytes is the bgra
                    // row after row, no padding between rows.
                    // In C#, a byte array (byte[]) is a fixed-size, sequential collection of 8-bit unsigned integers that can hold values ranging from 0 to 255
                    /*
                      Take a 2×2 pixel image, 3 bytes per pixel (RGB, no alpha, for simplicity)
                      (0,0)=Red     (1,0)=Green
                      (0,1)=Blue    (1,1)=White
                      Using x = column, y = row (this is the standard image convention — x goes right, y goes down):

                      pixel	        R	G	B
                      (0,0) Red	    255	0	0
                      (1,0) Green	0	255	0
                      (0,1) Blue	0	0	255
                      (1,1) White	255	255	255
                      
                      the actual byte array will look like this:
                      index:   0    1    2    3    4    5    6    7    8    9   10   11
                      value: [255,  0,   0,   0, 255,  0,    0,   0, 255, 255, 255, 255]

                      to get the starting byte index/offset for any pixel at (x,y) (col,row)

                      Step 1: How many indexes does ONE pixel take up?

                      Each pixel = 3 bytes (R, G, B). So one pixel = 3 indexes. (bytesPerPixel)
                      
                      Step 2: How many indexes does ONE ROW take up?

                      A row has width pixels sitting side by side. Our width = 2. So one row = 2 pixels × 3 indexes each = 6 indexes.
                      This number — "indexes per row" — is the important one. Let's just call it row-width-in-bytes = width * bytesPerPixel = 2 * 3 = 6.

                      Step 3: "Which row do I start on" — walked through by hand

                      Say we want pixel (x=0, y=1) — that's the Blue pixel, second row, first column.

                      Ask yourself: before I even get to row 1, how many entire rows do I have to walk past first?

                      Answer: row y=1 means "I am the row AFTER row 0." So I have to walk past exactly 1 full row (row 0) before row 1 even begins.

                      How many indexes is "1 full row"? We just said: 6 indexes (that's row-width-in-bytes).

                      So: to find where row 1 starts, skip 1 row * 6 indexes/row = 6 indexes. Row 1 starts at index 6.
                      
                      Generalize: how many bytes do I walk to get past y of those rows? "how many indexes to skip to reach the START of row y" = y * (indexes per row) = y * width * bytesPerPixel. or (Stride*y) stride is the size of one single row  

                      Step 4: "Which pixel within that row" — walked through by hand

                      Now that we know row y starts at index y * width * bytesPerPixel, we still need to walk sideways into that row to find column x.

                      Say we want pixel (x=1, y=1) — that's White, second row, second column.

                      We already know row 1 starts at index 6 (from Step 3). Now: how many pixels do I walk past to get from "start of row" to column x=1?

                      Answer: x=1 means "I am the pixel AFTER column 0." So I walk past exactly 1 pixel (column 0) first.

                      How many indexes is "1 pixel"? We said in Step 1: 3 indexes (bytesPerPixel).
                      
                      So: skip 1 pixel * 3 indexes/pixel = 3 indexes, starting from box 6. That lands us at index 6 + 3 = 9.
                      
                      Generalize: "how many indexes to walk sideways to reach column x, from the start of the row" = x * bytesPerPixel.
                      
                      Step 5: Add the two skips together

                      Total indexes to skip to reach pixel (x, y):
                      (indexes to skip to reach the start of row y)   +   (indexes to skip sideways to reach column x)
                      = y * width * bytesPerPixel                     +          x * bytesPerPixel
                      offet = (y*width + x)*bytesPerPixel

                      offset = y * (width * bytesPerPixel) + x * bytesPerPixel
                      offest = (y * width + x) * bytesPerPixel
                     */
                    byte[] pixels = bgra.WriteToMemory<byte>();

                    // DPI (Dots Per Inch) DPI tells Avalonia how to scale the bitmap when rendering it on displays with high pixel density,
                    // such as 4K monitors, Retina displays, or mobile screens.
                    // The universal baseline standard. Unless your specific app scales UI coordinates manually based on user monitors, sticking to 96 ensures layout stability.

                    // AlphaFormat defines how the transparency (Alpha) channel interacts with the color channels (Red, Green, and Blue) inside the pixel data.
                    // libvips treats the alpha channel independently (Straight Alpha) for PNGs and adds a solid alpha channel for JPEGs.
                    // Avalonia will handle the internal conversion to premultiplied alpha on its own.

                    // Create an empty writable bitmap of the right size/format for Avalonia to render.
                    // Unpremul = "straight" alpha (RGB values aren't pre-multiplied by alpha),
                    // which is how libvips (and most decoders) hand back pixel data.
                    var bitmap = new WriteableBitmap(
                        new PixelSize(bgra.Width, bgra.Height),
                        new Avalonia.Vector(96, 96),
                        Avalonia.Platform.PixelFormat.Bgra8888,
                        Avalonia.Platform.AlphaFormat.Unpremul);

                    // Stride = how many bytes is one row? = (width * bytesPerPixel) (also called pitch or scanline width) is the total number of bytes used to store exactly one row of pixels in computer memory
                    //A WriteableBitmap in Avalonia UI is an in-memory image buffer that allows you to manually manipulate pixel data.
                    //To modify a WriteableBitmap dynamically, you call the .Lock() method, which grants you safe, thread-local access to the underlying pixel buffer
                    //copy the pixels from WriteToMemory to the Avalonia WriteableBitmap
                    using (var fb = bitmap.Lock())
                    {
                        int srcRowBytes = bgra.Width * 4; //Essential if ensuring 4 bytes per pixel (RGBA)  -- Stride
                        if (fb.RowBytes == srcRowBytes)
                        {
                            /*
                               Marshal.Copy(byte[] source, int startIndex, IntPtr destination, int length)
                               source — a managed byte array (lives in normal .NET memory, garbage collected)
                               startIndex — where in source to start reading from
                               destination — an unmanaged memory address (a raw pointer, IntPtr) to start writing to
                               length — how many bytes to copy

                               So this method's whole job is: "reach across the managed/unmanaged boundary and copy bytes from a normal C# array 
                               into raw memory that .NET doesn't control." That's exactly the situation you're in — pixels is a normal managed array, 
                               but fb.Address is a pointer into memory that Avalonia/Skia allocated natively (outside the GC's control) for the bitmap's pixel buffer.
                             */
                            // tightly packed on both sides -> one copy
                            // Best case: Avalonia's internal buffer stride matches ours exactly
                            // (no padding on either side), so we can copy the whole thing in one go.
                            // Starting at index 0 of pixels, copy pixels.Length bytes total, writing them starting at address fb.Address.
                            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, fb.Address, pixels.Length);
                        }
                        else
                        {
                            // stride differs -> copy row by row
                            // Avalonia's framebuffer can pad each row to a platform-specific
                            // byte alignment, meaning fb.RowBytes (its stride) may be larger than
                            // srcRowBytes (our tightly-packed stride). If we blindly copied the
                            // whole array in one shot here, every row after the first would land
                            // at the wrong offset and the image would come out sheared/garbled.
                            // So instead, copy one row at a time, writing each row to its correct
                            // (possibly padded) offset in the destination.
                            // offset = y * stride + x * bytesPerPixel
                            for (int y = 0; y < bgra.Height; y++)
                            {
                                //starting at index y * srcRowBytes (row) of pixels, copy srcRowBytes (row) bytes total, writing them starting at address fb.Address + y * fb.RowBytes
                                //fb.Address + y * fb.RowBytes (address to start + how far into the buffer row y is) -> gets the actual real memory address of row y
                                System.Runtime.InteropServices.Marshal.Copy(pixels, y * srcRowBytes, fb.Address + y * fb.RowBytes, srcRowBytes);
                            }
                        }
                    }

                    return (Bitmap)bitmap; // WriteableBitmap derives from Bitmap, so this cast is safe/implicit
                }
                catch(Exception ex)
                {
                    Debug.WriteLine("NetVips failed: " + ex);
                    return LoadFromResource(new Uri("avares://ImagePerfect/Assets/missing_image.png"));
                }
            }
            else
            {
                return LoadFromResource(new Uri("avares://ImagePerfect/Assets/missing_image.png"));
            }
        }
    }
}
