using DTO;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
//using MongoDB.Driver.Builders;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FocasSmartDataCollection
{
    public static class MongoDatabaseAccess
    {
        private readonly static IMongoClient _MongoClient = null;
        private readonly static IMongoDatabase _MongoDatabase;
        private static string mongoDBConnString = ConfigurationManager.AppSettings["MongodbConnectionString"].ToString();

        static MongoDatabaseAccess()
        {
            BsonSerializer.RegisterSerializer(typeof(DateTime), DateTimeSerializer.LocalInstance);
            _MongoClient = new MongoClient(mongoDBConnString);
            _MongoDatabase = _MongoClient.GetDatabase(ConfigurationManager.AppSettings["MongoDatabase"]);
        }

        public static bool CheckConnectionMongoDB()
        {
            try
            {
                IAsyncCursor<BsonDocument> doc = _MongoDatabase.ListCollections();
            }
            catch (Exception ex)
            {
                Logger.WriteDebugLog("Could not connect to MongoDB - " + ex.Message);
                return false;
            }
            return true;
        }

        public static async Task InsertProcessParameterTransaction_BajajIoT(List<ProcessParameterTransactionDTO_Bajaj> dt)
        {
            try
            {
                bool IsConnected = CheckConnectionMongoDB();
                if (IsConnected)
                {
                    var mongoCollection = _MongoDatabase.GetCollection <ProcessParameterTransactionDTO_Bajaj>("ProcessParameterTransaction_BajajIoT");
                    //_MongoDatabase.CreateCollection("ProcessParameterTransaction_BajajIoT");
                    //var collection=_MongoDatabase.GetCollection<BsonDocument>("ProcessParameterTransaction_BajajIoT");
                    await mongoCollection.InsertManyAsync(dt);
                    //foreach (ProcessParameterTransactionDTO_Bajaj row in dt)
                    //{                        
                    //    await mongoCollection.InsertOneAsync(row);                        
                    //}
                    //Logger.WriteDebugLog("Mongo Db ProcessParameterTransaction_BajajIoT Insertion Completed");
                }
                else
                {
                    Logger.WriteDebugLog("Failed To Connect MongoDB. Can not insert data to MongoDB");
                }

            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(string.Format("Exception in inserting recods to table {0} in method InsertProcessParameterTransaction_BajajIoT .", ex.ToString()));
            }
        }

        public static async Task DeleteFromMongoDBCollection(int monthsToKeepData)
        {
            try
            {
                bool IsConnected = CheckConnectionMongoDB();
                var mongoCollection = _MongoDatabase.GetCollection<ProcessParameterTransactionDTO_Bajaj>("ProcessParameterTransaction_BajajIoT");
                if (IsConnected)
                {
                    //var monthData = new date();
                    //monthData.setMonth(monthData.getMonth() - 3);
                    //mongoCollection.DeleteMany()({ UpdatedtimeStamp: {$lte: monthData} });
                    
                    DateTime dt = DateTime.Now.AddMonths(-monthsToKeepData);
                    await mongoCollection.DeleteManyAsync(Builders<ProcessParameterTransactionDTO_Bajaj>.Filter.Lte("UpdatedtimeStamp", dt));
                    Logger.WriteDebugLog("Deleted Data from MongoDB Collection");
                }
            }
            catch(Exception ex)
            {
                Logger.WriteDebugLog(ex.Message);
            }
        }
    }
}
