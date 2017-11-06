using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// Interaction logic for Classify.xaml
    /// </summary>
    public partial class Classify : UserControl
    {
        public Classify()
        {
            InitializeComponent();
            var dm = DesignerProperties.GetIsInDesignMode(this);
            if (dm)
            {
                var color = Color.FromArgb(255, 250, 250, 250);
                Background = new SolidColorBrush(color);
            }
        }
    }
}
