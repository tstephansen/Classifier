using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Flann;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Emgu.CV.XFeatures2D;
using System;
using System.Drawing;
#pragma warning disable S125

namespace Classifier.Core
{
    public static class DocumentClassification
    {
        //public static long Classify(VectorOfKeyPoint modelKeyPoints, Mat modelDescriptors, Mat observedImage, double uniquenessThreshold, int k, int detectionType)
        public static long Classify(Mat modelDescriptors, Mat observedImage, double uniquenessThreshold, int k, int detectionType)
        {
            var score = 0L;
            using (var matches = new VectorOfVectorOfDMatch())
            {
                Mat mask = null;
                //Mat homography = null;
                var observedKeyPoints = new VectorOfKeyPoint();
                var obsImage = new Mat();
                CvInvoke.Threshold(observedImage, obsImage, 127.0, 255.0, ThresholdType.BinaryInv);
                using (UMat uObservedImage = obsImage.GetUMat(AccessType.Read))
                {
                    switch (detectionType)
                    {
                        default:
                            using (var featureDetector = new SIFT(0, 3, 0.04, 10.0, 1.6))
                            {
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
                                    score = 0;
                                    for (int i = 0; i < matches.Size; i++)
                                    {
                                        if (mask.GetData(i)[0] == 0) continue;
                                        foreach (var e in matches[i].ToArray())
                                            ++score;
                                    }
                                    //var nonZeroCount = CvInvoke.CountNonZero(mask);
                                    //if (nonZeroCount >= 4)
                                    //{
                                    //    nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(modelKeyPoints, observedKeyPoints, matches, mask, 1.5, 20);
                                    //    if (nonZeroCount >= 4)
                                    //        homography = Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(modelKeyPoints, observedKeyPoints, matches, mask, 2);
                                    //}
                                }

                            }
                            break;
                        case 1:
                            using (var featureDetector = new KAZE())
                            {
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
                                    score = 0;
                                    for (int i = 0; i < matches.Size; i++)
                                    {
                                        if (mask.GetData(i)[0] == 0) continue;
                                        foreach (var e in matches[i].ToArray())
                                            ++score;
                                    }
                                    //var nonZeroCount = CvInvoke.CountNonZero(mask);
                                    //if (nonZeroCount >= 4)
                                    //{
                                    //    nonZeroCount = Features2DToolbox.VoteForSizeAndOrientation(modelKeyPoints, observedKeyPoints, matches, mask, 1.5, 20);
                                    //    if (nonZeroCount >= 4)
                                    //        homography = Features2DToolbox.GetHomographyMatrixFromMatchedFeatures(modelKeyPoints, observedKeyPoints, matches, mask, 2);
                                    //}
                                }
                            }
                            break;
                    }
                }
            }
            return score;
        }

        public static Mat ClassifyForDrawing(Mat modelImage, Mat observedImage, double uniquenessThreshold, int k, out VectorOfKeyPoint modelKeyPoints, out VectorOfKeyPoint observedKeyPoints, out VectorOfVectorOfDMatch matches, out Mat homography, out long score)
        {
            var detectionType = 0;
            score = 0L;
            Mat mask = null;
            homography = null;
            modelKeyPoints = new VectorOfKeyPoint();
            observedKeyPoints = new VectorOfKeyPoint();
            var modelDescriptors = new Mat();
            var observedDescriptors = new Mat();
            var mdlImage = new Mat();
            var obsImage = new Mat();
            matches = new VectorOfVectorOfDMatch();
            CvInvoke.Threshold(modelImage, mdlImage, 100.0, 255.0, ThresholdType.BinaryInv);
            CvInvoke.Threshold(observedImage, obsImage, 100.0, 255.0, ThresholdType.BinaryInv);
            using (UMat uModelImage = mdlImage.GetUMat(AccessType.Read))
            using (UMat uObservedImage = obsImage.GetUMat(AccessType.Read))
            {
                switch (detectionType)
                {
                    default:
                        using (var featureDetector = new SIFT(0, 3, 0.04, 10.0, 1.6))
                        {
                            featureDetector.DetectAndCompute(uModelImage, null, modelKeyPoints, modelDescriptors, false);
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
                    case 1:
                        using (var featureDetector = new KAZE())
                        {
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
            return mask;
        }

        public static Mat ClassifyAndShowResult(Mat modelImage, Mat observedImage, double uniquenessThreshold, int k, out long score)
        {
            VectorOfKeyPoint modelKeyPoints = null;
            VectorOfKeyPoint observedKeyPoints = null;
            VectorOfVectorOfDMatch matches = null;
            Mat homography = null;
            score = 0;
            var mask = ClassifyForDrawing(modelImage, observedImage, uniquenessThreshold, k, out modelKeyPoints, out observedKeyPoints, out matches, out homography, out score);
            var result = new Mat();
            Features2DToolbox.DrawMatches(modelImage, modelKeyPoints, observedImage, observedKeyPoints,
                   matches, result, new MCvScalar(0, 0, 0), new MCvScalar(0, 0, 0), mask);
            Draw(homography, result, modelImage);
            return result;
        }

        private static void Draw(Mat homography, Mat result, Mat modelImage)
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
    }
}
