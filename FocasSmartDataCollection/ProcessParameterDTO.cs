using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FocasSmartDataCollection
{
    class ProcessParameterDTO
    {
        public string ParameterID { get; set; }
        public string ParameterName { get; set; }
        public string DisplayText { get; set; }
        public string DataReadLocation { get; set; }
        public string AddditionalQualifier { get; set; }
        public int PullingFreq { get; set; }
        public int ParameterValue { get; set; }

    }
}
