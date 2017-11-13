using Classifier.Core;
using Emgu.CV;
using Emgu.CV.UI;
using LandmarkDevs.Core.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.ViewModels
{
    public class TestingViewModel : Observable
    {
        public TestingViewModel()
        {
            TestCommand = new RelayCommand(Test);
            TestTwoCommand = new RelayCommand(TestTwo);
            _testGround = new TestGround();
            ImagePath = @"C:\Users\Tim\Downloads\Classifier Stuff\Results\Working\Combined Certs_Page_0008.png";
        }

        public IRelayCommand TestCommand { get; }
        public IRelayCommand TestTwoCommand { get; }
        
        public void Test()
        {
            using(var image = CvInvoke.Imread(ImagePath, Emgu.CV.CvEnum.ImreadModes.Grayscale))
            {
                var results = _testGround.Thresholding(image);
                foreach(var o in results)
                {
                    ImageViewer.Show(o);
                }
            }
        }

        public void TestTwo()
        {
            using (var image = CvInvoke.Imread(ImagePath, Emgu.CV.CvEnum.ImreadModes.Grayscale))
            {
                var result = _testGround.AdaptiveThreshold(image);
                ImageViewer.Show(result);
            }
        }

        public string ImagePath
        {
            get => _imagePath;
            set => Set(ref _imagePath, value);
        }
        private string _imagePath;

        private TestGround _testGround;
    }
}
