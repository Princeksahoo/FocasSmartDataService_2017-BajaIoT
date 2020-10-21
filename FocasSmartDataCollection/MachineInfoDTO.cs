using System;
using System.Collections.Generic;
using System.Text;
using DTO;

namespace FocasSmartDataCollection
{

    public class MachineInfoDTO
    {
        #region private
        private string _ip;       
        private int _portNo;
        private string _interfaceId;
        private string _machineId;
        public string  MTB { get; set; }

        #endregion

        public MachineSetting Settings { get; set; }

        public string IpAddress
        {
            get { return _ip; }
            set { _ip = value; }
        }
        public int PortNo
        {
            get { return _portNo; }
            set { _portNo = value; }
        }
        public string MachineId
        {
            get { return _machineId; }
            set { _machineId = value; }
        }

        public string InterfaceId
        {
            get { return _interfaceId; }
            set { _interfaceId = value; }
        }       
    }

    
}
