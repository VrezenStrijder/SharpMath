using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SharpMath.Core;

namespace SharpMath.UI.ViewModels
{
    public partial class MatrixViewModel : ObservableObject
    {
        [ObservableProperty] 
        private string name;

        [ObservableProperty] 
        private string matrixText;
    }

}
