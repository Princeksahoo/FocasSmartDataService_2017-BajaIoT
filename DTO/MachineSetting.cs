using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DTO
{

    public class SPCCharacteristics
    {
        public SPCCharacteristics()
        {
            this.DiamentionValue = double.MaxValue;
        }
        public short MacroLocation { get; set; }
        public short FeatureID { get; set; }
        public short DiamentionId { get; set; }
        public double DiamentionValue { get; set; }

    }

    public class TPMString
    {
        public Int64 Seq { get; set; }
        public string TpmString { get; set; }
        public DateTime DateTime { get; set; }

    }

    public class TPMMacroLocation
    {
        public short StatusMacro { get; set; }
        public short StartLocation { get; set; }
        public short EndLocation { get; set; }
    }

    public class MachineSetting
    {        
        public ushort CoolantOilLocationStart { get; set; }
        public ushort CoolantOilLocationEnd { get; set; }
        public short LocationTargetStart { get; set; }
        public short LocationTargetEnd { get; set; }
        public short LocationActualStart { get; set; }
        public short LocationActualEnd { get; set; }

        public short LocationTargetStartSubSpindle { get; set; }
        public short LocationTargetEndSubSpindle { get; set; }
        public short LocationActualStartSubSpindle { get; set; }
        public short LocationActualEndSubSpindle { get; set; }

        //public bool IsAlarmHistoryEnabled { get; set; }
        //public bool IsOperationHistoryEnabled { get; set; }
        //public bool IsExternalOperatorHistoryEnabled { get; set; }
        //public bool IsSpindleLoadSpeedEnabled { get; set; }
        //public bool IsToolLifeEnabled { get; set; }
        //public bool IsCoolentLubOilLevelEnabled { get; set; }
        //public bool IsTPMTrakDataCollectionEnabled { get; set; }
        //public bool IsPMCSinnalStatusReadingEnabled { get; set; }
        
        public short MachineMLocation { get; set; }
        public short ComponentMLocation { get; set; }
        public short OperationMLocation { get; set; }
        public short OperatorMLocation { get; set; }

        public short TPMFlagMLocation { get; set; }
        public short TPMStartMLocation { get; set; }
        public short TPMEndMLocation { get; set; }

        public short SpindleAxisNumber { get; set; }
        public short PartsCountUsingMacro { get; set; }

        public int TimeIntervalInMinute { get; set; }
        public int ReadCuttingForCycles { get; set; }

        public List<short> ProcessParameterInputMacroLocation { get; set; }
        public List<short> ProcessParameterOutputMacroLocation { get; set; }
        public List<TPMMacroLocation> TPMDataMacroLocations { get; set; }
        public List<PredictiveMaintenanceDTO> PredictiveMaintenanceSettings { get; set; }
        public List<ProcessParameterDTO> ProcessParameterSettings { get; set; }
        public List<ProcessParameterDTO_BAJAJ> LiveDashboard_Bajaj { get; set; }
        public List<ProcessParameterDTO_BAJAJ> GrindingCycleMonitoring_Bajaj { get; set; }
        public List<ProcessParameterDTO_BAJAJ> LoadScreen_Bajaj { get; set; }
        public List<ProcessParameterDTO_BAJAJ> GrindingApplication_Bajaj { get; set; }
        public GrindingCyclemonitoring_Bajaj grinding_Bajaj { get; set; }
    }

    public class PredictiveMaintenanceDTO
    {
        public string MachineId { get; set; }
        public int AlarmNo { get; set; }
        public ushort TargetDLocation { get; set; }
        public ushort CurrentValueDLocation { get; set; }
        public int TargetValue { get; set; }
        public int ActualValue { get; set; }
        public DateTime TimeStamp { get; set; }
        
    }

    public class ServiceSettingsVals
    {
        public int SpindleDataInterval { get; set; }
        public int LiveDataInterval { get; set; }
        public int AlarmDataInterval { get; set; }
        public int OperationHistoryInterval { get; set; }
        public string ProgramDownloadPath { get; set; }
        public string OperationHistoryFileDownloadPath { get; set; }
        
    }

    public class ProcessParameterDTO_SAC
    {
        public string MacroVariable { get; set; }
        public string Type { get; set; }
        public double Value { get; set; }
        public DateTime BatchTS { get; set; }
        public string MachineId { get; set; }
    }

    public class ProcessParameterDTO
    {
        public string MachineID { get; set; }
        public int ParameterID { get; set; }
        public ushort RLocation { get; set; }
        public double ParameterBitValue { get; set; }
        public DateTime UpdatedtimeStamp { get; set; }

    }
    public class ProcessParameterDTO_BAJAJ 
    {
        public string MachineID { get; set; }
        public string ParameterID { get; set; }
        public string ParameterName { get; set; }
        public string RLocation { get; set; }
        public int PullingFreq { get; set; }
        public string AdditionalQualifier { get; set; }
        public string ParameterValue { get; set; }
        public string UpdatedtimeStamp { get; set; }


    }

    public class GrindingCyclemonitoring_Bajaj
    {
        public ushort RLocationForGrindingCycles { get; set; }
        public ushort RLocationForGrindingFeedRate { get; set; }

    }

    public class ProcessParameterTransactionDTO_Bajaj
    {
        public string MachineID { get; set; }
        public string ParameterID { get; set; }
        public string ParameterName { get; set; }
        public string ParameterValue { get; set; }
        public string Part { get; set; }
        public string Opn { get; set; }
        public string Qualifier { get; set; }
        public string ProgramNo { get; set; }
        [BsonElement]        
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime UpdatedtimeStamp { get; set; }
    }

}

