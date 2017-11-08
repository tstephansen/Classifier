using Classifier.Data;
using ClosedXML.Excel;
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
                    //graphics.PixelOffsetMode = PixelOffsetMode.Default;
                    //graphics.InterpolationMode = InterpolationMode.Default;
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
            });
        }

        public static Task CreateCriteriaFilesAsync(List<DocumentCriteria> documentCriteria, List<DocumentTypes> types)
        {
            return Task.Run(() =>
            {
                var criteriaDirectoryInfo = new DirectoryInfo(Common.CriteriaStorage);
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
                        var image = Common.ConvertStringToImage(criteria.CriteriaBytes);
                        var imagePath = Path.Combine(Common.CriteriaStorage, $"{type.DocumentType}-{criteria.CriteriaName}.png");
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

        public static string AppStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier";
        public static string TempStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\temp";
        public static string PdfPath = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\PDFs";
        public static string CriteriaStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\Criteria";
        public static string UserCriteriaStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\UserCriteria";
        public static string ResultsStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\Results";
    }
}
