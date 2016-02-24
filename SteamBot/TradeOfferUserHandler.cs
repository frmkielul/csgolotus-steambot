using MySql.Data.MySqlClient;
using SteamKit2;
using SteamTrade;
using SteamTrade.TradeOffer;
using System;
using System.Collections.Generic;
using TradeAsset = SteamTrade.TradeOffer.TradeOffer.TradeStatusUser.TradeAsset;
using Quobject.SocketIoClientDotNet.Client;
using System.Web.Script.Serialization;
using Newtonsoft.Json;

namespace FrankUtils {
    public class Item {
        public string id { get; set; }
    }

}

namespace SteamBot
{
    public class TradeOfferUserHandler : UserHandler
    {
        /* --- Setup database connection to csgolotus --- */
        private string connection = "server=localhost;uid=csgolotus;" + "pwd=ufUL3e86NqUqjhV;database=csgolotus;";
        private MySqlConnection conn;
        private MySqlCommand cmd;
        private GenericInventory mySteamInventory;
        private GenericInventory OtherSteamInventory;

        public TradeOfferUserHandler(Bot bot, SteamID sid) : base(bot, sid)
        {
            try
            {
                conn = new MySqlConnection(connection);
                cmd = new MySqlCommand();
                // open the connection
                conn.Open();

                Log.Success("Successfully connected to MySQL database.");
                // Success
            }
            catch (MySqlException ex)
            {
                Log.Warn(ex.Message);
                // Could not connect
            }
            mySteamInventory = new GenericInventory(SteamWeb);
            OtherSteamInventory = new GenericInventory(SteamWeb);
        }

        public override void OnNewTradeOffer(TradeOffer offer)
        {
            //receiving a trade offer 
            //parse inventories of bot and other partner
            //either with webapi or generic inventory
            //Bot.GetInventory();
            //Bot.GetOtherInventory(OtherSID);

            var myItems = offer.Items.GetMyItems();
            var theirItems = offer.Items.GetTheirItems();
            Log.Info("They want " + myItems.Count + " of my items.");
            Log.Info("And I will get " + theirItems.Count + " of their items.");

            //do validation logic etc
            if (ValidateItems(myItems, theirItems))
            {
                string tradeid;
                if (offer.Accept(out tradeid))
                {
                    Bot.AcceptAllMobileTradeConfirmations();
                    Log.Success("Accepted trade offer successfully : Trade ID: " + tradeid);

                    // Success... Now credit the user in the database.
                    try
                    {
                        cmd.Connection = conn;

                        cmd.CommandText = "UPDATE users SET credits = @number WHERE STEAMID64 = @text";
                        cmd.Prepare();

                        cmd.Parameters.AddWithValue("@number", 1);
                        cmd.Parameters.AddWithValue("@text", offer.PartnerSteamId.ConvertToUInt64());

                        cmd.ExecuteNonQuery();

                        // TODO: -Test if the user has registered in the csgolotus database before accepting offer
                    } catch (MySqlException ex)
                    {
                        Log.Warn(ex.Message);
                    }
                }
            }
            else { }
        }
        public void SendTradeOffer(SteamID playerSID, List<int> items)
        {
            //creating a new trade offer using their steamid passed by conn_data
            var offer = Bot.NewTradeOffer(playerSID);

            // Add items as requested by the user on the site using conn_data
            //offer.Items.AddMyItem(0, 0, 0);

            if (offer.Items.NewVersion)
            {
                string newOfferId;
                if (offer.Send(out newOfferId))
                {
                    Bot.AcceptAllMobileTradeConfirmations();
                    Log.Success("Trade offer sent : Offer ID " + newOfferId + " to SteamID " + OtherSID);
                }
            }
        }
        public static void Connect_Socket()
        {
            var socket = IO.Socket("http://localhost:8080");
            Console.WriteLine("Connect_Socket() called!");
            socket.On(Socket.EVENT_CONNECT, () => { });

            socket.On("gamedata", (data) =>
            {
                Console.WriteLine("Data received from CSGOLotus: ");
                Console.WriteLine(data);

                // First we must parse the JSON object 'data' and create a List
                string json = JsonConvert.SerializeObject(data);
                Console.WriteLine(json);
                JavaScriptSerializer js = new JavaScriptSerializer();
                FrankUtils.Item[] items = js.Deserialize<FrankUtils.Item[]>(json);

                foreach (var i in items) {
                    Console.WriteLine(i.id);
                }

                // Then we will do a check to make sure the data is formatted properly, and the values are correct

                // After the check, we will call SendTradeOffer with the SteamID64 from the gamedata, and a List of items
            });
        }
        public override void OnMessage(string message, EChatEntryType type) { }

        public override bool OnGroupAdd() { return false; }

        public override bool OnFriendAdd() { return IsAdmin; }

        public override void OnFriendRemove() { }

        public override void OnLoginCompleted() { }

        public override bool OnTradeRequest() { return false; }

        public override void OnTradeError(string error) { }

        public override void OnTradeTimeout() { }

        public override void OnTradeSuccess() { }

        public override void OnTradeAwaitingConfirmation(long tradeOfferID) { }

        public override void OnTradeInit() { }

        public override void OnTradeAddItem(Schema.Item schemaItem, Inventory.Item inventoryItem) { }

        public override void OnTradeRemoveItem(Schema.Item schemaItem, Inventory.Item inventoryItem) { }

        public override void OnTradeMessage(string message) { }

        public override void OnTradeReady(bool ready) { }

        public override void OnTradeAccept() { }

        private bool ValidateItems(List<TradeAsset> myAssets, List<TradeAsset> theirAssets)
        {

            //       -Check current market value of all skins and set the @number parameter to ((valueOfSkins/0.03)*1000)
            //       - foreach skin in offer, compare to csgolounge schema and += to variable then send to db

            //compare items etc
            foreach (GenericInventory.Item item in mySteamInventory.items.Values)
            {
                if (mySteamInventory.getDescription(item.assetid).name == "Test")
                {
                    // Test will be replaced with each item that was sent from the server.

                    return true;
                }
            }
            return false;
        }
    }
}
