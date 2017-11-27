using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Web;

namespace FaceAPI_MVC.Web.Models
{
    public class MultiFaceDetectionModal
    {
        public MultiFaceDetectionModal()
        {
            this.Items = new List<FaceDetectionModal>();
        }

        public IList<FaceDetectionModal> Items { get; }
    }

    public class FaceDetectionModal {

        public ObservableCollection<vmFace> DetectedFaces { get; set; }

        public ObservableCollection<vmFace> ResultCollection { get; set; }
    }
}