using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Flann;
using Emgu.CV.ML;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.Core
{
    public class TestGround
    {
        public TestGround()
        {

        }

        public Mat AdaptiveThreshold(Mat image)
        {
            var lst = new List<Mat>();
            Mat result = new Mat();
            Mat blur = new Mat();
            CvInvoke.GaussianBlur(image, blur, new Size(5, 5), 0);
            CvInvoke.Threshold(blur, result, 0, 255, ThresholdType.Otsu);
            //var blur = cv2.GaussianBlur(img, (5, 5), 0)
            return result;
        }

        public List<Mat> Thresholding(Mat image)
        {
            //var thresh = CvInvoke.AdaptiveThreshold(image, 127, 255, ThresholdType.Binary);
            Mat binary = new Mat();
            Mat binaryInv = new Mat();
            Mat trunc = new Mat();
            Mat tozero = new Mat();
            Mat tozeroInv = new Mat();
            Mat otsu = new Mat();
            Mat mask = new Mat();
            //CvInvoke.AdaptiveThreshold(image, result, 127, 255, ThresholdType.Binary, 2, 1);
            CvInvoke.Threshold(image, binary, 127, 255, ThresholdType.Binary);
            CvInvoke.Threshold(image, binaryInv, 127, 255, ThresholdType.BinaryInv);
            CvInvoke.Threshold(image, trunc, 127, 255, ThresholdType.Trunc);
            CvInvoke.Threshold(image, tozero, 127, 255, ThresholdType.ToZero);
            CvInvoke.Threshold(image, tozeroInv, 127, 255, ThresholdType.ToZeroInv);
            CvInvoke.Threshold(image, otsu, 127, 255, ThresholdType.Otsu);
            CvInvoke.Threshold(image, mask, 127, 255, ThresholdType.Mask);
            var lst = new List<Mat>
            {
                binary,
                binaryInv,
                trunc,
                tozero,
                tozeroInv,
                otsu,
                mask
            };
            return lst;
        }

        //public void Prove(string imagesDir, int networkInputSize, float trainSplitSize)
        //{
        //    Console.WriteLine("Reading training set...");
        //    var filePaths = GetFiles(imagesDir);
        //    var files = new VectorOfCvString();
        //    foreach (var o in filePaths)
        //    {
        //        var str = new CvString(o);
        //        files.Push(str);
        //    }
        //    //CvInvoke.RandShuffle(files, 1, 0);
        //    //ReadImages(filePaths);

        //    //Mat descriptorsSet;
        //    //var descriptorsMetadata = new VectorOfMat();
        //    //var classes = new VectorOfCvString();



        //    //for(var i = 0; i<files.Size;i++)
        //    //{
        //    //    var img = CvInvoke.Imread(o);
        //    //    if (img.IsEmpty) continue;
        //    //    var className = GetClassName(o);
        //    //    var descriptors = GetDescriptors(img);

        //    //}
        //}

        public static List<String> GetFiles(string imagesDir)
        {
            var dir = new DirectoryInfo(imagesDir);
            var files = dir.GetFiles();
            var filePaths = new List<string>();
            foreach(var o in files)
            {
                filePaths.Add(o.FullName);
            }
            return filePaths;
        }

        public void ReadImages(List<string> files)
        {
            foreach(var o in files)
            {
                var img = CvInvoke.Imread(o);
                if (img.IsEmpty) continue;
                var className = GetClassName(o);
                var descriptors = GetDescriptors(img);

            }
        }

        public static CvString GetClassName(string filePath)
        {
            var str = filePath.Substring(filePath.LastIndexOf('/') + 1, 3);
            return new CvString(str);
        }

        public static Mat GetDescriptors(Mat img)
        {
            var descriptors = new Mat();
            using (var kaze = new KAZE())
            {
                var keypoints = new VectorOfKeyPoint();
                kaze.DetectAndCompute(img, null, keypoints, descriptors, false);
                return descriptors;
            }
        }
    }
}
