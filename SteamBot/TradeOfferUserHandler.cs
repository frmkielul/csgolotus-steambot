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
        public string sid { get; set; }
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
        public void SendTradeOffer(SteamID playerSID, List<string> items)
        {
            Console.WriteLine("SendTradeOffer() called!");
            /*creating a new trade offer using their steamid passed by conn_data
            var offer = Bot.NewTradeOffer(playerSID);

            // Add items as requested by the user on the site using conn_data
            // offer.Items.AddMyItem(0, 0, 0);

            if (offer.Items.NewVersion)
            {
                string newOfferId;
                if (offer.Send(out newOfferId))
                {
                    Bot.AcceptAllMobileTradeConfirmations();
                    Log.Success("Trade offer sent : Offer ID " + newOfferId + " to SteamID " + OtherSID);
                }
            }*/
        }
        public void Connect_Socket()
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
                JavaScriptSerializer js = new JavaScriptSerializer();
                FrankUtils.Item[] json_items = js.Deserialize<FrankUtils.Item[]>(json);

                List<string> items = new List<string>();
                string steamid64 = json_items[0].sid;
                foreach (var i in json_items) { items.Add(i.id); }

                // Call SendTradeOffer using items and steamid64
                SendTradeOffer(new SteamID(steamid64), items);
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

            List<long> contextId = new List<long>(); // if this doesn't work remove the 2 and add a line contextId.Add(2);
            contextId.Add(2);
            GenericInventory mySteamInventory = new GenericInventory(SteamWeb);
            mySteamInventory.load(730, contextId, Bot.SteamClient.SteamID);

            foreach (var i in mySteamInventory.items)
            {
                // Print the market hash name of each inventory item to the console
                // Example output: AK47 | Safari Mesh (Factory New)

                Console.WriteLine(mySteamInventory.getDescription(i.Value.assetid).market_hash_name);
            }

            /*
                
            */

            return false;
        }
    }
}
