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
using Newtonsoft.Json.Linq;

namespace FrankUtils
{
    public class Item
    {
        // Steam ID to send trade offer to
        public string sid { get; set; }
        // market_hash_name of the item
        public string id { get; set; }
        // user's trade url for getting the token
        public string tradeurl { get; set; }
        // value in USD of the skins
        public float value { get; set; }
        // # of that item that they want
        public int amt { get; set; }
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

        public TradeOfferUserHandler(Bot bot, SteamID sid) : base(bot, sid)
        {
            Connect_Socket();
            try
            {
                conn = new MySqlConnection(connection);
                cmd = new MySqlCommand();
                conn.Open();

                Log.Success("Successfully connected to MySQL database.");
                // Success
            }
            catch (MySqlException ex)
            {
                Log.Warn(ex.Message);
            }
        }

        // Receiving a trade offer
        public override void OnNewTradeOffer(TradeOffer offer)
        {
            var myItems = offer.Items.GetMyItems();
            var theirItems = offer.Items.GetTheirItems();

            if (myItems.Count > 0)
            {
                offer.Decline();
            }
            string tradeid;
            if (offer.Accept(out tradeid))
            {
                Bot.AcceptAllMobileTradeConfirmations();
                Log.Success("Accepted trade offer successfully : Trade ID: " + tradeid);

                // Success... Now credit the user in the database.
                try
                {
                    float credits = 0.00F;
                    using (var webClient = new System.Net.WebClient())
                    {
                        /* Backpack.TF JSON */
                        var json_string = webClient.DownloadString("http://backpack.tf/api/IGetMarketPrices/v1/?key=56cd0ca5b98d88be2ef9de16&appid=730");
                        JObject json_object = JObject.Parse(json_string);
                        /* User Inventory */
                        List<long> contextId = new List<long>();
                        contextId.Add(2);
                        GenericInventory theirSteamInventory = new GenericInventory(SteamWeb);
                        theirSteamInventory.load(730, contextId, OtherSID);
                        
                        /* 
                            3/1/16 - removed all skin value code... gotta re-do it
                        */

                        // Credit user in the database
                        cmd.Connection = conn;
                        cmd.CommandText = "UPDATE users SET credits = @number WHERE STEAMID64 = @text";
                        cmd.Prepare();
                        cmd.Parameters.AddWithValue("@number", credits);
                        cmd.Parameters.AddWithValue("@text", offer.PartnerSteamId.ConvertToUInt64());
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (MySqlException ex)
                {
                    Log.Warn(ex.Message);
                }
            }
        }
        public void SendTradeOffer(ulong sid, List<string> items)
        {
            SteamID playerSID = new SteamID(sid);
            var offer = Bot.NewTradeOffer(playerSID);
            List<long> contextId = new List<long>();
            contextId.Add(2);
            GenericInventory mySteamInventory = new GenericInventory(SteamWeb);
            mySteamInventory.load(730, contextId, Bot.SteamClient.SteamID);

            string lastItem = "";
            foreach (var x in mySteamInventory.items)
            {
                foreach (var y in items)
                {
                    if (mySteamInventory.getDescription(x.Value.assetid).market_hash_name == y)
                    {
                        if (y == lastItem) continue;
                        offer.Items.AddMyItem(730, 2, Convert.ToInt64(x.Value.assetid));
                        Console.WriteLine("Added " + y + " to the offer to SteamID " + playerSID);
                        lastItem = y;
                    }
                }
            }
            if (offer.Items.NewVersion)
            {
                string newOfferId;
                if (offer.Send(out newOfferId))
                {
                    Bot.AcceptAllMobileTradeConfirmations();
                    Log.Success("Trade offer sent : Offer ID " + newOfferId + " to SteamID " + playerSID);
                }
            }
        }
        public void Connect_Socket()
        {
            // Setup the connection to the server
            var socket = IO.Socket("http://localhost:8080");
            Log.Info("Connect_Socket() called!");
            socket.On(Socket.EVENT_CONNECT, () => { });

            // Listen for gamedata
            socket.On("gamedata", (data) =>
            {
                // First we must parse the JSON object 'data' and create a List
                string json = JsonConvert.SerializeObject(data);
                JavaScriptSerializer js = new JavaScriptSerializer();
                FrankUtils.Item[] json_items = js.Deserialize<FrankUtils.Item[]>(json);

                List<string> items = new List<string>();
                ulong steamid64 = Convert.ToUInt64(json_items[0].sid);  // json_items[0] will always be reserved for additional info such as t_hash, sid, and tradeurl
                string trade_url = json_items[0].tradeurl;
                string trade_token = trade_url.Split('=')[2];
                foreach (var i in json_items) { items.Add(i.id); }

                if (Bot.GetEscrowDuration(steamid64, trade_token).DaysTheirEscrow != 0)
                {
                    Log.Error("Could not send trade offer to SID " + steamid64 + ". Reason: Trade duration > 0");
                    return;
                }
                else
                {
                    // The trade will be instantaneous. Send the offer.
                    SendTradeOffer(steamid64, items);
                }
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
