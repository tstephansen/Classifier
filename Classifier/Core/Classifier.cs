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
        private void FindMatchModified(Mat modelImage, Mat observedImage, double uniquenessThreshold, int k, out VectorOfKeyPoint modelKeyPoints, out VectorOfKeyPoint observedKeyPoints, VectorOfVectorOfDMatch matches, out Mat mask, out Mat homography, out long score)
        {
            homography = null;
            modelKeyPoints = new VectorOfKeyPoint();
            observedKeyPoints = new VectorOfKeyPoint();
            using (UMat uModelImage = modelImage.GetUMat(AccessType.Read))
            using (UMat uObservedImage = observedImage.GetUMat(AccessType.Read))
            {
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
            }
        }

        public long ProcessImage(Mat modelImage, Mat observedImage, double uniquenessThreshold, int k)
        {
            var score = 0L;
            Mat homography = null;
            var modelKeyPoints = new VectorOfKeyPoint();
            var observedKeyPoints = new VectorOfKeyPoint();
            using (var matches = new VectorOfVectorOfDMatch())
            {
                FindMatchModified(modelImage, observedImage, uniquenessThreshold, k, out modelKeyPoints, out observedKeyPoints, matches, out Mat mask, out homography, out score);
                return score;
            }
        }

        public Mat ProcessImageAndShowResult(Mat modelImage, Mat observedImage, double uniquenessThreshold, int k)
        {
            var score = 0L;
            Mat homography = null;
            var modelKeyPoints = new VectorOfKeyPoint();
            var observedKeyPoints = new VectorOfKeyPoint();
            using (var matches = new VectorOfVectorOfDMatch())
            {
                FindMatchModified(modelImage, observedImage, uniquenessThreshold, k, out modelKeyPoints, out observedKeyPoints, matches, out Mat mask, out homography, out score);
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

    #region Old Code
    //    public class DocClassifierExample
    //    {

    //        public int SiftMatch(Mat modelImage, Mat observedImage, double uniquenessThreshold, int k)
    //        {
    //            VectorOfKeyPoint modelKeyPoints = new VectorOfKeyPoint();
    //            VectorOfKeyPoint observedKeyPoints = new VectorOfKeyPoint();
    //            var modelDescriptors = new Mat();
    //            var obsDescriptors = new Mat();
    //            var mask = new Mat();
    //            using (var sift = new SIFT())
    //            {
    //                sift.DetectAndCompute(modelImage, null, modelKeyPoints, modelDescriptors, false);
    //                sift.DetectAndCompute(observedImage, null, observedKeyPoints, obsDescriptors, false);
    //                using (VectorOfVectorOfDMatch matches = new VectorOfVectorOfDMatch())
    //                {
    //                    using (var ip = new LinearIndexParams())
    //                    using (var sp = new SearchParams())
    //                    using (var flann = new FlannBasedMatcher(ip, sp))
    //                    {
    //                        flann.KnnMatch(modelDescriptors, matches, 2, null);
    //                        mask = new Mat(matches.Size, 1, DepthType.Cv8U, 1);
    //                        mask.SetTo(new MCvScalar(255));
    //                        Features2DToolbox.VoteForUniqueness(matches, uniquenessThreshold, mask);

    //                    }
    //                }
    //                //using (DescriptorMatcher matcher = new FlannBasedMatcher(ip, sp))
    //                //{
    //                //    var matches = matcher.Flann
    //                //}
    //            }
    //        }
    //        public void Train(Bitmap bmp = null)
    //        {
    //            int trainSampleCount = 100;

    //            #region Generate the traning data and classes
    //            var trainData = new Matrix<float>(trainSampleCount, 2);
    //            var trainClasses = new Matrix<float>(trainSampleCount, 1);
    //            Image<Bgr, byte> img;
    //            if (bmp == null) img = new Image<Bgr, byte>(500, 500);
    //            else img = new Image<Bgr, byte>(bmp);
    //            var sample = new Matrix<float>(1, 2);
    //            Matrix<float> prediction = new Matrix<float>(1, 1);

    //            Matrix<float> trainData1 = trainData.GetRows(0, trainSampleCount >> 1, 1);
    //            trainData1.SetRandNormal(new MCvScalar(200), new MCvScalar(50));
    //            Matrix<float> trainData2 = trainData.GetRows(trainSampleCount >> 1, trainSampleCount, 1);
    //            trainData2.SetRandNormal(new MCvScalar(300), new MCvScalar(50));

    //            Matrix<float> trainClasses1 = trainClasses.GetRows(0, trainSampleCount >> 1, 1);
    //            trainClasses1.SetValue(1);
    //            Matrix<float> trainClasses2 = trainClasses.GetRows(trainSampleCount >> 1, trainSampleCount, 1);
    //            trainClasses2.SetValue(2);
    //            #endregion

    //            using (Matrix<int> layerSize = new Matrix<int>(new int[] { 2, 5, 1 }))
    //            using (Mat layerSizeMat = layerSize.Mat)
    //            using (TrainData td = new TrainData(trainData, Emgu.CV.ML.MlEnum.DataLayoutType.RowSample, trainClasses))
    //            using (ANN_MLP network = new ANN_MLP())
    //            {
    //                network.SetLayerSizes(layerSizeMat);
    //                network.SetActivationFunction(ANN_MLP.AnnMlpActivationFunction.SigmoidSym, 0, 0);
    //                network.TermCriteria = new MCvTermCriteria(10, 1.0e-8);
    //                network.SetTrainMethod(ANN_MLP.AnnMlpTrainMethod.Backprop, 0.1, 0.1);
    //                network.Train(td, (int)Emgu.CV.ML.MlEnum.AnnMlpTrainingFlag.Default);

    //#if !NETFX_CORE
    //                String fileName = Path.Combine(Path.GetTempPath(), "ann_mlp_model.xml");
    //                network.Save(fileName);
    //                if (File.Exists(fileName))
    //                    File.Delete(fileName);
    //#endif

    //                for (int i = 0; i < img.Height; i++)
    //                {
    //                    for (int j = 0; j < img.Width; j++)
    //                    {
    //                        sample.Data[0, 0] = j;
    //                        sample.Data[0, 1] = i;
    //                        network.Predict(sample, prediction);

    //                        // estimates the response and get the neighbors' labels
    //                        float response = prediction.Data[0, 0];

    //                        // highlight the pixel depending on the accuracy (or confidence)
    //                        img[i, j] = response < 1.5 ? new Bgr(90, 0, 0) : new Bgr(0, 90, 0);
    //                    }
    //                }
    //            }

    //            // display the original training samples
    //            for (int i = 0; i < (trainSampleCount >> 1); i++)
    //            {
    //                PointF p1 = new PointF(trainData1[i, 0], trainData1[i, 1]);
    //                img.Draw(new CircleF(p1, 2), new Bgr(255, 100, 100), -1);
    //                PointF p2 = new PointF((int)trainData2[i, 0], (int)trainData2[i, 1]);
    //                img.Draw(new CircleF(p2, 2), new Bgr(100, 255, 100), -1);
    //            }

    //            Emgu.CV.UI.ImageViewer.Show(img);
    //        }
    //    }
    #endregion
}
