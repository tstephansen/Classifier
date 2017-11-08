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

        public void Prove(string imagesDir, int networkInputSize, float trainSplitSize)
        {
            Console.WriteLine("Reading training set...");
            var filePaths = GetFiles(imagesDir);
            var files = new VectorOfCvString();
            foreach (var o in filePaths)
            {
                var str = new CvString(o);
                files.Push(str);
            }
            //CvInvoke.RandShuffle(files, 1, 0);
            //ReadImages(filePaths);

            Mat descriptorsSet;
            var descriptorsMetadata = new VectorOfMat();
            var classes = new VectorOfCvString();



            //for(var i = 0; i<files.Size;i++)
            //{
            //    var img = CvInvoke.Imread(o);
            //    if (img.IsEmpty) continue;
            //    var className = GetClassName(o);
            //    var descriptors = GetDescriptors(img);

            //}
        }

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
