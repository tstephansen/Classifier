using Classifier.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Classifier.Views
{
    /// <summary>
    /// Interaction logic for CriteriaCreator.xaml
    /// </summary>
    public partial class CriteriaCreator : UserControl
    {
        public CriteriaCreator()
        {
            InitializeComponent();
        }

        private bool _mouseDown = false;
        private Point _mouseDownPosition;
        private Point _mouseUpPosition;

        private void ImageGrid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _mouseDown = false;
            ImageGrid.ReleaseMouseCapture();
            _mouseUpPosition = e.GetPosition(ImageGrid);
            ((CriteriaCreatorViewModel)DataContext).InitialPosition = _mouseDownPosition;
            ((CriteriaCreatorViewModel)DataContext).ReleasePosition = _mouseUpPosition;
            ((CriteriaCreatorViewModel)DataContext).SelectionSize = new System.Drawing.Size(Convert.ToInt32(CriteriaSelectionBox.Width), Convert.ToInt32(CriteriaSelectionBox.Height));
            //System.Console.WriteLine($"Criteria Box: {CriteriaSelectionBox.Width} {CriteriaSelectionBox.Height}");
            //System.Console.WriteLine($"Original: {_mouseDownPosition}   Up: {_mouseUpPosition}");
        }

        private void ImageGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _mouseDown = true;
            _mouseDownPosition = e.GetPosition(ImageGrid);
            ImageGrid.CaptureMouse();

            Canvas.SetLeft(CriteriaSelectionBox, _mouseDownPosition.X);
            Canvas.SetTop(CriteriaSelectionBox, _mouseDownPosition.Y);
            CriteriaSelectionBox.Width = 0;
            CriteriaSelectionBox.Height = 0;
            CriteriaSelectionBox.Visibility = Visibility.Visible;
        }

        private void ImageGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (_mouseDown)
            {
                var mousePosition = e.GetPosition(ImageGrid);
                if(_mouseDownPosition.X < mousePosition.X)
                {
                    Canvas.SetLeft(CriteriaSelectionBox, _mouseDownPosition.X);
                    CriteriaSelectionBox.Width = mousePosition.X - _mouseDownPosition.X;
                }
                else
                {
                    Canvas.SetLeft(CriteriaSelectionBox, mousePosition.X);
                    CriteriaSelectionBox.Width = _mouseDownPosition.X - mousePosition.X;
                }

                if (_mouseDownPosition.Y < mousePosition.Y)
                {
                    Canvas.SetTop(CriteriaSelectionBox, _mouseDownPosition.Y);
                    CriteriaSelectionBox.Height = mousePosition.Y - _mouseDownPosition.Y;
                }
                else
                {
                    Canvas.SetLeft(CriteriaSelectionBox, mousePosition.Y);
                    CriteriaSelectionBox.Height = _mouseDownPosition.Y - mousePosition.Y;
                }
            }
        }
    }
}
