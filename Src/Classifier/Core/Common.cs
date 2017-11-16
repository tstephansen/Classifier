using Classifier.Data;
using Classifier.Models;
using ClosedXML.Excel;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using Emgu.CV.XFeatures2D;
using Microsoft.Win32;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Parsing;
using Syncfusion.Windows.Forms.PdfViewer;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Classifier.Core
{
    public static class Common
    {
        #region File Methods
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
        #endregion


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
#pragma warning disable S2930 // "IDisposables" should be disposed
                image = Image.FromStream(ms);
#pragma warning restore S2930 // "IDisposables" should be disposed
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
                    CvInvoke.Threshold(image, mdlImage, 127.0, 255.0, ThresholdType.BinaryInv);
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

        public static List<DocumentSelectionModel> LoadDocumentSelectionModels()
        {
            var documentSelectionList = new List<DocumentSelectionModel>();
            using (var context = new ClassifierContext())
            {
                var documentTypes = context.DocumentTypes.ToList();
                foreach (var o in documentTypes)
                {
                    documentSelectionList.Add(new DocumentSelectionModel { DocumentTypeId = o.Id, DocumentType = o.DocumentType, Selected = true });
                }
            }
            return documentSelectionList;
        }

        public static Task<Dictionary<string, string>> CopyImagesToTempFolderAsync(IEnumerable<string> pdfFiles)
        {
            return Task.Run(() =>
            {
                var pdfImages = new Dictionary<string, string>();
                var files = new List<FileInfo>();
                pdfFiles.ForEach(c => files.Add(new FileInfo(c)));
                foreach (var file in files.Where(c => c.Extension == ".png"))
                {
                    var copyPath = Path.Combine(TempStorage, file.Name);
                    File.Copy(file.FullName, copyPath);
                    pdfImages.Add(copyPath, file.FullName);
                }
                return pdfImages;
            });
        }

        public static Task<Dictionary<string, string>> CopyImagesToTempFolderAsync(FileInfo file)
        {
            return Task.Run(() =>
            {
                var pdfImages = new Dictionary<string, string>();
                var copyPath = Path.Combine(TempStorage, file.Name);
                File.Copy(file.FullName, copyPath);
                pdfImages.Add(copyPath, file.FullName);
                return pdfImages;
            });
        }

        public static (List<FileNamingModel>, List<CriteriaImageModel>) SetNamingAndCriteria(string namingSpreadsheetPath)
        {
            var criteriaImages = new List<CriteriaImageModel>();
            var namingModels = new List<FileNamingModel>();
            if (!string.IsNullOrWhiteSpace(namingSpreadsheetPath))
            {
                var spreadsheetDataTable = GetSpreadsheetDataTable(namingSpreadsheetPath, "Reference");
                if (spreadsheetDataTable != null)
                {
                    
                    foreach (DataRow dr in spreadsheetDataTable.Rows)
                    {
                        var serial = string.Empty;
                        var tag = string.Empty;
                        if (!string.IsNullOrWhiteSpace(dr["Serial"].ToString()))
                            serial = dr["Serial"].ToString();
                        if (!string.IsNullOrWhiteSpace(dr["Tag"].ToString()))
                            tag = dr["Tag"].ToString();
                        namingModels.Add(new FileNamingModel { Serial = serial, Tag = tag });
                    }
                }
            }
            var criteriaDirectoryInfo = new DirectoryInfo(CriteriaStorage);
            var criteriaFiles = criteriaDirectoryInfo.GetFiles();
            var images = CreateCriteriaArrays(criteriaFiles);
            criteriaImages.AddRange(images);
            return (namingModels, criteriaImages);
        }

        public static Task<Dictionary<string, string>> ConvertPdfsToImagesAsync(IEnumerable<string> pdfFiles, IProgress<TaskProgress> prog = null, EtaCalculator eta = null)
        {
            return Task.Run(() =>
            {
                var pdfImages = new Dictionary<string, string>();
                var files = new List<FileInfo>();
                pdfFiles.ForEach(c => files.Add(new FileInfo(c)));
                var pdfFilesList = files.Where(c => c.Extension == ".pdf").ToList();
                var currentFile = 0;
                var fileCount = pdfFilesList.Count;
                foreach(var file in pdfFilesList)
                {
                    using (var viewer = new PdfDocumentView())
                    {
                        viewer.Load(file.FullName);
                        var images = viewer.LoadedDocument.ExportAsImage(0, viewer.PageCount - 1, new SizeF(1428, 1848), true);
                        var imgCount = 1;
                        foreach (var image in images)
                        {
                            var imgPath = Path.Combine(TempStorage, $"{file.Name.Substring(0, file.Name.Length - 4)}.{imgCount}.png");
                            pdfImages.Add(imgPath, file.FullName);
                            image.Save(imgPath);
                            imgCount++;
                        }
                    }
                    if (prog == null || eta == null) continue;
                    currentFile++;
                    var rawProgress = (Convert.ToDouble(currentFile) / Convert.ToDouble(fileCount));
                    var progress = rawProgress * 100;
                    var progressFloat = (float)rawProgress;
                    eta.Update(progressFloat);
                    if (eta.ETAIsAvailable)
                    {
                        var timeRemaining = eta.ETR.ToString(@"dd\.hh\:mm\:ss");
                        prog.Report(new TaskProgress
                        {
                            ProgressText = file.Name,
                            ProgressPercentage = progress,
                            ProgressText2 = timeRemaining
                        });
                    }
                    else
                    {
                        prog.Report(new TaskProgress
                        {
                            ProgressText = file.Name,
                            ProgressPercentage = progress,
                            ProgressText2 = "Calculating..."
                        });
                    }
                }
                return pdfImages;
            });
        }

        public static Task SaveMatchedFilesAsync(List<CriteriaMatchModel> matchedFiles, List<FileNamingModel> namingModels, Dictionary<string, string> pdfImages, IEnumerable<string> pdfFiles, CancellationToken token)
        {
            return Task.Run(() =>
            {
                foreach (var item in matchedFiles)
                {
                    if (token.IsCancellationRequested)
                        return;
                    var fileNameWithPage = item.MatchedFileInfo.Name.Substring(0, item.MatchedFileInfo.Name.Length - 4);
                    var fileNameSplit = fileNameWithPage.Split('.');
                    var fileName = fileNameSplit[0];
                    var matchedFile = pdfFiles.First(c => c.Contains(fileName));
                    var matchedFileExtension = matchedFile.Substring(matchedFile.Length - 3);
                    if (matchedFileExtension.Equals("pdf", StringComparison.CurrentCultureIgnoreCase))
                    {
                        ExtractPageFromPdf(item.MatchedFileInfo, item.DocumentType.DocumentType, pdfImages, namingModels);
                    }
                    else
                    {
                        CreatePdfFromImage(item.MatchedFileInfo, item.DocumentType.DocumentType, pdfImages, namingModels);
                    }
                }
            });
        }

        public static void ExtractPageFromPdf(FileInfo file, string type, Dictionary<string, string> pdfImages, List<FileNamingModel> models = null)
        {
            var fileName = file.Name.Substring(0, file.Name.Length - 4);
            if (models != null)
            {
                var fileNameSplit = fileName.Split('.');
                var serialNumber = fileNameSplit[0];
                var model = models.FirstOrDefault(c => c.Serial == serialNumber);
                if (model != null)
                {
                    fileName = model.Tag;
                }
            }
            var origPdf = pdfImages.First(c => c.Key == file.FullName);
            var imagePath = origPdf.Key.Replace(".png", "");
            var pageSplit = imagePath.Split('.');
            var page = Convert.ToInt32(pageSplit[pageSplit.Length - 1]);
            var loadedDocument = new PdfLoadedDocument(origPdf.Value);

            var resultPath = Path.Combine(ResultsStorage, type);
            using (var document = new PdfDocument())
            {
                var startIndex = page - 1;
                var endIndex = page - 1;
                document.ImportPageRange(loadedDocument, startIndex, endIndex);
                var savePath = Path.Combine(resultPath, $"{fileName}.pdf");
                if (File.Exists(savePath))
                {
                    savePath = Path.Combine(resultPath, $"{fileName}-1.pdf");
                }
                document.Save(savePath);
                loadedDocument.Close(true);
                document.Close(true);
            }
        }

        public static void CreatePdfFromImage(FileInfo file, string type, Dictionary<string, string> pdfImages, List<FileNamingModel> models = null)
        {
            var fileName = file.Name.Substring(0, file.Name.Length - 4);
            if (models != null)
            {
                var fileNameSplit = fileName.Split('.');
                var serialNumber = fileNameSplit[0];
                var model = models.FirstOrDefault(c => c.Serial == serialNumber);
                if (model != null)
                {
                    fileName = model.Tag;
                }
            }
            var origPdf = pdfImages.First(c => c.Key == file.FullName);
            var resultPath = Path.Combine(ResultsStorage, type);
            using (var pdf = new PdfDocument())
            {
                var section = pdf.Sections.Add();
                var image = new PdfBitmap(origPdf.Key);
                var frameCount = image.FrameCount;
                for (var i = 0; i < frameCount; i++)
                {
                    var page = section.Pages.Add();
                    section.PageSettings.Margins.All = 0;
                    var graphics = page.Graphics;
                    image.ActiveFrame = i;
                    graphics.DrawImage(image, 0, 0, page.GetClientSize().Width, page.GetClientSize().Height);
                }
                var savePath = Path.Combine(resultPath, $"{fileName}.pdf");
                if (File.Exists(savePath))
                {
                    savePath = Path.Combine(resultPath, $"{fileName}-1.pdf");
                }
                pdf.Save(savePath);
                pdf.Close(true);
            }
        }

        public static readonly string AppStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier";
        public static readonly string TempStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier\\temp";
        public static readonly string PdfPath = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier\\PDFs";
        public static readonly string CriteriaStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier\\Criteria";
        public static readonly string UserCriteriaStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier\\UserCriteria";
        public static readonly string ResultsStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier\\Results";
    }
}
