﻿using FaceAPI_MVC.Web.Helper;
using FaceAPI_MVC.Web.Models;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace FaceAPI_MVC.Web.Controllers
{
    public class MultiFaceDetectionController : Controller
    {
        private static string StorageAccount = ConfigurationManager.AppSettings["StorageAccount"];

        private static string StorageKey = ConfigurationManager.AppSettings["StorageKey"];

        private static string Container = ConfigurationManager.AppSettings["Container"];

        private static string ServiceKey = ConfigurationManager.AppSettings["FaceServiceKey"];

        private static string directory = "~/MultiDetectedFiles";

        private MultiFaceDetectionModal finalModal = new MultiFaceDetectionModal();

        public int MaxImageSize
        {
            get
            {
                return 450;
            }
        }

        // GET: MultiFaceDetection
        public async Task<ActionResult> Index()
        {
            try
            {
                // Step 1. Get images from blob storage.
                BlobHelper BlobHelper = new BlobHelper(StorageAccount, StorageKey);

                List<string> blobs = BlobHelper.ListBlobs(Container);

                List<string> images = new List<string>();

                foreach (var blobName in blobs)
                {
                    images.Add(blobName);
                }

                // Step 2. For each image, run the face api detection algorithm.
                var faceServiceClient = new FaceServiceClient(ServiceKey, "https://westcentralus.api.cognitive.microsoft.com/face/v1.0");

                for (int i = 0; i < blobs.Count; i++)
                {
                    var detectedFaces = new ObservableCollection<vmFace>();
                    var resultCollection = new ObservableCollection<vmFace>();

                    using (WebClient client = new WebClient())
                    {
                        byte[] fileBytes = client.DownloadData(string.Concat("https://faceapiweustorage.blob.core.windows.net/cloudprojectsampleimages/", images[i]));

                        bool exists = System.IO.Directory.Exists(Server.MapPath(directory));
                        if (!exists)
                        {
                            try
                            {
                                Directory.CreateDirectory(Server.MapPath(directory));
                            }
                            catch (Exception ex)
                            {
                                ex.ToString();
                            }
                        }

                        string imageRelativePath = "../MultiDetectedFiles" + '/' + images[i];

                        string imageFullPath = Server.MapPath(directory) + '/' + images[i] as string;

                        System.IO.File.WriteAllBytes(imageFullPath, fileBytes);

                        using (var stream = client.OpenRead(string.Concat("https://faceapiweustorage.blob.core.windows.net/cloudprojectsampleimages/", images[i])))
                        {
                            Face[] faces = await faceServiceClient.DetectAsync(stream, true, true, new FaceAttributeType[] { FaceAttributeType.Gender, FaceAttributeType.Age, FaceAttributeType.Smile, FaceAttributeType.Glasses });

                            Bitmap CroppedFace = null;

                            foreach (var face in faces)
                            {
                                //Create & Save Cropped Images
                                var croppedImg = Convert.ToString(Guid.NewGuid()) + ".jpeg" as string;
                                var croppedImgPath = "../MultiDetectedFiles" + '/' + croppedImg as string;
                                var croppedImgFullPath = Server.MapPath(directory) + '/' + croppedImg as string;
                                CroppedFace = CropBitmap(
                                                (Bitmap)Bitmap.FromFile(imageFullPath),
                                                face.FaceRectangle.Left,
                                                face.FaceRectangle.Top,
                                                face.FaceRectangle.Width,
                                                face.FaceRectangle.Height);
                                CroppedFace.Save(croppedImgFullPath, ImageFormat.Jpeg);

                                if (CroppedFace != null)
                                    ((IDisposable)CroppedFace).Dispose();

                                detectedFaces.Add(new vmFace()
                                {
                                    ImagePath = imageRelativePath,
                                    FileName = images[i],
                                    FilePath = croppedImgPath,
                                    Left = face.FaceRectangle.Left,
                                    Top = face.FaceRectangle.Top,
                                    Width = face.FaceRectangle.Width,
                                    Height = face.FaceRectangle.Height,
                                    FaceId = face.FaceId.ToString(),
                                    Gender = face.FaceAttributes.Gender,
                                    Age = string.Format("{0:#} years old", face.FaceAttributes.Age),
                                    IsSmiling = face.FaceAttributes.Smile > 0.0 ? "Smile" : "Not Smile",
                                    Glasses = face.FaceAttributes.Glasses.ToString(),
                                });
                            }

                            // Convert detection result into UI binding object for rendering.
                            var imageInfo = UIHelper.GetImageInfoForRenderingFromStream(stream);
                            var rectFaces = UIHelper.CalculateFaceRectangleForRendering(faces, MaxImageSize, imageInfo);
                            foreach (var face in rectFaces)
                            {
                                resultCollection.Add(face);
                            }

                            var faceModal = new FaceDetectionModal { DetectedFaces = detectedFaces, ResultCollection = resultCollection };

                            this.finalModal.Items.Add(faceModal);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Write(ex);
                throw;
            }

            return View(this.finalModal);
        }

        public Bitmap CropBitmap(Bitmap bitmap, int cropX, int cropY, int cropWidth, int cropHeight)
        {
            Rectangle rect = new Rectangle(cropX, cropY, cropWidth, cropHeight);
            Bitmap cropped = bitmap.Clone(rect, bitmap.PixelFormat);
            return cropped;
        }

        public string FixBase64ForImage(string Image)
        {
            System.Text.StringBuilder sbText = new System.Text.StringBuilder(Image, Image.Length);
            sbText.Replace("\r\n", String.Empty); sbText.Replace(" ", String.Empty);
            return sbText.ToString();
        }
    }
}