using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SitecoreTactics.ThumbnailManager
{
    public class MediaConverter
    {
        #region Image Conversion
        public static void ConvertPDFtoJPG(string pdfFile, int pageNo, string jpgFile)
        {
            GhostscriptSharp.GhostscriptWrapper.GeneratePageThumb(pdfFile, jpgFile, pageNo, 72, 72, false, false);
        }

        public static string GetImageOrientation(string jpgFile)
        {
            using (FileStream fs = new FileStream(jpgFile, FileMode.Open, FileAccess.ReadWrite))
            {
                using (System.Drawing.Image photo = System.Drawing.Image.FromStream(fs, true, false))
                {
                    if (photo.Width > photo.Height)
                        return "L";
                    else
                        return "P";
                }
            }
        }

        public static void ReSizeJPG(string inputJpgFile, string outputJpgFile, int width, int height, bool isColor)
        {
            using (FileStream fs = new FileStream(inputJpgFile, FileMode.Open, FileAccess.ReadWrite))
            {
                using (System.Drawing.Image photo = System.Drawing.Image.FromStream(fs, true, false))
                {
                    using (System.Drawing.Image newImage = ScaleImage(photo, width, height, isColor))
                    {
                        newImage.Save(outputJpgFile, ImageFormat.Jpeg);
                    }
                }
            }
        }

        private static System.Drawing.Image ScaleImage(System.Drawing.Image image, int maxWidth, int maxHeight, bool isColor)
        {
            if (maxWidth == 0 && maxHeight == 0)
            {
                maxWidth = image.Width;
                maxHeight = image.Height;
            }
            else if (maxWidth == 0)
            {
                maxWidth = maxHeight * image.Width / image.Height;
            }
            else if (maxHeight == 0)
            {
                maxHeight = maxWidth * image.Height / image.Width;
            }


            var newImage = new Bitmap(maxWidth, maxHeight);

            if (!isColor)
            {
                ColorMatrix colorMatrix = new ColorMatrix(
                   new float[][] 
                              {
                                 new float[] {.3f, .3f, .3f, 0, 0},
                                 new float[] {.59f, .59f, .59f, 0, 0},
                                 new float[] {.11f, .11f, .11f, 0, 0},
                                 new float[] {0, 0, 0, 1, 0},
                                 new float[] {0, 0, 0, 0, 1}
                              });

                //create some image attributes
                ImageAttributes attributes = new ImageAttributes();

                //set the color matrix attribute
                attributes.SetColorMatrix(colorMatrix);
                Graphics.FromImage(newImage).DrawImage(image, new Rectangle(0, 0, maxWidth, maxHeight), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }
            else
                Graphics.FromImage(newImage).DrawImage(image, 0, 0, maxWidth, maxHeight);

            return newImage;
        }

        #endregion

        #region Sitecore Media Conversion
        public static void ConvertMediaItemToFile(MediaItem mediaItem, string fileName)
        {
            Assert.ArgumentNotNull(mediaItem, "mediaItem");

            if (mediaItem.InnerItem["file path"].Length > 0)
                return;

            // Have to use "blob" field in order to workaround an issue with mediaItem.GetStream() 
            // as it uses mediaItem.FileBased property that returns "true" if at least one version has a file based media asset.
            var blobField = mediaItem.InnerItem.Fields["blob"];
            Stream stream = blobField.GetBlobStream();
            if (stream == null)
            {
                //Log.Warn(string.Format("Cannot find media data at item '{0}'", mediaItem.MediaPath), typeof(MediaStorageManager));
                return;
            }

            //string fileName = GetFilename(mediaItem, stream);
            string relativePath = Sitecore.IO.FileUtil.UnmapPath(fileName);
            try
            {
                SaveToFile(stream, fileName);

                stream.Flush();
                stream.Close();

            }
            catch (Exception)
            {
                //Log.Error(string.Format("Cannot convert BLOB stream of '{0}' media item to '{1}' file", mediaItem.MediaPath, relativePath), ex, typeof(MediaStorageManager));
            }
        }

        private static void SaveToFile(Stream stream, string fileName)
        {
            byte[] buffer = new byte[8192];
            using (FileStream fs = File.Create(fileName))
            {
                int length;
                do
                {
                    length = stream.Read(buffer, 0, buffer.Length);
                    fs.Write(buffer, 0, length);
                }
                while (length > 0);

                fs.Flush();
                fs.Close();
            }
        }
        #endregion
    }
}
