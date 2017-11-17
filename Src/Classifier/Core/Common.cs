using Classifier.Data;
using Classifier.Models;
using ClosedXML.Excel;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using Emgu.CV.XFeatures2D;
using Microsoft.Win32;
using NLog;
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
                Parallel.ForEach(pdfFilesList, (file) =>
                {
                    try
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
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message.Trim());
                        System.Diagnostics.Debug.WriteLine(ex.Message.Trim());
                    }
                    finally
                    {
                        if (prog != null || eta != null)
                        {
                            lock (prog)
                            {
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
                        }
                    }
                });
                return pdfImages;
            });
        }

        public static readonly string AppStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier";
        public static readonly string LogStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier\\Logs";
        public static readonly string TempStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier\\temp";
        public static readonly string PdfPath = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier\\PDFs";
        public static readonly string CriteriaStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier\\Criteria";
        public static readonly string UserCriteriaStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier\\UserCriteria";
        public static readonly string ResultsStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\Classifier\\Results";

        public static ILogger Logger { get; set; }
    }
}
