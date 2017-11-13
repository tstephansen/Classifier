using Classifier.Models;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Flann;
using Emgu.CV.ML;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Emgu.CV.XFeatures2D;
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
    /// <summary>
    /// This code is mostly modified core from various examples.
    /// </summary>
    public class DocClassifier
    {
        public DocClassifier()
        {

        }

        #region Test Methods
        private void FindMatchModified(Mat modelImage, Mat observedImage, double uniquenessThreshold, int k, out VectorOfKeyPoint modelKeyPoints, out VectorOfKeyPoint observedKeyPoints, VectorOfVectorOfDMatch matches, out Mat mask, out Mat homography, out long score, int detectionType)
        {
            homography = null;
            modelKeyPoints = new VectorOfKeyPoint();
            observedKeyPoints = new VectorOfKeyPoint();
            var mdlImage = new Mat();
            var obsImage = new Mat();
            CvInvoke.Threshold(modelImage, mdlImage, 127, 255, ThresholdType.BinaryInv);
            CvInvoke.Threshold(observedImage, obsImage, 127, 255, ThresholdType.BinaryInv);
            using (UMat uModelImage = mdlImage.GetUMat(AccessType.Read))
            using (UMat uObservedImage = obsImage.GetUMat(AccessType.Read))
            {
                DetectFeatures(uModelImage, uObservedImage, modelKeyPoints, observedKeyPoints, uniquenessThreshold, k, matches, out mask, out homography, out score, detectionType);
            }
        }

        public static void DetectFeatures(UMat uModelImage, UMat uObservedImage, VectorOfKeyPoint modelKeyPoints, VectorOfKeyPoint observedKeyPoints, double uniquenessThreshold, int k, VectorOfVectorOfDMatch matches, out Mat mask, out Mat homography, out long score, int detectionType)
        {
            homography = null;
            switch (detectionType)
            {
                case 0:
                    using (var featureDetector = new SIFT(0, 3, 0.04, 10.0, 1.6))
                    {
                        var modelDescriptors = new Mat();
                        featureDetector.DetectAndCompute(uModelImage, null, modelKeyPoints, modelDescriptors, false);
                        var observedDescriptors = new Mat();
                        featureDetector.DetectAndCompute(uObservedImage, null, observedKeyPoints, observedDescriptors, false);

                        using (var ip = new KdTreeIndexParams())
                        using (var sp = new SearchParams())
                        using (DescriptorMatcher matcher = new FlannBasedMatcher(ip, sp))
                        {
                            matcher.Add(modelDescriptors);
                            matcher.KnnMatch(observedDescriptors, matches, k, null);
                            mask = new Mat(matches.Size, 1, DepthType.Cv8U, 1);
                            mask.SetTo(new MCvScalar(255));
                            Features2DToolbox.VoteForUniqueness(matches, uniquenessThreshold, mask);
                            // Calculate score based on matches size
                            score = 0;
                            for (int i = 0; i < matches.Size; i++)
                            {
                                if (mask.GetData(i)[0] == 0) continue;
                                foreach (var e in matches[i].ToArray())
                                    ++score;
                            }
                            var nonZeroCount = CvInvoke.CountNonZero(mask);
                            if (nonZeroCount >= 4)
                            {
                                nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(modelKeyPoints, observedKeyPoints, matches, mask, 1.5, 20);
                                if (nonZeroCount >= 4)
                                    homography = Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(modelKeyPoints, observedKeyPoints, matches, mask, 2);
                            }
                        }

                    }
                    break;
                default:
                case 1:
                    using (var featureDetector = new KAZE())
                    {
                        var modelDescriptors = new Mat();
                        featureDetector.DetectAndCompute(uModelImage, null, modelKeyPoints, modelDescriptors, false);
                        var observedDescriptors = new Mat();
                        featureDetector.DetectAndCompute(uObservedImage, null, observedKeyPoints, observedDescriptors, false);

                        // KdTree for faster results / less accuracy
                        using (var ip = new LinearIndexParams())
                        using (var sp = new SearchParams())
                        using (DescriptorMatcher matcher = new FlannBasedMatcher(ip, sp))
                        {
                            matcher.Add(modelDescriptors);
                            matcher.KnnMatch(observedDescriptors, matches, k, null);
                            mask = new Mat(matches.Size, 1, DepthType.Cv8U, 1);
                            mask.SetTo(new MCvScalar(255));
                            Features2DToolbox.VoteForUniqueness(matches, uniquenessThreshold, mask);
                            // Calculate score based on matches size
                            score = 0;
                            for (int i = 0; i < matches.Size; i++)
                            {
                                if (mask.GetData(i)[0] == 0) continue;
                                foreach (var e in matches[i].ToArray())
                                    ++score;
                            }
                            var nonZeroCount = CvInvoke.CountNonZero(mask);
                            if (nonZeroCount >= 4)
                            {
                                nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(modelKeyPoints, observedKeyPoints, matches, mask, 1.5, 20);
                                if (nonZeroCount >= 4)
                                    homography = Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(modelKeyPoints, observedKeyPoints, matches, mask, 2);
                            }
                        }
                    }
                    break;
            }
        }

        public long ProcessImage(Mat modelImage, Mat observedImage, double uniquenessThreshold, int k, int detectionType)
        {
            var score = 0L;
            Mat homography = null;
            var modelKeyPoints = new VectorOfKeyPoint();
            var observedKeyPoints = new VectorOfKeyPoint();
            using (var matches = new VectorOfVectorOfDMatch())
            {
                FindMatchModified(modelImage, observedImage, uniquenessThreshold, k, out modelKeyPoints, out observedKeyPoints, matches, out Mat mask, out homography, out score, detectionType);
                return score;
            }
        }

        public Mat ProcessImageAndShowResult(Mat modelImage, Mat observedImage, double uniquenessThreshold, int k, int detectionType)
        {
            var score = 0L;
            Mat homography = null;
            var modelKeyPoints = new VectorOfKeyPoint();
            var observedKeyPoints = new VectorOfKeyPoint();
            using (var matches = new VectorOfVectorOfDMatch())
            {
                FindMatchModified(modelImage, observedImage, uniquenessThreshold, k, out modelKeyPoints, out observedKeyPoints, matches, out Mat mask, out homography, out score, detectionType);
                Mat result = new Mat();
                Features2DToolbox.DrawMatches(modelImage, modelKeyPoints, observedImage, observedKeyPoints,
                   matches, result, new MCvScalar(0, 0, 0), new MCvScalar(0, 0, 0), mask);
                Draw(homography, result, modelImage);
                return result;
            }
        }
        #endregion

        #region Other Methods
        public int DetermineMatch(Mat modelImage, Mat observedImage, double uniquenessThreshold, int k, out long matchTime)
        {
            var homography = new Mat();
            var modelKeyPoints = new VectorOfKeyPoint();
            var observedKeyPoints = new VectorOfKeyPoint();
            using (VectorOfVectorOfDMatch matches = new VectorOfVectorOfDMatch())
            {
                var result = new Mat();
                FindMatch(modelImage, observedImage, out matchTime, out modelKeyPoints, out observedKeyPoints, matches, out Mat mask, out homography, uniquenessThreshold, k);
                Features2DToolbox.DrawMatches(modelImage, modelKeyPoints, observedImage, observedKeyPoints, matches, result, new MCvScalar(0, 0, 0), new MCvScalar(0, 0, 0), mask);
                var matchMatrix = new Matrix<Byte>(mask.Rows, mask.Cols);
                mask.CopyTo(matchMatrix);
                var totalMatches = CountMatches(matchMatrix);
                return totalMatches;
            }
        }

        public static int CountMatches(Matrix<byte> mask)
        {
            var matched = mask.ManagedArray;
            var list = matched.OfType<byte>().ToList();
            var count = list.Count(a => a.Equals(1));
            return count;
        }

        public Mat Classify(Mat modelImage, Mat observedImage, double uniquenessThreshold, int k, out long matchTime)
        {
            Mat homography;
            VectorOfKeyPoint modelKeyPoints;
            VectorOfKeyPoint observedKeyPoints;
            using (VectorOfVectorOfDMatch matches = new VectorOfVectorOfDMatch())
            {
                Mat mask;
                FindMatch(modelImage, observedImage, out matchTime, out modelKeyPoints, out observedKeyPoints, matches,
                   out mask, out homography, uniquenessThreshold, k);

                // Draw
                Mat result = new Mat();

                Features2DToolbox.DrawMatches(modelImage, modelKeyPoints, observedImage, observedKeyPoints,
                   matches, result, new MCvScalar(0, 0, 0), new MCvScalar(0, 0, 0), mask);

                Draw(homography, result, modelImage);

                return result;
            }
        }

        /// <summary>
        /// Draws the results on the image.
        /// </summary>
        /// <param name="homography"></param>
        /// <param name="result"></param>
        /// <param name="modelImage"></param>
        public void Draw(Mat homography, Mat result, Mat modelImage)
        {
            if (homography != null)
            {
                var rect = new Rectangle(Point.Empty, modelImage.Size);
                var pts = new PointF[]
                {
                      new PointF(rect.Left, rect.Bottom),
                      new PointF(rect.Right, rect.Bottom),
                      new PointF(rect.Right, rect.Top),
                      new PointF(rect.Left, rect.Top)
                };
                pts = CvInvoke.PerspectiveTransform(pts, homography);
                var points = Array.ConvertAll(pts, Point.Round);
                using (VectorOfPoint vp = new VectorOfPoint(points))
                {
                    CvInvoke.Polylines(result, vp, true, new MCvScalar(255, 0, 0, 255), 5);
                }
            }
        }

        public void FindMatch(Mat modelImage, Mat observedImage, out long matchTime, out VectorOfKeyPoint modelKeyPoints, out VectorOfKeyPoint observedKeyPoints, VectorOfVectorOfDMatch matches, out Mat mask, out Mat homography, double uniquenessThreshold, int k)
        {
            Stopwatch watch;
            homography = null;
            modelKeyPoints = new VectorOfKeyPoint();
            observedKeyPoints = new VectorOfKeyPoint();
            using (var uModelImage = modelImage.GetUMat(AccessType.Read))
            using (var uObservedImage = observedImage.GetUMat(AccessType.Read))
            {
                using (var featureDetector = new KAZE())
                {

                    //extract features from the object image
                    var modelDescriptors = new Mat();
                    featureDetector.DetectAndCompute(uModelImage, null, modelKeyPoints, modelDescriptors, false);
                    watch = Stopwatch.StartNew();
                    // extract features from the observed image
                    Mat observedDescriptors = new Mat();
                    featureDetector.DetectAndCompute(uObservedImage, null, observedKeyPoints, observedDescriptors, false);
                    // Bruteforce, slower but more accurate
                    // You can use KDTree for faster matching with slight loss in accuracy
                    using (Emgu.CV.Flann.LinearIndexParams ip = new Emgu.CV.Flann.LinearIndexParams())
                    using (Emgu.CV.Flann.SearchParams sp = new SearchParams())
                    using (DescriptorMatcher matcher = new FlannBasedMatcher(ip, sp))
                    {
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
                    }
                }
                watch.Stop();
            }
            matchTime = watch.ElapsedMilliseconds;
        }
        #endregion
    }
}