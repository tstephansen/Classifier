using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Flann;
using Emgu.CV.Structure;
using Emgu.CV.Util;
#if !__IOS__
using Emgu.CV.Cuda;
#endif
using Emgu.CV.XFeatures2D;

namespace Classifier.Core
{
    public static class SurfDrawMatches
    {
        public static void FindMatch(Mat modelImage, Mat observedImage, out long matchTime, out VectorOfKeyPoint modelKeyPoints, out VectorOfKeyPoint observedKeyPoints, VectorOfVectorOfDMatch matches, out Mat mask, out Mat homography)
        {
            int k = 2;
            double uniquenessThreshold = 0.8;
            double hessianThresh = 400;

            Stopwatch watch;
            homography = null;

            modelKeyPoints = new VectorOfKeyPoint();
            observedKeyPoints = new VectorOfKeyPoint();

#if !__IOS__
            if (CudaInvoke.HasCuda)
            {
                CudaSURF surfCuda = new CudaSURF((float)hessianThresh);
                using (GpuMat gpuModelImage = new GpuMat(modelImage))
                //extract features from the object image
                using (GpuMat gpuModelKeyPoints = surfCuda.DetectKeyPointsRaw(gpuModelImage, null))
                using (GpuMat gpuModelDescriptors = surfCuda.ComputeDescriptorsRaw(gpuModelImage, null, gpuModelKeyPoints))
                using (CudaBFMatcher matcher = new CudaBFMatcher(DistanceType.L2))
                {
                    surfCuda.DownloadKeypoints(gpuModelKeyPoints, modelKeyPoints);
                    watch = Stopwatch.StartNew();

                    // extract features from the observed image
                    using (GpuMat gpuObservedImage = new GpuMat(observedImage))
                    using (GpuMat gpuObservedKeyPoints = surfCuda.DetectKeyPointsRaw(gpuObservedImage, null))
                    using (GpuMat gpuObservedDescriptors = surfCuda.ComputeDescriptorsRaw(gpuObservedImage, null, gpuObservedKeyPoints))
                    //using (GpuMat tmp = new GpuMat())
                    //using (Stream stream = new Stream())
                    {
                        matcher.KnnMatch(gpuObservedDescriptors, gpuModelDescriptors, matches, k);

                        surfCuda.DownloadKeypoints(gpuObservedKeyPoints, observedKeyPoints);

                        mask = new Mat(matches.Size, 1, DepthType.Cv8U, 1);
                        mask.SetTo(new MCvScalar(255));
                        Features2DToolbox.VoteForUniqueness(matches, uniquenessThreshold, mask);

                        int nonZeroCount = CvInvoke.CountNonZero(mask);
                        if (nonZeroCount >= 4)
                        {
                            nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(modelKeyPoints, observedKeyPoints,
                               matches, mask, 1.5, 20);
                            if (nonZeroCount >= 4)
                                homography = Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(modelKeyPoints,
                                   observedKeyPoints, matches, mask, 2);
                        }
                    }
                    watch.Stop();
                }
            }
            else
#endif
            {
                using (UMat uModelImage = modelImage.GetUMat(AccessType.Read))
                using (UMat uObservedImage = observedImage.GetUMat(AccessType.Read))
                {
                    SURF surfCPU = new SURF(hessianThresh);
                    //extract features from the object image
                    UMat modelDescriptors = new UMat();
                    surfCPU.DetectAndCompute(uModelImage, null, modelKeyPoints, modelDescriptors, false);

                    watch = Stopwatch.StartNew();

                    // extract features from the observed image
                    UMat observedDescriptors = new UMat();
                    surfCPU.DetectAndCompute(uObservedImage, null, observedKeyPoints, observedDescriptors, false);
                    BFMatcher matcher = new BFMatcher(DistanceType.L2);
                    matcher.Add(modelDescriptors);

                    matcher.KnnMatch(observedDescriptors, matches, k, null);
                    mask = new Mat(matches.Size, 1, DepthType.Cv8U, 1);
                    mask.SetTo(new MCvScalar(255));
                    Features2DToolbox.VoteForUniqueness(matches, uniquenessThreshold, mask);

                    int nonZeroCount = CvInvoke.CountNonZero(mask);
                    if (nonZeroCount >= 4)
                    {
                        nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(modelKeyPoints, observedKeyPoints,
                           matches, mask, 1.5, 20);
                        if (nonZeroCount >= 4)
                            homography = Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(modelKeyPoints,
                               observedKeyPoints, matches, mask, 2);
                    }

                    watch.Stop();
                }
            }
            matchTime = watch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Draw the model image and observed image, the matched features and homography projection.
        /// </summary>
        /// <param name="modelImage">The model image</param>
        /// <param name="observedImage">The observed image</param>
        /// <param name="matchTime">The output total time for computing the homography matrix.</param>
        /// <returns>The model image and observed image, the matched features and homography projection.</returns>
        public static Mat Draw(Mat modelImage, Mat observedImage, out long matchTime)
        {
            Mat homography;
            VectorOfKeyPoint modelKeyPoints;
            VectorOfKeyPoint observedKeyPoints;
            using (VectorOfVectorOfDMatch matches = new VectorOfVectorOfDMatch())
            {
                Mat mask;
                FindMatch(modelImage, observedImage, out matchTime, out modelKeyPoints, out observedKeyPoints, matches,
                   out mask, out homography);

                //Draw the matched keypoints
                Mat result = new Mat();
                //Features2DToolbox.DrawMatches(modelImage, modelKeyPoints, observedImage, observedKeyPoints,
                //   matches, result, new MCvScalar(255, 255, 255), new MCvScalar(255, 255, 255), mask);
                Features2DToolbox.DrawMatches(modelImage, modelKeyPoints, observedImage, observedKeyPoints,
                   matches, result, new MCvScalar(0, 0, 0), new MCvScalar(0, 0, 0), mask);

                #region draw the projected region on the image

                if (homography != null)
                {
                    //draw a rectangle along the projected model
                    Rectangle rect = new Rectangle(Point.Empty, modelImage.Size);
                    PointF[] pts = new PointF[]
                    {
                  new PointF(rect.Left, rect.Bottom),
                  new PointF(rect.Right, rect.Bottom),
                  new PointF(rect.Right, rect.Top),
                  new PointF(rect.Left, rect.Top)
                    };
                    pts = CvInvoke.PerspectiveTransform(pts, homography);

                    Point[] points = Array.ConvertAll<PointF, Point>(pts, Point.Round);
                    using (VectorOfPoint vp = new VectorOfPoint(points))
                    {
                        CvInvoke.Polylines(result, vp, true, new MCvScalar(255, 0, 0, 255), 5);
                    }

                }

                #endregion

                return result;

            }
        }
    }
}
////    public static class DrawMatches
////    {
////        public static void FindMatch(Mat modelImage, Mat observedImage, out long matchTime, out VectorOfKeyPoint modelKeyPoints, out VectorOfKeyPoint observedKeyPoints, VectorOfVectorOfDMatch matches, out Mat mask, out Mat homography)
////        {
////            int k = 2;
////            double uniquenessThreshold = 0.8;
////            double hessianThresh = 300;

////            Stopwatch watch;
////            homography = null;

////            modelKeyPoints = new VectorOfKeyPoint();
////            observedKeyPoints = new VectorOfKeyPoint();

////#if !__IOS__
////            if (CudaInvoke.HasCuda)
////            {
////                CudaSURF surfCuda = new CudaSURF((float)hessianThresh);
////                using (GpuMat gpuModelImage = new GpuMat(modelImage))
////                //extract features from the object image
////                using (GpuMat gpuModelKeyPoints = surfCuda.DetectKeyPointsRaw(gpuModelImage, null))
////                using (GpuMat gpuModelDescriptors = surfCuda.ComputeDescriptorsRaw(gpuModelImage, null, gpuModelKeyPoints))
////                using (CudaBFMatcher matcher = new CudaBFMatcher(DistanceType.L2))
////                {
////                    surfCuda.DownloadKeypoints(gpuModelKeyPoints, modelKeyPoints);
////                    watch = Stopwatch.StartNew();

////                    // extract features from the observed image
////                    using (GpuMat gpuObservedImage = new GpuMat(observedImage))
////                    using (GpuMat gpuObservedKeyPoints = surfCuda.DetectKeyPointsRaw(gpuObservedImage, null))
////                    using (GpuMat gpuObservedDescriptors = surfCuda.ComputeDescriptorsRaw(gpuObservedImage, null, gpuObservedKeyPoints))
////                    //using (GpuMat tmp = new GpuMat())
////                    //using (Stream stream = new Stream())
////                    {
////                        matcher.KnnMatch(gpuObservedDescriptors, gpuModelDescriptors, matches, k);

////                        surfCuda.DownloadKeypoints(gpuObservedKeyPoints, observedKeyPoints);

////                        mask = new Mat(matches.Size, 1, DepthType.Cv8U, 1);
////                        mask.SetTo(new MCvScalar(255));
////                        Features2DToolbox.VoteForUniqueness(matches, uniquenessThreshold, mask);

////                        int nonZeroCount = CvInvoke.CountNonZero(mask);
////                        if (nonZeroCount >= 4)
////                        {
////                            nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(modelKeyPoints, observedKeyPoints,
////                               matches, mask, 1.5, 20);
////                            if (nonZeroCount >= 4)
////                                homography = Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(modelKeyPoints,
////                                   observedKeyPoints, matches, mask, 2);
////                        }
////                    }
////                    watch.Stop();
////                }
////            }
////            else
////#endif
////            {
////                using (UMat uModelImage = modelImage.GetUMat(AccessType.Read))
////                using (UMat uObservedImage = observedImage.GetUMat(AccessType.Read))
////                {
////                    SURF surfCPU = new SURF(hessianThresh);
////                    //extract features from the object image
////                    UMat modelDescriptors = new UMat();
////                    surfCPU.DetectAndCompute(uModelImage, null, modelKeyPoints, modelDescriptors, false);

////                    watch = Stopwatch.StartNew();

////                    // extract features from the observed image
////                    UMat observedDescriptors = new UMat();
////                    surfCPU.DetectAndCompute(uObservedImage, null, observedKeyPoints, observedDescriptors, false);
////                    BFMatcher matcher = new BFMatcher(DistanceType.L2);
////                    matcher.Add(modelDescriptors);

////                    matcher.KnnMatch(observedDescriptors, matches, k, null);
////                    mask = new Mat(matches.Size, 1, DepthType.Cv8U, 1);
////                    mask.SetTo(new MCvScalar(255));
////                    Features2DToolbox.VoteForUniqueness(matches, uniquenessThreshold, mask);

////                    int nonZeroCount = CvInvoke.CountNonZero(mask);
////                    if (nonZeroCount >= 4)
////                    {
////                        nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(modelKeyPoints, observedKeyPoints,
////                           matches, mask, 1.5, 20);
////                        if (nonZeroCount >= 4)
////                            homography = Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(modelKeyPoints,
////                               observedKeyPoints, matches, mask, 2);
////                    }

////                    watch.Stop();
////                }
////            }
////            matchTime = watch.ElapsedMilliseconds;
////        }

////        /// <summary>
////        /// Draw the model image and observed image, the matched features and homography projection.
////        /// </summary>
////        /// <param name="modelImage">The model image</param>
////        /// <param name="observedImage">The observed image</param>
////        /// <param name="matchTime">The output total time for computing the homography matrix.</param>
////        /// <returns>The model image and observed image, the matched features and homography projection.</returns>
////        public static Mat Draw(Mat modelImage, Mat observedImage, out long matchTime)
////        {
////            Mat homography;
////            VectorOfKeyPoint modelKeyPoints;
////            VectorOfKeyPoint observedKeyPoints;
////            using (VectorOfVectorOfDMatch matches = new VectorOfVectorOfDMatch())
////            {
////                Mat mask;
////                FindMatch(modelImage, observedImage, out matchTime, out modelKeyPoints, out observedKeyPoints, matches,
////                   out mask, out homography);

////                //Draw the matched keypoints
////                Mat result = new Mat();
////                Features2DToolbox.DrawMatches(modelImage, modelKeyPoints, observedImage, observedKeyPoints,
////                   matches, result, new MCvScalar(255, 255, 255), new MCvScalar(255, 255, 255), mask);

////                #region draw the projected region on the image

////                if (homography != null)
////                {
////                    //draw a rectangle along the projected model
////                    Rectangle rect = new Rectangle(Point.Empty, modelImage.Size);
////                    PointF[] pts = new PointF[]
////                    {
////                  new PointF(rect.Left, rect.Bottom),
////                  new PointF(rect.Right, rect.Bottom),
////                  new PointF(rect.Right, rect.Top),
////                  new PointF(rect.Left, rect.Top)
////                    };
////                    pts = CvInvoke.PerspectiveTransform(pts, homography);

////                    Point[] points = Array.ConvertAll<PointF, Point>(pts, Point.Round);
////                    using (VectorOfPoint vp = new VectorOfPoint(points))
////                    {
////                        CvInvoke.Polylines(result, vp, true, new MCvScalar(255, 0, 0, 255), 5);
////                    }

////                }

////                #endregion

////                return result;

////            }
////        }
////    }

//    public static class DrawMatches
//    {
//        public static void FindMatch(Mat modelImage, Mat observedImage, out long matchTime, out VectorOfKeyPoint modelKeyPoints, out VectorOfKeyPoint observedKeyPoints, VectorOfVectorOfDMatch matches, out Mat mask, out Mat homography)
//        {
//            int k = 2;
//            double uniquenessThreshold = 0.80;
//            Stopwatch watch;
//            homography = null;
//            modelKeyPoints = new VectorOfKeyPoint();
//            observedKeyPoints = new VectorOfKeyPoint();
//            using (UMat uModelImage = modelImage.GetUMat(AccessType.Read))
//            using (UMat uObservedImage = observedImage.GetUMat(AccessType.Read))
//            {
//                KAZE featureDetector = new KAZE();

//                //extract features from the object image
//                Mat modelDescriptors = new Mat();
//                featureDetector.DetectAndCompute(uModelImage, null, modelKeyPoints, modelDescriptors, false);

//                watch = Stopwatch.StartNew();

//                // extract features from the observed image
//                Mat observedDescriptors = new Mat();
//                featureDetector.DetectAndCompute(uObservedImage, null, observedKeyPoints, observedDescriptors, false);

//                // Bruteforce, slower but more accurate
//                // You can use KDTree for faster matching with slight loss in accuracy
//                using (Emgu.CV.Flann.LinearIndexParams ip = new Emgu.CV.Flann.LinearIndexParams())
//                using (Emgu.CV.Flann.SearchParams sp = new SearchParams())
//                using (DescriptorMatcher matcher = new FlannBasedMatcher(ip, sp))
//                {
//                    matcher.Add(modelDescriptors);

//                    matcher.KnnMatch(observedDescriptors, matches, k, null);
//                    mask = new Mat(matches.Size, 1, DepthType.Cv8U, 1);
//                    mask.SetTo(new MCvScalar(255));
//                    Features2DToolbox.VoteForUniqueness(matches, uniquenessThreshold, mask);

//                    int nonZeroCount = CvInvoke.CountNonZero(mask);
//                    if (nonZeroCount >= 4)
//                    {
//                        nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(modelKeyPoints, observedKeyPoints,
//                            matches, mask, 1.5, 20);
//                        if (nonZeroCount >= 4)
//                            homography = Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(modelKeyPoints,
//                                observedKeyPoints, matches, mask, 2);
//                    }
//                }
//                watch.Stop();

//            }
//            matchTime = watch.ElapsedMilliseconds;
//        }

//        /// <summary>
//        /// Draw the model image and observed image, the matched features and homography projection.
//        /// </summary>
//        /// <param name="modelImage">The model image</param>
//        /// <param name="observedImage">The observed image</param>
//        /// <param name="matchTime">The output total time for computing the homography matrix.</param>
//        /// <returns>The model image and observed image, the matched features and homography projection.</returns>
//        public static Mat Draw(Mat modelImage, Mat observedImage, out long matchTime)
//        {
//            Mat homography;
//            VectorOfKeyPoint modelKeyPoints;
//            VectorOfKeyPoint observedKeyPoints;
//            using (VectorOfVectorOfDMatch matches = new VectorOfVectorOfDMatch())
//            {
//                Mat mask;
//                FindMatch(modelImage, observedImage, out matchTime, out modelKeyPoints, out observedKeyPoints, matches,
//                   out mask, out homography);

//                //Draw the matched keypoints
//                Mat result = new Mat();
//                Features2DToolbox.DrawMatches(modelImage, modelKeyPoints, observedImage, observedKeyPoints,
//                   matches, result, new MCvScalar(255, 255, 255), new MCvScalar(255, 255, 255), mask);

//                #region draw the projected region on the image

//                if (homography != null)
//                {
//                    //draw a rectangle along the projected model
//                    Rectangle rect = new Rectangle(Point.Empty, modelImage.Size);
//                    PointF[] pts = new PointF[]
//                    {
//                      new PointF(rect.Left, rect.Bottom),
//                      new PointF(rect.Right, rect.Bottom),
//                      new PointF(rect.Right, rect.Top),
//                      new PointF(rect.Left, rect.Top)
//                    };
//                    pts = CvInvoke.PerspectiveTransform(pts, homography);

//#if NETFX_CORE
//                   Point[] points = Extensions.ConvertAll<PointF, Point>(pts, Point.Round);
//#else
//                    Point[] points = Array.ConvertAll<PointF, Point>(pts, Point.Round);
//#endif
//                    using (VectorOfPoint vp = new VectorOfPoint(points))
//                    {
//                        CvInvoke.Polylines(result, vp, true, new MCvScalar(255, 0, 0, 255), 5);
//                    }
//                }
//                #endregion

//                return result;

//            }
//        }
//    }

//    public class FeatureImplementation
//    {
//        public IList<IndecesMapping> Match()
//        {
//            string[] dbImages = { "1.jpg", "2.jpg", "3.jpg" };
//            string queryImage = "query.jpg";

//            IList<IndecesMapping> imap;

//            // compute descriptors for each image
//            var dbDescsList = ComputeMultipleDescriptors(dbImages, out imap);

//            // concatenate all DB images descriptors into single Matrix
//            //Matrix<float> dbDescs = ConcatDescriptors(dbDescsList);

//            // compute descriptors for the query image
//            //Matrix<float> queryDescriptors = ComputeSingleDescriptors(queryImage);
//            IList<Mat> dbDescs = ConcatDescriptors(dbDescsList);
//            FindMatches(dbDescs, queryDescriptors, ref imap);

//            return imap;
//        }

//        /// <summary>
//        /// Computes image descriptors.
//        /// </summary>
//        /// <param name="fileName">Image filename.</param>
//        /// <returns>The descriptors for the given image.</returns>
//        /// public Matrix<float> ComputeSingleDescriptors(string fileName)
//        public Mat ComputeSingleDescriptors(string fileName)
//        {
//            //Matrix<float> descs;
//            var descs = new Mat();
//            VectorOfKeyPoint keyPoints = new VectorOfKeyPoint();
//            using (Image<Gray, Byte> img = new Image<Gray, byte>(fileName))
//            {
//                detector.DetectRaw(img, keyPoints);
//                detector.Compute(img, keyPoints, descs);
//                //VectorOfKeyPoint keyPoints = detector.DetectKeyPointsRaw(img, null);
//                //descs = detector.ComputeDescriptorsRaw(img, null, keyPoints);
//            }

//            return descs;
//        }

//        /// <summary>
//        /// Convenience method for computing descriptors for multiple images.
//        /// On return imap is filled with structures specifying which descriptor ranges in the concatenated matrix belong to what image.
//        /// </summary>
//        /// <param name="fileNames">Filenames of images to process.</param>
//        /// <param name="imap">List of IndecesMapping to hold descriptor ranges for each image.</param>
//        /// <returns>List of descriptors for the given images.</returns>
//        /// public IList<Matrix<float>> ComputeMultipleDescriptors(string[] fileNames, out IList<IndecesMapping> imap)
//        public IList<Mat> ComputeMultipleDescriptors(string[] fileNames, out IList<IndecesMapping> imap)
//        {
//            imap = new List<IndecesMapping>();
//            //IList<Matrix<float>> descs = new List<Matrix<float>>();
//            IList<Mat> descs = new List<Mat>();
//            int r = 0;
//            for (int i = 0; i < fileNames.Length; i++)
//            {
//                var desc = ComputeSingleDescriptors(fileNames[i]);
//                descs.Add(desc);
//                imap.Add(new IndecesMapping
//                {
//                    fileName = fileNames[i],
//                    IndexStart = r,
//                    IndexEnd = r + desc.Rows - 1
//                });
//                r += desc.Rows;
//            }
//            return descs;
//        }

//        /// <summary>
//        /// Computes 'similarity' value (IndecesMapping.Similarity) for each image in the collection against our query image.
//        /// </summary>
//        /// <param name="dbDescriptors">Query image descriptor.</param>
//        /// <param name="queryDescriptors">Consolidated db images descriptors.</param>
//        /// <param name="imap">List of IndecesMapping to hold the 'similarity' value for each image in the collection.</param>
//        public void FindMatches(Matrix<float> dbDescriptors, Matrix<float> queryDescriptors, ref IList<IndecesMapping> imap)
//        {
//            var indices = new Matrix<int>(queryDescriptors.Rows, 2); // matrix that will contain indices of the 2-nearest neighbors found
//            var dists = new Matrix<float>(queryDescriptors.Rows, 2); // matrix that will contain distances to the 2-nearest neighbors found
//            // create FLANN index with 4 kd-trees and perform KNN search over it look for 2 nearest neighbours
//            var flannIndex = new Index(dbDescriptors, 4);
//            flannIndex.KnnSearch(queryDescriptors, indices, dists, 2, 24);
//            for (int i = 0; i < indices.Rows; i++)
//            {
//                // filter out all inadequate pairs based on distance between pairs
//                if (dists.Data[i, 0] < (0.6 * dists.Data[i, 1]))
//                {
//                    // find image from the db to which current descriptor range belongs and increment similarity value.
//                    // in the actual implementation this should be done differently as it's not very efficient for large image collections.
//                    foreach (var img in imap)
//                    {
//                        if (img.IndexStart <= i && img.IndexEnd >= i)
//                        {
//                            img.Similarity++;
//                            break;
//                        }
//                    }
//                }
//            }
//        }

//        /// <summary>
//        /// Concatenates descriptors from different sources (images) into single matrix.
//        /// </summary>
//        /// <param name="descriptors">Descriptors to concatenate.</param>
//        /// <returns>Concatenated matrix.</returns>
//        /// public Matrix<float> ConcatDescriptors(IList<Matrix<float>> descriptors)
//        public Matrix<float> ConcatDescriptors(IList<Mat> descriptors)
//        {
//            int cols = descriptors[0].Cols;
//            int rows = descriptors.Sum(a => a.Rows);
//            float[,] concatedDescs = new float[rows, cols];
//            int offset = 0;
//            foreach (var descriptor in descriptors)
//            {
//                // append new descriptors
//                Buffer.BlockCopy(descriptor.ManagedArray, 0, concatedDescs, offset, sizeof(float) * descriptor.ManagedArray.Length);
//                offset += sizeof(float) * descriptor.ManagedArray.Length;
//            }
//            return new Matrix<float>(concatedDescs);
//        }

//        private const double surfHessianThresh = 300;
//        private const bool surfExtendedFlag = true;
//        private SURF detector = new SURF(surfHessianThresh, 4, 2, true, false);
//    }

//    public class IndecesMapping
//    {
//        public int IndexStart { get; set; }
//        public int IndexEnd { get; set; }
//        public int Similarity { get; set; }
//        public string fileName { get; set; }
//    }
//}
