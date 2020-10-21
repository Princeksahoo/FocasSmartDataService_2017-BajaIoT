using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using S7.Net;

namespace FocasSmartDataCollection
{
    class Profinet
    {

        internal static List<uint> GetDWords(Plc client, int dbnum, int startloc, int nValues)
        {
            List<UInt32> lst = new List<uint>();
            if (client == null)
            {
                Logger.WriteDebugLog("Client is null");
                return lst;
            }

            try
            {
                if (client.IsAvailable)
                {
                    if (!client.IsConnected)
                    {
                        ErrorCode er = client.Open();
                        if (er != ErrorCode.NoError)
                        {
                            Logger.WriteDebugLog("client open error");
                        }
                    }
                    if (client.IsConnected)
                    {
                        var data = client.Read(DataType.DataBlock, dbnum, startloc, VarType.DWord, nValues);
                        lst = ((uint[])data).ToList();
                    }
                }
            }
            catch(Exception ex)
            {
                client.Close();
                client.Dispose();
            }
            return lst;
        }

        internal static int GetDWord(Plc client, int dbnum, string counterLoc)
        {
            int val = 0;
            if (client == null)
            {
                Logger.WriteDebugLog("Client is null");
                return val;
            }

            try
            {
                if (client.IsAvailable)
                {
                    if (!client.IsConnected)
                    {
                        ErrorCode er = client.Open();
                        if (er != ErrorCode.NoError)
                        {
                            Logger.WriteDebugLog("client open error");
                        }
                    }
                    if (client.IsConnected)
                    {
                        var data = client.Read(string.Format("DB{0}.DBD{1}", dbnum, counterLoc));
                        val = (int)data;
                    }
                }
            }
            catch (Exception ex)
            {
                client.Close();
                client.Dispose();
            }
            return val;
        }

        internal static ushort GetWord(Plc client, int dbnum, string loc)
        {
            ushort val = 0;
            if (client == null)
            {
                Logger.WriteDebugLog("Client is null");
                return val;
            }

            try
            {
                if (client.IsAvailable)
                {
                    if (!client.IsConnected)
                    {
                        ErrorCode er = client.Open();
                        if (er != ErrorCode.NoError)
                        {
                            Logger.WriteDebugLog("client open error");
                        }
                    }
                    if (client.IsConnected)
                    {
                        var data = client.Read(string.Format("DB{0}.DBW{1}", dbnum, loc));
                        val = (ushort)data;
                    }
                }
            }
            catch (Exception ex)
            {
                client.Close();
                client.Dispose();
            }
            return val;
        }

        internal static string GetString(Plc client, int dbnum, string locCycle, int nBytes)
        {
            string val = "";
            int location = int.Parse(locCycle) + 2;
            if (client == null)
            {
                Logger.WriteDebugLog("Client is null");
                return val;
            }

            try
            {
                if (client.IsAvailable)
                {
                    if (!client.IsConnected)
                    {
                        ErrorCode er = client.Open();
                        if (er != ErrorCode.NoError)
                        {
                            Logger.WriteDebugLog("client open error");
                        }
                    }
                    if (client.IsConnected)
                    {
                        var data = client.Read(DataType.DataBlock, dbnum, location, VarType.String, nBytes);
                        val = (string)data;
                    }
                }
            }
            catch (Exception ex)
            {
                client.Close();
                client.Dispose();
            }
            return val;
        }

        internal static float GetFloat(Plc client, int dbnum, string loc)
        {
            uint val = 0;
            float ret = 0F;
            if (client == null)
            {
                Logger.WriteDebugLog("Client is null");
                return ret;
            }

            try
            {
                if (client.IsAvailable)
                {
                    if (!client.IsConnected)
                    {
                        ErrorCode er = client.Open();
                        if (er != ErrorCode.NoError)
                        {
                            Logger.WriteDebugLog("client open error");
                        }
                    }
                    if (client.IsConnected)
                    {
                        var data = client.Read(string.Format("DB{0}.DBD{1}", dbnum, loc));
                        val = (uint)data;
                        ret = BitConverter.ToSingle(BitConverter.GetBytes(val), 0);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString());
                client.Close();
                client.Dispose();
            }
            return ret;
        }

        internal static bool PutWord(Plc client, int dbnum, string loc, ushort val)
        {
            if (client == null)
            {
                Logger.WriteDebugLog("Client is null");
                return false;
            }

            try
            {
                if (client.IsAvailable)
                {
                    if (!client.IsConnected)
                    {
                        ErrorCode er = client.Open();
                        if (er != ErrorCode.NoError)
                        {
                            Logger.WriteDebugLog("client open error");
                        }
                    }
                    if (client.IsConnected)
                    {
                        ErrorCode er = client.Write(string.Format("DB{0}.DBW{1}", dbnum, loc), val);
                        return er == ErrorCode.NoError;
                    }
                }
            }
            catch (Exception ex)
            {
                client.Close();
                client.Dispose();
            }
            return false;
        }

        internal static bool PutBoolean(Plc client, int dbnum, string cycleCompletedLoc, bool val)
        {
            if (client == null)
            {
                Logger.WriteDebugLog("Client is null");
                return false;
            }

            try
            {
                if (client.IsAvailable)
                {
                    if (!client.IsConnected)
                    {
                        ErrorCode er = client.Open();
                        if (er != ErrorCode.NoError)
                        {
                            Logger.WriteDebugLog("client open error");
                        }
                    }
                    if (client.IsConnected)
                    {
                        ErrorCode er = client.Write(string.Format("DB{0}.DBX{1}", dbnum, cycleCompletedLoc), val);
                        return er == ErrorCode.NoError;
                    }
                }
            }
            catch (Exception ex)
            {
                client.Close();
                client.Dispose();
            }
            return false;
        }

        internal static bool GetBoolean(Plc client, int dbnum, string cycleCompletedLoc)
        {
            bool val = false;
            if (client == null)
            {
                Logger.WriteDebugLog("Client is null");
                return val;
            }

            try
            {
                if (client.IsAvailable)
                {
                    if (!client.IsConnected)
                    {
                        ErrorCode er = client.Open();
                        if (er != ErrorCode.NoError)
                        {
                            Logger.WriteDebugLog("client open error");
                        }
                    }
                    if (client.IsConnected)
                    {
                        var data = client.Read(string.Format("DB{0}.DBX{1}", dbnum, cycleCompletedLoc));
                        val = (bool)data;
                    }
                }
            }
            catch (Exception ex)
            {
                client.Close();
                client.Dispose();
            }
            return val;
        }
    }
}
