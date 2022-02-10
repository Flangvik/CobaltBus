using CobaltBus.Models;
using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace CobaltBus.Handlers
{
    public class LiteDbHandler
    {

        public string DbName { get; set; }

        public LiteDatabase LiteDatabase { get; set; }
        public LiteDbHandler(string dbName)
        {
            DbName = dbName;

            LiteDatabase = new LiteDatabase(new ConnectionString() { Filename = DbName });
        }

        public void UpdateBeacon(Beacon beaconObject)
        {
            var collectionLink = LiteDatabase.GetCollection<Beacon>("beacons");
            collectionLink.Update(beaconObject);
            LiteDatabase.Checkpoint();

        }

        public void WriteBeacon(Beacon beaconObject)
        {
            var collectionLink = LiteDatabase.GetCollection<Beacon>("beacons");
            collectionLink.EnsureIndex(x => x.Id, true);
            collectionLink.Insert(beaconObject);
            LiteDatabase.Checkpoint();

        }

        public void WriteBeaconMsg(BeaconMsg beaconObject)
        {
           
            var collectionLink = LiteDatabase.GetCollection<BeaconMsg>("beaconmsg");
            collectionLink.Insert(beaconObject);
          
        }

        public bool DeleteBeaconMsg(BeaconMsg BeaconMsg)
        {
           
            var orders = LiteDatabase.GetCollection<BeaconMsg>("beaconmsg");
            if (orders.Delete(BeaconMsg.Id))
            {
               
                return true;
            }
            return false;
        }


        public List<BeaconMsg> QueryBeaconMsg()
        {
            try
            {
                Thread.Sleep(2000);
                var orders = LiteDatabase.GetCollection<BeaconMsg>("beaconmsg");
                return orders.FindAll().ToList();
            }
            catch (Exception ex)
            {
                //File is locked, ignore
                if (ex.Message.StartsWith("Invalid Collection on 0"))
                    return new List<BeaconMsg>() { };
                else
                    throw;
            }
        }


        public Beacon QueryBeacon(string beaconId)
        {
            var orders = LiteDatabase.GetCollection<Beacon>("beacons");
            return orders.Find(beacon => beacon.BeaconId.Equals(beaconId))?.FirstOrDefault();
        }

        public List<Beacon> QueryBeacons()
        {
            var orders = LiteDatabase.GetCollection<Beacon>("beacons");
            return orders.FindAll().ToList();
        }

    }
}
