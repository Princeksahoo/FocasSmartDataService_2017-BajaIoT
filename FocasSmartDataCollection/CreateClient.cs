using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading;
using FocasLib;
using FocasLibrary;
using System.Linq;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections;
using System.Text.RegularExpressions;
using MachineConnectLicenseDTO;
using S7.Net;
using System.Security.AccessControl;
using DTO;
using MongoDB.Driver;

namespace FocasSmartDataCollection
{
    public class CreateClient
    {
        private string ipAddress;
        private ushort portNo;
        private string machineId;
        private string interfaceId;
        private string MName;
        private short AddressPartSCountFromMacro = 0;
        private short _CompMacroLocation = 0;
        private short _OpnMacroLocation = 0;
        private int dressingReadCount = 0;
        private int grindingReadCount = 0;
        private int serviceStartedMinute = DateTime.Now.Minute;
        DateTime ReadTime = DateTime.MinValue;

        private short InspectionDataReadFlag = 0;
        private bool enableSMSforProgramChange = false;
        public string MachineName
        {
            get { return machineId; }
        }
        DTO.MachineSetting setting = default(DTO.MachineSetting);
        MachineInfoDTO machineDTO = default(MachineInfoDTO);
        private static string appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        CncMachineType _cncMachineType = CncMachineType.cncUnknown;
        string _cncSeries = string.Empty;

        //LIC details
        bool _isCNCIdReadSuccessfully = true;//**********************************TODO - change to false before check-in
        private bool _isLicenseValid = true;

        private List<SpindleSpeedLoadDTO> _spindleInfoQueue = new List<SpindleSpeedLoadDTO>();
        private List<LiveDTO> _liveDTOQueue = new List<LiveDTO>();
        private string _operationHistoryFolderPath = string.Empty;
        private int _timeDelayMainThread = 0;
        private string _programDownloadFolder = string.Empty;
        private int _DownloadFreq = 60;
        private Hashtable _AutoDownloadedSavedPrograms = new Hashtable();
        private bool _AutoDownloadEveryTimeIfNotSameAsMaster = true;
        private static DateTime _serviceStartedTimeStamp = DateTime.Now;
        private static DateTime _nextLicCheckedTimeStamp = _serviceStartedTimeStamp;
        bool _prevAlarmStatus = false;
        byte CycleMonitor = 0;
        byte FeedRateMonitor = 0;
        short cycleTimeMacroLocation = 0;
        short CycleStarted = 0;
        short grindApplicationFlag = 0;
        short GrindingFeedRate = 0;
        private short GrindingStartMCode = 0;
        private short GrindingEndMCode = 0;
        private short DressingStartMCode = 0;
        private short DressingEndMCode = 0;
        private short ApproachFeedRate = 0;
        private short RoughingFeedRate = 0;
        private short SemiFinishingFeedRate = 0;
        private short FinishingFeedRate = 0;
        private short DressingFeedRate = 0;
        private int SparkOutTime = 0;
        private short CycleEnded = 0;

        private ushort RAddrStart = 0;
        private ushort RAddrEnd = 0;
        private DataTable paramList = null;

        private ushort RAddrStart2 = 0;
        private ushort RAddrEnd2 = 0;
        private DataTable paramList2 = null;

        private Timer _timerAlarmHistory = null;
        private Timer _timerOperationHistory = null;
        private Timer _timerSpindleLoadSpeed = null;
        private Timer _timerTPMTrakDataCollection = null;
        private Timer _timerPredictiveMaintenanceReader = null;
        private Timer _timerOffsetHistoryReader = null;
        private Timer _timerCycletimeReader = null;
        private Timer _timerToolLife = null;
        private Timer _timerProcessParameter = null;
        private Timer _timerProcessParameter_FOF = null;
        private Timer _timerSignalStatusReader = null;
        private Timer _timerSignalStatusReader2 = null;
        private Timer _timerProcessParameter_BAJAJ = null;
        private Timer _timerGrindingCyclemonitoring_BAJAJ = null;
        private Timer _timerFeedRate_BAJAJ = null;

        object _lockerAlarmHistory = new object();
        object _lockerOperationHistory = new object();
        object _lockerTPMTrakDataCollection = new object();
        object _lockerSpindleLoadSpeed = new object();
        object _lockerPredictiveMaintenance = new object();
        object _lockerOffsetHistory = new object();
        object _lockerCycletimeReader = new object();
        object _lockerSignalStatus = new object();
        object _lockerSignalStatus2 = new object();
        object _lockerToolLife = new object();
        object _lockerMachineParamer_MGTL = new object();
        object _lockerProcessParameter_FOF = new object();
        object _lockerProcessParameter_BAJAJ = new object();
        object _lockerGrindingCycleMonitoring = new object();
        object _lockeFeedRate = new object();

        static volatile object _lockerReleaseMemory = new object();
        static DateTime CDT = DateTime.Now.Date.AddDays(1);
        List<ushort> _focasHandles = new List<ushort>();
        bool _isOEMVersion = false;
        List<OffsetHistoryDTO> offsetHistoryList = new List<OffsetHistoryDTO>();
        List<LiveAlarm> _liveAlarmsGlobal = new List<LiveAlarm>();
        List<int> offsetHistoryRange = new List<int>();
        List<LiveAlarm> liveAlarmsLocal = new List<LiveAlarm>();

        private string cycleStartMacro = "";
        private ushort cycleStartMacroU = 0;
        private ushort cycleStartMacroBit = 0;

        public CreateClient(MachineInfoDTO machine)
        {
            this.ipAddress = machine.IpAddress;
            this.portNo = (ushort)machine.PortNo;
            this.machineId = machine.MachineId;
            this.MName = this.machineId;
            this.interfaceId = machine.InterfaceId;
            this.setting = machine.Settings;
            this.machineDTO = machine;
            var appSettings = DatabaseAccess.GetServiceSettingsData();

            _operationHistoryFolderPath = appSettings.OperationHistoryFileDownloadPath;
            AddressPartSCountFromMacro = this.machineDTO.Settings.PartsCountUsingMacro;
            _programDownloadFolder = appSettings.ProgramDownloadPath;


            _timeDelayMainThread = (int)TimeSpan.FromSeconds(appSettings.LiveDataInterval).TotalMilliseconds;
            if (_timeDelayMainThread <= 4000) _timeDelayMainThread = 4000;

            int alaramsHistoryTimerDelay = (int)TimeSpan.FromMinutes(appSettings.AlarmDataInterval).TotalMilliseconds;
            if (alaramsHistoryTimerDelay > 0 && alaramsHistoryTimerDelay < (int)TimeSpan.FromMinutes(1).TotalMilliseconds)
                alaramsHistoryTimerDelay = (int)TimeSpan.FromMinutes(1).TotalMilliseconds;


            int spindleLoadSpeedTimerDelay = 0;
            if (appSettings.SpindleDataInterval >= 0 && appSettings.SpindleDataInterval < 1)
            {
                spindleLoadSpeedTimerDelay = (int)TimeSpan.FromSeconds(1).TotalMilliseconds;
            }
            else if (appSettings.SpindleDataInterval >= 1)
            {
                spindleLoadSpeedTimerDelay = (int)TimeSpan.FromSeconds(appSettings.SpindleDataInterval).TotalMilliseconds;
            }


            int operationHistoryTimerDelay = (int)TimeSpan.FromMinutes(appSettings.OperationHistoryInterval).TotalMilliseconds;
            if (operationHistoryTimerDelay > 0 && operationHistoryTimerDelay < (int)TimeSpan.FromMinutes(30).TotalMilliseconds)
                operationHistoryTimerDelay = (int)TimeSpan.FromMinutes(30).TotalMilliseconds;

            int predictiveMaintenanceDelay = 0;
            int.TryParse(ConfigurationManager.AppSettings["TimeDealyForPredictiveMaintenance"].ToString(), out predictiveMaintenanceDelay);
            if (predictiveMaintenanceDelay > 0)
            {
                predictiveMaintenanceDelay = (int)TimeSpan.FromMinutes(predictiveMaintenanceDelay).TotalMilliseconds;
            }

            int offsetHistoryReaderDelay = 0;
            int.TryParse(ConfigurationManager.AppSettings["TimeDealyForReadOffsetHistory"].ToString(), out offsetHistoryReaderDelay);
            if (offsetHistoryReaderDelay > 0)
            {
                if (offsetHistoryReaderDelay < 30) offsetHistoryReaderDelay = 30;
                offsetHistoryReaderDelay = (int)TimeSpan.FromSeconds(offsetHistoryReaderDelay).TotalMilliseconds;
            }

            int GrindingCyclemonitoringDelay = 0;
            int.TryParse(ConfigurationManager.AppSettings["TimeDelayForGrindingCyclemonitoring"].ToString(), out GrindingCyclemonitoringDelay);
            if (GrindingCyclemonitoringDelay > 0)
            {
                GrindingCyclemonitoringDelay = (int)TimeSpan.FromMinutes(GrindingCyclemonitoringDelay).TotalMilliseconds;
            }
            int processParameterDelay = 0;
            int.TryParse(ConfigurationManager.AppSettings["TimeDelayForProcessParameter"].ToString(), out processParameterDelay);
            if (processParameterDelay > 0)
            {
                processParameterDelay = (int)TimeSpan.FromMinutes(processParameterDelay).TotalMilliseconds;
            }

            if (Int32.TryParse(ConfigurationManager.AppSettings["DownloadFreq"], out _DownloadFreq))
            {
                if (_DownloadFreq <= 10) _DownloadFreq = 10;
            }
            else
            {
                _DownloadFreq = 60;
            }

            bool.TryParse(ConfigurationManager.AppSettings["AutoDownloadEveryTimeIfNotSameAsMaster"], out _AutoDownloadEveryTimeIfNotSameAsMaster);
            bool.TryParse(ConfigurationManager.AppSettings["EnableSMSforProgramChange"], out enableSMSforProgramChange);
            short.TryParse(ConfigurationManager.AppSettings["CycleTimeMacroLocationStartingLocation"].ToString(), out cycleTimeMacroLocation);

            int timeDelayCycleData = 0;
            int.TryParse(ConfigurationManager.AppSettings["TimeIntervalForCycleData"].ToString(), out timeDelayCycleData);
            if (timeDelayCycleData > 0)
            {
                timeDelayCycleData = (int)TimeSpan.FromSeconds(timeDelayCycleData).TotalMilliseconds;
            }

            int tpmTrakDataCollectionTimerDelay = 0;
            int.TryParse(ConfigurationManager.AppSettings["TPMTrakDataCollectionTimeDelay"], out tpmTrakDataCollectionTimerDelay);
            if (tpmTrakDataCollectionTimerDelay > 0)
            {
                if (tpmTrakDataCollectionTimerDelay < 10) tpmTrakDataCollectionTimerDelay = 10;
                tpmTrakDataCollectionTimerDelay = (int)TimeSpan.FromSeconds(tpmTrakDataCollectionTimerDelay).TotalMilliseconds;
            }

            int processMachineParametersTimerDelay_MGTL = 0;
            if (Int32.TryParse(ConfigurationManager.AppSettings["TimeDelayMachineParametersThread_MGTL"], out processMachineParametersTimerDelay_MGTL))
            {
                if (processMachineParametersTimerDelay_MGTL > 0)
                    processMachineParametersTimerDelay_MGTL = (int)TimeSpan.FromMinutes(processMachineParametersTimerDelay_MGTL).TotalMilliseconds;
            }


            //Timers

            if (alaramsHistoryTimerDelay > 0)
                _timerAlarmHistory = new Timer(GetAlarmsData, null, 1000 * 10, 1000 * 60 * 5);

            if (spindleLoadSpeedTimerDelay > 0)
                _timerSpindleLoadSpeed = new Timer(GetSpindleLoadSpeedData, null, 1000 * 1, 1000);

            // BAJAJ Process Parameter
            if (processMachineParametersTimerDelay_MGTL > 0)
                _timerProcessParameter = new Timer(GetMachineParameterData_MGTL, null, 2000, processMachineParametersTimerDelay_MGTL);

            if (GrindingCyclemonitoringDelay > 0)
                _timerGrindingCyclemonitoring_BAJAJ = new Timer(ReadGrindingCyclemonitoring_Bajaj, null, 1000 * 3, 1000);

            if (processParameterDelay > 0)
                _timerProcessParameter_BAJAJ = new Timer(ReadLiveProcessParameter_Bajaj, null, 1000 * 5, 4000);

            //_timerFeedRate_BAJAJ = new Timer(ReadFeedRate_Bajaj, null, 1000, 1);

        }

        public void GetClient()
        {
            ushort focasLibHandleMain = ushort.MinValue;
            bool IsConnected = false;

            string _previousProgramNumber = string.Empty;
            int _previousProgramCount;
            DateTime _previousBatchTS;

            int _previousMachineUpDownStatus = int.MinValue;
            DateTime _previousCNCtimeStamp = DateTime.MinValue;
            DateTime _previousUpDownStatusBatchTS = DateTime.MinValue;

            DateTime _programNextDownloadTime = DateTime.Now.AddMinutes(1);
            var plantId = DatabaseAccess.GetPlantIDForMachine(this.machineId);
            _programDownloadFolder = Path.Combine(_programDownloadFolder, plantId, this.machineId);
            DatabaseAccess.GetPartsCountAndBatchTS(this.machineId, out _previousProgramNumber, out _previousProgramCount, out _previousBatchTS, out _previousCNCtimeStamp, out _previousMachineUpDownStatus, out _previousUpDownStatusBatchTS);

            Logger.WriteDebugLog(string.Format("Thread {0} started for data collection.", machineId));
            bool SendCumulativeOffsetCorrectionValue = false;
            bool.TryParse(ConfigurationManager.AppSettings["SendCumulativeOffsetCorrectionValue"].ToString(), out SendCumulativeOffsetCorrectionValue);

            decimal IgnoreOffsetCorrectionForValueLessThan = 0.0M;
            decimal.TryParse(ConfigurationManager.AppSettings["IgnoreOffsetCorrectionForValueLessThan"].ToString(), out IgnoreOffsetCorrectionForValueLessThan);

            //TODO - satya
            bool LiveDataEnabled = false;
            bool.TryParse(ConfigurationManager.AppSettings["LiveDataEnabled"].ToString(), out LiveDataEnabled);

            bool OffsetCorrectionEnabled = false;
            bool.TryParse(ConfigurationManager.AppSettings["OffsetCorrectionEnabled"].ToString(), out OffsetCorrectionEnabled);

            bool enableAutoProgramDownload = false;
            bool.TryParse(ConfigurationManager.AppSettings["EnableAutoProgramDownload"].ToString(), out enableAutoProgramDownload);


            Int16.TryParse(ConfigurationManager.AppSettings["CompMacrolocation"].ToString(), out _CompMacroLocation);
            Int16.TryParse(ConfigurationManager.AppSettings["OpnMacrolocation"].ToString(), out _OpnMacroLocation);
            Int16.TryParse(ConfigurationManager.AppSettings["InspectionDataReadFlag"].ToString(), out InspectionDataReadFlag);
            bool compareSubProgramInPATH2 = ConfigurationManager.AppSettings["CampareSubProgramInPATH2"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase);

            short ret = 0;
            string machineStatus = string.Empty;
            string currentAmps = string.Empty;
            FocasLibBase.ODBDY2_1 dynamic_data = new FocasLibBase.ODBDY2_1();
            LiveDTO live = default(LiveDTO);
            if (_timerOffsetHistoryReader != null)
            {
                offsetHistoryRange = GetOffsetRange();
                foreach (int i in offsetHistoryRange)
                {
                    offsetHistoryList.Add(new OffsetHistoryDTO { MachineID = this.machineId, OffsetNo = i });
                }
            }

            while (true)
            {
                focasLibHandleMain = ushort.MinValue;
                try
                {
                    #region stop_service
                    if (ServiceStop.stop_service == 1)
                    {
                        try
                        {
                            Logger.WriteDebugLog("stopping the service. coming out of main while loop.");
                            break;
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteErrorLog(ex.Message);
                            break;
                        }
                    }
                    #endregion

                    #region clean up process started
                    if (CDT < DateTime.Now)
                    {
                        if (Monitor.TryEnter(_lockerReleaseMemory, TimeSpan.FromMilliseconds(100)))
                        {
                            try
                            {
                                if (CDT < DateTime.Now)
                                {
                                    CDT = CDT.AddDays(10);
                                    Logger.WriteDebugLog("clean up process started.");
                                    CleanUpProcess.DeleteFiles("Logs", this.ipAddress);
                                    if (_isOEMVersion)
                                    {
                                        DatabaseAccess.DeleteTableData(7, "Focas_LiveData");
                                        DatabaseAccess.DeleteTableData(4, "Focas_SpindleInfo");
                                        DatabaseAccess.DeleteTableData(7, "CompressData");
                                    }
                                    else
                                    {
                                        //delete the data keeping 30 days of data.... 
                                        DatabaseAccess.DeleteTableData(60, "Focas_LiveData");
                                        DatabaseAccess.DeleteTableData(10, "Focas_SpindleInfo");
                                        DatabaseAccess.DeleteTableData(60, "Focas_PredictiveMaintenance");
                                        DatabaseAccess.DeleteTableData(60, "Focas_ToolOffsetHistory");
                                        DatabaseAccess.DeleteTableData(60, "CompressData");
                                    }
                                    GC.Collect();
                                    GC.WaitForPendingFinalizers();
                                    Thread.Sleep(1000);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.WriteErrorLog(ex.ToString());
                            }
                            finally
                            {
                                Monitor.Exit(_lockerReleaseMemory);
                            }
                        }
                    }
                    #endregion

                    try
                    {
                        Ping ping = new Ping();
                        PingReply reply = ping.Send(ipAddress, 4000);
                        if (reply.Status == IPStatus.Success)
                        {
                            #region LicenseCheck
                            if (!_isCNCIdReadSuccessfully)
                            {
                                string cncId = string.Empty;
                                List<string> cncIdList = FocasSmartDataService.licInfo.CNCData.Where(s => s.CNCdata1 != null).Select(s => s.CNCdata1).ToList();
                                _isLicenseValid = this.ValidateCNCSerialNo(this.machineId, this.ipAddress, this.portNo, cncIdList, out _isCNCIdReadSuccessfully, out cncId);
                                _isLicenseValid = true; // sim:

                                if (!_isLicenseValid)
                                {
                                    if (_isCNCIdReadSuccessfully)
                                    {
                                        Logger.WriteErrorLog("Lic Validation failed. Please contact AMIT/MMT.");
                                        break;
                                    }
                                    Thread.Sleep(TimeSpan.FromSeconds(10.0));
                                    continue;
                                }
                                //update table 
                                if (_isLicenseValid)
                                {
                                    var cncDataList = FocasSmartDataService.licInfo.CNCData.Where(s => s.CNCdata1 != null && s.CNCdata1 == cncId).Select(s => s).ToList();
                                    var cncDataList2 = cncDataList.Where(s => s.IsOEM == false).Select(s => s).FirstOrDefault();
                                    if (cncDataList2 != null)
                                        _isOEMVersion = false;
                                    else
                                    {
                                        _isOEMVersion = true;
                                    }

                                    CNCData cncData = FocasSmartDataService.licInfo.CNCData.Where(s => s.CNCdata1 == cncId).Select(s => s).FirstOrDefault();
                                    cncData.IsOEM = _isOEMVersion;
                                    //_isOEMVersion = cncData.IsOEM;
                                    DatabaseAccess.UpdateMachineInfo(this.machineId, FocasSmartDataService.licInfo.LicType, FocasSmartDataService.licInfo.ExpiresAt, cncData);
                                    //this.ValidateMachineModel(this.machineId, this.ipAddress, this.portNo);
                                    this.SetCNCDateTime(this.machineId, this.ipAddress, this.portNo);

                                }
                            }
                            if (FocasSmartDataService.licInfo != null && FocasSmartDataService.licInfo.LicType != null && FocasSmartDataService.licInfo.LicType.Equals("Trial"))
                            {
                                if (_nextLicCheckedTimeStamp <= DateTime.Now)
                                {
                                    if (Utility.GetNetworkTime().Date >= DateTime.Parse(FocasSmartDataService.licInfo.ExpiresAt))
                                    {
                                        Logger.WriteErrorLog("Trial license expires. Please contact MMT/AMIT Pvt Ltd.");
                                        ServiceStop.stop_service = 1;
                                        _isLicenseValid = false;
                                        break;
                                    }
                                    int totalServiceruntime = DatabaseAccess.GetServiceRuntime();
                                    if (totalServiceruntime >= Math.Abs((DateTime.Parse(FocasSmartDataService.licInfo.StartDate) - DateTime.Parse(FocasSmartDataService.licInfo.ExpiresAt)).TotalHours))
                                    {
                                        Logger.WriteErrorLog("Trial license expires. Please contact MMT/AMIT Pvt Ltd.");
                                        ServiceStop.stop_service = 1;
                                        _isLicenseValid = false;
                                        break;
                                    }
                                    if (_serviceStartedTimeStamp != _nextLicCheckedTimeStamp)
                                    {
                                        DatabaseAccess.UpdateServiceRuntime(totalServiceruntime + 1);
                                    }
                                    _nextLicCheckedTimeStamp = DateTime.Now.AddHours(1.0);
                                }
                            }
                            #endregion

                            if (OffsetCorrectionEnabled)
                            {
                                #region OffsetCorrection
                                //Start Offset Correction
                                //call stored proc to check if any records to process                               
                                OffserCorrectionDTO offsetCorrection = DatabaseAccess.GetOffsetCorrectionValue(this.interfaceId);
                                if (offsetCorrection.Result != int.MinValue)
                                {
                                    int count = 0;
                                    ret = -88;
                                    while (ret != 0)
                                    {
                                        ret = FocasData.cnc_allclibhndl3(ipAddress, portNo, 1, out focasLibHandleMain);
                                        count++;
                                        Logger.WriteDebugLog("try connecting " + count + " . ret value = " + ret);
                                        if (ret == 0)
                                        {
                                            IsConnected = true;
                                        }
                                        if (ret == -16)
                                        {
                                            Logger.WriteDebugLog("try connecting using SOCKET " + count);
                                            TcpClient tcpClient = new TcpClient();
                                            Socket socket = tcpClient.Client;
                                            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 2000);
                                            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 5000);
                                            socket.SendBufferSize = 1500;
                                            socket.ReceiveBufferSize = 1500;
                                            socket.NoDelay = true;
                                            socket.LingerState = new LingerOption(true, 10);
                                            Logger.WriteDebugLog("socket is connecting...");
                                            try
                                            {
                                                socket.Connect(ipAddress, portNo);
                                                Logger.WriteDebugLog("socket has Connected.");
                                            }
                                            catch (Exception ex)
                                            {
                                                Logger.WriteErrorLog("Exception while connecting to CNC machine using SOCKET. Message = " + ex.Message);
                                            }
                                            if (socket != null && socket.Connected)
                                            {
                                                socket.Shutdown(SocketShutdown.Both);
                                                socket.Close();
                                            }
                                        }
                                        if (count >= 20) break;
                                    }
                                    if (ret == 0)
                                    {
                                        if (offsetCorrection.Result == 1)
                                        {
                                            decimal offsetCorrectionValue = 0.0M;
                                            offsetCorrectionValue = offsetCorrection.OffsetCorrectionValue;

                                            bool sendCorrectionToMachine = true;
                                            if (IgnoreOffsetCorrectionForValueLessThan > 0)
                                            {
                                                sendCorrectionToMachine = false;
                                                if (Math.Abs(offsetCorrectionValue) > IgnoreOffsetCorrectionForValueLessThan)
                                                {
                                                    sendCorrectionToMachine = true;
                                                }
                                            }
                                            if (sendCorrectionToMachine)
                                            {
                                                //decimal offsetValueAtMachineBeforeWrite = (decimal)ReadWearOffsetFromCNC(offsetCorrection.OffsetLocation, _focasLibHandle);
                                                double tempValue = ReadWearOffsetFromCNC(offsetCorrection.OffsetLocation, focasLibHandleMain);
                                                if (tempValue.ToString() != double.NaN.ToString())
                                                {
                                                    decimal offsetValueAtMachineBeforeWrite = (decimal)tempValue;
                                                    Logger.WriteDebugLog("Offset Value at Machine before write = " + offsetValueAtMachineBeforeWrite);

                                                    if (SendCumulativeOffsetCorrectionValue)
                                                    {
                                                        offsetCorrectionValue = offsetCorrection.OffsetCorrectionValue + offsetValueAtMachineBeforeWrite;
                                                    }
                                                    //send the offset correction value to CNC machine
                                                    ret = WriteWearOffsetToCNC(offsetCorrectionValue, offsetCorrection.OffsetLocation, focasLibHandleMain);
                                                    if (ret != 0)
                                                    {
                                                        IsConnected = false;
                                                        Logger.WriteErrorLog(" WriteWearOffsetToCNC failed. Ret = " + ret);
                                                    }

                                                    //decimal offsetValueAtMachineAfterWrite = (decimal)ReadWearOffsetFromCNC(offsetCorrection.OffsetLocation, _focasLibHandle);
                                                    tempValue = ReadWearOffsetFromCNC(offsetCorrection.OffsetLocation, focasLibHandleMain);
                                                    if (tempValue.ToString() != double.NaN.ToString())
                                                    {
                                                        decimal offsetValueAtMachineAfterWrite = (decimal)tempValue;
                                                        Logger.WriteDebugLog("Offset Value at Machine AFTER write = " + offsetValueAtMachineAfterWrite);
                                                        //update the MeasuredValue in Focas_WearOffsetCorrection table
                                                        DatabaseAccess.InsertNewOffsetVal(offsetCorrection.OffsetCorrectionMasterID, offsetCorrection.MeasuredValue, offsetCorrection.OffsetCorrectionValue, offsetValueAtMachineAfterWrite, offsetCorrection.ResultText);
                                                    }
                                                    else
                                                    {
                                                        IsConnected = false;
                                                        Logger.WriteDebugLog("ReadWearOffsetFromCNC failed.");
                                                    }
                                                    //update the tables Inspection_Autodata
                                                    DatabaseAccess.UpdateInspectionAutodata(offsetCorrection.SampleID, 1, this.interfaceId);
                                                    Logger.WriteDebugLog("Offset Correction Result = " + offsetCorrection.ResultText);
                                                }
                                                else
                                                {
                                                    IsConnected = false;
                                                    Logger.WriteDebugLog("ReadWearOffsetFromCNC failed.");
                                                }
                                            }
                                            else
                                            {
                                                //decimal offsetValueAtMachineBeforeWrite = (decimal)ReadWearOffsetFromCNC(offsetCorrection.OffsetLocation, _focasLibHandle);
                                                double tempValue = ReadWearOffsetFromCNC(offsetCorrection.OffsetLocation, focasLibHandleMain);
                                                if (tempValue.ToString() != double.NaN.ToString())
                                                {
                                                    decimal offsetValueAtMachineBeforeWrite = (decimal)tempValue;
                                                    DatabaseAccess.InsertNewOffsetVal(offsetCorrection.OffsetCorrectionMasterID, offsetCorrection.MeasuredValue, offsetCorrection.OffsetCorrectionValue, offsetValueAtMachineBeforeWrite,
                                                                                       string.Format("Correction {0} to be done is less than {1}. Ignore the correction.", offsetCorrectionValue, IgnoreOffsetCorrectionForValueLessThan));
                                                    //update the status to database.
                                                    Logger.WriteDebugLog(string.Format("Correction {0} to be done is less than {1}. Ignore the correction.", offsetCorrectionValue, IgnoreOffsetCorrectionForValueLessThan));
                                                    //update the tables Inspection_Autodata
                                                    DatabaseAccess.UpdateInspectionAutodata(offsetCorrection.SampleID, 3, this.interfaceId);
                                                }
                                                else
                                                {
                                                    IsConnected = false;
                                                    Logger.WriteDebugLog("ReadWearOffsetFromCNC failed.");
                                                }
                                            }
                                        }
                                        else if (offsetCorrection.Result == 2)
                                        {
                                            //decimal offsetValueAtMachineBeforeWrite = (decimal)ReadWearOffsetFromCNC(offsetCorrection.OffsetLocation, _focasLibHandle);
                                            double tempValue = ReadWearOffsetFromCNC(offsetCorrection.OffsetLocation, focasLibHandleMain);
                                            if (tempValue.ToString() != double.NaN.ToString())
                                            {
                                                decimal offsetValueAtMachineBeforeWrite = (decimal)tempValue;
                                                DatabaseAccess.InsertNewOffsetVal(offsetCorrection.OffsetCorrectionMasterID, offsetCorrection.MeasuredValue, offsetCorrection.OffsetCorrectionValue, offsetValueAtMachineBeforeWrite, offsetCorrection.ResultText);
                                                //update the status to database.
                                                Logger.WriteDebugLog("OffsetCorrection Result = " + offsetCorrection.ResultText);
                                                //update the tables Inspection_Autodata
                                                DatabaseAccess.UpdateInspectionAutodata(offsetCorrection.SampleID, 2, this.interfaceId);
                                            }
                                            else
                                            {
                                                IsConnected = false;
                                                Logger.WriteDebugLog("ReadWearOffsetFromCNC failed.");
                                            }
                                        }
                                        ret = FocasData.cnc_freelibhndl(focasLibHandleMain);
                                        if (ret != 0) _focasHandles.Add(focasLibHandleMain);
                                        Logger.WriteDebugLog("Closing connection. ret = " + ret);
                                        focasLibHandleMain = ushort.MinValue;
                                    }
                                    else
                                    {
                                        Logger.WriteDebugLog(string.Format("Not able to connect to CNC. Return value from function cnc_allclibhndl3 --> Handle value = {0}", ret));
                                        //Thread.Sleep(1000 * 1);
                                    }
                                    if (focasLibHandleMain != ushort.MinValue)
                                    {
                                        ret = FocasData.cnc_freelibhndl(focasLibHandleMain);
                                        if (ret != 0) _focasHandles.Add(focasLibHandleMain);
                                        Logger.WriteDebugLog("Closing connection. ret = " + ret);
                                        focasLibHandleMain = ushort.MinValue;
                                        IsConnected = false;
                                    }
                                }
                                //end offset correction 
                                #endregion
                            }

                            #region LiveDataCollection
                            if (LiveDataEnabled)
                            {
                                ret = FocasData.cnc_allclibhndl3(ipAddress, portNo, 4, out focasLibHandleMain);
                                if (ret == 0)
                                {                                   
                                    live = new LiveDTO();
                                    if (AddressPartSCountFromMacro > 0)
                                    {
                                        live.PartsCount = FocasData.ReadMacro(focasLibHandleMain, AddressPartSCountFromMacro);
                                    }
                                    else
                                    {
                                        live.PartsCount = FocasData.ReadPartsCount(focasLibHandleMain);
                                    }

                                    live.MachineID = this.machineId;
                                    live.MachineMode = FocasData.ReadMachineStatusMode(focasLibHandleMain, out machineStatus);
                                    live.MachineStatus = machineStatus;
                                    live.MachineUpDownStatus = machineStatus == "In Cycle" ? 1 : 0;
                                    dynamic_data = FocasData.cnc_rddynamic2(focasLibHandleMain);
                                    live.ProgramNo = "O" + dynamic_data.prgmnum.ToString();
                                    //live.ToolNo = FocasData.ReadToolNo(focasLibHandleMain) / 100;
                                    //live.OffsetNo = live.ToolNo % 100;

                                    live.SpindleSpeed = dynamic_data.acts;
                                    live.FeedRate = dynamic_data.actf;
                                    live.SpindleLoad = FocasData.ReadSpindleLoad(focasLibHandleMain);
                                    live.Temperature = FocasData.ReadSpindleMotorTemp(focasLibHandleMain);
                                    live.SpindleStatus = Math.Abs(live.SpindleSpeed) > 1 ? "RUNNING" : "STOPPED";
                                    live.SpindleTarque = 0;

                                    live.AlarmNo = dynamic_data.alarm == 0 ? -1 : get_alarm_type(dynamic_data.alarm);
                                    if (live.AlarmNo != -1) live.MachineStatus = "Alarm";
                                    live.CutTime = FocasData.ReadCuttingTime(focasLibHandleMain);
                                    live.PowerOnTime = FocasData.ReadPowerOnTime(focasLibHandleMain);
                                    live.OperatingTime = FocasData.ReadOperatorTime(focasLibHandleMain);

                                    //live.POSITION = FocasData.ReadAxisPositions(_focasLibHandle);
                                    //live.ServoLoad_XYZ = FocasData.ReadServoLoadCurrentDetails(focasLibHandleMain, out currentAmps);

                                    live.CNCTimeStamp = FocasData.ReadCNCTimeStamp(focasLibHandleMain);

                                    if (live.CNCTimeStamp == DateTime.MinValue)
                                    {
                                        Logger.WriteDebugLog("Date time wrong");
                                        ret = FocasData.cnc_freelibhndl(focasLibHandleMain);
                                        if (ret != 0) _focasHandles.Add(focasLibHandleMain);
                                        continue;
                                    }
                                    var programDetail = FocasData.ReadOneProgram(focasLibHandleMain, dynamic_data.prgmnum);
                                    if (programDetail != null && string.IsNullOrEmpty(programDetail.Comment) == false)
                                        live.ProgramBlock = programDetail.Comment.Trim(new char[] { '(', ')' });


                                    liveAlarmsLocal.Clear();

                                    if (live.AlarmNo != -1)
                                    {
                                        //read live alarm
                                        liveAlarmsLocal = FocasData.ReadLiveAlarms(focasLibHandleMain);
                                        _prevAlarmStatus = true;
                                    }
                                    else if (live.AlarmNo == -1 && _prevAlarmStatus == true)
                                    {
                                        _prevAlarmStatus = false;
                                        liveAlarmsLocal = FocasData.ReadLiveAlarms(focasLibHandleMain);
                                        GetAlarmsDataforEndTimeUpdate();
                                    }
                                    if (liveAlarmsLocal.Count < _liveAlarmsGlobal.Count && _prevAlarmStatus == true)
                                    {
                                        _prevAlarmStatus = false;
                                        GetAlarmsDataforEndTimeUpdate();
                                    }

                                    if (liveAlarmsLocal.Count > 0 || _liveAlarmsGlobal.Count > 0)
                                    {
                                        /*TODO - build logic for emg start/end                                    
                                         * check if alarm present in gobal pool, if not add to pool and write start
                                         * get all the arams which are not in local pool and remove from global pool - write end
                                         */
                                        foreach (var alarm in liveAlarmsLocal)
                                        {
                                            if (!_liveAlarmsGlobal.Contains(alarm))
                                            {
                                                _liveAlarmsGlobal.Add(alarm);
                                            }
                                        }

                                        foreach (var alarm in _liveAlarmsGlobal.ToList())
                                        {
                                            if (!liveAlarmsLocal.Contains(alarm))
                                            {
                                                if (alarm.EndTime == DateTime.MinValue)
                                                    alarm.EndTime = live.CNCTimeStamp;
                                                // the end time in alarm history table for this alarm no
                                                if (DatabaseAccess.UpdateAlarmEndTime(this.machineId, alarm.EndTime, alarm.AlarmNo))
                                                {
                                                    _liveAlarmsGlobal.Remove(alarm);
                                                }
                                            }
                                        }
                                    }



                                    ret = FocasData.cnc_freelibhndl(focasLibHandleMain);
                                    if (ret != 0) _focasHandles.Add(focasLibHandleMain);
                                    focasLibHandleMain = ushort.MinValue;

                                    //time stamp batching for program number change
                                    if ((_previousProgramNumber != live.ProgramNo) || (_previousProgramCount > live.PartsCount))
                                    {
                                        live.BatchTS = live.CNCTimeStamp;
                                        _previousBatchTS = live.CNCTimeStamp;
                                    }
                                    else
                                    {
                                        live.BatchTS = _previousBatchTS;
                                    }

                                    _previousProgramNumber = live.ProgramNo;
                                    _previousProgramCount = live.PartsCount;

                                    //time stamp batching for machine status change(Running = 1 /Stopped = 0)
                                    if ((_previousMachineUpDownStatus != live.MachineUpDownStatus) || (live.CNCTimeStamp.Subtract(_previousCNCtimeStamp).TotalMinutes >= 2))
                                    {
                                        live.MachineUpDownBatchTS = live.CNCTimeStamp;
                                        _previousUpDownStatusBatchTS = live.CNCTimeStamp;
                                    }
                                    else
                                    {
                                        live.MachineUpDownBatchTS = _previousUpDownStatusBatchTS;
                                    }

                                    _previousCNCtimeStamp = live.CNCTimeStamp;
                                    _previousMachineUpDownStatus = live.MachineUpDownStatus;

                                    _liveDTOQueue.Add(live);
                                    if (_liveDTOQueue.LongCount() >= 1)
                                    {
                                        //DatabaseAccess.InsertLive(live);
                                        DataTable dt = _liveDTOQueue.ToDataTable<LiveDTO>();
                                        DatabaseAccess.InsertBulkRows(dt, "[dbo].[Focas_LiveData]");
                                        //Thread thread = new Thread(() => DatabaseAccess.InsertBulkRows(dt, "[dbo].[Focas_LiveData]"));
                                        //thread.Name = this.machineId;
                                        //thread.Start();
                                        _liveDTOQueue.Clear();
                                    }
                                }
                                else
                                {
                                    IsConnected = false;
                                    Logger.WriteDebugLog(string.Format("Not able to connect to CNC. Handle value = {0}", ret));
                                    Thread.Sleep(1000 * 4);
                                }
                            }
                            #endregion

                            //write code to download program and compare

                            #region ProgramDownload
                            if (enableAutoProgramDownload && live != null
                                                    && live.MachineMode == "MEM" && live.MachineStatus == "In Cycle"
                                                    && DateTime.Now > _programNextDownloadTime)
                            {
                                _programNextDownloadTime = DateTime.Now.AddMinutes(10);
                                var result = -1;
                                try
                                {
                                    if (compareSubProgramInPATH2 == false)
                                    {
                                        result = DownloadProgram(this.machineId, this.ipAddress, this.portNo, live.ProgramNo);
                                    }
                                    else
                                    {
                                        result = DownloadProgramDASCNC(this.machineId, this.ipAddress, this.portNo, live.ProgramNo);
                                    }
                                }
                                catch (Exception exxx)
                                {
                                    Logger.WriteErrorLog(exxx.ToString());
                                }
                                finally
                                {
                                    if (result == 0)
                                        _programNextDownloadTime = DateTime.Now.AddMinutes(_DownloadFreq);
                                    else
                                    {
                                        _programNextDownloadTime = DateTime.Now.AddMinutes(10);
                                    }
                                }
                            }
                            #endregion

                            if (InspectionDataReadFlag > 0)
                                ReadInspectionData(this.machineId, this.ipAddress, this.portNo);

                            live = null;
                        }
                        else
                        {
                            IsConnected = false;
                            Logger.WriteDebugLog("Disconnected from network (No ping). Ping Status = " + reply.Status.ToString());
                            if (ServiceStop.stop_service == 1) break;
                            Thread.Sleep(1000 * 4);
                        }
                        if (ping != null) ping.Dispose();
                        if (_timeDelayMainThread > 0)
                        {
                            if (ServiceStop.stop_service == 1) break;
                            Thread.Sleep(_timeDelayMainThread);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.WriteErrorLog("Exception inside main while loop : " + e.ToString());
                        Thread.Sleep(1000 * 4);
                        IsConnected = false;
                    }
                    finally
                    {
                        if (focasLibHandleMain != ushort.MinValue)
                        {
                            ret = FocasData.cnc_freelibhndl(focasLibHandleMain);
                            if (ret != 0) _focasHandles.Add(focasLibHandleMain);
                            IsConnected = false;
                            Logger.WriteDebugLog("Closing connection. ret = " + ret);
                            focasLibHandleMain = ushort.MinValue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog("Exception from main while loop : " + ex.ToString());
                    Thread.Sleep(2000);
                }
            }
            this.CloseTimer();
            Logger.WriteDebugLog("End of while loop." + Environment.NewLine + "------------------------------------------");
        }

        public void GetToolLifeData(Object stateObject)
        {
            if (!_isLicenseValid) return;
            if (Monitor.TryEnter(_lockerToolLife, 2000))
            {
                Ping ping = default(Ping);
                try
                {
                    Thread.CurrentThread.Name = "ToolLifeData-" + this.machineId;
                    ping = new Ping();
                    PingReply reply = ping.Send(ipAddress, 4000);
                    if (reply.Status == IPStatus.Success)
                    {
                        ProcessToolLifeUsingFOCAS(this.machineId, this.ipAddress, this.portNo, setting, 2);

                        //if (this.machineDTO.MTB.Equals("ACE", StringComparison.OrdinalIgnoreCase))
                        //{
                        //    Logger.WriteDebugLog("ACE : Reading Tool Life History data for control type." + _cncMachineType.ToString());
                        //    ProcessToolLifeUsingFOCAS(this.machineId, this.ipAddress, this.portNo, setting, 2);
                        //}
                        //else if (this.machineDTO.MTB.Equals("AMS", StringComparison.OrdinalIgnoreCase))
                        //{
                        //    Logger.WriteDebugLog("AMS : Reading Tool Life History data for control type." + _cncMachineType.ToString());
                        //    ProcessToolLifeUsingDVariableAMS(this.machineId, this.ipAddress, this.portNo, _toolLifeDefaults);
                        //}
                        //else if (this.machineDTO.MTB.Equals("Kennametal", StringComparison.OrdinalIgnoreCase))
                        //{
                        //    Logger.WriteDebugLog("Kennametal : Reading Tool Life History data for control type." + _cncMachineType.ToString());
                        //    ProcessToolLifeUsingDVariableKennametal(this.machineId, this.ipAddress, this.portNo, _toolLifeDefaults);
                        //}
                        //else
                        //{
                        //    Logger.WriteDebugLog("ACE : Reading Tool Life History data for control type." + _cncMachineType.ToString());
                        //    ProcessToolLifeUsingFOCAS(this.machineId, this.ipAddress, this.portNo, setting, 2);
                        //}

                        Logger.WriteDebugLog("Completed Tool Life History data.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteDebugLog(ex.ToString());
                }
                finally
                {
                    if (ping != null) ping.Dispose();
                    Monitor.Exit(_lockerToolLife);
                }
            }
        }

        private void ProcessToolLifeUsingFOCAS(string machineId, string ipAddress, ushort portNo, MachineSetting setting, int spindleType)
        {
            //Read n(12-12) macro memory location for tool target, Actual values and store to database.            
            //read every 5 minutes
            int ret = 0;
            ushort focasLibHandle = 0;
            ret = FocasData.cnc_allclibhndl3(ipAddress, portNo, 10, out focasLibHandle);
            if (ret != 0)
            {
                Logger.WriteErrorLog("ProcessToolLife()--> cnc_allclibhndl3() failed. return value is = " + ret);
                return;
            }

            DateTime cNCTimeStamp = FocasData.ReadCNCTimeStamp(focasLibHandle);

            int component = FocasData.ReadMacro(focasLibHandle, _CompMacroLocation);
            int operation = FocasData.ReadMacro(focasLibHandle, _OpnMacroLocation);
            short programNo = FocasData.ReadMainProgram(focasLibHandle);
            var partsCount = FocasData.ReadPartsCount(focasLibHandle);

            Logger.WriteDebugLog(string.Format("Comp = {0}; Operation = {1} ; Program No = {2} ; PartsCount = {3}", component, operation, programNo, partsCount));
            //number of groups
            FocasLibBase.ODBTLIFE2 a = new FocasLibBase.ODBTLIFE2();
            ret = FocasLibrary.FocasLib.cnc_rdngrp(focasLibHandle, a);
            if (ret != 0)
            {
                Logger.WriteErrorLog("Reading groups issue. return value is = " + ret);
            }

            short NoOfGroups = (short)a.data;
            Logger.WriteDebugLog("No Of groups = " + NoOfGroups);

            //FocasLibBase.ODBUSEGRP aa = new FocasLibBase.ODBUSEGRP();
            //ret = FocasLibrary.FocasLib.cnc_rdtlusegrp(focasLibHandle, aa);
            //Logger.WriteDebugLog("cnc_rdtlusegrp = " + aa.use.ToString() + " --" + aa.next.ToString());

            //loop all the groups
            List<ToolLifeDO> toolife = new List<ToolLifeDO>();
            for (short groupNo = 1; groupNo <= NoOfGroups; groupNo++)
            {
                FocasLibBase.ODBTG c = new FocasLibBase.ODBTG();
                ret = FocasLibrary.FocasLib.cnc_rdtoolgrp(focasLibHandle, groupNo, (short)System.Runtime.InteropServices.Marshal.SizeOf(c), c);
                if (ret != 0)
                {
                    Logger.WriteErrorLog("cnc_rdtoolgrp issue. return value is = " + ret);
                }
                //for each tool in a group, create the ToolLifeDO object               
                List<ToolLifeDO> toolsByGroup = getEachToolData(c, component, operation, programNo, cNCTimeStamp, partsCount);

                toolife.AddRange(toolsByGroup);
            }

            DatabaseAccess.DeleteToolLifeTempRecords(this.machineId);
            DatabaseAccess.InsertBulkRows(toolife.ToDataTable<ToolLifeDO>(), "[dbo].[Focas_ToolLifeTemp]");
            DatabaseAccess.ProcessToolLifeTempToHistory(this.machineId);
            //DatabaseAccess.DeleteToolLifeTempRecords(this.machineId);

            if (focasLibHandle > 0)
            {
                FocasData.cnc_freelibhndl(focasLibHandle);
            }
        }

        private List<ToolLifeDO> getEachToolData(FocasLibBase.ODBTG c, int component, int operation, int programNo, DateTime cNCTimeStamp, int partsCount)
        {
            Logger.WriteDebugLog(string.Format("Group No = {0} , Tool Target = {1} , Tool Actual = {2}", c.grp_num, c.life, c.count));
            List<ToolLifeDO> toolife = new List<ToolLifeDO>();
            if (c.data.data1.tool_num > 0)
            {
                var tool = new ToolLifeDO()
                {
                    CNCTimeStamp = cNCTimeStamp,
                    ComponentID = component.ToString(),
                    MachineID = machineId,
                    OperationID = operation.ToString(),
                    ProgramNo = programNo,
                    ToolTarget = c.life,
                    ToolActual = c.count,
                    ToolNo = c.data.data1.tool_num.ToString(),
                    SpindleType = c.grp_num,
                    ToolUseOrderNumber = c.data.data1.tuse_num,
                    ToolInfo = c.data.data1.tinfo,
                    PartsCount = partsCount,
                };
                toolife.Add(tool);
            }
            if (c.data.data2.tool_num > 0)
            {
                var tool = new ToolLifeDO()
                {
                    CNCTimeStamp = cNCTimeStamp,
                    ComponentID = component.ToString(),
                    MachineID = machineId,
                    OperationID = operation.ToString(),
                    ProgramNo = programNo,
                    ToolTarget = c.life,
                    ToolActual = c.count,
                    ToolNo = c.data.data2.tool_num.ToString(),
                    SpindleType = c.grp_num,
                    ToolUseOrderNumber = c.data.data2.tuse_num,
                    ToolInfo = c.data.data2.tinfo,
                    PartsCount = partsCount,
                };
                toolife.Add(tool);
            }
            if (c.data.data3.tool_num > 0)
            {
                var tool = new ToolLifeDO()
                {
                    CNCTimeStamp = cNCTimeStamp,
                    ComponentID = component.ToString(),
                    MachineID = machineId,
                    OperationID = operation.ToString(),
                    ProgramNo = programNo,
                    ToolTarget = c.life,
                    ToolActual = c.count,
                    ToolNo = c.data.data3.tool_num.ToString(),
                    SpindleType = c.grp_num,
                    ToolUseOrderNumber = c.data.data3.tuse_num,
                    ToolInfo = c.data.data3.tinfo,
                    PartsCount = partsCount,
                };
                toolife.Add(tool);

            }
            if (c.data.data4.tool_num > 0)
            {
                var tool = new ToolLifeDO()
                {
                    CNCTimeStamp = cNCTimeStamp,
                    ComponentID = component.ToString(),
                    MachineID = machineId,
                    OperationID = operation.ToString(),
                    ProgramNo = programNo,
                    ToolTarget = c.life,
                    ToolActual = c.count,
                    ToolNo = c.data.data4.tool_num.ToString(),
                    SpindleType = c.grp_num,
                    ToolUseOrderNumber = c.data.data4.tuse_num,
                    ToolInfo = c.data.data4.tinfo,
                    PartsCount = partsCount,
                };
                toolife.Add(tool);
            }
            if (c.data.data5.tool_num > 0)
            {
                var tool = new ToolLifeDO()
                {
                    CNCTimeStamp = cNCTimeStamp,
                    ComponentID = component.ToString(),
                    MachineID = machineId,
                    OperationID = operation.ToString(),
                    ProgramNo = programNo,
                    ToolTarget = c.life,
                    ToolActual = c.count,
                    ToolNo = c.data.data5.tool_num.ToString(),
                    SpindleType = c.grp_num,
                    ToolUseOrderNumber = c.data.data5.tuse_num,
                    ToolInfo = c.data.data5.tinfo,
                    PartsCount = partsCount,
                };
                toolife.Add(tool);
            }



            if (c.data.data6.tool_num > 0)
            {
                var tool = new ToolLifeDO()
                {
                    CNCTimeStamp = cNCTimeStamp,
                    ComponentID = component.ToString(),
                    MachineID = machineId,
                    OperationID = operation.ToString(),
                    ProgramNo = programNo,
                    ToolTarget = c.life,
                    ToolActual = c.count,
                    ToolNo = c.data.data6.tool_num.ToString(),
                    SpindleType = c.grp_num,
                    ToolUseOrderNumber = c.data.data6.tuse_num,
                    ToolInfo = c.data.data6.tinfo,
                    PartsCount = partsCount,
                };
                toolife.Add(tool);
            }

            if (c.data.data7.tool_num > 0)
            {
                var tool = new ToolLifeDO()
                {
                    CNCTimeStamp = cNCTimeStamp,
                    ComponentID = component.ToString(),
                    MachineID = machineId,
                    OperationID = operation.ToString(),
                    ProgramNo = programNo,
                    ToolTarget = c.life,
                    ToolActual = c.count,
                    ToolNo = c.data.data7.tool_num.ToString(),
                    SpindleType = c.grp_num,
                    ToolUseOrderNumber = c.data.data7.tuse_num,
                    ToolInfo = c.data.data7.tinfo,
                    PartsCount = partsCount,
                };
                toolife.Add(tool);
            }

            if (c.data.data8.tool_num > 0)
            {
                var tool = new ToolLifeDO()
                {
                    CNCTimeStamp = cNCTimeStamp,
                    ComponentID = component.ToString(),
                    MachineID = machineId,
                    OperationID = operation.ToString(),
                    ProgramNo = programNo,
                    ToolTarget = c.life,
                    ToolActual = c.count,
                    ToolNo = c.data.data8.tool_num.ToString(),
                    SpindleType = c.grp_num,
                    ToolUseOrderNumber = c.data.data8.tuse_num,
                    ToolInfo = c.data.data8.tinfo,
                    PartsCount = partsCount,
                };
                toolife.Add(tool);
            }

            if (c.data.data9.tool_num > 0)
            {
                var tool = new ToolLifeDO()
                {
                    CNCTimeStamp = cNCTimeStamp,
                    ComponentID = component.ToString(),
                    MachineID = machineId,
                    OperationID = operation.ToString(),
                    ProgramNo = programNo,
                    ToolTarget = c.life,
                    ToolActual = c.count,
                    ToolNo = c.data.data9.tool_num.ToString(),
                    SpindleType = c.grp_num,
                    ToolUseOrderNumber = c.data.data9.tuse_num,
                    ToolInfo = c.data.data9.tinfo,
                    PartsCount = partsCount,
                };
                toolife.Add(tool);
            }

            if (c.data.data10.tool_num > 0)
            {
                var tool = new ToolLifeDO()
                {
                    CNCTimeStamp = cNCTimeStamp,
                    ComponentID = component.ToString(),
                    MachineID = machineId,
                    OperationID = operation.ToString(),
                    ProgramNo = programNo,
                    ToolTarget = c.life,
                    ToolActual = c.count,
                    ToolNo = c.data.data10.tool_num.ToString(),
                    SpindleType = c.grp_num,
                    ToolUseOrderNumber = c.data.data10.tuse_num,
                    ToolInfo = c.data.data10.tinfo,
                    PartsCount = partsCount,
                };
                toolife.Add(tool);
            }

            return toolife;
        }

        private void SetCNCDateTime(string machineId, string ipAddress, ushort port)
        {
            Ping ping = null;
            ushort focasLibHandle = 0;
            try
            {
                ping = new Ping();
                PingReply pingReply = null;
                int count = 0;
                while (true && count <= 4)
                {
                    pingReply = ping.Send(ipAddress, 10000);
                    if (pingReply.Status != IPStatus.Success)
                    {
                        if (ServiceStop.stop_service == 1) break;
                        Logger.WriteErrorLog("Not able to ping. Ping status = " + pingReply.Status.ToString());
                        Thread.Sleep(2000);
                    }
                    else if (pingReply.Status == IPStatus.Success || ServiceStop.stop_service == 1)
                    {
                        break;
                    }
                }
                if (pingReply.Status == IPStatus.Success)
                {
                    int num2 = FocasData.cnc_allclibhndl3(ipAddress, port, 10, out focasLibHandle);
                    if (num2 == 0)
                    {
                        FocasData.SetCNCDate(focasLibHandle, DateTime.Now);
                        FocasData.SetCNCTime(focasLibHandle, DateTime.Now);
                    }
                    else
                    {
                        Logger.WriteErrorLog("Not able to connect to machine. cnc_allclibhndl3 status = " + num2.ToString());
                    }
                }
                else
                {
                    Logger.WriteErrorLog("Not able to ping. Ping status = " + pingReply.Status.ToString());
                }
            }
            catch (Exception ex)
            {
                Logger.WriteDebugLog(ex.ToString());
            }
            finally
            {
                if (ping != null)
                {
                    ping.Dispose();
                }
                if (focasLibHandle != 0)
                {
                    short num3 = FocasData.cnc_freelibhndl(focasLibHandle);
                }
            }
        }

        private List<int> GetOffsetRange()
        {
            List<int> range = new List<int>();
            try
            {
                string offsetRange = ConfigurationManager.AppSettings["OffsetHistoryRange"].ToString();
                var splitRange = offsetRange.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string item in splitRange)
                {
                    var rangeArr = item.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);

                    for (int i = int.Parse(rangeArr[0]); i <= int.Parse(rangeArr[1]); i++)
                    {
                        if (!range.Exists(t => t == i))
                        {
                            range.Add(i);
                        }
                    }
                }
            }
            catch (Exception eee)
            {
                Logger.WriteErrorLog(eee.ToString());
            }
            return range;
        }

        private void ReadPredictiveMaintenanceData(Object stateObject)
        {
            if (this.machineDTO.Settings.PredictiveMaintenanceSettings == null || this.machineDTO.Settings.PredictiveMaintenanceSettings.Count == 0)
                return;

            if (!_isLicenseValid) return;
            if (Monitor.TryEnter(this._lockerPredictiveMaintenance))
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                ushort focasLibHandle = 0;
                string text = string.Empty;
                Ping ping = null;
                try
                {
                    Thread.CurrentThread.Name = "PredictiveMaintenanceDataCollation-" + this.machineId;
                    ping = new Ping();
                    PingReply pingReply = ping.Send(this.ipAddress, 10000);
                    if (pingReply.Status == IPStatus.Success)
                    {
                        try
                        {
                            int ret = 0;
                            ret = FocasData.cnc_allclibhndl3(ipAddress, portNo, 10, out focasLibHandle);
                            if (ret != 0)
                            {
                                Logger.WriteErrorLog("cnc_allclibhndl3() failed during ReadPredictiveMaintenanceData() . return value is = " + ret);
                                Thread.Sleep(1000);
                                return;
                            }

                            Logger.WriteDebugLog("Reading PredictiveMaintenance data....");
                            DateTime cncTimeStamp = FocasData.ReadCNCTimeStamp(focasLibHandle);
                            foreach (var item in this.machineDTO.Settings.PredictiveMaintenanceSettings)
                            {
                                item.MachineId = machineId;
                                item.TimeStamp = cncTimeStamp;
                                item.TargetValue = 0;
                                item.ActualValue = 0;
                                int targerValue = 0;
                                int currentValue = 0;

                                FocasData.GetPredictiveMaintenanceTargetCurrent(focasLibHandle, item.TargetDLocation, item.CurrentValueDLocation, out targerValue, out currentValue, this.machineDTO.MTB);
                                if (targerValue != int.MaxValue)
                                {
                                    item.TargetValue = targerValue;
                                }
                                if (currentValue != int.MaxValue)
                                {
                                    item.ActualValue = currentValue;
                                }
                            }

                            //insert data to datbase......
                            DataTable dt = this.machineDTO.Settings.PredictiveMaintenanceSettings.ToDataTable<PredictiveMaintenanceDTO>();
                            dt.Columns.Remove("TargetDLocation"); dt.Columns.Remove("CurrentValueDLocation");
                            DatabaseAccess.InsertBulkRows(dt, "[dbo].[Focas_PredictiveMaintenanceTemp]");
                            //Update main table from temp table
                            DatabaseAccess.ProcessTempTableToMainTable(this.machineId, "Predictive");
                            DatabaseAccess.DeleteTempTableRecords(this.machineId, "Focas_PredictiveMaintenanceTemp");
                            Logger.WriteDebugLog("Completed reading PredictiveMaintenance data....");
                        }
                        catch (Exception exx)
                        {
                            Logger.WriteErrorLog(exx.ToString());
                        }
                    }
                }
                catch (Exception exx)
                {
                    Logger.WriteErrorLog(exx.ToString());
                }
                finally
                {
                    if (focasLibHandle > 0)
                    {
                        var r = FocasData.cnc_freelibhndl(focasLibHandle);
                        if (r != 0) _focasHandles.Add(focasLibHandle);
                    }
                    Monitor.Exit(_lockerPredictiveMaintenance);
                }
            }
        }

        DateTime CycleStartDateTime = DateTime.MinValue;
        bool _ReadSpindleData = false;
        bool _ReadContinious = Convert.ToBoolean(ConfigurationManager.AppSettings["ReadCountinue"].ToString());
        private void ReadGrindingCyclemonitoring_Bajaj(object state)
        {

            if (Monitor.TryEnter(this._lockerGrindingCycleMonitoring, 100))
            {
                if (!_isLicenseValid) return;
                if (this.machineDTO.Settings.GrindingCycleMonitoring_Bajaj == null && this.machineDTO.Settings.GrindingCycleMonitoring_Bajaj.Count > 0)
                {
                    Logger.WriteDebugLog(string.Format("master data not found in \"[ProcessParameterMaster_BajajIoT]\" table for Grinding Cycle Monitoring parameters"));
                    return;
                }

                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                ushort focasLibHandle = 0;
                string text = string.Empty;
                ushort rLocationCycles = this.machineDTO.Settings.grinding_Bajaj.RLocationForGrindingCycles;
                ushort rLocationFeedRate = this.machineDTO.Settings.grinding_Bajaj.RLocationForGrindingFeedRate;
                Ping ping = null;
                try
                {
                    Thread.CurrentThread.Name = "GrindingCycleMonitoring_Bajaj_DataCollation-" + this.machineId;
                    ping = new Ping();
                    PingReply pingReply = ping.Send(this.ipAddress, 10000);
                    if (pingReply.Status == IPStatus.Success)
                    {
                        try
                        {
                            int ret = 0;
                            ret = FocasData.cnc_allclibhndl3(ipAddress, portNo, 10, out focasLibHandle);
                            if (ret != 0)
                            {
                                Logger.WriteErrorLog("cnc_allclibhndl3() failed during ReadProcessParameter_Bajaj() . return value is = " + ret);
                                Thread.Sleep(1000);
                                return;
                            }
                            DateTime TimeoutTime = DateTime.MinValue;
                            Logger.WriteDebugLog("Reading Grinding Cycle Monitoring data....");
                            List<ProcessParameterTransactionDTO_Bajaj> processParameterTransactions = new List<ProcessParameterTransactionDTO_Bajaj>();
                            int ret1, ret2;
                            if (serviceStartedMinute <= machineDTO.Settings.TimeIntervalInMinute)
                                ReadTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, machineDTO.Settings.TimeIntervalInMinute - 1, 0);
                            else
                                ReadTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour + 1, 0, 0).AddMinutes(-1);

                            TimeoutTime = ReadTime.AddMinutes(machineDTO.Settings.TimeIntervalInMinute);

                            //ReadTime = DateTime.Now.AddMinutes(-1);
                            //TimeoutTime = DateTime.MaxValue;
                            while (ret == 0)
                            {
                                try
                                {
                                    int bitToRead = int.MinValue;
                                    if ((DateTime.Now >= ReadTime && DateTime.Now <= TimeoutTime) || CycleStarted == 1 || _ReadContinious == true)
                                    {
                                        //TODO open connecton
                                        DateTime cncTimeStamp = FocasData.ReadCNCTimeStamp(focasLibHandle);

                                        var val = FocasData.ReadPMCRangeByte(focasLibHandle, 12, rLocationCycles, rLocationCycles, out ret1);
                                        this.CycleMonitor = val[0];
                                        val = FocasData.ReadPMCRangeByte(focasLibHandle, 12, rLocationFeedRate, rLocationFeedRate, out ret2);
                                        this.FeedRateMonitor = val[0];

                                        bool IsDressingOn = GetDressingStatus(focasLibHandle, 518, 519);
                                        if (cncTimeStamp == DateTime.MinValue || ret1 != 0 || ret2 != 0)
                                            break;
                                        var cycleStart = machineDTO.Settings.GrindingCycleMonitoring_Bajaj.Where(x => x.ParameterID.Equals("P1")).ToList().FirstOrDefault();
                                        if (cycleStart != null)
                                        {
                                            bitToRead = Convert.ToInt32(cycleStart.RLocation.ToString().Split('.')[1]);
                                            if ((Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead)) != this.CycleStarted))
                                            {
                                                this.CycleStarted = Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead));
                                                if (CycleStarted == 1 /*&& (grindingReadCount < machineDTO.Settings.ReadCuttingForCycles || dressingReadCount < machineDTO.Settings.ReadCuttingForCycles)*/)
                                                {
                                                    ProcessParameterTransactionDTO_Bajaj transaction = new ProcessParameterTransactionDTO_Bajaj();
                                                    transaction.MachineID = this.machineId;
                                                    transaction.ParameterID = cycleStart.ParameterID;
                                                    //transaction.ParameterName = grinding.ParameterName;
                                                    transaction.ParameterValue = Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead)).ToString();
                                                    transaction.Qualifier = cycleStart.AdditionalQualifier;
                                                    transaction.UpdatedtimeStamp = cncTimeStamp;
                                                    processParameterTransactions.Add(transaction);
                                                    this.grindApplicationFlag = 1;
                                                    CycleStartDateTime = DateTime.Now.AddSeconds(10);
                                                    processParameterTransactions.Add(transaction);
                                                }
                                            }
                                        }
                                        var cycleEnd = machineDTO.Settings.GrindingCycleMonitoring_Bajaj.Where(x => x.ParameterID.Equals("P2")).ToList().FirstOrDefault();
                                        if (cycleEnd != null)
                                        {
                                            bitToRead = Convert.ToInt32(cycleEnd.RLocation.ToString().Split('.')[1]);
                                            if ((Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead)) != this.CycleEnded))
                                            {
                                                this.CycleEnded = Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead));
                                                if (CycleEnded == 1 /*&& (grindingReadCount < machineDTO.Settings.ReadCuttingForCycles || dressingReadCount < machineDTO.Settings.ReadCuttingForCycles)*/)
                                                {
                                                    ProcessParameterTransactionDTO_Bajaj transaction = new ProcessParameterTransactionDTO_Bajaj();
                                                    transaction.MachineID = this.machineId;
                                                    transaction.ParameterID = cycleEnd.ParameterID;
                                                    transaction.ParameterValue = Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead)).ToString();
                                                    transaction.Qualifier = cycleEnd.AdditionalQualifier;
                                                    transaction.UpdatedtimeStamp = cncTimeStamp;
                                                    processParameterTransactions.Add(transaction);
                                                    _ReadSpindleData = false;
                                                }

                                            }

                                        }


                                        if (CycleStarted == 1)
                                        {
                                            if((!IsDressingOn && grindingReadCount < machineDTO.Settings.ReadCuttingForCycles) || 
                                                (IsDressingOn && dressingReadCount < machineDTO.Settings.ReadCuttingForCycles) || _ReadContinious == true)
                                            {
                                                _ReadSpindleData = true;
                                                UpdatedProcessParameters(focasLibHandle, ref processParameterTransactions, bitToRead, cncTimeStamp);
                                            }
                                            //if (IsDressingOn && dressingReadCount < machineDTO.Settings.ReadCuttingForCycles)
                                            //{
                                            //    UpdatedProcessParameters(focasLibHandle,ref processParameterTransactions, bitToRead, cncTimeStamp);
                                            //}
                                            
                                            //else if (!IsDressingOn && grindingReadCount < machineDTO.Settings.ReadCuttingForCycles)
                                            //{
                                            //    UpdatedProcessParameters(focasLibHandle, ref processParameterTransactions, bitToRead, cncTimeStamp);
                                               
                                            //}

                                        }
                                        
                                        if (processParameterTransactions != null && processParameterTransactions.Count > 0)
                                        {
                                            MongoDatabaseAccess.InsertProcessParameterTransaction_BajajIoT(processParameterTransactions).Wait();
                                            processParameterTransactions.Clear();
                                        }
                                    }
                                    else if (DateTime.Now > TimeoutTime)
                                    {
                                        dressingReadCount = 0;
                                        grindingReadCount = 0;
                                        ReadTime = ReadTime.AddMinutes(machineDTO.Settings.TimeIntervalInMinute);
                                        TimeoutTime = ReadTime.AddMinutes(machineDTO.Settings.TimeIntervalInMinute);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.WriteErrorLog("Exception while Reading Grinding Cycles Values : " + ex.Message);
                                    break;
                                }
                                Thread.Sleep(500);

                            }
                            serviceStartedMinute = DateTime.Now.Minute;
                            Logger.WriteDebugLog("Completed reading Grinding Cycle Monitoring data....");
                        }
                        catch (Exception exx)
                        {
                            Logger.WriteErrorLog(exx.ToString());
                        }
                    }
                }
                catch (Exception exx)
                {
                    Logger.WriteErrorLog(exx.ToString());
                }
                finally
                {
                    if (focasLibHandle > 0)
                    {
                        var r = FocasData.cnc_freelibhndl(focasLibHandle);
                        // if (r != 0) _focasHandles.Add(focasLibHandle);
                    }
                    Monitor.Exit(_lockerGrindingCycleMonitoring);
                }
            }
        }

        private void UpdatedProcessParameters(ushort focasLibHandle, ref List<ProcessParameterTransactionDTO_Bajaj> processParameterTransactions, int bitToRead, DateTime cncTimeStamp)
        {
            foreach (var grinding in this.machineDTO.Settings.GrindingCycleMonitoring_Bajaj)
            {
                if (grinding.ParameterID.Equals("P12"))// Reading From Macro Location
                {
                    //continue;
                    var sprkTime = FocasData.ReadMacro(focasLibHandle, Convert.ToInt16(grinding.RLocation));
                    if (sprkTime != SparkOutTime)
                    {
                        ProcessParameterTransactionDTO_Bajaj transaction = new ProcessParameterTransactionDTO_Bajaj();
                        transaction.MachineID = this.machineId;
                        transaction.ParameterID = grinding.ParameterID;
                        //transaction.ParameterName = grinding.ParameterName;
                        transaction.ParameterValue = sprkTime.ToString("00.0000");
                        transaction.Qualifier = grinding.AdditionalQualifier;
                        transaction.UpdatedtimeStamp = cncTimeStamp;
                        processParameterTransactions.Add(transaction);
                        SparkOutTime = sprkTime;
                    }


                }
                else // Reading ELocation
                {
                    bitToRead = Convert.ToInt32(grinding.RLocation.ToString().Split('.')[1]);
                   
                    if (grinding.ParameterID.Equals("P3") && (Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead)) != this.GrindingStartMCode))
                    {
                        this.GrindingStartMCode = Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead));
                        if (GrindingStartMCode == 1)
                        {
                            ProcessParameterTransactionDTO_Bajaj transaction = new ProcessParameterTransactionDTO_Bajaj();
                            transaction.MachineID = this.machineId;
                            transaction.ParameterID = grinding.ParameterID;
                            //transaction.ParameterName = grinding.ParameterName;
                            transaction.ParameterValue = Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead)).ToString();
                            transaction.Qualifier = grinding.AdditionalQualifier;
                            transaction.UpdatedtimeStamp = cncTimeStamp;
                            processParameterTransactions.Add(transaction);
                        }

                    }
                    else if (grinding.ParameterID.Equals("P4") && (Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead)) != this.GrindingEndMCode))
                    {

                        this.GrindingEndMCode = Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead));
                        if (GrindingEndMCode == 1)
                        {
                            ProcessParameterTransactionDTO_Bajaj transaction = new ProcessParameterTransactionDTO_Bajaj();
                            transaction.MachineID = this.machineId;
                            transaction.ParameterID = grinding.ParameterID;
                            //transaction.ParameterName = grinding.ParameterName;
                            transaction.ParameterValue = Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead)).ToString();
                            transaction.Qualifier = grinding.AdditionalQualifier;
                            transaction.UpdatedtimeStamp = cncTimeStamp;
                            processParameterTransactions.Add(transaction);
                            grindingReadCount++;
                        }

                    }
                    else if (grinding.ParameterID.Equals("P5") && (Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead)) != this.DressingStartMCode))
                    {
                        this.DressingStartMCode = Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead));
                        if (DressingStartMCode == 1)
                        {
                            ProcessParameterTransactionDTO_Bajaj transaction = new ProcessParameterTransactionDTO_Bajaj();
                            transaction.MachineID = this.machineId;
                            transaction.ParameterID = grinding.ParameterID;
                            //transaction.ParameterName = grinding.ParameterName;
                            transaction.ParameterValue = Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead)).ToString();
                            transaction.Qualifier = grinding.AdditionalQualifier;
                            transaction.UpdatedtimeStamp = cncTimeStamp;
                            processParameterTransactions.Add(transaction);

                        }
                    }
                    else if (grinding.ParameterID.Equals("P6") && (Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead)) != this.DressingEndMCode))
                    {
                        this.DressingEndMCode = Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead));
                        if (DressingEndMCode == 1)
                        {
                            ProcessParameterTransactionDTO_Bajaj transaction = new ProcessParameterTransactionDTO_Bajaj();
                            transaction.MachineID = this.machineId;
                            transaction.ParameterID = grinding.ParameterID;
                            //transaction.ParameterName = grinding.ParameterName;
                            transaction.ParameterValue = Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead)).ToString();
                            transaction.Qualifier = grinding.AdditionalQualifier;
                            transaction.UpdatedtimeStamp = cncTimeStamp;
                            processParameterTransactions.Add(transaction);
                            dressingReadCount++;
                        }

                    }
                    else if (grinding.ParameterID.Equals("P7") && (Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead)) != this.ApproachFeedRate))
                    {
                        this.ApproachFeedRate = Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead));

                        ProcessParameterTransactionDTO_Bajaj transaction = new ProcessParameterTransactionDTO_Bajaj();
                        transaction.MachineID = this.machineId;
                        transaction.ParameterID = grinding.ParameterID;
                        //transaction.ParameterName = grinding.ParameterName;
                        transaction.ParameterValue = ApproachFeedRate.ToString();
                        transaction.Qualifier = "";
                        transaction.UpdatedtimeStamp = cncTimeStamp;
                        processParameterTransactions.Add(transaction);
                    }

                    else if (grinding.ParameterID.Equals("P8") && (Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead)) != this.RoughingFeedRate))
                    {
                        this.RoughingFeedRate = Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead));
                        ProcessParameterTransactionDTO_Bajaj transaction = new ProcessParameterTransactionDTO_Bajaj();
                        transaction.MachineID = this.machineId;
                        transaction.ParameterID = grinding.ParameterID;
                        //transaction.ParameterName = grinding.ParameterName;
                        transaction.ParameterValue = RoughingFeedRate.ToString();
                        transaction.Qualifier = grinding.AdditionalQualifier;
                        transaction.UpdatedtimeStamp = cncTimeStamp;
                        processParameterTransactions.Add(transaction);
                    }
                    else if (grinding.ParameterID.Equals("P9") && (Convert.ToInt16(IsBitSet(this.FeedRateMonitor, bitToRead)) != this.SemiFinishingFeedRate))
                    {
                        this.SemiFinishingFeedRate = Convert.ToInt16(IsBitSet(this.FeedRateMonitor, bitToRead));
                        ProcessParameterTransactionDTO_Bajaj transaction = new ProcessParameterTransactionDTO_Bajaj();
                        transaction.MachineID = this.machineId;
                        transaction.ParameterID = grinding.ParameterID;
                        //transaction.ParameterName = grinding.ParameterName;
                        transaction.ParameterValue = SemiFinishingFeedRate.ToString();
                        transaction.Qualifier = grinding.AdditionalQualifier;
                        transaction.UpdatedtimeStamp = cncTimeStamp;
                        processParameterTransactions.Add(transaction);
                    }
                    else if (grinding.ParameterID.Equals("P10") && (Convert.ToInt16(IsBitSet(this.FeedRateMonitor, bitToRead)) != this.FinishingFeedRate))
                    {
                        this.FinishingFeedRate = Convert.ToInt16(IsBitSet(this.FeedRateMonitor, bitToRead));
                        ProcessParameterTransactionDTO_Bajaj transaction = new ProcessParameterTransactionDTO_Bajaj();
                        transaction.MachineID = this.machineId;
                        transaction.ParameterID = grinding.ParameterID;
                        //transaction.ParameterName = grinding.ParameterName;
                        transaction.ParameterValue = FinishingFeedRate.ToString();
                        transaction.Qualifier = grinding.AdditionalQualifier;
                        transaction.UpdatedtimeStamp = cncTimeStamp;
                        processParameterTransactions.Add(transaction);
                    }
                    else if (grinding.ParameterID.Equals("P11") && (Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead)) != this.DressingFeedRate))
                    {
                        this.DressingFeedRate = Convert.ToInt16(IsBitSet(this.CycleMonitor, bitToRead));
                        ProcessParameterTransactionDTO_Bajaj transaction = new ProcessParameterTransactionDTO_Bajaj();
                        transaction.MachineID = this.machineId;
                        transaction.ParameterID = grinding.ParameterID;
                        //transaction.ParameterName = grinding.ParameterName;
                        transaction.ParameterValue = DressingFeedRate.ToString();
                        transaction.Qualifier = grinding.AdditionalQualifier;
                        transaction.UpdatedtimeStamp = cncTimeStamp;
                        processParameterTransactions.Add(transaction);
                    }
                }

            }
        }
    
        private bool GetDressingStatus(ushort focasLibHandle, short v1, short v2)
        {
            bool isDressingOn = false;
            try
            {
                var val1 = FocasData.ReadMacro(focasLibHandle, v1);
                var val2 = FocasData.ReadMacro(focasLibHandle, v2);

                if (val1 == val2)
                    isDressingOn = true;
            }
            catch(Exception ex)
            {
                isDressingOn = false;
                Logger.WriteErrorLog(ex.Message);
            }
            return isDressingOn;
        }

        long msCount = 0;

        private void ReadFeedRate_Bajaj(object state)
        {

            if (Monitor.TryEnter(this._lockeFeedRate, 1000))
            {
                if (!_isLicenseValid) return;              

                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                ushort focasLibHandle = 0;               
                Ping ping = null;
                try
                {
                    Thread.CurrentThread.Name = "FeedRate_Bajaj_DataCollation-" + this.machineId;
                    ping = new Ping();
                    PingReply pingReply = ping.Send(this.ipAddress, 10000);
                    if (pingReply.Status == IPStatus.Success)
                    {
                        try
                        {
                            int ret = 0;
                            ret = FocasData.cnc_allclibhndl3(ipAddress, portNo, 10, out focasLibHandle);
                            if (ret != 0)
                            {
                                Logger.WriteErrorLog("cnc_allclibhndl3() failed during FeedRate_Bajaj_DataCollation() . return value is = " + ret);
                                Thread.Sleep(1000);
                                return;
                            }

                            //Logger.WriteDebugLog("Reading feed rate  data....");
                            List<ProcessParameterTransactionDTO_Bajaj> processParameterTransactions = new List<ProcessParameterTransactionDTO_Bajaj>();
                            int ret1, ret2;
                            while (ret == 0 && CycleStarted == 1)
                            {
                                try
                                {
                                    //if (CycleStarted == 1)
                                    //{
                                    DateTime cncTimeStamp = FocasData.ReadCNCTimeStamp(focasLibHandle, out ret).AddMilliseconds(msCount);
                                    if (ret != 0) break;
                                    var feedrate = FocasData.ReadFeedRateDecimal(focasLibHandle, out ret);
                                    if (ret != 0) break;
                                    msCount++;
                                    if (msCount >= 999)
                                    {
                                        msCount = 0;
                                    }
                                    if (feedrate > 0)
                                    {
                                        ProcessParameterTransactionDTO_Bajaj transaction = new ProcessParameterTransactionDTO_Bajaj();
                                        transaction.MachineID = this.machineId;
                                        transaction.ParameterID = "ActualFeedRate";
                                        //transaction.ParameterName = grinding.ParameterName;
                                        transaction.ParameterValue = feedrate.ToString(); // FocasData.ReadMacro(focasLibHandle, Convert.ToInt16(grinding.RLocation)).ToString("00.0000");
                                        transaction.Qualifier = string.Empty;
                                        transaction.UpdatedtimeStamp = cncTimeStamp;
                                        processParameterTransactions.Add(transaction);
                                        //Logger.WriteDebugLog("Completed reading FeedRate data! FeedRate Value : " + feedrate);
                                    }
                                    //}                                  

                                    if (processParameterTransactions != null && processParameterTransactions.Count > 0)
                                    {
                                        MongoDatabaseAccess.InsertProcessParameterTransaction_BajajIoT(processParameterTransactions).Wait();
                                        processParameterTransactions.Clear();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.WriteErrorLog("Exception while Reading Grinding Cycles Values : " + ex.Message);
                                    break;
                                }
                                finally
                                {
                                    Thread.Sleep(200);
                                }

                            }
                            
                        }
                        catch (Exception exx)
                        {
                            Logger.WriteErrorLog(exx.ToString());
                        }
                    }
                }
                catch (Exception exx)
                {
                    Logger.WriteErrorLog(exx.ToString());
                }
                finally
                {
                    if (focasLibHandle > 0)
                    {
                        var r = FocasData.cnc_freelibhndl(focasLibHandle);
                        // if (r != 0) _focasHandles.Add(focasLibHandle);
                    }
                    Monitor.Exit(_lockeFeedRate);
                }
            }
        }

        private void ReadLiveProcessParameter_Bajaj(Object stateObject)
        {
            if (this.CycleStarted == 0) return;
            if (this.machineDTO.Settings.LiveDashboard_Bajaj == null || this.machineDTO.Settings.LiveDashboard_Bajaj.Count == 0)
            {
                Logger.WriteDebugLog(string.Format("master data not found in \"[ProcessParameterMaster_BajajIoT]\" table for Live Dashboard parameters"));
                return;
            }
            
            //MongoDatabaseAccess.InsertProcessParameterTransaction_BajajIoT(this.machineDTO.Settings.LiveDashboard_Bajaj).Wait();
            
            if (!_isLicenseValid) return;
            if (Monitor.TryEnter(this._lockerProcessParameter_BAJAJ))
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                ushort focasLibHandle = 0;
                string text = string.Empty;
                DataTable dt = null;
                DateTime cncTimeStamp = DateTime.MinValue;
                List<string> columnsToMap = new List<string>();
                Ping ping = null;
                try
                {
                    Thread.CurrentThread.Name = "LiveDashboard_Bajaj_DataCollation-" + this.machineId;
                    ping = new Ping();
                    PingReply pingReply = ping.Send(this.ipAddress, 10000);
                    if (pingReply.Status == IPStatus.Success)
                    {
                        try
                        {
                            int ret = 0;
                            ret = FocasData.cnc_allclibhndl3(ipAddress, portNo, 10, out focasLibHandle);
                            if (ret != 0)
                            {
                                Logger.WriteErrorLog("cnc_allclibhndl3() failed during ReadProcessParameter_Bajaj() . return value is = " + ret);
                                Thread.Sleep(1000);
                                return;
                            }

                            Logger.WriteDebugLog("Reading ProcesParameter data....");
                            if (this.CycleStarted==1)
                            {
                                cncTimeStamp = FocasData.ReadCNCTimeStamp(focasLibHandle);
                                foreach (var item in this.machineDTO.Settings.LiveDashboard_Bajaj)
                                {
                                    item.MachineID = this.machineId;
                                    item.UpdatedtimeStamp = cncTimeStamp.ToString("yyyy-MM-dd HH:mm:ss");

                                    if (item.ParameterID.Equals("P21") || item.ParameterID.Equals("P20") || item.ParameterID.Equals("P24") || item.ParameterID.Equals("P96"))
                                    {
                                        var val = FocasData.ReadPMCRangeByte(focasLibHandle, 12, ushort.Parse(item.RLocation), ushort.Parse(item.RLocation));

                                        if (item.ParameterID.Equals("P20"))
                                        {
                                            if (IsBitSet(val[0], 2))
                                                item.ParameterValue = "Green";
                                            else if (IsBitSet(val[0], 3))
                                                item.ParameterValue = "Yellow";
                                            else if (IsBitSet(val[0], 4))
                                                item.ParameterValue = "Red";
                                            else
                                                item.ParameterValue = "Alarm";
                                        }
                                        else if (item.ParameterID.Equals("P21"))
                                        {
                                            if (IsBitSet(val[0], 5))
                                                item.ParameterValue = "Green";
                                            else if (IsBitSet(val[0], 6))
                                                item.ParameterValue = "Yellow";
                                            else if (IsBitSet(val[0], 7))
                                                item.ParameterValue = "Red";
                                            else
                                                item.ParameterValue = "Alarm";
                                        }
                                        else if (item.ParameterID.Equals("P24"))
                                        {
                                            if (IsBitSet(val[0], 0))
                                                item.ParameterValue = "OK";
                                            else
                                                item.ParameterValue = "NOT OK";
                                        }
                                        else if (item.ParameterID.Equals("P96"))
                                        {
                                            if (IsBitSet(val[0], 1))
                                                item.ParameterValue = "Red";
                                            else if (IsBitSet(val[0], 2))
                                                item.ParameterValue = "Yellow";
                                            else if (IsBitSet(val[0], 3))
                                                item.ParameterValue = "Green";
                                            else
                                                item.ParameterValue = "Alarm";
                                        }
                                    }
                                    else
                                    {
                                        var value = FocasData.ReadPMCOneWord(focasLibHandle, 12, ushort.Parse(item.RLocation), (ushort)(ushort.Parse(item.RLocation) + 2));
                                        
                                        if (value != short.MinValue)
                                            item.ParameterValue = Convert.ToDecimal(value).ToString();
                                    }

                                }

                                //Deleting Data From Temp Table
                                int success = DatabaseAccess.DeleteTempTableRecords(this.machineId, "ProcessParameterTransaction_BajajIoT");
                                if (success > 0)
                                    Logger.WriteDebugLog("Previous Parameter's Transaction Records Removed Successfully for machine : " + this.machineId);
                                else
                                    Logger.WriteDebugLog("Failed To Remove Previous Parameter's Transaction Records for machine : " + this.machineId);
                                //insert data to database......
                                dt = this.machineDTO.Settings.LiveDashboard_Bajaj.ToDataTable<ProcessParameterDTO_BAJAJ>();
                                columnsToMap = new List<string>() { "MachineID", "ParameterID", "ParameterValue", "UpdatedtimeStamp" };
                                DatabaseAccess.BulkInsertRows(dt, "[dbo].[ProcessParameterTransaction_BajajIoT]", columnsToMap);
                                
                                //Prince - TODO - compare with cycle start time period
                                if ((DateTime.Now >= CycleStartDateTime))
                                {
                                    DatabaseAccess.BulkInsertRows(dt, "[dbo].[ProcessParameterTransaction_History_BajajIoT]", columnsToMap);
                                }
                            }

                            Logger.WriteDebugLog("Completed reading ProcesParameter data....");
                        }
                        catch (Exception exx)
                        {
                            Logger.WriteErrorLog(exx.ToString());
                        }
                    }
                }
                catch (Exception exx)
                {
                    Logger.WriteErrorLog(exx.ToString());
                }
                finally
                {
                    if (focasLibHandle > 0)
                    {
                        var r = FocasData.cnc_freelibhndl(focasLibHandle);
                        if (r != 0) _focasHandles.Add(focasLibHandle);
                    }
                    Monitor.Exit(_lockerProcessParameter_BAJAJ);
                }
            }
        }

        private bool IsBitSet(byte b, int bitNumber)
        {
            return (b & (1 << bitNumber)) != 0;
        }

        private void ReadOffsetHistoryData(Object stateObject)
        {
            if (!_isLicenseValid) return;
            if (this.offsetHistoryList == null || this.offsetHistoryList.Count == 0)
                return;

            if (Monitor.TryEnter(this._lockerOffsetHistory))
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                ushort focasLibHandle = 0;
                string text = string.Empty;
                Ping ping = null;
                try
                {
                    Thread.CurrentThread.Name = string.IsNullOrWhiteSpace(Thread.CurrentThread.Name) ? "OffsetHistoryDataCollation-" + this.machineId : Thread.CurrentThread.Name;
                    ping = new Ping();
                    PingReply pingReply = ping.Send(this.ipAddress, 10000);
                    if (pingReply.Status == IPStatus.Success)
                    {
                        try
                        {
                            int ret = 0;
                            ret = FocasData.cnc_allclibhndl3(ipAddress, portNo, 10, out focasLibHandle);
                            if (ret != 0)
                            {
                                Logger.WriteErrorLog("cnc_allclibhndl3() failed during ReadOffsetHistoryData() . return value is = " + ret);
                                Thread.Sleep(1000);
                                return;
                            }

                            Logger.WriteDebugLog("Reading Offset values....");
                            DateTime cncTimeStamp = FocasData.ReadCNCTimeStamp(focasLibHandle);
                            string machineStatus;
                            var machineMode = FocasData.ReadMachineStatusMode(focasLibHandle, out machineStatus);
                            var dynamic_data = FocasData.cnc_rddynamic2(focasLibHandle);
                            foreach (var item in offsetHistoryList)
                            {
                                item.CNCTimeStamp = cncTimeStamp;
                                item.ProgramNo = "O" + dynamic_data.prgmnum.ToString();
                                item.MachineMode = machineMode;
                                item.ToolNo = FocasData.ReadToolNo(focasLibHandle);
                                item.WearOffsetX = FocasData.get_wear_offset(focasLibHandle, item.OffsetNo, 'x');
                                item.WearOffsetZ = FocasData.get_wear_offset(focasLibHandle, item.OffsetNo, 'z');
                                item.WearOffsetR = 0; // FocasData.get_wear_offset(focasLibHandle, item.OffsetNo, 'r');
                                item.WearOffsetT = 0;// FocasData.get_wear_offset(focasLibHandle, item.OffsetNo, 't');
                            }
                            //insert data to datbase......
                            DataTable dt = offsetHistoryList.ToDataTable<OffsetHistoryDTO>();
                            DatabaseAccess.InsertBulkRows(dt, "[dbo].[Focas_ToolOffsetHistoryTemp]");

                            DatabaseAccess.ProcessTempTableToMainTable(this.machineId, "OffsetHistory");
                            DatabaseAccess.DeleteTempTableRecords(this.machineId, "Focas_ToolOffsetHistoryTemp");
                            Logger.WriteDebugLog("Completed reading Offset values....");
                        }
                        catch (Exception exx)
                        {
                            Logger.WriteErrorLog(exx.ToString());
                        }
                        finally
                        {
                            if (focasLibHandle > 0)
                            {
                                var r = FocasData.cnc_freelibhndl(focasLibHandle);
                                if (r != 0) _focasHandles.Add(focasLibHandle);
                            }
                        }
                    }
                }
                catch (Exception exx)
                {
                    Logger.WriteErrorLog(exx.ToString());
                }
                finally
                {
                    Monitor.Exit(_lockerOffsetHistory);
                    if (ping != null) ping.Dispose();
                }
            }
        }
        private bool ValidateCNCSerialNo(string machineId, string ipAddress, ushort port, List<string> cncSerialnumbers, out bool isLicCheckedSucessfully, out string cncID)
        {
            bool result = false;
            isLicCheckedSucessfully = true;
            Ping ping = null;
            ushort focasLibHandle = 0;
            cncID = string.Empty;

            try
            {
                ping = new Ping();
                PingReply pingReply = null;
                while (true)
                {
                    pingReply = ping.Send(ipAddress, 10000);
                    if (pingReply.Status != IPStatus.Success)
                    {
                        if (ServiceStop.stop_service == 1) break;
                        Logger.WriteErrorLog("Not able to ping. Ping status = " + pingReply.Status.ToString());
                        Thread.Sleep(10000);
                    }
                    else if (pingReply.Status == IPStatus.Success || ServiceStop.stop_service == 1)
                    {
                        break;
                    }
                }
                if (pingReply.Status == IPStatus.Success)
                {
                    int num2 = FocasData.cnc_allclibhndl3(ipAddress, port, 10, out focasLibHandle);
                    if (num2 == 0)
                    {
                        string text = FocasData.ReadCNCId(focasLibHandle);
                        if (!string.IsNullOrEmpty(text))
                        {
                            if (cncSerialnumbers.Contains(text))
                            {
                                cncID = text;
                                result = true;
                            }
                        }
                        else
                        {
                            isLicCheckedSucessfully = false;
                        }
                    }
                    else
                    {
                        Logger.WriteErrorLog("Not able to connect to machine. cnc_allclibhndl3 status = " + num2.ToString());
                        isLicCheckedSucessfully = false;
                    }
                }
                else
                {
                    Logger.WriteErrorLog("Not able to ping. Ping status = " + pingReply.Status.ToString());
                    isLicCheckedSucessfully = false;
                }
            }
            catch (Exception ex)
            {
                isLicCheckedSucessfully = false;
                Logger.WriteDebugLog(ex.ToString());
            }
            finally
            {
                if (ping != null)
                {
                    ping.Dispose();
                }
                if (focasLibHandle != 0)
                {
                    short num3 = FocasData.cnc_freelibhndl(focasLibHandle);
                    if (num3 != 0) _focasHandles.Add(focasLibHandle);
                }
            }
            return result;
        }

        private bool ValidateMachineModel(string machineId, string ipAddress, ushort port)
        {
            bool result = false;
            Ping ping = null;
            ushort focasLibHandle = 0;
            try
            {
                ping = new Ping();
                PingReply pingReply = null;
                while (true)
                {
                    pingReply = ping.Send(ipAddress, 10000);
                    if (pingReply.Status != IPStatus.Success)
                    {
                        if (ServiceStop.stop_service == 1) break;
                        Logger.WriteErrorLog("Not able to ping. Ping status = " + pingReply.Status.ToString());
                        Thread.Sleep(10000);
                    }
                    else if (pingReply.Status == IPStatus.Success || ServiceStop.stop_service == 1)
                    {
                        break;
                    }
                }
                if (pingReply.Status == IPStatus.Success)
                {
                    int num2 = FocasData.cnc_allclibhndl3(ipAddress, port, 10, out focasLibHandle);
                    if (num2 == 0)
                    {
                        int mcModel = FocasData.ReadParameterInt(focasLibHandle, 4133);
                        int maxSpeedOnMotor = FocasData.ReadParameterInt(focasLibHandle, 4020);
                        int maxSpeedOnSpindle = FocasData.ReadParameterInt(focasLibHandle, 3741);
                        if (mcModel > 0)
                        {
                            DatabaseAccess.UpdateMachineModel(machineId, mcModel);
                        }
                    }
                    else
                    {
                        Logger.WriteErrorLog("Not able to connect to machine. cnc_allclibhndl3 status = " + num2.ToString());
                    }
                }
                else
                {
                    Logger.WriteErrorLog("Not able to ping. Ping status = " + pingReply.Status.ToString());
                }
            }
            catch (Exception ex)
            {
                Logger.WriteDebugLog(ex.ToString());
            }
            finally
            {
                if (ping != null)
                {
                    ping.Dispose();
                }
                if (focasLibHandle != 0)
                {
                    short num3 = FocasData.cnc_freelibhndl(focasLibHandle);
                    if (num3 != 0) _focasHandles.Add(focasLibHandle);
                }
            }
            return result;
        }


        //read comments-done, parse sub program-done, 
        //TODO - file naming in master file, file naming while saving to "AutoDownloadFolder"
        private int DownloadProgram(string machineId, string ipAddress, ushort port, string programNumber)
        {
            //download running program in temp folder
            bool result = false;
            int programNo = 0;
            programNumber = programNumber.TrimStart(new char[] { 'O' }).Trim();
            int.TryParse(programNumber, out programNo);
            if (programNo == 0) return 0;
            string mainProgramMasterStr = string.Empty;
            StringBuilder messageForSMS = new StringBuilder();
            Logger.WriteDebugLog("Downloading main program : " + programNo);
            string mainProgramCNCStr = FocasData.DownloadProgram(ipAddress, port, programNo, out result);
            if (!result) return -1;
            Hashtable hashSubPrograms = new Hashtable();
            //check if main program contains sub peogram(contains with M98P)
            string mainProgramCNCComment = FindProgramComment(mainProgramCNCStr);
            List<int> subProgramsCNC = FindSubPrograms(mainProgramCNCStr);
            List<int> subProgramsCNCTemp = new List<int>();
            if (subProgramsCNC.Count > 0)
            {
                //download sub programs starts with M98P
                foreach (var item in subProgramsCNC)
                {
                    Logger.WriteDebugLog("Downloading first level sub program : " + item);
                    string prgText = FocasData.DownloadProgram(ipAddress, port, item, out result);
                    if (result)
                    {
                        hashSubPrograms.Add(item, prgText);
                        //find second level sub programs
                        subProgramsCNCTemp.AddRange(FindSubPrograms(prgText));
                    }
                    //else
                    //    return -1;
                }
            }

            //download second level sub programs
            if (subProgramsCNCTemp.Count > 0)
            {
                //download sub programs starts with M98P
                foreach (var item in subProgramsCNCTemp.Distinct())
                {
                    Logger.WriteDebugLog("Downloading second level sub program : " + item);
                    string prgText = FocasData.DownloadProgram(ipAddress, port, item, out result);
                    if (result)
                    {
                        if (!hashSubPrograms.ContainsKey(item))
                        {
                            hashSubPrograms.Add(item, prgText);
                        }
                    }
                    //else
                    //    return -1;
                }
            }

            //compare main, sub program with Master folder program --> if not same, store the main and sub program with date time
            //O1234_yyyymmddhhmm.txt , O1234_567_yyyymmddhhmm.txt, O1234_678_yyyymmddhhmm.txt           
            CreateDirectory(_programDownloadFolder);

            //compaire the containt of main and sub program from "Master" folder under machine folder??
            string masterProgramFolderPath = Path.Combine(_programDownloadFolder, "MasterPrograms", "O" + programNumber + mainProgramCNCComment);
            string autoDownloadedProgramPath = Path.Combine(_programDownloadFolder, "AutoDownloadedPrograms", DateTime.Now.ToString("dd-MMM-yyyy"));


            string masterProgramPath = Path.Combine(masterProgramFolderPath, "O" + programNumber + mainProgramCNCComment + ".txt");
            string autoDownloadMainProgramPath = Path.Combine(autoDownloadedProgramPath, "O" + programNumber + mainProgramCNCComment, "O" + programNumber + mainProgramCNCComment + DateTime.Now.ToString("_yyyyMMddHHmm") + ".txt");

            if (Directory.Exists(masterProgramFolderPath) && File.Exists(masterProgramPath))
            {
                mainProgramMasterStr = ReadFileContent(masterProgramPath);
                //compare main CNC and main master program
                bool isMainProgramsSame = CompareContents(mainProgramCNCStr, mainProgramMasterStr);
                if (isMainProgramsSame == false)
                {
                    //save programs to autodownload folder
                    if (_AutoDownloadEveryTimeIfNotSameAsMaster)
                    {
                        Logger.WriteDebugLog("Main program not same, saving to autodownload folder");
                        CreateDirectory(Path.GetDirectoryName(autoDownloadMainProgramPath));
                        WriteFileContent(autoDownloadMainProgramPath, mainProgramCNCStr);
                        //TODO - downloaded program not same as Master Program - Log to MessageHistory Table
                        messageForSMS.AppendLine(" O" + programNumber + mainProgramCNCComment);
                        messageForSMS.AppendLine("File : " + Path.GetFileNameWithoutExtension(autoDownloadMainProgramPath));

                    }
                    else
                    {
                        //check the previous saved program
                        if (_AutoDownloadedSavedPrograms.ContainsKey(programNumber))
                        {
                            isMainProgramsSame = CompareContents(mainProgramCNCStr, _AutoDownloadedSavedPrograms[programNumber].ToString());
                            if (isMainProgramsSame == false)
                            {
                                Logger.WriteDebugLog("Main program not same with previous version of autodownload programs, saving to autodownload folder");
                                CreateDirectory(Path.GetDirectoryName(autoDownloadMainProgramPath));
                                WriteFileContent(autoDownloadMainProgramPath, mainProgramCNCStr);
                                _AutoDownloadedSavedPrograms[programNumber] = mainProgramCNCStr;
                                //TODO - downloaded program not same as Master Program - Log to MessageHistory Table
                                messageForSMS.AppendLine(" O" + programNumber + mainProgramCNCComment);
                                messageForSMS.AppendLine("File : " + Path.GetFileNameWithoutExtension(autoDownloadMainProgramPath));
                            }
                        }
                        else
                        {
                            Logger.WriteDebugLog("Main program not same with previous version of autodownload programs, saving to autodownload folder");
                            CreateDirectory(Path.GetDirectoryName(autoDownloadMainProgramPath));
                            _AutoDownloadedSavedPrograms.Add(programNumber, mainProgramCNCStr);
                            WriteFileContent(autoDownloadMainProgramPath, mainProgramCNCStr);
                            //TODO - downloaded program not same as Master Program - Log to MessageHistory Table
                            messageForSMS.AppendLine(" O" + programNumber + mainProgramCNCComment);
                            messageForSMS.AppendLine("File : " + Path.GetFileNameWithoutExtension(autoDownloadMainProgramPath));
                        }
                    }
                }
                foreach (int item in hashSubPrograms.Keys)
                {
                    string subProgramMasterFile = Path.Combine(masterProgramFolderPath, "O" + programNumber + mainProgramCNCComment + "_O" + item + ".txt");
                    string subProgramAutoDownloadFile = Path.Combine(autoDownloadedProgramPath, "O" + programNumber + mainProgramCNCComment, "O" + programNumber + mainProgramCNCComment + "_O" + item + DateTime.Now.ToString("_yyyyMMddHHmm") + ".txt");
                    if (File.Exists(subProgramMasterFile))
                    {
                        //compaire programs, if not match save it
                        bool isSubProgramSame = CompareContents(ReadFileContent(subProgramMasterFile), hashSubPrograms[item].ToString());
                        if (isSubProgramSame == false)
                        {
                            //check the previous saved program
                            if (_AutoDownloadEveryTimeIfNotSameAsMaster)
                            {
                                CreateDirectory(Path.GetDirectoryName(subProgramAutoDownloadFile));
                                WriteFileContent(subProgramAutoDownloadFile, hashSubPrograms[item].ToString());
                                //TODO - downloaded program not same as Master Program - Log to MessageHistory Table
                                messageForSMS.AppendLine(" " + Path.GetFileNameWithoutExtension(subProgramMasterFile));
                                messageForSMS.AppendLine("File : " + Path.GetFileNameWithoutExtension(subProgramAutoDownloadFile));
                            }
                            else
                            {
                                //check the previous saved program
                                if (_AutoDownloadedSavedPrograms.ContainsKey(item))
                                {
                                    isSubProgramSame = CompareContents(hashSubPrograms[item].ToString(), _AutoDownloadedSavedPrograms[item].ToString());
                                    if (isSubProgramSame == false)
                                    {
                                        CreateDirectory(Path.GetDirectoryName(subProgramAutoDownloadFile));
                                        WriteFileContent(subProgramAutoDownloadFile, hashSubPrograms[item].ToString());
                                        _AutoDownloadedSavedPrograms[item] = hashSubPrograms[item].ToString();
                                        //TODO - downloaded program not same as Master Program - Log to MessageHistory Table
                                        messageForSMS.AppendLine(" " + Path.GetFileNameWithoutExtension(subProgramMasterFile));
                                        messageForSMS.AppendLine("File : " + Path.GetFileNameWithoutExtension(subProgramAutoDownloadFile));
                                    }
                                }
                                else
                                {
                                    CreateDirectory(Path.GetDirectoryName(subProgramAutoDownloadFile));
                                    _AutoDownloadedSavedPrograms.Add(item, hashSubPrograms[item].ToString());
                                    WriteFileContent(subProgramAutoDownloadFile, hashSubPrograms[item].ToString());
                                    //TODO - downloaded program not same as Master Program - Log to MessageHistory Table
                                    messageForSMS.AppendLine(" " + Path.GetFileNameWithoutExtension(subProgramMasterFile));
                                    messageForSMS.AppendLine("File : " + Path.GetFileNameWithoutExtension(subProgramAutoDownloadFile));
                                }
                            }
                        }
                    }
                    else
                    {
                        //save to Master folder
                        Logger.WriteDebugLog("Master sub program created for : " + item);
                        WriteFileContent(subProgramMasterFile, hashSubPrograms[item].ToString());
                    }
                }
            }
            else
            {
                //main program not exists, save all programs to master folder  
                Logger.WriteDebugLog(string.Format("Main program {0} not exists, save master and sub programs to master folder", programNumber));
                CreateDirectory(masterProgramFolderPath);
                //write the programs to folder if containt not same...
                masterProgramPath = Path.Combine(masterProgramFolderPath, "O" + programNumber + mainProgramCNCComment + ".txt");
                WriteFileContent(masterProgramPath, mainProgramCNCStr);
                foreach (int item in hashSubPrograms.Keys)
                {
                    string subProgramMasterFile = Path.Combine(masterProgramFolderPath, "O" + programNumber + mainProgramCNCComment + "_O" + item + ".txt");
                    WriteFileContent(subProgramMasterFile, hashSubPrograms[item].ToString());
                }
            }
            if (enableSMSforProgramChange && messageForSMS.Length > 0)
            {
                //messageForSMS.Insert(0, "Program Change Alert : " + this.machineId);
                Logger.WriteDebugLog(messageForSMS.ToString());
                DatabaseAccess.InsertAlertNotificationHistory(this.machineId, messageForSMS.ToString());
            }

            return 0;
        }

        private int DownloadProgramDASCNC(string machineId, string ipAddress, ushort port, string programNumber)
        {
            //download running program in temp folder
            bool result = false;
            int programNo = 0;
            programNumber = programNumber.TrimStart(new char[] { 'O' }).Trim();
            int.TryParse(programNumber, out programNo);
            if (programNo == 0) return 0;

            //TODO - get program full path
            string programFolderPath = string.Empty;
            string programFolderFullPath_Path2 = string.Empty;
            string programFolderFullPath = FocasData.ReadFullProgramPathRunningProgram(ipAddress, port);
            if (!string.IsNullOrEmpty(programFolderFullPath))
            {
                programFolderFullPath_Path2 = programFolderFullPath.Replace("PATH1", "PATH2");
                programFolderPath = Directory.GetParent(programFolderFullPath).Name;
            }
            bool isProgramFolderSupports = string.IsNullOrEmpty(programFolderFullPath) ? false : true;
            string programFolderPath_PATH2 = "PATH2";


            string mainProgramMasterStr = string.Empty;
            StringBuilder messageForSMS = new StringBuilder();
            Logger.WriteDebugLog("Downloading main program : O" + programNo);
            string mainProgramCNCStr = FocasData.DownloadProgram(ipAddress, port, programNo, out result, programFolderFullPath, isProgramFolderSupports);
            if (!result) return -1;
            Hashtable hashSubPrograms = new Hashtable();
            //check if main program contains sub peogram(contains with M98P)
            string mainProgramCNCComment = FindProgramComment(mainProgramCNCStr);
            //List<int> subProgramsCNC = FindSubPrograms(mainProgramCNCStr);

            List<int> subProgramsCNC = FindSubProgramsDASCNC(mainProgramCNCStr);
            List<int> subProgramsCNCTemp = new List<int>();
            if (subProgramsCNC.Count > 0)
            {
                //download sub programs starts with M98P
                foreach (var item in subProgramsCNC)
                {
                    Logger.WriteDebugLog("Downloading first level sub program : " + item);//TODO - PATH2
                    string prgText = FocasData.DownloadProgram(ipAddress, port, item, out result, programFolderFullPath_Path2, isProgramFolderSupports);
                    if (result)
                    {
                        hashSubPrograms.Add(item, prgText);//TODO - PATH2
                        //find second level sub programs
                        subProgramsCNCTemp.AddRange(FindSubProgramsDASCNC(prgText));
                    }
                    //else
                    //    return -1;
                }
            }

            //download second level sub programs
            if (subProgramsCNCTemp.Count > 0)
            {
                //download sub programs starts with M98P
                foreach (var item in subProgramsCNCTemp.Distinct())
                {
                    Logger.WriteDebugLog("Downloading second level sub program : " + item);//TODO - PATH2
                    string prgText = FocasData.DownloadProgram(ipAddress, port, item, out result, programFolderFullPath_Path2, isProgramFolderSupports);
                    if (result)
                    {
                        if (!hashSubPrograms.ContainsKey(item))//TODO - PATH2
                        {
                            hashSubPrograms.Add(item, prgText);//TODO - PATH2
                        }
                    }
                    //else
                    //    return -1;
                }
            }

            //compare main, sub program with Master folder program --> if not same, store the main and sub program with date time
            //O1234_yyyymmddhhmm.txt , O1234_567_yyyymmddhhmm.txt, O1234_678_yyyymmddhhmm.txt           
            CreateDirectory(_programDownloadFolder);



            //compaire the containt of main and sub program from "Master" folder under machine folder??
            string masterProgramFolderPath = Path.Combine(_programDownloadFolder, "MasterPrograms", "O" + programNumber + mainProgramCNCComment);
            string autoDownloadedProgramPath = Path.Combine(_programDownloadFolder, "AutoDownloadedPrograms", DateTime.Now.ToString("dd-MMM-yyyy"));


            string masterProgramPath = Path.Combine(masterProgramFolderPath, programFolderPath + "_O" + programNumber + mainProgramCNCComment + ".txt");
            string autoDownloadMainProgramPath = Path.Combine(autoDownloadedProgramPath, "O" + programNumber + mainProgramCNCComment, programFolderPath + "_O" + programNumber + mainProgramCNCComment + DateTime.Now.ToString("_yyyyMMddHHmm") + ".txt");

            if (Directory.Exists(masterProgramFolderPath) && File.Exists(masterProgramPath))
            {
                mainProgramMasterStr = ReadFileContent(masterProgramPath);
                //compare main CNC and main master program
                bool isMainProgramsSame = CompareContents(mainProgramCNCStr, mainProgramMasterStr);
                if (isMainProgramsSame == false)
                {
                    //save programs to autodownload folder
                    if (_AutoDownloadEveryTimeIfNotSameAsMaster)
                    {
                        Logger.WriteDebugLog("Main program not same, saving to autodownload folder");
                        CreateDirectory(Path.GetDirectoryName(autoDownloadMainProgramPath));
                        WriteFileContent(autoDownloadMainProgramPath, mainProgramCNCStr);
                        //TODO - downloaded program not same as Master Program - Log to MessageHistory Table
                        messageForSMS.AppendLine(programFolderPath + "_O" + programNumber + mainProgramCNCComment);
                        messageForSMS.AppendLine("File : " + Path.GetFileNameWithoutExtension(autoDownloadMainProgramPath));

                    }
                    else
                    {
                        //check the previous saved program
                        if (_AutoDownloadedSavedPrograms.ContainsKey(programFolderPath + programNumber))
                        {
                            isMainProgramsSame = CompareContents(mainProgramCNCStr, _AutoDownloadedSavedPrograms[programFolderPath + programNumber].ToString());
                            if (isMainProgramsSame == false)
                            {
                                Logger.WriteDebugLog("Main program not same with previous version of autodownload programs, saving to autodownload folder");
                                CreateDirectory(Path.GetDirectoryName(autoDownloadMainProgramPath));
                                WriteFileContent(autoDownloadMainProgramPath, mainProgramCNCStr);
                                _AutoDownloadedSavedPrograms[programFolderPath + programNumber] = mainProgramCNCStr;
                                //TODO - downloaded program not same as Master Program - Log to MessageHistory Table
                                messageForSMS.AppendLine(" " + programFolderPath + "_O" + programNumber + mainProgramCNCComment);
                                messageForSMS.AppendLine("File : " + Path.GetFileNameWithoutExtension(autoDownloadMainProgramPath));
                            }
                        }
                        else
                        {
                            Logger.WriteDebugLog("Main program not same with previous version of autodownload programs, saving to autodownload folder");
                            CreateDirectory(Path.GetDirectoryName(autoDownloadMainProgramPath));
                            _AutoDownloadedSavedPrograms.Add(programFolderPath + programNumber, mainProgramCNCStr);
                            WriteFileContent(autoDownloadMainProgramPath, mainProgramCNCStr);
                            //TODO - downloaded program not same as Master Program - Log to MessageHistory Table
                            messageForSMS.AppendLine(" " + programFolderPath + "_O" + programNumber + mainProgramCNCComment);
                            messageForSMS.AppendLine("File : " + Path.GetFileNameWithoutExtension(autoDownloadMainProgramPath));
                        }
                    }
                }
                foreach (int item in hashSubPrograms.Keys)
                {
                    string subProgramMasterFile = Path.Combine(masterProgramFolderPath, "PATH2_O" + programNumber + mainProgramCNCComment + "_O" + item + ".txt");
                    string subProgramAutoDownloadFile = Path.Combine(autoDownloadedProgramPath, "O" + programNumber + mainProgramCNCComment, "PATH2_O" + programNumber + mainProgramCNCComment + "_O" + item + DateTime.Now.ToString("_yyyyMMddHHmm") + ".txt");
                    if (File.Exists(subProgramMasterFile))
                    {
                        //compaire programs, if not match save it
                        bool isSubProgramSame = CompareContents(ReadFileContent(subProgramMasterFile), hashSubPrograms[item].ToString());
                        if (isSubProgramSame == false)
                        {
                            //check the previous saved program
                            if (_AutoDownloadEveryTimeIfNotSameAsMaster)
                            {
                                CreateDirectory(Path.GetDirectoryName(subProgramAutoDownloadFile));
                                WriteFileContent(subProgramAutoDownloadFile, hashSubPrograms[item].ToString());
                                //TODO - downloaded program not same as Master Program - Log to MessageHistory Table
                                messageForSMS.AppendLine(Path.GetFileNameWithoutExtension(subProgramMasterFile));
                                messageForSMS.AppendLine("File : " + Path.GetFileNameWithoutExtension(subProgramAutoDownloadFile));
                            }
                            else
                            {
                                //check the previous saved program
                                if (_AutoDownloadedSavedPrograms.ContainsKey("PATH2" + item))
                                {
                                    isSubProgramSame = CompareContents(hashSubPrograms[item].ToString(), _AutoDownloadedSavedPrograms["PATH2" + item].ToString());
                                    if (isSubProgramSame == false)
                                    {
                                        CreateDirectory(Path.GetDirectoryName(subProgramAutoDownloadFile));
                                        WriteFileContent(subProgramAutoDownloadFile, hashSubPrograms[item].ToString());
                                        _AutoDownloadedSavedPrograms["PATH2" + item] = hashSubPrograms[item].ToString();
                                        //TODO - downloaded program not same as Master Program - Log to MessageHistory Table
                                        messageForSMS.AppendLine(Path.GetFileNameWithoutExtension(subProgramMasterFile));
                                        messageForSMS.AppendLine("File : " + Path.GetFileNameWithoutExtension(subProgramAutoDownloadFile));
                                    }
                                }
                                else
                                {
                                    CreateDirectory(Path.GetDirectoryName(subProgramAutoDownloadFile));
                                    _AutoDownloadedSavedPrograms.Add("PATH2" + item, hashSubPrograms[item].ToString());
                                    WriteFileContent(subProgramAutoDownloadFile, hashSubPrograms[item].ToString());
                                    //TODO - downloaded program not same as Master Program - Log to MessageHistory Table
                                    messageForSMS.AppendLine(" " + Path.GetFileNameWithoutExtension(subProgramMasterFile));
                                    messageForSMS.AppendLine("File : " + Path.GetFileNameWithoutExtension(subProgramAutoDownloadFile));
                                }
                            }
                        }
                    }
                    else
                    {
                        //save to Master folder
                        Logger.WriteDebugLog("Master sub program created for : " + item);
                        WriteFileContent(subProgramMasterFile, hashSubPrograms[item].ToString());
                    }
                }
            }
            else
            {
                //main program not exists, save all programs to master folder  
                Logger.WriteDebugLog(string.Format("Main program {0} not exists, save master and sub programs to master folder", programNumber));
                CreateDirectory(masterProgramFolderPath);
                //write the programs to folder if containt not same...
                masterProgramPath = Path.Combine(masterProgramFolderPath, programFolderPath + "_O" + programNumber + mainProgramCNCComment + ".txt");
                WriteFileContent(masterProgramPath, mainProgramCNCStr);
                foreach (int item in hashSubPrograms.Keys)
                {
                    string subProgramMasterFile = Path.Combine(masterProgramFolderPath, "PATH2_O" + programNumber + mainProgramCNCComment + "_O" + item + ".txt");
                    WriteFileContent(subProgramMasterFile, hashSubPrograms[item].ToString());
                }
            }
            if (enableSMSforProgramChange && messageForSMS.Length > 0)
            {
                //messageForSMS.Insert(0, "Program Change Alert : " + this.machineId);
                Logger.WriteDebugLog("Message For SMS = " + messageForSMS.ToString());
                DatabaseAccess.InsertAlertNotificationHistory(this.machineId, messageForSMS.ToString());
            }

            return 0;
        }

        private static List<int> FindSubPrograms(string programText)
        {
            List<int> programs = new List<int>();
            if (programText.Contains("M98P"))
            {
                string[] lines = programText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                //parse the file to findout sub-programs
                foreach (var line in lines)
                {
                    if (line.Contains("M98P"))
                    {
                        string prg = line.Remove(0, line.IndexOf("M98P") + 4);
                        Regex rgx = new Regex("[a-zA-Z ]"); //Regex.Replace(prg,"[^0-9 ]","");                       
                        prg = rgx.Replace(prg, "");
                        int p;
                        if (Int32.TryParse(prg, out p))
                        {
                            if (!programs.Contains(p))
                            {
                                programs.Add(p);
                            }
                        }
                    }
                }
            }
            return programs;
        }

        private static List<int> FindSubProgramsDASCNC(string programText)
        {
            List<int> programs = new List<int>();
            if (programText.Contains("M90"))
            {
                string[] lines = programText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                //parse the file to findout sub-programs
                foreach (var line in lines)
                {
                    if (line.Contains("M90"))
                    {
                        string prg = line.Remove(0, line.IndexOf("M90") + 3);
                        //Regex rgx = new Regex("[a-zA-Z )]"); //Regex.Replace(prg,"[^0-9 ]","");      
                        Regex rgx = new Regex("[^0-9 ]");
                        prg = rgx.Replace(prg, "");
                        int p;
                        if (Int32.TryParse(prg, out p))
                        {
                            if (!programs.Contains(p))
                            {
                                programs.Add(p);
                            }
                        }
                    }
                }
            }
            return programs;
        }
        private static string FindProgramComment(string programText)
        {
            string comment = "(";
            string[] lines = programText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines.ToList().Take(2))
            {
                if (line.Contains("(") && line.Contains(")"))
                {
                    comment += line.Substring(line.IndexOf("(") + 1, line.IndexOf(")") - line.IndexOf("(") - 1);
                    break;
                }
            }
            return Utility.SafeFileName(comment + ")");
        }

        private static string FindProgramNumberAndComment(string programText, out int programNumber)
        {
            string comment = "(";
            programNumber = 0;
            string[] lines = programText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines.ToList().Take(2))
            {
                if (line.Contains("O"))
                {
                    string prog = line;
                    if (line.Contains("("))
                    {
                        prog = prog.Substring(prog.IndexOf("O") + 1, prog.IndexOf("(") - 1);
                    }
                    else
                    {
                        Regex rgx = new Regex("[a-zA-Z() ]"); //Regex.Replace(prg,"[^0-9 ]","");                       
                        prog = rgx.Replace(prog, "");
                    }
                    int p;
                    if (Int32.TryParse(prog, out p))
                    {
                        programNumber = p;
                    }
                    break;
                }
            }
            foreach (var line in lines.ToList().Take(2))
            {
                if (line.Contains("(") && line.Contains(")"))
                {
                    comment += line.Substring(line.IndexOf("(") + 1, line.IndexOf(")") - line.IndexOf("(") - 1);
                    break;
                }
            }
            return Utility.SafeFileName(comment + ")");
        }


        private static bool CompareContents(string str1, string str2)
        {
            if (str1.Equals(str2, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static string ReadFileContent(string filePath)
        {
            try
            {
                return File.ReadAllText((filePath));
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString());
            }

            return string.Empty;
        }

        private static bool WriteFileContent(string filePath, string str)
        {
            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(filePath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                }
                File.WriteAllText((filePath), str);
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString());
            }

            return false;
        }

        public static string SafePathName(string name)
        {
            StringBuilder str = new StringBuilder(name);

            foreach (char c in System.IO.Path.GetInvalidPathChars())
            {
                str = str.Replace(c, '_');
            }
            return str.ToString();
        }

        public static bool CreateDirectory(string masterProgramFolderPath)
        {
            var safeMasterProgramFolderPath = SafePathName(masterProgramFolderPath);
            if (!Directory.Exists(safeMasterProgramFolderPath))
            {
                try
                {
                    Directory.CreateDirectory(safeMasterProgramFolderPath);
                }
                catch (Exception ex)
                {
                    Logger.WriteErrorLog(ex.ToString());
                    return false;
                }
            }
            return true;
        }

        private int get_alarm_type(int n)
        {
            int i, res = 0;
            for (i = 0; i < 32; i++)
            {
                int n1 = n;

                res = (int)(n1 & (1 << i));
                if (res != 0)
                {
                    return (i);
                }
            }
            if (i == 32)
            {
                return -1;
            }
            return -1;
        }

        public void GetAlarmsData(Object stateObject)
        {
            if (!_isLicenseValid) return;
            if (Monitor.TryEnter(_lockerAlarmHistory, 1000 * 10))
            {
                Ping ping = default(Ping);
                try
                {
                    System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                    Thread.CurrentThread.Name = "AlarmsHistory-" + Utility.SafeFileName(this.machineId);
                    ping = new Ping();
                    PingReply reply = ping.Send(ipAddress, 10000);
                    if (reply.Status == IPStatus.Success)
                    {
                        CheckMachineType();
                        Logger.WriteDebugLog("Reading Alarms History data for control type." + _cncMachineType.ToString());
                        if (_cncMachineType == CncMachineType.cncUnknown) return;
                        DataTable dt = default(DataTable);
                        if (_cncMachineType == CncMachineType.Series300i ||
                            _cncMachineType == CncMachineType.Series310i ||
                            _cncMachineType == CncMachineType.Series320i ||
                            _cncMachineType == CncMachineType.Series0i)
                        {
                            dt = FocasData.ReadAlarmHistory(machineId, ipAddress, portNo);
                        }
                        else
                        {
                            //oimc,210i
                            dt = FocasData.ReadAlarmHistory18i(machineId, ipAddress, portNo);
                        }
                        DatabaseAccess.InsertAlarms(dt, machineId);
                        Logger.WriteDebugLog("Completed reading Alarms History data.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteDebugLog(ex.ToString());
                }
                finally
                {
                    Monitor.Exit(_lockerAlarmHistory);
                    if (ping != null) ping.Dispose();
                }
            }

        }

        public void GetAlarmsDataforEndTimeUpdate()
        {
            Ping ping = default(Ping);
            try
            {
                ping = new Ping();
                PingReply reply = ping.Send(ipAddress, 10000);
                if (reply.Status == IPStatus.Success)
                {
                    CheckMachineType();
                    Logger.WriteDebugLog("Reading Alarms History data to update the ALARM END TIME for control type." + _cncMachineType.ToString());
                    if (_cncMachineType == CncMachineType.cncUnknown) return;
                    DataTable dt = default(DataTable);
                    if (_cncMachineType == CncMachineType.Series300i ||
                        _cncMachineType == CncMachineType.Series310i ||
                        _cncMachineType == CncMachineType.Series320i ||
                        _cncMachineType == CncMachineType.Series0i)
                    {
                        dt = FocasData.ReadAlarmHistory(machineId, ipAddress, portNo);
                    }
                    else
                    {
                        //oimc,210i
                        dt = FocasData.ReadAlarmHistory18i(machineId, ipAddress, portNo);
                    }
                    DatabaseAccess.InsertAlarms(dt, machineId);
                    Logger.WriteDebugLog("Completed reading Alarms History data.");
                }
            }
            catch (Exception ex)
            {
                Logger.WriteDebugLog(ex.ToString());
            }
            finally
            {
                if (ping != null) ping.Dispose();
            }

        }

        public void GetOperationHistoryData(Object stateObject)
        {
            if (!_isLicenseValid) return;
            if (Monitor.TryEnter(_lockerOperationHistory, 1000 * 60))
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                Ping ping = default(Ping);
                try
                {
                    Thread.CurrentThread.Name = "OperationHistory-" + Utility.SafeFileName(this.machineId);
                    ping = new Ping();
                    PingReply reply = ping.Send(ipAddress, 10000);
                    if (reply.Status == IPStatus.Success)
                    {
                        Logger.WriteDebugLog("Reading Operation History data for control type." + _cncMachineType.ToString());
                        string FilePath = Path.Combine(_operationHistoryFolderPath, this.machineId, DateTime.Now.ToString("yyyy-MM-dd"));
                        if (!Directory.Exists(FilePath))
                        {
                            try
                            {
                                Directory.CreateDirectory(FilePath);
                            }
                            catch { }
                        }
                        string fileName = Path.Combine(FilePath, DateTime.Now.ToString("yyyyMMddHHmmss") + ".txt");
                        short OperationHistoryFlagLocation = 0;
                        short dprint_flagLocation = 0;
                        int dprint_flagValue = 0;
                        if (!short.TryParse(ConfigurationManager.AppSettings["OperationHistory_FlagLocation"].ToString(), out OperationHistoryFlagLocation))
                        {
                            OperationHistoryFlagLocation = 0;
                        }
                        if (!short.TryParse(ConfigurationManager.AppSettings["DPRINT_FlagLocation"].ToString(), out dprint_flagLocation))
                        {
                            dprint_flagLocation = 0;
                        }


                        try
                        {
                            if (OperationHistoryFlagLocation > 0)
                            {
                                FocasData.UpdateOperatinHistoryMacroLocation(this.ipAddress, this.portNo, OperationHistoryFlagLocation, 1);
                            }
                            if (dprint_flagLocation > 0)
                            {
                                dprint_flagValue = FocasData.ReadOperatinHistoryDPrintLocation(this.ipAddress, this.portNo, dprint_flagLocation);
                            }
                            if (dprint_flagValue == 0)
                            {
                                FocasData.DownloadOperationHistory(this.ipAddress, this.portNo, fileName);
                            }
                            if (OperationHistoryFlagLocation > 0)
                            {
                                FocasData.UpdateOperatinHistoryMacroLocation(this.ipAddress, this.portNo, OperationHistoryFlagLocation, 0);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteErrorLog(ex.ToString());
                        }
                    }
                    else
                    {
                        Logger.WriteDebugLog("Not able to ping to machine =" + machineId + ". Ping Stats = " + reply.Status.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteDebugLog(ex.ToString());
                }
                finally
                {
                    Monitor.Exit(_lockerOperationHistory);
                    if (ping != null) ping.Dispose();
                }
            }
        }


        public void GetTPMTrakStringData(object stateObject)
        {
            if (!_isLicenseValid) return;
            if (Monitor.TryEnter(this._lockerTPMTrakDataCollection, 4000))
            {
                ushort focasLibHandle = 0;
                string text = string.Empty;
                Ping ping = null;
                try
                {
                    Thread.CurrentThread.Name = "TPMTrakDataCollation-" + this.machineId;
                    ping = new Ping();
                    PingReply pingReply = ping.Send(this.ipAddress, 10000);
                    if (pingReply.Status == IPStatus.Success)
                    {
                        int ret = (int)FocasData.cnc_allclibhndl3(this.ipAddress, this.portNo, 10, out focasLibHandle);
                        if (ret == 0)
                        {
                            //string mode = FocasData.ReadMachineMode(focasLibHandle);
                            //if (mode.Equals("MEM", StringComparison.OrdinalIgnoreCase))
                            {
                                List<TPMString> list = new List<TPMString>();
                                foreach (TPMMacroLocation current in this.setting.TPMDataMacroLocations)
                                {
                                    int isDataReadyToread = FocasData.ReadMacro(focasLibHandle, current.StatusMacro);
                                    if (isDataReadyToread > 0)
                                    {
                                        List<int> values = FocasData.ReadMacroRange(focasLibHandle, current.StartLocation, current.EndLocation);
                                        TPMString tPMString = new TPMString();
                                        tPMString.Seq = values[0];
                                        text = this.BuildString(values);
                                        tPMString.TpmString = text;
                                        this.SaveStringToTPMFile(text);
                                        //tPMString.DateTime = this.GetDatetimeFromtpmString(values);

                                        list.Add(tPMString);
                                        FocasData.WriteMacro(focasLibHandle, current.StatusMacro, 0);
                                    }
                                }
                                foreach (TPMString current2 in list.OrderBy(s => s.Seq))
                                {
                                    this.ProcessData(current2.TpmString, this.ipAddress, this.portNo.ToString(), this.machineId);
                                }
                            }
                        }
                        else
                        {
                            Logger.WriteErrorLog("Not able to connect to CNC machine. ret value from fun cnc_allclibhndl3 = " + ret);
                        }
                    }
                    else
                    {
                        Logger.WriteErrorLog("Not able to ping. Ping status = " + pingReply.Status.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteDebugLog(ex.ToString());
                }
                finally
                {
                    if (focasLibHandle != 0)
                    {
                        var r = FocasData.cnc_freelibhndl(focasLibHandle);
                        if (r != 0) _focasHandles.Add(focasLibHandle);
                    }
                    if (ping != null)
                    {
                        ping.Dispose();
                    }
                    Monitor.Exit(this._lockerTPMTrakDataCollection);

                }
            }
        }

        public void GetMachineParameterData_MGTL(object stateObject)
        {
            if (!_isLicenseValid) return;
            if (Monitor.TryEnter(_lockerMachineParamer_MGTL, 1000 * 10))
            {
                Ping ping = default(Ping);
                try
                {
                    Thread.CurrentThread.Name = "MachineParameterData_MGTL-" + Utility.SafeFileName(this.machineId);
                    ping = new Ping();
                    PingReply reply = ping.Send(ipAddress, 10000);
                    if (reply.Status == IPStatus.Success)
                    {
                        ReadMachineParameterData_MGTL(this.machineId, this.ipAddress, this.portNo);
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteDebugLog(ex.ToString());
                }
                finally
                {
                    Monitor.Exit(_lockerMachineParamer_MGTL);
                    if (ping != null) ping.Dispose();
                }
            }
        }
      
        public void GetProcessParameterData_FOF(Object stateObject)
        {
            if (!_isLicenseValid) return;
            if (Monitor.TryEnter(_lockerProcessParameter_FOF, 10000))
            {
                try
                {
                    System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                    Thread.CurrentThread.Name = "ProcessParameterData_FOF-" + Utility.SafeFileName(this.machineId);

                    if (Utility.CheckPingStatus(this.ipAddress))
                    {
                        ReadProcessParameterData_FOF(this.machineId, this.ipAddress, this.portNo);
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteDebugLog(ex.ToString());
                }
                finally
                {
                    Monitor.Exit(_lockerProcessParameter_FOF);
                }
            }
        }

        private void ReadProcessParameterData_FOF(string machineId, string ipAddress, ushort portNo)
        {
            ushort focasLibHandle = 0;
            try
            {
                int ret = 0;
                ret = FocasData.cnc_allclibhndl3(ipAddress, portNo, 10, out focasLibHandle);
                if (ret != 0)
                {
                    Logger.WriteErrorLog("ReadProcessParameterData => cnc_allclibhndl3() failed. return value is = " + ret);
                    Thread.Sleep(1000);
                    return;
                }

                var CNCTimeStamp = FocasData.ReadCNCTimeStamp(focasLibHandle);

                if (this.setting.ProcessParameterSettings.Count == 0)
                {
                    Logger.WriteDebugLog(string.Format("master data not found in \"[ProcessParameterMaster_MGTL]\" table for input parameters"));
                }

                List<ProcessParameterDTO> inputs = new List<ProcessParameterDTO>();
                foreach (var item in this.setting.ProcessParameterSettings)
                {
                    if (item.RLocation <= 0) continue;

                    var result = FocasData.ReadPMCOneWord(focasLibHandle, 5, item.RLocation, (ushort)(item.RLocation + 2));
                    if (result != short.MinValue)
                    {
                        ProcessParameterDTO obj = new ProcessParameterDTO()
                        {
                            UpdatedtimeStamp = CNCTimeStamp,
                            MachineID = this.machineId,
                            ParameterID = item.ParameterID,
                            ParameterBitValue = result
                        };
                        inputs.Add(obj);
                    }
                }
                DatabaseAccess.InsertBulkRows(inputs.ToDataTable<ProcessParameterDTO>(), "[dbo].[ProcessParameterTransaction_MGTL]");
                Logger.WriteDebugLog("Completed reading ProcessParameter data.");               
            }
            catch (Exception exx)
            {
                Logger.WriteErrorLog(exx.ToString());
            }
            finally
            {
                FocasData.cnc_freelibhndl(focasLibHandle);
            }
        }

        public void GetCycleTimeData(object stateObject)
        {
            if (!_isLicenseValid) return;
            if (Monitor.TryEnter(this._lockerCycletimeReader, 1000))
            {
                ushort focasLibHandle = 0;
                string text = string.Empty;
                Ping ping = null;
                try
                {
                    Thread.CurrentThread.Name = "TPMTrakCycleTimeDataCollation-" + this.machineId;
                    ping = new Ping();
                    PingReply pingReply = ping.Send(this.ipAddress, 10000);
                    if (pingReply.Status == IPStatus.Success)
                    {
                        int ret = (int)FocasData.cnc_allclibhndl3(this.ipAddress, this.portNo, 10, out focasLibHandle);
                        if (ret == 0)
                        {

                            int isDataReadyToread = FocasData.ReadMacro(focasLibHandle, (short)(cycleTimeMacroLocation + 5));
                            if (isDataReadyToread > 0)
                            {
                                List<int> values = FocasData.ReadMacroRange(focasLibHandle, cycleTimeMacroLocation, (short)(cycleTimeMacroLocation + 4));
                                FocasData.WriteMacro(focasLibHandle, (short)(cycleTimeMacroLocation + 5), 0);
                                DatabaseAccess.InsertCycleTimeData(values, this.machineId);
                            }
                        }
                        else
                        {
                            Logger.WriteErrorLog("Not able to connect to CNC machine. ret value from fun cnc_allclibhndl3 = " + ret);
                        }
                    }
                    else
                    {
                        Logger.WriteErrorLog("Not able to ping. Ping status = " + pingReply.Status.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteDebugLog(ex.ToString());
                }
                finally
                {
                    if (focasLibHandle != 0)
                    {
                        var r = FocasData.cnc_freelibhndl(focasLibHandle);
                        if (r != 0) _focasHandles.Add(focasLibHandle);
                    }
                    if (ping != null)
                    {
                        ping.Dispose();
                    }
                    Monitor.Exit(this._lockerCycletimeReader);

                }
            }
        }

        private DateTime GetDatetimeFromtpmString(List<int> values)
        {
            string[] formats = new string[]
			{
				"yyyyMMdd HHmmss"
			};
            DateTime minValue = DateTime.MinValue;
            var date = values[values.Count - 2];
            var time = values[values.Count - 1];
            if (!DateTime.TryParseExact(date + " " + time, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out minValue))
            {
                string strDate = Utility.get_actual_date(date);
                string strTime = Utility.get_actual_time(time);
                DateTime.TryParse(strDate + " " + strTime, out minValue);
            }
            return minValue;
        }

        private string BuildString(List<int> values)
        {
            return string.Format("START-{0}-{1}-{2}-{3}-{4}-{5}-{6}-{7}-{8}-{9}-END-{10}", values[1], values[2],
                                            values[3], values[4], values[5], values[6], values[7], values[8], values[9], values[10], values[0]);
        }

        private string BuildInspection37String(string mc, string comp, string opn, SPCCharacteristics spc, DateTime cncTime)
        {
            //START-37-MC-COMP-OPRN-Featureid-DIMENSIONid-<VALUE>-DATE-TIME-END 
            return string.Format("START-37-{0}-{1}-{2}-{3}-{4}-@{5}/-{6}-{7}-END", mc, comp, opn, spc.FeatureID, spc.DiamentionId, spc.DiamentionValue, cncTime.ToString("yyyyMMdd"), cncTime.ToString("HHmmss"));
        }

        private void SaveStringToTPMFile(string str)
        {
            string progTime = String.Format("_{0:yyyyMMdd}", DateTime.Now);

            StreamWriter writer = default(StreamWriter);
            try
            {
                writer = new StreamWriter(appPath + "\\TPMFiles\\F-" + Utility.SafeFileName(Thread.CurrentThread.Name + progTime) + ".tpm", true);
                writer.WriteLine(str);
                writer.Flush();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.Message);
            }
            finally
            {
                if (writer != null)
                {
                    writer.Close();
                    writer.Dispose();
                }
            }
        }

        public void WriteInToFileDBInsert(string str)
        {
            string progTime = String.Format("_{0:yyyyMMdd}", DateTime.Now);
            string location = appPath + "\\Logs\\DBInsert-" + Utility.SafeFileName(MName + progTime) + ".txt";

            StreamWriter writer = default(StreamWriter);
            try
            {
                writer = new StreamWriter(location, true, Encoding.Default, 8195);
                writer.WriteLine(str);
                writer.Flush();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.Message);
            }
            finally
            {
                if (writer != null)
                {
                    writer.Close();
                    writer.Dispose();
                }
            }
        }

        public void ProcessData(string InputStr, string IP, string PortNo, string MName)
        {
            try
            {
                string ValidString = FilterInvalids(InputStr);
                WriteInToFileDBInsert(string.Format("{0} : Start Insert Record - {1} ; IP = {2}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:FFF"), ValidString, IP));
                InsertDataUsingSP(ValidString, IP, PortNo);
                WriteInToFileDBInsert(string.Format("{0} : Stop Insert - {1}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:FFF"), IP));
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog("ProcessFile() :" + ex.ToString());
            }
            return;
        }

        public static string FilterInvalids(string DataString)
        {
            string FilterString = string.Empty;
            try
            {
                for (int i = 0; i < DataString.Length; i++)
                {
                    byte[] asciiBytes = Encoding.ASCII.GetBytes(DataString.Substring(i, 1));

                    if (asciiBytes[0] >= Encoding.ASCII.GetBytes("#")[0] && asciiBytes[0] <= Encoding.ASCII.GetBytes("}")[0])  //to handle STR   -1-0111-000000001-1-0002-1-20110713-175258914-20110713-175847898-END more than 2 spaces in string
                    {
                        FilterString = FilterString + DataString.Substring(i, 1);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.Message);
            }
            return FilterString;
        }

        public static int InsertDataUsingSP(string DataString, string IP, string PortNo)
        {
            SqlConnection Con = ConnectionManager.GetConnection();
            SqlCommand cmd = new SqlCommand("s_GetProcessDataString", Con);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add("@datastring", SqlDbType.NVarChar).Value = DataString;
            cmd.Parameters.Add("@IpAddress", SqlDbType.NVarChar).Value = IP;
            cmd.Parameters.Add("@OutputPara", SqlDbType.Int).Value = 0;
            cmd.Parameters.Add("@LogicalPortNo", SqlDbType.SmallInt).Value = PortNo;
            int OutPut = 0;
            try
            {
                OutPut = cmd.ExecuteNonQuery();
                if (OutPut < 0)
                {
                    Logger.WriteErrorLog(string.Format("InsertDataUsingSP() - ExecuteNonQuery returns < 0 value : {0} :- {1}", IP, DataString));
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog("InsertDataUsingSP():" + ex.Message);
            }
            finally
            {
                if (Con != null) Con.Close();
                cmd = null;
                Con = null;
            }
            return OutPut;
        }

        public void CloseTimer()
        {
            if (_timerAlarmHistory != null) _timerAlarmHistory.Dispose();
            if (_timerOperationHistory != null) _timerOperationHistory.Dispose();
            if (_timerSpindleLoadSpeed != null) _timerSpindleLoadSpeed.Dispose();
            if (_timerPredictiveMaintenanceReader != null) _timerPredictiveMaintenanceReader.Dispose();
            if (this._timerTPMTrakDataCollection != null) this._timerTPMTrakDataCollection.Dispose();
            if (this._timerOffsetHistoryReader != null) this._timerOffsetHistoryReader.Dispose();
            if (this._timerCycletimeReader != null) this._timerCycletimeReader.Dispose();
            if (_timerToolLife != null) this._timerToolLife.Dispose();

            if (_timerProcessParameter != null) this._timerProcessParameter.Dispose();
            if (_timerProcessParameter_FOF != null) this._timerProcessParameter_FOF.Dispose();
            if (_timerProcessParameter_BAJAJ != null) _timerProcessParameter_BAJAJ.Dispose();
            if (_timerGrindingCyclemonitoring_BAJAJ != null) _timerGrindingCyclemonitoring_BAJAJ.Dispose();
        }

        public void CheckMachineType()
        {
            if (_cncSeries.Equals(string.Empty))
            {
                ushort focasLibHandle = ushort.MinValue;
                short ret = FocasData.cnc_allclibhndl3(ipAddress, portNo, 4, out focasLibHandle);
                if (ret == 0)
                {
                    if (FocasData.GetFanucMachineType(focasLibHandle, ref _cncMachineType, out _cncSeries) != 0)
                    {
                        Logger.WriteErrorLog("Failed to get system info. method failed cnc_sysinfo()");
                    }
                    Logger.WriteDebugLog("CNC control type  = " + _cncMachineType.ToString() + " , " + _cncSeries);
                }
                ret = FocasData.cnc_freelibhndl(focasLibHandle);
                if (ret != 0) _focasHandles.Add(focasLibHandle);
            }
        }

        public short WriteWearOffsetToCNC(decimal value, short offsetLocation, ushort focasLibHandle)
        {
            try
            {
                return FocasData.WriteWearOffset2(focasLibHandle, offsetLocation, value);

            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString());
            }
            return 0;
        }

        public double ReadWearOffsetFromCNC(short offsetLocation, ushort focasLibHandle)
        {
            double offsetValue = double.NaN;
            try
            {
                //wear offset read write( only 3 decimal places) cnc_wrtofs( h, tidx, 0, 8, offset ) ;
                offsetValue = FocasData.ReadWearOffset2(focasLibHandle, offsetLocation);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString());
            }
            return offsetValue;
        }

        public short WriteWearOffsetToCNC_TEST(decimal value, short offsetLocation, ushort focasLibHandle)
        {
            try
            {
                return FocasData.WriteWearOffset_TEST(focasLibHandle, offsetLocation, value);

            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString());
            }
            return 0;
        }

        public void GetSpindleLoadSpeedData(Object stateObject)
        {
            if (!_isLicenseValid) return;
            if (CycleStarted != 1 || !_ReadSpindleData) return;
            if (Monitor.TryEnter(_lockerSpindleLoadSpeed, 100))
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                Ping ping = default(Ping);
                try
                {
                    Thread.CurrentThread.Name = "SpindleLoadSpeed-" + this.machineId;
                    ping = new Ping();
                    PingReply reply = ping.Send(ipAddress, 10000);
                    if (reply.Status == IPStatus.Success)
                    {
                        CheckMachineType();
                        //Logger.WriteDebugLog("Reading Spindle Load Speed History data for control type." + _cncMachineType.ToString());
                        if (_cncMachineType == CncMachineType.cncUnknown) return;                      

                        //ProcessSpindleLoadSpeed(this.machineId, this.ipAddress, this.portNo, setting.SpeedLocationStart, setting.SpeedLocationEnd,setting.LoadLocationStart,setting.LoadLocationEnd);
                        ProcessSpindleLoadSpeed(this.machineId, this.ipAddress, this.portNo);
                       // Logger.WriteDebugLog("Completed reading Spindle Load Speed History data.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteDebugLog(ex.ToString());
                }
                finally
                {
                    if (ping != null) ping.Dispose();
                    Monitor.Exit(_lockerSpindleLoadSpeed);
                }
            }
        }

        public void GetSignalStatus(Object stateObject) // SAC
        {
            //if (!_isLicenseValid) return;
            if (Monitor.TryEnter(_lockerSignalStatus, 10000))
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                Ping ping = default(Ping);
                try
                {
                    Thread.CurrentThread.Name = "SignalStatus-" + this.machineId;
                    ping = new Ping();
                    PingReply reply = ping.Send(ipAddress, 10000);
                    if (reply.Status == IPStatus.Success)
                    {
                        CheckMachineType();
                        Logger.WriteDebugLog("Reading Signal Status for control type." + _cncMachineType.ToString());
                        if (_cncMachineType == CncMachineType.cncUnknown) return;
                        ProcessSignalStatus(this.machineId, this.ipAddress, this.portNo, RAddrStart, RAddrEnd, paramList);
                        Logger.WriteDebugLog("Completed reading Signal Status");
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteDebugLog(ex.ToString());
                }
                finally
                {
                    if (ping != null) ping.Dispose();
                    Monitor.Exit(_lockerSignalStatus);
                }
            }
        }

        private void ReadMachineParameterData_MGTL(string machineId, string ipAddress, ushort portNo)
        {
            try
            {
                int ret = 0;
                ushort focasLibHandle = 0;
                if (this.CycleStarted == 1 && this.grindApplicationFlag==1 ) // If cycle Started and Only Once In One Cycle
                {
                    this.grindApplicationFlag = 0;
                    ret = FocasData.cnc_allclibhndl3(ipAddress, portNo, 10, out focasLibHandle);
                    if (ret != 0)
                    {
                        Logger.WriteErrorLog("ReadProcessParameterData_SAC => cnc_allclibhndl3() failed. return value is = " + ret);
                        Thread.Sleep(1000);
                        return;
                    }

                    //Logger.WriteDebugLog(string.Format("Read spc data for Comp = {0} and Opn = {1}.", CompInterface, OpnInterface));
                    var CNCTimeStamp = FocasData.ReadCNCTimeStamp(focasLibHandle);

                    if (this.setting.ProcessParameterInputMacroLocation.Count == 0)
                    {
                        Logger.WriteDebugLog(string.Format("master data not found in \"Focas_SpindleProcessParameters\" table for input parameters"));
                    }
                    if (this.setting.ProcessParameterOutputMacroLocation.Count == 0)
                    {
                        Logger.WriteDebugLog(string.Format("master data not found in \"Focas_SpindleProcessParameters\" table for output parameters"));
                    }

                    List<ProcessParameterDTO_SAC> inputs = new List<ProcessParameterDTO_SAC>();
                    foreach (short item in this.setting.ProcessParameterInputMacroLocation)
                    {
                        if (item <= 0) continue;
                        double inspectionValue = FocasData.ReadMacroDouble2(focasLibHandle, item);
                        if (inspectionValue != double.MaxValue)
                        {
                            ProcessParameterDTO_SAC obj = new ProcessParameterDTO_SAC()
                            {
                                BatchTS = CNCTimeStamp,
                                MachineId = this.machineId,
                                MacroVariable = item.ToString(),
                                Type = "User defined",
                                Value = inspectionValue
                            };
                            inputs.Add(obj);
                        }
                    }
                    if(this.machineDTO.Settings.GrindingApplication_Bajaj!=null && this.machineDTO.Settings.GrindingApplication_Bajaj.Count > 0)
                    {
                        foreach(var grindingApplication in this.machineDTO.Settings.GrindingApplication_Bajaj)
                        {
                            ProcessParameterDTO_SAC obj = new ProcessParameterDTO_SAC()
                            {
                                BatchTS = CNCTimeStamp,
                                MachineId = this.machineId,
                                MacroVariable = "E" + grindingApplication.RLocation,
                                Type = "User defined",
                                Value = Convert.ToDouble(FocasData.ReadPMCOneWord(focasLibHandle, 12, ushort.Parse(grindingApplication.RLocation), (ushort)(ushort.Parse(grindingApplication.RLocation) + 2)))
                            };
                            inputs.Add(obj);
                        }
                    }
                    DatabaseAccess.InsertBulkRows(inputs.ToDataTable<ProcessParameterDTO_SAC>(), "[dbo].[Focas_SpindleProcessValues]");
                    
                    List<ProcessParameterDTO_SAC> outputs = new List<ProcessParameterDTO_SAC>();
                    foreach (short item in this.setting.ProcessParameterOutputMacroLocation)
                    {
                        if (item <= 0) continue;

                        double inspectionValue = FocasData.ReadMacroDouble2(focasLibHandle, item);
                        if (inspectionValue != double.MaxValue)
                        {
                            ProcessParameterDTO_SAC obj = new ProcessParameterDTO_SAC()
                            {
                                BatchTS = CNCTimeStamp,
                                MachineId = this.machineId,
                                MacroVariable = item.ToString(),
                                Type = "output",
                                Value = inspectionValue
                            };
                            outputs.Add(obj);
                        }
                    }
                    DatabaseAccess.InsertBulkRows(outputs.ToDataTable<ProcessParameterDTO_SAC>(), "[dbo].[Focas_SpindleProcessValues]");
                    Logger.WriteDebugLog("Completed reading ProcessParameter data.");


                    if (focasLibHandle > 0)
                    {
                        var r = FocasData.cnc_freelibhndl(focasLibHandle);
                        if (r != 0) _focasHandles.Add(focasLibHandle);
                    }
                }
                
            }
            catch (Exception exx)
            {
                Logger.WriteErrorLog(exx.ToString());
            }
        }

        public void GetSignalStatus2(Object stateObject) // SAC, grinding
        {
            //if (!_isLicenseValid) return;
            if (Monitor.TryEnter(_lockerSignalStatus2, 10000))
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                Ping ping = default(Ping);
                try
                {
                    Thread.CurrentThread.Name = "SignalStatus2-" + this.machineId;
                    ping = new Ping();
                    PingReply reply = ping.Send(ipAddress, 10000);
                    if (reply.Status == IPStatus.Success)
                    {
                        CheckMachineType();
                        Logger.WriteDebugLog("Reading Signal Status2 for control type." + _cncMachineType.ToString());
                        if (_cncMachineType == CncMachineType.cncUnknown) return;
                        if (IsCycleRunning())
                        {
                            ProcessSignalStatus(this.machineId, this.ipAddress, this.portNo, RAddrStart2, RAddrEnd2, paramList2);
                        }
                        Logger.WriteDebugLog("Completed reading Signal Status2");
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteDebugLog(ex.ToString());
                }
                finally
                {
                    if (ping != null) ping.Dispose();
                    Monitor.Exit(_lockerSignalStatus2);
                }
            }
        }

        private bool IsCycleRunning()
        {
            bool res = false;
            try
            {
                List<byte> lstR = null;
                int ret = 0;
                ushort focasLibHandle = 0;
                ret = FocasData.cnc_allclibhndl3(ipAddress, portNo, 10, out focasLibHandle);
                if (ret != 0)
                {
                    Logger.WriteErrorLog("cnc_allclibhndl3() failed. return value is = " + ret);
                    return res;
                }
                lstR = FocasData.ReadPMCRangeByte(focasLibHandle, 5, cycleStartMacroU, (ushort)(cycleStartMacroU + 1));
                byte val = lstR[0];
                if (((1 << cycleStartMacroBit) & val) != 0)
                {
                    res = true;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog("IsCycleRunning: " + ex.ToString());
                res = false;
            }
            return res;
        }

        private void ProcessSignalStatus(string machineid, string ipAddress, ushort portNo, ushort RAddrStart, ushort RAddrEnd, DataTable paramList) // SAC
        {
            try
            {
                List<byte> lstR = null;
                int ret = 0;
                ushort focasLibHandle = 0;

                ret = FocasData.cnc_allclibhndl3(ipAddress, portNo, 10, out focasLibHandle);
                if (ret != 0)
                {
                    Logger.WriteErrorLog("cnc_allclibhndl3() failed. return value is = " + ret);
                    Thread.Sleep(1000);
                    return;
                }

                lstR = FocasData.ReadPMCRangeByte(focasLibHandle, 5, RAddrStart, RAddrEnd);
                int offset = 0;

                foreach (DataRow row in paramList.Rows)
                {
                    try
                    {
                        offset = (int)float.Parse(row["RedBit"].ToString().Substring(1)) - RAddrStart;
                        string paramID = row["ParameterID"].ToString().Trim();
                        string s = row["RedBit"].ToString();
                        if (!s.Equals("") && (lstR[offset] & (1 << ushort.Parse(s.Substring(s.IndexOf(".") + 1).Trim()))) != 0)
                        {
                            DatabaseAccess.InsertSignalStatus(this.machineId, paramID, s, "RedBit");
                        }

                        s = row["Red1Bit"].ToString();
                        if (!s.Equals("") && (lstR[offset] & (1 << ushort.Parse(s.Substring(s.IndexOf(".") + 1).Trim()))) != 0)
                        {
                            DatabaseAccess.InsertSignalStatus(this.machineId, paramID, s, "Red1Bit");
                        }

                        s = row["GreenBit"].ToString();
                        if (!s.Equals("") && (lstR[offset] & (1 << ushort.Parse(s.Substring(s.IndexOf(".") + 1).Trim()))) != 0)
                        {
                            DatabaseAccess.InsertSignalStatus(this.machineId, paramID, s, "GreenBit");
                        }

                        s = row["YellowBit"].ToString();
                        if (!s.Equals("") && (lstR[offset] & (1 << ushort.Parse(s.Substring(s.IndexOf(".") + 1).Trim()))) != 0)
                        {
                            DatabaseAccess.InsertSignalStatus(this.machineId, paramID, s, "YellowBit");
                        }
                    }
                    catch (Exception exx)
                    {
                        // data in row not conforming
                    }
                }
                if (focasLibHandle > 0)
                {
                    var r = FocasData.cnc_freelibhndl(focasLibHandle);
                    if (r != 0) _focasHandles.Add(focasLibHandle);
                }
            }
            catch (Exception exx)
            {
                Logger.WriteErrorLog(exx.ToString());
            }
        }

        //private void ProcessSignalStatus(string machineid, string ipAddress, ushort portNo) // SAC
        //{
        //    try
        //    {
        //        List<byte> lstR = null;
        //        int ret = 0;
        //        ushort focasLibHandle = 0;

        //        ret = FocasData.cnc_allclibhndl3(ipAddress, portNo, 10, out focasLibHandle);
        //        if (ret != 0)
        //        {
        //            Logger.WriteErrorLog("cnc_allclibhndl3() failed. return value is = " + ret);
        //            Thread.Sleep(1000);
        //            return;
        //        }

        //        lstR = FocasData.ReadPMCRangeByte(focasLibHandle, 5, RAddrStart, RAddrEnd);
        //        int offset = 0;

        //        foreach(DataRow row in paramList.Rows)
        //        {
        //            offset = (int)float.Parse(row["RedBit"].ToString().Substring(1)) - RAddrStart;

        //            string paramID = row["ParameterID"].ToString().Trim();
        //            string s = row["RedBit"].ToString();
        //            if ((lstR[offset] & (1 << ushort.Parse(s.Substring(s.IndexOf(".") + 1).Trim()))) != 0)
        //            {
        //                DatabaseAccess.InsertSignalStatus(this.machineId, paramID, s);
        //            }
        //            s = row["GreenBit"].ToString();
        //            if ((lstR[offset] & (1 << ushort.Parse(s.Substring(s.IndexOf(".") + 1).Trim()))) != 0)
        //            {
        //                DatabaseAccess.InsertSignalStatus(this.machineId, paramID, s);
        //            }
        //            s = row["YellowBit"].ToString();
        //            if ((lstR[offset] & (1 << ushort.Parse(s.Substring(s.IndexOf(".") + 1).Trim()))) != 0)
        //            {
        //                DatabaseAccess.InsertSignalStatus(this.machineId, paramID, s);
        //            }
        //            s = row["Red1Bit"].ToString();
        //            if ((lstR[offset] & (1 << ushort.Parse(s.Substring(s.IndexOf(".") + 1).Trim()))) != 0)
        //            {
        //                DatabaseAccess.InsertSignalStatus(this.machineId, paramID, s);
        //            }
        //        }
        //        if (focasLibHandle > 0)
        //        {
        //            var r = FocasData.cnc_freelibhndl(focasLibHandle);
        //            if (r != 0) _focasHandles.Add(focasLibHandle);
        //        }
        //    }
        //    catch (Exception exx)
        //    {
        //        Logger.WriteErrorLog(exx.ToString());
        //    }
        //}

        List<ProcessParameterTransactionDTO_Bajaj> processParameterTransactions = new List<ProcessParameterTransactionDTO_Bajaj>();
        private void ProcessSpindleLoadSpeed(string machineId, string ipAddress, ushort portNo)
        {
            try
            {               

            int ret = 0;
                ushort focasLibHandle = 0;

                ret = FocasData.cnc_allclibhndl3(ipAddress, portNo, 10, out focasLibHandle);
                if (ret != 0)
                {
                    Logger.WriteErrorLog("cnc_allclibhndl3() failed. return value is = " + ret);
                    Thread.Sleep(1000);
                    return;
                }
                short AXIS = setting.SpindleAxisNumber == 0 ? (short)1 : setting.SpindleAxisNumber;                
                {
                    int j = 1;
                    
                    FocasLibBase.ODBDY2_1 dynamic_data = FocasData.cnc_rddynamic2(focasLibHandle);
                    var programNo = "O" + dynamic_data.prgmnum.ToString();                   
                    List<ServoLoad> load = FocasData.ReadServoMotorLoad(focasLibHandle, 5);
                    DateTime CNCTimeStamp = FocasData.ReadCNCTimeStamp(focasLibHandle);

                    var progFeedRate = this.machineDTO.Settings.LoadScreen_Bajaj.Where(x => x.ParameterID.Equals("ProgramFeedRate")).ToList().FirstOrDefault();
                    ProcessParameterTransactionDTO_Bajaj transactionDTO_Bajaj = null;
                    if (progFeedRate != null)
                    {
                        transactionDTO_Bajaj = new ProcessParameterTransactionDTO_Bajaj();
                        transactionDTO_Bajaj.MachineID = machineId;
                        transactionDTO_Bajaj.ParameterID = progFeedRate.ParameterID;
                        //transactionDTO_Bajaj.ParameterName = "Feed Rate";
                        transactionDTO_Bajaj.ParameterValue = FocasData.ReadModalA(focasLibHandle).ToString("000.000"); 
                        //FocasData.ReadMacroDouble(focasLibHandle, Convert.ToInt16(progFeedRate.RLocation)).ToString("00.000"); //dynamic_data.actf.ToString("000.0000");
                        transactionDTO_Bajaj.ProgramNo = programNo;                        
                        transactionDTO_Bajaj.UpdatedtimeStamp = CNCTimeStamp;
                        processParameterTransactions.Add(transactionDTO_Bajaj);
                    }

                    var feedrate = FocasData.ReadFeedRateDecimal(focasLibHandle, out ret);
                    if (feedrate > 0)
                    {
                        ProcessParameterTransactionDTO_Bajaj transaction = new ProcessParameterTransactionDTO_Bajaj();
                        transaction.MachineID = this.machineId;
                        transaction.ParameterID = "ActualFeedRate";
                        //transaction.ParameterName = grinding.ParameterName;
                        transaction.ParameterValue = feedrate.ToString("00.000"); // FocasData.ReadMacro(focasLibHandle, Convert.ToInt16(grinding.RLocation)).ToString("00.0000");
                        transaction.Qualifier = string.Empty;
                        transaction.UpdatedtimeStamp = CNCTimeStamp;
                        processParameterTransactions.Add(transaction);
                        //Logger.WriteDebugLog("Completed reading FeedRate data! FeedRate Value : " + feedrate);
                    }

                    var WheelspindleRPM = this.machineDTO.Settings.LoadScreen_Bajaj.Where(x => x.ParameterID.Equals("WheelSpindleRPM")).ToList().FirstOrDefault();
                    if (WheelspindleRPM != null)
                    {
                        transactionDTO_Bajaj = new ProcessParameterTransactionDTO_Bajaj();
                        transactionDTO_Bajaj.MachineID = machineId;
                        transactionDTO_Bajaj.ParameterID = WheelspindleRPM.ParameterID;
                        //transactionDTO_Bajaj.ParameterName = "Feed Rate";
                        transactionDTO_Bajaj.ParameterValue = Convert.ToDecimal(FocasData.ReadPMCOneWord(focasLibHandle, 12, ushort.Parse(WheelspindleRPM.RLocation), (ushort)(ushort.Parse(WheelspindleRPM.RLocation) + 2))).ToString("00.000"); //dynamic_data.actf.ToString("000.0000");
                        transactionDTO_Bajaj.ProgramNo = programNo;
                        transactionDTO_Bajaj.UpdatedtimeStamp = CNCTimeStamp;
                        processParameterTransactions.Add(transactionDTO_Bajaj);
                    }

                    var WheelMotorKW = this.machineDTO.Settings.LoadScreen_Bajaj.Where(x => x.ParameterID.Equals("WheelMotorKW")).ToList().FirstOrDefault();
                    if (WheelMotorKW != null)
                    {
                        transactionDTO_Bajaj = new ProcessParameterTransactionDTO_Bajaj();
                        transactionDTO_Bajaj.MachineID = machineId;
                        transactionDTO_Bajaj.ParameterID = WheelMotorKW.ParameterID;
                        //transactionDTO_Bajaj.ParameterName = "Feed Rate";
                        transactionDTO_Bajaj.ParameterValue = Convert.ToDecimal(FocasData.ReadPMCOneWord(focasLibHandle, 12, ushort.Parse(WheelMotorKW.RLocation), (ushort)(ushort.Parse(WheelMotorKW.RLocation) + 2))).ToString("00.000"); //dynamic_data.actf.ToString("000.0000");
                        transactionDTO_Bajaj.ProgramNo = programNo;
                        transactionDTO_Bajaj.UpdatedtimeStamp = CNCTimeStamp;
                        processParameterTransactions.Add(transactionDTO_Bajaj);
                    }
                    var cAxisSpeed = this.machineDTO.Settings.LoadScreen_Bajaj.Where(x => x.ParameterID.Equals("C-AxisSpeed")).ToList().FirstOrDefault();
                    if (cAxisSpeed != null)
                    {
                        transactionDTO_Bajaj = new ProcessParameterTransactionDTO_Bajaj();
                        transactionDTO_Bajaj.MachineID = machineId;
                        transactionDTO_Bajaj.ParameterID = cAxisSpeed.ParameterID;
                        transactionDTO_Bajaj.ParameterValue = FocasData.ReadPMCOneWord(focasLibHandle, 12, ushort.Parse(cAxisSpeed.RLocation), (ushort)(ushort.Parse(cAxisSpeed.RLocation) + 2)).ToString();
                        transactionDTO_Bajaj.ProgramNo = programNo;
                        transactionDTO_Bajaj.UpdatedtimeStamp = CNCTimeStamp;
                        processParameterTransactions.Add(transactionDTO_Bajaj);
                    }
                    

                    for (short i = 1; i <= AXIS; i++)
                    {
                        //var spindle = new SpindleSpeedLoadDTO();
                        //spindle.CNCTimeStamp = FocasData.ReadCNCTimeStamp(focasLibHandle);
                        //spindle.MachineId = machineId;
                        //spindle.SpindleLoad = load.Where(l => l.AxisNumber == i).Select(a => a.Load).FirstOrDefault();
                        //spindle.Temperature = FocasData.ReadServoMotorTemp(focasLibHandle, i);
                        //spindle.ProgramNo = programNo;
                        //spindle.ToolNo = toolNo;
                        //spindle.SpindleSpeed = dynamic_data.acts;
                        //spindle.FeedRate = dynamic_data.actf;
                        //spindle.SpindleTarque = 0;
                        //string axisName = load.Where(l => l.AxisNumber == i).Select(a => a.Axis).FirstOrDefault();
                        //spindle.AxisNo = axisName != string.Empty ? axisName : i.ToString();
                        //_spindleInfoQueue.Add(spindle);

                      
                        transactionDTO_Bajaj = new ProcessParameterTransactionDTO_Bajaj();
                        transactionDTO_Bajaj.MachineID = machineId;
                        transactionDTO_Bajaj.ParameterID = load.Where(l => l.AxisNumber == i).Select(a => a.Axis).FirstOrDefault() + "-AxisLoad";                      
                        transactionDTO_Bajaj.ParameterValue= load.Where(l => l.AxisNumber == i).Select(a => a.Load).FirstOrDefault().ToString("00.0000");
                        transactionDTO_Bajaj.ProgramNo = programNo;
                        transactionDTO_Bajaj.UpdatedtimeStamp = CNCTimeStamp;
                        processParameterTransactions.Add(transactionDTO_Bajaj);

                        transactionDTO_Bajaj = new ProcessParameterTransactionDTO_Bajaj();
                        transactionDTO_Bajaj.MachineID = machineId;
                        transactionDTO_Bajaj.ParameterID = load.Where(l => l.AxisNumber == i).Select(a => a.Axis).FirstOrDefault() + "-AxisTemp";                      
                        transactionDTO_Bajaj.ParameterValue = FocasData.ReadServoMotorTemp(focasLibHandle, i).ToString("00.0000");
                        transactionDTO_Bajaj.ProgramNo = programNo;
                        transactionDTO_Bajaj.UpdatedtimeStamp = CNCTimeStamp;
                        processParameterTransactions.Add(transactionDTO_Bajaj);
                    }
                    if (processParameterTransactions != null && processParameterTransactions.Count > 0)
                    {
                        MongoDatabaseAccess.InsertProcessParameterTransaction_BajajIoT(processParameterTransactions).Wait();
                        processParameterTransactions.Clear();
                    }

                    if (focasLibHandle > 0)
                    {
                        var r = FocasData.cnc_freelibhndl(focasLibHandle);
                        //if (r != 0) _focasHandles.Add(focasLibHandle);
                    }
                }

            }
            catch (Exception exx)
            {
                Logger.WriteErrorLog(exx.ToString());
            }
        }

        private void ReadInspectionData(string machineId, string ipAddress, ushort portNo)
        {
            try
            {
                int ret = 0;
                ushort focasLibHandle = 0;

                ret = FocasData.cnc_allclibhndl3(ipAddress, portNo, 10, out focasLibHandle);
                if (ret != 0)
                {
                    Logger.WriteErrorLog("cnc_allclibhndl3() failed during ReadInspectionData() . return value is = " + ret);
                    Thread.Sleep(1000);
                    return;
                }

                //check new inspection data has been ready to read.
                int isDataChanged = FocasData.ReadMacro(focasLibHandle, InspectionDataReadFlag);
                if (isDataChanged > 0)
                {
                    Logger.WriteDebugLog("Started reading Inspection data.");
                    int CompInterface = FocasData.ReadMacro(focasLibHandle, _CompMacroLocation);
                    int OpnInterface = FocasData.ReadMacro(focasLibHandle, _OpnMacroLocation);
                    Logger.WriteDebugLog(string.Format("Read spc data for Comp = {0} and Opn = {1}.", CompInterface, OpnInterface));
                    var CNCTimeStamp = FocasData.ReadCNCTimeStamp(focasLibHandle);
                    //read inspection value from macro location
                    var inspectionData = DatabaseAccess.GetSPC_CharacteristicsForMCO(this.interfaceId, CompInterface.ToString(), OpnInterface.ToString());
                    foreach (var item in inspectionData)
                    {
                        double inspectionValue = FocasData.ReadMacroDouble2(focasLibHandle, item.MacroLocation);
                        if (inspectionValue != double.MaxValue)
                        {
                            item.DiamentionValue = inspectionValue;
                        }
                    }

                    //build type 37 string and insert to database
                    foreach (var item in inspectionData)
                    {
                        if (item.DiamentionValue == double.MaxValue) continue;
                        var type37String = BuildInspection37String(this.interfaceId, CompInterface.ToString(), OpnInterface.ToString(), item, CNCTimeStamp);
                        SaveStringToTPMFile(type37String);
                        ProcessData(type37String, ipAddress, portNo.ToString(), machineId);
                    }

                    //update all macro location value to '0'
                    foreach (var item in inspectionData)
                    {
                        FocasData.WriteMacro(focasLibHandle, item.MacroLocation, 0);
                    }

                    //reset the data read flag to '0'
                    FocasData.WriteMacro(focasLibHandle, InspectionDataReadFlag, 0);
                    Logger.WriteDebugLog("Completed reading Inspection data.");
                }

                if (focasLibHandle > 0)
                {
                    var r = FocasData.cnc_freelibhndl(focasLibHandle);
                    if (r != 0) _focasHandles.Add(focasLibHandle);
                }
            }
            catch (Exception exx)
            {
                Logger.WriteErrorLog(exx.ToString());
            }
        }

        private void Test(ushort handle)
        {

            int ret = 0;
            ushort focasLibHandle = 0;

            /*MGTL****************************************
               short axisNo = 5;
            FocasLibBase.ODBSVLOAD aa = new FocasLibBase.ODBSVLOAD();
            ret = FocasLibrary.FocasLib.cnc_rdsvmeter(focasLibHandle, ref axisNo, aa);

            FocasLibBase.ODBSPEED b = new FocasLibBase.ODBSPEED();
            FocasLibBase.ODBACT a = new FocasLibBase.ODBACT();
            ret = FocasLibrary.FocasLib.cnc_rdspeed(focasLibHandle, -1, b);
            ret = FocasLibrary.FocasLib.cnc_actf(focasLibHandle, a);
            ***********************/


            ret = FocasData.cnc_allclibhndl3(ipAddress, portNo, 10, out focasLibHandle);

            //FocasLibBase.ODBPTIME oDBPTIME = new FocasLibBase.ODBPTIME();
            //FocasLibrary.FocasLib.cnc_rdproctime(focasLibHandle, oDBPTIME);

            //int a = 0;
            //short b = 0;
            //FocasLibBase.PRGDIRTM c = new FocasLibBase.PRGDIRTM();
            //FocasLibrary.FocasLib.cnc_rdprgdirtime(focasLibHandle, ref a, ref b, c);

            FocasLibrary.FocasLibBase.ODBSINFO sinfo = new FocasLibBase.ODBSINFO();
            ret = FocasLibrary.FocasLib.cnc_getsraminfo(focasLibHandle, sinfo);

            ret = FocasLibrary.FocasLib.cnc_sramgetstart(focasLibHandle, sinfo.info.info1.sramname);

            short a1 = 0;
            int length = sinfo.info.info1.sramsize;

            string fileName = sinfo.info.info1.fname1.Trim();
            BinaryWriter writer = new BinaryWriter(File.Open(fileName, FileMode.Create));
            while (true)
            {
                char[] buf = new char[length];
                int c1 = buf.Length;
                ret = FocasLibrary.FocasLib.cnc_sramget(focasLibHandle, out a1, buf, ref c1);
                if (ret == 0)
                    writer.Write(buf, 0, c1);
                if (a1 == 0) break; //|| ret != 0
            }
            if (writer != null)
            {
                writer.Close();
                writer.Dispose();
            }

            //File.WriteAllText(fileName, programStr.ToString());
            ret = FocasLibrary.FocasLib.cnc_sramgetend(focasLibHandle);


            int oo = 00;
            //this.ValidateMachineModel(this.machineId, this.ipAddress, this.portNo);


            //cnc_rdproctime  

            //cnc_rdprgdirtime

            ////insert data to datbase......
            //DataTable dt = this.machineDTO.Settings.PredictiveMaintenanceSettings.ToDataTable<PredictiveMaintenanceDTO>();
            //dt.Columns.Remove("TargetDLocation"); dt.Columns.Remove("CurrentValueDLocation");
            //Thread thread = new Thread(() =>
            //DatabaseAccess.InsertBulkRows(dt, "[dbo].[Focas_PredictiveMaintenance]"));
            //thread.Start();


            //LiveDTO dto = new LiveDTO 
            //                        {   
            //                            MachineID = this.machineId,
            //                            MachineStatus = "In Cycle",
            //                            MachineUpDownStatus = 1,
            //                            CNCTimeStamp = DateTime.Now,
            //                            MachineUpDownBatchTS = DateTime.Now,                                        
            //                            BatchTS = DateTime.Now

            //                        };
            //_liveDTOQueue.Add(dto);
            //DataTable dt = _liveDTOQueue.ToDataTable<LiveDTO>();
            //DatabaseAccess.InsertBulkRows(dt, "[dbo].[Focas_LiveData]");

            //var cool = new CoolentLubOilLevelDTO()
            //{
            //    CNCTimeStamp = DateTime.Now,
            //    MachineId = this.machineId,
            //    CoolentLevel = 3000,
            //    LubOilLevel = 2000,
            //    PrevCoolentLevel = _prevCoolentLevel,
            //    PrevLubOilLevel = _prevLubOilLevel
            //};

            //DatabaseAccess.InsertCoolentLubOilInfo(cool);
            //_prevCoolentLevel = cool.CoolentLevel;
            //_prevLubOilLevel = cool.LubOilLevel;           

            //FocasData.ReadAllPrograms(this.ipAddress, this.portNo);
            // FocasData.DownloadProgram(this.ipAddress, this.portNo,9999);
            //FocasData.UploadProgram(this.ipAddress, this.portNo, "");
            //FocasData.SearchPrograms(this.ipAddress, this.portNo, 9999);
            //FocasData.DeletePrograms(this.ipAddress, this.portNo, 9999);
            //FocasData.DownloadOperationHistory(this.ipAddress, this.portNo, 9999);
            //Thread.Sleep(1000 * 60);
            //return;

            ////////1. - read external operator messages and insert to database
            //////List<OprMessageDTO> oprMessages = FocasData.ReadExternalOperatorMessageHistory0i(this.machineId, this.ipAddress, this.portNo);            
            //////DataTable dataTable = oprMessages.ToDataTable<OprMessageDTO>();
            //////DatabaseAccess.InsertBulkRows(dataTable, "[dbo].[Focas_ExOperatorMessageTemp]");
            //////DatabaseAccess.ProcessExOperatorMessageToHistory(machineId);
            //////DatabaseAccess.DeleteExOperatorMessageTempRecords(machineId);

            ////////2.- Read D-parameter table value - used to read 10 memory location for speed, Load
            ////////database field - id, machine id, cnc time stamp, speed, load
            ////////read every 1 second

            //////DateTime CNCTimeStamp = FocasData.ReadCNCTimeStamp(handle);
            //////List<int> speedList = FocasData.ReadPMCDataTableInt(handle, 1500, 1539);
            //////List<int> loadList = FocasData.ReadPMCDataTableInt(handle, 1600, 1639);
            //////List<SpindleSpeedLoad> speedLoad = new List<SpindleSpeedLoad>();
            //////for (int i = 0; i < speedList.Count; i++)
            //////{
            //////    var spindleInfo = new SpindleSpeedLoad { CNCTimeStamp = CNCTimeStamp, MachineId = this.machineId, SpindleSpeed = speedList[i]};
            //////    if (i < loadList.Count) spindleInfo.SpindleLoad = loadList[i];
            //////    speedLoad.Add(spindleInfo);
            //////}           
            //////DatabaseAccess.InsertBulkRows(speedLoad.ToDataTable<SpindleSpeedLoad>(), "[dbo].[Focas_SpindleInfo]");


            ////////3.- Read D-parameter table value - used to read 1 memory location for lub,coolent
            ////////database field - id, machine id, cnc time stamp, coolent level, lub oil level
            ////////read every 5 minutes
            //////DateTime CNCTimeStamp1 = FocasData.ReadCNCTimeStamp(handle);
            //////List<int> coolentLub = FocasData.ReadPMCDataTableInt(handle, 1500, 1508);
            //////var cool = new CoolentLubOilLevel() { CNCTimeStamp = CNCTimeStamp1, MachineId = this.machineId, CoolentLevel = coolentLub[0], LubOilLevel = coolentLub[1] };
            //////DatabaseAccess.InsertCoolentLubOilInfo(cool);

            ////////FocasData.ClearOperationHistory(handle);

            ////////4. - Read n(12-12) macro memory location for tool target, Actual values and store to database. 
            ////////database fields - machine id, program number, component, operation, toolNo - macro location,target , actual,cnc time stamp, 
            ////////read every 5 minutes
            //////DateTime CNCTimeStamp2 = FocasData.ReadCNCTimeStamp(handle);
            //////int component = FocasData.ReadMacro(handle, 1234);
            //////int operation = FocasData.ReadMacro(handle, 1235);
            //////short programNo = FocasData.ReadMainProgram(handle);
            //////List<int> toolTarget = FocasData.ReadMacroRange(handle, 100, 120);
            //////List<int> toolActual = FocasData.ReadMacroRange(handle, 200, 220);
            //////List<ToolLife> toolife = new List<ToolLife>();
            //////for (int i = 0; i < toolTarget.Count; i++)
            //////{
            //////    var tool = new ToolLife() {CNCTimeStamp=CNCTimeStamp2,ComponentID=component,MachineID=machineId,OperationID=operation,ProgramNo=programNo,
            //////    ToolTraget=toolTarget[i],ToolNoLocation= 100 + (i * 4)};
            //////    if (i < toolActual.Count) tool.ToolActual = toolActual[i];                
            //////}
            //////DatabaseAccess.InsertBulkRows(toolife.ToDataTable<ToolLife>(), "[dbo].[Focas_ToolActTrg]");
            //////Thread.Sleep(1000);

            /*
            FocasData.ClearOperationHistory(handle);
            //Read PMC data table           
            FocasData.ReadPMCDataTable(handle);

            DateTime date;
            FocasData.GetCNCDate(handle, out date);
            TimeSpan time;
            FocasData.GetCNCTime(handle, out time);

            //set the date and time on CNC
            FocasData.SetCNCDate(handle, DateTime.Now.AddDays(1));
            FocasData.SetCNCTime(handle, DateTime.Now.AddHours(12));

            //reset the date & time
            FocasData.SetCNCDate(handle, DateTime.Now);
            FocasData.SetCNCTime(handle, DateTime.Now);
            */
            //Reads the external operator's message history data
            //List<OprMessageDTO> oprMessages = FocasData.ReadExternalOperatorMessageHistory300i(this.machineId, this.ipAddress, this.portNo);
            //DataTable dataTable = Utility.ToDataTable<OprMessageDTO>(oprMessages);
            //DataTable dataTable1 = Utility.ConvertToDataTable<OprMessageDTO>(oprMessages);
            //0i
            //FocasData.ReadExternalOperatorMessageHistory18i(this.machineId, this.ipAddress, this.portNo);

            //FocasData.ReadAllPrograms(handle);

            /*
            //Reads the contents of the operator's message in CNC
            FocasData.ReadPMCAlarmMessage(handle);
            FocasData.ReadPMCMessages(handle);
            //read operation history
            FocasData.ReadOperationhistory18i(this.machineId,this.ipAddress,this.portNo);
            * 
             */

            //List<ToolLifeDO> toolife = new List<ToolLifeDO>();
            //for (int i = 0; i < 4; i++)
            //{
            //    var tool = new ToolLifeDO()
            //    {
            //        CNCTimeStamp = DateTime.Now,
            //        ComponentID = "100" + i.ToString(),
            //        MachineID = "vantage",
            //        OperationID = "10",
            //        ProgramNo = 2222,
            //        ToolTarget = i,
            //        ToolNo = "T" + (i + 1),
            //        SpindleType = 1,
            //    };
            //    toolife.Add(tool);
            //    // if (i < toolActual.Count) tool.ToolActual = toolActual[i];
            //}
            //DatabaseAccess.InsertBulkRows(toolife.ToDataTable<ToolLifeDO>(), "[dbo].[Focas_ToolLife]");

            //FocasData.GetTPMTrakFlagStatus(_focasLibHandle);
            //var dd = FocasData.GetCoolantLubricantLevel(_focasLibHandle);
            //Logger.WriteDebugLog(FocasData.GetCoolantLubricantLevel(_focasLibHandle));




        }

    }
}

