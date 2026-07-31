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

        //NetVips version of FormatImage for benchmarking against ImageSharp in parallel callers.
        public static async Task<Bitmap> FormatImageNetVips(string path)
        {
            if (File.Exists(path))
            {
                //any file that starts with a . will cause an ImageSharp error -- need filename here not path
                if (PathHelper.GetFileNameFromImagePath(path).StartsWith("."))
                {
                    return LoadFromResource(new Uri("avares://ImagePerfect/Assets/missing_image.png"));
                }

                //this will fail if an image is corrupted or libvips cannot decode it
                try
                {
                    //using var image = VipsImage.Thumbnail(path, 600);
                    //using var orientedImage = image.Autorot();

                    //var buffer = orientedImage.WriteToBuffer(".webp");

                    //using var ms = new MemoryStream(buffer);
                    //Bitmap img = new Bitmap(ms);
                    //return img;

                    using var thumb = VipsImage.Thumbnail(path, 600);
                    using var oriented = thumb.Autorot();

                    // Ensure 4 bands (add opaque alpha if source had none)
                    using var rgba = oriented.Bands == 3
                        ? oriented.Bandjoin(255)
                        : oriented;

                    // Reorder R,G,B,A -> B,G,R,A to match Avalonia's Bgra8888 layout
                    using var bgra = rgba[2].Bandjoin(rgba[1], rgba[0], rgba[3]);

                    byte[] pixels = bgra.WriteToMemory<byte>();

                    var bitmap = new WriteableBitmap(
                        new PixelSize(bgra.Width, bgra.Height),
                        new Avalonia.Vector(96, 96),
                        Avalonia.Platform.PixelFormat.Bgra8888,
                        Avalonia.Platform.AlphaFormat.Unpremul);

                    using (var fb = bitmap.Lock())
                    {
                        int srcRowBytes = bgra.Width * 4;
                        if (fb.RowBytes == srcRowBytes)
                        {
                            // tightly packed on both sides -> one copy
                            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, fb.Address, pixels.Length);
                        }
                        else
                        {
                            // stride differs -> copy row by row
                            for (int y = 0; y < bgra.Height; y++)
                            {
                                System.Runtime.InteropServices.Marshal.Copy(
                                    pixels, y * srcRowBytes,
                                    fb.Address + y * fb.RowBytes,
                                    srcRowBytes);
                            }
                        }
                    }

                    return (Bitmap)bitmap;
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
