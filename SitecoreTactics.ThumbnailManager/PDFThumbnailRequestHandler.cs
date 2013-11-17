using System;
using System.Web;
using Sitecore.Configuration;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Events;
using Sitecore.Resources.Media;
using Sitecore.Web;
using System.Web.SessionState;
using System.IO;
using Sitecore;

namespace SitecoreTactics.ThumbnailManager
{
    class PDFThumbnailRequestHandler : Sitecore.Resources.Media.MediaRequestHandler, IRequiresSessionState
    {
        protected override bool DoProcessRequest(HttpContext context)
        {

            Assert.ArgumentNotNull(context, "context");

            MediaRequest request = MediaManager.ParseMediaRequest(context.Request);
            request.Options.UseMediaCache = true;

            if (request == null)
            {
                return false;
            }

            Sitecore.Resources.Media.Media media = null;
            try
            {
                media = MediaManager.GetMedia(request.MediaUri);

                if (media != null)
                    return this.DoProcessRequest(context, request, media);
            }
            catch (Exception ex)
            {
                Log.Error("Error while creating thumbnails of PDF, URL:" + context.Request.Url.ToString() + ". Exception:" + ex.ToString(), this);
            }

            if (media == null)
            {
                context.Response.Write("404 - File not found");
                context.Response.End();
            }
            else
            {
                string itemNotFoundUrl = (Context.Site.LoginPage != string.Empty) ? Context.Site.LoginPage : Settings.NoAccessUrl;

                if (Settings.RequestErrors.UseServerSideRedirect)
                {
                    HttpContext.Current.Server.Transfer(itemNotFoundUrl);
                }
                else
                {
                    HttpContext.Current.Response.Redirect(itemNotFoundUrl);
                }
            }

            return true;
        }



        protected override bool DoProcessRequest(HttpContext context, MediaRequest request, Sitecore.Resources.Media.Media media)
        {
            Assert.ArgumentNotNull(context, "context");
            Assert.ArgumentNotNull(request, "request");
            Assert.ArgumentNotNull(media, "media");

            if (this.Modified(context, media, request.Options) == Sitecore.Tristate.False)
            {
                Event.RaiseEvent("media:request", new object[] { request });
                this.SendMediaHeaders(media, context);
                context.Response.StatusCode = 0x130;
                return true;
            }

            // Gets media stream for the requested media item thumbnail
            MediaStream stream = ProcessThumbnail(request, media);
            if (stream == null)
            {
                return false;
            }
            Event.RaiseEvent("media:request", new object[] { request });
            this.SendMediaHeaders(media, context);
            this.SendStreamHeaders(stream, context);
            using (stream)
            {
                context.Response.AddHeader("Content-Length", stream.Stream.Length.ToString());
                WebUtil.TransmitStream(stream.Stream, context.Response, Settings.Media.StreamBufferSize);
            }
            return true;
        }

        private MediaStream ProcessThumbnail(MediaRequest request, Sitecore.Resources.Media.Media media)
        {
            MediaStream mStream = null;

            if (request.Options.Height == 0 && request.Options.Width == 0)
            {
                request.Options.Thumbnail = true;
            }

            mStream = MediaManager.Cache.GetStream(media, request.Options);

            if (mStream == null)
            {
                string tempPath = Settings.TempFolderPath + "/PDF-Thumbnails/";

                tempPath = MainUtil.MapPath(tempPath);

                if (!Directory.Exists(tempPath))
                {
                    Directory.CreateDirectory(tempPath);
                }

                // Prepare filenames
                string pdfFile = tempPath + media.MediaData.MediaItem.ID.ToString() + ".pdf";
                string jpgFile = tempPath + media.MediaData.MediaItem.ID.ToString() + "_" + media.MediaData.MediaItem.InnerItem["revision"] + ".jpg";

                string resizedJpgFile = tempPath + media.MediaData.MediaItem.ID.ToString() + "_" + request.Options.Width.ToString() + "_" + request.Options.Height.ToString();

                if (!File.Exists(jpgFile))
                {
                    // Save BLOB media file to disk
                    MediaConverter.ConvertMediaItemToFile(media.MediaData.MediaItem, pdfFile);
                    
                    // Convert PDF to Jpeg - First Pager
                    MediaConverter.ConvertPDFtoJPG(pdfFile, 1, jpgFile);

                }

                // Resize Image
                MediaConverter.ReSizeJPG(jpgFile, resizedJpgFile, request.Options.Width, request.Options.Height, true);

                // Convert resized image to stream
                mStream = new MediaStream(File.Open(resizedJpgFile, FileMode.Open, FileAccess.Read, FileShare.Read), "jpg", media.MediaData.MediaItem);
                

                // Add the requested thumbnail to Media Cache
                MediaStream outStream = null;
                MediaManager.Cache.AddStream(media, request.Options, mStream, out outStream);

                if (outStream != null)
                {
                    // If Media cache is enabled
                    return outStream;
                }

            }

            // If Media cache is disabled
            return mStream;
        }
    }
}
