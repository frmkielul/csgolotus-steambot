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
        // Steam ID to send trade offer to
        public string sid { get; set; }
        // market_hash_name of the item
        public string id { get; set; }
        // value in USD of the skins
        public float value { get; set; }
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
                        
                    // TODO: Get the value of the items from the node server
                    cmd.Parameters.AddWithValue("@number", 1);
                    cmd.Parameters.AddWithValue("@text", offer.PartnerSteamId.ConvertToUInt64());

                    cmd.ExecuteNonQuery();
                } catch (MySqlException ex)
                {
                    Log.Warn(ex.Message);
                }
            }
            
        }
        public void SendTradeOffer(ulong sid, List<string> items)
        {
            /*
                TODO 2/25/16:
                    - Fix the unhandled exception when initializing NewTradeOffer()
                    - Add a check to ensure that the trading partner has been secured by mobile authenticator for 1 week.
                    - Possibly create a massive database full of pregenerated keys, and add a field to the network data
                        then test if the key passed matches an unused key in the database. If not, cancel the trade because it was forged.
            */
            SteamID playerSID = new SteamID(sid);

            var offer = Bot.NewTradeOffer(playerSID);
            
            List<long> contextId = new List<long>(); // if this doesn't work remove the 2 and add a line contextId.Add(2);
            contextId.Add(2);
            GenericInventory mySteamInventory = new GenericInventory(SteamWeb);
            mySteamInventory.load(730, contextId, Bot.SteamClient.SteamID);

            foreach (var x in mySteamInventory.items)
            {
                foreach (var y in items)
                {
                    if (mySteamInventory.getDescription(x.Value.assetid).market_hash_name == y) {
                        offer.Items.AddMyItem(730, 2, Convert.ToUInt32(x.Value.assetid));
                        Console.WriteLine("Added " + y + " to the offer.");
                    }
                }
            }

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
                ulong steamid64 = Convert.ToUInt64(json_items[0].sid);
                foreach (var i in json_items) { items.Add(i.id); }

                // Call SendTradeOffer using items and steamid64
                SendTradeOffer(steamid64, items);
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
    }
}
