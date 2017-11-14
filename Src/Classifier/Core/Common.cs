using Classifier.Data;
using Classifier.Models;
using ClosedXML.Excel;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using Emgu.CV.XFeatures2D;
using Microsoft.Win32;
using Syncfusion.Windows.Forms.PdfViewer;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.Core
{
    public static class Common
    {
        public static OpenFileDialog BrowseForFiles(string filter)
        {
            return BrowseForFiles(false, filter);
        }

        public static OpenFileDialog BrowseForFiles(bool multiSelect)
        {
            return BrowseForFiles(multiSelect, null);
        }

        public static OpenFileDialog BrowseForFiles(bool multiSelect = false, string filter = null)
        {
            filter = filter == null ? "All files (*.*)|*.*" : $"{filter}|All files (*.*)|*.*";
            var filesDialog = new OpenFileDialog
            {
                Filter = filter,
                FilterIndex = 1,
                RestoreDirectory = true,
                FileName = "",
                Multiselect = multiSelect
            };
            return filesDialog;
        }

        public static void Resize(string imageFile, string outputFile, double scaleFactor)
        {
            using (var srcImage = Image.FromFile(imageFile))
            {
                var newWidth = (int)(srcImage.Width * scaleFactor);
                var newHeight = (int)(srcImage.Height * scaleFactor);
                using (var newImage = new Bitmap(newWidth, newHeight))
                using (var graphics = Graphics.FromImage(newImage))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.DrawImage(srcImage, new Rectangle(0, 0, newWidth, newHeight));
                    newImage.Save(outputFile);
                }
            }
        }

        public static Bitmap ConvertStringToImage(string base64String)
        {
            Image image;
            var bytes = Convert.FromBase64String(base64String);
            using (var ms = new MemoryStream(bytes))
            {
                image = Image.FromStream(ms);
            }
            return (Bitmap)image;
        }

        public static string CreateStringFromImage(string filePath)
        {
            using (Image image = Image.FromFile(filePath))
            {
                using (MemoryStream m = new MemoryStream())
                {
                    image.Save(m, image.RawFormat);
                    var imageBytes = m.ToArray();
                    var base64String = Convert.ToBase64String(imageBytes);
                    return base64String;
                }
            }
        }

        public static Task<DataTable> GetSpreadsheetDataTableAsync(string spreadsheetPath, string spreadsheetName)
        {
            return Task.Run(() =>
            {
                return GetSpreadsheetDataTable(spreadsheetPath, spreadsheetName);
            });
        }

        public static DataTable GetSpreadsheetDataTable(string spreadsheetPath, string spreadsheetName)
        {
            var datatable = new DataTable();
            IXLWorksheet xlWorksheet;
            using (var workbook = new XLWorkbook(spreadsheetPath))
            {
                xlWorksheet = workbook.Worksheet(spreadsheetName);
            }
            var tbl = xlWorksheet.Range(xlWorksheet.FirstCellUsed(), xlWorksheet.LastCellUsed()).AsTable(spreadsheetName);
            var col = tbl.ColumnCount();
            datatable.Clear();
            for (var i = 1; i <= col; i++)
            {
                var column = tbl.Column(i).Cell(1);
                datatable.Columns.Add(column.Value.ToString());
            }
            var firstHeadRow = 0;
            var range = tbl.Range(tbl.FirstCellUsed(), tbl.LastCellUsed());
            foreach (var item in range.Rows())
            {
                if (firstHeadRow != 0)
                {
                    var array = new object[col];
                    for (var y = 1; y <= col; y++)
                    {
                        var cell = item.Cell(y);
                        array[y - 1] = !string.IsNullOrWhiteSpace(cell.FormulaA1) ? cell.ValueCached : cell.Value;
                    }
                    datatable.Rows.Add(array);
                }
                firstHeadRow++;
            }
            return datatable;
        }

        public static Task CreateCriteriaFilesAsync(List<DocumentCriteria> documentCriteria, List<DocumentTypes> types)
        {
            return Task.Run(() =>
            {
                var criteriaDirectoryInfo = new DirectoryInfo(CriteriaStorage);
                var files = criteriaDirectoryInfo.GetFiles();
                foreach (var file in files)
                {
                    File.Delete(file.FullName);
                }
                foreach (var type in types)
                {
                    var criterion = documentCriteria.Where(c => c.DocumentTypeId == type.Id).ToList();
                    foreach (var criteria in criterion)
                    {
                        var image = ConvertStringToImage(criteria.CriteriaBytes);
                        var imagePath = Path.Combine(CriteriaStorage, $"{type.DocumentType}-{criteria.CriteriaName}.png");
                        image.Save(imagePath);
                    }
                }
            });
        }

        public static Dictionary<string, string> ConvertPdfsToImages(List<string> pdfFiles)
        {
            var pdfImages = new Dictionary<string, string>();
            using (var viewer = new PdfDocumentView())
            {
                var files = new List<FileInfo>();
                pdfFiles.ForEach(c => files.Add(new FileInfo(c)));
                foreach (var file in files.Where(c => c.Extension == ".pdf"))
                {
                    viewer.Load(file.FullName);
                    var images = viewer.ExportAsImage(0, viewer.PageCount - 1);
                    var imgCount = 1;
                    foreach (var image in images)
                    {
                        var imgPath = Path.Combine(Common.TempStorage, $"{file.Name.Substring(0, file.Name.Length - 4)}.{imgCount}.png");
                        pdfImages.Add(imgPath, file.FullName);
                        image.Save(imgPath);
                        imgCount++;
                    }
                }
            }
            return pdfImages;
        }

        public static List<CriteriaImageModel> CreateCriteriaArrays(FileInfo[] criteriaFiles)
        {
            var criteriaImages = new List<CriteriaImageModel>();
            foreach (var o in criteriaFiles)
            {
                using (var image = CvInvoke.Imread(o.FullName, ImreadModes.Grayscale))
                {
                    var mdlImage = new Mat();
                    try
                    {
                        CvInvoke.Threshold(image, mdlImage, 127.0, 255.0, ThresholdType.BinaryInv);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            System.Windows.MessageBox.Show(ex.Message.Trim());
                        });
                    }
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        System.Windows.MessageBox.Show("Threshold complete");
                    });
                    var uModelImage = mdlImage.GetUMat(AccessType.Read);
                    var modelDescriptors = new Mat();
                    var modelKeyPoints = new VectorOfKeyPoint();
                    using (var featureDetector = new SIFT(0, 3, 0.04, 10.0, 1.6))
                    {
                        featureDetector.DetectAndCompute(uModelImage, null, modelKeyPoints, modelDescriptors, false);
                    }
                    criteriaImages.Add(new CriteriaImageModel
                    {
                        Info = o,
                        Image = uModelImage,
                        ModelDescriptors = modelDescriptors,
                        ModelKeyPoints = modelKeyPoints
                    });
                }
            }
            return criteriaImages;
        }

        public static void CreateImagePart(string filePath, DocumentCriteria criteria)
        {
            var cropPath = Path.Combine(TempStorage, "cropped.png");
            if (File.Exists(cropPath)) File.Delete(cropPath);
            using (var original = new Bitmap(filePath))
            {
                var originalWidth = original.Width;
                var originalHeight = original.Height;
                var positionX = criteria.PositionX;
                var positionY = criteria.PositionY;
                var scaleFactorX = 1.0;
                var scaleFactorY = 1.0;
                if ((criteria.BaseWidth != 0 && originalWidth != 0) && criteria.BaseWidth != originalWidth)
                    scaleFactorX = ConvertDimensions(criteria.BaseWidth, originalWidth);
                if ((criteria.BaseHeight != 0 && originalHeight != 0) && criteria.BaseHeight != originalHeight)
                    scaleFactorY = ConvertDimensions(criteria.BaseHeight, originalHeight);
                var scaledPositionX = criteria.PositionX / scaleFactorX;
                var scaledPositionY = criteria.PositionY / scaleFactorY;
                var scaledWidth = criteria.Width / scaleFactorX;
                var scaledHeight = criteria.Height / scaleFactorY;
                if ((scaledWidth + criteria.PositionX) > originalWidth)
                {
                    positionX = Convert.ToInt32(scaledPositionX);
                }
                if ((scaledHeight + criteria.PositionY) > originalHeight)
                {
                    positionY = Convert.ToInt32(scaledPositionY);
                }
                var scaledSize = new Size(Convert.ToInt32(scaledWidth), Convert.ToInt32(scaledHeight));
                var startPoint = new Point(positionX, positionY);
                var rect = new Rectangle(startPoint, scaledSize);
                var croppedImage = (Bitmap)original.Clone(rect, original.PixelFormat);
                croppedImage.Save(cropPath);
            }
        }

        public static double ConvertDimensions(int first, int second)
        {
            var doubleFirst = Convert.ToDouble(first);
            var doubleSecond = Convert.ToDouble(second);
            var value = doubleFirst / doubleSecond;
            return value;
        }

        public static readonly string AppStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier";
        public static readonly string TempStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier\\temp";
        public static readonly string PdfPath = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier\\PDFs";
        public static readonly string CriteriaStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier\\Criteria";
        public static readonly string UserCriteriaStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier\\UserCriteria";
        public static readonly string ResultsStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier\\Results";

        //public static readonly string AppStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier";
        //public static readonly string TempStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\temp";
        //public static readonly string PdfPath = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\PDFs";
        //public static readonly string CriteriaStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\Criteria";
        //public static readonly string UserCriteriaStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\UserCriteria";
        //public static readonly string ResultsStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\Results";
    }
}
