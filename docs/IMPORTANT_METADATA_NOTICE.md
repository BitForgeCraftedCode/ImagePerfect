[← Back to Docs Index](README.md)

<a id="important-metadata-note"></a>
## ⚠️ Important Note On Metadata
>
> **Shotwell Compatibility**
>
> ImagePerfect currently supports reading tags and ratings written by Shotwell, provided Shotwell's Write Metadata to Files option was enabled.
>
> **Supported Tags**
>
> ImagePerfect supports tags containing **ASCII letters and numbers only (A–Z, a–z, 0–9)**. Tags containing spaces or special characters are not currently supported.
>
> **Please understand how this affects your existing image metadata:**
>
> **Scanning:** When scanning image metadata, ImagePerfect will skip any existing keywords or tags containing unsupported characters. These tags will not be imported into ImagePerfect.
> **Adding or changing tags:** When ImagePerfect writes tags to an image, it replaces the image's existing keyword list with the tags currently supported and managed by ImagePerfect. Any previously skipped keywords will be permanently removed from the image's metadata.
> **Important — scan before editing:** If you add or change a tag without first scanning the image's existing metadata, ImagePerfect will replace the entire existing keyword list with the tags currently stored in its database. This can remove both supported and unsupported keywords that were previously present in the image.
>
> **Preparing Your Shotwell Library**
>
> If your existing Shotwell tags contain spaces or special characters, you can edit them in Shotwell first to match ImagePerfect's supported format. Then use Shotwell's Write Metadata to Files option and scan your folders in ImagePerfect before making changes.
>
> For example, Summer Vacation could become SummerVacation or SummerVacation2026.
>
> **Metadata Writing and Image Rotation**
> ImagePerfect uses ExifTool to write ratings and tags directly to image files without re-encoding image pixels. It also supports lossless image rotation through ExifTool, where supported by the image format.
>
> As with any bulk metadata operation, maintaining backups of your original images is recommended.
>
>
> **A Personal Note from the Developer**
> ImagePerfect was originally built to manage my own personal photo library. I designed it around my own needs and workflow, and I use it to manage my collection.
>
> The metadata limitations described above are intentional trade-offs in the current implementation. I use ImagePerfect myself, but as with any software that modifies files, I strongly recommend maintaining backups of your original images.
