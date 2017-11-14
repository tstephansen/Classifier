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
    public class DocumentClassification
    {
        public long Classify(VectorOfKeyPoint modelKeyPoints, Mat modelDescriptors, Mat observedImage, double uniquenessThreshold, int k, int detectionType)
        {
            var score = 0L;
            using (var matches = new VectorOfVectorOfDMatch())
            {
                Mat mask = null;
                Mat homography = null;
                var observedKeyPoints = new VectorOfKeyPoint();
                var obsImage = new Mat();
                CvInvoke.Threshold(observedImage, obsImage, 127.0, 255.0, ThresholdType.BinaryInv);
                using (UMat uObservedImage = obsImage.GetUMat(AccessType.Read))
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show("Got umat");
                    });
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
                        case 1:
                            using (var featureDetector = new KAZE())
                            {
                                var observedDescriptors = new Mat();
                                featureDetector.DetectAndCompute(uObservedImage, null, observedKeyPoints, observedDescriptors, false);

                                // KdTree for faster results / less accuracy
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
                    }
                }
            }
            return score;
        }
    }
}
